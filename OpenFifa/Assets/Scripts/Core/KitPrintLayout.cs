using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Result of laying out a player's shirt print into the print atlas.
    /// Atlas convention (shared with OpenFifaKit.shader): left half = back panel,
    /// right half = front panel, both viewed from outside the shirt.
    /// </summary>
    public class KitPrintLayout
    {
        public readonly int Width;
        public readonly int Height;
        public readonly KitFontStyle Style;
        public readonly float OutlineWidth;
        public readonly KitPrintRect BackPanel;
        public readonly KitPrintRect FrontPanel;
        public readonly List<KitGlyphPlacement> Glyphs = new List<KitGlyphPlacement>();

        /// <summary>Name actually printed (formatted/sanitized).</summary>
        public string PrintedName = "";

        /// <summary>Number actually printed.</summary>
        public int PrintedNumber;

        public KitPrintLayout(int width, int height, KitFontStyle style, float outlineWidth)
        {
            Width = width;
            Height = height;
            Style = style;
            OutlineWidth = outlineWidth;
            BackPanel = new KitPrintRect(0f, 0f, width * 0.5f, height);
            FrontPanel = new KitPrintRect(width * 0.5f, 0f, width * 0.5f, height);
        }

        /// <summary>Glyphs of one element.</summary>
        public List<KitGlyphPlacement> GlyphsOf(KitPrintElement element)
        {
            var list = new List<KitGlyphPlacement>();
            foreach (var g in Glyphs)
                if (g.Element == element) list.Add(g);
            return list;
        }
    }
}
