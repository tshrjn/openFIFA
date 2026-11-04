namespace OpenFifa.Core
{
    /// <summary>
    /// One contact with the ball by a player.
    /// </summary>
    public readonly struct BallTouch
    {
        public TeamIdentifier Team { get; }
        public int PlayerId { get; }
        public BallTouchKind Kind { get; }
        /// <summary>Set when this touch is the restart of play (e.g. a throw-in).</summary>
        public RestartType Restart { get; }

        public BallTouch(TeamIdentifier team, int playerId,
            BallTouchKind kind = BallTouchKind.Deliberate,
            RestartType restart = RestartType.None)
        {
            Team = team;
            PlayerId = playerId;
            Kind = kind;
            Restart = restart;
        }
    }
}
