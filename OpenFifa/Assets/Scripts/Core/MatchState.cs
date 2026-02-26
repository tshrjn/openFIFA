namespace OpenFifa.Core
{
    /// <summary>
    /// All possible states of the match state machine.
    /// </summary>
    public enum MatchState
    {
        PreKickoff = 0,
        FirstHalf = 1,
        HalfTime = 2,
        SecondHalf = 3,
        FullTime = 4,
        GoalCelebration = 5,
        Paused = 6
    }
}
