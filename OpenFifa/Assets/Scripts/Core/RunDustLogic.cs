namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# run dust particle logic.
    /// Determines emission state and rate based on player speed.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class RunDustLogic
    {
        private const float DefaultWalkThreshold = 2f;
        private const float MaxSpeedForScaling = 12f;
        private const float MinEmissionRate = 2f;
        private const float MaxEmissionRate = 15f;
        private const int DefaultMaxParticles = 20;

        private readonly float _walkThreshold;
        private readonly int _maxParticles;

        private bool _shouldEmit;
        private float _emissionRate;

        /// <summary>Speed threshold for emission activation.</summary>
        public float WalkThreshold => _walkThreshold;

        /// <summary>Maximum particle count per player.</summary>
        public int MaxParticles => _maxParticles;

        /// <summary>Whether particles should be emitting.</summary>
        public bool ShouldEmit => _shouldEmit;

        /// <summary>Current emission rate (particles per second).</summary>
        public float EmissionRate => _emissionRate;

        public RunDustLogic() : this(DefaultWalkThreshold, DefaultMaxParticles) { }

        public RunDustLogic(float walkThreshold, int maxParticles)
        {
            _walkThreshold = walkThreshold;
            _maxParticles = maxParticles;
        }

        /// <summary>
        /// Update dust state based on current player speed.
        /// </summary>
        /// <param name="speed">Player velocity magnitude.</param>
        public void Update(float speed)
        {
            _shouldEmit = speed > _walkThreshold;

            if (!_shouldEmit)
            {
                _emissionRate = 0f;
                return;
            }

            float speedAboveThreshold = speed - _walkThreshold;
            float maxAboveThreshold = MaxSpeedForScaling - _walkThreshold;
            float ratio = speedAboveThreshold / maxAboveThreshold;
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;

            _emissionRate = MinEmissionRate + (MaxEmissionRate - MinEmissionRate) * ratio;
        }
    }
}
