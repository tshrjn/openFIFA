using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure policy for external display output: render resolution tiers, aspect handling and frame pacing.
    /// </summary>
    public static class ExternalDisplayPolicy
    {
        /// <summary>
        /// Render heights available for a display of this native height, ascending. The configured tiers
        /// below native, plus native itself when it sits under the top tier (e.g. 1600 on a 2560x1600 panel),
        /// with the emergency height first when it is lower than everything else. A display smaller than the
        /// first tier gets just its native height.
        /// </summary>
        public static int[] RenderTiers(ExternalDisplayConfig config, int nativeHeight)
        {
            if (nativeHeight <= 0) return new[] { 0 };
            var tiers = new List<int>();
            int top = 0;
            foreach (int h in config.TierHeights)
            {
                if (h <= nativeHeight && !tiers.Contains(h)) tiers.Add(h);
                if (h > top) top = h;
            }
            if (nativeHeight < top && !tiers.Contains(nativeHeight)) tiers.Add(nativeHeight);
            if (tiers.Count == 0) tiers.Add(nativeHeight);
            tiers.Sort();
            int emergency = Math.Min(config.EmergencyHeight, nativeHeight);
            if (emergency > 0 && emergency < tiers[0]) tiers.Insert(0, emergency);
            return tiers.ToArray();
        }

        /// <summary>Lowest render height allowed at this thermal level, before the tier floor.</summary>
        public static bool AllowsEmergency(DeviceThermalLevel thermal) => thermal >= DeviceThermalLevel.Serious;

        /// <summary>
        /// Render resolution for a display of the given native size at the given render height. Keeps the
        /// display's aspect ratio, rounds to even pixel counts and never exceeds native.
        /// </summary>
        public static PixelSize RenderResolution(int nativeWidth, int nativeHeight, int renderHeight)
        {
            if (nativeWidth <= 0 || nativeHeight <= 0) return new PixelSize(0, 0);
            int h = Even(Math.Min(renderHeight > 0 ? renderHeight : nativeHeight, nativeHeight));
            int w = Even((int)Math.Round((double)nativeWidth * h / nativeHeight));
            if (w > nativeWidth) w = nativeWidth;
            return new PixelSize(w, h);
        }

        /// <summary>
        /// Camera viewport for a display of this size: full screen within [MinAspect, MaxAspect],
        /// pillarboxed when wider, letterboxed when narrower.
        /// </summary>
        public static ViewportRect Viewport(ExternalDisplayConfig config, int width, int height)
        {
            if (width <= 0 || height <= 0) return ViewportRect.Full;
            float aspect = (float)width / height;

            if (config.MaxAspect > 0f && aspect > config.MaxAspect + 0.01f)
            {
                float w = config.MaxAspect / aspect;
                return new ViewportRect((1f - w) * 0.5f, 0f, w, 1f);
            }
            if (config.MinAspect > 0f && aspect < config.MinAspect - 0.01f)
            {
                float h = aspect / config.MinAspect;
                return new ViewportRect(0f, (1f - h) * 0.5f, 1f, h);
            }
            return ViewportRect.Full;
        }

        /// <summary>
        /// Frame rate to request: the configured target, capped by what the monitor can refresh at
        /// (0 or less means unknown).
        /// </summary>
        public static int FrameRate(ExternalDisplayConfig config, int monitorMaxFps)
        {
            int target = config.TargetFrameRate > 0 ? config.TargetFrameRate : 60;
            if (monitorMaxFps > 0 && monitorMaxFps < target) return monitorMaxFps;
            return target;
        }

        /// <summary>Reduced render size for the iPad's own screen while the monitor is the main view.</summary>
        public static PixelSize DeviceScreenResolution(ExternalDisplayConfig config, int nativeWidth, int nativeHeight)
        {
            float s = config.DeviceScreenScale;
            if (s <= 0f || s > 1f) s = 1f;
            return new PixelSize(
                Math.Max(2, Even((int)Math.Round(nativeWidth * s))),
                Math.Max(2, Even((int)Math.Round(nativeHeight * s))));
        }

        /// <summary>Orthographic size that fits the pitch (plus margin) on a screen of this aspect, top-down with length along X.</summary>
        public static float CompanionOrthoSize(float pitchLength, float pitchWidth, float aspect, float margin = 1.08f)
        {
            if (aspect <= 0f) aspect = 4f / 3f;
            float halfW = pitchWidth * 0.5f * margin;
            float halfLByAspect = pitchLength * 0.5f * margin / aspect;
            return Math.Max(halfW, halfLByAspect);
        }

        private static int Even(int v) => v - (v & 1);
    }
}
