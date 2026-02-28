using System;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Handles kick execution synchronized with animation contact frame.
    /// Ball force is applied at the animation contact via OnKickContact().
    /// Pass force ~8, Shoot force ~15.
    /// </summary>
    public class PlayerKicker : MonoBehaviour
    {
        [SerializeField] private float _passForce = 8f;
        [SerializeField] private float _shootForce = 15f;
        [SerializeField] private float _contactFrameTime = 0.08f;

        private KickLogic _logic;
        private BallOwnership _ballOwnership;
        private PlayerAnimator _playerAnimator;

        /// <summary>The underlying kick logic.</summary>
        public KickLogic Logic => _logic;

        /// <summary>Fired when a kick makes contact with the ball.</summary>
        public static event Action OnKickContactEvent;

        private void Awake()
        {
            var config = new KickConfigData
            {
                PassForce = _passForce,
                ShootForce = _shootForce,
                ContactFrameTime = _contactFrameTime
            };
            _logic = new KickLogic(config);
            _playerAnimator = GetComponent<PlayerAnimator>();
        }

        private void Start()
        {
            _ballOwnership = FindFirstObjectByType<BallOwnership>();
        }

        /// <summary>
        /// Initiate a pass kick.
        /// </summary>
        public void Pass()
        {
            if (_ballOwnership == null || !_ballOwnership.IsOwned) return;

            _logic.PrepareKick(
                KickType.Pass,
                transform.position.x, transform.position.z,
                transform.forward.x, transform.forward.z
            );

            if (_playerAnimator != null)
                _playerAnimator.TriggerKick();
        }

        /// <summary>
        /// Initiate a shoot kick.
        /// </summary>
        public void Shoot()
        {
            if (_ballOwnership == null || !_ballOwnership.IsOwned) return;

            _logic.PrepareKick(
                KickType.Shoot,
                transform.position.x, transform.position.z,
                transform.forward.x, transform.forward.z
            );

            if (_playerAnimator != null)
                _playerAnimator.TriggerKick();
        }

        /// <summary>
        /// Called by AnimationEvent at the kick contact frame.
        /// Applies force to the ball and releases ownership.
        /// </summary>
        public void OnKickContact()
        {
            var result = _logic.ExecuteKick();
            if (!result.Applied) return;

            if (_ballOwnership != null)
            {
                // Release ownership before applying force
                _ballOwnership.Release();

                // Apply force to ball
                var ballRb = _ballOwnership.GetComponent<Rigidbody>();
                if (ballRb != null)
                {
                    Vector3 direction = new Vector3(result.DirectionX, 0f, result.DirectionZ);
                    ballRb.AddForce(direction * result.Force, ForceMode.Impulse);
                }
            }

            OnKickContactEvent?.Invoke();
        }
    }
}
