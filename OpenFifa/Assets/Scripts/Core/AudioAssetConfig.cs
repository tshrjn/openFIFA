using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Categories for audio assets to organize mixing and import settings.
    /// </summary>
    public enum AudioCategory
    {
        SFX = 0,
        Music = 1,
        Crowd = 2,
        Ambient = 3,
        UI = 4
    }

    /// <summary>
    /// Defines a single audio clip entry with playback and import configuration.
    /// </summary>
    public class AudioClipEntry
    {
        /// <summary>Human-readable name for the clip (e.g., "Kick_Impact_01").</summary>
        public string Name;

        /// <summary>Asset path relative to Assets folder (e.g., "Audio/SFX/Kick_Impact_01.wav").</summary>
        public string Path;

        /// <summary>Default playback volume (0-1).</summary>
        public float Volume = 1f;

        /// <summary>Minimum pitch multiplier for random variation.</summary>
        public float PitchMin = 0.9f;

        /// <summary>Maximum pitch multiplier for random variation.</summary>
        public float PitchMax = 1.1f;

        /// <summary>Whether the clip should loop.</summary>
        public bool Loop;

        /// <summary>Audio category for mixer routing.</summary>
        public AudioCategory Category = AudioCategory.SFX;

        public AudioClipEntry() { }

        public AudioClipEntry(string name, string path, AudioCategory category,
            float volume = 1f, float pitchMin = 0.9f, float pitchMax = 1.1f, bool loop = false)
        {
            Name = name;
            Path = path;
            Category = category;
            Volume = volume;
            PitchMin = pitchMin;
            PitchMax = pitchMax;
            Loop = loop;
        }

        /// <summary>
        /// Returns true if volume is in the valid 0..1 range.
        /// </summary>
        public bool IsVolumeValid()
        {
            return Volume >= 0f && Volume <= 1f;
        }

        /// <summary>
        /// Returns true if pitch range is valid (min > 0, min <= max).
        /// </summary>
        public bool IsPitchRangeValid()
        {
            return PitchMin > 0f && PitchMax > 0f && PitchMin <= PitchMax;
        }

        /// <summary>
        /// Returns true if name and path are non-empty.
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Name)
                && !string.IsNullOrEmpty(Path)
                && IsVolumeValid()
                && IsPitchRangeValid();
        }
    }

    /// <summary>
    /// Configuration for the audio mixer: group names, default volumes, crossfade settings.
    /// </summary>
    public class AudioMixerConfig
    {
        /// <summary>Mixer group names keyed by AudioCategory.</summary>
        public readonly Dictionary<AudioCategory, string> GroupNames = new Dictionary<AudioCategory, string>
        {
            { AudioCategory.SFX, "SFX" },
            { AudioCategory.Music, "Music" },
            { AudioCategory.Crowd, "Crowd" },
            { AudioCategory.Ambient, "Ambient" },
            { AudioCategory.UI, "UI" }
        };

        /// <summary>Default volume (0-1) per category.</summary>
        public readonly Dictionary<AudioCategory, float> DefaultVolumes = new Dictionary<AudioCategory, float>
        {
            { AudioCategory.SFX, 1.0f },
            { AudioCategory.Music, 0.7f },
            { AudioCategory.Crowd, 0.8f },
            { AudioCategory.Ambient, 0.5f },
            { AudioCategory.UI, 0.9f }
        };

        /// <summary>Duration in seconds for crossfading between music tracks.</summary>
        public float CrossfadeDuration = 2.0f;

        /// <summary>Master volume multiplier (0-1).</summary>
        public float MasterVolume = 1.0f;

        /// <summary>
        /// Returns the group name for the given category, or empty string if not found.
        /// </summary>
        public string GetGroupName(AudioCategory category)
        {
            string name;
            return GroupNames.TryGetValue(category, out name) ? name : string.Empty;
        }

        /// <summary>
        /// Returns the default volume for the given category, or 1.0 if not found.
        /// </summary>
        public float GetDefaultVolume(AudioCategory category)
        {
            float vol;
            return DefaultVolumes.TryGetValue(category, out vol) ? vol : 1.0f;
        }

        /// <summary>
        /// Returns true if all category volumes are valid (0..1) and crossfade is non-negative.
        /// </summary>
        public bool IsValid()
        {
            foreach (var kvp in DefaultVolumes)
            {
                if (kvp.Value < 0f || kvp.Value > 1f)
                    return false;
            }
            if (MasterVolume < 0f || MasterVolume > 1f)
                return false;
            if (CrossfadeDuration < 0f)
                return false;
            return true;
        }
    }

    /// <summary>
    /// Defines an audio bank — a named collection of audio clip entries for a context
    /// (e.g., Match, Menu, Celebration).
    /// </summary>
    public class AudioBankConfig
    {
        /// <summary>Bank name (e.g., "Match", "Menu", "Celebration").</summary>
        public string BankName;

        /// <summary>All audio clip entries in this bank.</summary>
        public readonly List<AudioClipEntry> Entries = new List<AudioClipEntry>();

        public AudioBankConfig(string bankName)
        {
            BankName = bankName ?? string.Empty;
        }

        /// <summary>
        /// Adds a clip entry to this bank.
        /// </summary>
        public void AddEntry(AudioClipEntry entry)
        {
            if (entry != null)
                Entries.Add(entry);
        }

        /// <summary>
        /// Returns all entries matching the given category.
        /// </summary>
        public List<AudioClipEntry> GetEntriesByCategory(AudioCategory category)
        {
            var result = new List<AudioClipEntry>();
            foreach (var entry in Entries)
            {
                if (entry.Category == category)
                    result.Add(entry);
            }
            return result;
        }

        /// <summary>
        /// Validates the entire bank: non-empty name, all entries valid, no duplicate names.
        /// Returns a list of error messages (empty if valid).
        /// </summary>
        public List<string> ValidateAudioBank()
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(BankName))
            {
                errors.Add("Bank name must not be empty");
            }

            if (Entries.Count == 0)
            {
                errors.Add($"Bank '{BankName}' has no entries");
            }

            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (entry == null)
                {
                    errors.Add($"Entry at index {i} is null");
                    continue;
                }

                if (!entry.IsValid())
                {
                    errors.Add($"Entry '{entry.Name ?? "(null)"}' at index {i} is invalid");
                }

                if (!string.IsNullOrEmpty(entry.Name))
                {
                    if (!seenNames.Add(entry.Name))
                    {
                        errors.Add($"Duplicate entry name '{entry.Name}' in bank '{BankName}'");
                    }
                }
            }

            return errors;
        }
    }

    /// <summary>
    /// Audio import settings for validating imported audio files.
    /// </summary>
    public class AudioImportSettings
    {
        /// <summary>Minimum acceptable sample rate in Hz.</summary>
        public int MinSampleRate = 22050;

        /// <summary>Maximum acceptable sample rate in Hz.</summary>
        public int MaxSampleRate = 48000;

        /// <summary>Minimum bit depth for quality.</summary>
        public int MinBitDepth = 16;

        /// <summary>Maximum bit depth.</summary>
        public int MaxBitDepth = 24;

        /// <summary>Maximum duration in seconds for SFX clips.</summary>
        public float MaxSFXDuration = 5f;

        /// <summary>Maximum duration in seconds for ambient/music loops.</summary>
        public float MaxLoopDuration = 300f;

        /// <summary>Maximum file size in bytes for a single clip (10 MB).</summary>
        public long MaxFileSizeBytes = 10 * 1024 * 1024;

        /// <summary>Valid audio file extensions.</summary>
        public static readonly string[] ValidExtensions = { ".wav", ".ogg", ".mp3", ".aiff" };

        /// <summary>
        /// Returns true if the sample rate is within acceptable range.
        /// </summary>
        public bool IsSampleRateValid(int sampleRate)
        {
            return sampleRate >= MinSampleRate && sampleRate <= MaxSampleRate;
        }

        /// <summary>
        /// Returns true if the bit depth is within acceptable range.
        /// </summary>
        public bool IsBitDepthValid(int bitDepth)
        {
            return bitDepth >= MinBitDepth && bitDepth <= MaxBitDepth;
        }

        /// <summary>
        /// Returns true if the clip duration is within acceptable bounds for its category.
        /// </summary>
        public bool IsDurationValid(float durationSeconds, AudioCategory category)
        {
            if (durationSeconds <= 0f) return false;

            float maxDuration = (category == AudioCategory.Ambient || category == AudioCategory.Music || category == AudioCategory.Crowd)
                ? MaxLoopDuration
                : MaxSFXDuration;

            return durationSeconds <= maxDuration;
        }

        /// <summary>
        /// Returns true if the file size is within limits.
        /// </summary>
        public bool IsFileSizeValid(long fileSizeBytes)
        {
            return fileSizeBytes > 0 && fileSizeBytes <= MaxFileSizeBytes;
        }

        /// <summary>
        /// Returns true if the file extension is a valid audio format.
        /// </summary>
        public static bool IsValidExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            string ext = extension.StartsWith(".") ? extension : "." + extension;
            foreach (var valid in ValidExtensions)
            {
                if (string.Equals(ext, valid, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if the clip name follows naming conventions:
        /// PascalCase or Snake_Case, starts with a letter, no spaces.
        /// </summary>
        public static bool IsNamingConventionValid(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.Contains(" ")) return false;
            if (!char.IsLetter(name[0])) return false;

            // Allow letters, digits, underscores, hyphens
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Configuration for real audio assets: clip names, compression settings,
    /// and import settings for Freesound.org CC0 audio files.
    /// </summary>
    public class AudioAssetConfig
    {
        /// <summary>Whistle sound effect clip name.</summary>
        public string WhistleClipName = "Referee_Whistle";

        /// <summary>Kick impact sound variation clip names (minimum 3).</summary>
        public List<string> KickClipNames = new List<string>
        {
            "Kick_Impact_01",
            "Kick_Impact_02",
            "Kick_Impact_03"
        };

        /// <summary>Crowd ambient loop clip name.</summary>
        public string CrowdAmbientClipName = "Stadium_Ambience_Loop";

        /// <summary>Goal celebration cheer clip name.</summary>
        public string GoalCheerClipName = "Crowd_Goal_Cheer";

        /// <summary>Audio compression format for all clips.</summary>
        public string CompressionFormat = "Vorbis";

        /// <summary>Compression quality percentage.</summary>
        public int CompressionQuality = 70;

        /// <summary>Whether SFX clips are forced to mono.</summary>
        public bool ForceToMonoSFX = true;

        /// <summary>Load type for ambient loops (Streaming for long clips).</summary>
        public string AmbientLoadType = "Streaming";

        /// <summary>Load type for SFX (DecompressOnLoad for low latency).</summary>
        public string SFXLoadType = "DecompressOnLoad";

        /// <summary>Crowd reaction sound variation names (at least 10).</summary>
        public List<string> CrowdReactionNames = new List<string>
        {
            "Crowd_Reaction_Cheer",
            "Crowd_Reaction_Gasp",
            "Crowd_Reaction_Boo",
            "Crowd_Reaction_Ooh",
            "Crowd_Reaction_Clap",
            "Crowd_Reaction_Roar",
            "Crowd_Reaction_Whistle",
            "Crowd_Reaction_Chant",
            "Crowd_Reaction_Drumroll",
            "Crowd_Reaction_Celebration"
        };

        /// <summary>Mixer configuration for all audio categories.</summary>
        public AudioMixerConfig MixerConfig = new AudioMixerConfig();

        /// <summary>Import settings for validating audio files.</summary>
        public AudioImportSettings ImportSettings = new AudioImportSettings();

        /// <summary>
        /// Builds a Match audio bank from the current configuration.
        /// </summary>
        public AudioBankConfig BuildMatchBank()
        {
            var bank = new AudioBankConfig("Match");

            bank.AddEntry(new AudioClipEntry(
                WhistleClipName, $"Audio/SFX/{WhistleClipName}.wav", AudioCategory.SFX));

            foreach (var kickName in KickClipNames)
            {
                bank.AddEntry(new AudioClipEntry(
                    kickName, $"Audio/SFX/{kickName}.wav", AudioCategory.SFX));
            }

            bank.AddEntry(new AudioClipEntry(
                CrowdAmbientClipName, $"Audio/Ambient/{CrowdAmbientClipName}.ogg",
                AudioCategory.Ambient, volume: 0.5f, pitchMin: 1f, pitchMax: 1f, loop: true));

            bank.AddEntry(new AudioClipEntry(
                GoalCheerClipName, $"Audio/Crowd/{GoalCheerClipName}.wav", AudioCategory.Crowd));

            foreach (var reactionName in CrowdReactionNames)
            {
                bank.AddEntry(new AudioClipEntry(
                    reactionName, $"Audio/Crowd/{reactionName}.wav", AudioCategory.Crowd));
            }

            return bank;
        }

        /// <summary>
        /// Builds a Menu audio bank with UI sounds.
        /// </summary>
        public AudioBankConfig BuildMenuBank()
        {
            var bank = new AudioBankConfig("Menu");
            bank.AddEntry(new AudioClipEntry(
                "UI_Select", "Audio/UI/UI_Select.wav", AudioCategory.UI));
            bank.AddEntry(new AudioClipEntry(
                "UI_Confirm", "Audio/UI/UI_Confirm.wav", AudioCategory.UI));
            bank.AddEntry(new AudioClipEntry(
                "UI_Back", "Audio/UI/UI_Back.wav", AudioCategory.UI));
            bank.AddEntry(new AudioClipEntry(
                "Menu_Music", "Audio/Music/Menu_Music.ogg",
                AudioCategory.Music, volume: 0.7f, pitchMin: 1f, pitchMax: 1f, loop: true));
            return bank;
        }

        /// <summary>
        /// Builds a Celebration audio bank with goal celebration sounds.
        /// </summary>
        public AudioBankConfig BuildCelebrationBank()
        {
            var bank = new AudioBankConfig("Celebration");
            bank.AddEntry(new AudioClipEntry(
                GoalCheerClipName, $"Audio/Crowd/{GoalCheerClipName}.wav", AudioCategory.Crowd));
            bank.AddEntry(new AudioClipEntry(
                "Celebration_Music", "Audio/Music/Celebration_Music.ogg",
                AudioCategory.Music, volume: 0.8f, pitchMin: 1f, pitchMax: 1f, loop: false));
            bank.AddEntry(new AudioClipEntry(
                "Goal_Horn", "Audio/SFX/Goal_Horn.wav", AudioCategory.SFX));
            return bank;
        }
    }

    /// <summary>
    /// Selects kick sound variations with anti-repeat logic to avoid
    /// playing the same clip twice in a row.
    /// </summary>
    public class KickVariationSelector
    {
        private readonly int _variationCount;
        private int _lastIndex = -1;
        private uint _seed;

        public KickVariationSelector(int variationCount, uint seed = 12345)
        {
            _variationCount = variationCount > 0 ? variationCount : 1;
            _seed = seed;
        }

        /// <summary>
        /// Returns the next kick variation index, guaranteed to differ
        /// from the previous selection (if count > 1).
        /// </summary>
        public int GetNextIndex()
        {
            if (_variationCount <= 1) return 0;

            int next;
            do
            {
                // LCG pseudo-random
                _seed = _seed * 1664525u + 1013904223u;
                next = (int)((_seed >> 16) % (uint)_variationCount);
            }
            while (next == _lastIndex);

            _lastIndex = next;
            return next;
        }
    }

    /// <summary>
    /// Selects crowd reaction sounds with weighted randomness based on game events.
    /// </summary>
    public class CrowdReactionSelector
    {
        private readonly int _reactionCount;
        private int _lastIndex = -1;
        private uint _seed;

        public CrowdReactionSelector(int reactionCount, uint seed = 54321)
        {
            _reactionCount = reactionCount > 0 ? reactionCount : 1;
            _seed = seed;
        }

        /// <summary>
        /// Returns the next crowd reaction index, guaranteed to differ
        /// from the previous selection (if count > 1).
        /// </summary>
        public int GetNextIndex()
        {
            if (_reactionCount <= 1) return 0;

            int next;
            do
            {
                _seed = _seed * 1664525u + 1013904223u;
                next = (int)((_seed >> 16) % (uint)_reactionCount);
            }
            while (next == _lastIndex);

            _lastIndex = next;
            return next;
        }
    }
}
