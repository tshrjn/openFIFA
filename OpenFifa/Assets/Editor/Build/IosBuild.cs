using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenFifa.Editor
{
    /// <summary>
    /// iPad-only iOS export for CI (.github/workflows/ipad.yml). Applies every iOS player setting in code
    /// at build time and restores the previous values afterwards, so ProjectSettings/ is never rewritten.
    ///
    /// Entry points for -executeMethod:
    ///   OpenFifa.Editor.IosBuild.ExportFromCommandLine   (exports listed in -ipadExports, default "device,simulator")
    ///   OpenFifa.Editor.IosBuild.ExportDevice            (device SDK, signed for App Store / TestFlight)
    ///   OpenFifa.Editor.IosBuild.ExportSimulator         (simulator SDK, arm64, unsigned)
    ///
    /// Command-line options (each also readable from the env var in brackets):
    ///   -ipadOutDir build/ipad        [IPAD_OUT_DIR]       relative paths resolve against the repo root
    ///   -ipadExports device,simulator [IPAD_EXPORTS]
    ///   -ipadBuildNumber 176...       [IPAD_BUILD_NUMBER]  CFBundleVersion; default: 10-second ticks since epoch
    ///   -ipadMarketingVersion 0.1.0   [IPAD_MARKETING_VERSION] CFBundleShortVersionString; default: PlayerSettings
    ///   -ipadBundleId com.x.y         [IPAD_BUNDLE_ID]     default com.tshrjn.openfifa (ios-factory convention)
    ///   -appleTeamId ABCDE12345       [APPLE_TEAM_ID]      written as DEVELOPMENT_TEAM; may also be passed to xcodebuild
    /// Device exports go to &lt;outDir&gt;/device, simulator exports to &lt;outDir&gt;/simulator.
    /// </summary>
    public static class IosBuild
    {
        public const string DefaultBundleId = "com.tshrjn.openfootball";
        public const string ProductName = "OpenFootball";
        public const string MinimumIosVersion = "16.0";
        public const string ProvisioningProfilePrefix = "ios-factory ";

        /// <summary>Set while one of our exports runs, read by <see cref="IosBuildPostprocess"/>.</summary>
        internal static IosExportContext Current { get; private set; }

        [MenuItem("OpenFootball/Build/iPad (Device Xcode project)")]
        public static void ExportDevice() => Run(new[] { IosExportKind.Device });

        [MenuItem("OpenFootball/Build/iPad (Simulator Xcode project)")]
        public static void ExportSimulator() => Run(new[] { IosExportKind.Simulator });

        public static void ExportFromCommandLine()
        {
            var list = Option("ipadExports", "IPAD_EXPORTS", "device,simulator");
            var kinds = list.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseKind)
                .Distinct()
                .ToArray();
            if (kinds.Length == 0)
            {
                Fail($"-ipadExports '{list}' names no export (use device, simulator or both)");
            }
            Run(kinds);
        }

        private static IosExportKind ParseKind(string s)
        {
            switch (s.Trim().ToLowerInvariant())
            {
                case "device": return IosExportKind.Device;
                case "simulator":
                case "sim": return IosExportKind.Simulator;
                default:
                    Fail($"unknown iPad export '{s}' (use device or simulator)");
                    return IosExportKind.Device;
            }
        }

        private static void Run(IosExportKind[] kinds)
        {
            var outDir = ResolveOutDir(Option("ipadOutDir", "IPAD_OUT_DIR", "build/ipad"));
            var bundleId = Option("ipadBundleId", "IPAD_BUNDLE_ID", DefaultBundleId);
            var buildNumber = Option("ipadBuildNumber", "IPAD_BUILD_NUMBER", "");
            if (string.IsNullOrEmpty(buildNumber))
            {
                buildNumber = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 10).ToString();
            }
            if (!buildNumber.All(c => char.IsDigit(c) || c == '.'))
            {
                Fail($"build number '{buildNumber}' must be digits and dots");
            }
            var marketingVersion = Option("ipadMarketingVersion", "IPAD_MARKETING_VERSION", "");
            var teamId = Option("appleTeamId", "APPLE_TEAM_ID", "");

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                Fail("could not switch the active build target to iOS (is the iOS module installed?)");
            }

            var snapshot = IosSettingsSnapshot.Capture();
            var failed = false;
            try
            {
                foreach (var kind in kinds)
                {
                    var path = Path.Combine(outDir, kind == IosExportKind.Device ? "device" : "simulator");
                    var ctx = new IosExportContext(kind, bundleId, teamId, buildNumber, path, RepoRoot());
                    Apply(ctx, marketingVersion);
                    if (!Export(ctx))
                    {
                        failed = true;
                        break;
                    }
                }
            }
            finally
            {
                Current = null;
                snapshot.Restore();
            }
            if (failed)
            {
                Fail("iPad export failed (see the build report above)");
            }
        }

        private static void Apply(IosExportContext ctx, string marketingVersion)
        {
            var ios = NamedBuildTarget.iOS;
            PlayerSettings.SetApplicationIdentifier(ios, ctx.BundleId);
            PlayerSettings.productName = ProductName;
            if (!string.IsNullOrEmpty(marketingVersion))
            {
                PlayerSettings.bundleVersion = marketingVersion;
            }
            PlayerSettings.iOS.buildNumber = ctx.BuildNumber;

            // iPad only, landscape only, no multitasking (a landscape-only iPad app must opt out of it).
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPadOnly;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.targetOSVersionString = MinimumIosVersion;

            // IL2CPP, ARM64 (1 = ARM64 for iOS), Metal only. "Optimize size" IL2CPP codegen keeps the
            // Xcode compile (the expensive macOS minutes) shorter.
            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(ios, 1);
            PlayerSettings.SetIl2CppCompilerConfiguration(ios, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetIl2CppCodeGeneration(ios, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });

            if (ctx.Kind == IosExportKind.Device)
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
                // Manual App Store signing, same identity and profile names ios-factory uses.
                // The postprocess step writes the per-target signing build settings.
                PlayerSettings.iOS.appleEnableAutomaticSigning = false;
                PlayerSettings.iOS.iOSManualProvisioningProfileType = ProvisioningProfileType.Distribution;
                PlayerSettings.iOS.appleDeveloperTeamID = ctx.TeamId;
            }
            else
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
                PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            }
            EditorUserBuildSettings.iOSXcodeBuildConfig = XcodeBuildConfig.Release;
        }

        private static bool Export(IosExportContext ctx)
        {
            if (Directory.Exists(ctx.OutputPath))
            {
                Directory.Delete(ctx.OutputPath, true);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ctx.OutputPath) ?? ".");

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Fail("no enabled scenes in EditorBuildSettings");
            }

            Debug.Log($"[IosBuild] exporting {ctx.Kind} Xcode project: bundle {ctx.BundleId}, build {ctx.BuildNumber}, " +
                      $"scenes [{string.Join(", ", scenes)}] -> {ctx.OutputPath}");
            Current = ctx;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ctx.OutputPath,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            });
            Current = null;

            var summary = report.summary;
            Debug.Log($"[IosBuild] {ctx.Kind}: {summary.result}, {summary.totalErrors} errors, " +
                      $"{summary.totalWarnings} warnings, {summary.totalTime}");
            return summary.result == BuildResult.Succeeded;
        }

        private static string Option(string arg, string env, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-" + arg)
                {
                    return args[i + 1].Trim();
                }
            }
            var value = Environment.GetEnvironmentVariable(env);
            return string.IsNullOrEmpty(value) ? fallback : value.Trim();
        }

        /// <summary>Repository root: the parent of the Unity project folder (OpenFifa/).</summary>
        internal static string RepoRoot()
        {
            var projectRoot = Directory.GetParent(Application.dataPath);
            return (projectRoot?.Parent ?? projectRoot)?.FullName ?? Directory.GetCurrentDirectory();
        }

        private static string ResolveOutDir(string dir) =>
            Path.GetFullPath(Path.IsPathRooted(dir) ? dir : Path.Combine(RepoRoot(), dir));

        private static void Fail(string message)
        {
            Debug.LogError("[IosBuild] " + message);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
            throw new BuildFailedException(message);
        }
    }
}
