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
        public double AnalystMinGap { get; set; } = 12.0;
        /// <summary>Chance (0..1) that a stoppage gets an analyst line (big moments always do, unless this is 0).</summary>
        public double AnalystChance { get; set; } = 0.8;
        /// <summary>A line is not reused within this many seconds while alternatives exist.</summary>
        public double LineRepeatWindow { get; set; } = 900.0;
        /// <summary>Minute from which a go-ahead goal counts as a late winner.</summary>
        public int LateWinnerMinute { get; set; } = 85;
        /// <summary>Goals up to this minute get "early_goal" lines.</summary>
        public int EarlyGoalMinute { get; set; } = 5;
        /// <summary>From this minute a chance for a team that is not ahead gets "late_chance" lines.</summary>
        public int LateChanceMinute { get; set; } = 82;
        /// <summary>From this minute lull chatter uses "late_on" lines.</summary>
        public int LateOnMinute { get; set; } = 78;
        /// <summary>
        /// Seconds of silence in live play before the engine fills it (a Lull event: colour, match situation). 0 disables.
        /// </summary>
        public double LullSeconds { get; set; } = 11.0;
        /// <summary>Chance (0..1) that a lull goes to the analyst rather than the lead voice (when it has lull lines).</summary>
        public double AnalystLullShare { get; set; } = 0.55;
        /// <summary>Analyst "live" lines (after a save, in a lull) need the lead voice quiet for this long first.</summary>
        public double AnalystLiveQuiet { get; set; } = 1.0;
        /// <summary>
        /// A higher-priority line waits instead of cutting the current one when that ends within this many seconds, so
        /// "Okafor shoots..." finishes before "...and it's saved!".
        /// </summary>
        public double InterruptGrace { get; set; } = 0.45;
        /// <summary>Lead lines below this priority (name calls) never cut the analyst; they are dropped while it talks.</summary>
        public int AnalystYieldPriority { get; set; } = 25;
    }
}
