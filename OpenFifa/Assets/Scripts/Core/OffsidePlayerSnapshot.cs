namespace OpenFifa.Core
{
    /// <summary>
    /// Position of one player at a given instant, in world metres on the ground plane.
    /// X is the pitch length axis (goal lines at +/- half length), Z is the width axis.
    /// The point is the player's centre; the configurable body radius is added by the logic.
    /// </summary>
    public readonly struct OffsidePlayerSnapshot
    {
        public TeamIdentifier Team { get; }
        public int PlayerId { get; }
        public float X { get; }
        public float Z { get; }

        public OffsidePlayerSnapshot(TeamIdentifier team, int playerId, float x, float z)
        {
            Team = team;
            PlayerId = playerId;
            X = x;
            Z = z;
        }
    }
}
