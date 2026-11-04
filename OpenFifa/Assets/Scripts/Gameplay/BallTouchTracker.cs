using System;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Turns ball ownership changes and player-ball collisions into touch events with
    /// team information (BallOwnership alone reports per-team indices, which collide
    /// across teams). Attach to the Ball next to BallOwnership.
    ///
    /// - Gaining possession: deliberate touch by the new owner.
    /// - Losing possession (pass, shot, dispossession): deliberate play by the previous owner.
    /// - Bumping into the ball without owning it: deflection.
    /// </summary>
    [RequireComponent(typeof(BallOwnership))]
    public class BallTouchTracker : MonoBehaviour
    {
        [Tooltip("Ignore repeated deflections by the same player within this many seconds.")]
        [SerializeField] private float _deflectionDebounce = 0.2f;

        private BallOwnership _ownership;
        private PlayerIdentity _currentOwner;
        private PlayerIdentity _lastDeflector;
        private float _lastDeflectionTime = -10f;
        private RestartType _pendingRestart = RestartType.None;

        /// <summary>Fired for every touch. Args: (player, kind, restart type of this touch).</summary>
        public event Action<PlayerIdentity, BallTouchKind, RestartType> OnBallTouched;

        /// <summary>Last player to touch the ball, or null.</summary>
        public PlayerIdentity LastToucher { get; private set; }

        private void Awake()
        {
            _ownership = GetComponent<BallOwnership>();
        }

        private void OnEnable()
        {
            if (_ownership == null) _ownership = GetComponent<BallOwnership>();
            if (_ownership != null) _ownership.OnOwnerChanged += HandleOwnerChanged;
        }

        private void OnDisable()
        {
            if (_ownership != null) _ownership.OnOwnerChanged -= HandleOwnerChanged;
        }

        /// <summary>
        /// Mark the next deliberate touch as a restart (e.g. a free kick or throw-in).
        /// </summary>
        public void SetPendingRestart(RestartType restart)
        {
            _pendingRestart = restart;
        }

        /// <summary>Forget possession history (e.g. after the ball is reset for a restart).</summary>
        public void ClearHistory()
        {
            _currentOwner = null;
            _lastDeflector = null;
            LastToucher = null;
        }

        private void HandleOwnerChanged(int previousOwnerId, int newOwnerId)
        {
            // Previous owner released the ball: they played it (pass/shot) at this instant.
            if (_currentOwner != null && (newOwnerId < 0 || !IsSame(_currentOwner, _ownership.CurrentOwnerTransform)))
            {
                var releaser = _currentOwner;
                _currentOwner = null;
                Emit(releaser, BallTouchKind.Deliberate);
            }

            if (newOwnerId >= 0 && _ownership.CurrentOwnerTransform != null)
            {
                var identity = _ownership.CurrentOwnerTransform.GetComponent<PlayerIdentity>();
                if (identity != null)
                {
                    _currentOwner = identity;
                    Emit(identity, BallTouchKind.Deliberate);
                }
            }
        }

        private void FixedUpdate()
        {
            // BallOwnership suppresses change events when two players share a per-team id
            // (e.g. TeamA #1 -> TeamB #1), so also detect owner changes by transform.
            if (_ownership == null) return;
            Transform owner = _ownership.IsOwned ? _ownership.CurrentOwnerTransform : null;
            Transform tracked = _currentOwner != null ? _currentOwner.transform : null;
            if (owner != tracked)
                HandleOwnerChanged(-1, owner != null ? _ownership.CurrentOwnerId : -1);
        }

        private void OnCollisionEnter(Collision collision)
        {
            var identity = collision.collider.GetComponentInParent<PlayerIdentity>();
            if (identity == null || identity == _currentOwner) return;

            if (identity == _lastDeflector && Time.time - _lastDeflectionTime < _deflectionDebounce) return;

            _lastDeflector = identity;
            _lastDeflectionTime = Time.time;
            Emit(identity, BallTouchKind.Deflection);
        }

        private void Emit(PlayerIdentity player, BallTouchKind kind)
        {
            RestartType restart = RestartType.None;
            if (kind == BallTouchKind.Deliberate && _pendingRestart != RestartType.None)
            {
                restart = _pendingRestart;
                _pendingRestart = RestartType.None;
            }

            LastToucher = player;
            OnBallTouched?.Invoke(player, kind, restart);
        }

        private static bool IsSame(PlayerIdentity identity, Transform t)
        {
            return t != null && identity != null && identity.transform == t;
        }
    }
}
