using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Presentation
{
    /// <summary>Render quality tiers: iPad/mobile gets the light tier, desktop and captures the high one, -spQuality overrides.</summary>
    [TestFixture]
    [Category("FID_PRESENTATION")]
    public class RenderQualityTierTests
    {
        [Test]
        public void Resolve_Platform_MobileGetsTheLightTier()
        {
            Assert.AreEqual(RenderQualityTier.Mobile, RenderQualityTiers.Resolve(new string[0], true));
            Assert.AreEqual(RenderQualityTier.High, RenderQualityTiers.Resolve(null, false));
        }

        [Test]
        public void Resolve_CommandLineOverride_WinsOverThePlatform()
        {
            Assert.AreEqual(RenderQualityTier.Mobile, RenderQualityTiers.Resolve(new[] { "player", "-spQuality", "low" }, false));
            Assert.AreEqual(RenderQualityTier.Mobile, RenderQualityTiers.Resolve(new[] { "-SPQUALITY", "iPad" }, false));
            Assert.AreEqual(RenderQualityTier.High, RenderQualityTiers.Resolve(new[] { "-spQuality", "high" }, true));
            Assert.AreEqual(RenderQualityTier.High, RenderQualityTiers.Resolve(new[] { "-spQuality", "bogus" }, false));
            Assert.AreEqual(RenderQualityTier.Mobile, RenderQualityTiers.Resolve(new[] { "-spQuality" }, true), "missing value: platform");
        }

        [Test]
        public void For_MobileTier_IsLighterThanHighOnEveryCost()
        {
            var hi = RenderQualityTiers.For(RenderQualityTier.High);
            var lo = RenderQualityTiers.For(RenderQualityTier.Mobile);
            Assert.AreEqual(4, hi.MsaaSamples);
            Assert.AreEqual(PostAntialiasing.SmaaHigh, hi.PostAa);
            Assert.AreEqual(4096, hi.ShadowResolution);
            Assert.IsTrue(hi.Ssao);
            Assert.LessOrEqual(lo.MsaaSamples, 2);
            Assert.That(lo.PostAa, Is.EqualTo(PostAntialiasing.Fxaa).Or.EqualTo(PostAntialiasing.SmaaLow));
            Assert.AreEqual(2048, lo.ShadowResolution);
            Assert.That(lo.ShadowCascades, Is.InRange(2, 3));
            Assert.AreEqual(2, lo.SoftShadowQuality, "medium soft shadows");
            Assert.IsFalse(lo.Ssao);
            Assert.LessOrEqual(lo.TurfBaseWidth, 512);
            foreach (var p in new[] { hi, lo })
            {
                Assert.That(p.TurfDetailAniso, Is.InRange(2, 4), "detail maps: aniso 2-4");
                Assert.LessOrEqual(p.TurfBaseAniso, 4);
            }
        }

        [Test]
        public void TextureBytes_MipChainAndBlocks()
        {
            Assert.AreEqual(4L * 4 * 4, RenderQualityTiers.TextureBytes(4, 4, 32, false));
            // 4x4 RGBA32 with mips: 64 + 16 + 4 bytes.
            Assert.AreEqual(84L, RenderQualityTiers.TextureBytes(4, 4, 32, true));
            // DXT5 (8 bpp) pads each mip to 4x4 blocks: 16 + 16 + 16.
            Assert.AreEqual(48L, RenderQualityTiers.TextureBytes(4, 4, 8, true, true));
        }

        [Test]
        public void TurfBytes_TiersCutTurfGpuMemory()
        {
            long before = RenderQualityTiers.LegacyTurfBytes();
            long desktop = RenderQualityTiers.TurfBytes(RenderQualityTiers.For(RenderQualityTier.High));
            long mobile = RenderQualityTiers.TurfBytes(RenderQualityTiers.For(RenderQualityTier.Mobile));
            // Before 6.00 MiB (all RGBA32), desktop 3.00 MiB (albedo maps DXT5), mobile 1.50 MiB (half-size maps).
            Assert.AreEqual(3.0, desktop / 1048576.0, 0.05);
            Assert.AreEqual(1.5, mobile / 1048576.0, 0.05);
            Assert.AreEqual(6.0, before / 1048576.0, 0.1, "4 x RGBA32 + mips at 1024x512 / 512x256 / 512 / 512");
            Assert.Less(desktop, before * 0.55, "compressing the albedo maps should roughly halve the turf");
            Assert.Less(mobile, before * 0.3, "mobile maps are a quarter of the pixels");
        }
    }
}
