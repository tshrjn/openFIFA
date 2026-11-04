namespace OpenFifa.Core
{
    /// <summary>
    /// One recorded/rendered line. <see cref="Text"/> excludes the name; when <see cref="Name"/> is not None a name
    /// clip is stitched before or after it. Optional conditions narrow when the line fits.
    /// </summary>
    public sealed class CommentaryLine
    {
        public string Id { get; }
        public string Text { get; }
        public CommentaryNamePlacement Name { get; }
        public CommentaryIntonation Tone { get; }
        public CommentaryIntonation NameTone { get; }

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
            float maxDistance = 1000f, float minCrowd = 0f, int minPassCount = 0, string result = null)
        {
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

        public bool Fits(CommentaryEvent e, float crowd)
        {
            if (Name != CommentaryNamePlacement.None && string.IsNullOrEmpty(e.PlayerName)) return false;
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
