using System;

namespace OpenFifa.Core
{
    /// <summary>Allocation-free math helpers for the team AI.</summary>
    public static class TeamAIMath
    {
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Abs(float v) => v < 0f ? -v : v;
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Sigmoid(float x) => 1f / (1f + (float)Math.Exp(-x));

        public static float FlatDistance(SimVec3 a, SimVec3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public static float FlatSqrDistance(SimVec3 a, SimVec3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }

        public static SimVec3 FlatDirection(SimVec3 from, SimVec3 to)
        {
            float dx = to.X - from.X, dz = to.Z - from.Z;
            float m = (float)Math.Sqrt(dx * dx + dz * dz);
            return m > 1e-5f ? new SimVec3(dx / m, 0f, dz / m) : SimVec3.Zero;
        }

        public static SimVec3 Flat(SimVec3 v) => new SimVec3(v.X, 0f, v.Z);

        /// <summary>Depth 0..1 along the team's attacking axis: 0 = own goal line, 1 = opponent goal line.</summary>
        public static float Depth(float z, int attackSign, PitchSpec pitch) =>
            (attackSign * z + pitch.HalfLength) / pitch.Length;

        public static float DepthToZ(float depth, int attackSign, PitchSpec pitch) =>
            attackSign * (depth * pitch.Length - pitch.HalfLength);

        /// <summary>Centre of the goal this team attacks.</summary>
        public static SimVec3 OpponentGoal(int attackSign, PitchSpec pitch) =>
            new SimVec3(0f, 0f, attackSign * pitch.HalfLength);

        /// <summary>Centre of the goal this team defends.</summary>
        public static SimVec3 OwnGoal(int attackSign, PitchSpec pitch) =>
            new SimVec3(0f, 0f, -attackSign * pitch.HalfLength);

        public static SimVec3 ClampToPitch(SimVec3 p, PitchSpec pitch, float margin)
        {
            float hx = pitch.HalfWidth - margin, hz = pitch.HalfLength - margin;
            return new SimVec3(Clamp(p.X, -hx, hx), 0f, Clamp(p.Z, -hz, hz));
        }

        /// <summary>Parameter 0..1 of the closest point on segment a-b to p (flat).</summary>
        public static float SegmentParam(SimVec3 a, SimVec3 b, SimVec3 p)
        {
            float abx = b.X - a.X, abz = b.Z - a.Z;
            float len2 = abx * abx + abz * abz;
            if (len2 < 1e-6f) return 0f;
            float t = ((p.X - a.X) * abx + (p.Z - a.Z) * abz) / len2;
            return Clamp01(t);
        }

        /// <summary>Flat perpendicular distance from p to the infinite line through a and b.</summary>
        public static float DistanceToLine(SimVec3 a, SimVec3 b, SimVec3 p)
        {
            float abx = b.X - a.X, abz = b.Z - a.Z;
            float len = (float)Math.Sqrt(abx * abx + abz * abz);
            if (len < 1e-5f) return FlatDistance(a, p);
            float cross = abx * (p.Z - a.Z) - abz * (p.X - a.X);
            return Abs(cross) / len;
        }

        /// <summary>Spearman-style time to intercept: reaction time, then straight run at top speed.</summary>
        public static float TimeToIntercept(in TeamAIPlayer p, SimVec3 target, float reaction)
        {
            float px = p.Position.X + p.Velocity.X * reaction;
            float pz = p.Position.Z + p.Velocity.Z * reaction;
            float dx = target.X - px, dz = target.Z - pz;
            float speed = p.MaxSpeed > 0.5f ? p.MaxSpeed : 0.5f;
            return reaction + (float)Math.Sqrt(dx * dx + dz * dz) / speed;
        }

        /// <summary>Rolling ball prediction with exponential decay (flat, ignores height).</summary>
        public static SimVec3 PredictBall(SimVec3 pos, SimVec3 vel, float t)
        {
            const float c = 0.45f;
            float k = (1f - (float)Math.Exp(-c * t)) / c;
            return new SimVec3(pos.X + vel.X * k, 0f, pos.Z + vel.Z * k);
        }

        /// <summary>Angle (radians) the goal mouth subtends from a point.</summary>
        public static float GoalAngle(SimVec3 from, SimVec3 goalCentre, float goalWidth)
        {
            float hw = goalWidth * 0.5f;
            float ax = goalCentre.X - hw - from.X, az = goalCentre.Z - from.Z;
            float bx = goalCentre.X + hw - from.X, bz = goalCentre.Z - from.Z;
            float a1 = (float)Math.Atan2(az, ax);
            float a2 = (float)Math.Atan2(bz, bx);
            float d = Abs(a1 - a2);
            if (d > (float)Math.PI) d = 2f * (float)Math.PI - d;
            return d;
        }

        /// <summary>Rotate a flat vector by an angle in radians around Y.</summary>
        public static SimVec3 RotateFlat(SimVec3 v, float radians)
        {
            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            return new SimVec3(v.X * c - v.Z * s, 0f, v.X * s + v.Z * c);
        }
    }
}
