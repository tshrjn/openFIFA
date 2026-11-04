using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure policy for which monitor the macOS / Windows build runs on and at what render scale.
    /// </summary>
    public static class DesktopDisplayPolicy
    {
        /// <summary>Preference value meaning "use the best connected display".</summary>
        public const string AutoPreference = "";

        /// <summary>
        /// Picks the display to run on. An explicitly preferred display (by name) wins while it is connected;
        /// otherwise the display with the most pixels (a plugged-in 1440p/4K monitor beats a laptop panel).
        /// Ties keep the earlier index, so the built-in screen (index 0) wins among equals. -1 when empty.
        /// </summary>
        public static int ChooseDisplay(IReadOnlyList<DisplayCandidate> displays, string preferredName)
        {
            if (displays == null || displays.Count == 0) return -1;

            if (!string.IsNullOrEmpty(preferredName))
            {
                for (int i = 0; i < displays.Count; i++)
                    if (displays[i].Name == preferredName) return i;
            }

            int best = 0;
            for (int i = 1; i < displays.Count; i++)
                if (displays[i].PixelCount > displays[best].PixelCount) best = i;
            return best;
        }

        /// <summary>
        /// Render scale (URP renderScale) that starts a display of this native height at the configured start
        /// height (1440p), never above native and never above <see cref="ExternalDisplayConfig.MaxRenderHeight"/>.
        /// </summary>
        public static float StartRenderScale(ExternalDisplayConfig config, int nativeHeight) =>
            ExternalDisplayPolicy.StartScale(config, nativeHeight);
    }
}
