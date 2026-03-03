using UnityEditor;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Editor
{
    /// <summary>
    /// AssetPostprocessor for audio files (.wav, .ogg) imported into Assets/Audio/.
    /// Auto-configures import settings: compression, sample rate, load type based on
    /// the subfolder category (SFX, Music, Crowd, Ambient, UI).
    /// </summary>
    public class AudioAssetImporter : AssetPostprocessor
    {
        private static readonly string AudioBasePath = "Assets/Audio/";
        private static readonly AudioAssetConfig DefaultConfig = new AudioAssetConfig();

        /// <summary>
        /// Pre-processes audio clip import settings based on category subfolder.
        /// </summary>
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioBasePath))
                return;

            var importer = assetImporter as AudioImporter;
            if (importer == null)
                return;

            var category = DetermineCategoryFromPath(assetPath);
            ConfigureImportSettings(importer, category);

            Debug.Log($"[AudioAssetImporter] Configured '{assetPath}' as {category}");
        }

        /// <summary>
        /// Post-processes audio clips: logs duration and validates settings.
        /// </summary>
        private void OnPostprocessAudio(AudioClip clip)
        {
            if (!assetPath.StartsWith(AudioBasePath))
                return;

            float duration = clip.length;
            int sampleRate = clip.frequency;
            int channels = clip.channels;

            var category = DetermineCategoryFromPath(assetPath);
            var importSettings = DefaultConfig.ImportSettings;

            // Validate sample rate
            if (!importSettings.IsSampleRateValid(sampleRate))
            {
                Debug.LogWarning(
                    $"[AudioAssetImporter] '{assetPath}' sample rate {sampleRate}Hz is outside " +
                    $"valid range ({importSettings.MinSampleRate}-{importSettings.MaxSampleRate}Hz)");
            }

            // Validate duration
            if (!importSettings.IsDurationValid(duration, category))
            {
                float maxDuration = (category == AudioCategory.Ambient || category == AudioCategory.Music || category == AudioCategory.Crowd)
                    ? importSettings.MaxLoopDuration
                    : importSettings.MaxSFXDuration;
                Debug.LogWarning(
                    $"[AudioAssetImporter] '{assetPath}' duration {duration:F1}s exceeds max {maxDuration:F0}s for {category}");
            }

            Debug.Log(
                $"[AudioAssetImporter] Audio imported: '{clip.name}' " +
                $"({duration:F2}s, {sampleRate}Hz, {channels}ch, category: {category})");
        }

        /// <summary>
        /// Determines the AudioCategory from the asset path subfolder.
        /// </summary>
        public static AudioCategory DetermineCategoryFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return AudioCategory.SFX;

            string lowerPath = path.ToLowerInvariant();

            if (lowerPath.Contains("/sfx/"))
                return AudioCategory.SFX;
            if (lowerPath.Contains("/music/"))
                return AudioCategory.Music;
            if (lowerPath.Contains("/crowd/"))
                return AudioCategory.Crowd;
            if (lowerPath.Contains("/ambient/"))
                return AudioCategory.Ambient;
            if (lowerPath.Contains("/ui/"))
                return AudioCategory.UI;

            return AudioCategory.SFX; // Default to SFX
        }

        /// <summary>
        /// Configures AudioImporter settings based on the audio category.
        /// </summary>
        private void ConfigureImportSettings(AudioImporter importer, AudioCategory category)
        {
            // Force to mono for SFX and UI (saves memory, spatial audio handles stereo positioning)
            bool forceToMono = (category == AudioCategory.SFX || category == AudioCategory.UI);
            importer.forceToMono = forceToMono && DefaultConfig.ForceToMonoSFX;

            // Platform-specific settings for default platform
            var sampleSettings = importer.defaultSampleSettings;

            // Compression format
            sampleSettings.compressionFormat = AudioCompressionFormat.Vorbis;
            sampleSettings.quality = DefaultConfig.CompressionQuality / 100f;

            // Load type based on category
            switch (category)
            {
                case AudioCategory.SFX:
                case AudioCategory.UI:
                    sampleSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                    break;

                case AudioCategory.Ambient:
                case AudioCategory.Music:
                    sampleSettings.loadType = AudioClipLoadType.Streaming;
                    break;

                case AudioCategory.Crowd:
                    // Crowd reactions are short-ish, so CompressedInMemory is a good balance
                    sampleSettings.loadType = AudioClipLoadType.CompressedInMemory;
                    break;
            }

            importer.defaultSampleSettings = sampleSettings;
        }

        /// <summary>
        /// Menu item to create the recommended Audio subfolder structure.
        /// </summary>
        [MenuItem("OpenFifa/Audio/Create Audio Folder Structure")]
        public static void CreateAudioFolderStructure()
        {
            string[] subfolders = { "SFX", "Music", "Crowd", "Ambient", "UI" };

            EnsureDirectoryExists("Assets/Audio");

            foreach (var subfolder in subfolders)
            {
                string path = "Assets/Audio/" + subfolder;
                EnsureDirectoryExists(path);
            }

            AssetDatabase.Refresh();
            Debug.Log("[AudioAssetImporter] Audio folder structure created at Assets/Audio/");
        }

        /// <summary>
        /// Menu item to re-import all audio files with current settings.
        /// </summary>
        [MenuItem("OpenFifa/Audio/Re-import All Audio")]
        public static void ReimportAllAudio()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" });
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
            }

            Debug.Log($"[AudioAssetImporter] Re-imported {count} audio clips.");
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                var current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }
    }
}
