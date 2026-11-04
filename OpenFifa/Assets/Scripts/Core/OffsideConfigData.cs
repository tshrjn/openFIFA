namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# configuration for offside (Law 11).
    /// The ScriptableObject wrapper (OffsideConfig) lives in Gameplay.
    /// </summary>
    public class OffsideConfigData
    {
        /// <summary>Offside applies in full-sided (11v11) matches.</summary>
        public bool EnabledFullSided { get; }

        /// <summary>Offside applies in small-sided matches (4v4 / 5v5 Rush).</summary>
        public bool EnabledSmallSided { get; }

        /// <summary>Teams with at least this many players per side count as full-sided.</summary>
        public int FullSidedMinPlayers { get; }

        /// <summary>
        /// Horizontal radius of the scoring body (head, body, feet; arms excluded),
        /// added to the player's centre in the direction of the goal line.
        /// </summary>
        public float BodyRadius { get; }

        /// <summary>Ball radius, added to the ball centre toward the goal line.</summary>
        public float BallRadius { get; }

        /// <summary>
        /// Players within this distance of the reference line are level, and level is onside.
        /// </summary>
        public float LevelTolerance { get; }

        /// <summary>
        /// An offside-positioned player who challenges an opponent within this distance
        /// of the ball is interfering with an opponent. 0 disables the challenge rule.
        /// </summary>
        public float InterferenceRadius { get; }

        /// <summary>World X of the halfway line.</summary>
        public float HalfwayLineX { get; }

        /// <summary>True when TeamA attacks the +X goal (TeamB then attacks -X).</summary>
        public bool TeamAAttacksPositiveX { get; }

        public OffsideConfigData(
            bool enabledFullSided = true,
            bool enabledSmallSided = true,
            int fullSidedMinPlayers = 11,
            float bodyRadius = 0.25f,
            float ballRadius = 0.11f,
            float levelTolerance = 0.02f,
            float interferenceRadius = 2f,
            float halfwayLineX = 0f,
            bool teamAAttacksPositiveX = true)
        {
            EnabledFullSided = enabledFullSided;
            EnabledSmallSided = enabledSmallSided;
            FullSidedMinPlayers = fullSidedMinPlayers;
            BodyRadius = bodyRadius < 0f ? 0f : bodyRadius;
            BallRadius = ballRadius < 0f ? 0f : ballRadius;
            LevelTolerance = levelTolerance < 0f ? 0f : levelTolerance;
            InterferenceRadius = interferenceRadius;
            HalfwayLineX = halfwayLineX;
            TeamAAttacksPositiveX = teamAAttacksPositiveX;
        }

        /// <summary>Whether offside applies for a match with the given players per side.</summary>
        public bool IsEnabledFor(int playersPerSide)
        {
            return playersPerSide >= FullSidedMinPlayers ? EnabledFullSided : EnabledSmallSided;
        }
    }
}
