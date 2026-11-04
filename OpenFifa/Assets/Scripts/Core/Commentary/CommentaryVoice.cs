namespace OpenFifa.Core
{
    /// <summary>The two commentary voices. Bank values: "pbp", "analyst".</summary>
    public enum CommentaryVoice
    {
        /// <summary>Lead commentator: describes the action as it happens.</summary>
        PlayByPlay = 0,
        /// <summary>Co-commentator: colour and opinion, only during stoppages.</summary>
        Analyst = 1
    }
}
