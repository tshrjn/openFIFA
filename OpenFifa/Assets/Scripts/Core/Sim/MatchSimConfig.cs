namespace OpenFifa.Core
{
    /// <summary>Top-level match sim configuration. Shared, read-only during a match.</summary>
    public sealed class MatchSimConfig
    {
        /// <summary>Sim ticks per second (player kinematics, rules, intents).</summary>
        public int TickRate = 60;

        /// <summary>Ball substeps per tick: 2 at 60 Hz = 120 Hz ball integration.</summary>
        public int BallSubsteps = 2;

        /// <summary>Length of each half in sim seconds (a short game half maps to 45 displayed minutes).</summary>
        public float HalfDurationSeconds = 360f;

        /// <summary>Dead-ball pause before a restart is set up (s).</summary>
        public float OutOfPlayDelay = 1.0f;
        public float GoalDelay = 3.0f;
        public float FoulDelay = 1.2f;
        public float HalfTimeBreakSeconds = 1.0f;

        /// <summary>If the restart taker does nothing for this long, the sim plays a short pass for them.</summary>
        public float RestartAutoTakeSeconds = 8f;

        /// <summary>Opponents must stand this far from the ball at set pieces (laws: 9.15 m).</summary>
        public float RestartDistance = 9.15f;

        public float PenaltySpotDistance = 11f;
        public float GoalAreaDepth = 5.5f;
        public float CentreCircleRadius = 9.15f;

        public PitchSpec Pitch = PitchSpec.Standard;
        public BallSimConfig Ball = new BallSimConfig();
        public PlayerKinematicsConfig Kinematics = new PlayerKinematicsConfig();
        public SimActionConfig Actions = new SimActionConfig();

        public float Dt => 1f / TickRate;
        public float BallDt => 1f / (TickRate * BallSubsteps);

        public int SecondsToTicks(float seconds) => (int)(seconds * TickRate + 0.5f);

        /// <summary>
        /// A copy of this config whose ball is set up for <paramref name="surface"/> (<see cref="BallSimConfig.WithSurface"/>).
        /// Normal, or a ball already made for a surface, returns this config unchanged. The other sub-configs are shared.
        /// </summary>
        public MatchSimConfig ForSurface(PitchSurface surface)
        {
            if (surface == PitchSurface.Normal || Ball.Surface != PitchSurface.Normal) return this;
            var c = (MatchSimConfig)MemberwiseClone();
            c.Ball = Ball.WithSurface(surface);
            return c;
        }
    }
}
