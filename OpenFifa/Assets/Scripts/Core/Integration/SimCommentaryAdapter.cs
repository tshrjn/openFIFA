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

        /// <summary>Displayed match minute (each half maps to 45 minutes).</summary>
        public static int Minute(MatchSim sim)
        {
            var s = sim.State;
            float half = sim.Config.HalfDurationSeconds;
            int m = (int)(s.Clock / half * 45f);
            if (m > 45) m = 45;
            return m + (s.Half >= 2 ? 45 : 0);
        }

        /// <summary>Converts one step's events. <paramref name="now"/> is the commentary clock (monotonic seconds).</summary>
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
                        Add(CommentaryEventType.KickOff, e, now, e.PlayerId);
                        break;
                    case MatchEventType.RestartTaken:
                        _lastRestartTaken = (RestartType)e.Detail;
                        Add(e.Detail == (int)RestartType.KickOff ? CommentaryEventType.KickOff : CommentaryEventType.PlayResumed, e, now, e.PlayerId);
                        break;
                    case MatchEventType.Pass:
                        if (e.Team != _passTeam) { _passTeam = e.Team; _passCount = 0; }
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
                        // Receiver on the ball: name calls carry the build-up (the engine rate-limits them).
                        else Add(CommentaryEventType.OnTheBall, e, now, e.PlayerId).PassCount = _passCount;
                        break;
                    case MatchEventType.Interception:
                    case MatchEventType.PossessionChange:
                        if (e.Team != _passTeam) { _passTeam = e.Team; _passCount = 0; }
                        if (e.Team != _wonBallTeam || e.Type == MatchEventType.Interception)
                        {
                            _wonBallTeam = e.Team;
                            _wonBallTime = (float)(s.Tick * _sim.Dt);
                            _wonBallInOwnHalf = _sim.AttackCoord(e.Team, e.Position) < 0f;
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
                        if (_shotPending) AddShot(CommentaryEventType.ShotWide, e, now, -1);
                        Add(CommentaryEventType.Corner, e, now, -1);
                        break;
                    case MatchEventType.Goal:
                        AddGoal(e, now);
                        break;
                    case MatchEventType.Tackle:
                        if (e.Detail == 1) Add(CommentaryEventType.Tackle, e, now, e.PlayerId);
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

        private CommentaryEvent Add(CommentaryEventType type, in MatchEvent e, double now, int player, int other = -1)
        {
            var s = _sim.State;
            var c = new CommentaryEvent
            {
                Type = type,
                Time = now,
                Minute = Minute(_sim),
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
            float t = (float)(_sim.State.Tick * _sim.Dt);
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
