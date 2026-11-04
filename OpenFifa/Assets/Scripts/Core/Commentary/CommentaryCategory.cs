using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// A group of interchangeable lines for one situation (e.g. "goal.header"), with the scheduling rules that
    /// apply to all of them.
    /// </summary>
    public sealed class CommentaryCategory
    {
        private readonly CommentaryLine[] _lines;

        public string Id { get; }
        public CommentaryEventType Event { get; }
        public CommentaryVoice Voice { get; }
        /// <summary>Optional match context the engine derives ("late_winner", "comeback", "equaliser", "after_goal", "second_half").</summary>
        public string Context { get; }
        /// <summary>Optional goal situation filter (snake_case <see cref="GoalSituation"/>).</summary>
        public string Situation { get; }
        /// <summary>0-100. Higher pre-empts lower.</summary>
        public int Priority { get; }
        /// <summary>Whether a higher-priority line may cut this one off.</summary>
        public bool Interruptible { get; }
        /// <summary>Outcome lines (goal, save, wide): ignore crowd gating.</summary>
        public bool Resolution { get; }
        /// <summary>Minimum seconds between two lines from this category.</summary>
        public float Cooldown { get; }
        /// <summary>Non-resolution lines are skipped when crowd intensity is above this (the crowd drowns them).</summary>
        public float MaxCrowd { get; }
        /// <summary>
        /// Analyst only: may speak in live play (after a save, during a lull) once the lead voice has been quiet for
        /// <see cref="CommentaryConfig.AnalystLiveQuiet"/>; any live event cuts it. Other analyst lines wait for stoppages.
        /// </summary>
        public bool Live { get; }

        public IReadOnlyList<CommentaryLine> Lines => _lines;

        public CommentaryCategory(string id, CommentaryEventType evt, CommentaryVoice voice, string context,
            string situation, int priority, bool interruptible, bool resolution, float cooldown, float maxCrowd,
            CommentaryLine[] lines, bool live = false)
        {
            Live = live;
            Id = id;
            Event = evt;
            Voice = voice;
            Context = context;
            Situation = situation;
            Priority = priority;
            Interruptible = interruptible;
            Resolution = resolution;
            Cooldown = cooldown;
            MaxCrowd = maxCrowd;
            _lines = (CommentaryLine[])lines.Clone();
        }
    }
}
