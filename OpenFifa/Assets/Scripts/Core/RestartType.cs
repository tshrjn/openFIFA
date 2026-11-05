namespace OpenFifa.Core
{
    /// <summary>
    /// How the ball was (or will be) put back into play. Shared by the match sim (pending restarts, the
    /// RestartTaken event's Detail) and the offside presentation path (<see cref="BallTouch"/>,
    /// <see cref="OffsideLogic"/>). Law 11.3 exempts goal kicks, throw-ins and corners from offside
    /// (<see cref="OffsideRule.AppliesToRestart"/>).
    /// The numeric values are part of the sim's state hash and of MatchEvent.Detail: do not renumber.
    /// </summary>
    public enum RestartType
    {
        None = 0,
        KickOff = 1,
        ThrowIn = 2,
        GoalKick = 3,
        Corner = 4,
        DirectFreeKick = 5,
        IndirectFreeKick = 6,
        Penalty = 7,
        DropBall = 8,
    }
}
