namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# minimap coordinate mapping.
    /// Converts world positions to minimap UI positions.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class MinimapLogic
    {
        private readonly float _pitchLength;
        private readonly float _pitchWidth;
        private readonly float _minimapWidth;
        private readonly float _minimapHeight;

        public MinimapLogic(float pitchLength, float pitchWidth, float minimapWidth, float minimapHeight)
        {
            _pitchLength = pitchLength;
            _pitchWidth = pitchWidth;
            _minimapWidth = minimapWidth;
            _minimapHeight = minimapHeight;
        }

        /// <summary>
        /// Convert world X, Z to minimap coordinates.
        /// World center (0,0) maps to minimap center.
        /// </summary>
        public void WorldToMinimap(float worldX, float worldZ, out float minimapX, out float minimapY)
        {
            float halfLength = _pitchLength * 0.5f;
            float halfWidth = _pitchWidth * 0.5f;

            // Normalize to 0-1
            float normalizedX = (worldX + halfLength) / _pitchLength;
            float normalizedY = (worldZ + halfWidth) / _pitchWidth;

            // Clamp
            if (normalizedX < 0f) normalizedX = 0f;
            if (normalizedX > 1f) normalizedX = 1f;
            if (normalizedY < 0f) normalizedY = 0f;
            if (normalizedY > 1f) normalizedY = 1f;

            // Scale to minimap dimensions
            minimapX = normalizedX * _minimapWidth;
            minimapY = normalizedY * _minimapHeight;
        }
    }

    /// <summary>
    /// Pure C# match state to display text mapping.
    /// </summary>
    public class MatchStateDisplay
    {
        /// <summary>
        /// Get the display text for a match state.
        /// </summary>
        public string GetStateText(MatchState state)
        {
            switch (state)
            {
                case MatchState.PreKickoff: return "KICKOFF";
                case MatchState.FirstHalf: return "FIRST HALF";
                case MatchState.HalfTime: return "HALF TIME";
                case MatchState.SecondHalf: return "SECOND HALF";
                case MatchState.FullTime: return "FULL TIME";
                case MatchState.GoalCelebration: return "GOAL!";
                case MatchState.Paused: return "PAUSED";
                default: return "";
            }
        }
    }
}
