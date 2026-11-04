namespace OpenFifa.Core
{
    /// <summary>
    /// Timing (in unscaled seconds from the whistle) of the offside presentation.
    /// The ScriptableObject wrapper (OffsidePresentationConfig) lives in Gameplay.
    /// </summary>
    public class OffsidePresentationTimingData
    {
        public float FlagRaiseAt { get; }
        public float SlowMotionStart { get; }
        public float FreezeStart { get; }
        public float CameraCutIn { get; }
        public float LinesShow { get; }
        public float LinesHide { get; }
        public float CameraCutOut { get; }
        public float TimeRestore { get; }
        public float BannerShow { get; }
        public float BannerHide { get; }
        public float TotalDuration { get; }

        /// <summary>Time scale used between SlowMotionStart and FreezeStart.</summary>
        public float SlowMotionScale { get; }

        /// <summary>Seconds for the lines to sweep across the pitch width after LinesShow.</summary>
        public float LineRevealDuration { get; }

        public OffsidePresentationTimingData(
            float flagRaiseAt = 0.05f,
            float slowMotionStart = 0f,
            float freezeStart = 0.45f,
            float cameraCutIn = 0.45f,
            float linesShow = 0.55f,
            float linesHide = 2.0f,
            float cameraCutOut = 2.05f,
            float timeRestore = 2.05f,
            float bannerShow = 0.3f,
            float bannerHide = 2.5f,
            float totalDuration = 2.5f,
            float slowMotionScale = 0.25f,
            float lineRevealDuration = 0.25f)
        {
            FlagRaiseAt = flagRaiseAt;
            SlowMotionStart = slowMotionStart;
            FreezeStart = freezeStart;
            CameraCutIn = cameraCutIn;
            LinesShow = linesShow;
            LinesHide = linesHide;
            CameraCutOut = cameraCutOut;
            TimeRestore = timeRestore;
            BannerShow = bannerShow;
            BannerHide = bannerHide;
            TotalDuration = totalDuration;
            SlowMotionScale = slowMotionScale < 0f ? 0f : (slowMotionScale > 1f ? 1f : slowMotionScale);
            LineRevealDuration = lineRevealDuration;
        }
    }
}
