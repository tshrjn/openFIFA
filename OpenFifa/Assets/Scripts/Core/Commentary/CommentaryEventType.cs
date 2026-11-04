namespace OpenFifa.Core
{
    /// <summary>
    /// Match events the commentary engine understands. The line bank's "event" field uses these names verbatim.
    /// </summary>
    public enum CommentaryEventType
    {
        KickOff = 0,
        PassSequence,
        ThroughBall,
        ShotSaved,
        ShotWide,
        ShotPost,
        Goal,
        Tackle,
        Foul,
        YellowCard,
        RedCard,
        Corner,
        Offside,
        HalfTime,
        FullTime,
        /// <summary>Restart after a stoppage (throw-in, free kick, goal kick taken). Ends analyst windows; has no lines.</summary>
        PlayResumed
    }
}
