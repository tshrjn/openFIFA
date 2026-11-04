using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>Supplies the user's control intent once per sim tick (gamepad, touch, a test bot...).</summary>
    public interface IHumanInputSource
    {
        /// <summary>
        /// Called once per sim step before the step. <paramref name="ctx"/> is the control context for the controlled
        /// player; feed it to the gamepad/touch resolver and return its intent.
        /// </summary>
        ControlIntent Read(SinglePlayerMatchSession session, HumanSlot slot, in ControlContext ctx);
    }

    /// <summary>
    /// One user-controlled seat: a team, the player selector for that seat and its input (gamepad/touch resolver or a
    /// bot). Single player uses one slot; local co-op adds more (same or opposite teams).
    /// </summary>
    public sealed class HumanSlot
    {
        public readonly SimTeam Team;
        public readonly HumanPlayerSelector Selector;
        public IHumanInputSource Input;
        /// <summary>The control intent applied on the last step.</summary>
        public ControlIntent LastControlIntent;

        public HumanSlot(SimTeam team, HumanPlayerSelector selector, IHumanInputSource input = null)
        {
            Team = team;
            Selector = selector;
            Input = input;
        }

        public int ControlledId => Selector.ControlledId;
    }

    /// <summary>Optional receiver for commentary events (the coordinator can plug any consumer in).</summary>
    public interface ICommentaryEventSink
    {
        void OnCommentaryEvent(CommentaryEvent e);
    }

    /// <summary>Start options for <see cref="SinglePlayerMatchSession"/>.</summary>
    public sealed class SinglePlayerMatchOptions
    {
        public TeamAIDifficultyLevel Level = TeamAIDifficultyLevel.Professional;
        public SimTeam HumanTeam = SimTeam.Home;
        public ulong Seed = 1UL;
        /// <summary>Null = <see cref="MatchSetup.CreateDefault"/>(Seed). The session copies attributes before changing them.</summary>
        public MatchSetup Setup;
        public MatchSimConfig Config;
        /// <summary>Null = fitted to the setup's kick-off shape.</summary>
        public TeamAITactics HomeTactics;
        public TeamAITactics AwayTactics;
        /// <summary>Spoken/HUD names and roster ids per sim id (0-21); optional.</summary>
        public string[] PlayerNames;
        public string[] PlayerIds;
        /// <summary>Camera yaw (radians) used to map the stick to the pitch.</summary>
        public float CameraYaw;
        /// <summary>Longest frame time the accumulator accepts (s); longer hitches are dropped to avoid a spiral.</summary>
        public float MaxFrameTime = 0.25f;
    }

    /// <summary>Final (or current) result from the user's point of view.</summary>
    public struct SinglePlayerMatchResult
    {
        public TeamAIDifficultyLevel Level;
        public SimTeam HumanTeam;
        public int HomeScore;
        public int AwayScore;
        public int HomeShots;
        public int AwayShots;
        /// <summary>Home share of possession 0..1.</summary>
        public float HomePossession;
        public bool Finished;

        public int HumanGoals => HumanTeam == SimTeam.Home ? HomeScore : AwayScore;
        public int AIGoals => HumanTeam == SimTeam.Home ? AwayScore : HomeScore;
        /// <summary>+1 user won, 0 draw, -1 AI won.</summary>
        public int Outcome => Math.Sign(HumanGoals - AIGoals);
    }

    /// <summary>
    /// One single-player match: the user controls one player of <see cref="SinglePlayerMatchOptions.HumanTeam"/>
    /// (selected by <see cref="HumanPlayerSelector"/>), the rest of that team runs on team AI at the level's teammate
    /// theta, and the other team runs on team AI at the opponent theta. Fixed-step accumulator, pause and result.
    /// Pure C#: the MonoBehaviour calls <see cref="Advance"/> every frame; tests call <see cref="StepOnce"/>.
    /// </summary>
    public sealed class SinglePlayerMatchSession
    {
        public readonly SinglePlayerMatchOptions Options;
        public readonly SinglePlayerDifficultyProfile Profile;
        public readonly MatchSim Sim;
        public readonly SimTeamAIAdapter AI;
        public readonly TeamAIDirector Director;
        /// <summary>User seats. Slot 0 is the primary user; co-op code may add more with <see cref="AddHumanSlot"/>.</summary>
        public readonly List<HumanSlot> HumanSlots = new List<HumanSlot>(2);
        public readonly SimAnimActionTracker Animation;
        public readonly SimCommentaryAdapter Commentary;

        private readonly PlayerIntent[] _intents = new PlayerIntent[MatchSimState.PlayerCount];
        private readonly List<MatchEvent> _frameEvents = new List<MatchEvent>(64);
        private readonly List<CommentaryEvent> _frameCommentary = new List<CommentaryEvent>(8);
        private double _accumulator;
        private ICommentaryEventSink _commentarySink;
        private CommentaryEngine _commentaryEngine;
        private readonly List<CommentaryCue> _frameCues = new List<CommentaryCue>(4);

        public SinglePlayerMatchSession(SinglePlayerMatchOptions options)
        {
            Options = options ?? new SinglePlayerMatchOptions();
            if (Options.HumanTeam == SimTeam.None) throw new ArgumentException("HumanTeam must be Home or Away");
            Profile = SinglePlayerDifficulty.For(Options.Level);
            var cfg = Options.Config ?? new MatchSimConfig();
            var setup = PrepareSetup(Options.Setup ?? MatchSetup.CreateDefault(Options.Seed), Options.Seed, Options.HumanTeam, Profile);

            Sim = new MatchSim(cfg, setup);
            AI = new SimTeamAIAdapter(Sim, Options.HomeTactics, Options.AwayTactics);
            // The ladder profile (thetas + opponent reaction bonus) goes to the team AI as one unit.
            Director = TeamAIDirector.ForSinglePlayer(cfg.Pitch, AI.HomeTactics, AI.AwayTactics,
                SimTeamAIAdapter.TeamIndex(Options.HumanTeam), Profile.Ladder, setup.Seed);
            // AI ball timing must come from this match's ball and kick config, never the defaults.
            var ballModel = new TeamAIBallModel(cfg.Ball, cfg.Actions);
            Director.Brains[0].BallModel = ballModel;
            Director.Brains[1].BallModel = ballModel;
            AddHumanSlot(Options.HumanTeam);
            // The AI side has a manager on the touchline: it changes mentality with the score and the clock. The human's
            // AI team-mates keep the user's tactics.
            Director.Brains[SimTeamAIAdapter.TeamIndex(AITeam)].Manager.Enabled = true;
            Animation = new SimAnimActionTracker();
            Commentary = new SimCommentaryAdapter(Sim, Options.PlayerNames, Options.PlayerIds);
            CameraYaw = Options.CameraYaw;
            SavePreviousSnapshot();
        }

        /// <summary>
        /// Copies the setup and applies the level's goalkeeper adjustments (1-99 clamped).
        /// The caller's setup is not modified.
        /// </summary>
        public static MatchSetup PrepareSetup(MatchSetup src, ulong seed, SimTeam humanTeam, in SinglePlayerDifficultyProfile profile)
        {
            var s = new MatchSetup
            {
                Seed = src.Seed != 0 ? src.Seed : seed,
                HomeFormation = (SimVec3[])src.HomeFormation.Clone(),
                AwayFormation = (SimVec3[])src.AwayFormation.Clone(),
                FirstKickOff = src.FirstKickOff,
                KickOffTakerSlot = src.KickOffTakerSlot,
            };
            for (int i = 0; i < MatchSetup.PlayersPerTeam; i++)
            {
                s.Home[i] = src.Home[i].Clone();
                s.Away[i] = src.Away[i].Clone();
            }
            var human = humanTeam == SimTeam.Home ? s.Home : s.Away;
            var ai = humanTeam == SimTeam.Home ? s.Away : s.Home;
            Keeper(ai[0], profile.OpponentKeeperReflexDelta);
            Keeper(human[0], profile.HumanKeeperReflexDelta);
            return s;
        }

        private static void Keeper(SimPlayerAttributes gk, int delta)
        {
            gk.GkReflexes = Clamp99(gk.GkReflexes + delta);
            gk.GkDiving = Clamp99(gk.GkDiving + delta);
        }

        private static int Clamp99(int v) => v < 1 ? 1 : (v > 99 ? 99 : v);

        /// <summary>
        /// Adds a user seat on <paramref name="team"/>, starting on the free outfielder nearest the ball. Returns it.
        /// AI theta for that team stays as constructed (teammate theta for the primary team, opponent theta otherwise).
        /// </summary>
        public HumanSlot AddHumanSlot(SimTeam team, IHumanInputSource input = null)
        {
            int start = HumanPlayerSelector.InitialPlayer(Sim, team);
            for (int k = 0; k < HumanSlots.Count; k++)
                if (HumanSlots[k].ControlledId == start) start = NextFree(team, start);
            var slot = new HumanSlot(team, new HumanPlayerSelector(team, start), input);
            HumanSlots.Add(slot);
            return slot;
        }

        private int NextFree(SimTeam team, int taken)
        {
            int first = MatchSim.FirstId(team);
            for (int id = first + 1; id < first + MatchSetup.PlayersPerTeam; id++)
            {
                bool used = id == taken;
                for (int k = 0; k < HumanSlots.Count && !used; k++) used = HumanSlots[k].ControlledId == id;
                if (!used) return id;
            }
            return taken;
        }

        /// <summary>
        /// Bit i set: player i is user-controlled this tick. A slot without an input is left to the team AI
        /// (attract mode, or a disconnected pad), so its player is not masked.
        /// </summary>
        public long HumanMask
        {
            get
            {
                long m = 0;
                for (int k = 0; k < HumanSlots.Count; k++)
                    if (HumanSlots[k].ControlledId >= 0 && HumanSlots[k].Input != null) m |= 1L << HumanSlots[k].ControlledId;
                return m;
            }
        }

        /// <summary>Primary slot's selector (single-player convenience).</summary>
        public HumanPlayerSelector Selector => HumanSlots[0].Selector;
        public SimTeam HumanTeam => Options.HumanTeam;
        public SimTeam AITeam => MatchSim.Opponent(Options.HumanTeam);
        public int ControlledId => Selector.ControlledId;
        public bool Paused { get; private set; }
        public bool IsFinished => Sim.Phase == SimMatchPhase.FullTime;
        /// <summary>Camera yaw (radians) for stick mapping; the presentation updates it as the camera moves.</summary>
        public float CameraYaw { get; set; }
        /// <summary>Monotonic sim time (tick * dt).</summary>
        public float SimTime => Sim.Tick * Sim.Dt;
        /// <summary>Fraction of a tick left in the accumulator (0..1), for render interpolation.</summary>
        public float InterpolationAlpha => (float)(_accumulator / Sim.Dt);
        /// <summary>The control intent applied on the last step.</summary>
        public ControlIntent LastControlIntent => HumanSlots[0].LastControlIntent;
        /// <summary>Sim intents sent on the last step (index = player id).</summary>
        public IReadOnlyList<PlayerIntent> LastIntents => _intents;
        /// <summary>All sim events of the steps run by the last <see cref="Advance"/> (or the last <see cref="StepOnce"/>).</summary>
        public IReadOnlyList<MatchEvent> FrameEvents => _frameEvents;
        /// <summary>Commentary events produced in the same window.</summary>
        public IReadOnlyList<CommentaryEvent> FrameCommentary => _frameCommentary;
        /// <summary>Cues returned by an attached <see cref="CommentaryEngine"/> in the same window.</summary>
        public IReadOnlyList<CommentaryCue> FrameCues => _frameCues;

        public void Pause() => Paused = true;
        public void Resume() => Paused = false;
        public void TogglePause() => Paused = !Paused;

        /// <summary>Route converted commentary events to a sink (e.g. the coordinator's audio bridge).</summary>
        public void SetCommentarySink(ICommentaryEventSink sink) => _commentarySink = sink;

        /// <summary>Push commentary events straight into an engine; its cues land in <see cref="FrameCues"/>.</summary>
        public void AttachCommentaryEngine(CommentaryEngine engine) => _commentaryEngine = engine;

        /// <summary>Control context for the controlled player right now.</summary>
        public ControlContext CurrentContext(int slot = 0) =>
            HumanControlAdapter.BuildContext(Sim, HumanSlots[slot].Team, HumanSlots[slot].ControlledId);

        public SinglePlayerMatchResult Result
        {
            get
            {
                var s = Sim.State;
                int total = s.HomePossessionTicks + s.AwayPossessionTicks;
                return new SinglePlayerMatchResult
                {
                    Level = Profile.Level,
                    HumanTeam = HumanTeam,
                    HomeScore = s.HomeScore,
                    AwayScore = s.AwayScore,
                    HomeShots = s.HomeShots,
                    AwayShots = s.AwayShots,
                    HomePossession = total > 0 ? s.HomePossessionTicks / (float)total : 0.5f,
                    Finished = IsFinished,
                };
            }
        }

        /// <summary>
        /// Advance by real frame time. Runs zero or more fixed sim steps; returns how many ran. A non-null
        /// <paramref name="input"/> replaces slot 0's input (single-player convenience).
        /// <paramref name="commentaryNow"/> is the commentary clock (monotonic real seconds); pass a negative value to use sim time.
        /// </summary>
        public int Advance(double frameSeconds, IHumanInputSource input, double commentaryNow = -1)
        {
            _frameEvents.Clear();
            _frameCommentary.Clear();
            _frameCues.Clear();
            if (input != null) HumanSlots[0].Input = input;
            if (Paused || IsFinished) return 0;
            if (frameSeconds > Options.MaxFrameTime) frameSeconds = Options.MaxFrameTime;
            if (frameSeconds < 0) frameSeconds = 0;
            _accumulator += frameSeconds;
            int steps = 0;
            double dt = Sim.Dt;
            while (_accumulator + 1e-9 >= dt && !IsFinished)
            {
                _accumulator -= dt;
                StepInternal(commentaryNow);
                steps++;
            }
            // The engine's Tick/Push results are views it reuses on the next call: copy them (AddRange), never keep them.
            if (_commentaryEngine != null) _frameCues.AddRange(_commentaryEngine.Tick(commentaryNow >= 0 ? commentaryNow : SimTime));
            return steps;
        }

        /// <summary>Run exactly one sim step (ignores pause). Clears and refills the frame lists.</summary>
        public IReadOnlyList<MatchEvent> StepOnce(IHumanInputSource input)
        {
            _frameEvents.Clear();
            _frameCommentary.Clear();
            _frameCues.Clear();
            if (input != null) HumanSlots[0].Input = input;
            if (!IsFinished) StepInternal(-1);
            return _frameEvents;
        }

        /// <summary>Diagnostics: AI shot decisions latched (one per decision tick), released to the sim, and dropped (window never opened or the AI changed its mind).</summary>
        public int AIShotsLatched, AIShotsReleased, AIShotsDropped;

        /// <summary>How long an AI kick or tackle decision waits for its window (s).</summary>
        public const float AIKickLatchSeconds = 0.6f;
        /// <summary>
        /// How long a shot decision waits (s). A carrier at pace has the ball up to a stride ahead, so the strike window
        /// can take longer than a pass's to open; dropping the shot after 0.6 s threw away about a third of them.
        /// </summary>
        public const float AIShotLatchSeconds = 1.0f;
        private readonly PlayerIntent[] _aiKick = new PlayerIntent[MatchSimState.PlayerCount];
        private readonly int[] _aiKickUntil = NewFilled(MatchSimState.PlayerCount, -1);

        private static int[] NewFilled(int n, int v)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = v;
            return a;
        }

        private readonly ControlIntent[] _slotIntents = new ControlIntent[8];

        /// <summary>Player positions of the previous fixed-step snapshot (render interpolation: lerp(prev, current, alpha)).</summary>
        public readonly SimVec3[] PreviousPositions = new SimVec3[MatchSimState.PlayerCount];
        /// <summary>Player facings (radians) of the previous snapshot.</summary>
        public readonly float[] PreviousFacing = new float[MatchSimState.PlayerCount];
        /// <summary>Ball position of the previous snapshot.</summary>
        public SimVec3 PreviousBall { get; private set; }

        private void SavePreviousSnapshot()
        {
            var st = Sim.State;
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                PreviousPositions[i] = st.Players[i].Position;
                PreviousFacing[i] = st.Players[i].Facing;
            }
            PreviousBall = st.Ball.Position;
        }

        private void StepInternal(double commentaryNow)
        {
            SavePreviousSnapshot();
            long humanMask = HumanMask;
            AI.Fill(humanMask);
            bool decided = Director.Tick(AI.World, Sim.Dt);

            int n = HumanSlots.Count;
            for (int k = 0; k < n; k++)
            {
                var slot = HumanSlots[k];
                var ctx = HumanControlAdapter.BuildContext(Sim, slot.Team, slot.ControlledId);
                ControlIntent ci = slot.Input != null ? slot.Input.Read(this, slot, ctx) : default;
                // Play stopped: walk a seat player who wandered into the run-off back on (the team AI only repositions its own).
                if (slot.Input != null) SeatRestartGuard.Apply(Sim, slot.ControlledId, CameraYaw, ref ci);
                slot.LastControlIntent = ci;
                if (k < _slotIntents.Length) _slotIntents[k] = ci;
            }

            var state = Sim.State;
            for (int id = 0; id < MatchSimState.PlayerCount; id++)
            {
                if ((humanMask & (1L << id)) != 0)
                {
                    _intents[id] = PlayerIntent.Idle; // masked: AI commands are ignored for user-controlled players
                    _aiKickUntil[id] = -1;
                    continue;
                }
                var intent = SimTeamAIAdapter.ToIntent(Director.Commands[id], state.Players[id], decided);
                // A latched shot is a commitment: a later decision tick that merely prefers to carry (decision noise flips
                // close calls) does not cancel it; only a new kick replaces it, and losing the ball ends it.
                bool shotCommitted = _aiKickUntil[id] >= state.Tick && PlayerIntent.IsShotAction(_aiKick[id].Action) &&
                                     state.Owner == id && !intent.IsKick;
                if (decided && !shotCommitted)
                {
                    if (_aiKickUntil[id] >= state.Tick && PlayerIntent.IsShotAction(_aiKick[id].Action) && !PlayerIntent.IsShotAction(intent.Action))
                        AIShotsDropped++;
                    _aiKickUntil[id] = -1;
                    if ((intent.IsKick || intent.Action == IntentAction.Tackle || intent.Action == IntentAction.SlideTackle) &&
                        !state.Players[id].IsFree)
                    {
                        // Already winding up or recovering: the kick in progress is the decision; re-latching it
                        // here left a stale latch behind the real shot (counted as dropped when it expired).
                        intent.Action = IntentAction.None;
                    }
                    else if (intent.IsKick || intent.Action == IntentAction.Tackle || intent.Action == IntentAction.SlideTackle)
                    {
                        // Latch the kick and release it when the strike window opens (see KickTiming).
                        if (PlayerIntent.IsShotAction(intent.Action)) AIShotsLatched++;
                        _aiKick[id] = intent;
                        float latch = PlayerIntent.IsShotAction(intent.Action) ? AIShotLatchSeconds : AIKickLatchSeconds;
                        _aiKickUntil[id] = state.Tick + Sim.Config.SecondsToTicks(latch);
                        intent.Action = IntentAction.None;
                    }
                }
                if (_aiKickUntil[id] >= state.Tick)
                {
                    var k = _aiKick[id];
                    bool open = k.IsKick
                        ? KickTiming.StrikeWindowOpen(Sim, id, KickTiming.WindupSeconds(Sim.Config.Actions, k.Action, k.Power))
                        : KickTiming.TackleWindowOpen(Sim, id, k.Action);
                    if (open)
                    {
                        if (PlayerIntent.IsShotAction(k.Action)) AIShotsReleased++;
                        intent.Action = k.Action;
                        intent.Power = k.Power;
                        intent.Aim = k.Aim;
                        intent.TargetPlayerId = k.TargetPlayerId;
                        _aiKickUntil[id] = -1;
                    }
                    else if (_aiKickUntil[id] == state.Tick && PlayerIntent.IsShotAction(k.Action))
                    {
                        AIShotsDropped++;
                    }
                    else
                    {
                        // Gather the ball first: run onto it at a controlled pace so the window opens.
                        SimVec3 to = (state.Ball.Position + state.Ball.Velocity.Flat * 0.15f - state.Players[id].Position).Flat;
                        float d = to.Magnitude;
                        if (d > 0.05f) intent.Move = to / d;
                        intent.Sprint = d > 2.5f;
                    }
                }
                _intents[id] = intent;
            }

            ApplyAIReceiverRun(humanMask);

            for (int k = 0; k < n; k++)
            {
                var slot = HumanSlots[k];
                int controlled = slot.ControlledId;
                if ((slot.LastControlIntent.Held & ControlHeld.TeammatePress) != 0) ApplyTeammatePress(slot.Team, humanMask);
                if (controlled >= 0 && slot.Input != null)
                {
                    _intents[controlled] = HumanControlAdapter.ToIntent(slot.LastControlIntent, CameraYaw, Sim, controlled,
                        slot.Selector.Teammates, Profile.PassAssist, Profile.ShotAssist);
                    HumanControlAdapter.ApplyReceiveAssist(Sim, controlled, ref _intents[controlled]);
                }
            }

            for (int id = 0; id < MatchSimState.PlayerCount; id++) SimTeamAIAdapter.SteerWindUpToBall(Sim, id, ref _intents[id]);

            var events = Sim.Step(_intents);

            for (int k = 0; k < n; k++)
            {
                var slot = HumanSlots[k];
                var ci = slot.LastControlIntent;
                if (ci.Action == ControlAction.SwitchPlayer && ci.DirectionalSwitch)
                    slot.Selector.SetFlickDirection(HumanControlAdapter.StickToPitch(ci.Aim, CameraYaw));
                long others = HumanMask & ~(slot.ControlledId >= 0 ? 1L << slot.ControlledId : 0L);
                slot.Selector.Update(Sim, events, ci, Profile.AutoSwitchOnReception, Profile.AutoSwitchDefenceDistance, others);
            }
            Animation.Update(Sim, events);

            for (int i = 0; i < events.Count; i++) _frameEvents.Add(events[i]);
            if (events.Count > 0)
            {
                double now = commentaryNow >= 0 ? commentaryNow : SimTime;
                var ce = Commentary.Convert(events, now);
                for (int i = 0; i < ce.Count; i++)
                {
                    _frameCommentary.Add(ce[i]);
                    _commentarySink?.OnCommentaryEvent(ce[i]);
                    if (_commentaryEngine != null) _frameCues.AddRange(_commentaryEngine.Push(ce[i]));
                }
            }
        }

        /// <summary>
        /// The sim leads a pass to where the receiver will be if he keeps his run (solver flight time), but the team AI
        /// re-plans the receiver at 10 Hz and often pulls him back to shape. While a pass to an AI player is live, run
        /// him onto the ball's predicted path instead (the sim's own ball prediction), so passes are actually received.
        /// </summary>
        private void ApplyAIReceiverRun(long humanMask)
        {
            var s = Sim.State;
            int r = s.PassTarget;
            if (!s.PassLive || r < 0 || s.Owner >= 0 || s.Phase != SimMatchPhase.InPlay) return;
            if ((humanMask & (1L << r)) != 0 || !s.Players[r].IsFree) return;
            if (MatchSim.TeamOf(r) != s.PassTeam) return;
            SimVec3 meet = KickTiming.InterceptPoint(Sim, r, out float t);
            SimVec3 to = (meet - s.Players[r].Position).Flat;
            float d = to.Magnitude;
            if (d < 0.3f) { _intents[r].Move = SimVec3.Zero; return; }
            _intents[r].Move = to / d * SimMath.Clamp(d / 1.5f, 0.3f, 1f);
            _intents[r].Sprint = d > 4f && d / SimMath.Max(t, 0.1f) > 5f;
            AIReceiverRunTicks++;
        }

        /// <summary>Diagnostics: ticks in which an AI receiver was routed onto a live pass.</summary>
        public int AIReceiverRunTicks;

        /// <summary>Held teammate press: the nearest AI teammate to the ball sprints at it.</summary>
        private void ApplyTeammatePress(SimTeam team, long humanMask)
        {
            var s = Sim.State;
            if (s.Owner < 0 || MatchSim.TeamOf(s.Owner) == team) return;
            int first = MatchSim.FirstId(team);
            int best = -1;
            float bestD = float.MaxValue;
            for (int id = first + 1; id < first + MatchSetup.PlayersPerTeam; id++)
            {
                if ((humanMask & (1L << id)) != 0 || s.Players[id].SentOff) continue;
                float d = SimMath.FlatDistance(s.Players[id].Position, s.Ball.Position);
                if (d < bestD) { bestD = d; best = id; }
            }
            if (best < 0) return;
            SimVec3 to = (s.Ball.Position - s.Players[best].Position).Flat;
            if (to.SqrMagnitude < 1e-4f) return;
            _intents[best] = PlayerIntent.MoveOnly(to.Normalized, true);
            if (to.Magnitude < 1.6f) { _intents[best].Action = IntentAction.Jockey; }
        }
    }
}
