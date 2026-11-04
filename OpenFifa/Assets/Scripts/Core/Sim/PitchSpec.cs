namespace OpenFifa.Core
{
    /// <summary>
    /// Pitch geometry in metres, centred on the origin. Home attacks +Z in the first half.
    /// </summary>
    public sealed class PitchSpec
    {
        public float Length { get; }
        public float Width { get; }
        public float GoalWidth { get; }
        public float GoalHeight { get; }
        public float PenaltyAreaDepth { get; }
        public float PenaltyAreaWidth { get; }

        public float HalfLength => Length * 0.5f;
        public float HalfWidth => Width * 0.5f;

        public PitchSpec(float length, float width, float goalWidth, float goalHeight,
            float penaltyAreaDepth, float penaltyAreaWidth)
        {
            Length = length;
            Width = width;
            GoalWidth = goalWidth;
            GoalHeight = goalHeight;
            PenaltyAreaDepth = penaltyAreaDepth;
            PenaltyAreaWidth = penaltyAreaWidth;
        }

        /// <summary>Standard 11-a-side pitch (105 x 68 m, 7.32 x 2.44 m goals).</summary>
        public static PitchSpec Standard => new PitchSpec(105f, 68f, 7.32f, 2.44f, 16.5f, 40.32f);
    }
}
