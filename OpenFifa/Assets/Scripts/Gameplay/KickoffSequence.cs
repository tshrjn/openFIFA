using System;
using System.Collections;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Manages the kickoff sequence: ball placement, player positioning, and start delay.
    /// </summary>
    public class KickoffSequence : MonoBehaviour
    {
        [SerializeField] private float _setupDelay = 1f;

        private KickoffLogic _logic;
        private KickoffState _currentState;
        private Transform _ballTransform;
        private Rigidbody _ballRigidbody;

        /// <summary>Current kickoff state.</summary>
        public KickoffState CurrentState => _currentState;

        /// <summary>Fired when kickoff state changes.</summary>
        public event Action<KickoffState> OnKickoffStateChanged;

        /// <summary>Fired when kickoff is complete and play should begin.</summary>
        public event Action OnPlayBegin;

        /// <summary>
        /// Initialize for tests or runtime.
        /// </summary>
        public void Initialize(Transform ball, KickoffLogic logic = null)
        {
            _ballTransform = ball;
            _ballRigidbody = ball != null ? ball.GetComponent<Rigidbody>() : null;
            _logic = logic ?? new KickoffLogic();
        }

        /// <summary>
        /// Start the kickoff sequence as a coroutine.
        /// </summary>
        public void StartKickoff()
        {
            StartCoroutine(KickoffRoutine());
        }

        /// <summary>
        /// Notify that a goal was scored (for alternating kickoff team).
        /// </summary>
        public void NotifyGoalScored(TeamIdentifier scoringTeam)
        {
            _logic?.OnGoalScored(scoringTeam);
        }

        private IEnumerator KickoffRoutine()
        {
            // Step 1: Setting up
            SetState(KickoffState.SettingUp);

            // Place ball at center
            if (_ballRigidbody != null)
            {
                _ballRigidbody.isKinematic = true;
                _ballRigidbody.linearVelocity = Vector3.zero;
                _ballRigidbody.angularVelocity = Vector3.zero;
            }
            if (_ballTransform != null)
            {
                _ballTransform.position = new Vector3(0f, 0.5f, 0f);
            }

            yield return null; // One frame for setup

            // Step 2: Wait for setup delay
            SetState(KickoffState.WaitingForKick);
            yield return new WaitForSeconds(_setupDelay);

            // Step 3: Complete - enable ball physics
            if (_ballRigidbody != null)
            {
                _ballRigidbody.isKinematic = false;
            }

            SetState(KickoffState.Complete);
            OnPlayBegin?.Invoke();
        }

        private void SetState(KickoffState state)
        {
            _currentState = state;
            OnKickoffStateChanged?.Invoke(state);
        }
    }
}
