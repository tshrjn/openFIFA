using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Tackle mechanic with lunge, dispossession, cooldown, and stun.
    /// Integrates TackleLogic (pure C#) with Unity physics.
    /// </summary>
    public class TackleSystem : MonoBehaviour
    {
        [SerializeField] private float _tackleRadius = 1.5f;
        [SerializeField] private float _cooldownDuration = 1.0f;
        [SerializeField] private float _stunDuration = 0.5f;
        [SerializeField] private float _lungeForce = 10f;
        [SerializeField] private float _lungeDuration = 0.3f;
        [SerializeField] private InputActionReference _tackleAction;

        private TackleLogic _logic;
        private Rigidbody _rb;
        private BallOwnership _ballOwnership;

        /// <summary>Whether tackle is on cooldown.</summary>
        public bool IsCoolingDown => _logic != null && _logic.IsCoolingDown;

        /// <summary>Whether the player is currently lunging.</summary>
        public bool IsLunging => _logic != null && _logic.IsLunging;

        /// <summary>Fired on successful tackle. Args: (tackledPlayerId).</summary>
        public event Action<int> OnTackle;

        private void Awake()
        {
            _logic = new TackleLogic(_tackleRadius, _cooldownDuration, _stunDuration);
            _rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            // Find ball ownership in the scene
            _ballOwnership = FindFirstObjectByType<BallOwnership>();
        }

        private void OnEnable()
        {
            if (_tackleAction != null && _tackleAction.action != null)
            {
                _tackleAction.action.Enable();
                _tackleAction.action.performed += OnTacklePerformed;
            }
        }

        private void OnDisable()
        {
            if (_tackleAction != null && _tackleAction.action != null)
            {
                _tackleAction.action.performed -= OnTacklePerformed;
            }
        }

        /// <summary>
        /// Programmatic tackle trigger (for tests and AI).
        /// </summary>
        public void AttemptTackle()
        {
            if (_ballOwnership == null || !_ballOwnership.IsOwned) return;

            Transform ballCarrier = _ballOwnership.CurrentOwnerTransform;
            if (ballCarrier == null) return;

            float distance = Vector3.Distance(transform.position, ballCarrier.position);
            var result = _logic.AttemptTackle(distance, Time.time);

            if (result.DidLunge)
            {
                Vector3 direction = (ballCarrier.position - transform.position).normalized;
                _rb.AddForce(direction * _lungeForce, ForceMode.Impulse);
                StartCoroutine(LungeCoroutine(result, ballCarrier));
            }
        }

        private void OnTacklePerformed(InputAction.CallbackContext ctx)
        {
            AttemptTackle();
        }

        private IEnumerator LungeCoroutine(TackleResult result, Transform ballCarrier)
        {
            yield return new WaitForSeconds(_lungeDuration);

            _logic.CompleteLunge();

            if (result.DidDispossess)
            {
                int tackledId = _ballOwnership.CurrentOwnerId;
                _ballOwnership.Release();

                // Stun the tackled player
                if (ballCarrier != null)
                {
                    var playerController = ballCarrier.GetComponent<PlayerController>();
                    if (playerController != null)
                    {
                        StartCoroutine(StunCoroutine(playerController, result.StunDuration));
                    }
                }

                OnTackle?.Invoke(tackledId);
            }
        }

        private IEnumerator StunCoroutine(PlayerController controller, float duration)
        {
            controller.enabled = false;
            yield return new WaitForSeconds(duration);
            controller.enabled = true;
        }
    }
}
