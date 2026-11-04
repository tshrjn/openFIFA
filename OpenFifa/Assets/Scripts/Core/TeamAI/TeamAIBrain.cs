using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Team AI for one side: phase machine, formation anchors and per-player utility decisions
    /// (on-ball, off-ball attacking, defending, goalkeeper). Call Decide at ~10 Hz (TeamAIDirector does this).
    /// All working memory is preallocated; Decide allocates nothing.
    /// </summary>
    public sealed class TeamAIBrain
    {
        public const float FreeKickDistance = 9.15f;
        public const float HeavyTouchDistance = 1.6f;
        /// <summary>Highest ball an outfielder can play (m), for interception points.</summary>
        public const float InterceptHeight = 1.9f;
        /// <summary>
        /// Seconds added to the straight-line time to intercept when picking a loose-ball chase point: that model starts
        /// at top speed, real players accelerate and turn. Without it the chaser aims for the last point he could
        /// just reach and arrives late (seen in full-sim matches once ball prediction became exact).
        /// </summary>
        public const float ChaseAccelerationLag = 0.3f;
        /// <summary>Scale of the adjustment drift while the ball is dead (set-piece holders, routine runners).</summary>
        public const float SetPieceDriftScale = 0.6f;
        /// <summary>Team-mates holding shape keep at least this far (m) from our carrier: support comes from 8-15 m.</summary>
        public const float CarrierSpacing = 8f;
        /// <summary>Score bonus for keeping last tick's support spot (relative to the carrier).</summary>
        public const float SupportSpotStickiness = 0.25f;
        /// <summary>Score cost per metre between a supporter and a candidate support spot.</summary>
        public const float SupportTravelCost = 0.015f;
        /// <summary>Score cost of a support spot straight down a running carrier's line (within ~30 deg).</summary>
        public const float SupportLanePenalty = 0.4f;

        public readonly int Team;
        /// <summary>The team's instructions (user or roster). The brain plays <see cref="Effective"/>: these plus the manager's mentality.</summary>
        public TeamAITactics Tactics;
        /// <summary>Tactics in force this tick: <see cref="Tactics"/> shifted by the manager's mentality.</summary>
        public readonly TeamAITactics Effective = new TeamAITactics();
        /// <summary>In-match mentality changes from score and clock (off unless the session enables it).</summary>
        public readonly TeamAIManager Manager = new TeamAIManager();
        /// <summary>Seconds a counter-attack lasts after winning the ball (scaled by <see cref="TeamAITactics.CounterBoost"/>).</summary>
        public const float CounterWindow = 6f;
        /// <summary>The side won the ball recently and is breaking (see <see cref="TeamAITactics.CounterAttack"/>).</summary>
        public bool CounterActive { get; private set; }
        private float _regainTime = -100f;
        private bool _hadBall;
        /// <summary>Ball depth below which a counter-attack can start, and up to which a running one carries on.</summary>
        public const float CounterStartDepth = 0.78f;
        public const float CounterEndDepth = 0.82f;
        /// <summary>
        /// Rest defence: outfielders who stay behind the ball. A late midfield run, or an early overlap by a
        /// commit-everyone side, only goes if at least this many team-mates are still behind the ball.
        /// </summary>
        public const int RestDefenders = 3;
        private int _restBehind;

        /// <summary>
        /// 0..1: how hard a pressing side (PressingIntensity above 0.5) squeezes while it is in its high press or
        /// counter-press; 0 in every other phase and for neutral or passive sides. Drives tighter cover shadows on the
        /// nearest passing options (to intercept), a closer second presser and earlier tackles.
        /// </summary>
        public float PressSqueeze =>
            Phase == TeamAIPhase.HighPress || Phase == TeamAIPhase.CounterPress
                ? TeamAIMath.Clamp01((Effective.PressingIntensity - 0.5f) * 2f)
                : 0f;
        /// <summary>Cover-shadow point: this far from the ball toward the receiver (0.4), up to 0.75 at full squeeze.</summary>
        public const float ShadowFraction = 0.4f;
        public const float SqueezeShadowFraction = 0.75f;
        /// <summary>
        /// How much lower a win chance a squeezing presser accepts before tackling (at full squeeze). 0.12 gave the
        /// lowest PPDA but 1.8x the fouls of a balanced side; 0.05 keeps fouls at 1.4x and PPDA at 0.7x Balanced.
        /// </summary>
        public const float SqueezeTackleShift = 0.05f;
        public TeamAIDifficulty Difficulty { get; private set; }
        public readonly TeamAIRandom Rng;
        public readonly TeamAIPhaseMachine PhaseMachine = new TeamAIPhaseMachine();
        public readonly TeamAIOnBall OnBall = new TeamAIOnBall();
        /// <summary>Corner, penalty and near-box free-kick routines (planned while a restart is pending).</summary>
        public readonly TeamAISetPieces SetPieces = new TeamAISetPieces();
        /// <summary>This tick's sim-matched free-ball prediction (built when the ball is loose).</summary>
        /// <summary>
        /// This decision tick's free-ball trajectory. <see cref="TeamAIDirector"/> gives both brains the same instance,
        /// so it is integrated once per tick instead of once per team (Build is cached per time, ball state and model).
        /// </summary>
        public TeamAIBallTrajectory Trajectory { get; set; } = new TeamAIBallTrajectory();
        /// <summary>A set-piece routine is running this tick.</summary>
        public bool SetPieceRoutineActive { get; private set; }

        private TeamAIBallModel _ballModel = TeamAIBallModel.Default;

        /// <summary>Ball timing model; set it when the sim runs a non-default <see cref="BallSimConfig"/>.</summary>
        public TeamAIBallModel BallModel
        {
            get => _ballModel;
            set { _ballModel = value ?? TeamAIBallModel.Default; OnBall.Model = _ballModel; SetPieces.Model = _ballModel; }
        }
        /// <summary>Formation anchors by slot (0..10), world space.</summary>
        public readonly SimVec3[] Anchors = new SimVec3[TeamAIFormations.SlotCount];
        /// <summary>
        /// Velocity of each shape spot (m/s, flat): how fast the block slides with the ball. Holders get it as their move
        /// target's velocity so they flow with the shape (see <see cref="TeamAIShapeMotion"/>).
        /// </summary>
        public readonly SimVec3[] AnchorVelocities = new SimVec3[TeamAIFormations.SlotCount];
        /// <summary>Where shape holders actually stand: the anchors followed with a lag at the slide speed (see UpdateShapeSpots).</summary>
        public readonly SimVec3[] ShapeSpots = new SimVec3[TeamAIFormations.SlotCount];
        private bool _shapePrimed;
        private float _shapeLineZ;
        private float _shapeLineVel;

        public TeamAIPhase Phase => PhaseMachine.Phase;
        /// <summary>Our back-line depth (0..1 from our goal) this tick.</summary>
        public float BackLineDepth { get; private set; }
        /// <summary>Offside line our attackers must stay behind (0..1 from our goal).</summary>
        public float OffsideDepth { get; private set; }
        /// <summary>A press trigger is active (seen and past the reaction delay).</summary>
        public bool PressTriggerActive { get; private set; }
        /// <summary>A press trigger condition is true in the current world (before reaction delay).</summary>
        public bool PressTriggerSeen { get; private set; }
        public int PresserIndex { get; private set; } = -1;
        public int SecondPresserIndex { get; private set; } = -1;
        public int ChaserIndex { get; private set; } = -1;

        private float _triggerSince = -1f;
        private float _triggerLastSeen = -100f;
        private int _lastOwnerId = int.MinValue;
        private float _ownerSince;
        private float _prevOffsideDepth = -1f;
        private float _lineSpeed;
        private readonly bool[] _running = new bool[TeamAIWorld.MaxPlayers];
        private readonly float[] _runStart = new float[TeamAIWorld.MaxPlayers];
        private readonly bool[] _oppTaken = new bool[TeamAIWorld.MaxPlayers];
        private readonly int[] _supporters = new int[2];
        private readonly SimVec3[] _supportOffset = new SimVec3[TeamAIWorld.MaxPlayers];
        private readonly int[] _supportOwner = new int[TeamAIWorld.MaxPlayers];
        private SimVec3 _firstSupportSpot;
        /// <summary>AI teammate making a run a human asked for this tick, -1 when none.</summary>
        public int DirectedRunnerIndex { get; private set; } = -1;
        /// <summary>AI teammate pressing because a human asked for it this tick, -1 when none.</summary>
        public int RequestedPresserIndex { get; private set; } = -1;

        public TeamAIBrain(int team, TeamAITactics tactics, float difficulty, ulong seed)
            : this(team, tactics, new TeamAIDifficulty(difficulty), seed) { }

        public TeamAIBrain(int team, TeamAITactics tactics, TeamAIDifficulty difficulty, ulong seed)
        {
            Team = team;
            Tactics = tactics ?? new TeamAITactics();
            Difficulty = difficulty;
            Rng = new TeamAIRandom(seed);
            for (int k = 0; k < _supportOwner.Length; k++) _supportOwner[k] = -1;
        }

        public void SetDifficulty(float theta) => Difficulty = new TeamAIDifficulty(theta);
        public void SetDifficulty(TeamAIDifficulty difficulty) => Difficulty = difficulty;

        /// <summary>
        /// Run one decision tick for this team and write commands for its players into `output`
        /// (indexed like world.Players). Other teams' entries are left untouched.
        /// </summary>
        public void Decide(TeamAIWorld world, TeamAIPitchControl pc, float dt, bool hasPossession, AIPlayerCommand[] output)
        {
            var pitch = world.Pitch;
            int sign = world.AttackSign(Team);
            var ball = TeamAIMath.Flat(world.Ball.Position);
            float ballDepth = TeamAIMath.Depth(ball.Z, sign, pitch);
            int ownerIndex = world.OwnerIndex;
            int ownerTeam = ownerIndex >= 0 ? world.Players[ownerIndex].Team : -1;

            if (world.Ball.OwnerId != _lastOwnerId)
            {
                _lastOwnerId = world.Ball.OwnerId;
                _ownerSince = world.Time;
            }

            // ---- Layer 0: manager (mentality from score and clock) ----
            Manager.Update(world, Team);
            TeamAIManager.Apply(Tactics, Manager.Current, Manager.Enabled ? Manager.StyleExecution : 1f, Effective);

            // ---- Layer 1: team phase ----
            float nearest = NearestTeammateDistance(world, ball);
            PhaseMachine.Update(dt, Effective, hasPossession, ballDepth, nearest, world.MatchPhase, world.RestartTeam, Team);
            var phase = PhaseMachine.Phase;
            bool inPossession = TeamAIFormationShape.InPossession(phase);

            // ---- Transition: break at once after winning the ball in open play ----
            bool openPlay = world.MatchPhase == TeamAIMatchPhase.InPlay;
            if (hasPossession && !_hadBall && openPlay) _regainTime = world.Time;
            // A restart ends any break (our own free kick is a set piece, not a counter).
            if (!openPlay) _regainTime = -100f;
            _hadBall = hasPossession;
            float boost = Effective.CounterBoost;
            // Hysteresis on the depth limit: a break that reached 0.78 runs on to 0.82 instead of flickering at the edge.
            CounterActive = hasPossession && openPlay && boost > 0f && world.Time - _regainTime < CounterWindow * boost &&
                            ballDepth < (CounterActive ? CounterEndDepth : CounterStartDepth);
            _restBehind = CountBehindBall(world, ballDepth, ownerIndex, sign);
            OnBall.Directness = TeamAIMath.Clamp01(Effective.Directness + (CounterActive ? 0.4f * boost : 0f));
            OnBall.CrossBias = 0.3f * TeamAIMath.Clamp01((Effective.Width - 0.6f) * 2.5f);

            // ---- Layer 2: formation anchors ----
            float line = TeamAIFormationShape.ComputeAnchors(phase, Effective, ball, sign, pitch, Anchors);
            if (!inPossession && ownerIndex >= 0 && ownerTeam != Team)
            {
                // Ball pressure rule: drop the line when the carrier is unpressured and facing our goal.
                float pressure = NearestTeammateDistance(world, ball);
                var facing = world.Players[ownerIndex].Facing;
                bool facingUs = facing.Z * -sign > 0.3f;
                if (pressure > 5f && facingUs) line = Math.Max(0.05f, line - 0.04f);
            }
            BackLineDepth = line;
            UpdateShapeSpots(world, phase, line, sign, dt);
            OffsideDepth = ComputeOffsideDepth(world, sign, ballDepth);
            // Speed of the offside line (m/s toward the opponent goal), for runners timing a run in behind it.
            _lineSpeed = _prevOffsideDepth >= 0f && dt > 1e-4f
                ? TeamAIMath.Clamp((OffsideDepth - _prevOffsideDepth) * 2f * pitch.HalfLength / dt, -9f, 9f)
                : 0f;
            _prevOffsideDepth = OffsideDepth;

            // ---- Press triggers (with reaction delay) ----
            bool trigger = !hasPossession && DetectPressTrigger(world, sign, ownerIndex, ownerTeam);
            PressTriggerSeen = trigger;
            if (trigger)
            {
                if (_triggerSince < 0f) _triggerSince = world.Time;
                _triggerLastSeen = world.Time;
            }
            else if (world.Time - _triggerLastSeen > 1.5f)
            {
                _triggerSince = -1f;
            }
            PressTriggerActive = _triggerSince >= 0f && world.Time - _triggerSince >= Difficulty.ReactionDelay;

            // ---- Ball chaser / receiver for loose balls ----
            if (ownerIndex < 0) Trajectory.Build(world, _ballModel);
            ChaserIndex = -1;
            SimVec3 chasePoint = ball;
            if (ownerIndex < 0) ChaserIndex = BestInterceptor(world, Team, out chasePoint);

            // ---- Pressers (out of possession) ----
            PresserIndex = -1;
            SecondPresserIndex = -1;
            RequestedPresserIndex = -1;
            if (!hasPossession && ownerIndex >= 0 && ownerTeam != Team)
            {
                SelectPressers(world, ball);
                if (world.Directives[Team].PressActive(world.Time)) ApplyPressRequest(world, ball);
            }

            // ---- Human-directed run ----
            DirectedRunnerIndex = -1;
            if (hasPossession && ownerIndex >= 0 && ownerTeam == Team && world.Directives[Team].RunActive(world.Time))
                DirectedRunnerIndex = ResolveDirectedRunner(world, ownerIndex, sign);

            for (int i = 0; i < TeamAIWorld.MaxPlayers; i++) _oppTaken[i] = false;
            _supporters[0] = -1;
            _supporters[1] = -1;
            if (hasPossession && ownerIndex >= 0 && ownerTeam == Team)
            {
                SelectSupporters(world, ownerIndex);
                // The first supporter's spot is fixed before anyone moves, so the second one (whatever his index) avoids
                // this tick's spot, not last tick's.
                if (_supporters[0] >= 0)
                    _firstSupportSpot = FindSupportSpot(world, pc, _supporters[0], ownerIndex, AnchorFor(world.Players[_supporters[0]]),
                        world.AttackSign(Team), false);
            }

            int runners = 0;
            int overlappers = 0;
            bool setPiece = phase == TeamAIPhase.SetPieceFor || phase == TeamAIPhase.SetPieceAgainst;
            int taker = phase == TeamAIPhase.SetPieceFor && ownerIndex < 0 ? SetPieceTaker(world, ball) : -1;
            int routineTaker = ownerIndex >= 0 && ownerTeam == Team ? ownerIndex : taker;
            SetPieceRoutineActive = setPiece && SetPieces.Plan(world, Team, routineTaker, Rng, Difficulty);

            for (int i = 0; i < world.PlayerCount; i++)
            {
                ref var p = ref world.Players[i];
                if (p.Team != Team) continue;
                ref var cmd = ref output[i];
                cmd = AIPlayerCommand.Idle(p.Id, p.Position);
                cmd.LookAt = ball;
                cmd.FaceLookAt = true;
                if (!p.Active || p.HumanControlled) continue; // humans move themselves; the adapter ignores their command

                var anchor = AnchorFor(p);

                // On the ball.
                if (i == ownerIndex)
                {
                    DecideOnBall(world, i, phase, setPiece, ref cmd);
                    continue;
                }

                // Set piece taker walks to the ball.
                if (i == taker)
                {
                    SetMove(ref cmd, p, ball, false, TeamAIIntent.SetPieceTaker);
                    continue;
                }

                // Goalkeeper.
                if (p.Role == TeamAIRole.GK)
                {
                    if (SetPieceRoutineActive && SetPieces.Roles[i] == TeamAISetPieceRole.Goalkeeper)
                    {
                        SetMove(ref cmd, p, SetPieces.Targets[i], TeamAIMath.FlatDistance(p.Position, SetPieces.Targets[i]) > 10f,
                            TeamAIIntent.GKPosition);
                    }
                    else if (setPiece || (hasPossession && ownerIndex >= 0))
                    {
                        var gkPos = TeamAIGoalkeeper.PositionTarget(ball, sign, pitch, BackLineDepth);
                        SetMove(ref cmd, p, gkPos, false, TeamAIIntent.GKPosition);
                    }
                    else
                    {
                        TeamAIGoalkeeper.Decide(world, i, BackLineDepth, hasPossession, ownerIndex < 0 ? Trajectory : null, _ballModel, ref cmd);
                    }
                    continue;
                }

                if (SetPieceRoutineActive && SetPieces.Roles[i] != TeamAISetPieceRole.None)
                {
                    var t = SetPieces.Targets[i];
                    var role = SetPieces.Roles[i];
                    bool marker = role == TeamAISetPieceRole.Marker;
                    if (RoutineRoleDrifts(role, SetPieces.Kind))
                        SetHold(ref cmd, p, world, t, SimVec3.Zero, TeamAIMath.FlatDistance(p.Position, t) > 12f,
                            TeamAIIntent.HoldShape, false, false, SetPieceDriftScale);
                    else
                        SetMove(ref cmd, p, t, TeamAIMath.FlatDistance(p.Position, t) > 12f,
                            marker ? TeamAIIntent.Mark : TeamAIIntent.HoldShape);
                    if (marker && SetPieces.MarkIndex[i] >= 0) cmd.TargetPlayerId = world.Players[SetPieces.MarkIndex[i]].Id;
                    continue;
                }

                if (setPiece)
                {
                    DecideSetPiece(world, i, anchor, ball, sign, phase, ref cmd);
                    continue;
                }

                // Loose ball: the best interceptor goes for it.
                if (i == ChaserIndex)
                {
                    bool ours = world.Ball.LastTouchTeam == Team && hasPossession;
                    SetMoveTracking(ref cmd, p, chasePoint, world.Ball.Velocity, true, ours ? TeamAIIntent.Receive : TeamAIIntent.ChaseBall);
                    continue;
                }

                if (inPossession)
                    DecideAttackingOffBall(world, pc, i, anchor, ownerIndex, phase, sign, ref runners, ref overlappers, ref cmd);
                else
                    DecideDefending(world, i, anchor, ownerIndex, phase, sign, ref cmd);
            }
        }

        // ------------------------------------------------------------------ on ball

        private void DecideOnBall(TeamAIWorld world, int i, TeamAIPhase phase, bool setPiece, ref AIPlayerCommand cmd)
        {
            ref var p = ref world.Players[i];
            int sign = world.AttackSign(Team);
            float delay = Difficulty.ReactionDelay * (1.3f - 0.6f * p.Reactions);
            if (world.Time - _ownerSince < delay)
            {
                // Still reacting to the reception: protect the ball, drift forward.
                var ahead = TeamAIMath.ClampToPitch(p.Position + new SimVec3(0f, 0f, sign * 2f), world.Pitch, 1f);
                SetMove(ref cmd, p, setPiece ? p.Position : ahead, false, TeamAIIntent.Carry);
                return;
            }
            var ctx = new TeamAIOnBallContext
            {
                IsGoalkeeper = p.Role == TeamAIRole.GK,
                SetPiece = setPiece,
                MatchPhase = world.MatchPhase,
                OffsideDepth = OffsideDepth
            };
            if (setPiece && SetPieceRoutineActive)
            {
                var kind = SetPieces.Kind;
                bool ours = kind == TeamAISetPieceKind.CornerFor || kind == TeamAISetPieceKind.PenaltyFor ||
                            kind == TeamAISetPieceKind.FreeKickFor;
                if (ours)
                {
                    if (!SetPieces.ReadyToTake(world))
                    {
                        SetMove(ref cmd, p, p.Position, false, TeamAIIntent.SetPieceTaker);
                        return;
                    }
                    if (SetPieces.ChooseTakerOption(world, i, Difficulty, Rng, out var opt))
                    {
                        OnBall.LastChoice = opt;
                        TeamAIOnBall.ApplyOption(world, i, opt, Difficulty, Rng, ctx, ref cmd);
                        return;
                    }
                }
            }
            ref readonly var calls = ref world.Directives[Team];
            if (calls.PassCallActive(world.Time))
            {
                int caller = world.IndexOfId(calls.PassCallerId);
                if (caller >= 0 && caller != i && world.Players[caller].Team == Team && world.Players[caller].Active)
                {
                    ctx.HasCall = true;
                    ctx.CalledReceiver = caller;
                    ctx.CalledThrough = calls.PassCallThrough;
                }
            }
            OnBall.Decide(world, i, phase, Difficulty, Rng, ctx, ref cmd);
        }

        // ------------------------------------------------------------------ attacking off the ball

        private void DecideAttackingOffBall(TeamAIWorld world, TeamAIPitchControl pc, int i, SimVec3 anchor, int ownerIndex,
            TeamAIPhase phase, int sign, ref int runners, ref int overlappers, ref AIPlayerCommand cmd)
        {
            ref var p = ref world.Players[i];
            var pitch = world.Pitch;
            float onsideDepth = OffsideDepth - 0.008f;
            bool attacking = phase == TeamAIPhase.Progression || phase == TeamAIPhase.FinalThird || CounterActive;
            bool ownerOurs = ownerIndex >= 0 && world.Players[ownerIndex].Team == Team;
            float myDepth = TeamAIMath.Depth(p.Position.Z, sign, pitch);

            // A human asked for this run: go now, regardless of timing (the user owns the offside risk).
            if (i == DirectedRunnerIndex)
            {
                ref readonly var d = ref world.Directives[Team];
                runners++;
                _running[i] = true;
                _runStart[i] = world.Time;
                SimVec3 target;
                if (d.RunHasTarget)
                {
                    target = TeamAIMath.ClampToPitch(d.RunTarget, pitch, 1.5f);
                }
                else
                {
                    float targetDepth = Math.Min(Math.Max(OffsideDepth, myDepth) + 0.12f, 0.95f);
                    target = TeamAIMath.ClampToPitch(new SimVec3(TeamAIMath.Lerp(p.Position.X, 0f, 0.3f), 0f,
                        TeamAIMath.DepthToZ(targetDepth, sign, pitch)), pitch, 1.5f);
                }
                SetMoveTracking(ref cmd, p, target, d.RunHasTarget ? SimVec3.Zero : LineVelocity(sign), true,
                    TeamAIIntent.RunInBehind);
                return;
            }

            // Run in behind, timed against the offside line.
            // Late runs from midfield join in when the team commits players forward (high ForwardRuns) in the final third.
            bool behindBall = myDepth < TeamAIMath.Depth(world.Ball.Position.Z, sign, pitch) - 0.02f;
            bool lateRunner = p.Role == TeamAIRole.CM && Effective.ForwardRuns >= 0.7f && phase == TeamAIPhase.FinalThird &&
                              (_running[i] || _restBehind - (behindBall ? 1 : 0) >= RestDefenders);
            if (ownerOurs && attacking && runners < Effective.MaxRunners &&
                (p.Role == TeamAIRole.ST || p.Role == TeamAIRole.W || p.Role == TeamAIRole.AM || lateRunner))
            {
                var owner = world.Players[ownerIndex];
                float ownerDepth = TeamAIMath.Depth(owner.Position.Z, sign, pitch);
                float ownerPressure = TeamAIOnBall.NearestOpponentDistance(world, Team, owner.Position);
                // The carrier can play the ball once no one is close enough to smother the kick (see TeamAIOnBall.BlockRisk);
                // waiting for a free carrier means a team pressed by one man never runs in behind.
                bool ownerHasTime = ownerPressure > TeamAIOnBall.BlockSafe + 0.6f && SimVec3.Dot(owner.Facing, new SimVec3(0f, 0f, sign)) > 0.2f;
                bool continuing = _running[i] && world.Time - _runStart[i] < 3f && myDepth < 0.96f;
                // On a counter the forwards go from deeper: anyone within reach of the line breaks.
                bool nearLine = myDepth >= OffsideDepth - (CounterActive ? 0.12f : 0.035f) && myDepth <= OffsideDepth + 0.002f;
                if (CounterActive && !ownerHasTime) ownerHasTime = ownerPressure > TeamAIOnBall.BlockSafe;
                bool spaceBehind = OffsideDepth < 0.9f;
                bool start = !continuing && ownerHasTime && nearLine && spaceBehind && ownerDepth < OffsideDepth &&
                             Rng.NextFloat() >= Difficulty.LapseChance;
                if (continuing || start)
                {
                    if (start) { _running[i] = true; _runStart[i] = world.Time; }
                    if (start && behindBall) _restBehind--;
                    runners++;
                    float targetDepth = Math.Min(OffsideDepth + 0.12f, 0.95f);
                    float x = TeamAIMath.Lerp(p.Position.X, 0f, 0.3f);
                    var target = TeamAIMath.ClampToPitch(new SimVec3(x, 0f, TeamAIMath.DepthToZ(targetDepth, sign, pitch)), pitch, 1.5f);
                    SetMoveTracking(ref cmd, p, target, LineVelocity(sign), true, TeamAIIntent.RunInBehind);
                    return;
                }
            }
            _running[i] = false;

            // Support the ball carrier with an open angle.
            if (ownerOurs && (i == _supporters[0] || i == _supporters[1]))
            {
                var raw = i == _supporters[0] ? _firstSupportSpot : FindSupportSpot(world, pc, i, ownerIndex, anchor, sign, true);
                var spot = SpaceFromCarrier(world, ownerIndex, raw);
                // The spot is relative to the carrier: it moves with him, so the supporter keeps his 8-15 m distance
                // (and angle) while the carrier dribbles instead of being run onto.
                // Supporting players jog to their spot; only a long way off (a fast break) do they sprint.
                float sd = TeamAIMath.FlatDistance(p.Position, spot);
                SetMove(ref cmd, p, spot, sd > TeamAITempo.SupportSprintDistance, TeamAIIntent.Support);
                cmd.SpeedLimit = TeamAITempo.SupportSpeed(sd, Difficulty.Theta);
                return;
            }

            // Overlap: ball-side full-back runs outside a wide carrier in the opponent half.
            bool restOk = Effective.ForwardRuns < 0.75f || _restBehind - (behindBall ? 1 : 0) >= RestDefenders;
            if (ownerOurs && p.Role == TeamAIRole.FB && overlappers == 0 && attacking && Effective.ForwardRuns >= 0.25f && restOk)
            {
                var owner = world.Players[ownerIndex];
                float ownerDepth = TeamAIMath.Depth(owner.Position.Z, sign, pitch);
                bool sameSide = owner.Position.X * anchor.X > 0f && TeamAIMath.Abs(owner.Position.X) > 6f;
                bool carrierRole = owner.Role == TeamAIRole.W || owner.Role == TeamAIRole.CM || owner.Role == TeamAIRole.AM;
                if (sameSide && carrierRole && ownerDepth > (Effective.ForwardRuns >= 0.75f ? 0.4f : 0.5f))
                {
                    overlappers++;
                    if (behindBall) _restBehind--;
                    float side = anchor.X > 0f ? 1f : -1f;
                    float depth = Math.Min(ownerDepth + 0.1f, onsideDepth);
                    var target = new SimVec3(side * (pitch.HalfWidth - 3f), 0f, TeamAIMath.DepthToZ(depth, sign, pitch));
                    SetMove(ref cmd, p, target, true, TeamAIIntent.Overlap);
                    return;
                }
            }

            // Wingers hold the width; everyone else holds the shape, staying onside.
            var shapeSpot = ShapeSpotFor(p);
            var t = shapeSpot;
            var intent = TeamAIIntent.HoldShape;
            if (p.Role == TeamAIRole.W)
            {
                t = new SimVec3((anchor.X >= 0f ? 1f : -1f) * (pitch.HalfWidth - 2.5f), 0f, shapeSpot.Z);
                intent = TeamAIIntent.HoldWidth;
            }
            float tDepth = TeamAIMath.Depth(t.Z, sign, pitch);
            if (tDepth > onsideDepth - 0.012f && tDepth > 0.5f)
                t = new SimVec3(t.X, 0f, TeamAIMath.DepthToZ(Math.Max(onsideDepth - 0.012f, 0.5f), sign, pitch)); // drift room
            SetHold(ref cmd, p, world, t, AnchorVelocityFor(p), TeamAIMath.FlatDistance(p.Position, t) > 15f, intent,
                false, intent == TeamAIIntent.HoldWidth, 1f, ownerOurs ? ownerIndex : -1);
        }

        private SimVec3 FindSupportSpot(TeamAIWorld world, TeamAIPitchControl pc, int i, int ownerIndex, SimVec3 anchor,
            int sign, bool avoidFirst)
        {
            var pitch = world.Pitch;
            var owner = world.Players[ownerIndex];
            var from = TeamAIMath.Flat(owner.Position);
            float ownerDepth = TeamAIMath.Depth(from.Z, sign, pitch);
            var best = anchor;
            float bestScore = float.MinValue;
            var forward = new SimVec3(0f, 0f, sign);
            // Support comes from 8-15 m (a closed-down carrier gets the short 8 m outlet first). Closer than that a
            // supporter only brings his marker into the carrier's space; the second supporter takes a different line
            // (at least 50 deg round from the first, and 8 m from his spot).
            bool pressed = TeamAIOnBall.NearestOpponentDistance(world, Team, from) < 2.5f;
            var me = TeamAIMath.Flat(world.Players[i].Position);
            bool keep = _supportOwner[i] == world.Ball.OwnerId && world.Ball.OwnerId >= 0;
            var prevOffset = _supportOffset[i];
            var carrierVel = TeamAIMath.Flat(owner.Velocity);
            bool carrierRunning = carrierVel.SqrMagnitude > 4f;
            var carrierDir = carrierRunning ? carrierVel.Normalized : SimVec3.Zero;
            var firstDir = TeamAIMath.FlatDirection(from, _firstSupportSpot);
            for (int ring = 0; ring < 2; ring++)
            {
                float radius = ring == 0 ? (pressed ? 8f : 10f) : (pressed ? 12f : 15f);
                for (int k = 0; k < 8; k++)
                {
                    var dir = TeamAIMath.RotateFlat(forward, k * 0.785398f);
                    var c = TeamAIMath.ClampToPitch(from + dir * radius, pitch, 2f);
                    float cDepth = TeamAIMath.Depth(c.Z, sign, pitch);
                    if (cDepth > OffsideDepth - 0.008f && cDepth > 0.5f) continue;
                    if (avoidFirst && (TeamAIMath.FlatDistance(c, _firstSupportSpot) < 8f ||
                                       SimVec3.Dot(TeamAIMath.FlatDirection(from, c), firstDir) > 0.64f)) continue;
                    float control = pc.ControlAt(Team, c);
                    float lane = TeamAIOnBall.InterceptionRisk(world, Team, from, c,
                        _ballModel.GroundPassTime(radius), false);
                    float drift = TeamAIMath.FlatDistance(c, anchor);
                    float score = control + 0.8f * (cDepth - ownerDepth) - 0.7f * lane -
                                  0.02f * Math.Max(0f, drift - 10f);
                    score += Rng.NextSigned() * Difficulty.DecisionNoise * 0.5f;
                    // Commit to a line: a spot near the one picked last tick (relative to the carrier) is preferred, and
                    // spots on the far side of the carrier cost the trip. A supporter whose spot flips round the carrier
                    // every 0.1 s never gets out of his space.
                    if (keep && TeamAIMath.FlatDistance(c, from + prevOffset) < 4f) score += SupportSpotStickiness;
                    score -= SupportTravelCost * TeamAIMath.FlatDistance(c, me);
                    // Not in the carrier's running lane: he would dribble straight into his supporter.
                    if (carrierRunning && SimVec3.Dot(dir, carrierDir) > 0.85f) score -= SupportLanePenalty;
                    if (score > bestScore) { bestScore = score; best = c; }
                }
            }
            _supportOffset[i] = best - from;
            _supportOwner[i] = world.Ball.OwnerId;
            return best;
        }

        // ------------------------------------------------------------------ defending

        private void DecideDefending(TeamAIWorld world, int i, SimVec3 anchor, int ownerIndex, TeamAIPhase phase, int sign,
            ref AIPlayerCommand cmd)
        {
            ref var p = ref world.Players[i];
            var pitch = world.Pitch;
            var ball = TeamAIMath.Flat(world.Ball.Position);
            var ownGoal = TeamAIMath.OwnGoal(sign, pitch);
            float ballDepth = TeamAIMath.Depth(ball.Z, sign, pitch);
            bool oppHasBall = ownerIndex >= 0 && world.Players[ownerIndex].Team != Team;

            if (oppHasBall && (i == PresserIndex || i == SecondPresserIndex))
            {
                DecidePresser(world, i, anchor, ownerIndex, phase, sign, i == SecondPresserIndex, ref cmd);
                return;
            }

            // Recovery: players caught upfield of the ball sprint back to shape.
            if (phase == TeamAIPhase.Recovery || phase == TeamAIPhase.CounterPress)
            {
                float myDepth = TeamAIMath.Depth(p.Position.Z, sign, pitch);
                if (phase == TeamAIPhase.Recovery && myDepth > ballDepth + 0.03f && p.Role != TeamAIRole.ST)
                {
                    SetMove(ref cmd, p, anchor, true, TeamAIIntent.Recover);
                    return;
                }
            }

            // Back line: hold one line, marking goal-side when an attacker is inside our zone.
            if (p.Role == TeamAIRole.CB || p.Role == TeamAIRole.FB)
            {
                float lineZ = TeamAIMath.DepthToZ(BackLineDepth, sign, pitch);
                var target = new SimVec3(anchor.X, 0f, lineZ);
                var intent = TeamAIIntent.KeepLine;
                var markVel = SimVec3.Zero;
                int opp = FindMarkTarget(world, anchor, sign, 11f, BackLineDepth + 0.12f);
                if (opp >= 0)
                {
                    _oppTaken[opp] = true;
                    var op = world.Players[opp].Position;
                    float oppDepth = TeamAIMath.Depth(op.Z, sign, pitch);
                    float x = TeamAIMath.Lerp(anchor.X, op.X, 0.6f);
                    float depth = BackLineDepth;
                    if (oppDepth < BackLineDepth)
                    {
                        // Attacker already goal-side of the line: track him, goal-side.
                        var mark = op + TeamAIMath.FlatDirection(op, ownGoal) * 1.5f;
                        x = mark.X;
                        depth = TeamAIMath.Depth(mark.Z, sign, pitch);
                        intent = TeamAIIntent.Mark;
                    }
                    target = new SimVec3(x, 0f, TeamAIMath.DepthToZ(depth, sign, pitch));
                    markVel = intent == TeamAIIntent.Mark ? world.Players[opp].Velocity : SimVec3.Zero;
                }
                if (opp >= 0)
                {
                    var markTarget = TeamAIMath.ClampToPitch(target, pitch, 0.5f);
                    SetMoveTracking(ref cmd, p, markTarget, markVel, false, intent);
                    cmd.TargetPlayerId = world.Players[opp].Id;
                    if (intent == TeamAIIntent.KeepLine)
                    {
                        // Holding the line opposite a forward: line tempo (a goal-side Mark keeps full pace).
                        bool sp = false;
                        cmd.SpeedLimit = TeamAITempo.ShapeSpeed(world, Team, p, markTarget, SimVec3.Zero, Phase, Difficulty.Theta,
                            world.MatchPhase != TeamAIMatchPhase.InPlay, ref sp);
                    }
                }
                else
                {
                    // Nobody to pick up: hold the (smoothed) line, sliding with it and adjusting along it.
                    var spot = ShapeSpotFor(p);
                    var vel = AnchorVelocityFor(p);
                    SetHold(ref cmd, p, world, new SimVec3(spot.X, 0f, _shapeLineZ), new SimVec3(vel.X, 0f, _shapeLineVel), false,
                        intent, true, false);
                }
                return;
            }

            // Midfield: cut passing lanes (cover shadow) or mark in the low block.
            if (oppHasBall)
            {
                int opp = FindShadowTarget(world, anchor, ownerIndex, sign);
                if (opp >= 0)
                {
                    _oppTaken[opp] = true;
                    var op = world.Players[opp].Position;
                    float squeeze = PressSqueeze;
                    var shadow = ball + (op - ball) * TeamAIMath.Lerp(ShadowFraction, SqueezeShadowFraction, squeeze);
                    shadow = new SimVec3(shadow.X, 0f, shadow.Z);
                    if (TeamAIMath.FlatDistance(shadow, anchor) < 12f + 10f * squeeze)
                    {
                        cmd.TargetPlayerId = world.Players[opp].Id;
                        // The shadow point sits 40% of the way from the ball to the runner and moves with both.
                        // A pass in flight drags the point at 60% of its speed: shift with it at a jog, not a sprint.
                        var shadowVel = TeamAIShapeMotion.ClampSpeed(TeamAIMath.Flat(world.Ball.Velocity * 0.6f + world.Players[opp].Velocity * 0.4f),
                            TeamAITempo.MaxShadowSpeed);
                        float shd = TeamAIMath.FlatDistance(p.Position, shadow);
                        SetMoveTracking(ref cmd, p, shadow, shadowVel, shd > 15f, TeamAIIntent.CoverShadow);
                        cmd.SpeedLimit = TeamAITempo.CoverSpeed(shd, shadowVel.Magnitude, Difficulty.Theta);
                        return;
                    }
                }
            }

            var holdSpot = ShapeSpotFor(p);
            SetHold(ref cmd, p, world, holdSpot, AnchorVelocityFor(p), TeamAIMath.FlatDistance(p.Position, holdSpot) > 15f,
                TeamAIIntent.HoldShape, false, false);
        }

        private void DecidePresser(TeamAIWorld world, int i, SimVec3 anchor, int ownerIndex, TeamAIPhase phase, int sign,
            bool second, ref AIPlayerCommand cmd)
        {
            ref var p = ref world.Players[i];
            var pitch = world.Pitch;
            var ball = TeamAIMath.Flat(world.Ball.Position);
            var owner = world.Players[ownerIndex];
            var ownGoal = TeamAIMath.OwnGoal(sign, pitch);
            var toGoal = TeamAIMath.FlatDirection(ball, ownGoal);
            float dist = TeamAIMath.FlatDistance(p.Position, ball);
            float ballDepth = TeamAIMath.Depth(ball.Z, sign, pitch);
            cmd.TargetPlayerId = owner.Id;

            bool pressAllowed;
            float aggression = Difficulty.PressAggression;
            float anchorToBall = TeamAIMath.FlatDistance(anchor, ball);
            switch (phase)
            {
                case TeamAIPhase.HighPress:
                case TeamAIPhase.CounterPress:
                    pressAllowed = true;
                    break;
                case TeamAIPhase.MidBlock:
                    pressAllowed = PressTriggerActive || anchorToBall < 7f * aggression;
                    break;
                case TeamAIPhase.LowBlock:
                    pressAllowed = PressTriggerActive && ballDepth < 0.4f || anchorToBall < 5f * aggression || ballDepth < 0.2f;
                    break;
                default:
                    pressAllowed = PressTriggerActive || dist < 6f;
                    break;
            }
            if (second && !PressTriggerActive && phase != TeamAIPhase.CounterPress) pressAllowed = false;
            if (i == RequestedPresserIndex)
            {
                // Teammate press asked for by a human: close down all the way, as the first presser would.
                pressAllowed = true;
                second = false;
            }

            if (!pressAllowed)
            {
                // Contain: sit goal-side of the ball inside our zone, no sprint.
                if (anchorToBall < 16f && !second)
                {
                    var contain = ball + toGoal * 4.5f;
                    SetMoveTracking(ref cmd, p, contain, owner.Velocity, false, TeamAIIntent.Jockey);
                    if (dist < 5.5f) { cmd.Action = TeamAIAction.Jockey; cmd.ActionTarget = ball; }
                }
                else
                {
                    SetHold(ref cmd, p, world, ShapeSpotFor(p), AnchorVelocityFor(p), false, TeamAIIntent.HoldShape, false, false);
                }
                return;
            }

            if (dist > 2.4f)
            {
                // Close down on a goal-side curve.
                var approach = ball + toGoal * (second ? 3f - 1.5f * PressSqueeze : 1.0f);
                SetMoveTracking(ref cmd, p, approach, owner.Velocity, true, TeamAIIntent.Press);
                // Slide: last-ditch, chasing a carrier who is getting away near our box.
                bool goalSideNow = SimVec3.Dot(p.Position - ball, toGoal) > 0f;
                float ownerSpeed = owner.Velocity.Flat.Magnitude;
                if (!second && !goalSideNow && dist < 3.6f && ballDepth < 0.35f && ownerSpeed > p.MaxSpeed * 0.7f &&
                    p.Defending > 0.5f && Rng.NextFloat() < p.Defending * 0.6f)
                {
                    cmd.Action = TeamAIAction.SlideTackle;
                    cmd.ActionTarget = ball;
                    cmd.Intent = TeamAIIntent.Tackle;
                }
                return;
            }

            // In tackling range: jockey or tackle.
            bool exposed = TeamAIMath.FlatDistance(ball, owner.Position) > 1.0f;
            bool facingAway = SimVec3.Dot(owner.Facing, toGoal) < -0.3f;
            bool goalSide = SimVec3.Dot(p.Position - ball, toGoal) > -0.3f;
            float success = TackleWinChance(p, owner, TeamAIMath.FlatDistance(p.Position, ball)) +
                            (exposed ? 0.1f : 0f) + (facingAway ? 0.05f : 0f);
            success += Rng.NextSigned() * Difficulty.DecisionNoise;
            if (goalSide && success > TackleThreshold(Difficulty.Theta) - SqueezeTackleShift * PressSqueeze)
            {
                SetMoveTracking(ref cmd, p, ball, owner.Velocity, false, TeamAIIntent.Tackle);
                cmd.Action = TeamAIAction.Tackle;
                cmd.ActionTarget = ball;
                return;
            }
            var jockey = ball + toGoal * 1.5f;
            SetMoveTracking(ref cmd, p, jockey, owner.Velocity, false, TeamAIIntent.Jockey);
            cmd.Action = TeamAIAction.Jockey;
            cmd.ActionTarget = ball;
        }

        /// <summary>
        /// Win chance of a standing tackle, mirroring the sim's resolution: skill gap on the 1-99 scale over 15, a 0.3
        /// base, part of the jockey bonus (the tackler jockeys into it), minus 0.8 x ball distance / 1.6 m range.
        /// </summary>
        public static float TackleWinChance(in TeamAIPlayer tackler, in TeamAIPlayer carrier, float ballDistance)
        {
            float z = (tackler.Defending - carrier.Dribbling) * 6.6f + 0.3f + 0.2f - 0.8f * ballDistance / 1.6f;
            return TeamAIMath.Sigmoid(z);
        }

        /// <summary>
        /// Win chance a presser needs before he commits to a tackle. A weak side jockeys and backs off; a strong side
        /// reads the touch and goes in (a missed tackle costs half a second, and the second presser covers).
        /// </summary>
        public static float TackleThreshold(float theta) => TeamAIMath.Lerp(0.5f, 0.36f, theta);

        /// <summary>Our active outfielders (not the carrier) more than 0.02 of the pitch behind the ball.</summary>
        private int CountBehindBall(TeamAIWorld world, float ballDepth, int ownerIndex, int sign)
        {
            int n = 0;
            for (int j = 0; j < world.PlayerCount; j++)
            {
                ref readonly var o = ref world.Players[j];
                if (j == ownerIndex || !o.Active || o.Team != Team || o.Role == TeamAIRole.GK) continue;
                if (TeamAIMath.Depth(o.Position.Z, sign, world.Pitch) < ballDepth - 0.02f) n++;
            }
            return n;
        }

        private int FindMarkTarget(TeamAIWorld world, SimVec3 anchor, int sign, float radius, float maxDepth)
        {
            int best = -1;
            float bestD = radius;
            int owner = world.OwnerIndex;
            for (int j = 0; j < world.PlayerCount; j++)
            {
                var o = world.Players[j];
                if (!o.Active || o.Team == Team || o.Role == TeamAIRole.GK || _oppTaken[j] || j == owner) continue;
                float depth = TeamAIMath.Depth(o.Position.Z, sign, world.Pitch);
                if (depth > maxDepth) continue;
                float d = TeamAIMath.FlatDistance(o.Position, anchor);
                // Runners already goal-side of our line are picked up from much further away, first.
                if (depth < BackLineDepth) d -= 15f;
                if (d < bestD) { bestD = d; best = j; }
            }
            return best;
        }

        private int FindShadowTarget(TeamAIWorld world, SimVec3 anchor, int ownerIndex, int sign)
        {
            int best = -1;
            float bestScore = float.MinValue;
            var ball = world.Ball.Position;
            for (int j = 0; j < world.PlayerCount; j++)
            {
                var o = world.Players[j];
                if (!o.Active || o.Team == Team || j == ownerIndex || o.Role == TeamAIRole.GK || _oppTaken[j]) continue;
                float dBall = TeamAIMath.FlatDistance(o.Position, ball);
                float dAnchor = TeamAIMath.FlatDistance(o.Position, anchor);
                if (dBall > 30f || dAnchor > 16f + 10f * PressSqueeze) continue;
                // Danger: closer to our goal is worse.
                float danger = 1f - TeamAIMath.Depth(o.Position.Z, sign, world.Pitch);
                float score = danger - 0.03f * dAnchor;
                if (score > bestScore) { bestScore = score; best = j; }
            }
            return best;
        }

        // ------------------------------------------------------------------ set pieces

        private void DecideSetPiece(TeamAIWorld world, int i, SimVec3 anchor, SimVec3 ball, int sign, TeamAIPhase phase,
            ref AIPlayerCommand cmd)
        {
            ref var p = ref world.Players[i];
            var pitch = world.Pitch;
            var t = anchor;
            if (world.MatchPhase == TeamAIMatchPhase.Kickoff)
            {
                float d = TeamAIMath.Depth(t.Z, sign, pitch);
                if (d > 0.49f) t = new SimVec3(t.X, 0f, TeamAIMath.DepthToZ(0.49f, sign, pitch));
            }
            bool kickoff = world.MatchPhase == TeamAIMatchPhase.Kickoff;
            var driftVel = SimVec3.Zero;
            if (!kickoff)
            {
                // Dead ball: walk small adjustments around the spot (kick-off spots stay put: the halfway line is close).
                TeamAIShapeMotion.Adjustment(p.Id, world.Time, false, false, out var off, out driftVel, SetPieceDriftScale);
                t += off;
            }
            if (phase == TeamAIPhase.SetPieceAgainst)
            {
                float d = TeamAIMath.FlatDistance(t, ball);
                if (d < FreeKickDistance + 0.5f)
                {
                    var away = TeamAIMath.FlatDirection(ball, t);
                    if (away.SqrMagnitude < 0.5f) away = TeamAIMath.FlatDirection(ball, TeamAIMath.OwnGoal(sign, pitch));
                    t = ball + away * (FreeKickDistance + 0.5f);
                }
            }
            t = TeamAIMath.ClampToPitch(t, pitch, 0.5f);
            if (kickoff) SetMove(ref cmd, p, t, false, TeamAIIntent.HoldShape);
            else SetHold(ref cmd, p, world, t, driftVel, false, TeamAIIntent.HoldShape, false, false, 0f);
        }

        /// <summary>
        /// Routine players who jostle for position while the ball is dead: box runners, zonal defenders, outlets. Walls,
        /// markers (they follow their man), keepers, the taker and every penalty role keep their exact spot.
        /// </summary>
        private static bool RoutineRoleDrifts(TeamAISetPieceRole role, TeamAISetPieceKind kind)
        {
            if (kind == TeamAISetPieceKind.PenaltyFor || kind == TeamAISetPieceKind.PenaltyAgainst) return false;
            switch (role)
            {
                case TeamAISetPieceRole.NearPost:
                case TeamAISetPieceRole.FarPost:
                case TeamAISetPieceRole.Central:
                case TeamAISetPieceRole.KeeperScreen:
                case TeamAISetPieceRole.EdgeOfBox:
                case TeamAISetPieceRole.ShortOption:
                case TeamAISetPieceRole.RestDefence:
                case TeamAISetPieceRole.Rebound:
                case TeamAISetPieceRole.ZonalPost:
                case TeamAISetPieceRole.ZonalSixYard:
                case TeamAISetPieceRole.ZonalEdge:
                case TeamAISetPieceRole.Outlet:
                    return true;
                default:
                    return false;
            }
        }

        private int SetPieceTaker(TeamAIWorld world, SimVec3 ball)
        {
            bool goalKick = world.MatchPhase == TeamAIMatchPhase.GoalKick;
            int best = -1;
            float bd = float.MaxValue;
            for (int j = 0; j < world.PlayerCount; j++)
            {
                var o = world.Players[j];
                if (!o.Active || o.Team != Team) continue;
                bool gk = o.Role == TeamAIRole.GK;
                if (goalKick != gk) continue;
                float d = TeamAIMath.FlatSqrDistance(o.Position, ball);
                if (d < bd) { bd = d; best = j; }
            }
            return best;
        }

        // ------------------------------------------------------------------ helpers

        private SimVec3 AnchorFor(in TeamAIPlayer p)
        {
            int slot = p.FormationSlot;
            if (slot < 0 || slot >= TeamAIFormations.SlotCount) slot = 0;
            return Anchors[slot];
        }

        private bool DetectPressTrigger(TeamAIWorld world, int sign, int ownerIndex, int ownerTeam)
        {
            var ball = world.Ball;
            var pitch = world.Pitch;
            if (ownerIndex >= 0 && ownerTeam != Team)
            {
                var o = world.Players[ownerIndex];
                // Heavy touch: the ball has run away from the carrier.
                if (TeamAIMath.FlatDistance(o.Position, ball.Position) > HeavyTouchDistance) return true;
                // Back to goal: the carrier faces his own goal (our attacking direction).
                if (o.Facing.Z * sign > 0.6f && o.Velocity.Flat.SqrMagnitude < 4f && o.Role != TeamAIRole.GK) return true;
                // Touchline trap.
                if (TeamAIMath.Abs(ball.Position.X) > pitch.HalfWidth - 5f) return true;
                return false;
            }
            // Loose ball after their touch, slow and close to us.
            if (ownerIndex < 0 && ball.LastTouchTeam >= 0 && ball.LastTouchTeam != Team &&
                ball.Velocity.Flat.SqrMagnitude < 36f && NearestTeammateDistance(world, ball.Position) < 6f) return true;
            return false;
        }

        private void SelectPressers(TeamAIWorld world, SimVec3 ball)
        {
            float b1 = float.MaxValue, b2 = float.MaxValue;
            for (int j = 0; j < world.PlayerCount; j++)
            {
                var o = world.Players[j];
                if (!o.Active || o.Team != Team || o.Role == TeamAIRole.GK) continue;
                float t = TeamAIMath.TimeToIntercept(o, ball, 0.2f);
                if (t < b1) { b2 = b1; SecondPresserIndex = PresserIndex; b1 = t; PresserIndex = j; }
                else if (t < b2) { b2 = t; SecondPresserIndex = j; }
            }
            bool twoPressers = PressTriggerActive || Phase == TeamAIPhase.CounterPress ||
                               (Phase == TeamAIPhase.HighPress && Effective.PressingIntensity >= 0.6f);
            if (!twoPressers || Effective.PressingIntensity < 0.3f) SecondPresserIndex = -1;
        }

        /// <summary>Teammate press: the AI outfielder who gets to the ball first presses it, as a second man when
        /// the first presser is a human.</summary>
        private void ApplyPressRequest(TeamAIWorld world, SimVec3 ball)
        {
            int best = -1;
            float bt = float.MaxValue;
            for (int j = 0; j < world.PlayerCount; j++)
            {
                var o = world.Players[j];
                if (!o.Active || o.Team != Team || o.Role == TeamAIRole.GK || o.HumanControlled) continue;
                float t = TeamAIMath.TimeToIntercept(o, ball, 0.2f);
                if (t < bt) { bt = t; best = j; }
            }
            if (best < 0) return;
            RequestedPresserIndex = best;
            if (best != PresserIndex) SecondPresserIndex = best;
        }

        /// <summary>The asked-for runner if he is an active AI teammate, else the AI outfielder best placed to run
        /// beyond the ball (furthest forward, forward roles first).</summary>
        private int ResolveDirectedRunner(TeamAIWorld world, int ownerIndex, int sign)
        {
            ref readonly var d = ref world.Directives[Team];
            int asked = world.IndexOfId(d.RunnerId);
            if (asked >= 0 && asked != ownerIndex)
            {
                var a = world.Players[asked];
                if (a.Active && a.Team == Team && !a.HumanControlled && a.Role != TeamAIRole.GK) return asked;
            }
            int best = -1;
            float bestScore = float.MinValue;
            var ownerPos = world.Players[ownerIndex].Position;
            for (int j = 0; j < world.PlayerCount; j++)
            {
                var o = world.Players[j];
                if (j == ownerIndex || !o.Active || o.Team != Team || o.HumanControlled) continue;
                if (o.Role == TeamAIRole.GK || o.Role == TeamAIRole.CB) continue;
                bool forward = o.Role == TeamAIRole.ST || o.Role == TeamAIRole.W || o.Role == TeamAIRole.AM;
                float ahead = (o.Position.Z - ownerPos.Z) * sign;
                float score = (forward ? 10f : 0f) - Math.Abs(ahead - 8f) * 0.3f - TeamAIMath.FlatDistance(o.Position, ownerPos) * 0.05f;
                if (d.RunHasTarget) score -= TeamAIMath.FlatDistance(o.Position, d.RunTarget) * 0.4f;
                if (score > bestScore) { bestScore = score; best = j; }
            }
            return best;
        }

        private void SelectSupporters(TeamAIWorld world, int ownerIndex)
        {
            float b1 = float.MaxValue, b2 = float.MaxValue;
            var o = world.Players[ownerIndex].Position;
            for (int j = 0; j < world.PlayerCount; j++)
            {
                var p = world.Players[j];
                if (j == ownerIndex || !p.Active || p.Team != Team || p.Role == TeamAIRole.GK || p.Role == TeamAIRole.ST) continue;
                if (_running[j] || p.HumanControlled || j == DirectedRunnerIndex) continue;
                float d = TeamAIMath.FlatDistance(p.Position, o);
                if (d < b1) { b2 = b1; _supporters[1] = _supporters[0]; b1 = d; _supporters[0] = j; }
                else if (d < b2) { b2 = d; _supporters[1] = j; }
            }
            // Lapses at low difficulty: the second supporter switches off.
            if (_supporters[1] >= 0 && Rng.NextFloat() < Difficulty.LapseChance) _supporters[1] = -1;
        }

        private int BestInterceptor(TeamAIWorld world, int team, out SimVec3 point)
        {
            var ball = world.Ball;
            int best = -1;
            float bestT = float.MaxValue;
            point = TeamAIMath.Flat(ball.Position);
            for (int j = 0; j < world.PlayerCount; j++)
            {
                var o = world.Players[j];
                if (!o.Active || o.Team != team) continue;
                if (o.Role == TeamAIRole.GK) continue; // keeper logic handles its own sweeps/claims
                for (int k = 0; k <= 15; k++)
                {
                    float t = k * 0.2f;
                    if (t >= bestT) break;
                    if (Trajectory.HeightAt(t) > InterceptHeight) continue; // over everyone's head at that moment
                    var bp = Trajectory.At(t);
                    if (TeamAIMath.TimeToIntercept(o, bp, 0.2f) + ChaseAccelerationLag <= t + 0.05f)
                    {
                        bestT = t;
                        best = j;
                        point = bp;
                        break;
                    }
                }
            }
            if (best < 0)
            {
                // Nobody gets there inside 3 s: closest to the resting point goes.
                var rest = Trajectory.At(3f);
                float bd = float.MaxValue;
                for (int j = 0; j < world.PlayerCount; j++)
                {
                    var o = world.Players[j];
                    if (!o.Active || o.Team != team || o.Role == TeamAIRole.GK) continue;
                    float d = TeamAIMath.FlatSqrDistance(o.Position, rest);
                    if (d < bd) { bd = d; best = j; }
                }
                point = rest;
            }
            point = TeamAIMath.ClampToPitch(point, world.Pitch, 0.3f);
            return best;
        }

        private float ComputeOffsideDepth(TeamAIWorld world, int sign, float ballDepth)
        {
            // Second-last opponent (keeper usually the last), measured as depth from OUR goal.
            float d1 = 0f, d2 = 0f; // deepest and second deepest opponents (closest to their own goal)
            for (int j = 0; j < world.PlayerCount; j++)
            {
                var o = world.Players[j];
                if (!o.Active || o.Team == Team) continue;
                float d = TeamAIMath.Depth(o.Position.Z, sign, world.Pitch);
                if (d > d1) { d2 = d1; d1 = d; }
                else if (d > d2) d2 = d;
            }
            float line = d2;
            if (ballDepth > line) line = ballDepth;
            if (line < 0.5f) line = 0.5f;
            return line;
        }

        private float NearestTeammateDistance(TeamAIWorld world, SimVec3 p)
        {
            float best = 999f;
            for (int j = 0; j < world.PlayerCount; j++)
            {
                var o = world.Players[j];
                if (!o.Active || o.Team != Team || o.Role == TeamAIRole.GK) continue;
                float d = TeamAIMath.FlatDistance(o.Position, p);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// Keeps a shape spot at least <see cref="CarrierSpacing"/> from our carrier: a team-mate whose spot falls on
        /// top of the ball holds it on the same bearing 8 m out instead, so he is a passing option, not a crowd.
        /// </summary>
        private static SimVec3 SpaceFromCarrier(TeamAIWorld world, int ownerIndex, SimVec3 spot)
        {
            var c = TeamAIMath.Flat(world.Players[ownerIndex].Position);
            float d = TeamAIMath.FlatDistance(spot, c);
            if (d >= CarrierSpacing) return spot;
            var dir = d > 0.3f ? TeamAIMath.FlatDirection(c, spot) : new SimVec3(c.X > 0f ? -1f : 1f, 0f, 0f);
            return TeamAIMath.ClampToPitch(c + dir * CarrierSpacing, world.Pitch, 1.5f);
        }

        /// <summary>
        /// Shape spots: the formation anchors as the players actually track them. The anchors are an instant function of
        /// the ball, so a 30 m pass moves them 10-30 m at once; real teams slide across progressively. Each spot follows
        /// its anchor with a <see cref="TeamAIShapeMotion.ShapeSmoothing"/> s lag, never faster than the phase's slide
        /// speed (<see cref="TeamAIShapeMotion.SlideSpeed"/>), and the spot's own velocity is passed to the holder so he
        /// walks or jogs with it. Logic that reads the shape (marking zones, pressing distances) still uses
        /// <see cref="Anchors"/>; only where holders stand is smoothed. Spots snap at kick-offs.
        /// </summary>
        private void UpdateShapeSpots(TeamAIWorld world, TeamAIPhase phase, float line, int sign, float dt)
        {
            bool snap = !_shapePrimed || world.MatchPhase == TeamAIMatchPhase.Kickoff || dt <= 0f;
            _shapePrimed = true;
            float maxStep = TeamAIShapeMotion.SlideSpeed(phase, Difficulty.Theta) * dt;
            float k = dt > 0f ? 1f - (float)Math.Exp(-dt / TeamAIShapeMotion.ShapeSmoothing) : 1f;
            for (int s = 0; s < TeamAIFormations.SlotCount; s++)
            {
                if (snap)
                {
                    ShapeSpots[s] = Anchors[s];
                    AnchorVelocities[s] = SimVec3.Zero;
                    continue;
                }
                var step = TeamAIShapeMotion.ClampSpeed((Anchors[s] - ShapeSpots[s]) * k, maxStep);
                ShapeSpots[s] = ShapeSpots[s] + step;
                AnchorVelocities[s] = step * (1f / dt);
            }
            float lineZ = TeamAIMath.DepthToZ(line, sign, world.Pitch);
            if (snap) { _shapeLineZ = lineZ; _shapeLineVel = 0f; }
            else
            {
                float dz = (lineZ - _shapeLineZ) * k;
                if (dz > maxStep) dz = maxStep; else if (dz < -maxStep) dz = -maxStep;
                _shapeLineZ += dz;
                _shapeLineVel = dz / dt;
            }
        }

        private SimVec3 ShapeSpotFor(in TeamAIPlayer p)
        {
            int slot = p.FormationSlot;
            if (slot < 0 || slot >= TeamAIFormations.SlotCount) slot = 0;
            return ShapeSpots[slot];
        }

        private SimVec3 AnchorVelocityFor(in TeamAIPlayer p)
        {
            int slot = p.FormationSlot;
            if (slot < 0 || slot >= TeamAIFormations.SlotCount) return SimVec3.Zero;
            return AnchorVelocities[slot];
        }

        /// <summary>
        /// Hold a shape spot: the spot plus the player's adjustment drift, moving with the block's slide and the drift
        /// (<see cref="TeamAIShapeMotion"/>). A player far from the spot just runs there (the drift is ignored by the
        /// arrival logic until he is close; it is under 1.3 m).
        /// </summary>
        private void SetHold(ref AIPlayerCommand cmd, in TeamAIPlayer p, TeamAIWorld world, SimVec3 spot, SimVec3 spotVelocity,
            bool sprint, TeamAIIntent intent, bool lineKeeper, bool wide, float driftScale = 1f, int spaceFromCarrier = -1)
        {
            TeamAIShapeMotion.Adjustment(p.Id, world.Time, lineKeeper, wide, out var off, out var offVel, driftScale);
            if (lineKeeper)
            {
                // The back line steps up and drops together: one shared depth drift per team, so it stays level.
                TeamAIShapeMotion.Adjustment(TeamAIShapeMotion.LineDriftId + Team, world.Time, true, false, out var lineOff,
                    out var lineVel, driftScale);
                off = new SimVec3(off.X, 0f, lineOff.Z);
                offVel = new SimVec3(offVel.X, 0f, lineVel.Z);
            }
            var t = TeamAIMath.ClampToPitch(spot + off, world.Pitch, 0.5f);
            if (spaceFromCarrier >= 0) t = SpaceFromCarrier(world, spaceFromCarrier, t);
            var tv = TeamAIMath.Flat(spotVelocity + offVel);
            float limit = TeamAITempo.ShapeSpeed(world, Team, p, t, tv, Phase, Difficulty.Theta, world.MatchPhase != TeamAIMatchPhase.InPlay,
                ref sprint);
            SetMove(ref cmd, p, t, sprint, intent);
            cmd.MoveTargetVelocity = tv;
            cmd.SpeedLimit = limit;
        }

        private static void SetMove(ref AIPlayerCommand cmd, in TeamAIPlayer p, SimVec3 target, bool sprint, TeamAIIntent intent)
        {
            cmd.MoveTarget = new SimVec3(target.X, 0f, target.Z);
            float d = TeamAIMath.FlatDistance(p.Position, target);
            cmd.MoveDirection = d > 0.6f ? TeamAIMath.FlatDirection(p.Position, target) : SimVec3.Zero;
            cmd.Sprint = sprint && d > 1.5f;
            cmd.Intent = intent;
            cmd.MoveTargetVelocity = SimVec3.Zero;
            cmd.SpeedLimit = 0f;
            cmd.SprintSpeed = p.MaxSpeed;
        }

        /// <summary>Velocity of the offside line our runners time their runs against (flat, along the attack).</summary>
        private SimVec3 LineVelocity(int sign) => new SimVec3(0f, 0f, sign * _lineSpeed);

        /// <summary>SetMove for a target that moves with <paramref name="targetVelocity"/> (see <see cref="AIPlayerCommand.MoveTargetVelocity"/>).</summary>
        /// <remarks>
        /// Reading a moving target is a skill: only <see cref="TargetVelocityRead"/> of its velocity is passed on, so a
        /// weak side brakes early and pulls up short of a carrier running away while a strong one runs onto him.
        /// </remarks>
        private void SetMoveTracking(ref AIPlayerCommand cmd, in TeamAIPlayer p, SimVec3 target, SimVec3 targetVelocity,
            bool sprint, TeamAIIntent intent)
        {
            SetMove(ref cmd, p, target, sprint, intent);
            float read = TargetVelocityRead(Difficulty.Theta);
            cmd.MoveTargetVelocity = new SimVec3(targetVelocity.X * read, 0f, targetVelocity.Z * read);
        }

        /// <summary>
        /// Share (0..1) of a moving target's velocity the AI tracks: 0.35 + 1.3 x theta, so about 0.4 for Beginner,
        /// 0.85 for SemiPro and all of it from theta 0.5 up (Professional and above, and every AI teammate of the user).
        /// </summary>
        public static float TargetVelocityRead(float theta) => TeamAIMath.Clamp01(0.35f + 1.3f * theta);
    }
}
