using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OpenFifa.Core;

namespace OpenFifa.UI
{
    /// <summary>
    /// Full match HUD with score, timer, minimap, and match state indicator.
    /// Extends the basic HUD from US-008 with minimap and state display.
    /// </summary>
    public class FullMatchHUD : MonoBehaviour
    {
        [Header("Score & Timer")]
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _timerText;

        [Header("Match State")]
        [SerializeField] private TMP_Text _matchStateText;

        [Header("Minimap")]
        [SerializeField] private RectTransform _minimapRect;
        [SerializeField] private RectTransform _ballDot;
        [SerializeField] private List<RectTransform> _teamADots;
        [SerializeField] private List<RectTransform> _teamBDots;

        [Header("References")]
        [SerializeField] private Transform _ball;
        [SerializeField] private List<Transform> _teamAPlayers;
        [SerializeField] private List<Transform> _teamBPlayers;

        [Header("Config")]
        [SerializeField] private float _pitchLength = 50f;
        [SerializeField] private float _pitchWidth = 30f;

        private MinimapLogic _minimapLogic;
        private MatchStateDisplay _stateDisplay;
        private MatchScore _score;
        private MatchTimer _timer;

        /// <summary>The minimap logic for coordinate mapping.</summary>
        public MinimapLogic MinimapLogic => _minimapLogic;

        private void Awake()
        {
            _stateDisplay = new MatchStateDisplay();

            // Configure safe area
            ConfigureSafeArea();
        }

        private void Start()
        {
            if (_minimapRect != null)
            {
                _minimapLogic = new MinimapLogic(
                    _pitchLength, _pitchWidth,
                    _minimapRect.rect.width, _minimapRect.rect.height
                );
            }
        }

        private void Update()
        {
            UpdateMinimap();
        }

        /// <summary>
        /// Set the score tracker to display.
        /// </summary>
        public void SetScore(MatchScore score)
        {
            _score = score;
            UpdateScoreDisplay();
        }

        /// <summary>
        /// Set the timer to display.
        /// </summary>
        public void SetTimer(MatchTimer timer)
        {
            _timer = timer;
        }

        /// <summary>
        /// Update the match state text display.
        /// </summary>
        public void OnMatchStateChanged(MatchState oldState, MatchState newState)
        {
            if (_matchStateText != null)
                _matchStateText.text = _stateDisplay.GetStateText(newState);
        }

        /// <summary>
        /// Update score display text.
        /// </summary>
        public void UpdateScoreDisplay()
        {
            if (_scoreText != null && _score != null)
            {
                _scoreText.text = _score.GetScoreDisplay();
            }
        }

        /// <summary>
        /// Update timer display text.
        /// </summary>
        public void UpdateTimerDisplay(float remainingTime)
        {
            if (_timerText != null)
            {
                _timerText.text = HUDFormatter.FormatTimer(remainingTime);
            }
        }

        private void UpdateMinimap()
        {
            if (_minimapLogic == null || _minimapRect == null) return;

            // Update ball dot
            if (_ball != null && _ballDot != null)
            {
                float mx, my;
                _minimapLogic.WorldToMinimap(_ball.position.x, _ball.position.z, out mx, out my);
                _ballDot.anchoredPosition = new Vector2(mx, my);
            }

            // Update team A dots
            UpdateDots(_teamAPlayers, _teamADots);

            // Update team B dots
            UpdateDots(_teamBPlayers, _teamBDots);
        }

        private void UpdateDots(List<Transform> players, List<RectTransform> dots)
        {
            if (players == null || dots == null || _minimapLogic == null) return;

            int count = Mathf.Min(players.Count, dots.Count);
            for (int i = 0; i < count; i++)
            {
                if (players[i] == null || dots[i] == null) continue;

                float mx, my;
                _minimapLogic.WorldToMinimap(players[i].position.x, players[i].position.z, out mx, out my);
                dots[i].anchoredPosition = new Vector2(mx, my);
            }
        }

        private void ConfigureSafeArea()
        {
            // Position HUD elements within safe area bounds
            var safeArea = Screen.safeArea;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect == null) return;

            // Apply safe area inset to this RectTransform
            var rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector2 anchorMin = safeArea.position;
                Vector2 anchorMax = safeArea.position + safeArea.size;

                anchorMin.x /= Screen.width;
                anchorMin.y /= Screen.height;
                anchorMax.x /= Screen.width;
                anchorMax.y /= Screen.height;

                rectTransform.anchorMin = anchorMin;
                rectTransform.anchorMax = anchorMax;
            }
        }
    }
}
