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
        PlayResumed,
        /// <summary>A shot is struck (before the outcome is known): "{name} shoots!". Cut or followed by the resolution.</summary>
        ShotTaken,
        /// <summary>A player receives the ball in open play: short name calls that carry the build-up.</summary>
        OnTheBall,
        /// <summary>Synthesised by the engine after a quiet spell in live play: colour and match-situation chatter.</summary>
        Lull
    }
}
