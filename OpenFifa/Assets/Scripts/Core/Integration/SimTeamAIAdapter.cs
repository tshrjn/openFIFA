using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Builds the team AI's <see cref="TeamAIWorld"/> from <see cref="MatchSim"/> state each tick (no allocation), and
    /// turns <see cref="AIPlayerCommand"/>s back into <see cref="PlayerIntent"/>s. World index == sim player id.
    /// </summary>
    public sealed class SimTeamAIAdapter
    {
        /// <summary>Distance (m) inside which a move target counts as reached.</summary>
        public const float ArriveRadius = 0.6f;
        /// <summary>Distance (m) over which the throttle ramps from 0 to 1 near the target.</summary>
        public const float SlowRadius = 3f;

        public readonly TeamAIWorld World;
        public readonly TeamAITactics HomeTactics;
        public readonly TeamAITactics AwayTactics;
        /// <summary>Team AI formation slot of every sim player id.</summary>
        public readonly int[] SlotOfPlayer = new int[MatchSimState.PlayerCount];
        public readonly TeamAIRole[] RoleOfPlayer = new TeamAIRole[MatchSimState.PlayerCount];

        private readonly MatchSim _sim;

        /// <summary>
        /// Pass null tactics to fit the formation template to the setup's kick-off shape.
        /// Given tactics keep their formation; slots are then matched to it.
        /// </summary>
        public SimTeamAIAdapter(MatchSim sim, TeamAITactics home = null, TeamAITactics away = null)
        {
            _sim = sim ?? throw new ArgumentNullException(nameof(sim));
            var setup = sim.Setup;
            HomeTactics = home ?? new TeamAITactics { Formation = SimFormationMapper.BestFit(setup.HomeFormation) };
            AwayTactics = away ?? new TeamAITactics { Formation = SimFormationMapper.BestFit(setup.AwayFormation) };
            World = new TeamAIWorld(sim.Config.Pitch);

            var map = new int[MatchSetup.PlayersPerTeam];
            for (int team = 0; team < 2; team++)
            {
                var tac = team == 0 ? HomeTactics : AwayTactics;
                SimFormationMapper.Assign(team == 0 ? setup.HomeFormation : setup.AwayFormation, tac.Formation, map);
                for (int s = 0; s < MatchSetup.PlayersPerTeam; s++)
                {
                    int id = team * MatchSetup.PlayersPerTeam + s;
                    SlotOfPlayer[id] = map[s];
                    RoleOfPlayer[id] = TeamAIFormations.RoleOf(tac.Formation, map[s]);
                }
            }

            for (int id = 0; id < MatchSimState.PlayerCount; id++)
            {
                var p = TeamAIPlayer.Create(id, id < MatchSetup.PlayersPerTeam ? 0 : 1, RoleOfPlayer[id], SlotOfPlayer[id], SimVec3.Zero);
                var a = sim.AttributesOf(id);
                p.Passing = Avg(a.ShortPassing, a.LongPassing, a.Vision);
                p.Shooting = Avg(a.Finishing, a.ShotPower, a.Composure);
                p.Dribbling = Avg(a.Dribbling, a.BallControl, a.Agility);
                p.Defending = Avg(a.StandingTackle, a.SlidingTackle, a.Reactions);
                p.Reactions = SimPlayerAttributes.N(a.Reactions);
                p.Goalkeeping = a.IsGoalkeeper ? Avg(a.GkDiving, a.GkReflexes, a.GkPositioning) : 0.1f;
                World.Players[id] = p;
            }
            World.PlayerCount = MatchSimState.PlayerCount;
        }

        private static float Avg(int a, int b, int c) => (SimPlayerAttributes.N(a) + SimPlayerAttributes.N(b) + SimPlayerAttributes.N(c)) / 3f;

        /// <summary>Copy the live sim state into <see cref="World"/>. humanMask bit i = player i is user-controlled.</summary>
        public void Fill(long humanMask = 0)
        {
            var s = _sim.State;
            var cfg = _sim.Config;
            for (int id = 0; id < MatchSimState.PlayerCount; id++)
            {
                ref var w = ref World.Players[id];
                ref readonly var p = ref s.Players[id];
                w.Position = p.Position.Flat;
                w.Velocity = p.Velocity.Flat;
                w.Facing = p.FacingDir;
                w.MaxSpeed = PlayerKinematics.SprintSpeed(_sim.AttributesOf(id), cfg.Kinematics, p.Stamina);
                w.Active = !p.SentOff;
                w.HumanControlled = (humanMask & (1L << id)) != 0;
            }

            World.Ball.Position = s.Ball.Position;
            World.Ball.Velocity = s.Ball.Velocity;
            World.Ball.Spin = s.Ball.Spin;
            World.Ball.OwnerId = s.Owner;
            World.Ball.LastTouchId = s.LastTouchPlayer;
            World.Ball.LastTouchTeam = TeamIndex(s.LastTouchTeam);
            World.HomeAttackSign = _sim.AttackSign(SimTeam.Home);
            World.Time = s.Tick * cfg.Dt; // monotonic across halves
            MapPhase(s, out World.MatchPhase, out World.RestartTeam);
            World.IndirectFreeKick = s.Phase != SimMatchPhase.InPlay && s.PendingRestart == RestartType.IndirectFreeKick;
        }

        public static int TeamIndex(SimTeam t) => t == SimTeam.Home ? 0 : (t == SimTeam.Away ? 1 : -1);

        public static void MapPhase(MatchSimState s, out TeamAIMatchPhase phase, out int restartTeam)
        {
            switch (s.Phase)
            {
                case SimMatchPhase.InPlay:
                    phase = TeamAIMatchPhase.InPlay;
                    restartTeam = -1;
                    return;
                case SimMatchPhase.AwaitingRestart:
                    phase = MapRestart(s.PendingRestart);
                    restartTeam = TeamIndex(s.RestartTeam);
                    return;
                case SimMatchPhase.Stoppage:
                    phase = TeamAIMatchPhase.Stopped;
                    restartTeam = TeamIndex(s.RestartTeam);
                    return;
                default:
                    phase = TeamAIMatchPhase.Stopped;
                    restartTeam = -1;
                    return;
            }
        }

        public static TeamAIMatchPhase MapRestart(RestartType r)
        {
            switch (r)
            {
                case RestartType.KickOff: return TeamAIMatchPhase.Kickoff;
                case RestartType.GoalKick: return TeamAIMatchPhase.GoalKick;
                case RestartType.Corner: return TeamAIMatchPhase.Corner;
                case RestartType.DirectFreeKick:
                case RestartType.IndirectFreeKick: return TeamAIMatchPhase.FreeKick;
                case RestartType.ThrowIn: return TeamAIMatchPhase.ThrowIn;
                case RestartType.Penalty: return TeamAIMatchPhase.Penalty;
                case RestartType.DropBall: return TeamAIMatchPhase.InPlay;
                default: return TeamAIMatchPhase.Stopped;
            }
        }

        public static IntentAction MapAction(TeamAIAction a)
        {
            switch (a)
            {
                case TeamAIAction.Pass: return IntentAction.Pass;
                case TeamAIAction.ThroughPass: return IntentAction.ThroughPass;
                case TeamAIAction.LobPass: return IntentAction.Lob;
                case TeamAIAction.Cross: return IntentAction.Cross;
                case TeamAIAction.Shoot: return IntentAction.Shoot;
                case TeamAIAction.FinesseShot: return IntentAction.FinesseShot;
                case TeamAIAction.DrivenShot: return IntentAction.DrivenShot;
                case TeamAIAction.Tackle: return IntentAction.Tackle;
                case TeamAIAction.SlideTackle: return IntentAction.SlideTackle;
                case TeamAIAction.Jockey: return IntentAction.Jockey;
                case TeamAIAction.Shield: return IntentAction.Shield;
                default: return IntentAction.None;
            }
        }

        /// <summary>
        /// Run-up assist: while a kick is winding up, the sim stops dribble touches, so a player who keeps running his
        /// own line leaves the ball behind and whiffs at contact. This steers the kicker at the ball's predicted position
        /// at the contact tick. Applied to AI and user players alike (the stick is ignored during the wind-up).
        /// </summary>
        public static void SteerWindUpToBall(MatchSim sim, int id, ref PlayerIntent intent)
        {
            var s = sim.State;
            ref readonly var p = ref s.Players[id];
            if (p.ActionState != SimPlayerActionState.WindUp || !PlayerIntent.IsKickAction(p.PendingAction)) return;
            if (s.Phase != SimMatchPhase.InPlay) return;
            int ticks = p.ContactTick - s.Tick;
            if (ticks < 0) ticks = 0;
            SimVec3 ball = s.Ball.Position + s.Ball.Velocity.Flat * (ticks * sim.Dt);
            // Aim to arrive just behind the ball along the kick direction.
            SimVec3 aim = p.PendingAim.Flat.SqrMagnitude > 1e-6f ? p.PendingAim.Flat.Normalized : p.FacingDir;
            SimVec3 spot = (ball - aim * 0.35f).Flat;
            SimVec3 to = spot - p.Position.Flat;
            float d = to.Magnitude;
            intent.Move = d > 0.05f ? to / d * SimMath.Clamp(d / 0.6f, 0f, 1f) : SimVec3.Zero;
            intent.Sprint = false;
        }

        /// <summary>
        /// Converts one AI command to a sim intent. Movement throttles down near the target. One-shot actions
        /// (kicks, tackles) are only forwarded when <paramref name="allowOneShot"/> is true, so a command that stays
        /// current for several ticks fires once; held actions (jockey, shield) pass every tick.
        /// </summary>
        public static PlayerIntent ToIntent(in AIPlayerCommand cmd, in SimPlayer player, bool allowOneShot)
        {
            var intent = PlayerIntent.Idle;
            SimVec3 to = (cmd.MoveTarget - player.Position).Flat;
            float dist = to.Magnitude;
            SimVec3 dir = cmd.MoveDirection.Flat;
            if (dir.SqrMagnitude < 1e-6f && dist > ArriveRadius) dir = to / dist;
            if (dir.SqrMagnitude > 1e-6f && dist > ArriveRadius)
            {
                float throttle = cmd.Sprint ? 1f : SimMath.Clamp((dist - ArriveRadius) / SlowRadius, 0.25f, 1f);
                intent.Move = dir.Normalized * throttle;
                intent.Sprint = cmd.Sprint;
            }

            IntentAction act = MapAction(cmd.Action);
            bool held = act == IntentAction.Jockey || act == IntentAction.Shield;
            if (act != IntentAction.None && (held || allowOneShot))
            {
                intent.Action = act;
                intent.Power = SimMath.Clamp01(cmd.Power);
                SimVec3 aim = cmd.Aim.Flat;
                if (aim.SqrMagnitude < 1e-6f) aim = (cmd.ActionTarget - player.Position).Flat;
                intent.Aim = aim.SqrMagnitude > 1e-6f ? aim.Normalized : SimVec3.Zero;
                intent.TargetPlayerId = PlayerIntent.IsPassAction(act) ? cmd.TargetPlayerId : -1;
            }
            return intent;
        }
    }
}
