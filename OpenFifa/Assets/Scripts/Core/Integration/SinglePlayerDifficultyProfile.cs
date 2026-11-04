namespace OpenFifa.Core
{
    /// <summary>
    /// Everything a single-player difficulty level changes. The team AI side (opponent/teammate theta and the
    /// opponent reaction bonus) is the <see cref="TeamAIDifficultyLadder"/> entry carried in <see cref="Ladder"/> and
    /// handed to <see cref="TeamAIDirector.ForSinglePlayer"/>; this profile adds goalkeeper reflexes, the AI outfielders' ratings and user assists,
    /// which the ladder does not cover. Values are starting points for playtest tuning.
    /// </summary>
    public readonly struct SinglePlayerDifficultyProfile
    {
        public readonly TeamAIDifficultyLevel Level;
        /// <summary>Ladder entry (opponent theta, teammate theta, reaction bonus).</summary>
        public readonly TeamAIDifficultyProfile Ladder;
        /// <summary>Added to the AI keeper's GkReflexes and GkDiving (1-99 scale) before kick-off.</summary>
        public readonly int OpponentKeeperReflexDelta;
        /// <summary>Added to the human team's keeper GkReflexes and GkDiving (the human never controls the keeper).</summary>
        public readonly int HumanKeeperReflexDelta;
        /// <summary>
        /// Added to every AI outfield player's outfield ratings (pace, technique, shooting, defending; 1-99 scale) before
        /// kick-off: the ladder's lever beyond theta, which flattens out at the top (theta 0.75 and 1.0 play alike).
        /// </summary>
        public readonly int OpponentOutfieldDelta;
        /// <summary>
        /// Added to every AI outfield player's finishing ratings (Finishing, LongShots, Composure; 1-99) on top of
        /// <see cref="OpponentOutfieldDelta"/>: the easy levels make the AI's shots less accurate instead of weakening
        /// the user's own keeper. Hidden like the outfield delta: only the fully AI side, never displayed.
        /// </summary>
        public readonly int OpponentFinishingDelta;
        /// <summary>Pass aim assist 0 (manual) .. 1 (fully assisted), fed to <see cref="ControlSettings.AssistLevel"/>.</summary>
        public readonly float PassAssist;
        /// <summary>Shot aim assist 0..1 for <see cref="AimAssist.ShotTarget"/>.</summary>
        public readonly float ShotAssist;
        /// <summary>Switch to the receiver when a pass reaches a teammate.</summary>
        public readonly bool AutoSwitchOnReception;
        /// <summary>On defence, auto-switch to the best interceptor when the controlled player is this far from the ball (m). 0 = off.</summary>
        public readonly float AutoSwitchDefenceDistance;

        public SinglePlayerDifficultyProfile(TeamAIDifficultyLevel level, TeamAIDifficultyProfile ladder,
            int opponentKeeperReflexDelta, int humanKeeperReflexDelta, float passAssist,
            float shotAssist, bool autoSwitchOnReception, float autoSwitchDefenceDistance, int opponentOutfieldDelta = 0,
            int opponentFinishingDelta = 0)
        {
            OpponentFinishingDelta = opponentFinishingDelta;
            OpponentOutfieldDelta = opponentOutfieldDelta;
            Level = level;
            Ladder = ladder;
            OpponentKeeperReflexDelta = opponentKeeperReflexDelta;
            HumanKeeperReflexDelta = humanKeeperReflexDelta;
            PassAssist = passAssist;
            ShotAssist = shotAssist;
            AutoSwitchOnReception = autoSwitchOnReception;
            AutoSwitchDefenceDistance = autoSwitchDefenceDistance;
        }

        public float OpponentTheta => Ladder.OpponentTheta;
        public float TeammateTheta => Ladder.TeammateTheta;
        public string DisplayName => TeamAIDifficultyLadder.DisplayName(Level);
    }

    /// <summary>Per-level table for <see cref="SinglePlayerDifficultyProfile"/>.</summary>
    public static class SinglePlayerDifficulty
    {
        //                                         Beg   Ama   Semi  Pro   WC    Leg   Ult
        private static readonly int[] OppKeeper = { -6, -5, 0, 5, 5, 5, 5 };
        // The user's own keeper is only weakened from World Class up; the easy levels lower AI finishing instead.
        private static readonly int[] HumanKeeper = { 0, 0, 0, 0, -3, -6, -9 };
        private static readonly int[] OppFinishing = { -8, -7, -3, -2, 0, 0, 0 };
        private static readonly float[] PassAssists = { 1.0f, 0.9f, 0.8f, 0.6f, 0.5f, 0.4f, 0.35f };
        private static readonly float[] ShotAssists = { 0.95f, 0.85f, 0.75f, 0.6f, 0.5f, 0.4f, 0.35f };
        private static readonly float[] DefenceSwitch = { 18f, 22f, 26f, 0f, 0f, 0f, 0f };
        private static readonly int[] OppOutfield = { -18, -11, -6, 0, 6, 12, 15 };

        public static SinglePlayerDifficultyProfile For(TeamAIDifficultyLevel level)
        {
            var ladder = TeamAIDifficultyLadder.For(level);
            int i = (int)ladder.Level;
            return new SinglePlayerDifficultyProfile(ladder.Level, ladder, OppKeeper[i], HumanKeeper[i],
                PassAssists[i], ShotAssists[i], true, DefenceSwitch[i], OppOutfield[i], OppFinishing[i]);
        }
    }
}
