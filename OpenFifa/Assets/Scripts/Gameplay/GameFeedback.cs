using UnityEngine;
using UnityEngine.InputSystem;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Game feedback system with controller rumble, screen shake, and audio impact.
    /// Supports Xbox-like gamepad rumble motors + camera shake + audio feedback.
    /// Graceful fallback on platforms/devices without rumble support.
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
        /// Trigger feedback at the specified intensity.
        /// Dispatches to: controller rumble + screen shake + audio impact.
        /// </summary>
        public void Trigger(FeedbackIntensity intensity)
        {
            TriggerControllerRumble(intensity);
            TriggerScreenShake(intensity);
            TriggerAudioImpact(intensity);
        }

        private void TriggerControllerRumble(FeedbackIntensity intensity)
        {
            var rumbleConfig = _mapper.GetRumbleConfig(intensity);

            // Find all connected gamepads and apply rumble
            try
            {
                if (Gamepad.current != null)
                {
                    Gamepad.current.SetMotorSpeeds(rumbleConfig.LowFrequency, rumbleConfig.HighFrequency);

                    // Schedule rumble stop after duration
                    if (rumbleConfig.Duration > 0f)
                    {
                        CancelInvoke(nameof(StopRumble));
                        Invoke(nameof(StopRumble), rumbleConfig.Duration);
                    }
                }
            }
            catch (System.Exception)
            {
                // Graceful fallback — no rumble on this device/platform
            }
        }

        private void StopRumble()
        {
            try
            {
                if (Gamepad.current != null)
                {
                    Gamepad.current.SetMotorSpeeds(0f, 0f);
                }
            }
            catch (System.Exception)
            {
                // Graceful fallback
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
    }
}
