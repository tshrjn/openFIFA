using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# AI decision logic.
    /// Evaluates current game state and returns the appropriate AI state.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class AIDecisionEngine
    {
        private readonly AIConfigData _config;

        public AIDecisionEngine(AIConfigData config)
        {
            _config = config;
        }

        /// <summary>
        /// Evaluate the AI state based on current positions.
        /// </summary>
        /// <param name="aiPositionX">AI player X position</param>
        /// <param name="aiPositionZ">AI player Z position</param>
        /// <param name="ballPositionX">Ball X position</param>
        /// <param name="ballPositionZ">Ball Z position</param>
        /// <param name="formationPositionX">Formation target X position</param>
        /// <param name="formationPositionZ">Formation target Z position</param>
        /// <param name="isNearestToBall">Whether this AI is the nearest teammate to the ball</param>
        /// <returns>The recommended AI state</returns>
        public AIState Evaluate(
            float aiPositionX, float aiPositionZ,
            float ballPositionX, float ballPositionZ,
            float formationPositionX, float formationPositionZ,
            bool isNearestToBall)
        {
            float distanceToBall = Distance(aiPositionX, aiPositionZ, ballPositionX, ballPositionZ);
            float distanceToFormation = Distance(aiPositionX, aiPositionZ, formationPositionX, formationPositionZ);

            // If nearest to ball and ball is in chase range -> chase
            if (isNearestToBall && distanceToBall <= _config.ChaseRange)
            {
                return AIState.ChaseBall;
            }

            // If at formation position and ball is far -> idle
            if (distanceToFormation <= _config.PositionThreshold && distanceToBall > _config.ChaseRange)
            {
                return AIState.Idle;
            }

            // Otherwise -> return to position
            return AIState.ReturnToPosition;
        }

        private static float Distance(float x1, float z1, float x2, float z2)
        {
            float dx = x2 - x1;
            float dz = z2 - z1;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
