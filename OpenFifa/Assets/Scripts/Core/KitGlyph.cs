namespace OpenFifa.Core
{
    /// <summary>
    /// Stroke geometry of one <see cref="KitGlyphFont"/> character in grid units.
    /// </summary>
    public sealed class KitGlyph
    {
        public readonly char Character;
        public readonly KitGlyphSegment[] Segments;
        public readonly float MinX;
        public readonly float MaxX;

        public KitGlyph(char character, KitGlyphSegment[] segments)
        {
            Character = character;
            Segments = segments ?? new KitGlyphSegment[0];

            float min = float.MaxValue, max = float.MinValue;
            foreach (var s in Segments)
            {
                if (s.X0 < min) min = s.X0;
                if (s.X1 < min) min = s.X1;
                if (s.X0 > max) max = s.X0;
                if (s.X1 > max) max = s.X1;
            }
            MinX = Segments.Length > 0 ? min : 0f;
            MaxX = Segments.Length > 0 ? max : 0f;
        }

        /// <summary>Horizontal centre of the glyph's centre-lines.</summary>
        public float CenterX => (MinX + MaxX) * 0.5f;
    }
}
