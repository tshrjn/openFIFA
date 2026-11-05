namespace OpenFifa.Core
{
    /// <summary>
    /// Shape of one skill move: timing, the fake, the body path and the ball release. Fractions are of
    /// <see cref="Duration"/>. Values are game-feel APPROXIMATIONS, tuned so a move takes roughly the time the real one
    /// takes at match speed (0.4-0.8 s) and exits at the angle it is known for.
    /// </summary>
    public readonly struct SkillMoveProfile
    {
        public readonly int RequiredStars;
        public readonly float Duration;
        /// <summary>When the fake is sold (defenders may bite), or negative for a move without a fake.</summary>
        public readonly float FakeAt;
        /// <summary>Exit heading relative to the entry heading, toward the chosen side (degrees).</summary>
        public readonly float ExitAngleDeg;
        /// <summary>Fraction at which the body swings from the entry heading onto the exit heading.</summary>
        public readonly float TurnStart;
        /// <summary>Speed kept through the set-up, as a fraction of the entry speed.</summary>
        public readonly float SetupSpeedFactor;
        /// <summary>Speed at the end of the move as a fraction of the player's sprint speed (the burst).</summary>
        public readonly float ExitSpeedFraction;
        /// <summary>Extra full body turns on top of the heading change (a roulette spins once).</summary>
        public readonly float SpinTurns;
        /// <summary>The body keeps facing the entry heading and side-steps (ball roll).</summary>
        public readonly bool KeepFacing;
        /// <summary>Ball distance from the feet along the exit at the end of the move (m).</summary>
        public readonly float BallLead;
        /// <summary>Release touch: ball speed over the player's speed (m/s).</summary>
        public readonly float ReleasePush;
        /// <summary>Tackle win-logit shift for a defender challenging during the move (positive: harder to tackle).</summary>
        public readonly float TackleEvasion;
        /// <summary>How convincing the fake is (scales the defender's bite chance logit).</summary>
        public readonly float FakeStrength;

        public SkillMoveProfile(int stars, float duration, float fakeAt, float exitDeg, float turnStart, float setupSpeed,
            float exitSpeed, float spin, bool keepFacing, float ballLead, float push, float evasion, float fake)
        {
            RequiredStars = stars;
            Duration = duration;
            FakeAt = fakeAt;
            ExitAngleDeg = exitDeg;
            TurnStart = turnStart;
            SetupSpeedFactor = setupSpeed;
            ExitSpeedFraction = exitSpeed;
            SpinTurns = spin;
            KeepFacing = keepFacing;
            BallLead = ballLead;
            ReleasePush = push;
            TackleEvasion = evasion;
            FakeStrength = fake;
        }

        public bool HasFake => FakeAt >= 0f;
    }

    /// <summary>
    /// The skill-move table, the right-stick mapping and the star gating. Pure data and lookups.
    /// </summary>
    public static class SkillMoveCatalog
    {
        public const int MinStars = 1;
        public const int MaxStars = 5;

        private static readonly SkillMoveProfile None = default;

        //                                                stars dur   fake  exit  turn  setup exit  spin  keep   lead  push  evade fake
        private static readonly SkillMoveProfile BodyFeintP = new SkillMoveProfile(1, 0.45f, 0.30f, 50f, 0.55f, 0.55f, 0.80f, 0f, false, 0.55f, 1.6f, 0.0f, 0.9f);
        private static readonly SkillMoveProfile StepoverP = new SkillMoveProfile(2, 0.55f, 0.40f, 45f, 0.60f, 0.45f, 0.85f, 0f, false, 0.65f, 1.8f, 0.0f, 1.2f);
        private static readonly SkillMoveProfile BallRollP = new SkillMoveProfile(2, 0.50f, -1f, 90f, 0.05f, 0.35f, 0.45f, 0f, true, 0.35f, 0.8f, 0.2f, 0f);
        private static readonly SkillMoveProfile DragBackP = new SkillMoveProfile(2, 0.60f, -1f, 180f, 0.45f, 0.15f, 0.50f, 0f, false, 0.45f, 1.0f, 0.3f, 0f);
        private static readonly SkillMoveProfile HeelChopP = new SkillMoveProfile(3, 0.45f, 0.25f, 120f, 0.40f, 0.30f, 0.70f, 0f, false, 0.55f, 1.5f, 0.1f, 1.1f);
        private static readonly SkillMoveProfile RouletteP = new SkillMoveProfile(4, 0.70f, 0.35f, 80f, 0.30f, 0.40f, 0.60f, 1f, false, 0.50f, 1.4f, 0.6f, 0.8f);
        private static readonly SkillMoveProfile FakeShotP = new SkillMoveProfile(2, 0.50f, 0.35f, 70f, 0.55f, 0.25f, 0.70f, 0f, false, 0.60f, 1.6f, -0.1f, 1.4f);

        public static SkillMoveProfile Get(SkillMoveKind kind)
        {
            switch (kind)
            {
                case SkillMoveKind.BodyFeint: return BodyFeintP;
                case SkillMoveKind.Stepover: return StepoverP;
                case SkillMoveKind.BallRoll: return BallRollP;
                case SkillMoveKind.DragBack: return DragBackP;
                case SkillMoveKind.HeelChop: return HeelChopP;
                case SkillMoveKind.Roulette: return RouletteP;
                case SkillMoveKind.FakeShot: return FakeShotP;
                default: return None;
            }
        }

        public static int RequiredStars(SkillMoveKind kind) => kind == SkillMoveKind.None ? 0 : Get(kind).RequiredStars;

        /// <summary>
        /// The move a player with <paramref name="stars"/> skill stars actually performs when he asks for
        /// <paramref name="kind"/>: the move itself if he has the stars, else the nearest simpler move of the same
        /// family (roulette -> ball roll, heel chop -> drag back, everything -> body feint).
        /// </summary>
        public static SkillMoveKind Gate(SkillMoveKind kind, int stars)
        {
            if (kind == SkillMoveKind.None) return kind;
            stars = SimMath.Clamp(stars, MinStars, MaxStars);
            while (kind != SkillMoveKind.BodyFeint && RequiredStars(kind) > stars)
            {
                switch (kind)
                {
                    case SkillMoveKind.Roulette: kind = SkillMoveKind.BallRoll; break;
                    case SkillMoveKind.HeelChop: kind = SkillMoveKind.DragBack; break;
                    default: kind = SkillMoveKind.BodyFeint; break;
                }
            }

            return kind;
        }

        /// <summary>
        /// Right-stick flick to move, relative to the carrier's facing. <paramref name="relativeDeg"/> is the flick
        /// direction minus the facing (positive = to his right):
        /// forward = stepover, forward diagonal = body feint, side = ball roll (roulette while sprinting),
        /// back diagonal = heel chop, straight back = drag back. The result is not gated yet (see <see cref="Gate"/>).
        /// </summary>
        public static SkillMoveKind FromFlick(float relativeDeg, bool sprint)
        {
            float a = SimMath.Abs(relativeDeg);
            if (a <= 22.5f) return SkillMoveKind.Stepover;
            if (a <= 67.5f) return SkillMoveKind.BodyFeint;
            if (a <= 112.5f) return sprint ? SkillMoveKind.Roulette : SkillMoveKind.BallRoll;
            if (a <= 157.5f) return SkillMoveKind.HeelChop;
            return SkillMoveKind.DragBack;
        }
    }
}
