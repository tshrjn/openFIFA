using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Small engine-free math helpers for animation sync.
    /// Yaw convention matches Unity: yaw 0 faces +Z, positive yaw turns toward +X (clockwise seen from above), in degrees.
    /// </summary>
    public static class AnimMath
    {
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public const float Rad2Deg = (float)(180.0 / Math.PI);

        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);

        /// <summary>Hermite smoothstep on [0,1].</summary>
        public static float SmoothStep01(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Wrap an angle to (-180, 180].</summary>
        public static float WrapAngle(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            if (deg <= -180f) deg += 360f;
            return deg;
        }

        /// <summary>Shortest signed difference from 'from' to 'to', in degrees.</summary>
        public static float DeltaAngle(float from, float to) => WrapAngle(to - from);

        /// <summary>Yaw (degrees) of a horizontal direction. Returns 0 for a zero vector.</summary>
        public static float YawOf(SimVec3 dir)
        {
            if (dir.X * dir.X + dir.Z * dir.Z < 1e-12f) return 0f;
            return (float)Math.Atan2(dir.X, dir.Z) * Rad2Deg;
        }

        /// <summary>Unit horizontal forward vector for a yaw in degrees.</summary>
        public static SimVec3 Forward(float yawDeg)
        {
            float r = yawDeg * Deg2Rad;
            return new SimVec3((float)Math.Sin(r), 0f, (float)Math.Cos(r));
        }

        /// <summary>Rotate a character-local offset (X right, Y up, Z forward) into world space by yaw.</summary>
        public static SimVec3 LocalToWorld(SimVec3 local, float yawDeg)
        {
            float r = yawDeg * Deg2Rad;
            float c = (float)Math.Cos(r), s = (float)Math.Sin(r);
            return new SimVec3(local.X * c + local.Z * s, local.Y, -local.X * s + local.Z * c);
        }

        /// <summary>Inverse of <see cref="LocalToWorld"/>.</summary>
        public static SimVec3 WorldToLocal(SimVec3 world, float yawDeg) => LocalToWorld(world, -yawDeg);
    }
}
