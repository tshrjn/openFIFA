namespace OpenFifa.Core
{
    /// <summary>Tunables for <see cref="CommentaryEngine"/>. Defaults suit a full-length match.</summary>
    public sealed class CommentaryConfig
    {
        /// <summary>Speech rate used to estimate clip length.</summary>
        public double CharsPerSecond { get; set; } = 15.0;
        /// <summary>Silence between stitched clips (name -> line).</summary>
        public double ClipGap { get; set; } = 0.08;
        /// <summary>Shortest a name clip is assumed to last.</summary>
        public double MinNameSeconds { get; set; } = 0.45;
        /// <summary>A queued play-by-play line older than this is dropped (the moment has passed).</summary>
        public double MaxQueueDelay { get; set; } = 2.0;
        /// <summary>Resolution lines (goal, save, wide) and dead-ball calls (corner, foul, card) may wait longer.</summary>
        public double MaxResolutionQueueDelay { get; set; } = 4.5;
        /// <summary>Pause after the lead voice finishes before the analyst starts.</summary>
        public double AnalystLeadIn { get; set; } = 0.6;
        /// <summary>A queued analyst line older than this is dropped.</summary>
        public double MaxAnalystDelay { get; set; } = 10.0;
        /// <summary>Minimum seconds between two analyst lines.</summary>
        public double AnalystMinGap { get; set; } = 15.0;
        /// <summary>Chance (0..1) that a stoppage gets an analyst line at all.</summary>
        public double AnalystChance { get; set; } = 0.65;
        /// <summary>A line is not reused within this many seconds while alternatives exist.</summary>
        public double LineRepeatWindow { get; set; } = 900.0;
        /// <summary>Minute from which a go-ahead goal counts as a late winner.</summary>
        public int LateWinnerMinute { get; set; } = 85;
    }
}
