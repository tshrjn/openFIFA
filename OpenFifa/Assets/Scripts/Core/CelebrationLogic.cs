namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# celebration sequence logic.
    /// Manages slow-motion state and celebration lifecycle.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class CelebrationLogic
    {
        private const float SlowMotionScale = 0.3f;
        private const float NormalScale = 1.0f;
        private const float DefaultDuration = 2.0f;

        private bool _isPlaying;
        private float _targetTimeScale;
        private bool _shouldTriggerKickoff;

        /// <summary>Whether a celebration is currently active.</summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>Target time scale (0.3 during celebration, 1.0 otherwise).</summary>
        public float TargetTimeScale => _targetTimeScale;

        /// <summary>Duration of the celebration in real-time seconds.</summary>
        public float CelebrationDuration => DefaultDuration;

        /// <summary>
        /// Whether kickoff should be triggered. Resets to false after reading.
        /// </summary>
        public bool ShouldTriggerKickoff
        {
            get
            {
                if (_shouldTriggerKickoff)
                {
                    _shouldTriggerKickoff = false;
                    return true;
                }
                return false;
            }
        }

        public CelebrationLogic()
        {
            _isPlaying = false;
            _targetTimeScale = NormalScale;
            _shouldTriggerKickoff = false;
        }

        /// <summary>
        /// Start a celebration. Sets slow motion and playing state.
        /// </summary>
        public void StartCelebration()
        {
            _isPlaying = true;
            _targetTimeScale = SlowMotionScale;
            _shouldTriggerKickoff = false;
        }

        /// <summary>
        /// Try to start a celebration. Returns false if one is already playing.
        /// Prevents stacking.
        /// </summary>
        public bool TryStartCelebration()
        {
            if (_isPlaying) return false;
            StartCelebration();
            return true;
        }

        /// <summary>
        /// Complete the celebration. Restores time scale and flags kickoff trigger.
        /// </summary>
        public void CompleteCelebration()
        {
            _isPlaying = false;
            _targetTimeScale = NormalScale;
            _shouldTriggerKickoff = true;
        }
    }
}
