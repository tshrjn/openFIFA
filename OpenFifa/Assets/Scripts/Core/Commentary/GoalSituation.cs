namespace OpenFifa.Core
{
    /// <summary>How a goal was scored. Matches the bank's "situation" field (snake_case of the name).</summary>
    public enum GoalSituation
    {
        OpenPlay = 0,
        Header,
        LongRange,
        Penalty,
        FreeKick,
        Counter,
        OwnGoal,
        TapIn
    }
}
