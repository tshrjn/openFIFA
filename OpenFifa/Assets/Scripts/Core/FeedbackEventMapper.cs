namespace OpenFifa.Core
{
    /// <summary>
    /// Feedback intensity levels for haptics and screen effects.
    /// </summary>
    public enum FeedbackIntensity
    {
        Light = 0,
        Medium = 1,
        Heavy = 2
    }

    /// <summary>
    /// Platform feedback types.
    /// </summary>
    public enum PlatformFeedbackType
    {
        Haptic = 0,     // iPad
        ScreenShake = 1, // macOS
        Audio = 2        // Both
    }

    /// <summary>
    /// Pure C# mapping from game events to feedback intensity.
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
    }
}
