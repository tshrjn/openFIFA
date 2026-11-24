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
    /// slab" beside the radar). A piece is hidden whenever it is in shot and runs off the bottom of the frame
    /// (<see cref="CutByBottomEdge"/>): that is foreground clutter at any size, and since it is hidden on the frame it
    /// enters, the switch is never seen. With play 8-11 m in from the near touchline the roofs come in at the bottom
    /// edge covering only 0-8% of the frame, which a size threshold alone left drawn. A piece wholly inside the frame is
    /// hidden while it covers more than <see cref="HideCoverage"/> of the frame height, and shown again below
    /// <see cref="ShowCoverage"/> (hysteresis, so it does not flicker). The near ad boards are a thin strip at broadcast
    /// distance and stay visible.
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

        /// <summary>
        /// True when the box is in shot and extends below the bottom edge of the frame (a corner projects below it, or
        /// lies behind the camera), so the frame cuts it off at the bottom.
        /// </summary>
        public static bool CutByBottomEdge(in BroadcastView view, in WorldBox box)
        {
            if (Coverage(view, box) <= 0f) return false;
            for (int i = 0; i < 8; i++)
            {
                float px = (i & 1) == 0 ? box.MinX : box.MaxX, py = (i & 2) == 0 ? box.MinY : box.MaxY, pz = (i & 4) == 0 ? box.MinZ : box.MaxZ;
                if (!view.Project(px, py, pz, out _, out float v) || v > 1f) return true;
            }
            return false;
        }

        /// <summary>Hidden state after this frame for a piece wholly inside the frame, with hysteresis.</summary>
        public static bool Hidden(bool wasHidden, float coverage) =>
            wasHidden ? coverage > ShowCoverage : coverage > HideCoverage;

        /// <summary>Hidden state after this frame: hidden whenever it is cut off by the bottom edge, else with hysteresis.</summary>
        public static bool Hidden(bool wasHidden, in BroadcastView view, in WorldBox box) =>
            CutByBottomEdge(view, box) || Hidden(wasHidden, Coverage(view, box));

        private static float Min(float a, float b) => a < b ? a : b;
        private static float Max(float a, float b) => a > b ? a : b;
    }
}
