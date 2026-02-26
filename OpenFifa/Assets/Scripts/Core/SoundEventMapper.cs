namespace OpenFifa.Core
{
    /// <summary>
    /// Types of sound events that can be triggered.
    /// </summary>
    public enum SoundEventType
    {
        None = 0,
        Whistle = 1,
        Kick = 2,
        GoalCheer = 3,
        CrowdAmbient = 4
    }

    /// <summary>
    /// Pure C# mapping from match state transitions to sound events.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class SoundEventMapper
    {
        /// <summary>
        /// Map a match state change to the appropriate sound event.
        /// </summary>
        /// <param name="oldState">Previous match state.</param>
        /// <param name="newState">New match state.</param>
        /// <returns>The sound event to play, or None if no sound.</returns>
        public SoundEventType MapMatchStateChange(MatchState oldState, MatchState newState)
        {
            // Whistle on match start, halftime, fulltime, and second half start
            if (newState == MatchState.FirstHalf && oldState == MatchState.PreKickoff)
                return SoundEventType.Whistle;

            if (newState == MatchState.HalfTime)
                return SoundEventType.Whistle;

            if (newState == MatchState.SecondHalf && oldState == MatchState.HalfTime)
                return SoundEventType.Whistle;

            if (newState == MatchState.FullTime)
                return SoundEventType.Whistle;

            // Goal cheer on goal celebration
            if (newState == MatchState.GoalCelebration)
                return SoundEventType.GoalCheer;

            return SoundEventType.None;
        }
    }
}
