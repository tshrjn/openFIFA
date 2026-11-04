using System;

namespace OpenFifa.Core
{
    /// <summary>Engine-free 2D vector for stick, touch and screen-space maths.</summary>
    public readonly struct ControlVec2 : IEquatable<ControlVec2>
    {
        public readonly float X;
        public readonly float Y;

        public static readonly ControlVec2 Zero = new ControlVec2(0f, 0f);
        public static readonly ControlVec2 Up = new ControlVec2(0f, 1f);

        public ControlVec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float SqrMagnitude => X * X + Y * Y;
        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

        public ControlVec2 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 1e-6f ? new ControlVec2(X / m, Y / m) : Zero;
            }
        }

        public static ControlVec2 operator +(ControlVec2 a, ControlVec2 b) => new ControlVec2(a.X + b.X, a.Y + b.Y);
        public static ControlVec2 operator -(ControlVec2 a, ControlVec2 b) => new ControlVec2(a.X - b.X, a.Y - b.Y);
        public static ControlVec2 operator *(ControlVec2 a, float s) => new ControlVec2(a.X * s, a.Y * s);

        public static float Dot(ControlVec2 a, ControlVec2 b) => a.X * b.X + a.Y * b.Y;

        public static ControlVec2 Lerp(ControlVec2 a, ControlVec2 b, float t) => a + (b - a) * t;

        public static ControlVec2 ClampMagnitude(ControlVec2 v, float max)
            => v.SqrMagnitude > max * max ? v.Normalized * max : v;

        /// <summary>
        /// Radial dead zone with rescale: below <paramref name="deadZone"/> returns zero; above it the
        /// magnitude is remapped from [deadZone, 1] to [0, 1] so there is no jump at the edge.
        /// </summary>
        public static ControlVec2 ApplyRadialDeadZone(ControlVec2 v, float deadZone)
        {
            float m = v.Magnitude;
            if (m <= deadZone || m < 1e-6f) return Zero;
            float clamped = Math.Min(m, 1f);
            float scaled = deadZone >= 1f ? 0f : (clamped - deadZone) / (1f - deadZone);
            return v * (scaled / m);
        }

        /// <summary>Eight-way direction of a vector; <see cref="Direction8.None"/> for a near-zero vector.</summary>
        public static Direction8 ToDirection8(ControlVec2 v)
        {
            if (v.SqrMagnitude < 1e-6f) return Direction8.None;
            double deg = Math.Atan2(v.X, v.Y) * 180.0 / Math.PI; // 0 = up, clockwise positive
            if (deg < 0) deg += 360.0;
            int sector = (int)Math.Floor((deg + 22.5) / 45.0) % 8;
            return (Direction8)(sector + 1);
        }

        public bool Equals(ControlVec2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is ControlVec2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X:F3}, {Y:F3})";
    }

    /// <summary>Axis-aligned rectangle in normalised screen space (0..1, origin bottom-left).</summary>
    public readonly struct ControlRect
    {
        public readonly float XMin;
        public readonly float YMin;
        public readonly float XMax;
        public readonly float YMax;

        public ControlRect(float xMin, float yMin, float xMax, float yMax)
        {
            XMin = xMin;
            YMin = yMin;
            XMax = xMax;
            YMax = yMax;
        }

        public bool Contains(ControlVec2 p) => p.X >= XMin && p.X <= XMax && p.Y >= YMin && p.Y <= YMax;

        public ControlRect MirroredX() => new ControlRect(1f - XMax, YMin, 1f - XMin, YMax);
    }
}
