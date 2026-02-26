using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using OpenFifa.Core;

namespace OpenFifa.UI
{
    /// <summary>
    /// Main menu controller with Play, Settings, and Credits buttons.
    /// Canvas uses Screen Space - Overlay with CanvasScaler.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _creditsButton;
        [SerializeField] private TMP_Text _titleText;

        private MenuNavigationLogic _logic;

        /// <summary>Play button reference (for test verification).</summary>
        public Button PlayButton => _playButton;

        /// <summary>Settings button reference.</summary>
        public Button SettingsButton => _settingsButton;

        /// <summary>Credits button reference.</summary>
        public Button CreditsButton => _creditsButton;

        private void Awake()
        {
            _logic = new MenuNavigationLogic();

            if (_titleText != null)
                _titleText.text = _logic.GameTitle;
        }

        private void Start()
        {
            if (_playButton != null)
                _playButton.onClick.AddListener(OnPlayClicked);

            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(OnSettingsClicked);

            if (_creditsButton != null)
                _creditsButton.onClick.AddListener(OnCreditsClicked);
        }

        /// <summary>Navigate to Team Select scene.</summary>
        public void OnPlayClicked()
        {
            string scene = _logic.GetTargetScene(MenuButton.Play);
            SceneManager.LoadScene(scene);
        }

        /// <summary>Navigate to Settings scene.</summary>
        public void OnSettingsClicked()
        {
            string scene = _logic.GetTargetScene(MenuButton.Settings);
            SceneManager.LoadScene(scene);
        }

        /// <summary>Navigate to Credits scene.</summary>
        public void OnCreditsClicked()
        {
            string scene = _logic.GetTargetScene(MenuButton.Credits);
            SceneManager.LoadScene(scene);
        }

        private void OnDestroy()
        {
            if (_playButton != null)
                _playButton.onClick.RemoveListener(OnPlayClicked);

            if (_settingsButton != null)
                _settingsButton.onClick.RemoveListener(OnSettingsClicked);

            if (_creditsButton != null)
                _creditsButton.onClick.RemoveListener(OnCreditsClicked);
        }
    }
}
