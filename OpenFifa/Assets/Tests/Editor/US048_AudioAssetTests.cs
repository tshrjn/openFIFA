using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-048")]
    [Category("Audio")]
    public class US048_AudioAssetTests
    {
        [Test]
        public void AudioAssetConfig_WhistleClip_NotNull()
        {
            var config = new AudioAssetConfig();
            Assert.IsNotNull(config.WhistleClipName);
            Assert.IsNotEmpty(config.WhistleClipName);
        }

        [Test]
        public void AudioAssetConfig_KickVariations_AtLeast3()
        {
            var config = new AudioAssetConfig();
            Assert.GreaterOrEqual(config.KickClipNames.Count, 3);
        }

        [Test]
        public void AudioAssetConfig_CrowdAmbient_NotNull()
        {
            var config = new AudioAssetConfig();
            Assert.IsNotNull(config.CrowdAmbientClipName);
            Assert.IsNotEmpty(config.CrowdAmbientClipName);
        }

        [Test]
        public void AudioAssetConfig_GoalCheer_NotNull()
        {
            var config = new AudioAssetConfig();
            Assert.IsNotNull(config.GoalCheerClipName);
            Assert.IsNotEmpty(config.GoalCheerClipName);
        }

        [Test]
        public void AudioAssetConfig_Compression_Vorbis()
        {
            var config = new AudioAssetConfig();
            Assert.AreEqual("Vorbis", config.CompressionFormat);
        }

        [Test]
        public void AudioAssetConfig_Quality_70Percent()
        {
            var config = new AudioAssetConfig();
            Assert.AreEqual(70, config.CompressionQuality);
        }

        [Test]
        public void AudioAssetConfig_SFX_ForceToMono()
        {
            var config = new AudioAssetConfig();
            Assert.IsTrue(config.ForceToMonoSFX);
        }

        [Test]
        public void AudioAssetConfig_Ambient_Streaming()
        {
            var config = new AudioAssetConfig();
            Assert.AreEqual("Streaming", config.AmbientLoadType);
        }

        [Test]
        public void AudioAssetConfig_SFX_DecompressOnLoad()
        {
            var config = new AudioAssetConfig();
            Assert.AreEqual("DecompressOnLoad", config.SFXLoadType);
        }

        [Test]
        public void KickVariationSelector_RandomIndex_InRange()
        {
            var config = new AudioAssetConfig();
            var selector = new KickVariationSelector(config.KickClipNames.Count);

            for (int i = 0; i < 50; i++)
            {
                int idx = selector.GetNextIndex();
                Assert.GreaterOrEqual(idx, 0);
                Assert.Less(idx, config.KickClipNames.Count);
            }
        }

        [Test]
        public void KickVariationSelector_MultipleGets_NoConsecutiveRepeats()
        {
            var selector = new KickVariationSelector(3);
            // After enough iterations, at least some should differ from previous
            int sameCount = 0;
            int lastIdx = -1;
            for (int i = 0; i < 30; i++)
            {
                int idx = selector.GetNextIndex();
                if (idx == lastIdx) sameCount++;
                lastIdx = idx;
            }
            // With anti-repeat logic, should never repeat
            Assert.AreEqual(0, sameCount);
        }
    }
}
