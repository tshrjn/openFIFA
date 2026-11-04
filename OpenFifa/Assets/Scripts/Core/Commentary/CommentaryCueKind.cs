namespace OpenFifa.Core
{
    /// <summary>What the audio layer should do with a cue from <see cref="CommentaryEngine"/>.</summary>
    public enum CommentaryCueKind
    {
        /// <summary>Start playing the utterance's clips in order on its voice channel.</summary>
        Start = 0,
        /// <summary>Stop (quick fade) the utterance currently playing on that voice.</summary>
        Stop = 1
    }
}
