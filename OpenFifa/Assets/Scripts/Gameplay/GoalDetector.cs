using System;
using System.Collections;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Detects when the ball fully crosses the goal line by entering a trigger volume.
    /// Placed behind each goal line. Fires OnGoalScored event with scoring team.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class GoalDetector : MonoBehaviour
    {
        [SerializeField] private TeamIdentifier _scoringTeam;
        [SerializeField] private float _resetDelay = 2f;

        private BoxCollider _triggerCollider;
        private Transform _ballTransform;
        private bool _goalDetected;

        /// <summary>
        /// Static event fired when a goal is scored.
        /// Payload is the team that scored.
        /// </summary>
        public static event Action<TeamIdentifier> OnGoalScored;

        /// <summary>
        /// Instance event for per-detector subscription.
        /// </summary>
        public event Action<TeamIdentifier> OnGoalDetected;

        private void Awake()
        {
            _triggerCollider = GetComponent<BoxCollider>();
            _triggerCollider.isTrigger = true;
        }

        /// <summary>
        /// Initialize the detector (for tests or runtime setup).
        /// </summary>
        public void Initialize(TeamIdentifier scoringTeam)
        {
            _scoringTeam = scoringTeam;
        }

        /// <summary>
        /// Set the ball reference for reset after goal.
        /// </summary>
        public void SetBallReference(Transform ballTransform)
        {
            _ballTransform = ballTransform;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_goalDetected) return;

            if (other.CompareTag("Ball"))
            {
                _goalDetected = true;

                // Fire events
                OnGoalScored?.Invoke(_scoringTeam);
                OnGoalDetected?.Invoke(_scoringTeam);

                // If we have a ball reference, reset it
                if (_ballTransform != null)
                {
                    StartCoroutine(ResetBallAfterDelay());
                }
                else
                {
                    // Try to find ball by tag
                    var ball = other.transform;
                    if (ball != null)
                    {
                        _ballTransform = ball;
                        StartCoroutine(ResetBallAfterDelay());
                    }
                }
            }
        }

        private IEnumerator ResetBallAfterDelay()
        {
            yield return new WaitForSeconds(_resetDelay);

            if (_ballTransform != null)
            {
                var rb = _ballTransform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                _ballTransform.position = new Vector3(0f, 0.5f, 0f);
            }

            // Allow new goal detection after reset
            _goalDetected = false;
        }

        /// <summary>
        /// Reset goal detection state (for testing).
        /// </summary>
        public void ResetDetection()
        {
            _goalDetected = false;
        }
    }
}
