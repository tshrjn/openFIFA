using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Editor-time validator for audio assets.
    /// Validates sample rate, bit depth, duration limits, file size, and naming conventions.
    /// </summary>
    public static class AudioAssetValidator
    {
        private static readonly AudioAssetConfig DefaultConfig = new AudioAssetConfig();
        private static readonly string AudioBasePath = "Assets/Audio/";

        /// <summary>
        /// Validates a single AudioClip asset against the import settings.
        /// Returns a list of validation error/warning messages.
        /// </summary>
        public static List<string> ValidateAudioClip(AudioClip clip, string assetPath)
        {
            var issues = new List<string>();

            if (clip == null)
            {
                issues.Add("AudioClip is null");
                return issues;
            }

            var importSettings = DefaultConfig.ImportSettings;
            var category = AudioAssetImporter.DetermineCategoryFromPath(assetPath);

            // Sample rate validation
            if (!importSettings.IsSampleRateValid(clip.frequency))
            {
                issues.Add(
                    $"Sample rate {clip.frequency}Hz outside valid range " +
                    $"({importSettings.MinSampleRate}-{importSettings.MaxSampleRate}Hz)");
            }

            // Duration validation
            if (!importSettings.IsDurationValid(clip.length, category))
            {
                float maxDuration = (category == AudioCategory.Ambient || category == AudioCategory.Music || category == AudioCategory.Crowd)
                    ? importSettings.MaxLoopDuration
                    : importSettings.MaxSFXDuration;
                issues.Add(
                    $"Duration {clip.length:F2}s exceeds max {maxDuration:F0}s for category {category}");
            }

            // File size validation
            if (!string.IsNullOrEmpty(assetPath))
            {
                string fullPath = Path.GetFullPath(assetPath);
                if (File.Exists(fullPath))
                {
                    long fileSize = new FileInfo(fullPath).Length;
                    if (!importSettings.IsFileSizeValid(fileSize))
                    {
                        issues.Add(
                            $"File size {fileSize / (1024 * 1024f):F1}MB exceeds max " +
                            $"{importSettings.MaxFileSizeBytes / (1024 * 1024f):F0}MB");
                    }
                }
            }

            // Naming convention validation
            string clipName = Path.GetFileNameWithoutExtension(assetPath);
            if (!AudioImportSettings.IsNamingConventionValid(clipName))
            {
                issues.Add(
                    $"Clip name '{clipName}' does not follow naming convention " +
                    "(PascalCase or Snake_Case, no spaces, starts with letter)");
            }

            // Extension validation
            string extension = Path.GetExtension(assetPath);
            if (!AudioImportSettings.IsValidExtension(extension))
            {
                issues.Add($"File extension '{extension}' is not a supported audio format");
            }

            return issues;
        }

        /// <summary>
        /// Validates all AudioClip assets under Assets/Audio/.
        /// </summary>
        public static Dictionary<string, List<string>> ValidateAllAudioAssets()
        {
            var results = new Dictionary<string, List<string>>();
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioBasePath });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var issues = ValidateAudioClip(clip, path);

                if (issues.Count > 0)
                {
                    results[path] = issues;
                }
            }

            return results;
        }

        /// <summary>
        /// Validates that all required clips from the AudioAssetConfig exist in the project.
        /// Returns a list of missing clip names.
        /// </summary>
        public static List<string> FindMissingRequiredClips()
        {
            var missing = new List<string>();
            var config = new AudioAssetConfig();

            // Check whistle
            if (!FindClipByName(config.WhistleClipName))
                missing.Add(config.WhistleClipName);

            // Check kick variations
            foreach (var kickName in config.KickClipNames)
            {
                if (!FindClipByName(kickName))
                    missing.Add(kickName);
            }

            // Check crowd ambient
            if (!FindClipByName(config.CrowdAmbientClipName))
                missing.Add(config.CrowdAmbientClipName);

            // Check goal cheer
            if (!FindClipByName(config.GoalCheerClipName))
                missing.Add(config.GoalCheerClipName);

            // Check crowd reactions
            foreach (var reactionName in config.CrowdReactionNames)
            {
                if (!FindClipByName(reactionName))
                    missing.Add(reactionName);
            }

            return missing;
        }

        /// <summary>
        /// Searches the project for an AudioClip matching the given name.
        /// </summary>
        private static bool FindClipByName(string clipName)
        {
            string[] guids = AssetDatabase.FindAssets($"{clipName} t:AudioClip");
            return guids.Length > 0;
        }

        /// <summary>
        /// Validates an AudioBankConfig (pure data validation, no asset loading).
        /// </summary>
        public static List<string> ValidateAudioBankConfig(AudioBankConfig bank)
        {
            if (bank == null)
                return new List<string> { "AudioBankConfig is null" };

            return bank.ValidateAudioBank();
        }

        /// <summary>
        /// Validates the AudioMixerConfig (pure data validation).
        /// </summary>
        public static List<string> ValidateMixerConfig(AudioMixerConfig mixerConfig)
        {
            var issues = new List<string>();
            if (mixerConfig == null)
            {
                issues.Add("AudioMixerConfig is null");
                return issues;
            }

            if (!mixerConfig.IsValid())
            {
                issues.Add("AudioMixerConfig has invalid volume values or crossfade duration");
            }

            // Check all categories have group names
            var allCategories = new[] {
                AudioCategory.SFX, AudioCategory.Music,
                AudioCategory.Crowd, AudioCategory.Ambient, AudioCategory.UI
            };

            foreach (var category in allCategories)
            {
                string groupName = mixerConfig.GetGroupName(category);
                if (string.IsNullOrEmpty(groupName))
                {
                    issues.Add($"Missing mixer group name for category {category}");
                }
            }

            return issues;
        }

        /// <summary>
        /// Menu item to validate all audio assets in the project.
        /// </summary>
        [MenuItem("OpenFifa/Audio/Validate All Audio Assets")]
        public static void ValidateAllAudioAssetsMenu()
        {
            var results = ValidateAllAudioAssets();

            if (results.Count == 0)
            {
                Debug.Log("[AudioAssetValidator] All audio assets passed validation.");
            }
            else
            {
                foreach (var kvp in results)
                {
                    foreach (var issue in kvp.Value)
                    {
                        Debug.LogWarning($"[AudioAssetValidator] {kvp.Key}: {issue}");
                    }
                }

                Debug.Log($"[AudioAssetValidator] {results.Count} audio files have validation issues.");
            }

            // Also check for missing required clips
            var missing = FindMissingRequiredClips();
            if (missing.Count > 0)
            {
                Debug.LogWarning(
                    $"[AudioAssetValidator] Missing required clips: {string.Join(", ", missing)}");
            }
        }

        /// <summary>
        /// Menu item to validate the audio bank configurations.
        /// </summary>
        [MenuItem("OpenFifa/Audio/Validate Audio Banks")]
        public static void ValidateAudioBanksMenu()
        {
            var config = new AudioAssetConfig();

            ValidateAndLogBank(config.BuildMatchBank());
            ValidateAndLogBank(config.BuildMenuBank());
            ValidateAndLogBank(config.BuildCelebrationBank());
        }

        private static void ValidateAndLogBank(AudioBankConfig bank)
        {
            var errors = bank.ValidateAudioBank();
            if (errors.Count == 0)
            {
                Debug.Log($"[AudioAssetValidator] Bank '{bank.BankName}' validation PASSED ({bank.Entries.Count} entries)");
            }
            else
            {
                foreach (var error in errors)
                {
                    Debug.LogError($"[AudioAssetValidator] Bank '{bank.BankName}': {error}");
                }
            }
        }
    }
}
