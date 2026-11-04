namespace OpenFifa.Core
{
    /// <summary>
    /// Playback rate for in-place locomotion clips so feet don't skate: inside the authored speed range the blend
    /// tree interpolates and the rate is 1; outside it the nearest clip is time-scaled, clamped.
    /// </summary>
    public sealed class LocomotionRateSolver
    {
        private readonly float[] _authoredSpeeds;
        public float MinRate { get; }
        public float MaxRate { get; }

        /// <param name="authoredSpeeds">Natural speeds (m/s) of the moving clips in the blend tree, ascending, all > 0.</param>
        public LocomotionRateSolver(float[] authoredSpeeds, float minRate = 0.6f, float maxRate = 1.4f)
        {
            if (authoredSpeeds == null || authoredSpeeds.Length == 0)
                throw new System.ArgumentException("Need at least one authored speed", nameof(authoredSpeeds));
            _authoredSpeeds = (float[])authoredSpeeds.Clone();
            System.Array.Sort(_authoredSpeeds);
            if (_authoredSpeeds[0] <= 0f)
                throw new System.ArgumentException("Authored speeds must be > 0", nameof(authoredSpeeds));
            MinRate = minRate;
            MaxRate = maxRate;
        }

        public float SlowestSpeed => _authoredSpeeds[0];
        public float FastestSpeed => _authoredSpeeds[_authoredSpeeds.Length - 1];

        public float RateFor(float speed)
        {
            if (speed < 0f) speed = -speed;
            if (speed < 1e-3f) return 1f; // idle
            if (speed < SlowestSpeed) return AnimMath.Clamp(speed / SlowestSpeed, MinRate, 1f);
            if (speed > FastestSpeed) return AnimMath.Clamp(speed / FastestSpeed, 1f, MaxRate);
            return 1f;
        }
    }
}
