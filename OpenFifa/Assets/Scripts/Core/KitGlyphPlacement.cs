namespace OpenFifa.Core
{
    /// <summary>
    /// Where one glyph is drawn in the print atlas.
    /// </summary>
    public struct KitGlyphPlacement
    {
        /// <summary>Normalized character (see <see cref="KitGlyphFont.NormalizeChar"/>).</summary>
        public char Character;

        /// <summary>Element the glyph belongs to.</summary>
        public KitPrintElement Element;

        /// <summary>Centre of the inked glyph box, atlas pixels.</summary>
        public float CenterX;

        /// <summary>Centre of the inked glyph box, atlas pixels.</summary>
        public float CenterY;

        /// <summary>Pixels per glyph grid unit.</summary>
        public float Scale;

        /// <summary>Counter-clockwise rotation in radians (non-zero for arched names).</summary>
        public float Rotation;

        /// <summary>Inked width in pixels (unrotated).</summary>
        public float InkWidth;

        /// <summary>Inked height in pixels (unrotated).</summary>
        public float InkHeight;
    }
}
