using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenFifa.Core
{
    /// <summary>
    /// Lays out back name (straight or arched), back number and small front number into
    /// the print atlas. Positions are fractions of each panel so the layout is resolution
    /// independent. Pure C#, so it is EditMode-testable.
    /// </summary>
    public static class KitPrintLayoutLogic
    {
        // Back panel (fractions of panel size).
        public const float NameCenterY = 0.86f;
        public const float NameHeight = 0.085f;
        public const float NameMaxWidth = 0.80f;
        public const float NameMinScaleFactor = 0.45f;
        public const float NumberCenterY = 0.44f;
        public const float NumberHeight = 0.46f;
        public const float NumberMaxWidth = 0.78f;

        // Front panel.
        public const float FrontNumberCenterX = 0.5f;
        public const float FrontNumberCenterY = 0.74f;
        public const float FrontNumberHeight = 0.17f;

        /// <summary>Arch radius as a multiple of panel width (larger = flatter).</summary>
        public const float ArchRadius = 0.95f;

        /// <summary>Gap between letters, grid units.</summary>
        public const float LetterGap = 0.55f;

        /// <summary>Gap between number digits, grid units.</summary>
        public const float DigitGap = 0.7f;

        /// <summary>
        /// Builds the complete print layout for one player.
        /// </summary>
        /// <param name="name">Raw player name; formatted with <see cref="NameFormatter.FormatAndSanitize"/>.</param>
        /// <param name="number">Squad number, clamped to 1-99.</param>
        public static KitPrintLayout Build(string name, int number, KitNameLayout nameLayout, KitFontStyle style,
            float outlineWidth, bool showFrontNumber, int width, int height)
        {
            if (width < 8 || height < 4) throw new ArgumentOutOfRangeException(nameof(width), "Atlas too small.");

            var layout = new KitPrintLayout(width, height, style, Math.Max(0f, outlineWidth));
            number = Math.Max(PlayerNumberConfig.MinNumber, Math.Min(PlayerNumberConfig.MaxNumber, number));
            layout.PrintedNumber = number;
            layout.PrintedName = NameFormatter.FormatAndSanitize(name);

            string digits = number.ToString(CultureInfo.InvariantCulture);
            var back = layout.BackPanel;
            var front = layout.FrontPanel;
            float pad = 2f * layout.OutlineWidth;

            if (layout.PrintedName.Length > 0)
            {
                float targetScale = back.Height * NameHeight / (KitGlyphFont.InkHeight(style) + pad);
                AddName(layout, layout.PrintedName, nameLayout, style, targetScale, back);
            }

            float numberScale = back.Height * NumberHeight / (KitGlyphFont.InkHeight(style) + pad);
            AddLine(layout, digits, KitPrintElement.BackNumber, style, numberScale, DigitGap,
                back.CenterX, back.Y + back.Height * NumberCenterY, back.Width * NumberMaxWidth, 0.5f);

            if (showFrontNumber)
            {
                float frontScale = front.Height * FrontNumberHeight / (KitGlyphFont.InkHeight(style) + pad);
                AddLine(layout, digits, KitPrintElement.FrontNumber, style, frontScale, DigitGap,
                    front.X + front.Width * FrontNumberCenterX, front.Y + front.Height * FrontNumberCenterY,
                    front.Width * 0.3f, 0.5f);
            }

            return layout;
        }

        /// <summary>
        /// Total advance width in grid units of a string (ink widths plus gaps, outline included).
        /// </summary>
        public static float MeasureUnits(string text, KitFontStyle style, float gap, float outlineWidth)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float total = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                total += KitGlyphFont.InkWidth(KitGlyphFont.GetGlyph(text[i]), style) + 2f * outlineWidth;
                if (i < text.Length - 1) total += gap;
            }
            return total;
        }

        private static void AddName(KitPrintLayout layout, string name, KitNameLayout nameLayout, KitFontStyle style,
            float scale, KitPrintRect back)
        {
            float centerX = back.CenterX;
            float centerY = back.Y + back.Height * NameCenterY;
            float maxWidth = back.Width * NameMaxWidth;

            if (nameLayout == KitNameLayout.Straight)
            {
                AddLine(layout, name, KitPrintElement.BackName, style, scale, LetterGap, centerX, centerY, maxWidth,
                    NameMinScaleFactor);
                return;
            }

            // Arched: glyphs sit on a circle whose centre is below the name.
            float gap = LetterGap;
            scale *= Fit(name, style, layout.OutlineWidth, scale, maxWidth, NameMinScaleFactor, ref gap);
            float widthUnits = MeasureUnits(name, style, gap, layout.OutlineWidth);

            float radius = back.Width * ArchRadius;
            float arcStart = -widthUnits * scale * 0.5f;
            float cursor = arcStart;
            for (int i = 0; i < name.Length; i++)
            {
                var glyph = KitGlyphFont.GetGlyph(name[i]);
                float ink = (KitGlyphFont.InkWidth(glyph, style) + 2f * layout.OutlineWidth) * scale;
                float arcPos = cursor + ink * 0.5f;
                float angle = arcPos / radius;

                if (glyph.Segments.Length > 0)
                {
                    layout.Glyphs.Add(new KitGlyphPlacement
                    {
                        Character = glyph.Character,
                        Element = KitPrintElement.BackName,
                        CenterX = centerX + radius * (float)Math.Sin(angle),
                        CenterY = centerY - radius + radius * (float)Math.Cos(angle),
                        Scale = scale,
                        Rotation = -angle,
                        InkWidth = ink,
                        InkHeight = (KitGlyphFont.InkHeight(style) + 2f * layout.OutlineWidth) * scale
                    });
                }
                cursor += ink + gap * scale;
            }
        }

        private static void AddLine(KitPrintLayout layout, string text, KitPrintElement element, KitFontStyle style,
            float scale, float gap, float centerX, float centerY, float maxWidth, float minScaleFactor)
        {
            scale *= Fit(text, style, layout.OutlineWidth, scale, maxWidth, minScaleFactor, ref gap);
            float widthUnits = MeasureUnits(text, style, gap, layout.OutlineWidth);

            float cursor = centerX - widthUnits * scale * 0.5f;
            for (int i = 0; i < text.Length; i++)
            {
                var glyph = KitGlyphFont.GetGlyph(text[i]);
                float ink = (KitGlyphFont.InkWidth(glyph, style) + 2f * layout.OutlineWidth) * scale;
                if (glyph.Segments.Length > 0)
                {
                    layout.Glyphs.Add(new KitGlyphPlacement
                    {
                        Character = glyph.Character,
                        Element = element,
                        CenterX = cursor + ink * 0.5f,
                        CenterY = centerY,
                        Scale = scale,
                        Rotation = 0f,
                        InkWidth = ink,
                        InkHeight = (KitGlyphFont.InkHeight(style) + 2f * layout.OutlineWidth) * scale
                    });
                }
                cursor += ink + gap * scale;
            }
        }

        /// <summary>
        /// Shrinks long text to fit: first tightens letter spacing to 40%, then scales glyphs
        /// down (not below <paramref name="minScaleFactor"/>; past that the text may overflow
        /// slightly rather than become unreadable). Returns the glyph scale factor.
        /// </summary>
        private static float Fit(string text, KitFontStyle style, float outlineWidth, float scale, float maxWidth,
            float minScaleFactor, ref float gap)
        {
            float width = MeasureUnits(text, style, gap, outlineWidth) * scale;
            if (width <= maxWidth || width <= 0f) return 1f;

            gap *= 0.4f;
            width = MeasureUnits(text, style, gap, outlineWidth) * scale;
            if (width <= maxWidth) return 1f;

            return Math.Max(minScaleFactor, maxWidth / width);
        }
    }
}
