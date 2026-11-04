using System.Collections.Generic;
using System.Text;

namespace OpenFifa.Core
{
    /// <summary>A line as scheduled: its clips in play order plus timing and scheduling metadata.</summary>
    public sealed class CommentaryUtterance
    {
        private readonly CommentaryClip[] _clips;

        public CommentaryCategory Category { get; }
        public CommentaryLine Line { get; }
        public CommentaryVoice Voice => Category.Voice;
        public int Priority => Category.Priority;
        public bool Interruptible => Category.Interruptible;
        public double StartTime { get; }
        /// <summary>Estimated end; the audio layer can correct it with <see cref="CommentaryEngine.NotifyFinished"/>.</summary>
        public double EndTime { get; internal set; }
        public IReadOnlyList<CommentaryClip> Clips => _clips;

        public CommentaryUtterance(CommentaryCategory category, CommentaryLine line, CommentaryClip[] clips,
            double startTime, double endTime)
        {
            Category = category;
            Line = line;
            _clips = clips;
            StartTime = startTime;
            EndTime = endTime;
        }

        /// <summary>Readable transcript, e.g. "Okafor | sweeps it into the corner!".</summary>
        public string Transcript
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var c in _clips)
                {
                    if (sb.Length > 0) sb.Append(" | ");
                    sb.Append(c.Text);
                }
                return sb.ToString();
            }
        }

        public override string ToString() => Voice + " " + Line.Id + ": " + Transcript;
    }
}
