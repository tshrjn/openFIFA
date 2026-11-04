using OpenFifa.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Daytime broadcast lighting for the match: a slightly warm sun with softer, lighter shadows (strength 0.8, URP
    /// high soft-shadow filter), a trilight ambient fill so the shaded side of players is not black, and post AA.
    /// Called by SinglePlayerMatchController after it positions the sun and camera. Values are in one place to tune.
    /// <para>
    /// Quality tier (<see cref="RenderQualityTiers"/>): desktop and captures keep the high tier (SMAA High over MSAA 4x,
    /// 4096 four-cascade shadows, high soft-shadow filter, SSAO); iOS/mobile, or <c>-spQuality low</c>, gets FXAA,
    /// MSAA 2x, a 2048 two-cascade shadow map, medium soft shadows and no SSAO (<see cref="ApplyQualityTier"/>).
    /// </para>
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

        private static bool s_tierResolved;
        private static RenderQualityProfile s_profile;

        /// <summary>The tier's settings: <c>-spQuality low|high</c> on the command line, else mobile platforms get the mobile tier.</summary>
        public static RenderQualityProfile Quality
        {
            get
            {
                if (!s_tierResolved)
                {
                    string[] args = null;
                    try { args = System.Environment.GetCommandLineArgs(); } catch (System.Exception) { /* not available */ }
                    s_profile = RenderQualityTiers.For(RenderQualityTiers.Resolve(args, Application.isMobilePlatform));
                    s_tierResolved = true;
                }
                return s_profile;
            }
        }

        // URP asset values before the tier changed them (restored on quit in the Editor, where the asset is a project file).
        private static UniversalRenderPipelineAsset s_tierAsset;
        private static int s_origMsaa, s_origShadowRes, s_origCascades;
        private static float s_origShadowDistance, s_origCascade2Split;
        private static readonly System.Collections.Generic.List<ScriptableRendererFeature> s_disabledFeatures =
            new System.Collections.Generic.List<ScriptableRendererFeature>();

        /// <summary>
        /// Applies the tier to the active URP asset (MSAA, shadow map size, cascades, distance) and its renderer features
        /// (SSAO off on the mobile tier). The high tier leaves the asset as authored. Safe to call more than once.
        /// </summary>
        public static void ApplyQualityTier()
        {
            var q = Quality;
            Debug.Log($"[BroadcastLighting] render quality tier {q.Tier} (override with {RenderQualityTiers.Argument} low|high)");
            if (q.Tier == RenderQualityTier.High || s_tierAsset != null) return;
            var asset = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (asset == null) return;
            s_tierAsset = asset;
            s_origMsaa = asset.msaaSampleCount;
            s_origShadowRes = asset.mainLightShadowmapResolution;
            s_origCascades = asset.shadowCascadeCount;
            s_origShadowDistance = asset.shadowDistance;
            s_origCascade2Split = asset.cascade2Split;
            asset.msaaSampleCount = q.MsaaSamples;
            asset.mainLightShadowmapResolution = q.ShadowResolution;
            asset.shadowCascadeCount = q.ShadowCascades;
            asset.shadowDistance = q.ShadowDistance;
            if (q.ShadowCascades == 2) asset.cascade2Split = 0.3f;
            if (!q.Ssao)
            {
                foreach (var data in asset.rendererDataList)
                {
                    if (data == null) continue;
                    foreach (var f in data.rendererFeatures)
                    {
                        if (f == null || !f.isActive || f.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                        f.SetActive(false);
                        s_disabledFeatures.Add(f);
                    }
                }
            }
            if (Application.isEditor) Application.quitting += RestoreQualityTier;
        }

        /// <summary>Puts the URP asset and renderer features back as authored (Editor play mode must not leave the project changed).</summary>
        public static void RestoreQualityTier()
        {
            Application.quitting -= RestoreQualityTier;
            if (s_tierAsset != null)
            {
                s_tierAsset.msaaSampleCount = s_origMsaa;
                s_tierAsset.mainLightShadowmapResolution = s_origShadowRes;
                s_tierAsset.shadowCascadeCount = s_origCascades;
                s_tierAsset.shadowDistance = s_origShadowDistance;
                s_tierAsset.cascade2Split = s_origCascade2Split;
                s_tierAsset = null;
            }
            foreach (var f in s_disabledFeatures) if (f != null) f.SetActive(true);
            s_disabledFeatures.Clear();
        }

        /// <summary>Sun: warm tint, soft filtered shadows (high filter; medium on the mobile tier), lighter shadow strength.</summary>
        public static void ConfigureSun(Light sun)
        {
            if (sun == null) return;
            sun.color = SunColour;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = ShadowStrength;
            var data = sun.GetComponent<UniversalAdditionalLightData>();
            if (data == null) data = sun.gameObject.AddComponent<UniversalAdditionalLightData>();
            data.softShadowQuality = Quality.SoftShadowQuality >= 3 ? SoftShadowQuality.High
                : (Quality.SoftShadowQuality == 2 ? SoftShadowQuality.Medium : SoftShadowQuality.Low);
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

        /// <summary>SMAA (high) on top of the URP asset's MSAA 4x: cleans line, net and stand-edge crawl; the mobile tier
        /// uses FXAA over MSAA 2x. TAA is not used: it needs MSAA off and motion vectors for every procedural mesh, and
        /// ghosts the small fast ball.</summary>
        public static void ConfigureAntialiasing(UniversalAdditionalCameraData camData)
        {
            if (camData == null) return;
            switch (Quality.PostAa)
            {
                case PostAntialiasing.None:
                    camData.antialiasing = AntialiasingMode.None;
                    break;
                case PostAntialiasing.Fxaa:
                    camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                    break;
                case PostAntialiasing.SmaaLow:
                    camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    camData.antialiasingQuality = AntialiasingQuality.Low;
                    break;
                default:
                    camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    camData.antialiasingQuality = AntialiasingQuality.High;
                    break;
            }
        }
    }
}
