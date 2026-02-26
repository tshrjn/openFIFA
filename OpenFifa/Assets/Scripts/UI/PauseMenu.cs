using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using OpenFifa.Core;

namespace OpenFifa.UI
{
    /// <summary>
    /// Pause menu with Resume, Restart, and Quit buttons.
    /// Freezes gameplay by setting Time.timeScale to 0.
    /// Integrates with MatchStateMachine for state-aware pausing.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject _pauseOverlay;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _quitButton;

        private PauseLogic _logic;
        private MatchStateMachine _matchStateMachine;

        /// <summary>Whether the game is currently paused.</summary>
        public bool IsPaused => _logic != null && _logic.IsPaused;

        /// <summary>The underlying pause logic.</summary>
        public PauseLogic Logic => _logic;

        private void Awake()
        {
            _logic = new PauseLogic();

            if (_pauseOverlay != null)
                _pauseOverlay.SetActive(false);
        }

        private void Start()
        {
            if (_resumeButton != null)
                _resumeButton.onClick.AddListener(OnResumeClicked);

            if (_restartButton != null)
                _restartButton.onClick.AddListener(OnRestartClicked);

            if (_quitButton != null)
                _quitButton.onClick.AddListener(OnQuitClicked);
        }

        /// <summary>
        /// Set the match state machine for state integration.
        /// </summary>
        public void SetMatchStateMachine(MatchStateMachine fsm)
        {
            _matchStateMachine = fsm;
        }

        /// <summary>
        /// Toggle pause state. Called by input (Escape key / on-screen button).
        /// </summary>
        public void TogglePause()
        {
            if (_logic.IsPaused)
                OnResumeClicked();
            else
                DoPause();
        }

        private void DoPause()
        {
            _logic.Pause(Time.timeScale);
            Time.timeScale = _logic.TargetTimeScale;

            if (_pauseOverlay != null)
                _pauseOverlay.SetActive(true);

            if (_matchStateMachine != null)
                _matchStateMachine.Pause();
        }

        /// <summary>Resume the game.</summary>
        public void OnResumeClicked()
        {
            _logic.Resume();
            Time.timeScale = _logic.TargetTimeScale;

            if (_pauseOverlay != null)
                _pauseOverlay.SetActive(false);

            if (_matchStateMachine != null)
                _matchStateMachine.Resume();
        }

        /// <summary>Restart the match.</summary>
        public void OnRestartClicked()
        {
            _logic.Restart();
            Time.timeScale = _logic.TargetTimeScale;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>Quit to main menu.</summary>
        public void OnQuitClicked()
        {
            _logic.Quit();
            Time.timeScale = _logic.TargetTimeScale;
            SceneManager.LoadScene("MainMenu");
        }

        private void OnDestroy()
        {
            if (_resumeButton != null)
                _resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (_restartButton != null)
                _restartButton.onClick.RemoveListener(OnRestartClicked);
            if (_quitButton != null)
                _quitButton.onClick.RemoveListener(OnQuitClicked);
        }
    }
}
