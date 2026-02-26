namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# data class holding pitch configuration values.
    /// No Unity dependency — fully testable in EditMode.
    /// The ScriptableObject wrapper (PitchConfig) lives in Gameplay.
    /// </summary>
    public class PitchConfigData
    {
        public float PitchLength { get; }
        public float PitchWidth { get; }
        public float GoalWidth { get; }
        public float CenterCircleRadius { get; }
        public float GoalAreaDepth { get; }
        public float GoalHeight { get; }
        public float BoundaryWallHeight { get; }
        public float BoundaryWallThickness { get; }

        // Derived properties
        public float HalfLength => PitchLength / 2f;
        public float HalfWidth => PitchWidth / 2f;
        public float GoalOpeningHalfWidth => GoalWidth / 2f;

        public PitchConfigData(
            float pitchLength = 50f,
            float pitchWidth = 30f,
            float goalWidth = 5f,
            float centerCircleRadius = 3f,
            float goalAreaDepth = 4f,
            float goalHeight = 2.4f,
            float boundaryWallHeight = 3f,
            float boundaryWallThickness = 1f)
        {
            PitchLength = pitchLength;
            PitchWidth = pitchWidth;
            GoalWidth = goalWidth;
            CenterCircleRadius = centerCircleRadius;
            GoalAreaDepth = goalAreaDepth;
            GoalHeight = goalHeight;
            BoundaryWallHeight = boundaryWallHeight;
            BoundaryWallThickness = boundaryWallThickness;
        }
    }
}
