using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace OpenFifa.Editor
{
    /// <summary>
    /// Runs after an <see cref="IosBuild"/> export (and only then): fixes up Info.plist, supplies a placeholder
    /// app icon when none is configured, writes per-target manual signing for device exports, and drops
    /// openfifa-export.env next to the Xcode project for the macOS CI scripts (ci/ipad/*.sh).
    /// </summary>
    public sealed class IosBuildPostprocess : IPostprocessBuildWithReport
    {
        private const string PlaceholderIcon = "ci/ipad/AppIcon-1024.png";
        private static readonly string[] Landscape = { "UIInterfaceOrientationLandscapeLeft", "UIInterfaceOrientationLandscapeRight" };

        public int callbackOrder => 1000;

        public void OnPostprocessBuild(BuildReport report)
        {
            var ctx = IosBuild.Current;
            if (ctx == null || report.summary.platform != BuildTarget.iOS)
            {
                return;
            }
            var outDir = ctx.OutputPath;
            PatchInfoPlist(Path.Combine(outDir, "Info.plist"));
            EnsureAppIcon(ctx);
            if (ctx.Kind == IosExportKind.Device)
            {
                WriteSigning(ctx);
            }
            WriteExportEnv(ctx);
        }

        private static void PatchInfoPlist(string path)
        {
            if (!File.Exists(path))
            {
                throw new BuildFailedException($"[IosBuild] exported project has no Info.plist at {path}");
            }
            var plist = IosPlistXml.Load(path);
            // No export-compliance prompt in App Store Connect (the game uses no non-exempt encryption).
            plist.SetBool("ITSAppUsesNonExemptEncryption", false);
            // Landscape-only iPad app: must opt out of multitasking or App Store validation rejects it.
            plist.SetBool("UIRequiresFullScreen", true);
            plist.SetStringArray("UISupportedInterfaceOrientations", Landscape);
            plist.SetStringArray("UISupportedInterfaceOrientations~ipad", Landscape);
            plist.Save(path);
            Debug.Log("[IosBuild] Info.plist: ITSAppUsesNonExemptEncryption=false, UIRequiresFullScreen=true, landscape only");
        }

        /// <summary>
        /// App Store uploads fail without a 1024px marketing icon. When the project has no iOS icon yet, replace
        /// the exported AppIcon set with a single-size icon (supported since Xcode 14) from ci/ipad/.
        /// </summary>
        private static void EnsureAppIcon(IosExportContext ctx)
        {
            var icons = PlayerSettings.GetIcons(NamedBuildTarget.iOS, IconKind.Application);
            if (icons != null && icons.Any(t => t != null))
            {
                return;
            }
            var source = Path.Combine(ctx.RepoRoot, PlaceholderIcon);
            var set = Directory.GetDirectories(ctx.OutputPath, "AppIcon.appiconset", SearchOption.AllDirectories).FirstOrDefault();
            if (!File.Exists(source) || set == null)
            {
                Debug.LogWarning($"[IosBuild] no iOS app icon configured and placeholder not applied (icon {source}, set {set}); " +
                                 "TestFlight upload will fail validation");
                return;
            }
            foreach (var f in Directory.GetFiles(set))
            {
                File.Delete(f);
            }
            File.Copy(source, Path.Combine(set, "Icon-1024.png"));
            File.WriteAllText(Path.Combine(set, "Contents.json"),
                "{\n  \"images\" : [\n    {\n      \"filename\" : \"Icon-1024.png\",\n      \"idiom\" : \"universal\",\n" +
                "      \"platform\" : \"ios\",\n      \"size\" : \"1024x1024\"\n    }\n  ],\n" +
                "  \"info\" : {\n    \"author\" : \"xcode\",\n    \"version\" : 1\n  }\n}\n");
            Debug.Log($"[IosBuild] placeholder app icon written to {set}");
        }

        /// <summary>
        /// Manual App Store signing, matching ios-factory: identity "Apple Distribution" and the profile
        /// "ios-factory &lt;bundle id&gt;" on the app target only. UnityFramework must not name a profile
        /// (frameworks do not support them), so it gets the identity alone and is re-signed on export.
        /// </summary>
        private static void WriteSigning(IosExportContext ctx)
        {
#if UNITY_IOS
            var path = PBXProject.GetPBXProjectPath(ctx.OutputPath);
            var proj = new PBXProject();
            proj.ReadFromFile(path);
            var app = proj.GetUnityMainTargetGuid();
            var framework = proj.GetUnityFrameworkTargetGuid();
            foreach (var target in new[] { app, framework })
            {
                proj.SetBuildProperty(target, "CODE_SIGN_STYLE", "Manual");
                proj.SetBuildProperty(target, "CODE_SIGN_IDENTITY", "Apple Distribution");
                proj.SetBuildProperty(target, "CODE_SIGN_IDENTITY[sdk=iphoneos*]", "Apple Distribution");
                if (!string.IsNullOrEmpty(ctx.TeamId))
                {
                    proj.SetBuildProperty(target, "DEVELOPMENT_TEAM", ctx.TeamId);
                }
            }
            proj.SetBuildProperty(app, "PROVISIONING_PROFILE_SPECIFIER", ctx.ProvisioningProfileName);
            proj.SetBuildProperty(framework, "PROVISIONING_PROFILE_SPECIFIER", "");
            proj.SetBuildProperty(app, "TARGETED_DEVICE_FAMILY", "2");
            proj.WriteToFile(path);
            Debug.Log($"[IosBuild] manual signing: Apple Distribution / '{ctx.ProvisioningProfileName}'" +
                      (string.IsNullOrEmpty(ctx.TeamId) ? " (team ID left for xcodebuild DEVELOPMENT_TEAM=)" : $" / team {ctx.TeamId}"));
#else
            Debug.LogWarning("[IosBuild] UNITY_IOS is not defined, so per-target signing was not written; " +
                             "ci/ipad/archive-upload.sh will stop before archiving");
#endif
        }

        private static void WriteExportEnv(IosExportContext ctx)
        {
            File.WriteAllText(Path.Combine(ctx.OutputPath, "openfifa-export.env"),
                $"EXPORT_KIND={Quote(ctx.Kind.ToString().ToLowerInvariant())}\n" +
                $"BUNDLE_ID={Quote(ctx.BundleId)}\n" +
                $"BUILD_NUMBER={Quote(ctx.BuildNumber)}\n" +
                $"PRODUCT_NAME={Quote(PlayerSettings.productName)}\n" +
                $"PROFILE_NAME={Quote(ctx.ProvisioningProfileName)}\n" +
                $"UNITY_VERSION={Quote(Application.unityVersion)}\n");
        }

        /// <summary>Single-quote a value so the file can be sourced by bash.</summary>
        private static string Quote(string s) => "'" + (s ?? "").Replace("'", "'\\''") + "'";
    }
}
