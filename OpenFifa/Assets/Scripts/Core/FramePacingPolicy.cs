namespace OpenFifa.Core
{
    /// <summary>Thermal level as reported by the OS (matches NSProcessInfoThermalState: 0 nominal .. 3 critical).</summary>
    public enum DeviceThermalLevel
    {
        Nominal = 0,
        Fair = 1,
        Serious = 2,
        Critical = 3,
    }

    /// <summary>The player's frame-rate preference (settings menu).</summary>
    public enum FrameRatePreference
    {
        /// <summary>A steady 60 fps (default: smooth, and cool enough to hold for a full match).</summary>
        Auto = 0,
        /// <summary>Highest the display allows (120 on ProMotion), dropping to 60 when warm or in Low Power Mode.</summary>
        Max = 1,
    }

    /// <summary>Inputs for <see cref="FramePacingPolicy.Choose"/>.</summary>
    public struct FramePacingInputs
    {
        /// <summary>True for iOS/iPadOS (Application.targetFrameRate drives pacing; vSyncCount is ignored).</summary>
        public bool Mobile;
        /// <summary>Display refresh rate in Hz (60 on most iPads, 120 on ProMotion; 0 if unknown).</summary>
        public int DisplayRefreshHz;
        public FrameRatePreference Preference;
        public DeviceThermalLevel Thermal;
        /// <summary>iOS Low Power Mode is on.</summary>
        public bool LowPowerMode;
        /// <summary>Rate requested by another system (e.g. the external display), or 0/-1 for none. Thermal caps still apply.</summary>
        public int Override;
    }

    /// <summary>
    /// Picks the frame-rate target. Pure logic so it is unit-tested without a device.
    /// iOS caps Unity at 30 fps unless Application.targetFrameRate is set, so mobile always gets an explicit
    /// target. 60 fps is the floor in every state except Critical thermal, where iOS is about to throttle hard
    /// and a steady 30 beats an unsteady 40-50. Desktop (macOS) keeps vSync pacing to the display.
    /// </summary>
    public static class FramePacingPolicy
    {
        public const int Floor = 60;
        public const int ProMotion = 120;
        public const int CriticalFallback = 30;

        /// <summary>Application.targetFrameRate to use. -1 means "leave to vSync" (desktop).</summary>
        public static int Choose(in FramePacingInputs i)
        {
            if (!i.Mobile) return i.Override > 0 ? i.Override : -1;
            if (i.Thermal >= DeviceThermalLevel.Critical) return CriticalFallback;
            if (i.Override > 0) return i.Thermal >= DeviceThermalLevel.Serious && i.Override > Floor ? Floor : i.Override;

            int refresh = i.DisplayRefreshHz > 0 ? i.DisplayRefreshHz : Floor;
            int ceiling = refresh < ProMotion ? refresh : ProMotion;
            if (ceiling < Floor) return ceiling;

            bool high = i.Preference == FrameRatePreference.Max && !i.LowPowerMode && i.Thermal <= DeviceThermalLevel.Fair;
            return high ? ceiling : Floor;
        }
    }
}
