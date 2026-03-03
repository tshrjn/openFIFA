namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# config for URP post-processing. No Unity dependencies.
    /// Values calibrated to approximate EA FC 26 Rush Mode night atmosphere.
    /// </summary>
    public class PostProcessingConfig
    {
        // Bloom
        public float BloomThreshold { get; }
        public float BloomIntensity { get; }
        public float BloomScatter { get; }

        // Color Adjustments
        public float PostExposure { get; }
        public float Contrast { get; }
        public float Saturation { get; }
        public float ColorFilterR { get; }
        public float ColorFilterG { get; }
        public float ColorFilterB { get; }

        // Vignette
        public float VignetteIntensity { get; }
        public float VignetteSmoothness { get; }

        public PostProcessingConfig(
            float bloomThreshold = 0.8f,
            float bloomIntensity = 0.5f,
            float bloomScatter = 0.65f,
            float postExposure = 0.2f,
            float contrast = 12f,
            float saturation = 8f,
            float colorFilterR = 1f,
            float colorFilterG = 0.97f,
            float colorFilterB = 0.92f,
            float vignetteIntensity = 0.3f,
            float vignetteSmoothness = 0.5f)
        {
            BloomThreshold = bloomThreshold;
            BloomIntensity = bloomIntensity;
            BloomScatter = bloomScatter;
            PostExposure = postExposure;
            Contrast = contrast;
            Saturation = saturation;
            ColorFilterR = colorFilterR;
            ColorFilterG = colorFilterG;
            ColorFilterB = colorFilterB;
            VignetteIntensity = vignetteIntensity;
            VignetteSmoothness = vignetteSmoothness;
        }

        public static PostProcessingConfig CreateNightStadium()
        {
            return new PostProcessingConfig();
        }
    }
}
