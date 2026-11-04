using System;
using System.IO;
using OpenFifa.Gameplay.SinglePlayer;
using OpenFifa.Gameplay.SinglePlayer.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenFifa.Gameplay.LocalMultiplayer.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/LocalMultiplayerMatch.unity from the single-player scene (so it never drifts from it) plus a
    /// <see cref="LocalMultiplayerMatch"/> on the match object, and a Linux capture player with that scene.
    /// Batch: -executeMethod OpenFifa.Gameplay.LocalMultiplayer.Editor.LocalMultiplayerSceneBootstrap.BuildLinuxCapture
    /// [-lmBuildOut path].
    /// </summary>
    public static class LocalMultiplayerSceneBootstrap
    {
        public const string ScenePath = "Assets/Scenes/LocalMultiplayerMatch.unity";
        public const string DefaultLinuxOut = "build/LocalMultiplayerCapture/OpenFootball.x86_64";

        [MenuItem("OpenFootball/Local Multiplayer/Create Scene")]
        public static string CreateScene()
        {
            SinglePlayerSceneBootstrap.CreateScene();
            var scene = EditorSceneManager.OpenScene(SinglePlayerSceneBootstrap.ScenePath, OpenSceneMode.Single);
            var controller = UnityEngine.Object.FindFirstObjectByType<SinglePlayerMatchController>();
            if (controller == null) throw new InvalidOperationException("single-player scene has no SinglePlayerMatchController");
            controller.gameObject.name = "LocalMultiplayerMatch";
            if (controller.GetComponent<LocalMultiplayerMatch>() == null) controller.gameObject.AddComponent<LocalMultiplayerMatch>();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("could not save " + ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("[OpenFootball] wrote " + ScenePath);
            return ScenePath;
        }

        public static void CreateSceneBatch()
        {
            int code = 1;
            try { CreateScene(); code = 0; }
            catch (Exception e) { Debug.LogError("[OpenFootball] local multiplayer scene failed: " + e); }
            EditorApplication.Exit(code);
        }

        /// <summary>Linux player with only the local multiplayer scene (review captures under Xvfb).</summary>
        public static void BuildLinuxCapture()
        {
            int code = 1;
            try
            {
                CreateScene();
                string outPath = ArgValue("-lmBuildOut") ?? DefaultLinuxOut;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
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
                Debug.Log($"[OpenFootball] local multiplayer capture build: {report.summary.result} -> {outPath}");
                code = report.summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError("[OpenFootball] local multiplayer capture build failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static string ArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
