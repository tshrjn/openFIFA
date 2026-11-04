using System;
using System.IO;
using System.Linq;
using OpenFifa.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OpenFifa.Gameplay.SinglePlayer.Editor
{
    /// <summary>
    /// Creates Assets/Scenes/SinglePlayerMatch.unity and puts it into builds without editing the committed
    /// EditorBuildSettings asset: the build wrappers prepend the scene for the duration of one build and restore the list.
    /// Batch: -executeMethod OpenFifa.Gameplay.SinglePlayer.Editor.SinglePlayerSceneBootstrap.CreateSceneBatch
    ///        -executeMethod OpenFifa.Gameplay.SinglePlayer.Editor.SinglePlayerSceneBootstrap.BuildLinuxCapture [-spBuildOut path]
    /// </summary>
    public static class SinglePlayerSceneBootstrap
    {
        public const string ScenePath = "Assets/Scenes/SinglePlayerMatch.unity";
        public const string ModelPath = "Assets/Animations/Quaternius/UAL2/UAL2_Standard.fbx";
        public const string ControllerPath = "Assets/Animations/Controllers/Footballer.controller";
        public const string DefaultLinuxOut = "build/LinuxCapture/OpenFootball.x86_64";
        public const string RendererDataPath = "Assets/Settings/URPRendererData.asset";
        public const string VolumeProfilePath = "Assets/Settings/SinglePlayerVolumeProfile.asset";
        public const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        [MenuItem("OpenFootball/Scenes/Create Single Player Match")]
        public static void CreateSceneMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateScene();
        }

        public static void CreateSceneBatch()
        {
            try
            {
                CreateScene();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[OpenFootball] scene creation failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Builds the scene from scratch (idempotent) and saves it. Returns the scene path.</summary>
        public static string CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Broadcast Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 27f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<SinglePlayerCamera>();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40f, -75f, 0f);

            EnsureRendererSetup();
            EnsureShaderKeepMaterials();
            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = EnsureVolumeProfile();

            var root = new GameObject("SinglePlayerMatch");
            var controller = root.AddComponent<SinglePlayerMatchController>();
            root.AddComponent<SinglePlayerHud>();
            root.AddComponent<SinglePlayerHudCanvas>();
            root.AddComponent<SinglePlayerCapture>();

            // Use the mannequin only when the animator controller exists; otherwise the capsules read better than a T-pose.
            var anim = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (anim == null) anim = TryBuildAnimator();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (anim != null && model != null)
            {
                controller.PlayerModelPrefab = model;
                controller.AnimatorController = anim;
            }
            Debug.Log($"[OpenFootball] single player scene: model {(controller.PlayerModelPrefab != null ? ModelPath : "capsules")}, " +
                      $"animator {(anim != null ? ControllerPath : "none")}");

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            return ScenePath;
        }

        /// <summary>
        /// Renderer: assigns URP's PostProcessData (it was empty, so Volume effects such as tonemapping never ran) and
        /// adds Screen Space Ambient Occlusion once. Idempotent; saves the renderer asset.
        /// </summary>
        public static void EnsureRendererSetup()
        {
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (rd == null)
            {
                Debug.LogWarning("[OpenFootball] renderer data not found at " + RendererDataPath);
                return;
            }
            bool dirty = false;
            if (rd.postProcessData == null)
            {
                rd.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
                dirty = rd.postProcessData != null;
            }
            if (!rd.rendererFeatures.Any(f => f is ScreenSpaceAmbientOcclusion))
            {
                var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "ScreenSpaceAmbientOcclusion";
                AssetDatabase.AddObjectToAsset(ssao, rd);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long localId);
                var so = new SerializedObject(rd);
                var features = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                dirty = true;
            }
            if (dirty)
            {
                EditorUtility.SetDirty(rd);
                AssetDatabase.SaveAssets();
                Debug.Log("[OpenFootball] renderer: post-process data " + (rd.postProcessData != null ? "set" : "missing") + ", SSAO on");
            }
        }

        /// <summary>
        /// Global volume profile for the match (created or topped up, idempotent): ACES tonemapping, a slight warm grade
        /// with a little less saturation, and mild bloom so the floodlight lamps and halos glow.
        /// </summary>
        public static VolumeProfile EnsureVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }
            bool changed = false;
            if (!profile.Has<Tonemapping>())
            {
                var tone = AddComponent<Tonemapping>(profile);
                tone.mode.Override(TonemappingMode.ACES);
                changed = true;
            }
            if (!profile.Has<ColorAdjustments>())
            {
                var colour = AddComponent<ColorAdjustments>(profile);
                colour.postExposure.Override(0.05f); // ACES darkens mid-tones; the trilight fill (BroadcastLighting) adds the rest
                colour.contrast.Override(10f); // mild S-curve
                colour.saturation.Override(-15f);
                changed = true;
            }
            if (!profile.Has<WhiteBalance>())
            {
                AddComponent<WhiteBalance>(profile).temperature.Override(12f); // warm afternoon grade
                changed = true;
            }
            if (!profile.Has<LiftGammaGain>())
            {
                var lgg = AddComponent<LiftGammaGain>(profile);
                lgg.lift.Override(new Vector4(1f, 0.995f, 0.98f, 0.02f)); // slight warm lift of the blacks
                lgg.gain.Override(new Vector4(1f, 0.995f, 0.985f, 0f));
                changed = true;
            }
            if (!profile.Has<Vignette>())
            {
                var vig = AddComponent<Vignette>(profile);
                vig.intensity.Override(0.16f);
                vig.smoothness.Override(0.45f);
                changed = true;
            }
            if (!profile.Has<Bloom>())
            {
                var bloom = AddComponent<Bloom>(profile);
                bloom.threshold.Override(1.1f); // only HDR-bright lamps and halos, not white kits or lines
                bloom.intensity.Override(0.35f);
                bloom.scatter.Override(0.6f);
                changed = true;
            }
            if (changed)
            {
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
            return profile;
        }

        private static T AddComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var c = profile.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        public const string ShaderKeepFolder = "Assets/Resources/OpenFootball/ShaderKeep";

        /// <summary>
        /// Player builds only ship shaders (and keyword variants) that some included asset uses. The stadium's LED
        /// boards, lamps and halos use URP/Unlit created at runtime, so keep two materials under Resources (always in
        /// the build): opaque Unlit and transparent additive Unlit. Idempotent; no ProjectSettings edits.
        /// </summary>
        public static void EnsureShaderKeepMaterials()
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) return;
            Directory.CreateDirectory(ShaderKeepFolder);
            string opaquePath = ShaderKeepFolder + "/KeepUnlitOpaque.mat", addPath = ShaderKeepFolder + "/KeepUnlitAdditive.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(opaquePath) == null)
                AssetDatabase.CreateAsset(new Material(unlit) { name = "KeepUnlitOpaque" }, opaquePath);
            if (AssetDatabase.LoadAssetAtPath<Material>(addPath) == null)
            {
                var m = new Material(unlit) { name = "KeepUnlitAdditive" };
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 2f);
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                AssetDatabase.CreateAsset(m, addPath);
            }
            AssetDatabase.SaveAssets();
        }

        private static RuntimeAnimatorController TryBuildAnimator()
        {
            try
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null) return null;
                OpenFifa.Editor.FootballAnimationBuilder.BuildAll();
                AssetDatabase.Refresh();
                return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OpenFootball] animator build skipped: " + e.Message);
                return null;
            }
        }

        /// <summary>Linux player with only the single-player scene (review captures under Xvfb).</summary>
        public static void BuildLinuxCapture()
        {
            int code = 1;
            try
            {
                CreateScene();
                string outPath = ArgValue("-spBuildOut") ?? DefaultLinuxOut;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
                // The project defaults to IL2CPP (iOS); the Linux editor here only ships Mono for standalone.
                var standalone = UnityEditor.Build.NamedBuildTarget.Standalone;
                var backend = PlayerSettings.GetScriptingBackend(standalone);
                BuildReport report;
                try
                {
                    PlayerSettings.SetScriptingBackend(standalone, ScriptingImplementation.Mono2x);
                    report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = new[] { ScenePath },
                        locationPathName = outPath,
                        target = BuildTarget.StandaloneLinux64,
                        options = BuildOptions.None,
                    });
                }
                finally
                {
                    PlayerSettings.SetScriptingBackend(standalone, backend);
                }
                Debug.Log($"[OpenFootball] Linux capture build: {report.summary.result}, {report.summary.totalErrors} errors -> {outPath}");
                code = report.summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError("[OpenFootball] Linux capture build failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        [MenuItem("OpenFootball/Build/iOS with Single Player")]
        public static void BuildIOSWithSinglePlayer() => WithSinglePlayerScene(OpenFifa.Editor.BuildScript.BuildIOS);

        [MenuItem("OpenFootball/Build/macOS Dev with Single Player")]
        public static void BuildMacOSDevWithSinglePlayer() => WithSinglePlayerScene(OpenFifa.Editor.BuildScript.BuildMacOSDev);

        /// <summary>For the iOS export pipeline: -executeMethod ...SinglePlayerSceneBootstrap.ExportIOSWithSinglePlayer.</summary>
        public static void ExportIOSWithSinglePlayer() => WithSinglePlayerScene(OpenFifa.Editor.IosBuild.ExportFromCommandLine);

        /// <summary>
        /// Creates the scene, makes it the first build scene (LocalMultiplayerMatch second) for one build, then restores
        /// the original list.
        /// </summary>
        public static void WithSinglePlayerScene(Action build)
        {
            var original = EditorBuildSettings.scenes;
            try
            {
                CreateScene();
                // SinglePlayerMatch first, LocalMultiplayerMatch second, then the rest (BuildSceneOrder); disabled
                // entries are dropped for the build and come back with the original list.
                EditorBuildSettings.scenes = BuildSceneOrder.For(
                        original.Where(s => s.enabled).Select(s => s.path), p => File.Exists(p))
                    .Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
                build();
            }
            finally
            {
                EditorBuildSettings.scenes = original;
            }
        }

        private static string ArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
