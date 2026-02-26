namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# kickoff logic.
    /// Tracks which team kicks off and manages setup parameters.
    /// No Unity dependency.
    /// </summary>
    public class KickoffLogic
    {
        private TeamIdentifier _kickingTeam;

        /// <summary>Which team will kick off next.</summary>
        public TeamIdentifier KickingTeam => _kickingTeam;

        /// <summary>Ball center X position (always 0).</summary>
        public float BallCenterX => 0f;

        /// <summary>Ball center Z position (always 0).</summary>
        public float BallCenterZ => 0f;

        /// <summary>Delay in seconds between setup and kick.</summary>
        public float SetupDelay { get; }

        public KickoffLogic(TeamIdentifier initialKickingTeam = TeamIdentifier.TeamA, float setupDelay = 1f)
        {
            _kickingTeam = initialKickingTeam;
            SetupDelay = setupDelay;
        }

        /// <summary>
        /// Called when a goal is scored. The non-scoring team kicks off.
        /// </summary>
        public void OnGoalScored(TeamIdentifier scoringTeam)
        {
            _kickingTeam = scoringTeam == TeamIdentifier.TeamA
                ? TeamIdentifier.TeamB
                : TeamIdentifier.TeamA;
        }

        /// <summary>
        /// Set kicking team for second half (non-starting team).
        /// </summary>
        public void SetSecondHalfKickoff(TeamIdentifier firstHalfStarter)
        {
            _kickingTeam = firstHalfStarter == TeamIdentifier.TeamA
                ? TeamIdentifier.TeamB
                : TeamIdentifier.TeamA;
        }
    }
}
