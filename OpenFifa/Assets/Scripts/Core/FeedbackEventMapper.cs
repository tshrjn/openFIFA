namespace OpenFifa.Core
{
    /// <summary>
    /// Feedback intensity levels for controller rumble and screen effects.
    /// </summary>
    public enum FeedbackIntensity
    {
        Light = 0,
        Medium = 1,
        Heavy = 2
    }

    /// <summary>
    /// Feedback channel types.
    /// </summary>
    public enum FeedbackChannelType
    {
        ControllerRumble = 0,  // Xbox/gamepad rumble motors
        ScreenShake = 1,       // Camera shake effect
        Audio = 2              // Audio impact SFX
    }

    /// <summary>
    /// Controller rumble motor configuration.
    /// Xbox controllers have two motors: low-frequency (left) and high-frequency (right).
    /// </summary>
    public class RumbleConfig
    {
        /// <summary>Low-frequency motor intensity (0-1). Heavy, deep vibration.</summary>
        public float LowFrequency { get; }

        /// <summary>High-frequency motor intensity (0-1). Light, buzzy vibration.</summary>
        public float HighFrequency { get; }

        /// <summary>Duration in seconds.</summary>
        public float Duration { get; }

        public RumbleConfig(float lowFrequency, float highFrequency, float duration)
        {
            LowFrequency = lowFrequency;
            HighFrequency = highFrequency;
            Duration = duration;
        }
    }

    /// <summary>
    /// Pure C# mapping from game events to feedback intensity and rumble configuration.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class FeedbackEventMapper
    {
        /// <summary>Goal scored: heavy feedback.</summary>
        public FeedbackIntensity MapGoalScored() => FeedbackIntensity.Heavy;

        /// <summary>Successful tackle: medium feedback.</summary>
        public FeedbackIntensity MapTackle() => FeedbackIntensity.Medium;

        /// <summary>Whistle event: light feedback.</summary>
        public FeedbackIntensity MapWhistle() => FeedbackIntensity.Light;

        /// <summary>
        /// Get rumble motor configuration for a given intensity.
        /// Xbox controllers: left motor = low frequency, right motor = high frequency.
        /// </summary>
        public RumbleConfig GetRumbleConfig(FeedbackIntensity intensity)
        {
            switch (intensity)
            {
                case FeedbackIntensity.Heavy:
                    return new RumbleConfig(1.0f, 0.8f, 0.5f);
                case FeedbackIntensity.Medium:
                    return new RumbleConfig(0.5f, 0.4f, 0.3f);
                case FeedbackIntensity.Light:
                    return new RumbleConfig(0.2f, 0.1f, 0.15f);
                default:
                    return new RumbleConfig(0f, 0f, 0f);
            }
        }
    }
}
