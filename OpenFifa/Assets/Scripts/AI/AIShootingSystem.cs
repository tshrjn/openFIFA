using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.AI
{
    /// <summary>
    /// AI shooting system that detects shooting opportunities and executes shots.
    /// </summary>
    public class AIShootingSystem : MonoBehaviour
    {
        [SerializeField] private float _shootRange = 15f;
        [SerializeField] private float _baseShotForce = 12f;
        [SerializeField] private float _shotForceMultiplier = 0.5f;
        [SerializeField] private LayerMask _defenderLayerMask;

        private ShotEvaluator _evaluator;
        private Transform _ballTransform;
        private Rigidbody _ballRigidbody;
        private Vector3 _goalPosition;
        private float _goalHalfWidth;

        /// <summary>Whether a shot opportunity is currently available.</summary>
        public bool HasShotOpportunity { get; private set; }

        /// <summary>Last computed distance to goal.</summary>
        public float DistanceToGoal { get; private set; }

        private void Awake()
        {
            _evaluator = new ShotEvaluator(_shootRange, _baseShotForce, _shotForceMultiplier);
        }

        /// <summary>
        /// Initialize for tests or runtime.
        /// </summary>
        public void Initialize(Transform ball, Vector3 goalPosition, float goalHalfWidth)
        {
            _ballTransform = ball;
            _ballRigidbody = ball != null ? ball.GetComponent<Rigidbody>() : null;
            _goalPosition = goalPosition;
            _goalHalfWidth = goalHalfWidth;
            _evaluator = new ShotEvaluator(_shootRange, _baseShotForce, _shotForceMultiplier);
        }

        /// <summary>
        /// Evaluate whether a shot opportunity exists.
        /// </summary>
        public bool EvaluateShotOpportunity(bool hasPossession)
        {
            if (_ballTransform == null) return false;

            Vector3 toGoal = _goalPosition - transform.position;
            toGoal.y = 0f;
            DistanceToGoal = toGoal.magnitude;

            bool clearLine = CheckClearLine();

            HasShotOpportunity = _evaluator.ShouldShoot(DistanceToGoal, clearLine, hasPossession);
            return HasShotOpportunity;
        }

        /// <summary>
        /// Execute a shot toward the goal.
        /// </summary>
        public bool ExecuteShot()
        {
            if (!HasShotOpportunity || _ballRigidbody == null) return false;

            float targetZ = _evaluator.CalculateShotTargetZ(
                _goalPosition.z, _goalHalfWidth);

            Vector3 targetPoint = new Vector3(
                _goalPosition.x,
                0.5f, // Aim slightly above ground
                targetZ
            );

            Vector3 direction = (targetPoint - _ballTransform.position);
            direction.y = 0.2f; // Slight upward arc
            direction.Normalize();

            float force = _evaluator.CalculateShotForce(DistanceToGoal);

            _ballRigidbody.linearVelocity = Vector3.zero;
            _ballRigidbody.AddForce(direction * force, ForceMode.Impulse);

            HasShotOpportunity = false;
            return true;
        }

        private bool CheckClearLine()
        {
            if (_ballTransform == null) return false;

            Vector3 start = _ballTransform.position + Vector3.up * 0.5f;
            Vector3 end = _goalPosition + Vector3.up * 0.5f;

            // Check for defenders blocking the line
            bool blocked = Physics.Linecast(start, end, _defenderLayerMask);
            return !blocked;
        }
    }
}
