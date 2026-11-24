using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>World axis-aligned box (m).</summary>
    public readonly struct WorldBox
    {
        public readonly float MinX, MinY, MinZ, MaxX, MaxY, MaxZ;

        public WorldBox(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            MinX = minX; MinY = minY; MinZ = minZ; MaxX = maxX; MaxY = maxY; MaxZ = maxZ;
        }
    }

    /// <summary>
    /// Near-side stadium furniture (dugouts, tunnel canopy) between the broadcast camera and the pitch. With the play on
    /// the near touchline the camera looks down past it and its white roofs fill the bottom of the frame (the "white
    /// slab" beside the radar). Each piece is hidden while it covers more than <see cref="HideCoverage"/> of the frame
    /// height, and shown again below <see cref="ShowCoverage"/> (hysteresis, so it does not flicker at the edge).
    /// The near ad boards are a thin strip at broadcast distance and stay visible.
    /// </summary>
    public static class NearSideOcclusion
    {
        public const float HideCoverage = 0.08f, ShowCoverage = 0.05f;

        /// <summary>Dugout shells (roof included) and the players' tunnel canopy, world boxes, near (+X) side.</summary>
        public static List<WorldBox> Occluders(StadiumLayout layout)
        {
            var list = new List<WorldBox>(3);
            if (layout == null) return list;
            var sp = layout.Spec;
            foreach (float ac in layout.DugoutCentres)
                list.Add(Box(layout, sp.DugoutFront - 0.2f, sp.DugoutFront + sp.DugoutDepth,
                    ac - sp.DugoutWidth * 0.5f, ac + sp.DugoutWidth * 0.5f, sp.DugoutHeight + 0.15f));
            layout.TunnelSpan(out float t0, out float t1);
            list.Add(Box(layout, sp.DugoutFront + 0.3f, layout.MainStand.MinDepth, t0 - 0.15f, t1 + 0.15f, sp.TunnelHeight + 0.15f));
            return list;
        }

        private static WorldBox Box(StadiumLayout layout, float d0, float d1, float a0, float a1, float top)
        {
            var p = layout.ToWorld(StandSide.Near, d0, a0, 0f);
            var q = layout.ToWorld(StandSide.Near, d1, a1, top);
            return new WorldBox(Min(p.X, q.X), 0f, Min(p.Z, q.Z), Max(p.X, q.X), top, Max(p.Z, q.Z));
        }

        /// <summary>Fraction of the frame height the box covers (0 when it is out of shot).</summary>
        public static float Coverage(in BroadcastView view, in WorldBox box)
        {
            if (!view.ProjectBox(box.MinX, box.MinY, box.MinZ, box.MaxX, box.MaxY, box.MaxZ,
                    out _, out float vMin, out _, out float vMax)) return 0f;
            return vMax - vMin;
        }

        /// <summary>Hidden state after this frame, with hysteresis.</summary>
        public static bool Hidden(bool wasHidden, float coverage) =>
            wasHidden ? coverage > ShowCoverage : coverage > HideCoverage;

        private static float Min(float a, float b) => a < b ? a : b;
        private static float Max(float a, float b) => a > b ? a : b;
    }
}
