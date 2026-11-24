using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Engine-free placement of the side-on broadcast camera: a fixed height, a depth behind the near touchline that
    /// follows the play across the pitch, and a pitch (downward tilt) held inside
    /// [<see cref="MinPitchDegrees"/>, <see cref="MaxPitchDegrees"/>].
    ///
    /// The old rig sat on a fixed line 28 m back and aimed straight at the play, so its tilt ran from ~9 deg (far
    /// touchline) to ~38 deg (near touchline, close to overhead). Here the camera rides back towards the main stand
    /// when play is on the near side and forward over the lower tier when it is on the far side, so the slant
    /// distance (player size) and the tilt both stay steady; whatever tilt is left over is clamped and the ball simply
    /// sits a little higher or lower in the frame.
    /// </summary>
    public static class BroadcastRig
    {
        /// <summary>Tilt range below the horizon (degrees).</summary>
        public const float MinPitchDegrees = 22f, MaxPitchDegrees = 30f;
        /// <summary>Camera height (m). The stand fit may raise it (never lowers it).</summary>
        public const float DefaultHeight = 25f;
        /// <summary>Depth behind the near touchline (m) with the play on the near touchline / on the far touchline.</summary>
        public const float DefaultNearSetback = 33f, DefaultFarSetback = 16f;
        /// <summary>Vertical field of view (deg): players ~42 px tall at 720p in mid-pitch.</summary>
        public const float DefaultFov = 27f;

        /// <summary>
        /// Camera depth behind the near (+X) touchline for a framed point at <paramref name="focusX"/>: linear from
        /// <paramref name="farSetback"/> (focus on the far touchline, -halfWidth) to <paramref name="nearSetback"/>
        /// (focus on the near touchline, +halfWidth), clamped outside that range.
        /// </summary>
        public static float Setback(float focusX, float halfWidth, float nearSetback, float farSetback)
        {
            if (halfWidth <= 0f) return nearSetback;
            float t = (focusX + halfWidth) / (2f * halfWidth);
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return farSetback + (nearSetback - farSetback) * t;
        }

        /// <summary>
        /// Tilt (degrees below the horizon) that puts a ground point <paramref name="horizontalDistance"/> away at
        /// <paramref name="fromTop"/> down the frame, clamped to the broadcast range.
        /// </summary>
        public static float PitchDegrees(float height, float horizontalDistance, float verticalFovDegrees, float fromTop)
        {
            float below = (float)(Math.Atan2(height, Math.Max(0.01f, horizontalDistance)) * 180.0 / Math.PI);
            float pitch = below - BroadcastFraming.PitchUpDegrees(verticalFovDegrees, fromTop);
            return pitch < MinPitchDegrees ? MinPitchDegrees : (pitch > MaxPitchDegrees ? MaxPitchDegrees : pitch);
        }

        /// <summary>
        /// Full solve for a framed point at <paramref name="focusX"/> (world X; the near touchline is +halfWidth):
        /// camera world X and tilt.
        /// </summary>
        public static void Solve(float focusX, float halfWidth, float height, float nearSetback, float farSetback,
            float verticalFovDegrees, float fromTop, out float cameraX, out float pitchDegrees)
        {
            float setback = Setback(focusX, halfWidth, nearSetback, farSetback);
            cameraX = halfWidth + setback;
            pitchDegrees = PitchDegrees(height, cameraX - focusX, verticalFovDegrees, fromTop);
        }

        /// <summary>The gantry slides along the pitch but stops this far short of each goal line (m).</summary>
        public const float GantryEndInset = 14f;

        /// <summary>
        /// Full broadcast pose for a framed ground point (<paramref name="focusX"/>, <paramref name="focusZ"/>): camera
        /// position, yaw (degrees, Unity convention: -90 = looking along -X, across the pitch from the +X side) and tilt.
        /// The gantry stops <see cref="GantryEndInset"/> short of the goal lines, so near a goal the camera turns
        /// towards it (yaw clamped to <paramref name="maxYawDegrees"/> either side of straight across).
        /// </summary>
        public static void Pose(float focusX, float focusZ, float halfWidth, float halfLength, float height,
            float nearSetback, float farSetback, float verticalFovDegrees, float fromTop, float maxYawDegrees,
            out float camX, out float camY, out float camZ, out float yawDegrees, out float pitchDegrees)
        {
            camX = halfWidth + Setback(focusX, halfWidth, nearSetback, farSetback);
            camY = height;
            float zLimit = Math.Max(0f, halfLength - GantryEndInset);
            camZ = focusZ < -zLimit ? -zLimit : (focusZ > zLimit ? zLimit : focusZ);
            HorizontalAim(camX, camZ, focusX, focusZ, maxYawDegrees, out float across, out float dz);
            // Unity yaw = atan2(forward.x, forward.z); the horizontal forward is (-across, dz).
            yawDegrees = (float)(Math.Atan2(-across, dz) * 180.0 / Math.PI);
            float horizontal = (float)Math.Sqrt(across * across + dz * dz);
            pitchDegrees = PitchDegrees(height, horizontal, verticalFovDegrees, fromTop);
        }

        /// <summary>
        /// Horizontal aim from the camera to the framed point: <paramref name="across"/> metres towards -X (at least 1)
        /// and <paramref name="dz"/> along the pitch, clamped so the yaw stays within <paramref name="maxYawDegrees"/>.
        /// The sim's camera yaw is atan2(-across, dz) (single precision, as Unity computes it from the camera forward).
        /// </summary>
        public static void HorizontalAim(float camX, float camZ, float focusX, float focusZ, float maxYawDegrees,
            out float across, out float dz)
        {
            across = Math.Max(1f, camX - focusX);
            float maxDz = (float)Math.Tan(maxYawDegrees * Math.PI / 180.0) * across;
            dz = focusZ - camZ;
            dz = dz < -maxDz ? -maxDz : (dz > maxDz ? maxDz : dz);
        }

        /// <summary>Where a ground point <paramref name="horizontalDistance"/> away lands, as a fraction from the top, for a tilt.</summary>
        public static float GroundPointFromTop(float height, float horizontalDistance, float pitchDegrees, float verticalFovDegrees)
        {
            float below = (float)(Math.Atan2(height, Math.Max(0.01f, horizontalDistance)) * 180.0 / Math.PI);
            return BroadcastFraming.FromTopForAngle(verticalFovDegrees, below - pitchDegrees);
        }
    }
}
