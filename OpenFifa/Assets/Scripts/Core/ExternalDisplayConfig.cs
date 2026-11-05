namespace OpenFifa.Core
{
    /// <summary>
    /// Tunables for driving an external monitor (iPad) or a desktop display (MacBook).
    /// </summary>
    public sealed class ExternalDisplayConfig
    {
        /// <summary>
        /// Render heights the governor moves between (ascending). Quantized so the render target is only
        /// reallocated on a real tier change. The lowest is the normal floor ("2K" / QHD).
        /// </summary>
        public int[] TierHeights = { 1440, 1800, 2160 };

        /// <summary>Emergency render height allowed only when the device is Serious/Critical thermally.</summary>
        public int EmergencyHeight = 1080;

        /// <summary>Frame rate to hold on the external display.</summary>
        public int TargetFrameRate = 60;

        /// <summary>Narrowest picture shown before letterboxing (4:3 projectors get bars top and bottom).</summary>
        public float MinAspect = 16f / 10f;

        /// <summary>Widest picture shown before pillarboxing. 2.4 keeps "21:9" ultrawides (3440x1440 = 2.39) full screen; 32:9 super-ultrawides get bars left and right.</summary>
        public float MaxAspect = 2.4f;

        /// <summary>Frames averaged per governor decision window (~1.5 s at 60 fps).</summary>
        public int SampleFrames = 90;

        /// <summary>A window whose average work time exceeds budget by this ratio steps one tier down.</summary>
        public float DownscaleRatio = 1.08f;

        /// <summary>A window whose average work time is under budget by this ratio counts as headroom.</summary>
        public float UpscaleRatio = 0.75f;

        /// <summary>Consecutive headroom windows needed before stepping a tier up (~6 s), to avoid flapping.</summary>
        public int UpscaleWindows = 4;

        /// <summary>Render scale of the iPad's own screen while the monitor is the main view.</summary>
        public float DeviceScreenScale = 0.5f;
    }
}
