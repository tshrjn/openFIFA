namespace OpenFifa.Core
{
    /// <summary>
    /// Original 5 x 7 bitmap font (A-Z, 0-9, a few symbols) for procedural textures such as the LED board wordmarks.
    /// Rows are listed top to bottom, bit 4 is the leftmost column.
    /// </summary>
    public static class PixelFont
    {
        public const int GlyphWidth = 5, GlyphHeight = 7, Advance = 6;

        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!#-.";

        private static readonly byte[][] Glyphs =
        {
            new byte[] { 14, 17, 17, 31, 17, 17, 17 }, // A
            new byte[] { 30, 17, 17, 30, 17, 17, 30 }, // B
            new byte[] { 14, 17, 16, 16, 16, 17, 14 }, // C
            new byte[] { 30, 17, 17, 17, 17, 17, 30 }, // D
            new byte[] { 31, 16, 16, 30, 16, 16, 31 }, // E
            new byte[] { 31, 16, 16, 30, 16, 16, 16 }, // F
            new byte[] { 14, 17, 16, 23, 17, 17, 15 }, // G
            new byte[] { 17, 17, 17, 31, 17, 17, 17 }, // H
            new byte[] { 14, 4, 4, 4, 4, 4, 14 },      // I
            new byte[] { 7, 2, 2, 2, 2, 18, 12 },      // J
            new byte[] { 17, 18, 20, 24, 20, 18, 17 }, // K
            new byte[] { 16, 16, 16, 16, 16, 16, 31 }, // L
            new byte[] { 17, 27, 21, 21, 17, 17, 17 }, // M
            new byte[] { 17, 17, 25, 21, 19, 17, 17 }, // N
            new byte[] { 14, 17, 17, 17, 17, 17, 14 }, // O
            new byte[] { 30, 17, 17, 30, 16, 16, 16 }, // P
            new byte[] { 14, 17, 17, 17, 21, 18, 13 }, // Q
            new byte[] { 30, 17, 17, 30, 20, 18, 17 }, // R
            new byte[] { 15, 16, 16, 14, 1, 1, 30 },   // S
            new byte[] { 31, 4, 4, 4, 4, 4, 4 },       // T
            new byte[] { 17, 17, 17, 17, 17, 17, 14 }, // U
            new byte[] { 17, 17, 17, 17, 17, 10, 4 },  // V
            new byte[] { 17, 17, 17, 21, 21, 21, 10 }, // W
            new byte[] { 17, 17, 10, 4, 10, 17, 17 },  // X
            new byte[] { 17, 17, 10, 4, 4, 4, 4 },     // Y
            new byte[] { 31, 1, 2, 4, 8, 16, 31 },     // Z
            new byte[] { 14, 17, 19, 21, 25, 17, 14 }, // 0
            new byte[] { 4, 12, 4, 4, 4, 4, 14 },      // 1
            new byte[] { 14, 17, 1, 2, 4, 8, 31 },     // 2
            new byte[] { 31, 2, 4, 2, 1, 17, 14 },     // 3
            new byte[] { 2, 6, 10, 18, 31, 2, 2 },     // 4
            new byte[] { 31, 16, 30, 1, 1, 17, 14 },   // 5
            new byte[] { 6, 8, 16, 30, 17, 17, 14 },   // 6
            new byte[] { 31, 1, 2, 4, 8, 8, 8 },       // 7
            new byte[] { 14, 17, 17, 14, 17, 17, 14 }, // 8
            new byte[] { 14, 17, 17, 15, 1, 2, 12 },   // 9
            new byte[] { 4, 4, 4, 4, 4, 0, 4 },        // !
            new byte[] { 10, 31, 10, 10, 31, 10, 0 },  // #
            new byte[] { 0, 0, 0, 31, 0, 0, 0 },       // -
            new byte[] { 0, 0, 0, 0, 0, 0, 4 },        // .
        };

        public static bool Supports(char c) => c == ' ' || Chars.IndexOf(char.ToUpperInvariant(c)) >= 0;

        /// <summary>True if glyph <paramref name="c"/> has ink at column x (0 left) and row y (0 top). Space and unknown characters are blank.</summary>
        public static bool Pixel(char c, int x, int y)
        {
            if (x < 0 || x >= GlyphWidth || y < 0 || y >= GlyphHeight) return false;
            int i = Chars.IndexOf(char.ToUpperInvariant(c));
            if (i < 0) return false;
            return (Glyphs[i][y] & (1 << (GlyphWidth - 1 - x))) != 0;
        }

        /// <summary>Width in font pixels of a string (no trailing gap).</summary>
        public static int Width(string s) => string.IsNullOrEmpty(s) ? 0 : s.Length * Advance - 1;

        /// <summary>Ink at (x, y) font pixels inside the laid-out string (y = 0 top).</summary>
        public static bool TextPixel(string s, int x, int y)
        {
            if (string.IsNullOrEmpty(s) || x < 0 || y < 0 || y >= GlyphHeight) return false;
            int ci = x / Advance;
            if (ci >= s.Length) return false;
            return Pixel(s[ci], x - ci * Advance, y);
        }
    }
}
