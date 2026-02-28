using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US041")]
    public class US041_MultiResTests
    {
        [Test]
        public void ResolutionConfig_MacOS_MinWidth1280()
        {
            var config = new ResolutionConfig();
            Assert.AreEqual(1280, config.MinWindowWidth);
        }

        [Test]
        public void ResolutionConfig_MacOS_MinHeight720()
        {
            var config = new ResolutionConfig();
            Assert.AreEqual(720, config.MinWindowHeight);
        }

        [Test]
        public void ResolutionConfig_UIDefaults_MinUITarget44()
        {
            var config = new ResolutionConfig();
            Assert.AreEqual(44, config.MinUITargetPoints);
        }

        [Test]
        public void ResolutionConfig_CanvasScaler_MatchHalf()
        {
            var config = new ResolutionConfig();
            Assert.AreEqual(0.5f, config.MatchWidthOrHeight, 0.001f);
        }

        [Test]
        public void SafeAreaLogic_FullScreen_NoInset()
        {
            var logic = new SafeAreaLogic();
            float minX, minY, maxX, maxY;
            logic.CalculateAnchors(0, 0, 1920, 1080, 1920, 1080, out minX, out minY, out maxX, out maxY);
            Assert.AreEqual(0f, minX, 0.001f);
            Assert.AreEqual(0f, minY, 0.001f);
            Assert.AreEqual(1f, maxX, 0.001f);
            Assert.AreEqual(1f, maxY, 0.001f);
        }

        [Test]
        public void SafeAreaLogic_WithInset_CalculatesAnchors()
        {
            var logic = new SafeAreaLogic();
            float minX, minY, maxX, maxY;
            // Safe area with 50px inset on all sides
            logic.CalculateAnchors(50, 50, 1820, 980, 1920, 1080, out minX, out minY, out maxX, out maxY);
            Assert.Greater(minX, 0f);
            Assert.Greater(minY, 0f);
            Assert.Less(maxX, 1f);
            Assert.Less(maxY, 1f);
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1600)]
        public void ResolutionConfig_MacOS_Resolutions_Supported(int width, int height)
        {
            var config = new ResolutionConfig();
            Assert.IsTrue(config.IsResolutionSupported(width, height));
        }

        [TestCase(1668, 2388)]
        [TestCase(2048, 2732)]
        [TestCase(1640, 2360)]
        public void ResolutionConfig_iPad_Resolutions_Supported(int width, int height)
        {
            var config = new ResolutionConfig();
            Assert.IsTrue(config.IsResolutionSupported(width, height));
        }
    }
}
