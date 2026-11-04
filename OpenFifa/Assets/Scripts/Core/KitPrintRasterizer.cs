using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// CPU rasterizer for the shirt print atlas. Produces an RGBA32 byte buffer
    /// (row 0 = bottom, ready for Texture2D.SetPixelData) encoded as data, not colour:
    ///   R = fill coverage (name/number ink)
    ///   G = fill + outline coverage
    ///   B = print "height" ramp near the edges (drives the heat-press bevel in the shader)
    ///   A = same as G
    /// Colours are applied in the shader, so changing number colours never needs a re-raster.
    /// Only glyph bounding boxes are touched; a full 1024x512 print costs a few milliseconds.
    /// Pure C#.
    /// </summary>
    public static class KitPrintRasterizer
    {
        /// <summary>Width of the edge height ramp in glyph grid units.</summary>
        public const float BevelUnits = 0.35f;

        /// <summary>Rasterizes a layout into a new buffer.</summary>
        public static byte[] Rasterize(KitPrintLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var buffer = new byte[layout.Width * layout.Height * 4];
            RasterizeInto(layout, buffer);
            return buffer;
        }

        /// <summary>Clears <paramref name="buffer"/> and rasterizes the layout into it (avoids reallocating).</summary>
        public static void RasterizeInto(KitPrintLayout layout, byte[] buffer)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (buffer == null || buffer.Length != layout.Width * layout.Height * 4)
                throw new ArgumentException("Buffer must be Width * Height * 4 bytes.", nameof(buffer));

            Array.Clear(buffer, 0, buffer.Length);

            var style = layout.Style;
            float halfStroke = KitGlyphFont.StrokeWidth(style) * 0.5f;
            float xScale = KitGlyphFont.HorizontalScale(style);
            bool square = KitGlyphFont.SquareCaps(style);
            float outline = layout.OutlineWidth;

            foreach (var placement in layout.Glyphs)
            {
                var glyph = KitGlyphFont.GetGlyph(placement.Character);
                if (glyph.Segments.Length == 0 || placement.Scale <= 0f) continue;

                var segments = ScaleSegments(glyph, xScale);
                float glyphCenterX = glyph.CenterX * xScale;
                float glyphCenterY = KitGlyphFont.GridHeight * 0.5f;
                float pixelUnits = 1f / placement.Scale;

                float cos = (float)Math.Cos(placement.Rotation);
                float sin = (float)Math.Sin(placement.Rotation);
                float radius = 0.5f * (float)Math.Sqrt(placement.InkWidth * placement.InkWidth
                    + placement.InkHeight * placement.InkHeight) + 2f;

                int x0 = Math.Max(0, (int)Math.Floor(placement.CenterX - radius));
                int x1 = Math.Min(layout.Width - 1, (int)Math.Ceiling(placement.CenterX + radius));
                int y0 = Math.Max(0, (int)Math.Floor(placement.CenterY - radius));
                int y1 = Math.Min(layout.Height - 1, (int)Math.Ceiling(placement.CenterY + radius));

                for (int y = y0; y <= y1; y++)
                {
                    float dy = y + 0.5f - placement.CenterY;
                    int row = y * layout.Width;
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - placement.CenterX;

                        // Rotate into glyph space (inverse of placement rotation), then to grid units.
                        float lx = dx * cos + dy * sin;
                        float ly = -dx * sin + dy * cos;
                        float ux = lx * pixelUnits + glyphCenterX;
                        float uy = ly * pixelUnits + glyphCenterY;

                        float d = DistanceToSegments(segments, ux, uy, square);
                        float edge = halfStroke + outline - d;
                        if (edge <= -pixelUnits) continue;

                        float fill = Clamp01((halfStroke - d) / pixelUnits + 0.5f);
                        float union = Clamp01(edge / pixelUnits + 0.5f);
                        float height = Clamp01(edge / BevelUnits);

                        int i = (row + x) * 4;
                        Max(buffer, i, fill);
                        Max(buffer, i + 1, union);
                        Max(buffer, i + 2, height);
                        Max(buffer, i + 3, union);
                    }
                }
            }
        }

        /// <summary>
        /// Distance from a point to the nearest stroke centre-line. Square caps use a
        /// Chebyshev-style metric along horizontal/vertical segments so stroke ends and
        /// corners are sharp (block sports lettering).
        /// </summary>
        public static float DistanceToSegments(KitGlyphSegment[] segments, float px, float py, bool squareCaps)
        {
            float best = float.MaxValue;
            for (int s = 0; s < segments.Length; s++)
            {
                float d = DistanceToSegment(segments[s], px, py, squareCaps);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Distance from a point to one segment (see <see cref="DistanceToSegments"/>).</summary>
        public static float DistanceToSegment(KitGlyphSegment seg, float px, float py, bool squareCaps)
        {
            float ax = px - seg.X0;
            float ay = py - seg.Y0;
            float vx = seg.X1 - seg.X0;
            float vy = seg.Y1 - seg.Y0;
            float len = (float)Math.Sqrt(vx * vx + vy * vy);

            if (len < 1e-5f)
            {
                return squareCaps
                    ? Math.Max(Math.Abs(ax), Math.Abs(ay))
                    : (float)Math.Sqrt(ax * ax + ay * ay);
            }

            float ux = vx / len;
            float uy = vy / len;
            float along = ax * ux + ay * uy;
            float perp = Math.Abs(ax * uy - ay * ux);

            float beyond = along < 0f ? -along : (along > len ? along - len : 0f);

            // Square terminals only on horizontal/vertical strokes; diagonal strokes get round
            // ends so joints (V, W, X, K...) do not grow rotated corner spikes.
            bool axisAligned = Math.Abs(ux) < 1e-3f || Math.Abs(uy) < 1e-3f;
            return squareCaps && axisAligned
                ? Math.Max(beyond, perp)
                : (float)Math.Sqrt(beyond * beyond + perp * perp);
        }

        private static KitGlyphSegment[] ScaleSegments(KitGlyph glyph, float xScale)
        {
            var result = new KitGlyphSegment[glyph.Segments.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var s = glyph.Segments[i];
                result[i] = new KitGlyphSegment(s.X0 * xScale, s.Y0, s.X1 * xScale, s.Y1);
            }
            return result;
        }

        private static void Max(byte[] buffer, int index, float value)
        {
            byte b = KitColorUtility.ToByte(value);
            if (b > buffer[index]) buffer[index] = b;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}
