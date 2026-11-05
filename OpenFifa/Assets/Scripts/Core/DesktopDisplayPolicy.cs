using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure policy for which monitor the macOS / Windows build runs on.
    /// </summary>
    public static class DesktopDisplayPolicy
    {
        /// <summary>Preference value meaning "pick automatically".</summary>
        public const string AutoPreference = "";

        /// <summary>
        /// True for a laptop's own panel. macOS names it "Built-in Retina Display" / "Built-in Liquid Retina
        /// Display" (older releases: "Color LCD").
        /// </summary>
        public static bool IsBuiltIn(DisplayCandidate display) =>
            display.Name.IndexOf("Built-in", StringComparison.OrdinalIgnoreCase) >= 0 ||
            display.Name.IndexOf("Color LCD", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Picks the display to run on.
        /// 1. A pinned display (name + occurrence among displays with that name) wins while it is connected.
        /// 2. Otherwise the largest external display that is at least <paramref name="minExternalHeight"/>
        ///    tall: plugging a 1440p monitor into a MacBook moves the game there, even though the MacBook
        ///    panel has more pixels.
        /// 3. Otherwise the display with the most pixels (ties keep the earlier index).
        /// Returns -1 when the list is empty.
        /// </summary>
        public static int ChooseDisplay(IReadOnlyList<DisplayCandidate> displays, string preferredName, int preferredOccurrence, int minExternalHeight)
        {
            if (displays == null || displays.Count == 0) return -1;

            if (!string.IsNullOrEmpty(preferredName))
            {
                int seen = 0;
                for (int i = 0; i < displays.Count; i++)
                {
                    if (displays[i].Name != preferredName) continue;
                    if (seen == preferredOccurrence) return i;
                    seen++;
                }
            }

            int bestExternal = -1;
            for (int i = 0; i < displays.Count; i++)
            {
                var d = displays[i];
                if (IsBuiltIn(d) || d.Height < minExternalHeight) continue;
                if (bestExternal < 0 || d.PixelCount > displays[bestExternal].PixelCount) bestExternal = i;
            }
            if (bestExternal >= 0) return bestExternal;

            int best = 0;
            for (int i = 1; i < displays.Count; i++)
                if (displays[i].PixelCount > displays[best].PixelCount) best = i;
            return best;
        }

        /// <summary>How many displays before <paramref name="index"/> share its name (to tell identical monitors apart).</summary>
        public static int Occurrence(IReadOnlyList<DisplayCandidate> displays, int index)
        {
            int n = 0;
            for (int i = 0; i < index; i++)
                if (displays[i].Name == displays[index].Name) n++;
            return n;
        }
    }
}
