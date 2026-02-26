using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenFifa.Editor
{
    public static class BuildScript
    {
        [MenuItem("OpenFifa/Build/iOS")]
        public static void BuildIOS()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

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

        [MenuItem("OpenFifa/Build/macOS (Dev)")]
        public static void BuildMacOSDev()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                scenes = new[] { "Assets/Scenes/Match.unity" };
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "build/macOS/OpenFifa.app",
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
