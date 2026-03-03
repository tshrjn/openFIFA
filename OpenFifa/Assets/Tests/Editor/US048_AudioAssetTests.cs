using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US048")]
    [Category("Audio")]
    public class US048_AudioAssetTests
    {
        // ============================================================
        // Existing tests (preserved)
        // ============================================================

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

        // ============================================================
        // New AudioClipEntry tests
        // ============================================================

        [Test]
        public void AudioClipEntry_DefaultVolume_IsOne()
        {
            var entry = new AudioClipEntry();
            Assert.AreEqual(1f, entry.Volume,
                "Default volume should be 1.0");
        }

        [Test]
        public void AudioClipEntry_IsVolumeValid_InRange_ReturnsTrue()
        {
            var entry = new AudioClipEntry { Volume = 0.5f };
            Assert.IsTrue(entry.IsVolumeValid(),
                "Volume 0.5 should be valid");
        }

        [Test]
        public void AudioClipEntry_IsVolumeValid_Negative_ReturnsFalse()
        {
            var entry = new AudioClipEntry { Volume = -0.1f };
            Assert.IsFalse(entry.IsVolumeValid(),
                "Negative volume should be invalid");
        }

        [Test]
        public void AudioClipEntry_IsVolumeValid_AboveOne_ReturnsFalse()
        {
            var entry = new AudioClipEntry { Volume = 1.5f };
            Assert.IsFalse(entry.IsVolumeValid(),
                "Volume above 1.0 should be invalid");
        }

        [Test]
        public void AudioClipEntry_IsPitchRangeValid_Default_ReturnsTrue()
        {
            var entry = new AudioClipEntry();
            Assert.IsTrue(entry.IsPitchRangeValid(),
                "Default pitch range (0.9 - 1.1) should be valid");
        }

        [Test]
        public void AudioClipEntry_IsPitchRangeValid_MinGreaterThanMax_ReturnsFalse()
        {
            var entry = new AudioClipEntry { PitchMin = 1.5f, PitchMax = 0.5f };
            Assert.IsFalse(entry.IsPitchRangeValid(),
                "Pitch min > max should be invalid");
        }

        [Test]
        public void AudioClipEntry_IsPitchRangeValid_ZeroPitch_ReturnsFalse()
        {
            var entry = new AudioClipEntry { PitchMin = 0f, PitchMax = 1f };
            Assert.IsFalse(entry.IsPitchRangeValid(),
                "Zero pitch min should be invalid");
        }

        [Test]
        public void AudioClipEntry_IsValid_CompleteEntry_ReturnsTrue()
        {
            var entry = new AudioClipEntry("TestClip", "Audio/SFX/TestClip.wav", AudioCategory.SFX);
            Assert.IsTrue(entry.IsValid(),
                "Fully constructed entry should be valid");
        }

        [Test]
        public void AudioClipEntry_IsValid_MissingName_ReturnsFalse()
        {
            var entry = new AudioClipEntry { Path = "Audio/SFX/Test.wav" };
            Assert.IsFalse(entry.IsValid(),
                "Entry with null name should be invalid");
        }

        [Test]
        public void AudioClipEntry_IsValid_MissingPath_ReturnsFalse()
        {
            var entry = new AudioClipEntry { Name = "TestClip" };
            Assert.IsFalse(entry.IsValid(),
                "Entry with null path should be invalid");
        }

        // ============================================================
        // New AudioCategory tests
        // ============================================================

        [Test]
        public void AudioCategory_HasAllExpectedValues()
        {
            Assert.AreEqual(0, (int)AudioCategory.SFX);
            Assert.AreEqual(1, (int)AudioCategory.Music);
            Assert.AreEqual(2, (int)AudioCategory.Crowd);
            Assert.AreEqual(3, (int)AudioCategory.Ambient);
            Assert.AreEqual(4, (int)AudioCategory.UI);
        }

        // ============================================================
        // New AudioMixerConfig tests
        // ============================================================

        [Test]
        public void AudioMixerConfig_DefaultConfig_IsValid()
        {
            var mixer = new AudioMixerConfig();
            Assert.IsTrue(mixer.IsValid(),
                "Default mixer config should be valid");
        }

        [Test]
        public void AudioMixerConfig_AllCategoriesHaveGroupNames()
        {
            var mixer = new AudioMixerConfig();
            Assert.IsNotEmpty(mixer.GetGroupName(AudioCategory.SFX));
            Assert.IsNotEmpty(mixer.GetGroupName(AudioCategory.Music));
            Assert.IsNotEmpty(mixer.GetGroupName(AudioCategory.Crowd));
            Assert.IsNotEmpty(mixer.GetGroupName(AudioCategory.Ambient));
            Assert.IsNotEmpty(mixer.GetGroupName(AudioCategory.UI));
        }

        [Test]
        public void AudioMixerConfig_DefaultVolumes_InValidRange()
        {
            var mixer = new AudioMixerConfig();
            foreach (var kvp in mixer.DefaultVolumes)
            {
                Assert.That(kvp.Value, Is.InRange(0f, 1f),
                    $"Default volume for {kvp.Key} should be in range 0..1");
            }
        }

        [Test]
        public void AudioMixerConfig_CrossfadeDuration_IsPositive()
        {
            var mixer = new AudioMixerConfig();
            Assert.Greater(mixer.CrossfadeDuration, 0f,
                "Crossfade duration should be positive");
        }

        [Test]
        public void AudioMixerConfig_InvalidVolume_ReturnsInvalid()
        {
            var mixer = new AudioMixerConfig { MasterVolume = -0.5f };
            Assert.IsFalse(mixer.IsValid(),
                "Mixer config with negative master volume should be invalid");
        }

        [Test]
        public void AudioMixerConfig_GetDefaultVolume_UnknownCategory_ReturnsOne()
        {
            var mixer = new AudioMixerConfig();
            // Cast an invalid int to AudioCategory to test the fallback
            float vol = mixer.GetDefaultVolume((AudioCategory)99);
            Assert.AreEqual(1.0f, vol,
                "Unknown category should return default volume of 1.0");
        }

        // ============================================================
        // New AudioBankConfig tests
        // ============================================================

        [Test]
        public void AudioBankConfig_MatchBank_HasCorrectName()
        {
            var config = new AudioAssetConfig();
            var bank = config.BuildMatchBank();
            Assert.AreEqual("Match", bank.BankName);
        }

        [Test]
        public void AudioBankConfig_MatchBank_ContainsWhistleAndKicks()
        {
            var config = new AudioAssetConfig();
            var bank = config.BuildMatchBank();

            // Should contain whistle + 3 kick variations + ambient + goal cheer + 10 crowd reactions
            Assert.GreaterOrEqual(bank.Entries.Count, 15,
                $"Match bank should have at least 15 entries, got {bank.Entries.Count}");

            var sfxEntries = bank.GetEntriesByCategory(AudioCategory.SFX);
            Assert.GreaterOrEqual(sfxEntries.Count, 4,
                "Match bank should have at least 4 SFX entries (whistle + 3 kicks)");
        }

        [Test]
        public void AudioBankConfig_MatchBank_Validates()
        {
            var config = new AudioAssetConfig();
            var bank = config.BuildMatchBank();
            var errors = bank.ValidateAudioBank();
            Assert.AreEqual(0, errors.Count,
                $"Match bank should have no validation errors. Got: {string.Join("; ", errors)}");
        }

        [Test]
        public void AudioBankConfig_MenuBank_Validates()
        {
            var config = new AudioAssetConfig();
            var bank = config.BuildMenuBank();
            var errors = bank.ValidateAudioBank();
            Assert.AreEqual(0, errors.Count,
                $"Menu bank should have no validation errors. Got: {string.Join("; ", errors)}");
        }

        [Test]
        public void AudioBankConfig_CelebrationBank_Validates()
        {
            var config = new AudioAssetConfig();
            var bank = config.BuildCelebrationBank();
            var errors = bank.ValidateAudioBank();
            Assert.AreEqual(0, errors.Count,
                $"Celebration bank should have no validation errors. Got: {string.Join("; ", errors)}");
        }

        [Test]
        public void AudioBankConfig_EmptyBank_FailsValidation()
        {
            var bank = new AudioBankConfig("EmptyBank");
            var errors = bank.ValidateAudioBank();
            Assert.Greater(errors.Count, 0,
                "Empty bank should fail validation");
        }

        [Test]
        public void AudioBankConfig_DuplicateNames_FailsValidation()
        {
            var bank = new AudioBankConfig("DuplicateTest");
            bank.AddEntry(new AudioClipEntry("Clip_01", "Audio/SFX/Clip_01.wav", AudioCategory.SFX));
            bank.AddEntry(new AudioClipEntry("Clip_01", "Audio/SFX/Clip_01_v2.wav", AudioCategory.SFX));
            var errors = bank.ValidateAudioBank();
            Assert.Greater(errors.Count, 0,
                "Bank with duplicate entry names should fail validation");
        }

        [Test]
        public void AudioBankConfig_GetEntriesByCategory_FiltersCorrectly()
        {
            var bank = new AudioBankConfig("FilterTest");
            bank.AddEntry(new AudioClipEntry("SFX_01", "Audio/SFX/SFX_01.wav", AudioCategory.SFX));
            bank.AddEntry(new AudioClipEntry("Music_01", "Audio/Music/Music_01.ogg", AudioCategory.Music));
            bank.AddEntry(new AudioClipEntry("SFX_02", "Audio/SFX/SFX_02.wav", AudioCategory.SFX));

            var sfx = bank.GetEntriesByCategory(AudioCategory.SFX);
            Assert.AreEqual(2, sfx.Count, "Should find exactly 2 SFX entries");

            var music = bank.GetEntriesByCategory(AudioCategory.Music);
            Assert.AreEqual(1, music.Count, "Should find exactly 1 Music entry");

            var crowd = bank.GetEntriesByCategory(AudioCategory.Crowd);
            Assert.AreEqual(0, crowd.Count, "Should find 0 Crowd entries");
        }

        // ============================================================
        // New AudioImportSettings tests
        // ============================================================

        [Test]
        public void AudioImportSettings_SampleRate_DefaultRange()
        {
            var settings = new AudioImportSettings();
            Assert.IsTrue(settings.IsSampleRateValid(44100),
                "44100Hz should be valid");
            Assert.IsTrue(settings.IsSampleRateValid(48000),
                "48000Hz should be valid");
            Assert.IsFalse(settings.IsSampleRateValid(8000),
                "8000Hz should be too low");
            Assert.IsFalse(settings.IsSampleRateValid(96000),
                "96000Hz should be too high");
        }

        [Test]
        public void AudioImportSettings_BitDepth_DefaultRange()
        {
            var settings = new AudioImportSettings();
            Assert.IsTrue(settings.IsBitDepthValid(16), "16-bit should be valid");
            Assert.IsTrue(settings.IsBitDepthValid(24), "24-bit should be valid");
            Assert.IsFalse(settings.IsBitDepthValid(8), "8-bit should be invalid");
            Assert.IsFalse(settings.IsBitDepthValid(32), "32-bit should be too high");
        }

        [Test]
        public void AudioImportSettings_Duration_SFX_MaxLimit()
        {
            var settings = new AudioImportSettings();
            Assert.IsTrue(settings.IsDurationValid(2f, AudioCategory.SFX),
                "2s SFX should be valid");
            Assert.IsFalse(settings.IsDurationValid(10f, AudioCategory.SFX),
                "10s SFX should exceed max 5s limit");
        }

        [Test]
        public void AudioImportSettings_Duration_Ambient_AllowsLong()
        {
            var settings = new AudioImportSettings();
            Assert.IsTrue(settings.IsDurationValid(120f, AudioCategory.Ambient),
                "120s ambient loop should be valid");
            Assert.IsFalse(settings.IsDurationValid(600f, AudioCategory.Ambient),
                "600s ambient should exceed max 300s limit");
        }

        [Test]
        public void AudioImportSettings_Duration_ZeroOrNegative_ReturnsFalse()
        {
            var settings = new AudioImportSettings();
            Assert.IsFalse(settings.IsDurationValid(0f, AudioCategory.SFX),
                "Zero duration should be invalid");
            Assert.IsFalse(settings.IsDurationValid(-1f, AudioCategory.Music),
                "Negative duration should be invalid");
        }

        [Test]
        public void AudioImportSettings_FileSize_WithinLimit_ReturnsTrue()
        {
            var settings = new AudioImportSettings();
            Assert.IsTrue(settings.IsFileSizeValid(1024 * 1024),
                "1MB file should be within limit");
        }

        [Test]
        public void AudioImportSettings_FileSize_OverLimit_ReturnsFalse()
        {
            var settings = new AudioImportSettings();
            Assert.IsFalse(settings.IsFileSizeValid(20 * 1024 * 1024),
                "20MB file should exceed 10MB limit");
        }

        [Test]
        public void AudioImportSettings_ValidExtension_Wav_ReturnsTrue()
        {
            Assert.IsTrue(AudioImportSettings.IsValidExtension(".wav"));
            Assert.IsTrue(AudioImportSettings.IsValidExtension(".ogg"));
            Assert.IsTrue(AudioImportSettings.IsValidExtension(".mp3"));
            Assert.IsTrue(AudioImportSettings.IsValidExtension(".aiff"));
        }

        [Test]
        public void AudioImportSettings_InvalidExtension_ReturnsFalse()
        {
            Assert.IsFalse(AudioImportSettings.IsValidExtension(".txt"));
            Assert.IsFalse(AudioImportSettings.IsValidExtension(".exe"));
            Assert.IsFalse(AudioImportSettings.IsValidExtension(""));
            Assert.IsFalse(AudioImportSettings.IsValidExtension(null));
        }

        [Test]
        public void AudioImportSettings_NamingConvention_ValidNames_ReturnsTrue()
        {
            Assert.IsTrue(AudioImportSettings.IsNamingConventionValid("Kick_Impact_01"),
                "Snake_Case with digits should be valid");
            Assert.IsTrue(AudioImportSettings.IsNamingConventionValid("StadiumAmbience"),
                "PascalCase should be valid");
            Assert.IsTrue(AudioImportSettings.IsNamingConventionValid("UI-Click"),
                "Hyphenated name should be valid");
        }

        [Test]
        public void AudioImportSettings_NamingConvention_InvalidNames_ReturnsFalse()
        {
            Assert.IsFalse(AudioImportSettings.IsNamingConventionValid("kick impact"),
                "Name with spaces should be invalid");
            Assert.IsFalse(AudioImportSettings.IsNamingConventionValid("123clip"),
                "Name starting with digit should be invalid");
            Assert.IsFalse(AudioImportSettings.IsNamingConventionValid(""),
                "Empty name should be invalid");
            Assert.IsFalse(AudioImportSettings.IsNamingConventionValid(null),
                "Null name should be invalid");
        }

        // ============================================================
        // New CrowdReaction tests
        // ============================================================

        [Test]
        public void AudioAssetConfig_CrowdReactions_AtLeast10()
        {
            var config = new AudioAssetConfig();
            Assert.GreaterOrEqual(config.CrowdReactionNames.Count, 10,
                "Should have at least 10 crowd reaction variations");
        }

        [Test]
        public void CrowdReactionSelector_ReturnsValidIndices()
        {
            var config = new AudioAssetConfig();
            var selector = new CrowdReactionSelector(config.CrowdReactionNames.Count);

            for (int i = 0; i < 50; i++)
            {
                int idx = selector.GetNextIndex();
                Assert.GreaterOrEqual(idx, 0);
                Assert.Less(idx, config.CrowdReactionNames.Count);
            }
        }

        [Test]
        public void CrowdReactionSelector_NoConsecutiveRepeats()
        {
            var selector = new CrowdReactionSelector(10);
            int sameCount = 0;
            int lastIdx = -1;
            for (int i = 0; i < 50; i++)
            {
                int idx = selector.GetNextIndex();
                if (idx == lastIdx) sameCount++;
                lastIdx = idx;
            }
            Assert.AreEqual(0, sameCount,
                "Crowd reaction selector should never repeat consecutively");
        }

        [Test]
        public void KickVariationSelector_SingleVariation_AlwaysReturnsZero()
        {
            var selector = new KickVariationSelector(1);
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(0, selector.GetNextIndex(),
                    "Single-variation selector should always return 0");
            }
        }

        // ============================================================
        // New MixerConfig integration tests
        // ============================================================

        [Test]
        public void AudioAssetConfig_MixerConfig_IsAccessible()
        {
            var config = new AudioAssetConfig();
            Assert.IsNotNull(config.MixerConfig,
                "MixerConfig should be initialized by default");
            Assert.IsTrue(config.MixerConfig.IsValid(),
                "Default MixerConfig should be valid");
        }

        [Test]
        public void AudioAssetConfig_ImportSettings_IsAccessible()
        {
            var config = new AudioAssetConfig();
            Assert.IsNotNull(config.ImportSettings,
                "ImportSettings should be initialized by default");
        }
    }
}
