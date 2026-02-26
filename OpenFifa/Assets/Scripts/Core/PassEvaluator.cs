using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# pass evaluation logic.
    /// Evaluates teammate openness and calculates pass force.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class PassEvaluator
    {
        private readonly float _minPassForce;
        private readonly float _maxPassForce;
        private readonly float _passForceMultiplier;

        public PassEvaluator(
            float minPassForce = 4f,
            float maxPassForce = 20f,
            float passForceMultiplier = 0.5f)
        {
            _minPassForce = minPassForce;
            _maxPassForce = maxPassForce;
            _passForceMultiplier = passForceMultiplier;
        }

        /// <summary>
        /// Find the most open teammate (greatest distance to nearest opponent).
        /// Returns the index of the best pass target, or -1 if no teammates.
        /// </summary>
        public int FindMostOpenTeammate(PositionData[] teammates, PositionData[] opponents)
        {
            if (teammates == null || teammates.Length == 0) return -1;

            int bestIndex = -1;
            float bestOpenness = -1f;

            for (int i = 0; i < teammates.Length; i++)
            {
                float openness = CalculateOpenness(teammates[i], opponents);
                if (openness > bestOpenness)
                {
                    bestOpenness = openness;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// Calculate openness score for a position.
        /// Openness = distance to nearest opponent.
        /// </summary>
        public float CalculateOpenness(PositionData position, PositionData[] opponents)
        {
            if (opponents == null || opponents.Length == 0) return float.MaxValue;

            float minDist = float.MaxValue;
            for (int i = 0; i < opponents.Length; i++)
            {
                float dist = Distance(position.X, position.Z, opponents[i].X, opponents[i].Z);
                if (dist < minDist) minDist = dist;
            }
            return minDist;
        }

        /// <summary>
        /// Calculate pass force based on distance to target.
        /// Further targets get more force.
        /// </summary>
        public float CalculatePassForce(float distance)
        {
            float force = _minPassForce + distance * _passForceMultiplier;
            return Math.Min(force, _maxPassForce);
        }

        private static float Distance(float x1, float z1, float x2, float z2)
        {
            float dx = x2 - x1;
            float dz = z2 - z1;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
