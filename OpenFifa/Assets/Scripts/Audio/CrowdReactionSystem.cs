using UnityEngine;
using UnityEngine.Audio;
using OpenFifa.Core;

namespace OpenFifa.Audio
{
    /// <summary>
    /// Dynamic crowd reaction system. Scales crowd volume based on
    /// ball proximity to goals and plays reaction sounds for near-misses and goals.
    /// </summary>
    public class CrowdReactionSystem : MonoBehaviour
    {
        [SerializeField] private Transform _ball;
        [SerializeField] private float _pitchHalfLength = 25f;
        [SerializeField] private AudioMixer _audioMixer;
        [SerializeField] private string _crowdVolumeParam = "CrowdVolume";
        [SerializeField] private AudioClip _crowdRoarClip;
        [SerializeField] private AudioClip _crowdGroanClip;

        private CrowdReactionLogic _logic;
        private AudioSource _reactionSource;
        private Rigidbody _ballRb;

        /// <summary>Current crowd intensity (0-1).</summary>
        public float CurrentIntensity => _logic != null ? _logic.SmoothedIntensity : 0f;

        /// <summary>The underlying reaction logic.</summary>
        public CrowdReactionLogic Logic => _logic;

        private void Awake()
        {
            _logic = new CrowdReactionLogic(_pitchHalfLength);

            _reactionSource = gameObject.AddComponent<AudioSource>();
            _reactionSource.playOnAwake = false;
        }

        private void Start()
        {
            if (_ball != null)
                _ballRb = _ball.GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            GoalDetector.OnGoalScored += HandleGoalScored;
        }

        private void OnDisable()
        {
            GoalDetector.OnGoalScored -= HandleGoalScored;
        }

        private void Update()
        {
            if (_ball == null || _logic == null) return;

            _logic.UpdateBallPosition(_ball.position.x, _ball.position.z);
            _logic.SmoothUpdate(Time.deltaTime);

            // Update AudioMixer volume
            if (_audioMixer != null)
            {
                _audioMixer.SetFloat(_crowdVolumeParam, _logic.VolumeDe);
            }

            // Check for near-miss
            if (_ballRb != null)
            {
                float speed = _ballRb.linearVelocity.magnitude;
                bool movingTowardGoal = Mathf.Abs(_ballRb.linearVelocity.x) > speed * 0.5f;

                if (_logic.CheckNearMiss(_ball.position.x, _ball.position.z, speed, movingTowardGoal))
                {
                    PlayGroan();
                }
            }
        }

        private void HandleGoalScored(TeamIdentifier team)
        {
            PlayRoar();
        }

        /// <summary>Play crowd roar (goal scored).</summary>
        public void PlayRoar()
        {
            if (_crowdRoarClip != null)
                _reactionSource.PlayOneShot(_crowdRoarClip);
        }

        /// <summary>Play crowd groan (near miss).</summary>
        public void PlayGroan()
        {
            if (_crowdGroanClip != null && !_reactionSource.isPlaying)
                _reactionSource.PlayOneShot(_crowdGroanClip);
        }
    }
}
