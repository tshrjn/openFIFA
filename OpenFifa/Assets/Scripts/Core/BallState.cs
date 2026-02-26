namespace OpenFifa.Core
{
    /// <summary>
    /// Represents the current state of the ball in gameplay.
    /// </summary>
    public enum BallState
    {
        /// <summary>The ball is free and not possessed by any player.</summary>
        Free = 0,

        /// <summary>The ball is currently possessed/controlled by a player.</summary>
        Possessed = 1,

        /// <summary>The ball is in flight (kicked, passed, or shot).</summary>
        InFlight = 2
    }
}
