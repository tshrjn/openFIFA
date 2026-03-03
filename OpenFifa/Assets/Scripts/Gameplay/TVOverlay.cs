using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// TV-style broadcast overlay with score bug, replay label, event info,
    /// and competition branding. Attaches to a Canvas and manages overlay
    /// elements for broadcast camera presentation.
    /// </summary>
    public class TVOverlay : MonoBehaviour
    {
        [Header("Score Bug")]
        [SerializeField] private GameObject _scoreBugRoot;
        [SerializeField] private TextMeshProUGUI _homeTeamNameText;
        [SerializeField] private TextMeshProUGUI _awayTeamNameText;
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private TextMeshProUGUI _matchClockText;

        [Header("Replay Label")]
        [SerializeField] private GameObject _replayLabelRoot;
        [SerializeField] private TextMeshProUGUI _replayText;
        [SerializeField] private CanvasGroup _replayCanvasGroup;
        [SerializeField] private float _replayFadeInDuration = 0.3f;
        [SerializeField] private float _replayFadeOutDuration = 0.5f;

        [Header("Event Info")]
        [SerializeField] private GameObject _eventInfoRoot;
        [SerializeField] private TextMeshProUGUI _eventInfoText;
        [SerializeField] private CanvasGroup _eventInfoCanvasGroup;
        [SerializeField] private float _eventInfoDisplayDuration = 3f;

        [Header("Branding")]
        [SerializeField] private GameObject _brandingRoot;
        [SerializeField] private TextMeshProUGUI _competitionNameText;
        [SerializeField] private TextMeshProUGUI _matchDayText;

        [Header("Slow-Mo Indicator")]
        [SerializeField] private GameObject _slowMoIndicatorRoot;
        [SerializeField] private TextMeshProUGUI _slowMoSpeedText;

        [Header("Director Reference")]
        [SerializeField] private BroadcastDirector _broadcastDirector;

        private TVOverlayConfig _config;
        private string _homeTeamName = "HOME";
        private string _awayTeamName = "AWAY";
        private int _homeScore;
        private int _awayScore;
        private Coroutine _replayFadeCoroutine;
        private Coroutine _eventInfoCoroutine;

        /// <summary>Whether the replay label is currently visible.</summary>
        public bool IsReplayLabelVisible => _replayLabelRoot != null && _replayLabelRoot.activeSelf;

        /// <summary>Whether the slow-mo indicator is visible.</summary>
        public bool IsSlowMoIndicatorVisible => _slowMoIndicatorRoot != null && _slowMoIndicatorRoot.activeSelf;

        /// <summary>The overlay configuration.</summary>
        public TVOverlayConfig Config => _config;

        private void Awake()
        {
            _config = new TVOverlayConfig();
            InitializeOverlay();
        }

        private void OnEnable()
        {
            if (_broadcastDirector != null)
            {
                _broadcastDirector.OnDirectorStateChanged += HandleDirectorStateChanged;
                _broadcastDirector.OnCameraAngleChanged += HandleCameraAngleChanged;
            }

            GoalDetector.OnGoalScored += HandleGoalScored;
        }

        private void OnDisable()
        {
            if (_broadcastDirector != null)
            {
                _broadcastDirector.OnDirectorStateChanged -= HandleDirectorStateChanged;
                _broadcastDirector.OnCameraAngleChanged -= HandleCameraAngleChanged;
            }

            GoalDetector.OnGoalScored -= HandleGoalScored;
        }

        /// <summary>
        /// Initialize with explicit configuration for testing.
        /// </summary>
        public void Initialize(TVOverlayConfig config, string homeTeamName, string awayTeamName)
        {
            _config = config;
            _homeTeamName = homeTeamName;
            _awayTeamName = awayTeamName;
            InitializeOverlay();
        }

        /// <summary>
        /// Set team names.
        /// </summary>
        public void SetTeamNames(string homeTeam, string awayTeam)
        {
            _homeTeamName = homeTeam;
            _awayTeamName = awayTeam;
            UpdateTeamNameDisplay();
        }

        /// <summary>
        /// Update the score display.
        /// </summary>
        public void UpdateScore(int homeScore, int awayScore)
        {
            _homeScore = homeScore;
            _awayScore = awayScore;
            UpdateScoreDisplay();
        }

        /// <summary>
        /// Update the match clock display.
        /// </summary>
        public void UpdateMatchClock(string clockText)
        {
            if (_matchClockText != null)
            {
                _matchClockText.text = clockText;
            }
        }

        /// <summary>
        /// Show the replay label with fade-in animation.
        /// </summary>
        public void ShowReplayLabel()
        {
            if (_replayLabelRoot == null) return;

            _replayLabelRoot.SetActive(true);

            if (_replayFadeCoroutine != null)
                StopCoroutine(_replayFadeCoroutine);

            _replayFadeCoroutine = StartCoroutine(FadeCanvasGroup(_replayCanvasGroup, 0f, 1f, _replayFadeInDuration));
        }

        /// <summary>
        /// Hide the replay label with fade-out animation.
        /// </summary>
        public void HideReplayLabel()
        {
            if (_replayLabelRoot == null) return;

            if (_replayFadeCoroutine != null)
                StopCoroutine(_replayFadeCoroutine);

            _replayFadeCoroutine = StartCoroutine(FadeAndDeactivate(_replayCanvasGroup, _replayLabelRoot, _replayFadeOutDuration));
        }

        /// <summary>
        /// Show the slow-motion speed indicator.
        /// </summary>
        public void ShowSlowMoIndicator(float speed)
        {
            if (_slowMoIndicatorRoot != null)
            {
                _slowMoIndicatorRoot.SetActive(true);

                if (_slowMoSpeedText != null)
                {
                    _slowMoSpeedText.text = $"{speed:F2}x";
                }
            }
        }

        /// <summary>
        /// Hide the slow-motion indicator.
        /// </summary>
        public void HideSlowMoIndicator()
        {
            if (_slowMoIndicatorRoot != null)
            {
                _slowMoIndicatorRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Show event info text (e.g. goal scorer, minute).
        /// </summary>
        public void ShowEventInfo(string text)
        {
            if (_eventInfoRoot == null) return;

            _eventInfoRoot.SetActive(true);

            if (_eventInfoText != null)
            {
                _eventInfoText.text = text;
            }

            if (_eventInfoCoroutine != null)
                StopCoroutine(_eventInfoCoroutine);

            _eventInfoCoroutine = StartCoroutine(ShowEventInfoCoroutine());
        }

        /// <summary>
        /// Set the competition branding text.
        /// </summary>
        public void SetCompetitionBranding(string competitionName, string matchDay)
        {
            if (_competitionNameText != null)
            {
                _competitionNameText.text = competitionName;
            }

            if (_matchDayText != null)
            {
                _matchDayText.text = matchDay;
            }
        }

        /// <summary>
        /// Show or hide the score bug.
        /// </summary>
        public void SetScoreBugVisible(bool visible)
        {
            if (_scoreBugRoot != null)
            {
                _scoreBugRoot.SetActive(visible);
            }
        }

        private void InitializeOverlay()
        {
            // Ensure replay and event info start hidden
            if (_replayLabelRoot != null) _replayLabelRoot.SetActive(false);
            if (_eventInfoRoot != null) _eventInfoRoot.SetActive(false);
            if (_slowMoIndicatorRoot != null) _slowMoIndicatorRoot.SetActive(false);

            // Set initial score
            UpdateTeamNameDisplay();
            UpdateScoreDisplay();

            // Set replay text
            if (_replayText != null)
            {
                _replayText.text = "REPLAY";
            }
        }

        private void UpdateTeamNameDisplay()
        {
            if (_homeTeamNameText != null) _homeTeamNameText.text = _homeTeamName;
            if (_awayTeamNameText != null) _awayTeamNameText.text = _awayTeamName;
        }

        private void UpdateScoreDisplay()
        {
            if (_scoreText != null)
            {
                _scoreText.text = $"{_homeScore} - {_awayScore}";
            }
        }

        private void HandleDirectorStateChanged(DirectorState oldState, DirectorState newState)
        {
            switch (newState)
            {
                case DirectorState.Replay:
                    ShowReplayLabel();
                    break;

                case DirectorState.Live:
                    if (oldState == DirectorState.Replay)
                    {
                        HideReplayLabel();
                        HideSlowMoIndicator();
                    }
                    break;

                case DirectorState.Celebration:
                    // Score bug stays visible during celebration
                    break;
            }
        }

        private void HandleCameraAngleChanged(CameraAngle newAngle)
        {
            // Could show angle-specific UI elements here
        }

        private void HandleGoalScored(TeamIdentifier team)
        {
            if (team == TeamIdentifier.TeamA)
            {
                _homeScore++;
            }
            else
            {
                _awayScore++;
            }

            UpdateScoreDisplay();
            ShowEventInfo($"GOAL! {(team == TeamIdentifier.TeamA ? _homeTeamName : _awayTeamName)}");
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;

            group.alpha = from;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            group.alpha = to;
        }

        private IEnumerator FadeAndDeactivate(CanvasGroup group, GameObject root, float duration)
        {
            yield return FadeCanvasGroup(group, 1f, 0f, duration);

            if (root != null) root.SetActive(false);
        }

        private IEnumerator ShowEventInfoCoroutine()
        {
            if (_eventInfoCanvasGroup != null)
            {
                yield return FadeCanvasGroup(_eventInfoCanvasGroup, 0f, 1f, 0.3f);
            }

            yield return new WaitForSecondsRealtime(_eventInfoDisplayDuration);

            if (_eventInfoCanvasGroup != null)
            {
                yield return FadeCanvasGroup(_eventInfoCanvasGroup, 1f, 0f, 0.5f);
            }

            if (_eventInfoRoot != null) _eventInfoRoot.SetActive(false);
        }
    }
}
