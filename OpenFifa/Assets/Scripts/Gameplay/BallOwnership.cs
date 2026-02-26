using System;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Tracks ball ownership at runtime. Attaches to the Ball GameObject.
    /// When possessed, ball follows the owner at a fixed offset near their feet.
    /// Loose balls can be claimed by the nearest player within proximity.
    /// </summary>
    public class BallOwnership : MonoBehaviour
    {
        [SerializeField] private float _claimRadius = 1f;
        [SerializeField] private Vector3 _possessionOffset = new Vector3(0f, 0.1f, 0.5f);
        [SerializeField] private LayerMask _playerLayerMask;

        private BallOwnershipLogic _logic;
        private Rigidbody _rb;
        private Transform _currentOwnerTransform;

        /// <summary>The underlying pure C# logic.</summary>
        public BallOwnershipLogic Logic => _logic;

        /// <summary>Current owner player ID, or -1 for loose ball.</summary>
        public int CurrentOwnerId => _logic.CurrentOwnerId;

        /// <summary>Whether the ball is currently owned.</summary>
        public bool IsOwned => _logic.IsOwned;

        /// <summary>Transform of the current owner, or null.</summary>
        public Transform CurrentOwnerTransform => _currentOwnerTransform;

        /// <summary>Fired when ownership changes. Args: (previousOwnerId, newOwnerId).</summary>
        public event Action<int, int> OnOwnerChanged;

        private void Awake()
        {
            _logic = new BallOwnershipLogic();
            _rb = GetComponent<Rigidbody>();
            _logic.OnOwnerChanged += HandleOwnerChanged;
        }

        private void OnDestroy()
        {
            if (_logic != null)
                _logic.OnOwnerChanged -= HandleOwnerChanged;
        }

        private void FixedUpdate()
        {
            if (_logic.IsOwned && _currentOwnerTransform != null)
            {
                // Ball follows owner at offset
                Vector3 targetPos = _currentOwnerTransform.position +
                    _currentOwnerTransform.forward * _possessionOffset.z +
                    Vector3.up * _possessionOffset.y;
                _rb.MovePosition(targetPos);
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            else if (!_logic.IsOwned)
            {
                // Check for nearby players who can claim
                TryClaimByNearestPlayer();
            }
        }

        /// <summary>
        /// Set the owner externally (e.g., after receiving a pass).
        /// </summary>
        public void SetOwner(Transform ownerTransform, int playerId)
        {
            _currentOwnerTransform = ownerTransform;
            _logic.SetOwner(playerId);
        }

        /// <summary>
        /// Release ownership. Ball becomes loose.
        /// </summary>
        public void Release()
        {
            _currentOwnerTransform = null;
            _logic.Release();
        }

        /// <summary>
        /// Transfer ownership to a new player.
        /// </summary>
        public void TransferTo(Transform newOwnerTransform, int newPlayerId)
        {
            _currentOwnerTransform = newOwnerTransform;
            _logic.Transfer(newPlayerId);
        }

        private void TryClaimByNearestPlayer()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _claimRadius, _playerLayerMask);
            if (hits.Length == 0) return;

            // Find nearest
            float nearestDist = float.MaxValue;
            Collider nearestCollider = null;

            for (int i = 0; i < hits.Length; i++)
            {
                float dist = Vector3.Distance(transform.position, hits[i].transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestCollider = hits[i];
                }
            }

            if (nearestCollider != null)
            {
                // Try to get a player identity component for the ID
                var identity = nearestCollider.GetComponent<PlayerIdentity>();
                int playerId = identity != null ? identity.PlayerId : nearestCollider.GetInstanceID();
                _currentOwnerTransform = nearestCollider.transform;
                _logic.SetOwner(playerId);
            }
        }

        private void HandleOwnerChanged(int previousOwnerId, int newOwnerId)
        {
            OnOwnerChanged?.Invoke(previousOwnerId, newOwnerId);
        }
    }
}
