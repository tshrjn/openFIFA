using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Where the procedural stadium stops: the top rims of the four open corner fills and the front edges of the four
    /// roofs. Above that skyline there is no geometry (the camera's clear colour), so a camera that looks above it shows
    /// a flat dark slab, as the first goal camera did through the open corner behind the goal. Also an engine-free
    /// "is this point inside the stadium" test for checking a camera's position and near frustum.
    /// </summary>
    public static class StadiumSkyline
    {
        /// <summary>Corner fill parapet above its top row (ProceduralStadium.BuildCorners).</summary>
        public const float CornerParapet = 1.1f;

        /// <summary>World points along the skyline: corner fill rims (outer parapet top) and roof front edges (fascia bottom).</summary>
        public static List<SimVec3> Points(StadiumLayout layout, float spacing = 2f)
        {
            var list = new List<SimVec3>();
            if (layout == null) return list;
            var lower = layout.SideStand.Lower;
            float rimRadius = lower.BackDepth - layout.Spec.SideSetback;
            float rimTop = lower.TopHeight + CornerParapet;
            int arcSteps = Math.Max(4, (int)Math.Ceiling(0.5 * Math.PI * rimRadius / spacing));
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var c = layout.CornerCentre(sx, sz);
                for (int k = 0; k <= arcSteps; k++)
                {
                    double th = 0.5 * Math.PI * k / arcSteps;
                    list.Add(new SimVec3(c.X + sx * (float)Math.Cos(th) * rimRadius, rimTop, c.Z + sz * (float)Math.Sin(th) * rimRadius));
                }
            }
            for (int s = 0; s < 4; s++)
            {
                var side = (StandSide)s;
                var p = layout.Profile(side);
                float h = p.RoofFrontHeight - p.RoofFasciaDepth;
                float e = layout.HalfExtent(side);
                int n = Math.Max(2, (int)Math.Ceiling(2f * e / spacing));
                for (int i = 0; i <= n; i++)
                    list.Add(layout.ToWorld(side, p.RoofFrontDepth, -e + 2f * e * i / n, h));
            }
            return list;
        }

        /// <summary>
        /// Lowest screen position (fraction from the top, can be negative = above the frame) of any skyline point that is
        /// in front of the camera and inside the frame horizontally. Positive means empty space shows in the frame at that
        /// point. Returns -1 when no skyline point is in view horizontally.
        /// </summary>
        public static float LowestInView(in BroadcastView view, List<SimVec3> skyline)
        {
            float worst = -1f;
            for (int i = 0; i < skyline.Count; i++)
            {
                var q = skyline[i];
                if (!view.Project(q.X, q.Y, q.Z, out float u, out float v)) continue;
                if (u < 0f || u > 1f) continue;
                if (v > worst) worst = v;
            }
            return worst;
        }

        /// <summary>True when the world point is inside stadium geometry: a stand (incl. roof), an ad board or a corner fill.</summary>
        public static bool InsideSolid(StadiumLayout layout, float x, float y, float z)
        {
            for (int s = 0; s < 4; s++)
            {
                var side = (StandSide)s;
                StadiumLayout.Outward(side, out float ox, out float oz);
                StadiumLayout.Along(side, out float ax, out float az);
                float d = x * ox + z * oz - layout.LineDistance(side);
                float a = x * ax + z * az;
                if (Math.Abs(a) > layout.HalfExtent(side)) continue;
                foreach (var b in layout.SightlineSolids(side))
                    if (b.Contains(d, y)) return true;
            }
            float r = layout.CornerFillRadius;
            float top = layout.SideStand.Lower.TopHeight + CornerParapet;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var c = layout.CornerCentre(sx, sz);
                float dx = (x - c.X) * sx, dz = (z - c.Z) * sz;
                if (dx > 0f && dz > 0f && dx * dx + dz * dz < r * r && y > 0f && y < top) return true;
            }
            return false;
        }
    }
}
