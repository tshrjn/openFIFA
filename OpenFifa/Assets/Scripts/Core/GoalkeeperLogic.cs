using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# goalkeeper positioning and shot detection logic.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class GoalkeeperLogic
    {
        private readonly float _goalAreaWidth;
        private readonly float _goalCenterX;
        private readonly float _goalCenterZ;
        private readonly float _goalAreaHalfWidth;

        public GoalkeeperLogic(float goalAreaWidth, float goalCenterX, float goalCenterZ)
        {
            _goalAreaWidth = goalAreaWidth;
            _goalCenterX = goalCenterX;
            _goalCenterZ = goalCenterZ;
            _goalAreaHalfWidth = goalAreaWidth / 2f;
        }

        /// <summary>
        /// Calculate the goalkeeper's lateral position (Z axis) based on ball position.
        /// Returns a Z position between goal center and ball, clamped to goal area.
        /// </summary>
        public float CalculateLateralPosition(float ballZ)
        {
            // Lerp between goal center Z and ball Z, at 0.6 ratio toward ball
            float targetZ = _goalCenterZ + (ballZ - _goalCenterZ) * 0.6f;

            // Clamp to goal area width
            float clamped = Math.Max(-_goalAreaHalfWidth, Math.Min(_goalAreaHalfWidth, targetZ));
            return clamped;
        }

        /// <summary>
        /// Detect if an incoming shot is heading toward the goal.
        /// </summary>
        public bool IsShotDetected(
            float ballX, float ballZ,
            float ballVelocityX, float ballVelocityZ,
            float ballSpeed, float speedThreshold)
        {
            if (ballSpeed < speedThreshold) return false;

            // Check if ball is moving toward goal (dot product of velocity and direction to goal)
            float dirToGoalX = _goalCenterX - ballX;
            float dirToGoalZ = _goalCenterZ - ballZ;
            float dirMag = (float)Math.Sqrt(dirToGoalX * dirToGoalX + dirToGoalZ * dirToGoalZ);

            if (dirMag < 0.01f) return false;

            dirToGoalX /= dirMag;
            dirToGoalZ /= dirMag;

            float velMag = ballSpeed;
            float normVelX = ballVelocityX / velMag;
            float normVelZ = ballVelocityZ / velMag;

            float dot = normVelX * dirToGoalX + normVelZ * dirToGoalZ;

            return dot > 0.5f; // Ball moving at least partially toward goal
        }

        /// <summary>
        /// Predict where the ball will arrive at the goal line X position.
        /// Returns the predicted Z position.
        /// </summary>
        public float PredictBallArrivalZ(
            float ballX, float ballZ,
            float ballVelocityX, float ballVelocityZ)
        {
            if (Math.Abs(ballVelocityX) < 0.01f) return ballZ;

            float timeToGoalLine = (_goalCenterX - ballX) / ballVelocityX;
            if (timeToGoalLine < 0) return ballZ; // Ball moving away

            float predictedZ = ballZ + ballVelocityZ * timeToGoalLine;
            return (float)Math.Max(-_goalAreaHalfWidth, Math.Min(_goalAreaHalfWidth, predictedZ));
        }
    }
}
