using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OpenFifa.Core;

namespace OpenFifa.UI
{
    /// <summary>
    /// Controls the in-match HUD displaying score and timer.
    /// Subscribes to MatchScore and MatchTimer events.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _timerText;

        private MatchTimer _matchTimer;
        private MatchScore _matchScore;
        private string _teamAName = "TeamA";
        private string _teamBName = "TeamB";

        /// <summary>The score text component (read-only access for tests).</summary>
        public TMP_Text ScoreText => _scoreText;

        /// <summary>The timer text component (read-only access for tests).</summary>
        public TMP_Text TimerText => _timerText;

        /// <summary>
        /// Initialize with explicit references (for tests).
        /// </summary>
        public void Initialize(MatchTimer timer, MatchScore score,
            TMP_Text scoreText, TMP_Text timerText,
            string teamAName = "TeamA", string teamBName = "TeamB")
        {
            _matchTimer = timer;
            _matchScore = score;
            _scoreText = scoreText;
            _timerText = timerText;
            _teamAName = teamAName;
            _teamBName = teamBName;

            // Subscribe to events
            _matchScore.OnScoreChanged += OnScoreChanged;
            _matchTimer.OnTimeUpdated += OnTimeUpdated;

            // Initial refresh
            RefreshScore();
            RefreshTimer();
        }

        /// <summary>
        /// Configure a CanvasScaler with the correct HUD settings.
        /// </summary>
        public void ConfigureCanvasScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void OnDestroy()
        {
            if (_matchScore != null)
                _matchScore.OnScoreChanged -= OnScoreChanged;
            if (_matchTimer != null)
                _matchTimer.OnTimeUpdated -= OnTimeUpdated;
        }

        private void Update()
        {
            // Update timer every frame for smooth countdown display
            if (_matchTimer != null)
            {
                RefreshTimer();
            }
        }

        private void OnScoreChanged(TeamIdentifier team, int newScore)
        {
            RefreshScore();
        }

        private void OnTimeUpdated(float remainingSeconds)
        {
            RefreshTimer();
        }

        /// <summary>
        /// Refresh the score display text.
        /// </summary>
        public void RefreshScore()
        {
            if (_scoreText != null && _matchScore != null)
            {
                _scoreText.text = HUDFormatter.FormatScore(_matchScore, _teamAName, _teamBName);
            }
        }

        /// <summary>
        /// Refresh the timer display text.
        /// </summary>
        public void RefreshTimer()
        {
            if (_timerText != null && _matchTimer != null)
            {
                _timerText.text = HUDFormatter.FormatTimer(_matchTimer.RemainingSeconds);
            }
        }
    }
}
