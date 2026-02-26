using System;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.AI
{
    /// <summary>
    /// Goalkeeper AI with positioning, shot detection, and diving.
    /// States: Positioning, Diving, Recovering.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GoalkeeperAI : MonoBehaviour
    {
        [SerializeField] private float _goalAreaWidth = 5f;
        [SerializeField] private float _diveSpeed = 15f;
        [SerializeField] private float _positioningSpeed = 5f;
        [SerializeField] private float _recoveryTime = 2f;
        [SerializeField] private float _shotSpeedThreshold = 5f;

        private Rigidbody _rigidbody;
        private GoalkeeperLogic _logic;
        private GoalkeeperState _currentState;
        private Transform _ballTransform;
        private Rigidbody _ballRigidbody;
        private Vector3 _goalCenter;
        private Vector3 _diveTarget;
        private float _recoveryTimer;

        /// <summary>Current goalkeeper state.</summary>
        public GoalkeeperState CurrentState => _currentState;

        /// <summary>Fired on state transitions.</summary>
        public event Action<GoalkeeperState, GoalkeeperState> OnStateChanged;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Initialize for tests or runtime.
        /// </summary>
        public void Initialize(Transform ball, Vector3 goalCenter, float goalAreaWidth)
        {
            _ballTransform = ball;
            _ballRigidbody = ball != null ? ball.GetComponent<Rigidbody>() : null;
            _goalCenter = goalCenter;
            _goalAreaWidth = goalAreaWidth;
            _logic = new GoalkeeperLogic(goalAreaWidth, goalCenter.x, goalCenter.z);
            _currentState = GoalkeeperState.Positioning;

            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (_logic == null || _ballTransform == null) return;

            switch (_currentState)
            {
                case GoalkeeperState.Positioning:
                    UpdatePositioning();
                    break;
                case GoalkeeperState.Diving:
                    UpdateDiving();
                    break;
                case GoalkeeperState.Recovering:
                    UpdateRecovering();
                    break;
            }
        }

        private void UpdatePositioning()
        {
            // Position laterally based on ball
            float targetZ = _logic.CalculateLateralPosition(_ballTransform.position.z);
            Vector3 targetPos = new Vector3(_goalCenter.x, transform.position.y, targetZ);

            Vector3 moveDir = (targetPos - transform.position);
            moveDir.y = 0;

            if (moveDir.sqrMagnitude > 0.01f)
            {
                Vector3 velocity = moveDir.normalized * _positioningSpeed;
                _rigidbody.linearVelocity = new Vector3(velocity.x, _rigidbody.linearVelocity.y, velocity.z);
            }
            else
            {
                _rigidbody.linearVelocity = new Vector3(0, _rigidbody.linearVelocity.y, 0);
            }

            // Check for incoming shot
            if (_ballRigidbody != null)
            {
                bool shotDetected = _logic.IsShotDetected(
                    _ballTransform.position.x, _ballTransform.position.z,
                    _ballRigidbody.linearVelocity.x, _ballRigidbody.linearVelocity.z,
                    _ballRigidbody.linearVelocity.magnitude,
                    _shotSpeedThreshold
                );

                if (shotDetected)
                {
                    float predictedZ = _logic.PredictBallArrivalZ(
                        _ballTransform.position.x, _ballTransform.position.z,
                        _ballRigidbody.linearVelocity.x, _ballRigidbody.linearVelocity.z
                    );

                    _diveTarget = new Vector3(_goalCenter.x, transform.position.y, predictedZ);
                    SetState(GoalkeeperState.Diving);
                }
            }
        }

        private void UpdateDiving()
        {
            Vector3 direction = (_diveTarget - transform.position);
            direction.y = 0;

            if (direction.sqrMagnitude > 0.1f)
            {
                Vector3 velocity = direction.normalized * _diveSpeed;
                _rigidbody.linearVelocity = new Vector3(velocity.x, _rigidbody.linearVelocity.y, velocity.z);
            }
            else
            {
                // Reached dive target, start recovering
                _rigidbody.linearVelocity = new Vector3(0, _rigidbody.linearVelocity.y, 0);
                _recoveryTimer = _recoveryTime;
                SetState(GoalkeeperState.Recovering);
            }
        }

        private void UpdateRecovering()
        {
            _recoveryTimer -= Time.fixedDeltaTime;

            // Move back toward center
            Vector3 targetPos = _goalCenter;
            targetPos.y = transform.position.y;
            Vector3 direction = (targetPos - transform.position);
            direction.y = 0;

            if (direction.sqrMagnitude > 0.1f)
            {
                float speed = _positioningSpeed * 0.5f; // Slower recovery
                Vector3 velocity = direction.normalized * speed;
                _rigidbody.linearVelocity = new Vector3(velocity.x, _rigidbody.linearVelocity.y, velocity.z);
            }

            if (_recoveryTimer <= 0f)
            {
                SetState(GoalkeeperState.Positioning);
            }
        }

        private void SetState(GoalkeeperState newState)
        {
            if (_currentState != newState)
            {
                var previous = _currentState;
                _currentState = newState;
                OnStateChanged?.Invoke(previous, newState);
            }
        }
    }
}
