namespace OpenFifa.Core
{
    /// <summary>
    /// What audio actually exists (the rendered clip index). When the engine has a catalog it skips lines whose clips
    /// are missing (e.g. a player added after the last TTS render) and times utterances from real clip lengths.
    /// Without one, every line is usable and lengths are estimated from the text.
    /// </summary>
    public interface ICommentaryClipCatalog
    {
        /// <summary>True when the clip is rendered for that voice.</summary>
        bool Has(CommentaryVoice voice, string clipId);

        /// <summary>Clip length in seconds, or a value &lt;= 0 when unknown.</summary>
        double Duration(CommentaryVoice voice, string clipId);
    }
}
