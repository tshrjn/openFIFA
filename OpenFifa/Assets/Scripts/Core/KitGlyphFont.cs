using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenFifa.Core
{
    /// <summary>
    /// Original procedural stroke font for shirt names and numbers (0-9, A-Z, - ' . space).
    /// Each glyph is a set of stroke centre-line segments on a 4 x 6 grid (y up). The
    /// rasterizer thickens the strokes, so the same data drives every <see cref="KitFontStyle"/>.
    /// No font asset is needed at runtime. Pure C#.
    /// </summary>
    public static class KitGlyphFont
    {
        /// <summary>Grid height of a capital letter / digit (centre-line units).</summary>
        public const float GridHeight = 6f;

        /// <summary>Nominal grid width of a full-width glyph.</summary>
        public const float GridWidth = 4f;

        // Polylines separated by '|', points separated by ' ', coordinates "x,y".
        private static readonly Dictionary<char, string> Definitions = new Dictionary<char, string>
        {
            { '0', "0,0 4,0 4,6 0,6 0,0" },
            { '1', "0.8,4.9 2,6 2,0" },
            { '2', "0,6 4,6 4,3 0,3 0,0 4,0" },
            { '3', "0,6 4,6 4,0 0,0|1.2,3 4,3" },
            { '4', "0,6 0,2 4,2|3,4.2 3,0" },
            { '5', "4,6 0,6 0,3.2 4,3.2 4,0 0,0" },
            { '6', "4,6 0,6 0,0 4,0 4,3 0,3" },
            { '7', "0,6 4,6 1.6,0" },
            { '8', "0,0 4,0 4,6 0,6 0,0|0,3 4,3" },
            { '9', "4,3 0,3 0,6 4,6 4,0 0,0" },
            { 'A', "0,0 0,6 4,6 4,0|0,3 4,3" },
            { 'B', "0,0 0,6 3.2,6 3.2,3|0,3 4,3 4,0 0,0" },
            { 'C', "4,6 0,6 0,0 4,0" },
            { 'D', "0,0 0,6 3,6 4,5 4,1 3,0 0,0" },
            { 'E', "4,6 0,6 0,0 4,0|0,3 3,3" },
            { 'F', "4,6 0,6 0,0|0,3 3,3" },
            { 'G', "4,6 0,6 0,0 4,0 4,3 2,3" },
            { 'H', "0,0 0,6|4,0 4,6|0,3 4,3" },
            { 'I', "1,6 3,6|2,6 2,0|1,0 3,0" },
            { 'J', "4,6 4,0 0,0 0,2" },
            { 'K', "0,0 0,6|4,6 0.6,3 4,0" },
            { 'L', "0,6 0,0 4,0" },
            { 'M', "0,0 0,6 2,3 4,6 4,0" },
            { 'N', "0,0 0,6 4,0 4,6" },
            { 'O', "0,0 4,0 4,6 0,6 0,0" },
            { 'P', "0,0 0,6 4,6 4,3 0,3" },
            { 'Q', "4,1.2 4,6 0,6 0,0 2.8,0|2.4,1.6 4,0" },
            { 'R', "0,0 0,6 4,6 4,3 0,3|2,3 4,0" },
            { 'S', "4,5 4,6 0,6 0,3 4,3 4,0 0,0 0,1" },
            { 'T', "0,6 4,6|2,6 2,0" },
            { 'U', "0,6 0,0 4,0 4,6" },
            { 'V', "0,6 2,0 4,6" },
            { 'W', "0,6 1,0 2,4 3,0 4,6" },
            { 'X', "0,0 4,6|0,6 4,0" },
            { 'Y', "0,6 2,3 4,6|2,3 2,0" },
            { 'Z', "0,6 4,6 0,0 4,0" },
            { '-', "0.5,3 2.5,3" },
            { '\'', "0.5,6 0.5,4.6" },
            { '.', "0.5,0 0.5,0" },
            { ' ', "" }
        };

        private static readonly Dictionary<char, KitGlyph> Cache = BuildCache();

        /// <summary>True when the character (after <see cref="NormalizeChar"/>) has a glyph.</summary>
        public static bool HasGlyph(char c)
        {
            return Cache.ContainsKey(NormalizeChar(c));
        }

        /// <summary>
        /// Glyph for a character. Lower case and accented Latin letters map to their base
        /// capital; anything else unknown maps to space.
        /// </summary>
        public static KitGlyph GetGlyph(char c)
        {
            KitGlyph glyph;
            return Cache.TryGetValue(NormalizeChar(c), out glyph) ? glyph : Cache[' '];
        }

        /// <summary>Upper-cases and strips diacritics (e.g. 'é' becomes 'E').</summary>
        public static char NormalizeChar(char c)
        {
            if (Definitions.ContainsKey(c)) return c;

            var decomposed = c.ToString().Normalize(NormalizationForm.FormD);
            foreach (char d in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(d) == UnicodeCategory.NonSpacingMark) continue;
                char upper = char.ToUpperInvariant(d);
                if (Definitions.ContainsKey(upper)) return upper;
            }
            return ' ';
        }

        /// <summary>Stroke width in grid units for a style.</summary>
        public static float StrokeWidth(KitFontStyle style)
        {
            switch (style)
            {
                case KitFontStyle.Rounded: return 1.05f;
                case KitFontStyle.Condensed: return 1.0f;
                default: return 1.25f;
            }
        }

        /// <summary>Horizontal squash applied to glyph geometry for a style.</summary>
        public static float HorizontalScale(KitFontStyle style)
        {
            return style == KitFontStyle.Condensed ? 0.72f : 1f;
        }

        /// <summary>True when stroke ends are square (block look) rather than round.</summary>
        public static bool SquareCaps(KitFontStyle style)
        {
            return style != KitFontStyle.Rounded;
        }

        /// <summary>
        /// Width of the inked glyph in grid units for a style, including stroke thickness.
        /// Space has a fixed advance.
        /// </summary>
        public static float InkWidth(KitGlyph glyph, KitFontStyle style)
        {
            if (glyph.Segments.Length == 0) return 2.2f * HorizontalScale(style);
            return (glyph.MaxX - glyph.MinX) * HorizontalScale(style) + StrokeWidth(style);
        }

        /// <summary>Height of the inked glyph in grid units, including stroke thickness.</summary>
        public static float InkHeight(KitFontStyle style)
        {
            return GridHeight + StrokeWidth(style);
        }

        private static Dictionary<char, KitGlyph> BuildCache()
        {
            var cache = new Dictionary<char, KitGlyph>();
            foreach (var pair in Definitions)
                cache[pair.Key] = Parse(pair.Key, pair.Value);
            return cache;
        }

        private static KitGlyph Parse(char c, string definition)
        {
            var segments = new List<KitGlyphSegment>();
            if (!string.IsNullOrEmpty(definition))
            {
                foreach (var polyline in definition.Split('|'))
                {
                    var points = polyline.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    float px = 0f, py = 0f;
                    for (int i = 0; i < points.Length; i++)
                    {
                        var xy = points[i].Split(',');
                        float x = float.Parse(xy[0], CultureInfo.InvariantCulture);
                        float y = float.Parse(xy[1], CultureInfo.InvariantCulture);
                        if (points.Length == 1)
                            segments.Add(new KitGlyphSegment(x, y, x, y));
                        else if (i > 0)
                            segments.Add(new KitGlyphSegment(px, py, x, y));
                        px = x;
                        py = y;
                    }
                }
            }
            return new KitGlyph(c, segments.ToArray());
        }
    }
}
