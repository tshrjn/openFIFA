namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# ball trail logic. Determines emission state, alpha, and rate
    /// based on ball velocity. No Unity dependency.
    /// </summary>
    public class BallTrailLogic
    {
        private const float DefaultVelocityThreshold = 10f;
        private const float MaxSpeedForScaling = 30f;
        private const float MinEmissionRate = 5f;
        private const float MaxEmissionRate = 30f;
        private const int DefaultMaxParticles = 50;

        private readonly float _velocityThreshold;
        private readonly int _maxParticles;

        private bool _shouldEmit;
        private float _trailAlpha;
        private float _emissionRate;

        /// <summary>Velocity threshold for emission activation.</summary>
        public float VelocityThreshold => _velocityThreshold;

        /// <summary>Maximum particle count.</summary>
        public int MaxParticles => _maxParticles;

        /// <summary>Whether particles should be emitting.</summary>
        public bool ShouldEmit => _shouldEmit;

        /// <summary>Trail color alpha (0-1) proportional to speed.</summary>
        public float TrailAlpha => _trailAlpha;

        /// <summary>Emission rate (particles per second).</summary>
        public float EmissionRate => _emissionRate;

        public BallTrailLogic() : this(DefaultVelocityThreshold, DefaultMaxParticles) { }

        public BallTrailLogic(float velocityThreshold, int maxParticles)
        {
            _velocityThreshold = velocityThreshold;
            _maxParticles = maxParticles;
        }

        /// <summary>
        /// Update trail state based on current ball speed.
        /// </summary>
        /// <param name="speed">Ball velocity magnitude.</param>
        public void Update(float speed)
        {
            _shouldEmit = speed >= _velocityThreshold;

            if (!_shouldEmit)
            {
                _trailAlpha = 0f;
                _emissionRate = 0f;
                return;
            }

            // Speed ratio above threshold, clamped to 0-1
            float speedAboveThreshold = speed - _velocityThreshold;
            float maxAboveThreshold = MaxSpeedForScaling - _velocityThreshold;
            float ratio = speedAboveThreshold / maxAboveThreshold;
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;

            _trailAlpha = ratio;

            // Emission rate: lerp between min and max
            _emissionRate = MinEmissionRate + (MaxEmissionRate - MinEmissionRate) * ratio;
        }
    }
}
