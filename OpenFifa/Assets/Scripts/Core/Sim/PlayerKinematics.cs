namespace OpenFifa.Core
{
    /// <summary>
    /// Attribute-driven player movement: acceleration curves per <see cref="SimAccelerationType"/>,
    /// braking, speed-dependent turn rate, plant-and-turn slowdown and stamina. Pure and deterministic.
    /// </summary>
    public static class PlayerKinematics
    {
        public static float SprintSpeed(SimPlayerAttributes a, PlayerKinematicsConfig c, float stamina) =>
            (c.SprintSpeedMin + c.SprintSpeedRange * SimPlayerAttributes.N(a.Pace)) * StaminaFactor(c, stamina);

        public static float StaminaFactor(PlayerKinematicsConfig c, float stamina) =>
            SimMath.Lerp(c.ExhaustedFactor, 1f, SimMath.Clamp01(stamina));

        public static float PeakAcceleration(SimPlayerAttributes a, PlayerKinematicsConfig c) =>
            c.AccelMin + c.AccelRange * SimPlayerAttributes.N(a.Acceleration);

        public static float Deceleration(SimPlayerAttributes a, PlayerKinematicsConfig c) =>
            c.DecelMin + c.DecelRange * SimPlayerAttributes.N(a.Agility);

        /// <summary>Max yaw rate (rad/s) at a given speed.</summary>
        public static float TurnRate(SimPlayerAttributes a, PlayerKinematicsConfig c, float speed)
        {
            float agile = 0.75f * SimPlayerAttributes.N(a.Agility) + 0.25f * SimPlayerAttributes.N(a.Balance);
            float rate = (c.TurnRateMin + c.TurnRateRange * agile) / (1f + speed / c.TurnRateHalfSpeed);
            float lateral = c.LateralAccelMin + c.LateralAccelRange * agile;
            return speed > 0.1f ? SimMath.Min(rate, lateral / speed) : rate;
        }

        /// <summary>
        /// Acceleration multiplier at speed fraction f (0 = standing, 1 = top speed) for each profile.
        /// Explosive is front-loaded, Lengthy is back-loaded, Controlled sits between.
        /// </summary>
        public static float AccelerationCurve(SimAccelerationType type, float f)
        {
            f = SimMath.Clamp01(f);
            switch (type)
            {
                case SimAccelerationType.Explosive: return 1.40f - 1.00f * f;
                case SimAccelerationType.Lengthy: return 0.78f - 0.08f * f;
                default: return 1.00f - 0.50f * f;
            }
        }

        /// <summary>
        /// Advances one player by <paramref name="dt"/>.
        /// </summary>
        /// <param name="move">Desired flat direction, magnitude 0..1 is throttle.</param>
        /// <param name="speedCap">Hard cap on speed (dribbling, jockey, shield); use float.MaxValue for none.</param>
        /// <param name="faceDir">If non-zero, the body turns to face this direction (jockey/strafe) instead of the run.</param>
        public static void Step(ref SimPlayer p, SimPlayerAttributes a, PlayerKinematicsConfig c,
            SimVec3 move, bool sprint, float speedCap, SimVec3 faceDir, float dt)
        {
            SimVec3 flatMove = move.Flat;
            float throttle = SimMath.Clamp01(flatMove.Magnitude);
            float sprintSpeed = SprintSpeed(a, c, p.Stamina);
            float top = sprint ? sprintSpeed : sprintSpeed * c.JogFraction;
            float target = SimMath.Min(top * throttle, speedCap);

            SimVec3 vel = p.Velocity.Flat;
            float speed = vel.Magnitude;
            bool strafing = faceDir.SqrMagnitude > 1e-6f;
            // From rest, a strafing (jockeying) player steps straight off in the move direction: his run is free of the
            // body. Starting from the facing instead deadlocked any move more than 90 deg off it (a backward step):
            // plant-and-turn zeroed the speed every tick, so the heading reset to the facing and he never moved.
            float heading = speed > 0.05f ? SimMath.Yaw(vel)
                : (strafing && throttle > 1e-3f ? SimMath.Yaw(flatMove) : p.Facing);
            float turnRate = TurnRate(a, c, speed);

            if (throttle > 1e-3f)
            {
                float desired = SimMath.Yaw(flatMove);
                float delta = SimMath.DeltaAngle(heading, desired);
                // While strafing (jockey) the run direction is free of the body, so turn quickly.
                float maxTurn = (strafing ? turnRate * 2.5f : turnRate) * dt;
                if (speed < 0.5f) maxTurn = SimMath.Max(maxTurn, turnRate * 2f * dt);
                heading += SimMath.Clamp(delta, -maxTurn, maxTurn);
                float remaining = SimMath.DeltaAngle(heading, desired);
                // Plant and turn: the more misaligned, the more speed must be shed first.
                float align = (float)System.Math.Cos(remaining);
                target *= align > 0f ? align : 0f;
            }

            if (target > speed)
            {
                float f = sprintSpeed > 0f ? speed / sprintSpeed : 1f;
                float acc = PeakAcceleration(a, c) * AccelerationCurve(a.AccelerationType, f) * StaminaFactor(c, p.Stamina);
                speed = SimMath.Min(target, speed + acc * dt);
            }
            else
            {
                speed = SimMath.Max(target, speed - Deceleration(a, c) * dt);
            }

            SimVec3 newVel = SimMath.FromYaw(heading) * speed;
            p.Velocity = newVel;
            p.Position = new SimVec3(p.Position.X + newVel.X * dt, 0f, p.Position.Z + newVel.Z * dt);

            // Facing.
            float faceTarget = strafing ? SimMath.Yaw(faceDir) : (speed > 0.3f ? heading : (throttle > 1e-3f ? SimMath.Yaw(flatMove) : p.Facing));
            float faceDelta = SimMath.DeltaAngle(p.Facing, faceTarget);
            float maxFace = turnRate * 1.5f * dt;
            p.Facing = SimMath.WrapAngle(p.Facing + SimMath.Clamp(faceDelta, -maxFace, maxFace));

            // Stamina.
            p.Sprinting = sprint && speed > sprintSpeed * c.JogFraction;
            if (p.Sprinting)
            {
                float drain = c.SprintDrainPerSecond * (1.4f - SimPlayerAttributes.N(a.Stamina));
                p.Stamina = SimMath.Clamp01(p.Stamina - drain * dt);
            }
            else if (speed <= sprintSpeed * c.JogFraction)
            {
                p.Stamina = SimMath.Clamp01(p.Stamina + c.RecoveryPerSecond * dt);
            }
        }
    }
}
