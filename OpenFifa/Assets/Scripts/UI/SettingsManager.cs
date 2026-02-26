using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using OpenFifa.Core;

namespace OpenFifa.UI
{
    /// <summary>
    /// Settings screen controller managing volume and difficulty.
    /// Persists settings via PlayerPrefs.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        [SerializeField] private Slider _sfxSlider;
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private AudioMixer _audioMixer;
        [SerializeField] private string _sfxMixerParam = "SFXVolume";
        [SerializeField] private string _musicMixerParam = "MusicVolume";

        private SettingsLogic _logic;

        /// <summary>The underlying settings logic.</summary>
        public SettingsLogic Logic => _logic;

        private void Awake()
        {
            _logic = new SettingsLogic();
            LoadSettings();
        }

        private void Start()
        {
            // Set slider values
            if (_sfxSlider != null)
            {
                _sfxSlider.minValue = 0f;
                _sfxSlider.maxValue = 100f;
                _sfxSlider.value = _logic.SFXVolume;
                _sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            }

            if (_musicSlider != null)
            {
                _musicSlider.minValue = 0f;
                _musicSlider.maxValue = 100f;
                _musicSlider.value = _logic.MusicVolume;
                _musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            }

            ApplyAudioSettings();
        }

        private void OnSFXVolumeChanged(float value)
        {
            _logic.SetSFXVolume(value);
            ApplyAudioSettings();
            SaveSettings();
        }

        private void OnMusicVolumeChanged(float value)
        {
            _logic.SetMusicVolume(value);
            ApplyAudioSettings();
            SaveSettings();
        }

        /// <summary>Set difficulty from dropdown index.</summary>
        public void OnDifficultyChanged(int index)
        {
            _logic.SetDifficulty((GameDifficulty)index);
            SaveSettings();
        }

        private void ApplyAudioSettings()
        {
            if (_audioMixer == null) return;

            _audioMixer.SetFloat(_sfxMixerParam, SettingsLogic.VolumeToDb(_logic.SFXVolume));
            _audioMixer.SetFloat(_musicMixerParam, SettingsLogic.VolumeToDb(_logic.MusicVolume));
        }

        private void LoadSettings()
        {
            float sfx = PlayerPrefs.GetFloat(SettingsLogic.SFXVolumeKey, 75f);
            float music = PlayerPrefs.GetFloat(SettingsLogic.MusicVolumeKey, 75f);
            int difficulty = PlayerPrefs.GetInt(SettingsLogic.DifficultyKey, 1);
            _logic.Load(sfx, music, difficulty);
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetFloat(SettingsLogic.SFXVolumeKey, _logic.SFXVolume);
            PlayerPrefs.SetFloat(SettingsLogic.MusicVolumeKey, _logic.MusicVolume);
            PlayerPrefs.SetInt(SettingsLogic.DifficultyKey, (int)_logic.Difficulty);
            PlayerPrefs.Save();
        }

        private void OnDestroy()
        {
            if (_sfxSlider != null)
                _sfxSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
            if (_musicSlider != null)
                _musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        }
    }
}
