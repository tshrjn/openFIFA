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
    /// 5. The analyst speaks during stoppages, and in live play only from "live" categories (a reaction to a save, a
    ///    lull) once the lead voice has been quiet for AnalystLiveQuiet; always only when the lead voice is silent, at
    ///    most once per AnalystMinGap. Play resuming cuts the analyst and clears its queue. The lead voice starting a
    ///    line also stops the analyst, so the two never overlap; low-priority name calls are dropped instead of cutting
    ///    it (AnalystYieldPriority).
    /// 6. Lines with {name} / {team} are stitched from a name clip (in the line's name intonation) and a line clip; a
    ///    bare "{name}" line is a name call. With a clip catalog, lines whose clips are not rendered are skipped and
    ///    utterances are timed from the real clip lengths.
    /// 7. A higher-priority line waits instead of cutting when the current line ends within InterruptGrace.
    /// 8. After LullSeconds of silence in live play the engine synthesises a Lull event (match-situation chatter),
    ///    given to the analyst AnalystLullShare of the time.
    ///
    /// Usage: call <see cref="Push"/> for each event and <see cref="Tick"/> every frame; play Start cues and fade
    /// out Stop cues. Times are seconds on one monotonic clock.
    ///
    /// Allocation: <see cref="Tick"/> and <see cref="Push"/> return lists owned by the engine and reused (copy the cues
    /// if you need them past the next call of the same method). In steady state (nothing new to say, or an event that is
    /// gated or queued) neither call allocates; starting a line allocates its utterance and clips.
    /// </summary>
    public sealed class CommentaryEngine
    {
        /// <summary>Contexts the engine derives, in precedence order. Bank "context" values must be one of these.</summary>
        public static readonly string[] Contexts =
        {
            "hat_trick", "brace", "late_winner", "comeback", "equaliser", "early_goal", "late_chance", "after_goal",
            "second_half", "late_on", "comfortable", "tight", "goalless"
        };

        // Indices into Contexts.
        private const int CtxHatTrick = 0, CtxBrace = 1, CtxLateWinner = 2, CtxComeback = 3, CtxEqualiser = 4,
            CtxEarlyGoal = 5, CtxLateChance = 6, CtxAfterGoal = 7, CtxSecondHalf = 8, CtxLateOn = 9, CtxComfortable = 10,
            CtxTight = 11, CtxGoalless = 12;

        private struct Pending
        {
            public CommentaryCategory Category;
            public CommentaryEvent Event;
            public double QueuedAt;
            public float Crowd;

            public bool IsSet => Category != null;
        }

        private static readonly string[] SituationNames = BuildSituationNames();

        private static string[] BuildSituationNames()
        {
            var values = (GoalSituation[])Enum.GetValues(typeof(GoalSituation));
            int max = 0;
            foreach (var v in values) if ((int)v > max) max = (int)v;
            var names = new string[max + 1];
            foreach (var v in values) names[(int)v] = CommentaryLineBank.SnakeCase(v.ToString());
            return names;
        }

        private static string SituationName(GoalSituation g)
        {
            int i = (int)g;
            return i >= 0 && i < SituationNames.Length && SituationNames[i] != null
                ? SituationNames[i]
                : CommentaryLineBank.SnakeCase(g.ToString());
        }

        // Reused scratch and output lists (see the allocation note above).
        private readonly List<CommentaryCue> _tickCues = new List<CommentaryCue>(4);
        private readonly List<CommentaryCue> _pushCues = new List<CommentaryCue>(4);
        // Read-only views handed to callers (allocated once): a returned list cannot be cast back and mutated.
        private readonly System.Collections.ObjectModel.ReadOnlyCollection<CommentaryCue> _tickView;
        private readonly System.Collections.ObjectModel.ReadOnlyCollection<CommentaryCue> _pushView;
        private readonly List<string> _contexts = new List<string>(13);
        private readonly List<CommentaryLine> _fits = new List<CommentaryLine>(16);
        private readonly List<CommentaryLine> _fresh = new List<CommentaryLine>(16);

        private readonly CommentaryLineBank _bank;
        private readonly CommentaryConfig _config;
        private readonly CommentaryRandom _rng;
        private readonly Dictionary<string, double> _categoryLastUsed = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _lineLastUsed = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly int[] _maxDeficit = new int[2];
        private readonly bool[] _ctxFlags = new bool[Contexts.Length];
        private readonly Dictionary<string, int> _goalsBy = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly CommentaryEvent _lull = new CommentaryEvent { Type = CommentaryEventType.Lull };
        private int _lastMinute, _lastHome, _lastAway;
        private double _lastActivity = double.NegativeInfinity;
        private ICommentaryClipCatalog _catalog;

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

        /// <summary>Rendered-audio index (optional). Set it before the match; see <see cref="ICommentaryClipCatalog"/>.</summary>
        public ICommentaryClipCatalog Catalog
        {
            get => _catalog;
            set => _catalog = value;
        }

        public CommentaryEngine(CommentaryLineBank bank, CommentaryConfig config = null, uint seed = 1)
        {
            _bank = bank ?? throw new ArgumentNullException(nameof(bank));
            _config = config ?? new CommentaryConfig();
            _rng = new CommentaryRandom(seed);
            _tickView = _tickCues.AsReadOnly();
            _pushView = _pushCues.AsReadOnly();
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
                case CommentaryEventType.ShotTaken:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Feeds one event (at e.Time). Returns cues to act on immediately, as a read-only view of a list owned by the
        /// engine. Lifetime: the view stays valid until the next <see cref="Push"/>, which clears and refills it (a
        /// <see cref="Tick"/> does not touch it). Callers that keep cues past that must copy them (e.g. List.AddRange).
        /// </summary>
        public IReadOnlyList<CommentaryCue> Push(CommentaryEvent e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            double now = e.Time;
            var cues = _pushCues;
            cues.Clear();
            Settle(now);
            if (e.CrowdIntensity >= 0f) CrowdIntensity = Clamp01(e.CrowdIntensity);
            _lastMinute = e.Minute;
            _lastHome = e.HomeScore;
            _lastAway = e.AwayScore;
            if (e.Type == CommentaryEventType.KickOff || e.Type == CommentaryEventType.PlayResumed)
                _lastActivity = Math.Max(_lastActivity, now);
            if (e.Type == CommentaryEventType.Goal) CountGoal(e);

            var contexts = DeriveContexts(e);

            // Stoppage state.
            if (IsLivePlayEvent(e.Type))
            {
                IsStoppage = false;
                _pendingAnalyst = default;
                if (_analyst != null) StopAnalyst(now, cues);
            }
            else if (IsStoppageEvent(e.Type)) IsStoppage = true;

            // Lead voice.
            var cat = ChooseCategory(e, CommentaryVoice.PlayByPlay, contexts, now, CrowdIntensity, false);
            if (cat != null && cat.Priority < _config.AnalystYieldPriority && (_analyst != null || _pendingAnalyst.IsSet))
                cat = null; // a name call never cuts the co-commentator
            if (cat != null)
            {
                var p = new Pending { Category = cat, Event = e, QueuedAt = now, Crowd = CrowdIntensity };
                if (_pbp == null)
                {
                    _pendingPbp = default;
                    StartPbp(p, now, cues);
                }
                else if (_pbp.Interruptible && cat.Priority > _pbp.Priority && _pbp.EndTime - now > _config.InterruptGrace)
                {
                    cues.Add(new CommentaryCue(CommentaryCueKind.Stop, _pbp));
                    _pbp.EndTime = now;
                    _pbp = null;
                    StartPbp(p, now, cues);
                }
                else if (!_pendingPbp.IsSet || cat.Priority >= _pendingPbp.Category.Priority)
                {
                    _pendingPbp = p;
                }
            }

            // Analyst: any category at a stoppage, "live" categories otherwise.
            bool stoppage = IsStoppage && IsStoppageEvent(e.Type);
            if (stoppage || !IsLivePlayEvent(e.Type))
            {
                var acat = ChooseCategory(e, CommentaryVoice.Analyst, contexts, now, CrowdIntensity, !stoppage);
                if (acat != null && now - _lastAnalystEnd >= _config.AnalystMinGap &&
                    _rng.NextDouble() < _config.AnalystChance &&
                    (!_pendingAnalyst.IsSet || acat.Priority >= _pendingAnalyst.Category.Priority))
                {
                    _pendingAnalyst = new Pending { Category = acat, Event = e, QueuedAt = now, Crowd = CrowdIntensity };
                }
            }

            UpdateMatchState(e);
            Advance(now, cues);
            return _pushView;
        }

        /// <summary>
        /// Advances the scheduler; call every frame. Returns cues to act on, as a read-only view of a list owned by the
        /// engine. Lifetime: valid until the next <see cref="Tick"/>, which clears and refills it (a <see cref="Push"/> does
        /// not touch it); copy to keep. Allocation-free when nothing starts.
        /// </summary>
        public IReadOnlyList<CommentaryCue> Tick(double now)
        {
            var cues = _tickCues;
            cues.Clear();
            Advance(now, cues);
            return _tickView;
        }

        private void Advance(double now, List<CommentaryCue> cues)
        {
            Settle(now);

            if (_pendingPbp.IsSet)
            {
                bool deadBall = _pendingPbp.Category.Resolution || IsStoppageEvent(_pendingPbp.Event.Type);
                double maxDelay = deadBall ? _config.MaxResolutionQueueDelay : _config.MaxQueueDelay;
                if (now - _pendingPbp.QueuedAt > maxDelay) _pendingPbp = default;
                else if (_pbp == null)
                {
                    var p = _pendingPbp;
                    _pendingPbp = default;
                    StartPbp(p, now, cues);
                }
            }

            if (_pendingAnalyst.IsSet)
            {
                bool live = _pendingAnalyst.Category.Live;
                double leadIn = live ? _config.AnalystLiveQuiet : _config.AnalystLeadIn;
                if ((!IsStoppage && !live) || now - _pendingAnalyst.QueuedAt > _config.MaxAnalystDelay) _pendingAnalyst = default;
                else if (_pbp == null && !_pendingPbp.IsSet && _analyst == null && now >= _pbpIdleSince + leadIn)
                {
                    var p = _pendingAnalyst;
                    _pendingAnalyst = default;
                    var u = Build(p, now);
                    if (u != null)
                    {
                        _analyst = u;
                        cues.Add(new CommentaryCue(CommentaryCueKind.Start, u));
                    }
                }
            }

            MaybeFillLull(now, cues);
        }

        /// <summary>After a quiet spell in live play, says something about the match (Lull event).</summary>
        private void MaybeFillLull(double now, List<CommentaryCue> cues)
        {
            if (_config.LullSeconds <= 0 || IsStoppage || _pbp != null || _analyst != null || _pendingPbp.IsSet ||
                _pendingAnalyst.IsSet)
                return;
            double quietSince = Math.Max(_lastActivity, Math.Max(_pbpIdleSince, _lastAnalystEnd));
            if (now - quietSince < _config.LullSeconds) return;
            _lastActivity = now; // one attempt per quiet spell, said or not

            var e = _lull;
            e.Time = now;
            e.Minute = _lastMinute;
            e.HomeScore = _lastHome;
            e.AwayScore = _lastAway;
            var contexts = DeriveContexts(e);
            bool analystFirst = _rng.NextDouble() < _config.AnalystLullShare && now - _lastAnalystEnd >= _config.AnalystMinGap;
            for (int pass = 0; pass < 2; pass++)
            {
                bool analyst = analystFirst == (pass == 0);
                var voice = analyst ? CommentaryVoice.Analyst : CommentaryVoice.PlayByPlay;
                var cat = ChooseCategory(e, voice, contexts, now, CrowdIntensity, false);
                if (cat == null) continue;
                var u = Build(new Pending { Category = cat, Event = e, QueuedAt = now, Crowd = CrowdIntensity }, now);
                if (u == null) continue;
                if (analyst) _analyst = u;
                else _pbp = u;
                cues.Add(new CommentaryCue(CommentaryCueKind.Start, u));
                return;
            }
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
            CommentaryClip lineClip = line.NameOnly ? null : new CommentaryClip(line.ClipId, line.Text, voice, line.Tone, false);
            CommentaryClip[] clips;
            double dur = lineClip == null ? 0 : ClipSeconds(voice, line.ClipId, line.Text, 0.3);
            if (line.Name == CommentaryNamePlacement.None) clips = new[] { lineClip };
            else
            {
                string name = line.SpokenName(p.Event);
                string nameId = NameClipId(line, name);
                var nameClip = new CommentaryClip(nameId, name, voice, line.NameTone, true);
                if (lineClip == null) clips = new[] { nameClip };
                else
                {
                    clips = line.Name == CommentaryNamePlacement.Before ? new[] { nameClip, lineClip } : new[] { lineClip, nameClip };
                    dur += _config.ClipGap;
                }
                dur += ClipSeconds(voice, nameId, name, _config.MinNameSeconds);
            }
            return new CommentaryUtterance(p.Category, line, clips, now, now + dur);
        }

        private double ClipSeconds(CommentaryVoice voice, string clipId, string text, double min)
        {
            double d = _catalog != null ? _catalog.Duration(voice, clipId) : 0;
            return d > 0 ? d : Math.Max(min, text.Length / _config.CharsPerSecond);
        }

        private static string NameClipId(CommentaryLine line, string name) =>
            line.NameKind == CommentaryNameKind.Team
                ? CommentaryClip.TeamClipId(name, line.NameTone)
                : CommentaryClip.NameClipId(name, line.NameTone);

        /// <summary>Line conditions plus, with a catalog, that its clips are rendered.</summary>
        private static readonly string OwnGoalSituation = CommentaryLineBank.SnakeCase(nameof(GoalSituation.OwnGoal));

        private bool LineUsable(CommentaryLine l, CommentaryCategory cat, CommentaryEvent e, float crowd)
        {
            var voice = cat.Voice;
            if (!l.Fits(e, crowd)) return false;
            // Never name the player behind an own goal in a line written for a scorer (context categories, analyst
            // fallbacks): only the own-goal category's lines may name him.
            if (e.Type == CommentaryEventType.Goal && e.Situation == GoalSituation.OwnGoal &&
                l.Name != CommentaryNamePlacement.None && l.NameKind == CommentaryNameKind.Player &&
                cat.Situation != OwnGoalSituation)
                return false;
            if (_catalog == null) return true;
            if (!l.NameOnly && !_catalog.Has(voice, l.ClipId)) return false;
            return l.Name == CommentaryNamePlacement.None || _catalog.Has(voice, NameClipId(l, l.SpokenName(e)));
        }

        private CommentaryLine PickLine(CommentaryCategory cat, CommentaryEvent e, float crowd, double now)
        {
            var fits = _fits;
            fits.Clear();
            var lines = cat.Lines;
            for (int i = 0; i < lines.Count; i++)
                if (LineUsable(lines[i], cat, e, crowd)) fits.Add(lines[i]);
            if (fits.Count == 0) return null;

            var fresh = _fresh;
            fresh.Clear();
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
            double now, float crowd, bool liveOnly)
        {
            var all = _bank.ForCached(e.Type, voice);
            if (all.Count == 0) return null;
            string situation = e.Type == CommentaryEventType.Goal ? SituationName(e.Situation) : null;

            // An own goal is about the mistake first: its own category beats every context (equaliser, late winner...),
            // so the defender is never praised for "scoring".
            if (e.Type == CommentaryEventType.Goal && e.Situation == GoalSituation.OwnGoal)
                foreach (var c in all)
                    if (c.Situation == situation && Usable(c, e, now, crowd, liveOnly)) return c;

            foreach (var ctx in contexts)
                foreach (var c in all)
                    if (c.Context == ctx && (c.Situation == null || c.Situation == situation) && Usable(c, e, now, crowd, liveOnly)) return c;
            if (situation != null)
                foreach (var c in all)
                    if (c.Context == null && c.Situation == situation && Usable(c, e, now, crowd, liveOnly)) return c;
            foreach (var c in all)
                if (c.Context == null && c.Situation == null && Usable(c, e, now, crowd, liveOnly)) return c;
            return null;
        }

        private bool Usable(CommentaryCategory c, CommentaryEvent e, double now, float crowd, bool liveOnly)
        {
            if (liveOnly && !c.Live) return false;
            if (!c.Resolution && crowd > c.MaxCrowd) return false;
            if (c.Cooldown > 0 && _categoryLastUsed.TryGetValue(c.Id, out double t) && now - t < c.Cooldown) return false;
            var lines = c.Lines;
            for (int i = 0; i < lines.Count; i++)
                if (LineUsable(lines[i], c, e, crowd)) return true;
            return false;
        }

        /// <summary>Tallies the scorer's goals this match (own goals excluded) into <see cref="CommentaryEvent.PlayerGoals"/>.</summary>
        private void CountGoal(CommentaryEvent e)
        {
            string key = e.PlayerId ?? e.PlayerName;
            if (e.Situation == GoalSituation.OwnGoal || string.IsNullOrEmpty(key))
            {
                e.PlayerGoals = 0;
                return;
            }
            _goalsBy.TryGetValue(key, out int n);
            _goalsBy[key] = ++n;
            e.PlayerGoals = n;
        }

        private List<string> DeriveContexts(CommentaryEvent e)
        {
            var f = _ctxFlags;
            Array.Clear(f, 0, f.Length);
            switch (e.Type)
            {
                case CommentaryEventType.Goal:
                {
                    int t = e.Team == 1 ? 1 : 0;
                    int ownAfter = t == 0 ? e.HomeScore : e.AwayScore;
                    int opp = t == 0 ? e.AwayScore : e.HomeScore;
                    int ownBefore = ownAfter - 1;
                    if (e.PlayerGoals == 3) f[CtxHatTrick] = true;
                    else if (e.PlayerGoals == 2) f[CtxBrace] = true;
                    if (e.Minute >= _config.LateWinnerMinute && ownBefore == opp) f[CtxLateWinner] = true;
                    if ((_maxDeficit[t] >= 2 && ownAfter >= opp) || (_maxDeficit[t] >= 1 && ownBefore == opp))
                        f[CtxComeback] = true;
                    if (ownAfter == opp) f[CtxEqualiser] = true;
                    if (e.Minute <= _config.EarlyGoalMinute && e.HomeScore + e.AwayScore == 1) f[CtxEarlyGoal] = true;
                    break;
                }
                case CommentaryEventType.ShotTaken:
                case CommentaryEventType.ShotSaved:
                case CommentaryEventType.ShotWide:
                case CommentaryEventType.ShotPost:
                {
                    int own = e.Team == 1 ? e.AwayScore : e.HomeScore;
                    int opp = e.Team == 1 ? e.HomeScore : e.AwayScore;
                    if (e.Minute >= _config.LateChanceMinute && own <= opp && own >= opp - 1) f[CtxLateChance] = true;
                    break;
                }
                case CommentaryEventType.KickOff:
                    if (_lastType == CommentaryEventType.Goal) f[CtxAfterGoal] = true;
                    else if (_halfTimeSeen && !_secondHalfStarted) f[CtxSecondHalf] = true;
                    break;
                case CommentaryEventType.FullTime:
                {
                    if (e.HomeScore != e.AwayScore)
                    {
                        int winner = e.HomeScore > e.AwayScore ? 0 : 1;
                        if (_maxDeficit[winner] >= 1) f[CtxComeback] = true;
                    }
                    break;
                }
                case CommentaryEventType.Lull:
                {
                    int diff = Math.Abs(e.HomeScore - e.AwayScore);
                    if (e.Minute >= _config.LateOnMinute) f[CtxLateOn] = true;
                    if (diff >= 2) f[CtxComfortable] = true;
                    else if (diff == 1) f[CtxTight] = true;
                    else if (e.HomeScore == 0) f[CtxGoalless] = true;
                    break;
                }
            }
            // Keep the documented precedence order (Contexts).
            var ordered = _contexts;
            ordered.Clear();
            for (int i = 0; i < f.Length; i++)
                if (f[i]) ordered.Add(Contexts[i]);
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
            if (e.Type != CommentaryEventType.PlayResumed && e.Type != CommentaryEventType.OnTheBall &&
                e.Type != CommentaryEventType.Lull)
                _lastType = e.Type;
        }
    }
}
