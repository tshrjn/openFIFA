namespace OpenFifa.Core
{
    /// <summary>
    /// Deterministic segment geometry for swept collision tests (ball vs goal frame, ball vs player capsule).
    /// Closest-point routines follow Ericson, Real-Time Collision Detection, 5.1.2 and 5.1.9.
    /// </summary>
    public static class SimGeometry
    {
        private const float Eps = 1e-8f;

        /// <summary>Closest point to <paramref name="p"/> on segment [a, b].</summary>
        public static SimVec3 ClosestOnSegment(SimVec3 p, SimVec3 a, SimVec3 b)
        {
            SimVec3 ab = b - a;
            float len2 = ab.SqrMagnitude;
            if (len2 < Eps) return a;
            float t = SimMath.Clamp01(SimVec3.Dot(p - a, ab) / len2);
            return a + ab * t;
        }

        /// <summary>Squared distance from <paramref name="p"/> to segment [a, b].</summary>
        public static float SqrDistanceToSegment(SimVec3 p, SimVec3 a, SimVec3 b) =>
            (p - ClosestOnSegment(p, a, b)).SqrMagnitude;

        /// <summary>Parameters s (on [p1, q1]) and t (on [p2, q2]) of the closest points between two segments.</summary>
        public static void ClosestSegmentSegment(SimVec3 p1, SimVec3 q1, SimVec3 p2, SimVec3 q2, out float s, out float t)
        {
            SimVec3 d1 = q1 - p1;
            SimVec3 d2 = q2 - p2;
            SimVec3 r = p1 - p2;
            float a = SimVec3.Dot(d1, d1);
            float e = SimVec3.Dot(d2, d2);
            float f = SimVec3.Dot(d2, r);
            if (a <= Eps && e <= Eps) { s = 0f; t = 0f; return; }
            if (a <= Eps) { s = 0f; t = SimMath.Clamp01(f / e); return; }
            float c = SimVec3.Dot(d1, r);
            if (e <= Eps) { t = 0f; s = SimMath.Clamp01(-c / a); return; }
            float b = SimVec3.Dot(d1, d2);
            float denom = a * e - b * b;
            s = denom > Eps ? SimMath.Clamp01((b * f - c * e) / denom) : 0f;
            t = (b * s + f) / e;
            if (t < 0f) { t = 0f; s = SimMath.Clamp01(-c / a); }
            else if (t > 1f) { t = 1f; s = SimMath.Clamp01((b - c) / a); }
        }

        /// <summary>
        /// Swept sphere (centre moving from <paramref name="p0"/> to <paramref name="p1"/>) against a capsule with axis
        /// [a, b]; <paramref name="radius"/> is the sum of both radii. Returns true on contact, with the fraction
        /// <paramref name="hit"/> of the motion at first contact (0 when already overlapping at p0).
        /// The distance along a linear motion is convex, so the first contact is found by bisection on [0, s*].
        /// </summary>
        public static bool SweepSphereCapsule(SimVec3 p0, SimVec3 p1, SimVec3 a, SimVec3 b, float radius, out float hit)
        {
            float r2 = radius * radius;
            hit = 0f;
            if (SqrDistanceToSegment(p0, a, b) < r2) return true;
            ClosestSegmentSegment(p0, p1, a, b, out float s, out _);
            SimVec3 pm = p0 + (p1 - p0) * s;
            if (SqrDistanceToSegment(pm, a, b) >= r2) return false;

            float lo = 0f, hi = s;
            for (int i = 0; i < 14; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (SqrDistanceToSegment(p0 + (p1 - p0) * mid, a, b) < r2) hi = mid; else lo = mid;
            }

            hit = hi;
            return true;
        }
    }
}
