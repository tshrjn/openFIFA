using System.Collections;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Goal celebration sequence with slow-motion and camera zoom.
    /// On goal scored: slow-motion at 0.3x for 2 real-time seconds,
    /// scoring player celebrates, then kickoff begins.
    /// </summary>
    public class CelebrationSequence : MonoBehaviour
    {
        [SerializeField] private float _slowMotionScale = 0.3f;
        [SerializeField] private float _celebrationDuration = 2f;

        private CelebrationLogic _logic;
        private Coroutine _activeCoroutine;

        /// <summary>Whether a celebration is currently playing.</summary>
        public bool IsPlaying => _logic != null && _logic.IsPlaying;

        /// <summary>The underlying celebration logic.</summary>
        public CelebrationLogic Logic => _logic;

        private void Awake()
        {
            _logic = new CelebrationLogic();
        }

        private void OnEnable()
        {
            GoalDetector.OnGoalScored += HandleGoalScored;
        }

        private void OnDisable()
        {
            GoalDetector.OnGoalScored -= HandleGoalScored;
        }

        private void HandleGoalScored(TeamIdentifier scoringTeam)
        {
            if (!_logic.TryStartCelebration()) return;

            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _activeCoroutine = StartCoroutine(CelebrationCoroutine());
        }

        /// <summary>
        /// Trigger celebration manually (for testing).
        /// </summary>
        public void TriggerCelebration()
        {
            if (!_logic.TryStartCelebration()) return;

            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _activeCoroutine = StartCoroutine(CelebrationCoroutine());
        }

        private IEnumerator CelebrationCoroutine()
        {
            // Step 1: Set slow motion
            Time.timeScale = _slowMotionScale;

            // Step 2: Wait for celebration duration (real-time, unaffected by timeScale)
            yield return new WaitForSecondsRealtime(_celebrationDuration);

            // Step 3: Restore time scale
            Time.timeScale = 1.0f;

            // Step 4: Complete celebration
            _logic.CompleteCelebration();
            _activeCoroutine = null;
        }
    }
}
