using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Game difficulty levels.
    /// </summary>
    public enum GameDifficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2
    }

    /// <summary>
    /// Pure C# settings logic.
    /// Manages volume and difficulty values with clamping and dB conversion.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class SettingsLogic
    {
        public const string SFXVolumeKey = "SFXVolume";
        public const string MusicVolumeKey = "MusicVolume";
        public const string DifficultyKey = "Difficulty";

        private float _sfxVolume;
        private float _musicVolume;
        private GameDifficulty _difficulty;

        /// <summary>SFX volume (0-100).</summary>
        public float SFXVolume => _sfxVolume;

        /// <summary>Music volume (0-100).</summary>
        public float MusicVolume => _musicVolume;

        /// <summary>Current difficulty setting.</summary>
        public GameDifficulty Difficulty => _difficulty;

        public SettingsLogic()
        {
            _sfxVolume = 75f;
            _musicVolume = 75f;
            _difficulty = GameDifficulty.Medium;
        }

        /// <summary>Set SFX volume, clamped to 0-100.</summary>
        public void SetSFXVolume(float volume)
        {
            _sfxVolume = Clamp(volume, 0f, 100f);
        }

        /// <summary>Set Music volume, clamped to 0-100.</summary>
        public void SetMusicVolume(float volume)
        {
            _musicVolume = Clamp(volume, 0f, 100f);
        }

        /// <summary>Set difficulty.</summary>
        public void SetDifficulty(GameDifficulty difficulty)
        {
            _difficulty = difficulty;
        }

        /// <summary>
        /// Load settings from stored values.
        /// </summary>
        public void Load(float sfxVolume, float musicVolume, int difficulty)
        {
            _sfxVolume = Clamp(sfxVolume, 0f, 100f);
            _musicVolume = Clamp(musicVolume, 0f, 100f);
            _difficulty = (GameDifficulty)Clamp(difficulty, 0, 2);
        }

        /// <summary>
        /// Convert 0-100 volume to dB for AudioMixer.
        /// 0 = -80dB (silent), 100 = 0dB (full volume).
        /// </summary>
        public static float VolumeToDb(float volume)
        {
            if (volume <= 0f) return -80f;
            // Log10(volume/100) * 20
            return (float)(Math.Log10(volume / 100.0) * 20.0);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
