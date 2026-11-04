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

        public bool IsNone => Id == 0 || Kind == AnimActionKind.None;
    }

    /// <summary>
    /// Watches the sim's event stream and keeps, per player, the current or upcoming contact-synchronised action for
    /// the animation layer: kicks and tackles (from KickWindup, which carries the contact tick), keeper saves, first
    /// touches, falls after fouls and goal celebrations. Call <see cref="Update"/> once per sim step.
    /// </summary>
    public sealed class SimAnimActionTracker
    {
        public const float HeaderHeight = 1.25f;
        /// <summary>How long after its contact an action is still reported (s).</summary>
        public const float HoldAfterContact = 0.6f;
        /// <summary>Celebrations and falls last longer.</summary>
        public const float LongHold = 2.5f;

        private readonly SimAnimAction[] _actions = new SimAnimAction[MatchSimState.PlayerCount];
        private int _nextId = 1;

        /// <summary>Monotonic sim time of the last update (s).</summary>
        public float SimTime { get; private set; }

        public SimAnimAction Get(int playerId)
        {
            var a = _actions[playerId];
            if (a.IsNone) return a;
            float hold = a.Kind == AnimActionKind.Celebrate || a.Kind == AnimActionKind.Fall ? LongHold : HoldAfterContact;
            return SimTime > a.ContactTime + hold ? default : a;
        }

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
                        if (e.PlayerId >= 0) StartNow(sim, e.PlayerId, AnimActionKind.GoalkeeperDive, e.Position, e.Position - s.Players[e.PlayerId].Position);
                        break;
                    case MatchEventType.FirstTouch:
                        if (e.PlayerId >= 0) StartNow(sim, e.PlayerId, AnimActionKind.FirstTouch, s.Ball.Position, s.Ball.Velocity);
                        break;
                    case MatchEventType.Foul:
                        if (e.OtherPlayerId >= 0) StartNow(sim, e.OtherPlayerId, AnimActionKind.Fall, s.Ball.Position, SimVec3.Zero);
                        break;
                    case MatchEventType.Goal:
                        if (e.PlayerId >= 0 && e.Detail == 0) StartNow(sim, e.PlayerId, AnimActionKind.Celebrate, s.Ball.Position, SimVec3.Zero);
                        break;
                }
            }
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
        }

        private void StartNow(MatchSim sim, int id, AnimActionKind kind, SimVec3 ball, SimVec3 dir)
        {
            var s = sim.State;
            ref readonly var p = ref s.Players[id];
            SimVec3 flat = dir.Flat;
            ActionSide side = ActionSide.Right;
            if (kind == AnimActionKind.GoalkeeperDive)
            {
                // Dive side: which side of the keeper's facing the ball is on.
                SimVec3 right = SimMath.FromYaw(p.Facing + SimMath.Pi * 0.5f);
                side = SimVec3.Dot((ball - p.Position).Flat, right) >= 0f ? ActionSide.Right : ActionSide.Left;
            }
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
        }
    }
}
