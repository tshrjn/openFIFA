namespace OpenFifa.Core
{
    /// <summary>Engine-free copy of the presentation layer's PlayerAnimAction (Gameplay converts it 1:1).</summary>
    public struct SimAnimAction
    {
        /// <summary>Unique, increasing across the whole match (0 = none). Same events give the same ids.</summary>
        public int Id;
        public AnimActionKind Kind;
        public ActionSide Side;
        /// <summary>Contact time on the monotonic sim clock (tick * dt; does not reset at half time).</summary>
        public float ContactTime;
        public int ContactTick;
        public SimVec3 BallAtContact;
        public SimVec3 Direction;
        public SimVec3 RootAtContact;
        /// <summary>Facing at contact, radians (0 = +Z, positive toward +X), like the sim.</summary>
        public float YawAtContact;
        public bool HasRootAtContact;

        /// <summary>
        /// A dribble touch (Kind is <see cref="AnimActionKind.FirstTouch"/>): presentation should play it as a running
        /// touch (foot IK to <see cref="BallAtContact"/> at <see cref="ContactTime"/>, or an additive touch layer) without
        /// leaving locomotion.
        /// </summary>
        public bool IsDribbleTouch;

        public bool IsNone => Id == 0 || Kind == AnimActionKind.None;
    }

    /// <summary>
    /// Watches the sim's event stream and keeps, per player, the current or upcoming contact-synchronised action for
    /// the animation layer: kicks and tackles (from KickWindup, which carries the contact tick), keeper saves, first
    /// touches, dribble touches, falls after fouls and goal celebrations. Call <see cref="Update"/> once per sim step.
    ///
    /// Contacts the sim resolves without a wind-up are predicted so the clip can start ahead of them and its contact
    /// frame lands on the tick the sim resolves the contact:
    /// <list type="bullet">
    /// <item>Dribble touches: <see cref="MatchSim.TryPredictDribbleTouch"/>, the sim's own rule: the first tick at or
    /// after <see cref="SimPlayer.NextTouchTick"/> at which the owner has caught up to within
    /// <see cref="MatchSim.DribbleCatchUpFactor"/> x ControlRadius of the ball (a sprinting player pushes it 1.4-3 m
    /// ahead, so the touch usually comes well after NextTouchTick).</item>
    /// <item>First touches: the loose ball's path (the sim's own integrator, <see cref="PredictSeconds"/> ahead) against
    /// each player's extrapolated position and control radius; the earliest eligible player gets the action.</item>
    /// <item>Keeper saves: the substep where the ball reaches the keeper's plane (the sim's closest-approach rule), for a
    /// keeper with a save attempt pending and the ball near his reach. Beaten keepers still dive.</item>
    /// </list>
    /// Predictions are refreshed every update with a stable <see cref="SimAnimAction.Id"/>; when the sim resolves the
    /// contact (DribbleTouch / FirstTouch / Save event) the same action snaps to the resolved tick. A prediction that
    /// disappears before its contact (someone else got there first) is withdrawn.
    /// </summary>
    public sealed class SimAnimActionTracker
    {
        public const float HeaderHeight = 1.25f;
        /// <summary>How long after its contact an action is still reported (s).</summary>
        public const float HoldAfterContact = 0.6f;
        /// <summary>Celebrations and falls last longer.</summary>
        public const float LongHold = 2.5f;
        /// <summary>
        /// A resolved touch (dribble or first touch) yields to the next predicted dribble touch after this (s): the touch
        /// IK's whole blend-out (<see cref="DribbleTouchSolver.BlendOut"/>) plus a little over one 60 Hz tick, because the
        /// driver renders up to one tick behind the sim. A shorter hold hands the driver the next touch's id while the
        /// previous touch is still at 20-30 % IK weight, and the switch drops it to 0 in one frame (foot pop).
        /// </summary>
        public const float TouchHold = DribbleTouchSolver.BlendOut + 0.02f;
        /// <summary>How far ahead loose-ball contacts (first touches, saves) are predicted (s).</summary>
        public const float PredictSeconds = 1.0f;

        private readonly SimAnimAction[] _actions = new SimAnimAction[MatchSimState.PlayerCount];
        /// <summary>_actions[i] is an unconfirmed prediction.</summary>
        private readonly bool[] _predicted = new bool[MatchSimState.PlayerCount];
        private readonly bool[] _refreshed = new bool[MatchSimState.PlayerCount];
        private SimVec3[] _path = new SimVec3[1];
        private SimVec3[] _pathVel = new SimVec3[1];
        /// <summary>Full ball state at each path tick, so the next tick can tell whether the sim followed the prediction.</summary>
        private SimBall[] _pathBall = new SimBall[1];
        /// <summary>Tick the cached path was built on (-1 = none) and the sim it belongs to.</summary>
        private int _pathTick = -1;
        private int _pathSteps;
        private MatchSim _pathSim;
        private int _nextId = 1;

        /// <summary>Monotonic sim time of the last update (s).</summary>
        public float SimTime { get; private set; }

        public SimAnimAction Get(int playerId)
        {
            var a = _actions[playerId];
            if (a.IsNone) return a;
            return SimTime > a.ContactTime + HoldOf(a) ? default : a;
        }

        private static float HoldOf(in SimAnimAction a) =>
            a.Kind == AnimActionKind.Celebrate || a.Kind == AnimActionKind.Fall ? LongHold : HoldAfterContact;

        public void Update(MatchSim sim, System.Collections.Generic.IReadOnlyList<MatchEvent> events)
        {
            var s = sim.State;
            float dt = sim.Dt;
            SimTime = s.Tick * dt;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                switch (e.Type)
                {
                    case MatchEventType.KickWindup:
                        if (e.PlayerId >= 0) StartKick(sim, e.PlayerId, e.Action, e.Detail);
                        break;
                    case MatchEventType.Save:
                        if (e.PlayerId >= 0)
                            Resolve(sim, e.PlayerId, AnimActionKind.GoalkeeperDive, false, e.Tick, e.Position,
                                e.Position - s.Players[e.PlayerId].Position);
                        break;
                    case MatchEventType.FirstTouch:
                        if (e.PlayerId >= 0) Resolve(sim, e.PlayerId, AnimActionKind.FirstTouch, false, e.Tick, s.Ball.Position, s.Ball.Velocity);
                        break;
                    case MatchEventType.DribbleTouch:
                        if (e.PlayerId >= 0) Resolve(sim, e.PlayerId, AnimActionKind.FirstTouch, true, e.Tick, e.Position, s.Ball.Velocity);
                        break;
                    case MatchEventType.Foul:
                        if (e.OtherPlayerId >= 0) StartNow(sim, e.OtherPlayerId, AnimActionKind.Fall, s.Ball.Position, SimVec3.Zero);
                        break;
                    case MatchEventType.Goal:
                        if (e.PlayerId >= 0 && e.Detail == 0) StartNow(sim, e.PlayerId, AnimActionKind.Celebrate, s.Ball.Position, SimVec3.Zero);
                        break;
                }
            }

            Predict(sim);
        }

        public static AnimActionKind KindOf(IntentAction a, float ballHeight)
        {
            switch (a)
            {
                case IntentAction.Tackle: return AnimActionKind.StandingTackle;
                case IntentAction.SlideTackle: return AnimActionKind.SlideTackle;
                case IntentAction.Cross: return ballHeight > HeaderHeight ? AnimActionKind.Header : AnimActionKind.Cross;
                case IntentAction.Shoot:
                case IntentAction.FinesseShot:
                case IntentAction.DrivenShot: return ballHeight > HeaderHeight ? AnimActionKind.Header : AnimActionKind.Shot;
                case IntentAction.Pass:
                case IntentAction.ThroughPass:
                case IntentAction.Lob: return ballHeight > HeaderHeight ? AnimActionKind.Header : AnimActionKind.Pass;
                default: return AnimActionKind.None;
            }
        }

        private void StartKick(MatchSim sim, int id, IntentAction act, int contactTick)
        {
            var s = sim.State;
            ref readonly var p = ref s.Players[id];
            float dt = sim.Dt;
            int ticks = contactTick - s.Tick;
            if (ticks < 0) ticks = 0;
            float lead = ticks * dt;

            // Root: the sim caps speed during the wind-up, so extrapolating the current velocity is close.
            SimVec3 root = (p.Position + p.Velocity.Flat * lead).Flat;
            SimVec3 ball;
            if (s.Owner == id) ball = new SimVec3(root.X, sim.Config.Ball.Radius, root.Z) + p.FacingDir * 0.45f;
            else ball = sim.PredictBall(lead).Position;

            SimVec3 dir = p.PendingAim.Flat.SqrMagnitude > 1e-6f ? p.PendingAim.Flat.Normalized : p.FacingDir;
            var a = sim.AttributesOf(id);
            var action = new SimAnimAction
            {
                Id = _nextId++,
                Kind = KindOf(act, ball.Y),
                Side = a.PreferredFoot == SimFoot.Left ? ActionSide.Left : ActionSide.Right,
                ContactTick = contactTick,
                ContactTime = contactTick * dt,
                BallAtContact = ball,
                Direction = dir,
                RootAtContact = root,
                YawAtContact = SimMath.Yaw(dir),
                HasRootAtContact = true,
            };
            _actions[id] = action;
            _predicted[id] = false;
        }

        /// <summary>The sim resolved a contact: snap the matching prediction to it (same id), or start it now.</summary>
        private void Resolve(MatchSim sim, int id, AnimActionKind kind, bool dribble, int tick, SimVec3 ball, SimVec3 dir)
        {
            ref SimAnimAction a = ref _actions[id];
            if (_predicted[id] && a.Kind == kind && a.IsDribbleTouch == dribble)
            {
                ref readonly var p = ref sim.State.Players[id];
                SimVec3 flat = dir.Flat;
                a.ContactTick = tick;
                a.ContactTime = tick * sim.Dt;
                a.BallAtContact = ball;
                if (flat.SqrMagnitude > 1e-6f) a.Direction = flat.Normalized;
                a.RootAtContact = p.Position.Flat;
                a.YawAtContact = p.Facing;
                if (kind == AnimActionKind.GoalkeeperDive) a.Side = DiveSide(p, ball);
                _predicted[id] = false;
                return;
            }

            StartNow(sim, id, kind, ball, dir);
            _actions[id].IsDribbleTouch = dribble;
        }

        private static ActionSide DiveSide(in SimPlayer p, SimVec3 ball) => DiveSide(p.Position, p.Facing, ball);

        private static ActionSide DiveSide(SimVec3 keeper, float facing, SimVec3 ball)
        {
            // Dive side: which side of the keeper's facing the ball is on.
            SimVec3 right = SimMath.FromYaw(facing + SimMath.Pi * 0.5f);
            return SimVec3.Dot((ball - keeper).Flat, right) >= 0f ? ActionSide.Right : ActionSide.Left;
        }

        private void StartNow(MatchSim sim, int id, AnimActionKind kind, SimVec3 ball, SimVec3 dir)
        {
            var s = sim.State;
            ref readonly var p = ref s.Players[id];
            SimVec3 flat = dir.Flat;
            ActionSide side = kind == AnimActionKind.GoalkeeperDive ? DiveSide(p, ball) : ActionSide.Right;
            _actions[id] = new SimAnimAction
            {
                Id = _nextId++,
                Kind = kind,
                Side = side,
                ContactTick = s.Tick,
                ContactTime = s.Tick * sim.Dt,
                BallAtContact = ball,
                Direction = flat.SqrMagnitude > 1e-6f ? flat.Normalized : SimVec3.Zero,
                RootAtContact = p.Position.Flat,
                YawAtContact = p.Facing,
                HasRootAtContact = true,
            };
            _predicted[id] = false;
        }

        // ---------------------------------------------------------------- prediction

        /// <summary>
        /// A confirmed action that a new prediction must not replace yet: anything still pending or within its hold,
        /// except touches, which yield after <see cref="TouchHold"/>.
        /// </summary>
        private bool Busy(int id)
        {
            ref readonly var a = ref _actions[id];
            if (a.IsNone || _predicted[id]) return false;
            float hold = a.Kind == AnimActionKind.FirstTouch ? TouchHold : HoldOf(a);
            return SimTime <= a.ContactTime + hold;
        }

        private void SetPredicted(MatchSim sim, int id, AnimActionKind kind, bool dribble, int tick, SimVec3 ball, SimVec3 dir)
        {
            _refreshed[id] = true;
            ref SimAnimAction a = ref _actions[id];
            ref readonly var p = ref sim.State.Players[id];
            float lead = (tick - sim.State.Tick) * sim.Dt;
            SimVec3 flat = dir.Flat;
            bool same = _predicted[id] && a.Kind == kind && a.IsDribbleTouch == dribble;
            if (!same)
            {
                a = new SimAnimAction { Id = _nextId++, Kind = kind, IsDribbleTouch = dribble, HasRootAtContact = true };
                _predicted[id] = true;
            }

            a.ContactTick = tick;
            a.ContactTime = tick * sim.Dt;
            a.BallAtContact = ball;
            a.Direction = flat.SqrMagnitude > 1e-6f ? flat.Normalized : SimVec3.Zero;
            a.RootAtContact = (p.Position + p.Velocity.Flat * lead).Flat;
            a.YawAtContact = p.Facing;
            if (kind == AnimActionKind.GoalkeeperDive)
            {
                // The AI keeps moving the keeper until the save: side and reach direction from where he will be.
                a.Side = DiveSide(a.RootAtContact, p.Facing, ball);
                SimVec3 reach = (ball - a.RootAtContact).Flat;
                if (reach.SqrMagnitude > 1e-6f) a.Direction = reach.Normalized;
            }
            else
            {
                var attr = sim.AttributesOf(id);
                a.Side = attr.PreferredFoot == SimFoot.Left ? ActionSide.Left : ActionSide.Right;
            }
        }

        private void Predict(MatchSim sim)
        {
            var s = sim.State;
            for (int i = 0; i < MatchSimState.PlayerCount; i++) _refreshed[i] = false;

            if (s.Phase == SimMatchPhase.InPlay)
            {
                if (s.Owner >= 0 && !s.KeeperHolding) PredictDribbleTouch(sim, s.Owner);
                else if (s.Owner < 0 && s.Ball.InNet == 0) PredictLooseBall(sim);
            }

            // Withdraw predictions that were not refreshed: before their contact they did not happen (someone else got
            // the ball, play stopped); at or after it they are kept as played (a beaten keeper still dived).
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                if (!_predicted[i] || _refreshed[i]) continue;
                _predicted[i] = false;
                if (_actions[i].ContactTick > s.Tick + 1) _actions[i] = default;
            }
        }

        private void PredictDribbleTouch(MatchSim sim, int id)
        {
            var s = sim.State;
            ref readonly var p = ref s.Players[id];
            if (p.SentOff || (!p.IsFree && p.ActionState != SimPlayerActionState.Recovery) || Busy(id)) return;
            // No touch within the horizon (still catching up, or about to lose the ball): nothing to announce yet; an
            // earlier prediction is withdrawn and comes back once the touch is in range.
            if (!sim.TryPredictDribbleTouch(id, sim.Config.SecondsToTicks(PredictSeconds), out int tick, out SimVec3 ball)) return;
            SimVec3 dir = p.Speed > 0.5f ? p.Velocity.Flat : p.FacingDir;
            SetPredicted(sim, id, AnimActionKind.FirstTouch, true, tick, ball, dir);
        }

        private void PredictLooseBall(MatchSim sim)
        {
            var s = sim.State;
            var cfg = sim.Config;
            var ac = cfg.Actions;
            float dt = sim.Dt;
            float bdt = cfg.BallDt;
            int n = cfg.SecondsToTicks(PredictSeconds);
            if (_path.Length < n + 1)
            {
                _path = new SimVec3[n + 1];
                _pathVel = new SimVec3[n + 1];
                _pathBall = new SimBall[n + 1];
                _pathTick = -1;
            }

            // Keepers with a save attempt to come for this kick (the sim's own rule).
            int homeGk = MatchSim.KeeperId(SimTeam.Home), awayGk = MatchSim.KeeperId(SimTeam.Away);
            bool homeSave = SaveCandidate(sim, homeGk), awaySave = SaveCandidate(sim, awayGk);
            int saveTick = int.MaxValue;
            int saver = -1;
            SimVec3 saveBall = SimVec3.Zero;

            SimBall b = s.Ball;
            int startK = 1;
            bool saveFree = !homeSave && !awaySave;
            if (saveFree && _pathTick == s.Tick - 1 && _pathSim == sim && _pathSteps == n && SameBall(_pathBall[1], s.Ball))
            {
                // The sim moved the ball exactly as last tick's free-flight path predicted, so that path from k = 1 on is
                // this tick's path from k = 0: shift it and integrate only the new final tick. Bit-identical to a rebuild.
                System.Array.Copy(_path, 1, _path, 0, n);
                System.Array.Copy(_pathVel, 1, _pathVel, 0, n);
                System.Array.Copy(_pathBall, 1, _pathBall, 0, n);
                b = _pathBall[n - 1];
                startK = n;
            }
            int steps = startK - 1;
            _path[0] = s.Ball.Position;
            _pathVel[0] = s.Ball.Velocity;
            _pathBall[0] = s.Ball;
            for (int k = startK; k <= n; k++)
            {
                for (int sub = 0; sub < cfg.BallSubsteps; sub++)
                {
                    BallIntegrator.Step(ref b, cfg.Ball, cfg.Pitch, bdt);
                    if (saver < 0 && (homeSave || awaySave))
                    {
                        if (homeSave && SaveContact(sim, homeGk, b, out SimVec3 ca, ref homeSave)) { saver = homeGk; saveTick = s.Tick + k; saveBall = ca; }
                        else if (awaySave && SaveContact(sim, awayGk, b, out ca, ref awaySave)) { saver = awayGk; saveTick = s.Tick + k; saveBall = ca; }
                    }
                }

                _path[k] = b.Position;
                _pathVel[k] = b.Velocity;
                _pathBall[k] = b;
                steps = k;
                if (b.InNet != 0 || saver >= 0) break;
            }

            // Keep the path for the next tick only when it is a complete free flight with no keeper save checks.
            _pathTick = saveFree && saver < 0 && b.InNet == 0 ? s.Tick : -1;
            _pathSteps = steps;
            _pathSim = sim;

            if (saver >= 0)
            {
                if (!Busy(saver))
                    SetPredicted(sim, saver, AnimActionKind.GoalkeeperDive, false, saveTick, saveBall,
                        saveBall - s.Players[saver].Position);
                // The save resolves before any pickup at or after that tick.
                steps = saveTick - s.Tick - 1;
            }

            // First touch: the earliest player whose control radius the ball enters (UpdatePossession's rule).
            int best = -1, bestK = int.MaxValue;
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                ref readonly var p = ref s.Players[i];
                if (p.SentOff || i == saver) continue;
                if (p.ActionState == SimPlayerActionState.Grounded || p.ActionState == SimPlayerActionState.Sliding) continue;
                if (Busy(i) || (p.ActionState == SimPlayerActionState.WindUp)) continue;
                bool keeperHands = sim.AttributesOf(i).IsGoalkeeper && sim.IsInOwnPenaltyArea(p.Team, p.Position);
                if (keeperHands && sim.IsSavePending(i)) continue;
                float radius = keeperHands ? ac.ControlRadius * 1.5f : ac.ControlRadius;
                float maxH = keeperHands ? ac.GkMaxReachHeight : ac.ControlMaxHeight;
                float r2 = radius * radius;
                SimVec3 v = p.Velocity.Flat;
                int last = bestK - 1 < steps ? bestK - 1 : steps;
                for (int k = 1; k <= last; k++)
                {
                    if (s.Tick + k < p.ControlBlockedUntilTick) continue;
                    SimVec3 ball = _path[k];
                    if (ball.Y > maxH) continue;
                    SimVec3 at = p.Position + v * (k * dt);
                    float dx = ball.X - at.X, dz = ball.Z - at.Z;
                    if (dx * dx + dz * dz <= r2)
                    {
                        best = i;
                        bestK = k;
                        break;
                    }
                }
            }

            if (best >= 0)
                SetPredicted(sim, best, AnimActionKind.FirstTouch, false, s.Tick + bestK, _path[bestK], _pathVel[bestK]);
        }

        /// <summary>Bit-exact ball state comparison (floats compared by bits, so -0 and NaN never match loosely).</summary>
        private static bool SameBall(in SimBall a, in SimBall b) =>
            Same(a.Position, b.Position) && Same(a.Velocity, b.Velocity) && Same(a.Spin, b.Spin) &&
            a.InNet == b.InNet && a.Grounded == b.Grounded;

        private static bool Same(SimVec3 a, SimVec3 b) =>
            Bits(a.X) == Bits(b.X) && Bits(a.Y) == Bits(b.Y) && Bits(a.Z) == Bits(b.Z);

        private static int Bits(float f) => System.BitConverter.SingleToInt32Bits(f);

        private static bool SaveCandidate(MatchSim sim, int gk)
        {
            ref readonly var k = ref sim.State.Players[gk];
            if (k.SentOff || k.ActionState == SimPlayerActionState.Grounded) return false;
            if (!sim.AttributesOf(gk).IsGoalkeeper || !sim.IsInOwnPenaltyArea(k.Team, k.Position)) return false;
            return sim.IsSavePending(gk);
        }

        /// <summary>
        /// The sim's closest-approach test for one ball substep (see MatchSim.TrySaves): true when the keeper's save is
        /// resolved on this substep and the ball is near enough his reach to be worth a dive. Clears
        /// <paramref name="pending"/> once the ball is past him or no longer heading at goal.
        /// </summary>
        private static bool SaveContact(MatchSim sim, int gk, SimBall b, out SimVec3 ca, ref bool pending)
        {
            ca = SimVec3.Zero;
            ref readonly var k = ref sim.State.Players[gk];
            if (!sim.IsBallHeadingAtGoal(k.Team, b)) return false;
            var cfg = sim.Config;
            float bdt = cfg.BallDt;
            SimVec3 flatV = b.Velocity.Flat;
            float flatSpeed = flatV.Magnitude;
            if (flatSpeed < 1e-4f) return false;
            SimVec3 dir = flatV / flatSpeed;
            SimVec3 rel = (k.Position - b.Position).Flat;
            float along = SimVec3.Dot(rel, dir);
            if (along > flatSpeed * bdt) return false;
            pending = false; // the keeper's one chance is on this substep
            if (along < -0.5f) return false;
            float tCa = SimMath.Max(0f, along) / flatSpeed;
            ca = b.Position + b.Velocity * tCa + new SimVec3(0f, -0.5f * cfg.Ball.Gravity * tCa * tCa, 0f);
            var ka = sim.AttributesOf(gk);
            var ac = cfg.Actions;
            float reach = ac.GkStandingReach + ac.GkDiveReachRange * SimPlayerAttributes.N(ka.GkDiving);
            float lateral = SimMath.Abs(dir.X * rel.Z - dir.Z * rel.X) + SimMath.Max(0f, ca.Y - 2.0f);
            // Dive for anything in reach (saved or beaten) and for near misses; let wide balls go.
            return ca.Y <= ac.GkMaxReachHeight + 0.5f && lateral <= reach + 1.0f;
        }
    }
}
