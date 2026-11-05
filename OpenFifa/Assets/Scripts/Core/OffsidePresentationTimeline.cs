using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# sequencer for the offside presentation (whistle, flag, slow motion,
    /// freeze, camera cut, offside lines, lower-third banner). Advance it with
    /// unscaled delta time; it reports which cues fired. Skippable.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class OffsidePresentationTimeline
    {
        private static readonly OffsideCue[] TeardownCues =
        {
            OffsideCue.LinesHide, OffsideCue.CameraCutOut, OffsideCue.TimeRestore, OffsideCue.BannerHide
        };

        private readonly OffsidePresentationTimingData _timing;
        private readonly HashSet<OffsideCue> _fired = new HashSet<OffsideCue>();
        private readonly List<OffsideCue> _buffer = new List<OffsideCue>();

        public float Elapsed { get; private set; }
        public bool IsComplete { get; private set; }
        public bool WasSkipped { get; private set; }
        public OffsidePresentationTimingData Timing => _timing;

        public OffsidePresentationTimeline(OffsidePresentationTimingData timing = null)
        {
            _timing = timing ?? new OffsidePresentationTimingData();
        }

        public bool HasFired(OffsideCue cue) => _fired.Contains(cue);

        /// <summary>Time of a cue in seconds from the start.</summary>
        public float GetCueTime(OffsideCue cue)
        {
            switch (cue)
            {
                case OffsideCue.Whistle: return 0f;
                case OffsideCue.FlagRaise: return _timing.FlagRaiseAt;
                case OffsideCue.SlowMotionStart: return _timing.SlowMotionStart;
                case OffsideCue.BannerShow: return _timing.BannerShow;
                case OffsideCue.FreezeStart: return _timing.FreezeStart;
                case OffsideCue.CameraCutIn: return _timing.CameraCutIn;
                case OffsideCue.LinesShow: return _timing.LinesShow;
                case OffsideCue.LinesHide: return _timing.LinesHide;
                case OffsideCue.CameraCutOut: return _timing.CameraCutOut;
                case OffsideCue.TimeRestore: return _timing.TimeRestore;
                case OffsideCue.BannerHide: return _timing.BannerHide;
                default: return _timing.TotalDuration;
            }
        }

        /// <summary>
        /// Advance by unscaled seconds. Returns cues that fired during this step,
        /// in cue order. The returned list is reused between calls.
        /// </summary>
        public IReadOnlyList<OffsideCue> Advance(float unscaledDeltaTime)
        {
            _buffer.Clear();
            if (IsComplete) return _buffer;

            if (unscaledDeltaTime > 0f) Elapsed += unscaledDeltaTime;

            for (int c = (int)OffsideCue.Whistle; c <= (int)OffsideCue.Complete; c++)
            {
                var cue = (OffsideCue)c;
                if (_fired.Contains(cue)) continue;
                if (Elapsed + 1e-5f >= GetCueTime(cue)) Fire(cue);
            }
            return _buffer;
        }

        /// <summary>
        /// Skip to the end. Returns the teardown cues still owed (so the game state is
        /// always restored) followed by Complete. Intro cues not yet fired are dropped.
        /// </summary>
        public IReadOnlyList<OffsideCue> Skip()
        {
            _buffer.Clear();
            if (IsComplete) return _buffer;

            WasSkipped = true;
            for (int i = 0; i < TeardownCues.Length; i++)
            {
                if (!_fired.Contains(TeardownCues[i])) Fire(TeardownCues[i]);
            }
            for (int c = (int)OffsideCue.Whistle; c < (int)OffsideCue.Complete; c++)
                _fired.Add((OffsideCue)c);
            Fire(OffsideCue.Complete);
            Elapsed = _timing.TotalDuration;
            return _buffer;
        }

        /// <summary>Time scale the game should run at, at the current elapsed time.</summary>
        public float GetTimeScale()
        {
            if (IsComplete || Elapsed >= _timing.TimeRestore) return 1f;
            if (Elapsed >= _timing.FreezeStart) return 0f;
            if (Elapsed >= _timing.SlowMotionStart) return _timing.SlowMotionScale;
            return 1f;
        }

        /// <summary>0..1 progress of the lines sweeping across the pitch.</summary>
        public float GetLineReveal()
        {
            if (!_fired.Contains(OffsideCue.LinesShow) || _fired.Contains(OffsideCue.LinesHide)) return 0f;
            if (_timing.LineRevealDuration <= 0f) return 1f;
            float t = (Elapsed - _timing.LinesShow) / _timing.LineRevealDuration;
            return t < 0f ? 0f : (t > 1f ? 1f : t);
        }

        private void Fire(OffsideCue cue)
        {
            _fired.Add(cue);
            _buffer.Add(cue);
            if (cue == OffsideCue.Complete) IsComplete = true;
        }
    }
}
