using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure policy for external display output: render resolution, aspect handling and frame pacing.
    /// </summary>
    public static class ExternalDisplayPolicy
    {
        /// <summary>
        /// Lowest render scale the governor may use for a monitor of this native height
        /// (keeps the render at or above <see cref="ExternalDisplayConfig.MinRenderHeight"/>).
        /// </summary>
        public static float MinScale(ExternalDisplayConfig config, int nativeHeight)
        {
            if (nativeHeight <= 0) return 1f;
            return Clamp01((float)config.MinRenderHeight / nativeHeight);
        }

        /// <summary>Highest render scale for a monitor of this native height.</summary>
        public static float MaxScale(ExternalDisplayConfig config, int nativeHeight)
        {
            if (nativeHeight <= 0) return 1f;
            return Clamp01((float)config.MaxRenderHeight / nativeHeight);
        }

        /// <summary>Render scale used right after a monitor connects.</summary>
        public static float StartScale(ExternalDisplayConfig config, int nativeHeight)
        {
            if (nativeHeight <= 0) return 1f;
            float start = Clamp01((float)config.StartRenderHeight / nativeHeight);
            float min = MinScale(config, nativeHeight);
            float max = MaxScale(config, nativeHeight);
            if (start < min) start = min;
            if (start > max) start = max;
            return start;
        }

        /// <summary>
        /// Render resolution for a monitor at the given native size and scale. Keeps the monitor's aspect
        /// ratio, rounds to even pixel counts and never exceeds native or drops below the minimum height
        /// (unless the monitor is smaller than that).
        /// </summary>
        public static PixelSize RenderResolution(ExternalDisplayConfig config, int nativeWidth, int nativeHeight, float scale)
        {
            if (nativeWidth <= 0 || nativeHeight <= 0) return new PixelSize(0, 0);

            float min = MinScale(config, nativeHeight);
            float max = MaxScale(config, nativeHeight);
            if (scale < min) scale = min;
            if (scale > max) scale = max;

            int h = Even((int)Math.Round(nativeHeight * scale));
            int w = Even((int)Math.Round((double)nativeWidth * h / nativeHeight));
            if (h > nativeHeight) h = nativeHeight;
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

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
