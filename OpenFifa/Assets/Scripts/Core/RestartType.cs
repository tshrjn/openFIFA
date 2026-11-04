namespace OpenFifa.Core
{
    /// <summary>
    /// How the ball was put back into play. Used by offside logic because
    /// Law 11 exempts goal kicks, throw-ins and corner kicks.
    /// </summary>
    public enum RestartType
    {
        None = 0,
        KickOff = 1,
        GoalKick = 2,
        ThrowIn = 3,
        CornerKick = 4,
        FreeKick = 5,
        PenaltyKick = 6,
        DropBall = 7
    }
}
