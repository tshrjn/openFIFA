using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Builds a Linux player that boots straight into the match, for headless
    /// gameplay recording on the build box (see scripts/capture-gameplay.sh).
    /// </summary>
    public static class CaptureBuild
    {
        [MenuItem("OpenFootball/Build/Linux Capture Player")]
        public static void BuildLinuxCapture()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Match.unity" },
                locationPathName = "build/LinuxCapture/OpenFootball.x86_64",
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            };

            // The box has no Linux IL2CPP module; Mono is fine for a review capture. Restore afterwards so
            // macOS builds keep IL2CPP (Standalone shares one scripting-backend setting).
            var standalone = UnityEditor.Build.NamedBuildTarget.Standalone;
            var previousBackend = PlayerSettings.GetScriptingBackend(standalone);
            PlayerSettings.SetScriptingBackend(standalone, ScriptingImplementation.Mono2x);
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(standalone, previousBackend);
            }
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Linux capture build failed with {report.summary.totalErrors} errors");
                EditorApplication.Exit(1);
            }
        }
    }
}
