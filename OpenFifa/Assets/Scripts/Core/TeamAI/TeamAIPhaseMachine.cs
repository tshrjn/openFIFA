namespace OpenFifa.Core
{
    /// <summary>
    /// Team phase state machine (top AI layer). In possession: build-up, progression, final third.
    /// Out of possession: high press, mid block, low block. Transitions: counter-press after a loss,
    /// recovery when the counter-press is off or fails. Set pieces override everything.
    /// </summary>
    public sealed class TeamAIPhaseMachine
    {
        public TeamAIPhase Phase { get; private set; } = TeamAIPhase.MidBlock;
        /// <summary>Seconds spent in the current phase.</summary>
        public float TimeInPhase { get; private set; }
        public bool HasPossession { get; private set; }

        private const float Hysteresis = 0.03f;

        public void Reset(TeamAIPhase phase, bool hasPossession)
        {
            Phase = phase;
            HasPossession = hasPossession;
            TimeInPhase = 0f;
        }

        /// <summary>
        /// Advance the machine.
        /// </summary>
        /// <param name="hasPossession">This team controls the ball (sticky possession, see TeamAIBrain).</param>
        /// <param name="ballDepth">Ball depth 0..1 from this team's own goal.</param>
        /// <param name="nearestToBall">Distance of this team's closest player to the ball (m).</param>
        public TeamAIPhase Update(float dt, TeamAITactics tactics, bool hasPossession, float ballDepth,
            float nearestToBall, TeamAIMatchPhase matchPhase, int restartTeam, int team)
        {
            TimeInPhase += dt;

            if (matchPhase != TeamAIMatchPhase.InPlay && matchPhase != TeamAIMatchPhase.Stopped)
            {
                var sp = restartTeam == team ? TeamAIPhase.SetPieceFor : TeamAIPhase.SetPieceAgainst;
                HasPossession = restartTeam == team;
                Set(sp);
                return Phase;
            }

            bool wasInPossession = HasPossession;
            HasPossession = hasPossession;

            if (hasPossession)
            {
                if (!wasInPossession || !TeamAIFormationShape.InPossession(Phase) || Phase == TeamAIPhase.SetPieceFor)
                {
                    Set(InPossessionPhase(ballDepth, TeamAIPhase.Progression, true));
                }
                else
                {
                    Set(InPossessionPhase(ballDepth, Phase, false));
                }
                return Phase;
            }

            // Out of possession.
            if (wasInPossession || Phase == TeamAIPhase.SetPieceAgainst || Phase == TeamAIPhase.SetPieceFor)
            {
                bool counterPress = tactics.PressingIntensity >= 0.3f && nearestToBall < 10f && wasInPossession;
                Set(counterPress ? TeamAIPhase.CounterPress : TeamAIPhase.Recovery);
                return Phase;
            }

            if (Phase == TeamAIPhase.CounterPress)
            {
                float window = 1.5f + 4f * tactics.PressingIntensity;
                if (TimeInPhase < window) return Phase;
                // Counter-press failed: drop into recovery if the ball is in our half, otherwise settle.
                Set(ballDepth < 0.5f ? TeamAIPhase.Recovery : SettledBlock(tactics, ballDepth, Phase, true));
                return Phase;
            }

            if (Phase == TeamAIPhase.Recovery)
            {
                if (TimeInPhase < 2.5f) return Phase;
                Set(SettledBlock(tactics, ballDepth, Phase, true));
                return Phase;
            }

            Set(SettledBlock(tactics, ballDepth, Phase, false));
            return Phase;
        }

        private void Set(TeamAIPhase p)
        {
            if (p == Phase) return;
            Phase = p;
            TimeInPhase = 0f;
        }

        private static TeamAIPhase InPossessionPhase(float ballDepth, TeamAIPhase current, bool fresh)
        {
            float h = fresh ? 0f : Hysteresis;
            // Thresholds: build-up below 0.36, final third above 0.66.
            switch (current)
            {
                case TeamAIPhase.BuildUp:
                    if (ballDepth > 0.66f + h) return TeamAIPhase.FinalThird;
                    if (ballDepth > 0.36f + h) return TeamAIPhase.Progression;
                    return TeamAIPhase.BuildUp;
                case TeamAIPhase.FinalThird:
                    if (ballDepth < 0.36f - h) return TeamAIPhase.BuildUp;
                    if (ballDepth < 0.66f - h) return TeamAIPhase.Progression;
                    return TeamAIPhase.FinalThird;
                default:
                    if (ballDepth > 0.66f + h) return TeamAIPhase.FinalThird;
                    if (ballDepth < 0.36f - h) return TeamAIPhase.BuildUp;
                    return TeamAIPhase.Progression;
            }
        }

        /// <summary>Settled out-of-possession block chosen from ball depth and pressing intensity.</summary>
        public static TeamAIPhase SettledBlock(TeamAITactics tactics, float ballDepth, TeamAIPhase current, bool fresh)
        {
            float h = fresh ? 0f : Hysteresis;
            // ballDepth is from our own goal; the opponent's build-up happens at high ballDepth.
            float pressLine = 0.68f - 0.18f * tactics.PressingIntensity; // high press starts higher for passive teams
            float lowLine = 0.30f - 0.08f * tactics.Depth;
            bool canPress = tactics.PressingIntensity >= 0.35f;

            if (canPress)
            {
                float enter = current == TeamAIPhase.HighPress ? pressLine - h : pressLine + h;
                if (ballDepth > enter) return TeamAIPhase.HighPress;
            }
            float lowEnter = current == TeamAIPhase.LowBlock ? lowLine + h : lowLine - h;
            if (ballDepth < lowEnter) return TeamAIPhase.LowBlock;
            return TeamAIPhase.MidBlock;
        }
    }
}
