using System;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Controls ball physics behavior and state management.
    /// Applies configuration from BallPhysicsConfig ScriptableObject.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class BallController : MonoBehaviour
    {
        [SerializeField] private BallPhysicsConfig _physicsConfig;

        private Rigidbody _rigidbody;
        private SphereCollider _sphereCollider;
        private BallState _currentState;

        /// <summary>Current state of the ball.</summary>
        public BallState CurrentState
        {
            get => _currentState;
            private set
            {
                if (_currentState != value)
                {
                    var previous = _currentState;
                    _currentState = value;
                    OnStateChanged?.Invoke(previous, value);
                }
            }
        }

        /// <summary>Current velocity magnitude in m/s.</summary>
        public float Speed => _rigidbody != null ? _rigidbody.linearVelocity.magnitude : 0f;

        /// <summary>Current velocity vector.</summary>
        public Vector3 Velocity => _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;

        /// <summary>Whether the ball is currently on the ground.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>Fired when ball state changes. Args: (previousState, newState)</summary>
        public event Action<BallState, BallState> OnStateChanged;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _sphereCollider = GetComponent<SphereCollider>();

            ApplyPhysicsConfig();
        }

        /// <summary>
        /// Applies physics configuration from ScriptableObject or default values.
        /// </summary>
        public void ApplyPhysicsConfig()
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_sphereCollider == null) _sphereCollider = GetComponent<SphereCollider>();

            BallPhysicsData data = _physicsConfig != null
                ? _physicsConfig.ToData()
                : new BallPhysicsData();

            ApplyPhysicsData(data);
        }

        /// <summary>
        /// Applies physics data directly. Useful for tests without ScriptableObject.
        /// </summary>
        public void ApplyPhysicsData(BallPhysicsData data)
        {
            // Configure Rigidbody
            _rigidbody.mass = data.Mass;
            _rigidbody.linearDamping = data.Drag;
            _rigidbody.angularDamping = data.AngularDrag;
            _rigidbody.useGravity = true;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Configure PhysicsMaterial
            var physicsMaterial = new PhysicsMaterial("BallPhysicsMaterial");
            physicsMaterial.bounciness = data.Bounciness;
            physicsMaterial.dynamicFriction = data.DynamicFriction;
            physicsMaterial.staticFriction = data.StaticFriction;
            physicsMaterial.bounceCombine = PhysicsMaterialCombine.Average;
            physicsMaterial.frictionCombine = PhysicsMaterialCombine.Average;
            _sphereCollider.sharedMaterial = physicsMaterial;

            // Set sphere collider size (standard soccer ball radius ~0.11m)
            _sphereCollider.radius = 0.11f;
        }

        /// <summary>
        /// Sets the ball to free state (not possessed, not in flight).
        /// </summary>
        public void SetFree()
        {
            CurrentState = BallState.Free;
        }

        /// <summary>
        /// Sets the ball to possessed state.
        /// </summary>
        public void SetPossessed()
        {
            CurrentState = BallState.Possessed;
        }

        /// <summary>
        /// Kicks the ball with the given force direction and magnitude.
        /// </summary>
        public void Kick(Vector3 force, ForceMode mode = ForceMode.Impulse)
        {
            CurrentState = BallState.InFlight;
            _rigidbody.AddForce(force, mode);
        }

        /// <summary>
        /// Resets the ball to a specified position with zero velocity.
        /// </summary>
        public void ResetToPosition(Vector3 position)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            transform.position = position;
            CurrentState = BallState.Free;
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Check if grounded
            foreach (var contact in collision.contacts)
            {
                if (contact.normal.y > 0.5f)
                {
                    IsGrounded = true;
                    break;
                }
            }

            // Transition from InFlight to Free on ground contact
            if (CurrentState == BallState.InFlight && IsGrounded)
            {
                CurrentState = BallState.Free;
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            IsGrounded = false;
        }
    }
}
