using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Generates an original placeholder crest (shield with a top band and team initials)
    /// as an sRGB RGBA32 buffer, row 0 = bottom. Used until licensed crest art is supplied.
    /// Pure C#.
    /// </summary>
    public static class KitCrestRasterizer
    {
        private const float ShieldHalfWidth = 0.42f;
        private const float ShieldTop = 0.95f;
        private const float ShieldBottom = 0.03f;
        private const float ShieldShoulder = 0.52f;
        private const float BorderWidth = 0.055f;
        private const float ChiefBottom = 0.70f;

        /// <summary>Rasterizes a square crest texture.</summary>
        public static byte[] Rasterize(int size, SimpleColor field, SimpleColor band, SimpleColor trim,
            string initials, KitFontStyle style)
        {
            if (size < 8) throw new ArgumentOutOfRangeException(nameof(size));

            var buffer = new byte[size * size * 4];
            float px = 1f / size;

            string text = NameFormatter.FormatAndSanitize(initials ?? "");
            if (text.Length > 3) text = text.Substring(0, 3);

            float glyphScale = 0.26f / KitGlyphFont.InkHeight(style); // crest units per grid unit
            float halfStroke = KitGlyphFont.StrokeWidth(style) * 0.5f;
            float xs = KitGlyphFont.HorizontalScale(style);
            bool square = KitGlyphFont.SquareCaps(style);
            float textWidth = KitPrintLayoutLogic.MeasureUnits(text, style, KitPrintLayoutLogic.LetterGap, 0f) * glyphScale;
            float maxTextWidth = 0.56f;
            if (textWidth > maxTextWidth)
            {
                glyphScale *= maxTextWidth / textWidth;
                textWidth = maxTextWidth;
            }

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) * px;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) * px - 0.5f;
                    float sd = ShieldDistance(u, v);
                    float coverage = Clamp01(0.5f - sd / px);
                    if (coverage <= 0f) continue;

                    SimpleColor c = v > ChiefBottom ? band : field;

                    // Initials (lower field).
                    if (text.Length > 0)
                    {
                        float ink = TextInk(text, style, glyphScale, halfStroke, xs, square, u + textWidth * 0.5f, v - 0.30f, px);
                        c = Lerp(c, trim, ink);
                    }

                    // Border and chief divider in trim colour.
                    float border = Clamp01(0.5f - (-sd - BorderWidth) / px);
                    float divider = Clamp01(0.5f - (Math.Abs(v - ChiefBottom) - BorderWidth * 0.35f) / px);
                    c = Lerp(c, trim, Math.Max(border, divider));

                    int i = (y * size + x) * 4;
                    buffer[i] = KitColorUtility.ToByte(c.R);
                    buffer[i + 1] = KitColorUtility.ToByte(c.G);
                    buffer[i + 2] = KitColorUtility.ToByte(c.B);
                    buffer[i + 3] = KitColorUtility.ToByte(coverage);
                }
            }
            return buffer;
        }

        /// <summary>
        /// Approximate signed distance to the shield outline in crest units (negative inside).
        /// </summary>
        public static float ShieldDistance(float u, float v)
        {
            float halfWidth = v >= ShieldShoulder
                ? ShieldHalfWidth
                : ShieldHalfWidth * (float)Math.Sqrt(Math.Max(0f, (v - ShieldBottom) / (ShieldShoulder - ShieldBottom)));
            return Math.Max(Math.Abs(u) - halfWidth, Math.Max(v - ShieldTop, ShieldBottom - v));
        }

        private static float TextInk(string text, KitFontStyle style, float scale, float halfStroke, float xs,
            bool square, float localX, float localY, float px)
        {
            // localX: distance from the left edge of the text, localY: from the text baseline-bottom.
            float cursor = 0f;
            float pixelUnits = px / scale;
            for (int i = 0; i < text.Length; i++)
            {
                var glyph = KitGlyphFont.GetGlyph(text[i]);
                float ink = KitGlyphFont.InkWidth(glyph, style) * scale;
                if (glyph.Segments.Length > 0 && localX >= cursor - px && localX <= cursor + ink + px)
                {
                    float ux = (localX - cursor) / scale - halfStroke + glyph.MinX * xs;
                    float uy = localY / scale - halfStroke;
                    float best = float.MaxValue;
                    foreach (var s in glyph.Segments)
                    {
                        var scaled = new KitGlyphSegment(s.X0 * xs, s.Y0, s.X1 * xs, s.Y1);
                        float d = KitPrintRasterizer.DistanceToSegment(scaled, ux, uy, square);
                        if (d < best) best = d;
                    }
                    return Clamp01((halfStroke - best) / pixelUnits + 0.5f);
                }
                cursor += ink + KitPrintLayoutLogic.LetterGap * scale;
            }
            return 0f;
        }

        private static SimpleColor Lerp(SimpleColor a, SimpleColor b, float t)
        {
            return new SimpleColor(
                a.R + (b.R - a.R) * t,
                a.G + (b.G - a.G) * t,
                a.B + (b.B - a.B) * t,
                1f);
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}
