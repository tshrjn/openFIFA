using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenFifa.Editor
{
    public static class BuildScript
    {
        /// <summary>
        /// Scenes for a player build: SinglePlayerMatch, then LocalMultiplayerMatch, then the other enabled
        /// EditorBuildSettings scenes (<see cref="OpenFifa.Core.BuildSceneOrder"/>), so builds boot into the 11v11 flow.
        /// </summary>
        public static string[] PlayerScenes() => OpenFifa.Core.BuildSceneOrder.For(
            EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path),
            p => System.IO.File.Exists(p));

        [MenuItem("OpenFootball/Build/iOS")]
        public static void BuildIOS()
        {
            var scenes = PlayerScenes();

            if (scenes.Length == 0)
            {
                Debug.LogWarning("No scenes in build settings. Adding default scene.");
                scenes = new[] { "Assets/Scenes/Match.unity" };
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "build/iOS",
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"iOS build succeeded: {summary.totalSize} bytes, {summary.totalTime}");
            }
            else
            {
                Debug.LogError($"iOS build failed with {summary.totalErrors} errors");
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("OpenFootball/Build/macOS (Dev)")]
        public static void BuildMacOSDev()
        {
            var scenes = PlayerScenes();

            if (scenes.Length == 0)
            {
                scenes = new[] { "Assets/Scenes/Match.unity" };
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "build/macOS/OpenFootball.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"macOS build failed with {report.summary.totalErrors} errors");
                EditorApplication.Exit(1);
            }
        }
    }
}
