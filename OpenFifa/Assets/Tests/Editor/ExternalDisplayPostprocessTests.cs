using System.IO;
using NUnit.Framework;
using OpenFifa.Editor;
using UnityEditor;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("ExternalDisplay")]
    public class ExternalDisplayPostprocessTests
    {
        /// <summary>The Info.plist template the installed editor's iOS export starts from.</summary>
        private static string TrampolinePlist()
        {
            var path = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "iOSSupport", "Trampoline", "Info.plist");
            if (!File.Exists(path)) Assert.Ignore($"iOS build support not installed ({path})");
            return File.ReadAllText(path);
        }

        [Test]
        public void AddExternalDisplayScene_RealUnityExportPlist_AddsExternalRoleWithDelegate()
        {
            var plist = TrampolinePlist();
            var result = ExternalDisplayPostprocess.AddExternalDisplayScene(plist, out var error);

            Assert.IsNull(error, error);
            StringAssert.Contains("<key>UIWindowSceneSessionRoleExternalDisplayNonInteractive</key>", result);
            StringAssert.Contains("<string>OFExternalDisplayScene</string>", result);
            StringAssert.Contains("<string>UnityScene</string>", result, "application scene must be kept");
            StringAssert.Contains("<!DOCTYPE plist", result, "DOCTYPE must survive");
            StringAssert.Contains("<key>UIRequiredDeviceCapabilities</key>", result, "other keys must be kept");
        }

        [Test]
        public void AddExternalDisplayScene_RunTwice_IsIdempotent()
        {
            var once = ExternalDisplayPostprocess.AddExternalDisplayScene(TrampolinePlist(), out _);
            var twice = ExternalDisplayPostprocess.AddExternalDisplayScene(once, out var error);
            Assert.IsNull(error, error);
            Assert.AreEqual(once, twice);
        }

        [Test]
        public void AddExternalDisplayScene_NoSceneManifest_RefusesAndLeavesPlistUnchanged()
        {
            const string appDelegatePlist = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict>" +
                                            "<key>CFBundleName</key><string>X</string></dict></plist>";
            var result = ExternalDisplayPostprocess.AddExternalDisplayScene(appDelegatePlist, out var error);
            Assert.IsNotNull(error, "must refuse: a manifest without an application role gives a black main window");
            Assert.AreEqual(appDelegatePlist, result);
        }

        [Test]
        public void AddExternalDisplayScene_ManifestWithoutApplicationRole_Refuses()
        {
            const string plist = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict>" +
                                 "<key>UIApplicationSceneManifest</key><dict>" +
                                 "<key>UISceneConfigurations</key><dict></dict></dict></dict></plist>";
            var result = ExternalDisplayPostprocess.AddExternalDisplayScene(plist, out var error);
            Assert.IsNotNull(error);
            Assert.AreEqual(plist, result);
        }
    }
}
