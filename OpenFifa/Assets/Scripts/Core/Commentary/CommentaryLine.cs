namespace OpenFifa.Core
{
    /// <summary>
    /// One recorded/rendered line. <see cref="Text"/> excludes the name; when <see cref="Name"/> is not None a name
    /// clip (player or club, see <see cref="NameKind"/>) is stitched before or after it. A name call ("{name}...") has
    /// empty text and plays the name clip alone. Optional conditions narrow when the line fits.
    /// </summary>
    public sealed class CommentaryLine
    {
        public string Id { get; }
        public string Text { get; }
        public CommentaryNamePlacement Name { get; }
        public CommentaryIntonation Tone { get; }
        public CommentaryIntonation NameTone { get; }
        public CommentaryNameKind NameKind { get; }
        /// <summary>True for a bare name call: no line clip, only the stitched name.</summary>
        public bool NameOnly => Text.Length == 0;

        public float MinQuality { get; }
        public float MaxQuality { get; }
        public float MinDistance { get; }
        public float MaxDistance { get; }
        public float MinCrowd { get; }
        public int MinPassCount { get; }
        /// <summary>null, "draw" or "win" (full-time / half-time lines).</summary>
        public string Result { get; }

        public CommentaryLine(string id, string text, CommentaryNamePlacement name, CommentaryIntonation tone,
            CommentaryIntonation nameTone, float minQuality = 0f, float maxQuality = 1f, float minDistance = 0f,
            float maxDistance = 1000f, float minCrowd = 0f, int minPassCount = 0, string result = null,
            CommentaryNameKind nameKind = CommentaryNameKind.Player)
        {
            NameKind = nameKind;
            Id = id;
            Text = text;
            Name = name;
            Tone = tone;
            NameTone = nameTone;
            MinQuality = minQuality;
            MaxQuality = maxQuality;
            MinDistance = minDistance;
            MaxDistance = maxDistance;
            MinCrowd = minCrowd;
            MinPassCount = minPassCount;
            Result = result;
        }

        /// <summary>Clip id of the line audio: "line.&lt;id&gt;".</summary>
        public string ClipId => "line." + Id;

        /// <summary>The spoken name this line stitches for <paramref name="e"/> (null when it has none).</summary>
        public string SpokenName(CommentaryEvent e) =>
            Name == CommentaryNamePlacement.None ? null : NameKind == CommentaryNameKind.Team ? e.TeamName : e.PlayerName;

        public bool Fits(CommentaryEvent e, float crowd)
        {
            if (Name != CommentaryNamePlacement.None && string.IsNullOrEmpty(SpokenName(e))) return false;
            if (e.ShotQuality < MinQuality || e.ShotQuality > MaxQuality) return false;
            if (e.DistanceMetres < MinDistance || e.DistanceMetres > MaxDistance) return false;
            if (crowd < MinCrowd) return false;
            if (e.PassCount < MinPassCount) return false;
            if (Result != null)
            {
                bool draw = e.HomeScore == e.AwayScore;
                if (Result == "draw" && !draw) return false;
                if (Result == "win" && draw) return false;
            }
            return true;
        }
    }
}
