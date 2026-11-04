namespace OpenFifa.Core
{
    /// <summary>
    /// Runs both team brains at a fixed decision rate (default 10 Hz) over a shared, time-sliced pitch-control
    /// grid, and keeps sticky possession. Feed it the world every sim step; read Commands.
    /// </summary>
    public sealed class TeamAIDirector
    {
        public const float DefaultDecisionInterval = 0.1f;

        public readonly TeamAIBrain[] Brains = new TeamAIBrain[2];
        public readonly TeamAIPitchControl PitchControl;
        /// <summary>Latest commands, indexed like world.Players.</summary>
        public readonly AIPlayerCommand[] Commands = new AIPlayerCommand[TeamAIWorld.MaxPlayers];
        public float DecisionInterval = DefaultDecisionInterval;
        /// <summary>Grid rows refreshed per decision tick (21 rows / 7 = full refresh every 0.3 s).</summary>
        public int RowsPerSlice = 7;
        /// <summary>Team in possession (sticky through passes and loose balls), -1 before the first touch.</summary>
        public int PossessionTeam { get; private set; } = -1;
        public int DecisionTicks { get; private set; }

        private float _accumulator;
        private bool _gridPrimed;

        public TeamAIDirector(PitchSpec pitch, TeamAITactics home, TeamAITactics away, float homeDifficulty,
            float awayDifficulty, ulong seed)
            : this(pitch, home, away, new TeamAIDifficulty(homeDifficulty), new TeamAIDifficulty(awayDifficulty), seed) { }

        public TeamAIDirector(PitchSpec pitch, TeamAITactics home, TeamAITactics away, TeamAIDifficulty homeDifficulty,
            TeamAIDifficulty awayDifficulty, ulong seed)
        {
            PitchControl = new TeamAIPitchControl(pitch);
            Brains[0] = new TeamAIBrain(0, home, homeDifficulty, seed * 2UL + 1UL);
            Brains[1] = new TeamAIBrain(1, away, awayDifficulty, seed * 2UL + 2UL);
        }

        /// <summary>
        /// Single-player director: the AI opponents of <paramref name="humanTeam"/> get the ladder's opponent difficulty
        /// (theta plus OpponentReactionBonus), the human's AI teammates get the teammate difficulty.
        /// </summary>
        public static TeamAIDirector ForSinglePlayer(PitchSpec pitch, TeamAITactics home, TeamAITactics away,
            int humanTeam, in TeamAIDifficultyProfile profile, ulong seed)
        {
            var opp = profile.OpponentDifficulty;
            var mate = profile.TeammateDifficulty;
            return humanTeam == 0
                ? new TeamAIDirector(pitch, home, away, mate, opp, seed)
                : new TeamAIDirector(pitch, home, away, opp, mate, seed);
        }

        /// <summary>Advance by dt. Returns true when a decision tick ran and Commands were refreshed.</summary>
        public bool Tick(TeamAIWorld world, float dt)
        {
            _accumulator += dt;
            if (_accumulator + 1e-5f < DecisionInterval) return false;
            float step = _accumulator;
            _accumulator = 0f;
            DecideNow(world, step);
            return true;
        }

        /// <summary>Run a decision tick immediately (tests, restarts).</summary>
        public void DecideNow(TeamAIWorld world, float dt)
        {
            UpdatePossession(world);
            if (!_gridPrimed)
            {
                PitchControl.RebuildAll(world);
                _gridPrimed = true;
            }
            else
            {
                PitchControl.UpdateSlice(world, RowsPerSlice);
            }
            Brains[0].Decide(world, PitchControl, dt, PossessionTeam == 0, Commands);
            Brains[1].Decide(world, PitchControl, dt, PossessionTeam == 1, Commands);
            DecisionTicks++;
        }

        /// <summary>Force possession (kick-off, restarts, tests).</summary>
        public void SetPossession(int team) => PossessionTeam = team;

        private void UpdatePossession(TeamAIWorld world)
        {
            if (world.MatchPhase != TeamAIMatchPhase.InPlay && world.RestartTeam >= 0)
            {
                PossessionTeam = world.RestartTeam;
                return;
            }
            int ownerTeam = world.OwnerTeam;
            if (ownerTeam >= 0) PossessionTeam = ownerTeam;
            else if (PossessionTeam < 0 && world.Ball.LastTouchTeam >= 0) PossessionTeam = world.Ball.LastTouchTeam;
        }
    }
}
