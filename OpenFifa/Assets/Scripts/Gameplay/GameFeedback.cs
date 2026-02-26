using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Platform-aware game feedback singleton.
    /// iPad: haptic feedback via native plugin.
    /// macOS: screen shake + audio impact.
    /// Graceful fallback on unsupported platforms.
    /// </summary>
    public class GameFeedback : MonoBehaviour
    {
        private static GameFeedback _instance;

        [SerializeField] private GoalCameraShake _cameraShake;
        [SerializeField] private AudioSource _impactAudioSource;

        private FeedbackEventMapper _mapper;

        /// <summary>Singleton instance.</summary>
        public static GameFeedback Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _mapper = new FeedbackEventMapper();
        }

        private void OnEnable()
        {
            GoalDetector.OnGoalScored += HandleGoalScored;
        }

        private void OnDisable()
        {
            GoalDetector.OnGoalScored -= HandleGoalScored;
        }

        private void HandleGoalScored(TeamIdentifier team)
        {
            Trigger(_mapper.MapGoalScored());
        }

        /// <summary>
        /// Trigger feedback for a tackle event.
        /// </summary>
        public void TriggerTackle()
        {
            Trigger(_mapper.MapTackle());
        }

        /// <summary>
        /// Trigger feedback for a whistle event.
        /// </summary>
        public void TriggerWhistle()
        {
            Trigger(_mapper.MapWhistle());
        }

        /// <summary>
        /// Trigger platform-aware feedback at the specified intensity.
        /// </summary>
        public void Trigger(FeedbackIntensity intensity)
        {
#if UNITY_IOS && !UNITY_EDITOR
            TriggerHaptic(intensity);
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
            TriggerScreenShake(intensity);
            TriggerAudioImpact(intensity);
#else
            // Fallback: audio only
            TriggerAudioImpact(intensity);
#endif
        }

        private void TriggerHaptic(FeedbackIntensity intensity)
        {
            // Native iOS haptic via plugin
            // Calls into a .mm native plugin with UIImpactFeedbackGenerator
            // Gracefully does nothing if plugin not available
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                switch (intensity)
                {
                    case FeedbackIntensity.Light:
                        _TriggerHapticLight();
                        break;
                    case FeedbackIntensity.Medium:
                        _TriggerHapticMedium();
                        break;
                    case FeedbackIntensity.Heavy:
                        _TriggerHapticHeavy();
                        break;
                }
#endif
            }
            catch (System.Exception)
            {
                // Graceful fallback — no haptics on this device
            }
        }

        private void TriggerScreenShake(FeedbackIntensity intensity)
        {
            if (_cameraShake == null) return;

            _cameraShake.TriggerShake();
        }

        private void TriggerAudioImpact(FeedbackIntensity intensity)
        {
            if (_impactAudioSource == null) return;

            float volume = intensity == FeedbackIntensity.Heavy ? 1f :
                          intensity == FeedbackIntensity.Medium ? 0.6f : 0.3f;
            _impactAudioSource.volume = volume;
            _impactAudioSource.Play();
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _TriggerHapticLight();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _TriggerHapticMedium();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _TriggerHapticHeavy();
#endif
    }
}
