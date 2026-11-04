using System;

namespace OpenFifa.Core
{
    /// <summary>One predicted trajectory sample (ground plane).</summary>
    public readonly struct TrajectorySample
    {
        public readonly float Time;
        public readonly SimVec3 Position;
        public readonly SimVec3 Velocity;
        public readonly float Yaw;

        public TrajectorySample(float time, SimVec3 position, SimVec3 velocity, float yaw)
        {
            Time = time;
            Position = position;
            Velocity = velocity;
            Yaw = yaw;
        }
    }

    /// <summary>
    /// Future trajectory for motion-matching queries, from the sim's current state and intent.
    /// Critically damped springs (Holden, "Spring-It-On" / "Code vs Data Driven Displacement"), closed form, so
    /// the result is exact for any horizon and independent of frame rate.
    /// </summary>
    public sealed class TrajectoryPredictor
    {
        /// <summary>Velocity halflife (s): how fast the predicted velocity converges to the desired velocity.</summary>
        public float VelocityHalflife = 0.2f;
        /// <summary>Facing halflife (s).</summary>
        public float YawHalflife = 0.15f;

        private static float HalflifeToDamping(float halflife) => (4f * 0.69314718f) / (halflife + 1e-5f);

        /// <summary>Predict a sample at time t (s) ahead.</summary>
        /// <param name="position">Current ground position.</param>
        /// <param name="velocity">Current horizontal velocity.</param>
        /// <param name="acceleration">Current horizontal acceleration (zero if unknown).</param>
        /// <param name="desiredVelocity">Intent velocity the player is converging to.</param>
        /// <param name="yaw">Current facing yaw (deg).</param>
        /// <param name="yawRate">Current yaw rate (deg/s).</param>
        /// <param name="desiredYaw">Intent facing yaw (deg).</param>
        public TrajectorySample Predict(SimVec3 position, SimVec3 velocity, SimVec3 acceleration, SimVec3 desiredVelocity,
            float yaw, float yawRate, float desiredYaw, float t)
        {
            float y = HalflifeToDamping(VelocityHalflife) * 0.5f;
            float eydt = (float)Math.Exp(-y * t);
            SimVec3 v = velocity.Flat, a = acceleration.Flat, vg = desiredVelocity.Flat;
            SimVec3 j0 = v - vg;
            SimVec3 j1 = a + j0 * y;
            SimVec3 pos = position
                + eydt * ((-1f * j1) / (y * y) + ((-1f * j0) - j1 * t) / y)
                + j1 / (y * y) + j0 / y + vg * t;
            SimVec3 vel = eydt * (j0 + j1 * t) + vg;

            float yy = HalflifeToDamping(YawHalflife) * 0.5f;
            float e2 = (float)Math.Exp(-yy * t);
            float k0 = AnimMath.DeltaAngle(desiredYaw, yaw);
            float k1 = yawRate + k0 * yy;
            float predYaw = AnimMath.WrapAngle(e2 * (k0 + k1 * t) + desiredYaw);
            return new TrajectorySample(t, new SimVec3(pos.X, position.Y, pos.Z), vel, predYaw);
        }

        /// <summary>Fill <paramref name="output"/> with predictions at each horizon in <paramref name="times"/>.</summary>
        public void PredictMany(SimVec3 position, SimVec3 velocity, SimVec3 acceleration, SimVec3 desiredVelocity,
            float yaw, float yawRate, float desiredYaw, float[] times, TrajectorySample[] output)
        {
            for (int i = 0; i < times.Length && i < output.Length; i++)
                output[i] = Predict(position, velocity, acceleration, desiredVelocity, yaw, yawRate, desiredYaw, times[i]);
        }
    }
}
