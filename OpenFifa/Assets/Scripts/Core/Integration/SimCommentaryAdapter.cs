using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Turns the sim's <see cref="MatchEvent"/> stream into <see cref="CommentaryEvent"/>s: resolves shots into
    /// saved / wide / post / goal, counts pass sequences, classifies goal situations and fills names from the roster.
    /// Pure and deterministic. Feed the output to <see cref="CommentaryEngine.Push"/> (or any other consumer).
    /// </summary>
    public sealed class SimCommentaryAdapter
    {
        /// <summary>Pass counts that trigger a PassSequence line.</summary>
        public static readonly int[] SequenceMilestones = { 6, 10, 15 };
        public const float LongRangeMetres = 22f;
        public const float TapInMetres = 6f;
        /// <summary>A goal within this many seconds of winning the ball in the own half is a counter.</summary>
        public const float CounterSeconds = 10f;
        /// <summary>A completed pass that travels at least this far across the pitch is a switch of play.</summary>
        public const float SwitchMetres = 25f;
        /// <summary>A completed pass that gains at least this much ground toward goal (ending in the attacking half) is progressive.</summary>
        public const float ProgressiveMetres = 18f;
        /// <summary>Uninterrupted possession for this long is a possession spell (one call per spell).</summary>
        public const float PossessionSpellSeconds = 20f;

        private readonly MatchSim _sim;
        private readonly string[] _names;
        private readonly string[] _ids;
        private readonly string[] _teamNames;
        private readonly List<CommentaryEvent> _out = new List<CommentaryEvent>(4);

        private int _passCount;
        private SimTeam _passTeam = SimTeam.None;
        private bool _shotPending;
        private int _shooter = -1;
        private float _shotXg;
        private float _shotDistance;
        private float _shotHeight;
        private SimTeam _shotTeam = SimTeam.None;
        private float _wonBallTime = -100f;
        private bool _wonBallInOwnHalf;
        private SimTeam _wonBallTeam = SimTeam.None;
        private RestartType _lastRestartTaken;
        /// <summary>The restart the pending shot was struck from (None in open play), read when the Shot event fires.</summary>
        private RestartType _shotRestart;
        // Build-up tracking: where the current pass was struck, and how long the side on the ball has kept it.
        private SimVec3 _passOrigin;
        private SimTeam _passOriginTeam = SimTeam.None;
        private bool _passWasThrough;
        private SimTeam _spellTeam = SimTeam.None;
        private float _spellStart;
        private bool _spellCalled;
        // The last won tackle: its winner is not called again as a "press" for the same ball.
        private int _tackleWinner = -1;
        private int _tackleTick = -1000;
        // Dribble tracking: the goal-side marker the carrier is running at, and when that was seen.
        private int _dribbler = -1;
        private int _dribbleMarker = -1;
        private int _dribbleMarkerTick;

        /// <param name="playerNames">Spoken names (roster KnownAs) per sim id; null entries are allowed.</param>
        /// <param name="playerIds">Roster ids per sim id; may be null.</param>
        /// <param name="teamNames">Spoken club names { home, away } for "{team}" lines; may be null.</param>
        public SimCommentaryAdapter(MatchSim sim, string[] playerNames = null, string[] playerIds = null,
            string[] teamNames = null)
        {
            _sim = sim;
            _names = playerNames ?? new string[MatchSimState.PlayerCount];
            _ids = playerIds ?? new string[MatchSimState.PlayerCount];
            _teamNames = teamNames ?? new string[2];
        }

        /// <summary>Displayed match minute now (each half maps to 45 minutes).</summary>
        public static int Minute(MatchSim sim) => Minute(sim.Config.HalfDurationSeconds, sim.State.Clock, sim.State.Half);

        /// <summary>Displayed match minute when <paramref name="e"/> happened (its own half clock, not the sim's current one).</summary>
        public static int Minute(MatchSim sim, in MatchEvent e) => Minute(sim.Config.HalfDurationSeconds, e.Time, e.Half);

        private static int Minute(float halfSeconds, float clock, int half)
        {
            int m = (int)(clock / halfSeconds * 45f);
            if (m > 45) m = 45;
            return m + (half >= 2 ? 45 : 0);
        }

        /// <summary>The event's sim time in seconds (its own tick, not the sim's current one).</summary>
        private float EventTime(in MatchEvent e) => (float)(e.Tick * _sim.Dt);

        /// <summary>
        /// Converts one step's events. <paramref name="now"/> is the commentary clock (monotonic seconds). Every match-time
        /// reading (won-ball and spell timers, the tackle and dribble windows, the counter window, the displayed minute)
        /// comes from the event's own tick and clock, not the sim's current state: a fast-forward batch may convert several
        /// ticks' events after the sim has run all of them, so State.Tick is later than an event in it. In live play (one
        /// Step per Convert) the two are the same.
        /// </summary>
        public IReadOnlyList<CommentaryEvent> Convert(IReadOnlyList<MatchEvent> events, double now)
        {
            _out.Clear();
            var s = _sim.State;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                switch (e.Type)
                {
                    case MatchEventType.KickOff: // the sim emits kick-offs as their own type, not RestartTaken
                        _lastRestartTaken = RestartType.KickOff;
                        RestartPossession(e);
                        Add(CommentaryEventType.KickOff, e, now, e.PlayerId);
                        break;
                    case MatchEventType.RestartTaken:
                        _lastRestartTaken = (RestartType)e.Detail;
                        RestartPossession(e);
                        Add(e.Detail == (int)RestartType.KickOff ? CommentaryEventType.KickOff : CommentaryEventType.PlayResumed, e, now, e.PlayerId);
                        break;
                    case MatchEventType.Pass:
                        if (e.Team != _passTeam) { _passTeam = e.Team; _passCount = 0; }
                        _passOrigin = e.Position;
                        _passOriginTeam = e.Team;
                        _passWasThrough = e.Action == IntentAction.ThroughPass;
                        if (e.Action == IntentAction.ThroughPass) Add(CommentaryEventType.ThroughBall, e, now, e.PlayerId);
                        break;
                    case MatchEventType.PassCompleted:
                        if (e.Team != _passTeam) { _passTeam = e.Team; _passCount = 0; }
                        _passCount++;
                        if (System.Array.IndexOf(SequenceMilestones, _passCount) >= 0)
                        {
                            var c = Add(CommentaryEventType.PassSequence, e, now, e.PlayerId);
                            c.PassCount = _passCount;
                        }
                        else
                        {
                            // Receiver on the ball: describe the pass when it changed the picture (switch, ball forward
                            // into the attacking half, a long spell on the ball), else a name call. The engine falls back to
                            // the name call when a build-up category is on cooldown, so the build-up keeps its voice.
                            var c = Add(BuildUpType(e), e, now, e.PlayerId);
                            c.PassCount = _passCount;
                        }
                        _passOriginTeam = SimTeam.None;
                        break;
                    case MatchEventType.DribbleTouch:
                        TrackDribble(e, now);
                        break;
                    case MatchEventType.WrongFooted:
                        // Player = the beaten defender, other = the carrier who beat him.
                        if (e.OtherPlayerId >= 0)
                        {
                            var d = Add(CommentaryEventType.DribblePast, e, now, e.OtherPlayerId, e.PlayerId);
                            d.Team = e.Team == SimTeam.Home ? 1 : 0;
                            d.TeamName = _teamNames[d.Team];
                        }
                        break;
                    case MatchEventType.Interception:
                    case MatchEventType.PossessionChange:
                        if (e.Team != _passTeam) { _passTeam = e.Team; _passCount = 0; }
                        if (e.Team != _wonBallTeam || e.Type == MatchEventType.Interception)
                        {
                            bool turnover = e.Team != _wonBallTeam && _wonBallTeam != SimTeam.None;
                            _wonBallTeam = e.Team;
                            _wonBallTime = EventTime(e);
                            _wonBallInOwnHalf = _sim.AttackCoord(e.Team, e.Position) < 0f;
                            // Won back in the attacking third: the press worked.
                            bool justTackled = e.PlayerId == _tackleWinner && e.Tick - _tackleTick <= _sim.Config.SecondsToTicks(2f);
                            if (turnover && !justTackled && _sim.AttackCoord(e.Team, e.Position) > _sim.Config.Pitch.HalfLength / 3f && e.PlayerId >= 0)
                                Add(CommentaryEventType.PressWon, e, now, e.PlayerId);
                        }
                        if (e.Team != _spellTeam)
                        {
                            _spellTeam = e.Team;
                            _spellStart = EventTime(e);
                            _spellCalled = false;
                        }
                        break;
                    case MatchEventType.Shot:
                        _shotPending = true;
                        _shooter = e.PlayerId;
                        _shotTeam = e.Team;
                        _shotXg = e.Value;
                        _shotHeight = e.Position.Y;
                        // The sim sets LastKickRestart in the same kick that emits Shot, so this is the shot's own restart.
                        _shotRestart = s.LastKickRestart;
                        var goal = new SimVec3(0f, 0f, _sim.AttackSign(e.Team) * _sim.Config.Pitch.HalfLength);
                        _shotDistance = SimMath.FlatDistance(e.Position, goal);
                        var st = Add(CommentaryEventType.ShotTaken, e, now, e.PlayerId);
                        st.ShotQuality = _shotXg;
                        st.DistanceMetres = _shotDistance;
                        break;
                    case MatchEventType.Save:
                        if (_shotPending) AddShot(CommentaryEventType.ShotSaved, e, now, e.PlayerId);
                        break;
                    case MatchEventType.PostHit:
                    case MatchEventType.CrossbarHit:
                        if (_shotPending) AddShot(CommentaryEventType.ShotPost, e, now, -1);
                        break;
                    case MatchEventType.OutOfPlay:
                    case MatchEventType.GoalKick:
                        if (_shotPending) AddShot(CommentaryEventType.ShotWide, e, now, -1);
                        break;
                    case MatchEventType.Corner:
                        // A shot that goes out for a corner was deflected or tipped: not "wide". The corner call covers it.
                        _shotPending = false;
                        Add(CommentaryEventType.Corner, e, now, -1);
                        break;
                    case MatchEventType.Goal:
                        AddGoal(e, now);
                        break;
                    case MatchEventType.Tackle:
                        if (e.Detail == 1)
                        {
                            Add(CommentaryEventType.Tackle, e, now, e.PlayerId);
                            _tackleWinner = e.PlayerId;
                            _tackleTick = e.Tick;
                        }
                        break;
                    case MatchEventType.Foul:
                        _shotPending = false;
                        Add(CommentaryEventType.Foul, e, now, e.PlayerId, e.OtherPlayerId);
                        break;
                    case MatchEventType.YellowCard:
                        Add(CommentaryEventType.YellowCard, e, now, e.PlayerId);
                        break;
                    case MatchEventType.RedCard:
                        Add(CommentaryEventType.RedCard, e, now, e.PlayerId);
                        break;
                    case MatchEventType.Offside:
                        Add(CommentaryEventType.Offside, e, now, e.PlayerId);
                        break;
                    case MatchEventType.HalfTime:
                        _shotPending = false;
                        Add(CommentaryEventType.HalfTime, e, now, -1);
                        break;
                    case MatchEventType.FullTime:
                        _shotPending = false;
                        Add(CommentaryEventType.FullTime, e, now, -1);
                        break;
                }
            }
            return _out;
        }

        /// <summary>
        /// A carrier beats his man: on a dribble touch, the nearest opponent goal-side of him (within 3 m ahead) is noted;
        /// when a later touch by the same carrier, within 2.5 s, finds that defender more than a metre behind the ball and
        /// still close (he was gone past rather than avoided), it is a DribblePast. Skill moves that wrong-foot a defender
        /// are called from their own event.
        /// </summary>
        private void TrackDribble(in MatchEvent e, double now)
        {
            var s = _sim.State;
            int id = e.PlayerId;
            if (id < 0 || id >= MatchSimState.PlayerCount) return;
            var team = e.Team;
            float carrier = _sim.AttackCoord(team, s.Players[id].Position);
            if (id == _dribbler && _dribbleMarker >= 0 && e.Tick - _dribbleMarkerTick <= _sim.Config.SecondsToTicks(2.5f))
            {
                var m = s.Players[_dribbleMarker].Position;
                float rel = _sim.AttackCoord(team, m) - carrier;
                if (rel < -1f && SimMath.FlatDistance(m, s.Players[id].Position) < 4.5f)
                {
                    var c = Add(CommentaryEventType.DribblePast, e, now, id, _dribbleMarker);
                    c.Team = team == SimTeam.Away ? 1 : 0;
                    c.TeamName = _teamNames[c.Team];
                    _dribbleMarker = -1;
                    return;
                }
            }
            if (id != _dribbler) { _dribbler = id; _dribbleMarker = -1; }
            int best = -1;
            float bestD = 3f;
            for (int j = 0; j < MatchSimState.PlayerCount; j++)
            {
                ref readonly var q = ref s.Players[j];
                if (q.Team == team || q.SentOff) continue;
                float rel = _sim.AttackCoord(team, q.Position) - carrier;
                if (rel < 0.3f || rel > 3f) continue;
                float d = SimMath.FlatDistance(q.Position, s.Players[id].Position);
                if (d < bestD) { bestD = d; best = j; }
            }
            if (best >= 0 && best != _dribbleMarker) { _dribbleMarker = best; _dribbleMarkerTick = e.Tick; }
        }

        /// <summary>
        /// A restart (kick-off, throw-in, goal kick, corner, free kick) hands the ball to the taker's side without a
        /// PossessionChange event: that side now has the ball (so a press that wins it back is a turnover again), the
        /// counter-attack timer restarts without an own-half win (a set piece is not a counter), and a new possession
        /// spell starts. Timed at the event's own tick (see <see cref="Convert"/>).
        /// </summary>
        private void RestartPossession(in MatchEvent e)
        {
            if (e.Team == SimTeam.None) return;
            float t = EventTime(e);
            _wonBallTeam = e.Team;
            _wonBallTime = t;
            _wonBallInOwnHalf = false;
            _spellTeam = e.Team;
            _spellStart = t;
            _spellCalled = false;
        }

        /// <summary>The build-up event for a completed pass that is not a sequence milestone (OnTheBall when nothing stands out).</summary>
        private CommentaryEventType BuildUpType(in MatchEvent e)
        {
            float t = EventTime(e);
            if (e.Team != _spellTeam)
            {
                _spellTeam = e.Team;
                _spellStart = t;
                _spellCalled = false;
            }
            if (_passOriginTeam == e.Team)
            {
                if (SimMath.Abs(e.Position.X - _passOrigin.X) >= SwitchMetres) return CommentaryEventType.SwitchOfPlay;
                float from = _sim.AttackCoord(e.Team, _passOrigin), to = _sim.AttackCoord(e.Team, e.Position);
                // A through ball already had its own call when it was played.
                if (to - from >= ProgressiveMetres && to > 0f && !_passWasThrough) return CommentaryEventType.ProgressivePass;
            }
            if (!_spellCalled && t - _spellStart >= PossessionSpellSeconds)
            {
                _spellCalled = true;
                return CommentaryEventType.PossessionSpell;
            }
            return CommentaryEventType.OnTheBall;
        }

        private CommentaryEvent Add(CommentaryEventType type, in MatchEvent e, double now, int player, int other = -1)
        {
            var s = _sim.State;
            var c = new CommentaryEvent
            {
                Type = type,
                Time = now,
                Minute = Minute(_sim, e),
                Team = e.Team == SimTeam.Away ? 1 : 0,
                TeamName = _teamNames[e.Team == SimTeam.Away ? 1 : 0],
                HomeScore = s.HomeScore,
                AwayScore = s.AwayScore,
            };
            if (player >= 0 && player < MatchSimState.PlayerCount)
            {
                c.PlayerId = _ids[player];
                c.PlayerName = _names[player];
            }
            if (other >= 0 && other < MatchSimState.PlayerCount)
            {
                c.SecondaryPlayerId = _ids[other];
                c.SecondaryPlayerName = _names[other];
            }
            _out.Add(c);
            return c;
        }

        private void AddShot(CommentaryEventType type, in MatchEvent e, double now, int keeper)
        {
            _shotPending = false;
            var c = Add(type, e, now, _shooter, keeper);
            c.Team = _shotTeam == SimTeam.Away ? 1 : 0;
            c.TeamName = _teamNames[c.Team];
            c.ShotQuality = _shotXg;
            c.DistanceMetres = _shotDistance;
        }

        private void AddGoal(in MatchEvent e, double now)
        {
            bool ownGoal = e.Detail == 1;
            var c = Add(CommentaryEventType.Goal, e, now, e.PlayerId, e.OtherPlayerId);
            if (_shotPending && _shotTeam == e.Team)
            {
                c.ShotQuality = _shotXg;
                c.DistanceMetres = _shotDistance;
            }
            float t = EventTime(e);
            bool fromShot = _shotPending && _shotTeam == e.Team && e.PlayerId == _shooter;
            GoalSituation sit = GoalSituation.OpenPlay;
            if (ownGoal) sit = GoalSituation.OwnGoal;
            // Penalty / free kick only when the goal is that restart's own shot. LastKickRestart alone is sticky: only kicks
            // reset it, so a goal dribbled or deflected in after a saved penalty or a short free kick would keep it.
            else if (fromShot && _shotRestart == RestartType.Penalty) sit = GoalSituation.Penalty;
            else if (fromShot && _shotRestart == RestartType.DirectFreeKick) sit = GoalSituation.FreeKick;
            else if (_shotPending && _shotHeight > SimAnimActionTracker.HeaderHeight) sit = GoalSituation.Header;
            else if (_shotPending && _shotDistance >= LongRangeMetres) sit = GoalSituation.LongRange;
            else if (_wonBallTeam == e.Team && _wonBallInOwnHalf && t - _wonBallTime <= CounterSeconds) sit = GoalSituation.Counter;
            else if (_shotPending && _shotDistance <= TapInMetres) sit = GoalSituation.TapIn;
            c.Situation = sit;
            _shotPending = false;
            _shotRestart = RestartType.None;
            _passCount = 0;
        }
    }
}
