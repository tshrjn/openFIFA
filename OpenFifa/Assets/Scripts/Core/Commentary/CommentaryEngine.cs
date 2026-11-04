using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Event-tagged, priority-gated commentary stitcher (pure C#, deterministic for a given seed and event stream).
    ///
    /// Rules, in order:
    /// 1. Each event maps to a category: the most specific match of context (late winner > comeback > equaliser >
    ///    after goal > second half), then goal situation, then the generic category.
    /// 2. Gating: a category is skipped while on cooldown, and non-resolution categories are skipped when crowd
    ///    intensity is above their MaxCrowd (the noise would drown a build-up line anyway).
    /// 3. Scheduling on the lead voice: if idle, start now; if the current line is interruptible and the new one has
    ///    a strictly higher priority, stop it and start the new one; otherwise keep the best single pending line,
    ///    which is dropped if it gets stale (MaxQueueDelay; resolution and dead-ball lines wait longer).
    /// 4. No-repeat memory: a line is not reused within LineRepeatWindow while any alternative fits; otherwise the
    ///    least recently used line is taken. Choices among fresh lines use a seeded RNG.
    /// 5. The analyst speaks only during stoppages, only when the lead voice is silent, at most once per
    ///    AnalystMinGap. Play resuming cuts the analyst and clears its queue. The lead voice starting a line also
    ///    stops the analyst, so the two never overlap.
    /// 6. Lines with {name} are stitched from a name clip (in the line's name intonation) and a line clip.
    ///
    /// Usage: call <see cref="Push"/> for each event and <see cref="Tick"/> every frame; play Start cues and fade
    /// out Stop cues. Times are seconds on one monotonic clock.
    /// </summary>
    public sealed class CommentaryEngine
    {
        /// <summary>Contexts the engine derives, in precedence order. Bank "context" values must be one of these.</summary>
        public static readonly string[] Contexts = { "late_winner", "comeback", "equaliser", "after_goal", "second_half" };

        private sealed class Pending
        {
            public CommentaryCategory Category;
            public CommentaryEvent Event;
            public double QueuedAt;
            public float Crowd;
        }

        private readonly CommentaryLineBank _bank;
        private readonly CommentaryConfig _config;
        private readonly CommentaryRandom _rng;
        private readonly Dictionary<string, double> _categoryLastUsed = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _lineLastUsed = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly int[] _maxDeficit = new int[2];

        private CommentaryUtterance _pbp;
        private CommentaryUtterance _analyst;
        private Pending _pendingPbp;
        private Pending _pendingAnalyst;
        private double _pbpIdleSince = double.NegativeInfinity;
        private double _lastAnalystEnd = double.NegativeInfinity;
        private CommentaryEventType? _lastType;
        private bool _halfTimeSeen;
        private bool _secondHalfStarted;

        public bool IsStoppage { get; private set; } = true;
        public float CrowdIntensity { get; private set; }
        public CommentaryUtterance CurrentPlayByPlay => _pbp;
        public CommentaryUtterance CurrentAnalyst => _analyst;
        public CommentaryConfig Config => _config;

        public CommentaryEngine(CommentaryLineBank bank, CommentaryConfig config = null, uint seed = 1)
        {
            _bank = bank ?? throw new ArgumentNullException(nameof(bank));
            _config = config ?? new CommentaryConfig();
            _rng = new CommentaryRandom(seed);
        }

        public void SetCrowdIntensity(float value) => CrowdIntensity = Clamp01(value);

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public static bool IsStoppageEvent(CommentaryEventType t)
        {
            switch (t)
            {
                case CommentaryEventType.Goal:
                case CommentaryEventType.ShotWide:
                case CommentaryEventType.Foul:
                case CommentaryEventType.YellowCard:
                case CommentaryEventType.RedCard:
                case CommentaryEventType.Corner:
                case CommentaryEventType.Offside:
                case CommentaryEventType.HalfTime:
                case CommentaryEventType.FullTime:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsLivePlayEvent(CommentaryEventType t)
        {
            switch (t)
            {
                case CommentaryEventType.KickOff:
                case CommentaryEventType.PlayResumed:
                case CommentaryEventType.PassSequence:
                case CommentaryEventType.ThroughBall:
                case CommentaryEventType.Tackle:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Feeds one event (at e.Time). Returns cues to act on immediately.</summary>
        public List<CommentaryCue> Push(CommentaryEvent e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            double now = e.Time;
            var cues = new List<CommentaryCue>();
            Settle(now);
            if (e.CrowdIntensity >= 0f) CrowdIntensity = Clamp01(e.CrowdIntensity);

            var contexts = DeriveContexts(e);

            // Stoppage state.
            if (IsLivePlayEvent(e.Type))
            {
                IsStoppage = false;
                _pendingAnalyst = null;
                if (_analyst != null) StopAnalyst(now, cues);
            }
            else if (IsStoppageEvent(e.Type)) IsStoppage = true;

            // Lead voice.
            var cat = ChooseCategory(e, CommentaryVoice.PlayByPlay, contexts, now, CrowdIntensity);
            if (cat != null)
            {
                var p = new Pending { Category = cat, Event = e, QueuedAt = now, Crowd = CrowdIntensity };
                if (_pbp == null)
                {
                    _pendingPbp = null;
                    StartPbp(p, now, cues);
                }
                else if (_pbp.Interruptible && cat.Priority > _pbp.Priority)
                {
                    cues.Add(new CommentaryCue(CommentaryCueKind.Stop, _pbp));
                    _pbp.EndTime = now;
                    _pbp = null;
                    StartPbp(p, now, cues);
                }
                else if (_pendingPbp == null || cat.Priority >= _pendingPbp.Category.Priority)
                {
                    _pendingPbp = p;
                }
            }

            // Analyst, stoppages only.
            if (IsStoppage && IsStoppageEvent(e.Type))
            {
                var acat = ChooseCategory(e, CommentaryVoice.Analyst, contexts, now, CrowdIntensity);
                if (acat != null && now - _lastAnalystEnd >= _config.AnalystMinGap &&
                    _rng.NextDouble() < _config.AnalystChance &&
                    (_pendingAnalyst == null || acat.Priority >= _pendingAnalyst.Category.Priority))
                {
                    _pendingAnalyst = new Pending { Category = acat, Event = e, QueuedAt = now, Crowd = CrowdIntensity };
                }
            }

            UpdateMatchState(e);
            cues.AddRange(Tick(now));
            return cues;
        }

        /// <summary>Advances the scheduler; call every frame. Returns cues to act on.</summary>
        public List<CommentaryCue> Tick(double now)
        {
            var cues = new List<CommentaryCue>();
            Settle(now);

            if (_pendingPbp != null)
            {
                bool deadBall = _pendingPbp.Category.Resolution || IsStoppageEvent(_pendingPbp.Event.Type);
                double maxDelay = deadBall ? _config.MaxResolutionQueueDelay : _config.MaxQueueDelay;
                if (now - _pendingPbp.QueuedAt > maxDelay) _pendingPbp = null;
                else if (_pbp == null)
                {
                    var p = _pendingPbp;
                    _pendingPbp = null;
                    StartPbp(p, now, cues);
                }
            }

            if (_pendingAnalyst != null)
            {
                if (!IsStoppage || now - _pendingAnalyst.QueuedAt > _config.MaxAnalystDelay) _pendingAnalyst = null;
                else if (_pbp == null && _pendingPbp == null && _analyst == null &&
                         now >= _pbpIdleSince + _config.AnalystLeadIn)
                {
                    var p = _pendingAnalyst;
                    _pendingAnalyst = null;
                    var u = Build(p, now);
                    if (u != null)
                    {
                        _analyst = u;
                        cues.Add(new CommentaryCue(CommentaryCueKind.Start, u));
                    }
                }
            }
            return cues;
        }

        /// <summary>The audio layer reports that a voice finished (or was skipped) at <paramref name="now"/>.</summary>
        public void NotifyFinished(CommentaryVoice voice, double now)
        {
            if (voice == CommentaryVoice.PlayByPlay && _pbp != null)
            {
                _pbp.EndTime = now;
                _pbp = null;
                _pbpIdleSince = now;
            }
            else if (voice == CommentaryVoice.Analyst && _analyst != null)
            {
                _analyst.EndTime = now;
                _analyst = null;
                _lastAnalystEnd = now;
            }
        }

        // ---- internals ----

        private void Settle(double now)
        {
            if (_pbp != null && now >= _pbp.EndTime)
            {
                _pbpIdleSince = _pbp.EndTime;
                _pbp = null;
            }
            if (_analyst != null && now >= _analyst.EndTime)
            {
                _lastAnalystEnd = _analyst.EndTime;
                _analyst = null;
            }
        }

        private void StopAnalyst(double now, List<CommentaryCue> cues)
        {
            cues.Add(new CommentaryCue(CommentaryCueKind.Stop, _analyst));
            _analyst.EndTime = now;
            _analyst = null;
            _lastAnalystEnd = now;
        }

        private void StartPbp(Pending p, double now, List<CommentaryCue> cues)
        {
            var u = Build(p, now);
            if (u == null) return;
            if (_analyst != null) StopAnalyst(now, cues);
            _pbp = u;
            cues.Add(new CommentaryCue(CommentaryCueKind.Start, u));
        }

        private CommentaryUtterance Build(Pending p, double now)
        {
            var line = PickLine(p.Category, p.Event, p.Crowd, now);
            if (line == null) return null;
            _categoryLastUsed[p.Category.Id] = now;
            _lineLastUsed[line.Id] = now;

            var voice = p.Category.Voice;
            var lineClip = new CommentaryClip(line.ClipId, line.Text, voice, line.Tone, false);
            CommentaryClip[] clips;
            double dur = Math.Max(0.3, line.Text.Length / _config.CharsPerSecond);
            if (line.Name == CommentaryNamePlacement.None) clips = new[] { lineClip };
            else
            {
                string name = p.Event.PlayerName;
                var nameClip = new CommentaryClip(CommentaryClip.NameClipId(name, line.NameTone), name, voice, line.NameTone, true);
                clips = line.Name == CommentaryNamePlacement.Before ? new[] { nameClip, lineClip } : new[] { lineClip, nameClip };
                dur += Math.Max(_config.MinNameSeconds, name.Length / _config.CharsPerSecond) + _config.ClipGap;
            }
            return new CommentaryUtterance(p.Category, line, clips, now, now + dur);
        }

        private CommentaryLine PickLine(CommentaryCategory cat, CommentaryEvent e, float crowd, double now)
        {
            var fits = new List<CommentaryLine>();
            foreach (var l in cat.Lines)
                if (l.Fits(e, crowd)) fits.Add(l);
            if (fits.Count == 0) return null;

            var fresh = new List<CommentaryLine>();
            foreach (var l in fits)
                if (!_lineLastUsed.TryGetValue(l.Id, out double t) || now - t >= _config.LineRepeatWindow) fresh.Add(l);
            if (fresh.Count > 0) return fresh[_rng.Next(fresh.Count)];

            // Everything fitting was used recently: take the least recently used (first in bank order on ties).
            CommentaryLine best = null;
            double bestT = double.PositiveInfinity;
            foreach (var l in fits)
            {
                double t = _lineLastUsed[l.Id];
                if (t < bestT) { bestT = t; best = l; }
            }
            return best;
        }

        private CommentaryCategory ChooseCategory(CommentaryEvent e, CommentaryVoice voice, List<string> contexts,
            double now, float crowd)
        {
            var all = _bank.For(e.Type, voice);
            if (all.Count == 0) return null;
            string situation = e.Type == CommentaryEventType.Goal ? CommentaryLineBank.SnakeCase(e.Situation.ToString()) : null;

            foreach (var ctx in contexts)
                foreach (var c in all)
                    if (c.Context == ctx && (c.Situation == null || c.Situation == situation) && Usable(c, e, now, crowd)) return c;
            if (situation != null)
                foreach (var c in all)
                    if (c.Context == null && c.Situation == situation && Usable(c, e, now, crowd)) return c;
            foreach (var c in all)
                if (c.Context == null && c.Situation == null && Usable(c, e, now, crowd)) return c;
            return null;
        }

        private bool Usable(CommentaryCategory c, CommentaryEvent e, double now, float crowd)
        {
            if (!c.Resolution && crowd > c.MaxCrowd) return false;
            if (c.Cooldown > 0 && _categoryLastUsed.TryGetValue(c.Id, out double t) && now - t < c.Cooldown) return false;
            foreach (var l in c.Lines)
                if (l.Fits(e, crowd)) return true;
            return false;
        }

        private List<string> DeriveContexts(CommentaryEvent e)
        {
            var set = new List<string>();
            switch (e.Type)
            {
                case CommentaryEventType.Goal:
                {
                    int t = e.Team == 1 ? 1 : 0;
                    int ownAfter = t == 0 ? e.HomeScore : e.AwayScore;
                    int opp = t == 0 ? e.AwayScore : e.HomeScore;
                    int ownBefore = ownAfter - 1;
                    if (e.Minute >= _config.LateWinnerMinute && ownBefore == opp) set.Add("late_winner");
                    if ((_maxDeficit[t] >= 2 && ownAfter >= opp) || (_maxDeficit[t] >= 1 && ownBefore == opp))
                        set.Add("comeback");
                    if (ownAfter == opp) set.Add("equaliser");
                    break;
                }
                case CommentaryEventType.KickOff:
                    if (_lastType == CommentaryEventType.Goal) set.Add("after_goal");
                    else if (_halfTimeSeen && !_secondHalfStarted) set.Add("second_half");
                    break;
                case CommentaryEventType.FullTime:
                {
                    if (e.HomeScore != e.AwayScore)
                    {
                        int winner = e.HomeScore > e.AwayScore ? 0 : 1;
                        if (_maxDeficit[winner] >= 1) set.Add("comeback");
                    }
                    break;
                }
            }
            // Keep the documented precedence order.
            var ordered = new List<string>();
            foreach (var c in Contexts)
                if (set.Contains(c)) ordered.Add(c);
            return ordered;
        }

        private void UpdateMatchState(CommentaryEvent e)
        {
            if (e.Type == CommentaryEventType.Goal)
            {
                int dHome = e.AwayScore - e.HomeScore;
                if (dHome > _maxDeficit[0]) _maxDeficit[0] = dHome;
                if (-dHome > _maxDeficit[1]) _maxDeficit[1] = -dHome;
            }
            if (e.Type == CommentaryEventType.HalfTime) _halfTimeSeen = true;
            if (e.Type == CommentaryEventType.KickOff && _halfTimeSeen) _secondHalfStarted = true;
            if (e.Type != CommentaryEventType.PlayResumed) _lastType = e.Type;
        }
    }
}
