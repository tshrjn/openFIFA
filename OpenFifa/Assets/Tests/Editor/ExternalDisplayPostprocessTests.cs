using NUnit.Framework;
using OpenFifa.Editor;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("ExternalDisplay")]
    public class ExternalDisplayPostprocessTests
    {
        private const string UnityPlist =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
            "<plist version=\"1.0\"><dict>" +
            "<key>CFBundleName</key><string>OpenFootball</string>" +
            "<key>UIApplicationSceneManifest</key><dict>" +
            "<key>UIApplicationSupportsMultipleScenes</key><false/>" +
            "<key>UISceneConfigurations</key><dict>" +
            "<key>UIWindowSceneSessionRoleApplication</key><array><dict>" +
            "<key>UISceneConfigurationName</key><string>Default Configuration</string>" +
            "<key>UISceneDelegateClassName</key><string>UnityScene</string>" +
            "</dict></array></dict></dict></dict></plist>";

        [Test]
        public void AddExternalDisplayScene_UnityPlist_AddsExternalRoleWithDelegate()
        {
            var result = ExternalDisplayPostprocess.AddExternalDisplayScene(UnityPlist);
            StringAssert.Contains("<key>UIWindowSceneSessionRoleExternalDisplayNonInteractive</key>", result);
            StringAssert.Contains("<string>OFExternalDisplayScene</string>", result);
            StringAssert.Contains("<string>UnityScene</string>", result, "application scene must be kept");
            StringAssert.Contains("<string>OpenFootball</string>", result, "other keys must be kept");
        }

        [Test]
        public void AddExternalDisplayScene_RunTwice_IsIdempotent()
        {
            var once = ExternalDisplayPostprocess.AddExternalDisplayScene(UnityPlist);
            var twice = ExternalDisplayPostprocess.AddExternalDisplayScene(once);
            Assert.AreEqual(once, twice);
        }

        [Test]
        public void AddExternalDisplayScene_NoSceneManifest_CreatesIt()
        {
            const string bare = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict>" +
                                "<key>CFBundleName</key><string>X</string></dict></plist>";
            var result = ExternalDisplayPostprocess.AddExternalDisplayScene(bare);
            StringAssert.Contains("<key>UIApplicationSceneManifest</key>", result);
            StringAssert.Contains("<key>UISceneConfigurations</key>", result);
            StringAssert.Contains("OFExternalDisplayScene", result);
        }
    }
}
