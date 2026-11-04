namespace OpenFifa.Core
{
    /// <summary>
    /// Procedural print font styles used for shirt names and numbers.
    /// All styles are original stroke fonts generated in code, so no font
    /// asset (and no font licence) is needed at runtime.
    /// </summary>
    public enum KitFontStyle
    {
        /// <summary>Heavy strokes with square terminals: classic sports block.</summary>
        Block = 0,

        /// <summary>Slightly lighter strokes with round terminals.</summary>
        Rounded = 1,

        /// <summary>Narrow, tall glyphs with square terminals.</summary>
        Condensed = 2
    }
}
