namespace OpenFifa.Core
{
    /// <summary>
    /// Data payload for goal scored events.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class GoalEventData
    {
        /// <summary>The team that scored the goal.</summary>
        public TeamIdentifier ScoringTeam { get; }

        /// <summary>The team that was scored against.</summary>
        public TeamIdentifier DefendingTeam { get; }

        public GoalEventData(TeamIdentifier scoringTeam)
        {
            ScoringTeam = scoringTeam;
            DefendingTeam = scoringTeam == TeamIdentifier.TeamA
                ? TeamIdentifier.TeamB
                : TeamIdentifier.TeamA;
        }
    }
}
