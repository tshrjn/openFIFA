using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Engine-free 3D vector for the deterministic match simulation.
    /// Axes match Unity: X = across the pitch (touchline to touchline), Y = up, Z = along the pitch (goal to goal).
    /// </summary>
    public readonly struct SimVec3 : IEquatable<SimVec3>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public static readonly SimVec3 Zero = new SimVec3(0f, 0f, 0f);
        public static readonly SimVec3 Up = new SimVec3(0f, 1f, 0f);

        public SimVec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float SqrMagnitude => X * X + Y * Y + Z * Z;
        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);
        public SimVec3 Flat => new SimVec3(X, 0f, Z);

        public SimVec3 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 1e-6f ? this / m : Zero;
            }
        }

        public static SimVec3 operator +(SimVec3 a, SimVec3 b) => new SimVec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static SimVec3 operator -(SimVec3 a, SimVec3 b) => new SimVec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static SimVec3 operator -(SimVec3 a) => new SimVec3(-a.X, -a.Y, -a.Z);
        public static SimVec3 operator *(SimVec3 a, float s) => new SimVec3(a.X * s, a.Y * s, a.Z * s);
        public static SimVec3 operator *(float s, SimVec3 a) => a * s;
        public static SimVec3 operator /(SimVec3 a, float s) => new SimVec3(a.X / s, a.Y / s, a.Z / s);

        public static float Dot(SimVec3 a, SimVec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static SimVec3 Cross(SimVec3 a, SimVec3 b) => new SimVec3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

        public static float Distance(SimVec3 a, SimVec3 b) => (a - b).Magnitude;

        public static SimVec3 Lerp(SimVec3 a, SimVec3 b, float t) => a + (b - a) * t;

        public static SimVec3 ClampMagnitude(SimVec3 v, float max)
        {
            float sqr = v.SqrMagnitude;
            return sqr > max * max ? v.Normalized * max : v;
        }

        public bool Equals(SimVec3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is SimVec3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
    }
}
