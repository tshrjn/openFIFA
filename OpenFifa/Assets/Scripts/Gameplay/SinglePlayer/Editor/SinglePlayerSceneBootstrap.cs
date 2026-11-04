using System;
using System.IO;
using System.Linq;
using OpenFifa.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
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
            cam.fieldOfView = 34f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<SinglePlayerCamera>();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var root = new GameObject("SinglePlayerMatch");
            var controller = root.AddComponent<SinglePlayerMatchController>();
            root.AddComponent<SinglePlayerHud>();
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

        /// <summary>Creates the scene, makes it the first build scene for one build, then restores the original list.</summary>
        public static void WithSinglePlayerScene(Action build)
        {
            var original = EditorBuildSettings.scenes;
            try
            {
                CreateScene();
                var list = original.Where(s => s.path != ScenePath).ToList();
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
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
