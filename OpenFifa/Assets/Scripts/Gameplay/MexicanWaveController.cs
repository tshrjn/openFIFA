using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// MonoBehaviour controller for triggering and managing Mexican waves.
    /// Monitors match conditions and triggers waves on specific events
    /// (big goal, 2+ goal lead). Also provides a public API for manual triggering.
    /// Works with CrowdAnimationSystem to animate crowd sections.
    /// </summary>
    public class MexicanWaveController : MonoBehaviour
    {
        [SerializeField] private int _goalLeadThreshold = 2;
        [SerializeField] private float _cooldownDuration = 30f;
        [SerializeField] private bool _clockwise = true;

        private CrowdAnimationSystem _animationSystem;
        private float _cooldownTimer;
        private bool _isOnCooldown;
        private int _lastGoalDifference;
        private int _totalGoals;

        /// <summary>Whether a wave is currently in progress.</summary>
        public bool IsWaveActive => _animationSystem != null
            && _animationSystem.Director != null
            && _animationSystem.Director.WaveOrchestrator.IsWaveActive;

        /// <summary>Whether the controller is on cooldown after a wave.</summary>
        public bool IsOnCooldown => _isOnCooldown;

        /// <summary>Remaining cooldown time in seconds.</summary>
        public float CooldownRemaining => _isOnCooldown ? _cooldownDuration - _cooldownTimer : 0f;

        /// <summary>Total goals tracked for wave trigger logic.</summary>
        public int TotalGoals => _totalGoals;

        private void Awake()
        {
            _animationSystem = GetComponent<CrowdAnimationSystem>();
        }

        private void OnEnable()
        {
            GoalDetector.OnGoalScored += HandleGoalScored;
        }

        private void OnDisable()
        {
            GoalDetector.OnGoalScored -= HandleGoalScored;
        }

        private void Update()
        {
            if (_isOnCooldown)
            {
                _cooldownTimer += Time.deltaTime;
                if (_cooldownTimer >= _cooldownDuration)
                {
                    _isOnCooldown = false;
                    _cooldownTimer = 0f;
                }
            }
        }

        /// <summary>
        /// Initialize with custom settings (for testing).
        /// </summary>
        public void Initialize(CrowdAnimationSystem animSystem, int goalLeadThreshold = 2,
            float cooldown = 30f, bool clockwise = true)
        {
            _animationSystem = animSystem;
            _goalLeadThreshold = goalLeadThreshold;
            _cooldownDuration = cooldown;
            _clockwise = clockwise;
        }

        /// <summary>
        /// Manually trigger a Mexican wave. Respects cooldown.
        /// </summary>
        /// <param name="clockwise">Direction of the wave. If null, uses configured default.</param>
        /// <returns>True if wave was started, false if on cooldown or already active.</returns>
        public bool TriggerWave(bool? clockwise = null)
        {
            if (_isOnCooldown) return false;
            if (IsWaveActive) return false;
            if (_animationSystem == null || _animationSystem.Director == null) return false;

            bool direction = clockwise ?? _clockwise;
            _animationSystem.Director.WaveOrchestrator.StartWave(direction);

            _isOnCooldown = true;
            _cooldownTimer = 0f;

            return true;
        }

        /// <summary>
        /// Stop the current wave immediately.
        /// </summary>
        public void StopWave()
        {
            if (_animationSystem == null || _animationSystem.Director == null) return;
            _animationSystem.Director.WaveOrchestrator.StopWave();
        }

        /// <summary>
        /// Update the goal difference tracking. Call this when the score changes.
        /// </summary>
        /// <param name="homeGoals">Home team goals.</param>
        /// <param name="awayGoals">Away team goals.</param>
        public void UpdateScore(int homeGoals, int awayGoals)
        {
            _lastGoalDifference = homeGoals - awayGoals;
            _totalGoals = homeGoals + awayGoals;
        }

        private void HandleGoalScored(TeamIdentifier team)
        {
            _totalGoals++;

            if (team == TeamIdentifier.TeamA)
                _lastGoalDifference++;
            else
                _lastGoalDifference--;

            // Check wave trigger conditions:
            // 1. Goal lead meets threshold
            // 2. Not on cooldown
            // 3. No wave currently active
            if (ShouldTriggerWave())
            {
                TriggerWave();
            }
        }

        private bool ShouldTriggerWave()
        {
            if (_isOnCooldown) return false;
            if (IsWaveActive) return false;

            // Trigger on significant goal lead
            int absLead = _lastGoalDifference < 0 ? -_lastGoalDifference : _lastGoalDifference;
            if (absLead >= _goalLeadThreshold)
                return true;

            return false;
        }
    }
}
