namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# tackle logic with range checking, cooldown, and dispossession.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class TackleLogic
    {
        private readonly float _tackleRadius;
        private readonly float _cooldownDuration;
        private readonly float _stunDuration;

        private bool _isLunging;
        private float _lastTackleTime;
        private bool _hasTackled;

        /// <summary>Maximum distance for a successful tackle.</summary>
        public float TackleRadius => _tackleRadius;

        /// <summary>Duration the tackled player is stunned.</summary>
        public float StunDuration => _stunDuration;

        /// <summary>Whether the player is currently lunging.</summary>
        public bool IsLunging => _isLunging;

        /// <summary>Whether tackle is on cooldown.</summary>
        public bool IsCoolingDown => _hasTackled;

        /// <summary>
        /// Create tackle logic with default values: radius 1.5, cooldown 1.0, stun 0.5.
        /// </summary>
        public TackleLogic() : this(1.5f, 1.0f, 0.5f) { }

        /// <summary>
        /// Create tackle logic with custom values.
        /// </summary>
        public TackleLogic(float tackleRadius, float cooldownDuration, float stunDuration)
        {
            _tackleRadius = tackleRadius;
            _cooldownDuration = cooldownDuration;
            _stunDuration = stunDuration;
            _isLunging = false;
            _hasTackled = false;
            _lastTackleTime = -999f;
        }

        /// <summary>
        /// Check if a tackle can be attempted at the given distance and time.
        /// </summary>
        /// <param name="distanceToTarget">Distance to the ball carrier.</param>
        /// <param name="currentTime">Current game time (e.g., Time.time).</param>
        public bool CanAttemptTackle(float distanceToTarget, float currentTime)
        {
            if (_isLunging) return false;
            if (_hasTackled && (currentTime - _lastTackleTime) < _cooldownDuration) return false;
            if (distanceToTarget > _tackleRadius) return false;
            return true;
        }

        /// <summary>
        /// Attempt a tackle. Returns the result including whether dispossession occurred.
        /// </summary>
        /// <param name="distanceToTarget">Distance to the ball carrier.</param>
        /// <param name="currentTime">Current game time.</param>
        public TackleResult AttemptTackle(float distanceToTarget, float currentTime)
        {
            if (!CanAttemptTackle(distanceToTarget, currentTime))
            {
                return new TackleResult
                {
                    DidLunge = false,
                    DidDispossess = false,
                    StunDuration = 0f
                };
            }

            _isLunging = true;
            _lastTackleTime = currentTime;

            bool dispossess = distanceToTarget <= _tackleRadius;

            return new TackleResult
            {
                DidLunge = true,
                DidDispossess = dispossess,
                StunDuration = _stunDuration
            };
        }

        /// <summary>
        /// Called when the lunge animation/movement completes.
        /// Starts cooldown.
        /// </summary>
        public void CompleteLunge()
        {
            _isLunging = false;
            _hasTackled = true;
        }
    }

    /// <summary>
    /// Result of a tackle attempt.
    /// </summary>
    public struct TackleResult
    {
        /// <summary>Whether the tackler lunged forward.</summary>
        public bool DidLunge;

        /// <summary>Whether the ball carrier was dispossessed.</summary>
        public bool DidDispossess;

        /// <summary>Duration the tackled player should be stunned (0 if no dispossession).</summary>
        public float StunDuration;
    }
}
