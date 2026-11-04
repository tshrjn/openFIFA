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
        /// <summary>Deceleration (m/s^2) assumed when braking onto a target; under the sim's weakest braking (6 m/s^2).</summary>
        public const float ArriveDeceleration = 5f;
        /// <summary>Throttle while braking: low enough to shed speed, non-zero so the player keeps turning to the spot.</summary>
        public const float BrakeThrottle = 0.05f;
        /// <summary>Once settled (slower than <see cref="SettledSpeed"/>), a player only moves again beyond this distance (m).</summary>
        public const float ResumeRadius = 1.2f;
        /// <summary>Speed (m/s) under which a player counts as settled on his spot.</summary>
        public const float SettledSpeed = 0.6f;
        /// <summary>Throttle that turns a standing player to face the ball without moving him (under 2 cm/s).</summary>
        public const float FaceThrottle = 0.002f;
        /// <summary>Fastest sprint the sim allows (6.6 + 3.1 m/s) and the jog fraction, for the arrival speed cap.</summary>
        public const float MaxSprintSpeed = 9.7f;
        public const float MaxJogFraction = 0.62f;
        /// <summary>A move target slower than this (m/s) is treated as a fixed spot by the pace-keeping logic.</summary>
        public const float MovingTargetSpeed = 0.5f;
        /// <summary>
        /// A target with any velocity above this (m/s) is a drifting shape spot (<see cref="TeamAIShapeMotion"/>): its
        /// speed crosses zero on the ellipse's short axis (a back-line spot's depth drift peaks near 0.3 m/s), so it is
        /// never "settled on". Only an exactly fixed spot gets the <see cref="ResumeRadius"/> dead zone.
        /// </summary>
        public const float DriftTargetSpeed = 0.02f;
        /// <summary>Closing gain (1/s) when walking with a drifting spot inside the arrive radius.</summary>
        public const float DriftClosingGain = 1f;
        /// <summary>
        /// Typical jog and sprint top speeds (m/s; sim: sprint 6.6-9.7, jog 62% of it) used to turn a target's speed
        /// into a throttle. Only used to keep pace with a moving target; the live closing speed corrects any error.
        /// </summary>
        public const float AssumedJogSpeed = 5f;
        public const float AssumedSprintSpeed = 8f;
        /// <summary>
        /// Shape adjustments: a plain (non-sprint) move to a spot closer than this (m) that moves slower than
        /// <see cref="AdjustMaxTargetSpeed"/> is walked as a shuffle that keeps the body on the ball (the sim's held
        /// Jockey: faces the ball, strafes or backpedals), the way an off-ball player re-positions without turning his
        /// back on play. Further away, or for a fast target, he turns and runs.
        /// </summary>
        public const float AdjustRadius = 3f;
        public const float AdjustMaxTargetSpeed = 2f;
        /// <summary>Throttle cap of an adjustment shuffle (about 2 m/s at a typical jog top speed).</summary>
        public const float AdjustMaxThrottle = 0.4f;

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
                p.BallControl = SimPlayerAttributes.N(a.BallControl);
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
            World.Score[0] = s.HomeScore;
            World.Score[1] = s.AwayScore;
            float half = cfg.HalfDurationSeconds > 1f ? cfg.HalfDurationSeconds : 1f;
            float inHalf = s.Clock / half;
            if (inHalf > 1f) inHalf = 1f;
            World.MatchProgress = s.Phase == SimMatchPhase.FullTime ? 1f : 0.5f * (s.Half - 1) + 0.5f * inHalf;
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

        /// <summary>
        /// Throttle for the command's tempo limit (<see cref="AIPlayerCommand.SpeedLimit"/>, m/s) on a non-sprint move:
        /// the sim's jog top speed is <see cref="MaxJogFraction"/> of the player's sprint speed. 1 = no limit.
        /// </summary>
        public static float TempoThrottle(in AIPlayerCommand cmd)
        {
            if (cmd.SpeedLimit <= 0f) return 1f;
            return SimMath.Clamp01(cmd.SpeedLimit / JogTop(cmd));
        }

        /// <summary>The player's jog top speed (m/s) as the sim computes it: <see cref="MaxJogFraction"/> of his stamina-scaled sprint.</summary>
        public static float JogTop(in AIPlayerCommand cmd) => SprintTop(cmd) * MaxJogFraction;

        /// <summary>The player's (stamina-scaled) sprint top speed from the command, or the typical value when unknown.</summary>
        public static float SprintTop(in AIPlayerCommand cmd) => cmd.SprintSpeed > 0.5f ? cmd.SprintSpeed : AssumedSprintSpeed;

        /// <summary>Off-ball positioning intents that may shuffle facing the ball (never the carrier or a ball chaser).</summary>
        public static bool IsOffBallShapeIntent(TeamAIIntent intent)
        {
            switch (intent)
            {
                case TeamAIIntent.HoldShape:
                case TeamAIIntent.HoldWidth:
                case TeamAIIntent.KeepLine:
                case TeamAIIntent.Support:
                case TeamAIIntent.Mark:
                case TeamAIIntent.CoverShadow:
                case TeamAIIntent.GKPosition:
                    return true;
                default:
                    return false;
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

        /// <summary>A defender this far (m) upfield of the ball, out of possession, is caught upfield.</summary>
        public const float CaughtUpfieldMargin = 3f;
        /// <summary>... and sprints back when his spot is more than this (m) behind him.</summary>
        public const float RecoveryRunDistance = 8f;

        /// <summary>
        /// Recovery run: a back-line or holding player (CB, FB, DM) caught upfield of the ball while the other side has
        /// it (or touched it last) sprints back to his spot, whatever the team phase. The team AI only sends players back
        /// at full pace in its short Recovery phase; after it, a shape command (hold, keep line, mark) carries the
        /// off-ball tempo limit, so a centre-back left 30 m upfield by a long ball jogged home at 3-4 m/s. This lifts the
        /// limit and sets the sprint on such commands. Applied by the match session to the director's command before
        /// <see cref="ToIntent"/>; true when the command was changed.
        /// </summary>
        public bool ApplyRecoveryRun(ref AIPlayerCommand cmd, int id) => ApplyRecoveryRun(ref cmd, World, id);

        public static bool ApplyRecoveryRun(ref AIPlayerCommand cmd, TeamAIWorld world, int id)
        {
            if (world == null || id < 0 || id >= world.PlayerCount) return false;
            if (world.MatchPhase != TeamAIMatchPhase.InPlay) return false;
            ref readonly var p = ref world.Players[id];
            if (!p.Active || p.HumanControlled) return false;
            if (p.Role != TeamAIRole.CB && p.Role != TeamAIRole.FB && p.Role != TeamAIRole.DM) return false;
            if (cmd.Action != TeamAIAction.None || cmd.Sprint || !IsOffBallShapeIntent(cmd.Intent)) return false;
            int team = p.Team;
            int owner = world.Ball.OwnerId;
            bool theirBall = owner >= 0 && owner < world.PlayerCount
                ? world.Players[owner].Team != team
                : world.Ball.LastTouchTeam >= 0 && world.Ball.LastTouchTeam != team;
            if (!theirBall) return false;
            int sign = world.AttackSign(team);
            float upfield = (p.Position.Z - world.Ball.Position.Z) * sign;
            float spotBehind = (p.Position.Z - cmd.MoveTarget.Z) * sign;
            if (upfield < CaughtUpfieldMargin || spotBehind < RecoveryRunDistance) return false;
            cmd.Sprint = true;
            cmd.SpeedLimit = 0f;
            cmd.Intent = TeamAIIntent.Recover;
            return true;
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
        /// <para>
        /// Arrival braking is on the closing speed: for a fixed spot that is the player's speed, for a target moving
        /// away along the approach (<see cref="AIPlayerCommand.MoveTargetVelocity"/>: a fleeing carrier, a rolling
        /// ball, the offside line) only the speed in excess of the target's counts, so a presser keeps running onto a
        /// carrier instead of pulling up 6 m short of him.
        /// </para>
        /// </summary>
        public static PlayerIntent ToIntent(in AIPlayerCommand cmd, in SimPlayer player, bool allowOneShot)
        {
            var intent = PlayerIntent.Idle;
            // Steer by the live offset to the target every tick: the command (and its MoveDirection) is only refreshed
            // at the decision rate, and a sprinter following a 0.1 s old direction runs ~0.8 m past his spot.
            SimVec3 to = (cmd.MoveTarget - player.Position).Flat;
            float dist = to.Magnitude;
            SimVec3 tv = cmd.MoveTargetVelocity.Flat;
            float tvSpeed = tv.Magnitude;
            bool moving = tvSpeed > MovingTargetSpeed;
            float speed = player.Velocity.Flat.Magnitude;
            // Dead zone with hysteresis: a player who has settled (slow) only sets off again once the spot is more than
            // ResumeRadius away, so a target that jitters by a few tens of cm between decisions does not have him
            // stepping back and forth (and his body flipping) every 0.1 s.
            // Only for plain movement: an action's own small step (shielding 1 m away from the presser) still runs.
            bool plainMove = cmd.Action == TeamAIAction.None;
            // A drifting shape spot (any velocity) has no settle hysteresis: its speed passes through zero twice a lap,
            // and parking the player until it is 1.2 m away (about the drift's whole amplitude) made the back line
            // stop and go. Inside the arrive radius he walks with the drift instead (below).
            bool drifting = plainMove && !moving && tvSpeed > DriftTargetSpeed && IsOffBallShapeIntent(cmd.Intent);
            float radius = plainMove && !moving && !drifting && speed < SettledSpeed ? ResumeRadius : ArriveRadius;
            if (dist > radius)
            {
                SimVec3 dir = to / dist;
                float throttle = cmd.Sprint ? 1f : SimMath.Clamp((dist - ArriveRadius) / SlowRadius, 0.25f, 1f);
                bool sprint = cmd.Sprint;
                float recede = 0f;
                if (moving)
                {
                    // Keep pace with a target running away along the approach, then close on top of that.
                    // Against this player's own jog top (stamina-scaled) when it is under the typical one: a slow or
                    // tired player whose jog cannot keep pace switches to the sprint gait instead of falling behind.
                    float jogTop = Math.Min(AssumedJogSpeed, JogTop(cmd));
                    recede = SimMath.Max(0f, SimVec3.Dot(tv, dir));
                    if (recede > jogTop * 0.9f) { sprint = true; throttle = 1f; }
                    else if (!sprint) throttle = SimMath.Max(throttle, SimMath.Min(1f, recede / jogTop + 0.25f));
                }
                // Arrive: cap the speed so the closing speed never exceeds what a sure deceleration can shed before the
                // spot. The cap is continuous in the distance (no brake/release toggling between ticks) and assumes the
                // fastest top speed, so no player is let in faster than the profile allows.
                float allowed = (float)Math.Sqrt(2f * ArriveDeceleration * SimMath.Max(0f, dist - ArriveRadius * 0.5f));
                float cap = (allowed + 0.25f + recede) / (sprint ? MaxSprintSpeed : MaxSprintSpeed * MaxJogFraction);
                if (cap < throttle) throttle = SimMath.Max(cap, BrakeThrottle);
                if (!sprint) throttle = SimMath.Min(throttle, TempoThrottle(cmd));
                intent.Move = dir * throttle;
                intent.Sprint = sprint;
            }
            else if (drifting)
            {
                // Walk with the drifting spot, closing the remaining gap gently: continuous motion, no park-and-restart.
                SimVec3 want = tv + to * DriftClosingGain;
                float th = SimMath.Clamp01(want.Magnitude / JogTop(cmd));
                th = SimMath.Min(th, TempoThrottle(cmd));
                float wm = want.Magnitude;
                intent.Move = wm > 1e-4f ? want / wm * th : SimVec3.Zero;
            }
            else if (moving)
            {
                // On a moving target: run with it.
                float jogTop = Math.Min(AssumedJogSpeed, JogTop(cmd));
                bool sprint = tvSpeed > jogTop * 0.9f;
                float th = SimMath.Clamp01(tvSpeed / (sprint ? Math.Min(AssumedSprintSpeed, SprintTop(cmd)) : jogTop));
                if (!sprint) th = SimMath.Min(th, TempoThrottle(cmd));
                intent.Move = tv / tvSpeed * th;
                intent.Sprint = sprint;
            }
            else if (cmd.FaceLookAt && plainMove)
            {
                // Holding the spot: no micro-steering, just turn to face the ball. A throttle this small turns the body
                // (the sim faces the move direction below 0.3 m/s) while asking for under 2 cm/s.
                SimVec3 look = (cmd.LookAt - player.Position).Flat;
                if (look.SqrMagnitude > 0.25f)
                {
                    // Still coasting in: brake facing the ball (held Jockey, no move) instead of swinging the body round
                    // to the run direction for the last half metre; settled: the tiny turn-only throttle.
                    if (speed > 0.3f && IsOffBallShapeIntent(cmd.Intent)) intent.Action = IntentAction.Jockey;
                    else intent.Move = look.Normalized * FaceThrottle;
                }
            }

            // Small shape adjustment: shuffle facing the ball instead of turning the body to every 1 m step.
            // Hysteresis on the sim's own Jockeying flag (last tick's mode): stay in the shuffle until clearly out of it,
            // so the body does not swap between facing the ball and facing the run every few ticks.
            bool shuffling = player.Jockeying;
            float adjustRadius = shuffling ? AdjustRadius + 1f : AdjustRadius;
            float adjustTargetSpeed = shuffling ? AdjustMaxTargetSpeed + 0.5f : AdjustMaxTargetSpeed;
            if (plainMove && IsOffBallShapeIntent(cmd.Intent) && !intent.Sprint && cmd.FaceLookAt && dist < adjustRadius && tvSpeed < adjustTargetSpeed &&
                (shuffling || speed < 2.6f) &&
                intent.Move.SqrMagnitude > FaceThrottle * FaceThrottle * 4f && (cmd.LookAt - player.Position).Flat.SqrMagnitude > 0.25f)
            {
                float m = intent.Move.Magnitude;
                if (m > AdjustMaxThrottle) intent.Move = intent.Move * (AdjustMaxThrottle / m);
                intent.Action = IntentAction.Jockey;
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
