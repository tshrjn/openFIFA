using System;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.AI
{
    /// <summary>
    /// AI player controller using a finite state machine.
    /// States: Idle, ChaseBall, ReturnToPosition.
    /// Uses AIDecisionEngine for state transitions.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class AIController : MonoBehaviour
    {
        [SerializeField] private float _chaseRange = 10f;
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _positionThreshold = 1f;

        private Rigidbody _rigidbody;
        private AIDecisionEngine _decisionEngine;
        private AIConfigData _config;
        private Transform _ballTransform;
        private Vector3 _formationPosition;
        private AIState _currentState;
        private bool _isNearestToBall;

        /// <summary>Current AI state.</summary>
        public AIState CurrentState => _currentState;

        /// <summary>Formation position target.</summary>
        public Vector3 FormationPosition => _formationPosition;

        /// <summary>Fired on state transitions.</summary>
        public event Action<AIState, AIState> OnStateChanged;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();

            if (_config == null)
            {
                _config = new AIConfigData(_chaseRange, _moveSpeed, _positionThreshold);
            }
            _decisionEngine = new AIDecisionEngine(_config);
        }

        /// <summary>
        /// Initialize for tests or runtime.
        /// </summary>
        public void Initialize(AIConfigData config, Transform ballTransform, Vector3 formationPosition)
        {
            _config = config;
            _ballTransform = ballTransform;
            _formationPosition = formationPosition;
            _decisionEngine = new AIDecisionEngine(config);

            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Set whether this AI is the nearest teammate to the ball.
        /// Called by team coordinator or externally.
        /// </summary>
        public void SetAsNearestToBall(bool isNearest)
        {
            _isNearestToBall = isNearest;
        }

        /// <summary>
        /// Update the formation position target.
        /// </summary>
        public void SetFormationPosition(Vector3 position)
        {
            _formationPosition = position;
        }

        /// <summary>
        /// Set the ball reference.
        /// </summary>
        public void SetBallReference(Transform ball)
        {
            _ballTransform = ball;
        }

        private void Update()
        {
            if (_decisionEngine == null || _ballTransform == null) return;

            // Evaluate state
            AIState newState = _decisionEngine.Evaluate(
                aiPositionX: transform.position.x,
                aiPositionZ: transform.position.z,
                ballPositionX: _ballTransform.position.x,
                ballPositionZ: _ballTransform.position.z,
                formationPositionX: _formationPosition.x,
                formationPositionZ: _formationPosition.z,
                isNearestToBall: _isNearestToBall
            );

            if (newState != _currentState)
            {
                var previous = _currentState;
                _currentState = newState;
                OnStateChanged?.Invoke(previous, newState);

                #if UNITY_EDITOR
                Debug.Log($"[AI] {gameObject.name}: {previous} -> {newState}");
                #endif
            }
        }

        private void FixedUpdate()
        {
            if (_rigidbody == null || _config == null) return;

            switch (_currentState)
            {
                case AIState.Idle:
                    // Slow down to stop
                    ApplyDeceleration();
                    break;

                case AIState.ChaseBall:
                    if (_ballTransform != null)
                    {
                        MoveToward(_ballTransform.position, _config.SprintSpeed);
                    }
                    break;

                case AIState.ReturnToPosition:
                    MoveToward(_formationPosition, _config.MoveSpeed);
                    break;
            }
        }

        private void MoveToward(Vector3 target, float speed)
        {
            Vector3 direction = new Vector3(
                target.x - transform.position.x,
                0f,
                target.z - transform.position.z
            );

            if (direction.sqrMagnitude > 0.01f)
            {
                direction.Normalize();
                Vector3 targetVelocity = direction * speed;
                _rigidbody.linearVelocity = new Vector3(
                    targetVelocity.x,
                    _rigidbody.linearVelocity.y,
                    targetVelocity.z
                );
            }
        }

        private void ApplyDeceleration()
        {
            Vector3 horizontal = new Vector3(_rigidbody.linearVelocity.x, 0, _rigidbody.linearVelocity.z);
            if (horizontal.sqrMagnitude > 0.01f)
            {
                horizontal *= 0.9f; // Gradual slowdown
                _rigidbody.linearVelocity = new Vector3(horizontal.x, _rigidbody.linearVelocity.y, horizontal.z);
            }
            else
            {
                _rigidbody.linearVelocity = new Vector3(0, _rigidbody.linearVelocity.y, 0);
            }
        }
    }
}
