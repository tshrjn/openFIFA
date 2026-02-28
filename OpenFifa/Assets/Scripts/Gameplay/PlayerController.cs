using System;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Controls player movement via input.
    /// Supports keyboard (WASD/arrows) via Unity Input System or programmatic input.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerStatsConfig _statsConfig;

        private Rigidbody _rigidbody;
        private PlayerStatsData _stats;
        private Vector2 _moveInput;
        private bool _sprintInput;

        /// <summary>Whether the player is currently sprinting.</summary>
        public bool IsSprinting => _sprintInput && _moveInput.sqrMagnitude > 0.01f;

        /// <summary>Current velocity magnitude in m/s.</summary>
        public float CurrentSpeed => _rigidbody != null ? _rigidbody.linearVelocity.magnitude : 0f;

        /// <summary>Current movement input direction (normalized).</summary>
        public Vector2 MoveInput => _moveInput;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();

            // Apply stats from config or defaults
            if (_statsConfig != null)
            {
                _stats = _statsConfig.ToData();
            }
            else if (_stats == null)
            {
                _stats = new PlayerStatsData();
            }

            ConfigureRigidbody();
        }

        /// <summary>
        /// Initialize with explicit stats data (for tests).
        /// </summary>
        public void Initialize(PlayerStatsData stats)
        {
            _stats = stats;
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            ConfigureRigidbody();
        }

        /// <summary>
        /// Set movement input programmatically.
        /// Used by Input System callbacks or by tests.
        /// Input is expected as Vector2 where x = horizontal, y = vertical.
        /// </summary>
        public void SetMoveInput(Vector2 input)
        {
            _moveInput = input;
        }

        /// <summary>
        /// Set sprint state programmatically.
        /// </summary>
        public void SetSprinting(bool sprinting)
        {
            _sprintInput = sprinting;
        }

        private void FixedUpdate()
        {
            if (_stats == null) return;

            ApplyMovement();
        }

        private void ApplyMovement()
        {
            // Normalize input to prevent diagonal speed boost
            Vector2 normalizedInput = _moveInput.sqrMagnitude > 1f
                ? _moveInput.normalized
                : _moveInput;

            // Convert 2D input to 3D direction (X = horizontal, Z = vertical)
            Vector3 moveDirection = new Vector3(normalizedInput.x, 0f, normalizedInput.y);

            if (moveDirection.sqrMagnitude > 0.01f)
            {
                // Ensure rigidbody is awake when player provides input
                if (_rigidbody.IsSleeping()) _rigidbody.WakeUp();
                // Determine target speed
                float targetSpeed = IsSprinting ? _stats.SprintSpeed : _stats.BaseSpeed;

                // Calculate target velocity
                Vector3 targetVelocity = moveDirection.normalized * targetSpeed;

                // Get current horizontal velocity
                Vector3 currentHorizontalVelocity = new Vector3(
                    _rigidbody.linearVelocity.x, 0f, _rigidbody.linearVelocity.z);

                // Accelerate toward target
                Vector3 velocityDiff = targetVelocity - currentHorizontalVelocity;
                float acceleration = _stats.Acceleration;
                Vector3 force = Vector3.ClampMagnitude(velocityDiff, acceleration * Time.fixedDeltaTime);

                // Apply as velocity change (ignores mass)
                _rigidbody.linearVelocity = new Vector3(
                    currentHorizontalVelocity.x + force.x,
                    _rigidbody.linearVelocity.y,
                    currentHorizontalVelocity.z + force.z);
            }
            else
            {
                // Decelerate when no input
                Vector3 currentHorizontalVelocity = new Vector3(
                    _rigidbody.linearVelocity.x, 0f, _rigidbody.linearVelocity.z);

                if (currentHorizontalVelocity.sqrMagnitude > 0.01f)
                {
                    Vector3 deceleration = -currentHorizontalVelocity.normalized
                        * _stats.Deceleration * Time.fixedDeltaTime;

                    // Don't overshoot past zero
                    if (deceleration.sqrMagnitude > currentHorizontalVelocity.sqrMagnitude)
                    {
                        _rigidbody.linearVelocity = new Vector3(0f, _rigidbody.linearVelocity.y, 0f);
                    }
                    else
                    {
                        _rigidbody.linearVelocity = new Vector3(
                            currentHorizontalVelocity.x + deceleration.x,
                            _rigidbody.linearVelocity.y,
                            currentHorizontalVelocity.z + deceleration.z);
                    }
                }
            }
        }

        private void ConfigureRigidbody()
        {
            if (_rigidbody == null) return;

            // Freeze all rotations to prevent tipping
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX
                                   | RigidbodyConstraints.FreezeRotationY
                                   | RigidbodyConstraints.FreezeRotationZ;

            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.useGravity = true;

            // Zero-friction material so ground contact doesn't resist velocity-based movement
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule != null && capsule.sharedMaterial == null)
            {
                var mat = new PhysicsMaterial("PlayerMovement");
                mat.dynamicFriction = 0f;
                mat.staticFriction = 0f;
                mat.frictionCombine = PhysicsMaterialCombine.Minimum;
                capsule.sharedMaterial = mat;
            }
        }

        // Input System callback methods (called by PlayerInput component)
        public void OnMove(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            _moveInput = context.ReadValue<Vector2>();
        }

        public void OnSprint(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            _sprintInput = context.ReadValueAsButton();
        }
    }
}
