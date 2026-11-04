using System;

namespace OpenFifa.Core
{
    /// <summary>Small deterministic math helpers for the match sim (flat XZ-plane work, clamping, angles).</summary>
    public static class SimMath
    {
        public const float Pi = 3.14159265f;
        public const float TwoPi = 6.28318531f;
        public const float Deg2Rad = Pi / 180f;

        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Sqrt(float v) => v <= 0f ? 0f : (float)Math.Sqrt(v);
        public static float Abs(float v) => v < 0f ? -v : v;
        public static float Sign(float v) => v < 0f ? -1f : 1f;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Abs(target - current) <= maxDelta) return target;
            return current + Sign(target - current) * maxDelta;
        }

        /// <summary>Yaw (radians) of a flat direction. 0 = +Z, positive turns towards +X (clockwise seen from above).</summary>
        public static float Yaw(SimVec3 dir) => (float)Math.Atan2(dir.X, dir.Z);

        /// <summary>Unit flat vector for a yaw angle (inverse of <see cref="Yaw"/>).</summary>
        public static SimVec3 FromYaw(float yaw) => new SimVec3((float)Math.Sin(yaw), 0f, (float)Math.Cos(yaw));

        /// <summary>Wraps an angle to (-pi, pi].</summary>
        public static float WrapAngle(float a)
        {
            while (a > Pi) a -= TwoPi;
            while (a <= -Pi) a += TwoPi;
            return a;
        }

        /// <summary>Signed yaw difference from <paramref name="from"/> to <paramref name="to"/>, in (-pi, pi].</summary>
        public static float DeltaAngle(float from, float to) => WrapAngle(to - from);

        /// <summary>Rotates a vector about +Y by yaw radians (same sense as <see cref="Yaw"/>).</summary>
        public static SimVec3 RotateY(SimVec3 v, float yaw)
        {
            float c = (float)Math.Cos(yaw);
            float s = (float)Math.Sin(yaw);
            return new SimVec3(v.X * c + v.Z * s, v.Y, -v.X * s + v.Z * c);
        }

        /// <summary>Unsigned angle in radians between two flat directions (0 if either is zero).</summary>
        public static float FlatAngle(SimVec3 a, SimVec3 b)
        {
            SimVec3 fa = a.Flat.Normalized;
            SimVec3 fb = b.Flat.Normalized;
            if (fa.SqrMagnitude < 0.5f || fb.SqrMagnitude < 0.5f) return 0f;
            float d = Clamp(SimVec3.Dot(fa, fb), -1f, 1f);
            return (float)Math.Acos(d);
        }

        public static float FlatDistance(SimVec3 a, SimVec3 b)
        {
            float dx = a.X - b.X;
            float dz = a.Z - b.Z;
            return Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Exponential decay factor exp(-dt / tau), with tau &lt;= 0 meaning no decay.</summary>
        public static float Decay(float dt, float tau) => tau <= 0f ? 1f : (float)Math.Exp(-dt / tau);

        public static float Logistic(float z) => 1f / (1f + (float)Math.Exp(-z));
    }
}
