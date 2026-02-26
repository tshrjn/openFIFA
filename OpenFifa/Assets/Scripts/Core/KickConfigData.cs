namespace OpenFifa.Core
{
    /// <summary>
    /// Kick type: pass or shoot.
    /// </summary>
    public enum KickType
    {
        Pass = 0,
        Shoot = 1
    }

    /// <summary>
    /// Pure C# configuration data for kick mechanics.
    /// No Unity dependency.
    /// </summary>
    public class KickConfigData
    {
        /// <summary>Force applied for a pass kick.</summary>
        public float PassForce = 8f;

        /// <summary>Force applied for a shoot kick.</summary>
        public float ShootForce = 15f;

        /// <summary>
        /// Time in seconds when the kick contact occurs in the animation.
        /// Must be under 100ms for responsive feel.
        /// </summary>
        public float ContactFrameTime = 0.08f;

        /// <summary>Duration of the full kick animation.</summary>
        public float KickAnimationDuration = 0.4f;
    }

    /// <summary>
    /// Pure C# kick logic. Stores pending kick data and executes on contact frame.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class KickLogic
    {
        private readonly KickConfigData _config;
        private bool _hasPendingKick;
        private KickType _pendingType;
        private float _pendingForce;
        private float _directionX;
        private float _directionZ;

        /// <summary>Whether there is a kick waiting for the contact frame.</summary>
        public bool HasPendingKick => _hasPendingKick;

        /// <summary>Force of the pending kick.</summary>
        public float PendingForce => _pendingForce;

        public KickLogic(KickConfigData config)
        {
            _config = config;
        }

        /// <summary>
        /// Prepare a kick. The kick will be executed when ExecuteKick() is called
        /// (at the animation contact frame).
        /// </summary>
        /// <param name="type">Pass or Shoot.</param>
        /// <param name="playerX">Player position X (unused, for future targeting).</param>
        /// <param name="playerZ">Player position Z (unused, for future targeting).</param>
        /// <param name="facingX">Player facing direction X.</param>
        /// <param name="facingZ">Player facing direction Z.</param>
        public void PrepareKick(KickType type, float playerX, float playerZ, float facingX, float facingZ)
        {
            _hasPendingKick = true;
            _pendingType = type;
            _pendingForce = type == KickType.Pass ? _config.PassForce : _config.ShootForce;

            // Normalize direction
            float mag = Sqrt(facingX * facingX + facingZ * facingZ);
            if (mag > 0.0001f)
            {
                _directionX = facingX / mag;
                _directionZ = facingZ / mag;
            }
            else
            {
                _directionX = 0f;
                _directionZ = 1f; // Default forward
            }
        }

        /// <summary>
        /// Execute the pending kick (called at the animation contact frame).
        /// Returns the kick result with force and direction.
        /// </summary>
        public KickResult ExecuteKick()
        {
            if (!_hasPendingKick)
            {
                return new KickResult { Applied = false };
            }

            var result = new KickResult
            {
                Applied = true,
                Force = _pendingForce,
                DirectionX = _directionX,
                DirectionZ = _directionZ,
                Type = _pendingType
            };

            _hasPendingKick = false;
            return result;
        }

        private static float Sqrt(float value)
        {
            // Simple Newton's method sqrt for pure C# (no Mathf)
            if (value <= 0f) return 0f;
            float guess = value;
            for (int i = 0; i < 10; i++)
            {
                guess = (guess + value / guess) * 0.5f;
            }
            return guess;
        }
    }

    /// <summary>
    /// Result of a kick execution.
    /// </summary>
    public struct KickResult
    {
        /// <summary>Whether the kick was applied.</summary>
        public bool Applied;

        /// <summary>Force magnitude.</summary>
        public float Force;

        /// <summary>Direction X component (normalized).</summary>
        public float DirectionX;

        /// <summary>Direction Z component (normalized).</summary>
        public float DirectionZ;

        /// <summary>Type of kick.</summary>
        public KickType Type;
    }
}
