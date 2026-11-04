namespace OpenFifa.Core
{
    /// <summary>
    /// One pre-rendered audio file. Ids match the TTS manifest (tools/commentary/build_tts_manifest.py):
    /// "line.&lt;lineId&gt;" for lines, "name.&lt;slug&gt;.&lt;intonation&gt;" for player call-outs (lead voice) and
    /// "team.&lt;slug&gt;.&lt;intonation&gt;" for club names (both voices).
    /// </summary>
    public sealed class CommentaryClip
    {
        public string ClipId { get; }
        public string Text { get; }
        public CommentaryVoice Voice { get; }
        public CommentaryIntonation Intonation { get; }
        public bool IsName { get; }

        public CommentaryClip(string clipId, string text, CommentaryVoice voice, CommentaryIntonation intonation, bool isName)
        {
            ClipId = clipId;
            Text = text;
            Voice = voice;
            Intonation = intonation;
            IsName = isName;
        }

        public static string NameClipId(string spokenName, CommentaryIntonation intonation) =>
            "name." + RosterSchema.Slug(spokenName) + "." + intonation.ToString().ToLowerInvariant();

        public static string TeamClipId(string spokenTeam, CommentaryIntonation intonation) =>
            "team." + RosterSchema.Slug(spokenTeam) + "." + intonation.ToString().ToLowerInvariant();

        public override string ToString() => ClipId;
    }
}
