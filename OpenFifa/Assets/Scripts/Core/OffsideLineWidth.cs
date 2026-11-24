using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// World width of an offside line at a given distance from the camera, so it reads at broadcast distance: at
    /// least <paramref name="baseWidth"/> metres, and never thinner on screen than <paramref name="minScreenFraction"/>
    /// of the screen height (a 0.09 m line was about one pixel from the broadcast camera, so it vanished).
    /// Pure C#: EditMode testable.
    /// </summary>
    public static class OffsideLineWidth
    {
        /// <summary>Default world width of a line (m).</summary>
        public const float DefaultBaseWidth = 0.22f;

        /// <summary>Default minimum on-screen width: 0.4% of the screen height (~4 px at 1080p, ~3 px at 720p).</summary>
        public const float DefaultMinScreenFraction = 0.004f;

        /// <summary>
        /// Width in metres for a line point <paramref name="distance"/> metres from a perspective camera with vertical
        /// field of view <paramref name="verticalFovDegrees"/>.
        /// </summary>
        public static float AtDistance(float baseWidth, float distance, float verticalFovDegrees,
            float minScreenFraction = DefaultMinScreenFraction)
        {
            if (baseWidth < 0f) baseWidth = 0f;
            if (distance <= 0f || verticalFovDegrees <= 0f || minScreenFraction <= 0f) return baseWidth;
            // Height of the view frustum at that distance, in metres; the line must cover the fraction of it.
            double frustumHeight = 2.0 * distance * Math.Tan(verticalFovDegrees * 0.5 * Math.PI / 180.0);
            float min = (float)(frustumHeight * minScreenFraction);
            return baseWidth > min ? baseWidth : min;
        }
    }
}
