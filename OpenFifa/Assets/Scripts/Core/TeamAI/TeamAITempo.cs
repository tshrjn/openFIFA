using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Off-ball tempo: how fast players move when they are not pressing, chasing or running in behind. Real outfield
    /// players average about 2 m/s over a match and walk or stand 40-50 % of the time; sprints are for pressing triggers,
    /// chases, runs in behind and recovery. The sim's plain "jog" is 62 % of a player's top speed (4-6 m/s), so without a
    /// limit every 4 m shape correction became a run. These limits (m/s, applied by the adapter on non-sprint moves):
    /// <list type="bullet">
    /// <item>shape holders walk small corrections and jog larger ones (<see cref="ShapeSpeed"/>), slower far from the
    /// ball, quicker when defending near our goal or in a transition; a long shift only becomes a sprint when it is
    /// urgent (out of possession, ball in our half, spot more than <see cref="UrgentShiftDistance"/> m away);</item>
    /// <item>an attack in progress pushes on (<see cref="AttackUrgency"/>); supporting players jog (<see cref="SupportSpeed"/>),
    /// cover-shadow players jog with the lane;</item>
    /// <item>the limit never drops below the spot's own speed plus a margin, so nobody falls behind a sliding block.</item>
    /// </list>
    /// Scaled by difficulty (<see cref="DifficultyFactor"/>: a stronger side shifts quicker). Pure, deterministic, allocation-free.
    /// </summary>
    public static class TeamAITempo
    {
        public const float WalkSpeed = 1.1f;
        /// <summary>A brisk walk (m/s), reached <see cref="BriskWalkDistance"/> m from the spot.</summary>
        public const float BriskWalk = 1.9f;
        public const float BriskWalkDistance = 7f;
        public const float EasyJog = 2.6f;
        public const float Jog = 3.4f;
        /// <summary>Distance (m) at which a holder reaches an easy jog.</summary>
        public const float WalkToJogDistance = 13f;
        /// <summary>A shape spot this far away (m) is a real shift: full jog.</summary>
        public const float ShiftDistance = 20f;
        public const float UrgentShiftDistance = 18f;
        /// <summary>Margin (m/s) over a moving spot's own speed, so a holder keeps up with the block.</summary>
        public const float TrackMargin = 0.3f;
        public const float SupportSprintDistance = 16f;
        public const float MaxShadowSpeed = 4.5f;
        /// <summary>Tempo multiplier for an attack in progress (Progression, FinalThird).</summary>
        public const float AttackUrgency = 1.4f;

        /// <summary>
        /// Difficulty factor on all limits and on the block's slide speed: 0.8 (theta 0) to 1.2 (theta 1). A weak side
        /// shifts late and leaves space; a strong one is always in shape.
        /// </summary>
        public static float DifficultyFactor(float theta) => 0.8f + 0.4f * TeamAIMath.Clamp01(theta);

        /// <summary>Speed limit for a shape holder going to <paramref name="target"/>; may set or clear the sprint flag.</summary>
        public static float ShapeSpeed(TeamAIWorld world, int team, in TeamAIPlayer p, SimVec3 target, SimVec3 targetVelocity,
            TeamAIPhase phase, float theta, bool deadBall, ref bool sprint)
        {
            float d = TeamAIMath.FlatDistance(p.Position, target);
            var ball = TeamAIMath.Flat(world.Ball.Position);
            float ballDist = TeamAIMath.FlatDistance(p.Position, ball);
            int sign = world.AttackSign(team);
            float ballDepth = TeamAIMath.Depth(ball.Z, sign, world.Pitch);
            bool inPossession = TeamAIFormationShape.InPossession(phase);

            float v;
            if (d <= 2f) v = WalkSpeed;
            else if (d < BriskWalkDistance) v = WalkSpeed + (BriskWalk - WalkSpeed) * (d - 2f) / (BriskWalkDistance - 2f);
            else if (d < WalkToJogDistance) v = BriskWalk + (EasyJog - BriskWalk) * (d - BriskWalkDistance) / (WalkToJogDistance - BriskWalkDistance);
            else if (d < ShiftDistance) v = EasyJog + (Jog - EasyJog) * (d - WalkToJogDistance) / (ShiftDistance - WalkToJogDistance);
            else v = Jog;

            // Urgency: defending with the ball in our half, or a transition, moves quicker; play far away, slower.
            bool transition = phase == TeamAIPhase.Recovery || phase == TeamAIPhase.CounterPress;
            if (!inPossession && (ballDepth < 0.45f || transition)) v *= 1.15f;
            // Attacking (Progression, FinalThird): the team pushes on at a run, so the carrier has options.
            bool attacking = phase == TeamAIPhase.Progression || phase == TeamAIPhase.FinalThird;
            if (attacking) v *= AttackUrgency;
            if (ballDist > 35f) v *= 0.8f;
            if (deadBall && d <= ShiftDistance) v = Math.Min(v, EasyJog);
            v *= DifficultyFactor(theta);

            // Never fall behind a moving spot.
            float keep = targetVelocity.Magnitude + TrackMargin;
            if (v < keep) v = keep;

            // Sprint only for an urgent long shift.
            sprint = sprint && !deadBall && !inPossession && d > UrgentShiftDistance && ballDepth < 0.5f;
            return v;
        }

        /// <summary>Supporters jog: 3 m/s plus 0.15 m/s per metre to the spot, up to 5.5 m/s, times the difficulty factor.</summary>
        public static float SupportSpeed(float distance, float theta) =>
            TeamAIMath.Clamp(3.0f + 0.15f * distance, 3.0f, 5.5f) * DifficultyFactor(theta);

        /// <summary>Cover-shadow players jog with the lane they cut: its speed plus a margin, at least an easy jog.</summary>
        public static float CoverSpeed(float distance, float laneSpeed, float theta) =>
            Math.Max(EasyJog + 0.1f * distance, laneSpeed + TrackMargin) * DifficultyFactor(theta);
    }
}
