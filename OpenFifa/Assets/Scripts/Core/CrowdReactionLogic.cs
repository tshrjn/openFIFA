using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# crowd reaction logic. Calculates crowd intensity
    /// based on ball proximity to goals and detects near-misses.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class CrowdReactionLogic
    {
        private const float BaseIntensityValue = 0.3f;
        private const float MinVolumeDe = -20f;
        private const float MaxVolumeDe = 0f;
        private const float NearMissGoalProximity = 23f; // X position threshold
        private const float NearMissPostDistance = 4f; // Wider than goal half-width (2.5m)
        private const float NearMissSpeedThreshold = 10f;
        private const float LerpSpeed = 3f;

        private readonly float _pitchHalfLength;
        private float _currentIntensity;
        private float _smoothedIntensity;

        /// <summary>Base crowd intensity (30%).</summary>
        public float BaseIntensity => BaseIntensityValue;

        /// <summary>Current target intensity based on ball position (0-1).</summary>
        public float CurrentIntensity => _currentIntensity;

        /// <summary>Smoothed intensity (lerped toward current).</summary>
        public float SmoothedIntensity => _smoothedIntensity;

        /// <summary>Volume in decibels mapped from smoothed intensity.</summary>
        public float VolumeDe => MinVolumeDe + _smoothedIntensity * (MaxVolumeDe - MinVolumeDe);

        /// <param name="pitchHalfLength">Half the pitch length (e.g., 25 for a 50m pitch).</param>
        public CrowdReactionLogic(float pitchHalfLength)
        {
            _pitchHalfLength = pitchHalfLength;
            _currentIntensity = BaseIntensityValue;
            _smoothedIntensity = BaseIntensityValue;
        }

        /// <summary>
        /// Update target intensity based on ball position.
        /// Intensity increases as ball gets closer to either goal.
        /// </summary>
        /// <param name="ballX">Ball X position (goals at +/- pitchHalfLength).</param>
        /// <param name="ballZ">Ball Z position.</param>
        public void UpdateBallPosition(float ballX, float ballZ)
        {
            // Distance to nearest goal (absolute X / pitchHalfLength gives 0..1 ratio)
            float absX = Math.Abs(ballX);
            float goalProximity = absX / _pitchHalfLength; // 0 at center, 1 at goal
            if (goalProximity > 1f) goalProximity = 1f;

            // Map goal proximity to intensity: base at center, 1.0 at goal
            _currentIntensity = BaseIntensityValue + (1f - BaseIntensityValue) * goalProximity;
            if (_currentIntensity > 1f) _currentIntensity = 1f;
        }

        /// <summary>
        /// Smooth the intensity transition over time.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        public void SmoothUpdate(float deltaTime)
        {
            float diff = _currentIntensity - _smoothedIntensity;
            float step = LerpSpeed * deltaTime;
            if (Math.Abs(diff) < step)
            {
                _smoothedIntensity = _currentIntensity;
            }
            else
            {
                _smoothedIntensity += Math.Sign(diff) * step;
            }
        }

        /// <summary>
        /// Check if a near-miss event occurred (ball passes close to goal without scoring).
        /// </summary>
        /// <param name="ballX">Ball X position.</param>
        /// <param name="ballZ">Ball Z position.</param>
        /// <param name="ballSpeed">Ball speed magnitude.</param>
        /// <param name="movingTowardGoal">Whether the ball is moving toward a goal.</param>
        public bool CheckNearMiss(float ballX, float ballZ, float ballSpeed, bool movingTowardGoal)
        {
            if (!movingTowardGoal) return false;
            if (ballSpeed < NearMissSpeedThreshold) return false;

            float absX = Math.Abs(ballX);
            if (absX < NearMissGoalProximity) return false;

            // Ball is near goal line, check if Z is outside goal width (near post)
            float absZ = Math.Abs(ballZ);
            if (absZ > 2.5f && absZ < NearMissPostDistance)
                return true;

            return false;
        }
    }
}
