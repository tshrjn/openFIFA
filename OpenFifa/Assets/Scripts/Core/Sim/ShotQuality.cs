using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// xG-style shot quality from distance and the angle the goal mouth subtends. A hand-tuned logistic,
    /// NOT fitted to data: ~0.75 from 6 m central, ~0.4 from the penalty spot in open play, ~0.08 from 25 m.
    /// </summary>
    public static class ShotQuality
    {
        /// <param name="shooter">Shooter (or ball) position.</param>
        /// <param name="attackSign">+1 if the shooter attacks the +Z goal, -1 for the -Z goal.</param>
        public static float Estimate(SimVec3 shooter, int attackSign, PitchSpec pitch, float pressure, bool header)
        {
            float goalZ = attackSign * pitch.HalfLength;
            float half = pitch.GoalWidth * 0.5f;
            float dz = SimMath.Abs(goalZ - shooter.Z);
            float dist = SimMath.Sqrt(dz * dz + shooter.X * shooter.X);
            float angle = MouthAngle(shooter.X, dz, half);
            float z = -0.2f - 0.11f * dist + 1.6f * angle;
            if (header) z -= 0.9f;
            float xg = SimMath.Logistic(z) * (1f - 0.35f * SimMath.Clamp01(pressure));
            return SimMath.Clamp(xg, 0.005f, 0.97f);
        }

        /// <summary>Angle (radians) between the lines from the shooter to each post.</summary>
        public static float MouthAngle(float x, float depth, float halfGoal)
        {
            if (depth < 0.01f) return SimMath.Abs(x) < halfGoal ? SimMath.Pi : 0f;
            float a1 = (float)Math.Atan2(halfGoal - x, depth);
            float a2 = (float)Math.Atan2(-halfGoal - x, depth);
            return SimMath.Abs(a1 - a2);
        }
    }
}
