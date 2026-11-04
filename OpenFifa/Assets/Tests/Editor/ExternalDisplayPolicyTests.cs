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
        public void RenderTiers_4KMonitor_Are1080Emergency_1440_1800_2160()
        {
            CollectionAssert.AreEqual(new[] { 1080, 1440, 1800, 2160 }, ExternalDisplayPolicy.RenderTiers(_config, 2160));
        }

        [Test]
        public void RenderTiers_5KMonitor_CappedAt2160()
        {
            CollectionAssert.AreEqual(new[] { 1080, 1440, 1800, 2160 }, ExternalDisplayPolicy.RenderTiers(_config, 2880));
        }

        [Test]
        public void RenderTiers_1600pMonitor_IncludesNative()
        {
            CollectionAssert.AreEqual(new[] { 1080, 1440, 1600 }, ExternalDisplayPolicy.RenderTiers(_config, 1600));
        }

        [Test]
        public void RenderTiers_1080pMonitor_IsJustNative()
        {
            CollectionAssert.AreEqual(new[] { 1080 }, ExternalDisplayPolicy.RenderTiers(_config, 1080));
        }

        [Test]
        public void RenderResolution_4KMonitorAt1440_KeepsAspect()
        {
            Assert.AreEqual(new PixelSize(2560, 1440), ExternalDisplayPolicy.RenderResolution(3840, 2160, 1440));
        }

        [Test]
        public void RenderResolution_Ultrawide_KeepsMonitorAspect()
        {
            Assert.AreEqual(new PixelSize(3440, 1440), ExternalDisplayPolicy.RenderResolution(3440, 1440, 1440));
        }

        [Test]
        public void RenderResolution_NeverExceedsNative()
        {
            Assert.AreEqual(new PixelSize(1920, 1080), ExternalDisplayPolicy.RenderResolution(1920, 1080, 2160));
        }

        [Test]
        public void RenderResolution_AlwaysEvenDimensions()
        {
            foreach (int h in new[] { 1080, 1440, 1599, 1800, 2160 })
            {
                var size = ExternalDisplayPolicy.RenderResolution(3440, 1600, h);
                Assert.AreEqual(0, size.Width % 2, $"odd width {size} at {h}");
                Assert.AreEqual(0, size.Height % 2, $"odd height {size} at {h}");
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
        public void Governor_4K_StartsAt1440p()
        {
            var gov = new DynamicResolutionGovernor(_config, 2160, 60);
            Assert.AreEqual(1440, gov.RenderHeight);
            Assert.AreEqual(1440, gov.FloorHeight);
        }

        [Test]
        public void Governor_OverBudget_NeverDropsBelow1440WhenCool()
        {
            var gov = new DynamicResolutionGovernor(_config, 2160, 60);
            Feed(gov, 1f / 30f, 20);
            Assert.AreEqual(1440, gov.RenderHeight);
        }

        [Test]
        public void Governor_Headroom_ClimbsTierByTierTo4K()
        {
            var gov = new DynamicResolutionGovernor(_config, 2160, 60);
            Feed(gov, 0.008f, _config.UpscaleWindows);
            Assert.AreEqual(1800, gov.RenderHeight, "one tier after the dwell");
            Feed(gov, 0.008f, _config.UpscaleWindows);
            Assert.AreEqual(2160, gov.RenderHeight);
            Feed(gov, 0.008f, _config.UpscaleWindows * 3);
            Assert.AreEqual(2160, gov.RenderHeight, "capped at 4K");
        }

        [Test]
        public void Governor_SingleHeadroomWindow_DoesNotStepUp()
        {
            var gov = new DynamicResolutionGovernor(_config, 2160, 60);
            Feed(gov, 0.008f, _config.UpscaleWindows - 1);
            Feed(gov, 1f / 60f, 1);
            Feed(gov, 0.008f, _config.UpscaleWindows - 1);
            Assert.AreEqual(1440, gov.RenderHeight, "headroom windows must be consecutive");
        }

        [Test]
        public void Governor_OverBudgetAt4K_StepsDownOneTierPerWindow()
        {
            var gov = new DynamicResolutionGovernor(_config, 2160, 60);
            Feed(gov, 0.008f, _config.UpscaleWindows * 2);
            Assert.AreEqual(2160, gov.RenderHeight);
            Feed(gov, 1f / 40f, 1);
            Assert.AreEqual(1800, gov.RenderHeight);
        }

        [Test]
        public void Governor_SeriousThermal_CapsAt1440AndAllows1080()
        {
            var gov = new DynamicResolutionGovernor(_config, 2160, 60);
            Feed(gov, 0.008f, _config.UpscaleWindows * 2);
            Assert.IsTrue(gov.SetThermal(DeviceThermalLevel.Serious));
            Assert.AreEqual(1440, gov.RenderHeight);
            Feed(gov, 1f / 40f, 1);
            Assert.AreEqual(1080, gov.RenderHeight, "emergency tier when hot");
        }

        [Test]
        public void Governor_CriticalThermal_HoldsEmergencyTier()
        {
            var gov = new DynamicResolutionGovernor(_config, 2160, 60);
            gov.SetThermal(DeviceThermalLevel.Critical);
            Assert.AreEqual(1080, gov.RenderHeight);
            Feed(gov, 0.008f, _config.UpscaleWindows * 3);
            Assert.AreEqual(1080, gov.RenderHeight);
        }

        [Test]
        public void Governor_CoolsDown_ReturnsToFloor()
        {
            var gov = new DynamicResolutionGovernor(_config, 2160, 60);
            gov.SetThermal(DeviceThermalLevel.Critical);
            gov.SetThermal(DeviceThermalLevel.Nominal);
            Assert.AreEqual(1440, gov.RenderHeight);
        }

        [Test]
        public void Governor_IgnoresHitches()
        {
            var gov = new DynamicResolutionGovernor(_config, 2160, 60);
            for (int i = 0; i < 10 * _config.SampleFrames; i++) Assert.IsFalse(gov.AddFrame(0.5f));
            Assert.AreEqual(1440, gov.RenderHeight);
        }

        private void Feed(DynamicResolutionGovernor gov, float seconds, int windows)
        {
            for (int i = 0; i < windows * _config.SampleFrames; i++) gov.AddFrame(seconds);
        }
    }
}
