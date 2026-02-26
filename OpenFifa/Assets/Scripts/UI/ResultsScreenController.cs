using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using OpenFifa.Core;

namespace OpenFifa.UI
{
    /// <summary>
    /// Post-match results screen showing final score, duration,
    /// man of the match, and navigation buttons.
    /// </summary>
    public class ResultsScreenController : MonoBehaviour
    {
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _durationText;
        [SerializeField] private TMP_Text _motmText;
        [SerializeField] private TMP_Text _winnerText;
        [SerializeField] private Button _playAgainButton;
        [SerializeField] private Button _mainMenuButton;

        private MatchResultsLogic _results;

        /// <summary>Play Again button (for test verification).</summary>
        public Button PlayAgainButton => _playAgainButton;

        /// <summary>Main Menu button (for test verification).</summary>
        public Button MainMenuButton => _mainMenuButton;

        private void Start()
        {
            if (_playAgainButton != null)
                _playAgainButton.onClick.AddListener(OnPlayAgainClicked);

            if (_mainMenuButton != null)
                _mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        /// <summary>
        /// Set the match results to display.
        /// </summary>
        public void SetResults(MatchResultsLogic results)
        {
            _results = results;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (_results == null) return;

            if (_scoreText != null)
                _scoreText.text = _results.FinalScoreDisplay;

            if (_durationText != null)
                _durationText.text = _results.DurationDisplay;

            if (_motmText != null)
                _motmText.text = _results.GetManOfTheMatch();

            if (_winnerText != null)
                _winnerText.text = _results.WinnerTeam;
        }

        /// <summary>Navigate to team select for another match.</summary>
        public void OnPlayAgainClicked()
        {
            SceneManager.LoadScene("TeamSelect");
        }

        /// <summary>Navigate to main menu.</summary>
        public void OnMainMenuClicked()
        {
            SceneManager.LoadScene("MainMenu");
        }

        private void OnDestroy()
        {
            if (_playAgainButton != null)
                _playAgainButton.onClick.RemoveListener(OnPlayAgainClicked);
            if (_mainMenuButton != null)
                _mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
        }
    }
}
