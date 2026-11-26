using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Natural ground speeds of the locomotion clips on the player model, measured in the Editor with
    /// OpenFifa.Editor.LocomotionClipMeasure (planted-foot speed relative to the hips, UAL2 mannequin). The blend tree
    /// places each clip at its natural speed, so a tree position whose magnitude equals the blended clip speed plays
    /// with zero foot slide at rate 1; <see cref="LocomotionGaitSolver"/> scales the rate for every other speed.
    /// Re-measure and update these after changing a clip, then rebuild the controller.
    /// </summary>
    public static class LocomotionGaitTable
    {
        // Measured 2026-11-04 on UAL2_Standard (contact-foot speed, ankle/toe lowest point; see LocomotionClipMeasure):
        //   Walk_Loop 1.333 s cycle, 0.96-1.09 m/s; Jog_Fwd_Loop 0.933 s, 5.9-6.0 m/s; Sprint_Loop 0.667 s, 8.3-8.9 m/s
        //   (mean trunk lean: walk 12 deg, jog 25 deg, sprint 39 deg). The old table (1.4 / 3.4 / 6.5) played the jog
        //   clip at 3.4 m/s, so its feet covered 5.9 m/s: the "running in place" look.

        /// <summary>UAL1 Walk_Loop (m/s).</summary>
        public const float WalkSpeed = 1.0f;
        /// <summary>UAL1 Jog_Fwd_Loop (m/s). A long-striding run: it covers the sim's whole non-sprint range.</summary>
        public const float JogSpeed = 5.9f;
        /// <summary>UAL1 Sprint_Loop (m/s), with the strong forward lean.</summary>
        public const float SprintSpeed = 8.5f;
        /// <summary>Generated jockey shuffle (Gen_Jockey_R_Loop), sideways (m/s), measured 0.71.</summary>
        public const float JockeySpeed = 0.7f;
        /// <summary>Backpedal = walk reversed.</summary>
        public const float BackpedalSpeed = WalkSpeed;
        /// <summary>Normalised time at which the right foot lands in the forward clips (left lands at 0).</summary>
        public const float RightFootStrikePhase = 0.5f;
    }

    /// <summary>How the body moves relative to where it faces.</summary>
    public enum LocomotionMode
    {
        Idle = 0,
        Forward = 1,
        /// <summary>The sim moves the player sideways or backwards while it faces elsewhere (jockey, watching the ball).</summary>
        Strafe = 2,
    }

    /// <summary>
    /// Turns sim speed and direction into blend-tree coordinates and a playback rate for in-place clips:
    ///  - the blend magnitude picks the gait (idle, walk, jog, sprint), the rate makes the feet match the ground speed
    ///    (rate = speed / natural speed of the blend), so feet do not slide at any speed;
    ///  - the jog clip is time-scaled through the sim's jog range and the sprint clip (with its lean) only blends in
    ///    above <see cref="SprintOnset"/>, reaching full weight at <see cref="SprintFull"/>;
    ///  - the direction is forward unless the sim itself moves the player off its facing (strafe/backpedal), with
    ///    hysteresis so a curved run never flickers into a side-step.
    /// Pure and allocation-free; one instance per player (it keeps the strafe hysteresis).
    /// </summary>
    public sealed class LocomotionGaitSolver
    {
        public float WalkSpeed = LocomotionGaitTable.WalkSpeed;
        public float JogSpeed = LocomotionGaitTable.JogSpeed;
        public float SprintSpeed = LocomotionGaitTable.SprintSpeed;
        public float JockeySpeed = LocomotionGaitTable.JockeySpeed;
        public float BackpedalSpeed = LocomotionGaitTable.BackpedalSpeed;

        /// <summary>Below this ground speed (m/s) the player idles.</summary>
        public float IdleSpeed = 0.2f;
        /// <summary>Sprint clip weight starts here (m/s): the sim's jog tops out at ~6.0, a sprint starts at 6.6 ...</summary>
        public float SprintOnset = 6.2f;
        /// <summary>... and is full here (m/s).</summary>
        public float SprintFull = 7.6f;
        /// <summary>Max sprint weight while the sim does not flag a sprint (a fast jog keeps the jog look).</summary>
        public float NonSprintMaxSprintWeight = 0.35f;
        public float MinRate = 0.5f;
        public float MaxRate = 2.0f;
        /// <summary>Angle (deg) between sim facing and velocity above which the sim is strafing; leave below the exit angle.</summary>
        public float StrafeEnterDeg = 40f;
        public float StrafeExitDeg = 25f;
        /// <summary>
        /// Above this speed (m/s) a side/back move is shown as a run in the travel direction instead (nobody shuffles
        /// sideways at 3.6 m/s); the driver then turns the visual body to the velocity.
        /// </summary>
        public float StrafeMaxSpeed = 2.4f;

        public LocomotionMode Mode { get; private set; }

        public void Reset() => Mode = LocomotionMode.Idle;

        /// <summary>Sprint clip weight for a ground speed.</summary>
        public float SprintWeight(float speed, bool sprinting)
        {
            float w = AnimMath.SmoothStep01((speed - SprintOnset) / Math.Max(1e-3f, SprintFull - SprintOnset));
            return sprinting ? w : Math.Min(w, NonSprintMaxSprintWeight);
        }

        /// <summary>
        /// Blend magnitude along the forward axis (in the tree's m/s space) for a ground speed: equal to the speed in the
        /// walk/jog range, held at the jog clip above it, and moved toward the sprint clip by <see cref="SprintWeight"/>.
        /// </summary>
        public float ForwardMagnitude(float speed, bool sprinting)
        {
            if (speed <= JogSpeed) return Math.Max(0f, speed);
            return JogSpeed + SprintWeight(speed, sprinting) * (SprintSpeed - JogSpeed);
        }

        /// <summary>
        /// Rate that makes a blend of magnitude <paramref name="blendMagnitude"/> (the damped value the tree actually
        /// shows) cover <paramref name="speed"/> m/s. Idle (no stride) plays at 1.
        /// </summary>
        public float RateFor(float speed, float blendMagnitude)
        {
            if (speed < IdleSpeed || blendMagnitude < 0.05f) return 1f;
            return AnimMath.Clamp(speed / blendMagnitude, MinRate, MaxRate);
        }

        /// <summary>
        /// Target blend-tree position (strafe = x, forward = z, m/s space) for this frame.
        /// <paramref name="localVelocity"/> is the velocity in the SIM facing frame (x right, z forward): it decides
        /// whether the sim strafes; the visual facing never does.
        /// </summary>
        public void Solve(float localRight, float localForward, bool sprinting, out float strafe, out float forward)
        {
            float speed = (float)Math.Sqrt(localRight * localRight + localForward * localForward);
            if (speed < IdleSpeed)
            {
                Mode = LocomotionMode.Idle;
                strafe = 0f;
                forward = 0f;
                return;
            }
            float offDeg = Math.Abs((float)Math.Atan2(localRight, localForward) * AnimMath.Rad2Deg);
            bool was = Mode == LocomotionMode.Strafe;
            bool strafing = was
                ? offDeg > StrafeExitDeg && speed < StrafeMaxSpeed + 0.3f
                : offDeg > StrafeEnterDeg && speed < StrafeMaxSpeed;
            Mode = strafing ? LocomotionMode.Strafe : LocomotionMode.Forward;
            if (!strafing)
            {
                strafe = 0f;
                forward = ForwardMagnitude(speed, sprinting);
                return;
            }
            // Strafe/backpedal: place the blend on the side (jockey) / back (walk reversed) / forward (walk) clips at
            // their natural speed for this direction, or below it (toward idle) when slower; the rate covers the rest.
            float dx = localRight / speed, dz = localForward / speed;
            float natural = Math.Abs(dx) * JockeySpeed + (dz < 0f ? -dz * BackpedalSpeed : dz * WalkSpeed);
            float m = Math.Min(speed, Math.Max(0.1f, natural));
            strafe = dx * m;
            forward = dz * m;
        }
    }
}
