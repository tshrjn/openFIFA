namespace OpenFifa.Core
{
    /// <summary>Where a stitched name clip goes relative to its line clip ("{name}" at the start or end of the text).</summary>
    public enum CommentaryNamePlacement
    {
        None = 0,
        Before = 1,
        After = 2
    }

    /// <summary>What the stitched name clip says: the event's player ("{name}") or club ("{team}").</summary>
    public enum CommentaryNameKind
    {
        Player = 0,
        Team = 1
    }
}
