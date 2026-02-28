using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US042")]
    public class US042_BuildTests
    {
        [Test]
        public void BuildConfig_MacOS_MinVersion14()
        {
            var config = new BuildHardeningConfig();
            Assert.AreEqual("14.0", config.MacOSMinVersion);
        }

        [Test]
        public void BuildConfig_IPadOS_MinVersion17()
        {
            var config = new BuildHardeningConfig();
            Assert.AreEqual("17.0", config.IPadOSMinVersion);
        }

        [Test]
        public void BuildConfig_Architecture_ARM64()
        {
            var config = new BuildHardeningConfig();
            Assert.AreEqual("ARM64", config.TargetArchitecture);
        }

        [Test]
        public void BuildConfig_ScriptingBackend_IL2CPP()
        {
            var config = new BuildHardeningConfig();
            Assert.AreEqual("IL2CPP", config.ScriptingBackend);
        }

        [Test]
        public void BuildConfig_MaxBundleSize_Under200MB()
        {
            var config = new BuildHardeningConfig();
            Assert.Less(config.MaxBundleSizeMB, 200);
        }

        [Test]
        public void BuildConfig_RequiredFrameworks_MacOS()
        {
            var config = new BuildHardeningConfig();
            Assert.Contains("Metal", config.MacOSFrameworks);
            Assert.Contains("AppKit", config.MacOSFrameworks);
            Assert.Contains("AVFoundation", config.MacOSFrameworks);
        }

        [Test]
        public void BuildConfig_RequiredFrameworks_iPad()
        {
            var config = new BuildHardeningConfig();
            Assert.Contains("Metal", config.IPadFrameworks);
            Assert.Contains("UIKit", config.IPadFrameworks);
            Assert.Contains("AVFoundation", config.IPadFrameworks);
        }
    }
}
