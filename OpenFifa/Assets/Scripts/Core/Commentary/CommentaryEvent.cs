namespace OpenFifa.Core
{
    /// <summary>
    /// One tagged match event fed to <see cref="CommentaryEngine.Push"/>. Fill what you know; unknown numbers stay at
    /// their defaults. Gameplay builds these from sim events; nothing here depends on UnityEngine.
    /// </summary>
    public sealed class CommentaryEvent
    {
        public CommentaryEventType Type { get; set; }

        /// <summary>Real time in seconds (monotonic, e.g. unscaled time since match load). Drives scheduling.</summary>
        public double Time { get; set; }

        /// <summary>Match clock minute (0-90+). Used for "late" contexts.</summary>
        public int Minute { get; set; }

        /// <summary>0 = home, 1 = away. For goals: the team the goal counts for (also for own goals).</summary>
        public int Team { get; set; }

        /// <summary>Main player (scorer, shooter, passer, fouler...). Optional.</summary>
        public string PlayerId { get; set; }

        /// <summary>Name spoken in the stitched name clip (roster KnownAs). Lines with {name} need it.</summary>
        public string PlayerName { get; set; }

        /// <summary>Second player (keeper who saved, player fouled, assist). Informational for now.</summary>
        public string SecondaryPlayerId { get; set; }
        public string SecondaryPlayerName { get; set; }

        /// <summary>Spoken club name of <see cref="Team"/> (roster Name), for "{team}" lines. Optional.</summary>
        public string TeamName { get; set; }

        /// <summary>Score AFTER this event.</summary>
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }

        /// <summary>Chance quality 0..1 (expected-goal style) for shots; 0 when not a shot.</summary>
        public float ShotQuality { get; set; }

        /// <summary>Distance to goal in metres for shots; 0 when unknown.</summary>
        public float DistanceMetres { get; set; }

        public GoalSituation Situation { get; set; }

        /// <summary>Consecutive passes in the move (PassSequence).</summary>
        public int PassCount { get; set; }

        /// <summary>Goals the main player has scored in this match, this one included (filled by the engine for goals).</summary>
        public int PlayerGoals { get; set; }

        /// <summary>Crowd intensity 0..1 at the moment of the event; negative leaves the engine's value unchanged.</summary>
        public float CrowdIntensity { get; set; } = -1f;
    }
}
