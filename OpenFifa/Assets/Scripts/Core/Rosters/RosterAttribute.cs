namespace OpenFifa.Core
{
    /// <summary>
    /// OpenFifa's own player attributes, each rated 1-20 (see docs/ROSTER_FORMAT.md).
    /// The numeric order is part of the format: JSON keys come from <see cref="RosterSchema.AttributeKeys"/>.
    /// </summary>
    public enum RosterAttribute
    {
        // Pace
        Acceleration = 0,
        TopSpeed,
        // Shooting
        Finishing,
        ShotPower,
        LongShots,
        OffTheBall,
        Volleys,
        Penalties,
        // Passing
        ShortPassing,
        LongPassing,
        Vision,
        Crossing,
        Curl,
        SetPieces,
        // Dribbling
        Dribbling,
        FirstTouch,
        Agility,
        Balance,
        Composure,
        // Defending
        Marking,
        StandingTackle,
        SlidingTackle,
        Interceptions,
        Heading,
        // Physical
        Stamina,
        Strength,
        Jumping,
        Aggression,
        Reactions,
        // Goalkeeping
        GkDiving,
        GkHandling,
        GkDistribution,
        GkReflexes,
        GkPositioning
    }
}
