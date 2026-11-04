using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("ExternalDisplay")]
    public class ExternalDisplayPolicyTests
    {
        private ExternalDisplayConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = new ExternalDisplayConfig();
        }

        [Test]
        public void RenderResolution_1440pMonitor_RendersNative()
        {
            float start = ExternalDisplayPolicy.StartScale(_config, 1440);
            var size = ExternalDisplayPolicy.RenderResolution(_config, 2560, 1440, start);
            Assert.AreEqual(new PixelSize(2560, 1440), size);
        }

        [Test]
        public void RenderResolution_4KMonitor_StartsAt1440pKeepingAspect()
        {
            float start = ExternalDisplayPolicy.StartScale(_config, 2160);
            var size = ExternalDisplayPolicy.RenderResolution(_config, 3840, 2160, start);
            Assert.AreEqual(new PixelSize(2560, 1440), size);
        }

        [Test]
        public void RenderResolution_4KMonitor_FullScaleIsNative4K()
        {
            var size = ExternalDisplayPolicy.RenderResolution(_config, 3840, 2160, 1f);
            Assert.AreEqual(new PixelSize(3840, 2160), size);
        }

        [Test]
        public void RenderResolution_5KMonitor_CappedAt2160()
        {
            var size = ExternalDisplayPolicy.RenderResolution(_config, 5120, 2880, 1f);
            Assert.AreEqual(2160, size.Height);
            Assert.AreEqual(3840, size.Width);
        }

        [Test]
        public void RenderResolution_ScaleBelowFloor_NeverDropsUnder1440()
        {
            var size = ExternalDisplayPolicy.RenderResolution(_config, 3840, 2160, 0.1f);
            Assert.AreEqual(1440, size.Height, $"got {size}");
        }

        [Test]
        public void RenderResolution_1080pMonitor_RendersNativeNotUpscaled()
        {
            var size = ExternalDisplayPolicy.RenderResolution(_config, 1920, 1080, 1f);
            Assert.AreEqual(new PixelSize(1920, 1080), size);
        }

        [Test]
        public void RenderResolution_Ultrawide_KeepsMonitorAspect()
        {
            var size = ExternalDisplayPolicy.RenderResolution(_config, 3440, 1440, 1f);
            Assert.AreEqual(new PixelSize(3440, 1440), size);
        }

        [Test]
        public void RenderResolution_AlwaysEvenDimensions()
        {
            for (float s = 0.5f; s <= 1f; s += 0.0625f)
            {
                var size = ExternalDisplayPolicy.RenderResolution(_config, 3840, 2160, s);
                Assert.AreEqual(0, size.Width % 2, $"odd width {size} at scale {s}");
                Assert.AreEqual(0, size.Height % 2, $"odd height {size} at scale {s}");
            }
        }

        [Test]
        public void Viewport_16by9_IsFullScreen()
        {
            Assert.IsTrue(ExternalDisplayPolicy.Viewport(_config, 2560, 1440).IsFull);
        }

        [Test]
        public void Viewport_21by9Ultrawide_IsFullScreen()
        {
            Assert.IsTrue(ExternalDisplayPolicy.Viewport(_config, 3440, 1440).IsFull);
        }

        [Test]
        public void Viewport_32by9SuperUltrawide_IsPillarboxedToMaxAspect()
        {
            var vp = ExternalDisplayPolicy.Viewport(_config, 5120, 1440);
            Assert.AreEqual(1f, vp.Height, 0.0001f);
            Assert.AreEqual(_config.MaxAspect, vp.Width * 5120f / 1440f, 0.01f);
            Assert.AreEqual((1f - vp.Width) / 2f, vp.X, 0.0001f);
        }

        [Test]
        public void Viewport_4by3_IsLetterboxedTo16by10()
        {
            var vp = ExternalDisplayPolicy.Viewport(_config, 2048, 1536);
            Assert.AreEqual(1f, vp.Width, 0.0001f);
            Assert.AreEqual(16f / 10f, 2048f / (1536f * vp.Height), 0.01f);
            Assert.AreEqual((1f - vp.Height) / 2f, vp.Y, 0.0001f);
        }

        [Test]
        public void FrameRate_CappedByMonitorRefresh()
        {
            Assert.AreEqual(60, ExternalDisplayPolicy.FrameRate(_config, 120));
            Assert.AreEqual(50, ExternalDisplayPolicy.FrameRate(_config, 50));
            Assert.AreEqual(60, ExternalDisplayPolicy.FrameRate(_config, 0));
        }

        [Test]
        public void DeviceScreenResolution_HalvesIPadResolution()
        {
            var size = ExternalDisplayPolicy.DeviceScreenResolution(_config, 2732, 2048);
            Assert.AreEqual(new PixelSize(1366, 1024), size);
        }

        [Test]
        public void CompanionOrthoSize_FitsWholePitchOn4by3()
        {
            float ortho = ExternalDisplayPolicy.CompanionOrthoSize(50f, 30f, 4f / 3f);
            Assert.GreaterOrEqual(ortho, 15f, "pitch width must fit vertically");
            Assert.GreaterOrEqual(ortho * 4f / 3f, 25f, "pitch length must fit horizontally");
        }

        [Test]
        public void Governor_OverBudget_StepsDownToFloor()
        {
            var gov = new DynamicResolutionGovernor(_config, 1f, 0.6667f, 1f, 60);
            for (int i = 0; i < 20 * _config.SampleFrames; i++) gov.AddFrame(1f / 30f);
            Assert.AreEqual(0.6667f, gov.Scale, 0.0001f);
        }

        [Test]
        public void Governor_Headroom_StepsUpToCeiling()
        {
            var gov = new DynamicResolutionGovernor(_config, 0.6667f, 0.6667f, 1f, 60);
            for (int i = 0; i < 20 * _config.SampleFrames; i++) gov.AddFrame(0.008f);
            Assert.AreEqual(1f, gov.Scale, 0.0001f);
        }

        [Test]
        public void Governor_OnBudget_HoldsScale()
        {
            var gov = new DynamicResolutionGovernor(_config, 0.8f, 0.6667f, 1f, 60);
            for (int i = 0; i < 10 * _config.SampleFrames; i++) gov.AddFrame(1f / 60f);
            Assert.AreEqual(0.8f, gov.Scale, 0.0001f);
        }

        [Test]
        public void Governor_IgnoresHitches()
        {
            var gov = new DynamicResolutionGovernor(_config, 1f, 0.6667f, 1f, 60);
            for (int i = 0; i < 10 * _config.SampleFrames; i++) Assert.IsFalse(gov.AddFrame(0.5f));
            Assert.AreEqual(1f, gov.Scale, 0.0001f);
        }

        [Test]
        public void Governor_ChangesOnlyAfterFullSampleWindow()
        {
            var gov = new DynamicResolutionGovernor(_config, 1f, 0.6667f, 1f, 60);
            for (int i = 0; i < _config.SampleFrames - 1; i++) Assert.IsFalse(gov.AddFrame(1f / 20f));
            Assert.IsTrue(gov.AddFrame(1f / 20f));
        }
    }
}
