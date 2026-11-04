namespace OpenFifa.Core
{
    /// <summary>
    /// Tunables for driving an external monitor from the iPad.
    /// </summary>
    public sealed class ExternalDisplayConfig
    {
        /// <summary>Never render the external display below this height (1440 = "2K" / QHD), unless the monitor itself is smaller.</summary>
        public int MinRenderHeight = 1440;

        /// <summary>Render height the game starts at when a monitor connects (climbs from here when there is headroom).</summary>
        public int StartRenderHeight = 1440;

        /// <summary>Upper bound for the render height (2160 = 4K UHD). Above this the iOS compositor upscales.</summary>
        public int MaxRenderHeight = 2160;

        /// <summary>Frame rate to hold on the external display.</summary>
        public int TargetFrameRate = 60;

        /// <summary>Narrowest picture shown before letterboxing (4:3 projectors get bars top and bottom).</summary>
        public float MinAspect = 16f / 10f;

        /// <summary>Widest picture shown before pillarboxing. 2.4 keeps "21:9" ultrawides (3440x1440 = 2.39) full screen; 32:9 super-ultrawides get bars left and right.</summary>
        public float MaxAspect = 2.4f;

        /// <summary>Resolution scale change per governor step.</summary>
        public float ScaleStep = 0.0625f;

        /// <summary>Frames averaged before the governor decides.</summary>
        public int SampleFrames = 90;

        /// <summary>Step down when the average frame time exceeds budget by this ratio.</summary>
        public float DownscaleRatio = 1.08f;

        /// <summary>Step up when the average frame time is under budget by this ratio.</summary>
        public float UpscaleRatio = 0.80f;

        /// <summary>Render scale of the iPad's own screen while it acts as companion / mirror.</summary>
        public float DeviceScreenScale = 0.5f;
    }
}
