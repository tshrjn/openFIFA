using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Engine-free pinhole camera with Unity's conventions (left-handed, +Y up, yaw 0 = looking along +Z, yaw +90 deg =
    /// +X, positive pitch tilts down), so presentation logic (near-side occlusion, HUD overlap) can project world points
    /// to the screen in tests. Screen coordinates: u from the left, v from the top, both 0..1 across the frame.
    /// </summary>
    public readonly struct BroadcastView
    {
        public readonly float X, Y, Z;
        public readonly float YawDegrees, PitchDegrees, VerticalFovDegrees, Aspect;
        private readonly float _fx, _fy, _fz, _rx, _rz, _ux, _uy, _uz, _tanHalf;

        public BroadcastView(float x, float y, float z, float yawDegrees, float pitchDegrees, float verticalFovDegrees, float aspect)
        {
            X = x; Y = y; Z = z;
            YawDegrees = yawDegrees; PitchDegrees = pitchDegrees; VerticalFovDegrees = verticalFovDegrees;
            Aspect = aspect > 0.01f ? aspect : 16f / 9f;
            double yaw = yawDegrees * Math.PI / 180.0, pitch = pitchDegrees * Math.PI / 180.0;
            double cp = Math.Cos(pitch), sp = Math.Sin(pitch), cy = Math.Cos(yaw), sy = Math.Sin(yaw);
            _fx = (float)(sy * cp); _fy = (float)(-sp); _fz = (float)(cy * cp);
            // Right = up x forward (left-handed): horizontal.
            _rx = (float)cy; _rz = (float)(-sy);
            // Up = forward x right.
            _ux = (float)(sy * sp); _uy = (float)cp; _uz = (float)(cy * sp);
            _tanHalf = (float)Math.Tan(verticalFovDegrees * 0.5 * Math.PI / 180.0);
        }

        /// <summary>Depth of a point along the view axis (m); negative behind the camera.</summary>
        public float Depth(float px, float py, float pz) => (px - X) * _fx + (py - Y) * _fy + (pz - Z) * _fz;

        /// <summary>Projects a world point; false when it is behind the camera (within 0.1 m of its plane).</summary>
        public bool Project(float px, float py, float pz, out float u, out float v)
        {
            float dx = px - X, dy = py - Y, dz = pz - Z;
            float depth = dx * _fx + dy * _fy + dz * _fz;
            if (depth <= 0.1f) { u = v = 0f; return false; }
            float right = dx * _rx + dz * _rz;
            float up = dx * _ux + dy * _uy + dz * _uz;
            u = 0.5f + 0.5f * right / (depth * _tanHalf * Aspect);
            v = 0.5f - 0.5f * up / (depth * _tanHalf);
            return true;
        }

        /// <summary>
        /// Screen rectangle of an axis-aligned box, clipped to the frame. False when no corner is in front of the
        /// camera or the box lies wholly outside the frame. Corners behind the camera are ignored (fine for the
        /// stadium furniture this is used on, which is always well in front).
        /// </summary>
        public bool ProjectBox(float minX, float minY, float minZ, float maxX, float maxY, float maxZ,
            out float uMin, out float vMin, out float uMax, out float vMax)
        {
            uMin = vMin = float.MaxValue;
            uMax = vMax = float.MinValue;
            bool any = false;
            for (int i = 0; i < 8; i++)
            {
                float px = (i & 1) == 0 ? minX : maxX, py = (i & 2) == 0 ? minY : maxY, pz = (i & 4) == 0 ? minZ : maxZ;
                if (!Project(px, py, pz, out float u, out float v)) continue;
                any = true;
                if (u < uMin) uMin = u;
                if (u > uMax) uMax = u;
                if (v < vMin) vMin = v;
                if (v > vMax) vMax = v;
            }
            if (!any) { uMin = vMin = uMax = vMax = 0f; return false; }
            uMin = Clamp01(uMin); uMax = Clamp01(uMax); vMin = Clamp01(vMin); vMax = Clamp01(vMax);
            return uMax > uMin && vMax > vMin;
        }

        private static float Clamp01(float a) => a < 0f ? 0f : (a > 1f ? 1f : a);
    }
}
