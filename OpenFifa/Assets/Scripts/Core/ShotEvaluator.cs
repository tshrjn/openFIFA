using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# shot evaluation logic.
    /// Determines shooting opportunities and calculates shot parameters.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class ShotEvaluator
    {
        private readonly float _shootRange;
        private readonly float _baseShotForce;
        private readonly float _shotForceMultiplier;
        private readonly Random _random;

        public ShotEvaluator(
            float shootRange = 15f,
            float baseShotForce = 12f,
            float shotForceMultiplier = 0.5f)
        {
            _shootRange = shootRange;
            _baseShotForce = baseShotForce;
            _shotForceMultiplier = shotForceMultiplier;
            _random = new Random();
        }

        /// <summary>
        /// Check if the shooter is within shooting range of the goal.
        /// </summary>
        public bool IsInShootingRange(float shooterX, float shooterZ, float goalX, float goalZ)
        {
            float dx = goalX - shooterX;
            float dz = goalZ - shooterZ;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);
            return distance <= _shootRange;
        }

        /// <summary>
        /// Calculate shot force based on distance to goal.
        /// </summary>
        public float CalculateShotForce(float distanceToGoal)
        {
            return _baseShotForce + distanceToGoal * _shotForceMultiplier;
        }

        /// <summary>
        /// Calculate a slightly randomized shot target Z position within the goal.
        /// </summary>
        public float CalculateShotTargetZ(float goalCenterZ, float goalHalfWidth)
        {
            float range = goalHalfWidth * 0.8f;
            float offset = (float)(_random.NextDouble() * 2.0 - 1.0) * range;
            return goalCenterZ + offset;
        }

        /// <summary>
        /// Determine whether the AI should shoot.
        /// </summary>
        public bool ShouldShoot(float distanceToGoal, bool hasClearLine, bool hasBallPossession)
        {
            return hasBallPossession && hasClearLine && distanceToGoal <= _shootRange;
        }
    }
}
