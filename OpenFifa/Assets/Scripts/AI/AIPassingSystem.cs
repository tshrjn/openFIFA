using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.AI
{
    /// <summary>
    /// AI passing system that evaluates teammate openness and executes passes.
    /// </summary>
    public class AIPassingSystem : MonoBehaviour
    {
        [SerializeField] private float _passForceMultiplier = 0.5f;
        [SerializeField] private float _minPassForce = 4f;
        [SerializeField] private float _maxPassForce = 20f;
        [SerializeField] private float _evaluationInterval = 0.5f;

        private PassEvaluator _evaluator;
        private Transform _ballTransform;
        private Rigidbody _ballRigidbody;
        private Transform[] _teammates;
        private Transform[] _opponents;
        private float _lastEvaluationTime;

        /// <summary>Last selected pass target (index into teammates array). -1 if none.</summary>
        public int LastSelectedTargetIndex { get; private set; } = -1;

        /// <summary>Last selected pass target transform.</summary>
        public Transform LastSelectedTarget => LastSelectedTargetIndex >= 0 && LastSelectedTargetIndex < _teammates.Length
            ? _teammates[LastSelectedTargetIndex]
            : null;

        private void Awake()
        {
            _evaluator = new PassEvaluator(_minPassForce, _maxPassForce, _passForceMultiplier);
        }

        /// <summary>
        /// Initialize for tests or runtime.
        /// </summary>
        public void Initialize(Transform ball, Transform[] teammates, Transform[] opponents)
        {
            _ballTransform = ball;
            _ballRigidbody = ball != null ? ball.GetComponent<Rigidbody>() : null;
            _teammates = teammates;
            _opponents = opponents;
            _evaluator = new PassEvaluator(_minPassForce, _maxPassForce, _passForceMultiplier);
        }

        /// <summary>
        /// Evaluate and find the best pass target.
        /// Returns the index of the best teammate.
        /// </summary>
        public int EvaluatePassTarget()
        {
            if (_teammates == null || _teammates.Length == 0) return -1;

            var teammatePositions = new PositionData[_teammates.Length];
            for (int i = 0; i < _teammates.Length; i++)
            {
                if (_teammates[i] != null)
                {
                    teammatePositions[i] = new PositionData(
                        i, _teammates[i].position.x, _teammates[i].position.z);
                }
            }

            var opponentPositions = _opponents != null
                ? new PositionData[_opponents.Length]
                : new PositionData[0];
            if (_opponents != null)
            {
                for (int i = 0; i < _opponents.Length; i++)
                {
                    if (_opponents[i] != null)
                    {
                        opponentPositions[i] = new PositionData(
                            i, _opponents[i].position.x, _opponents[i].position.z);
                    }
                }
            }

            LastSelectedTargetIndex = _evaluator.FindMostOpenTeammate(teammatePositions, opponentPositions);
            return LastSelectedTargetIndex;
        }

        /// <summary>
        /// Execute a pass to the last evaluated target.
        /// Returns true if pass was executed.
        /// </summary>
        public bool ExecutePass()
        {
            if (LastSelectedTargetIndex < 0 || _ballRigidbody == null) return false;
            if (LastSelectedTargetIndex >= _teammates.Length) return false;

            Transform target = _teammates[LastSelectedTargetIndex];
            if (target == null) return false;

            Vector3 direction = (target.position - _ballTransform.position);
            direction.y = 0f; // Keep pass on ground plane
            float distance = direction.magnitude;

            if (distance < 0.1f) return false;

            direction.Normalize();
            float force = _evaluator.CalculatePassForce(distance);

            _ballRigidbody.linearVelocity = Vector3.zero;
            _ballRigidbody.AddForce(direction * force, ForceMode.Impulse);

            return true;
        }

        /// <summary>
        /// Evaluate and execute pass in one call.
        /// </summary>
        public bool AttemptPass()
        {
            EvaluatePassTarget();
            return ExecutePass();
        }
    }
}
