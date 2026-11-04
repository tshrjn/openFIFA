using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Daytime broadcast lighting for the match: a slightly warm sun with softer, lighter shadows (strength 0.8, URP
    /// high soft-shadow filter), a trilight ambient fill so the shaded side of players is not black, and post AA.
    /// Called by SinglePlayerMatchController after it positions the sun and camera. Values are in one place to tune.
    /// </summary>
    public static class BroadcastLighting
    {
        public const float SunIntensity = 1.2f;
        public const float ShadowStrength = 0.8f;
        public static readonly Color SunColour = new Color(1f, 0.965f, 0.91f);   // ~5200 K afternoon sun

        // Ambient (linear). Up-facing grass gets ~0.35 of fill against the sun's ~0.77; player sides get ~0.3.
        public static readonly Color AmbientSkyLinear = new Color(0.33f, 0.36f, 0.42f);
        public static readonly Color AmbientEquatorLinear = new Color(0.31f, 0.30f, 0.28f);
        public static readonly Color AmbientGroundLinear = new Color(0.10f, 0.095f, 0.075f);

        /// <summary>Sun: warm tint, soft high-quality filtered shadows, lighter shadow strength.</summary>
        public static void ConfigureSun(Light sun)
        {
            if (sun == null) return;
            sun.color = SunColour;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = ShadowStrength;
            var data = sun.GetComponent<UniversalAdditionalLightData>();
            if (data == null) data = sun.gameObject.AddComponent<UniversalAdditionalLightData>();
            data.softShadowQuality = SoftShadowQuality.High;
        }

        /// <summary>
        /// Trilight ambient plus an explicit ambient probe built from the same colours (no baked lighting data exists at
        /// runtime, so the skybox probe cannot be relied on).
        /// </summary>
        public static void ApplyAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.ambientSkyColor = AmbientSkyLinear.gamma;
            RenderSettings.ambientEquatorColor = AmbientEquatorLinear.gamma;
            RenderSettings.ambientGroundColor = AmbientGroundLinear.gamma;
            RenderSettings.ambientProbe = BuildTrilightProbe(AmbientSkyLinear, AmbientEquatorLinear, AmbientGroundLinear);
        }

        /// <summary>SH probe: equator everywhere, plus up and down lobes towards sky and ground colours.</summary>
        public static SphericalHarmonicsL2 BuildTrilightProbe(Color sky, Color equator, Color ground)
        {
            var sh = new SphericalHarmonicsL2();
            sh.Clear();
            sh.AddAmbientLight(equator);
            sh.AddDirectionalLight(Vector3.up, sky - equator, 1f);
            sh.AddDirectionalLight(Vector3.down, ground - equator, 1f);
            return sh;
        }

        /// <summary>SMAA (high) on top of the URP asset's MSAA 4x: cleans line, net and stand-edge crawl. TAA is not used:
        /// it needs MSAA off and motion vectors for every procedural mesh, and ghosts the small fast ball.</summary>
        public static void ConfigureAntialiasing(UniversalAdditionalCameraData camData)
        {
            if (camData == null) return;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;
        }
    }
}
