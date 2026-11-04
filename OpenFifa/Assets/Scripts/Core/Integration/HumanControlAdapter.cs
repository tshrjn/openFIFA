using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Glue between the control layer (<see cref="ControlIntent"/>, stick space) and the sim (<see cref="PlayerIntent"/>,
    /// pitch space): camera-relative stick mapping, control context from sim state, and action/aim translation with
    /// aim assist.
    /// </summary>
    public static class HumanControlAdapter
    {
        /// <summary>Wide channel (m from the touchline) where lob turns into cross.</summary>
        public const float CrossingChannel = 18f;
        /// <summary>Crossing only counts in the last this-many metres before the opponents' goal line.</summary>
        public const float CrossingDepth = 36f;

        /// <summary>
        /// Rotates a stick vector (+Y = up the screen) into a flat pitch direction for a camera with the given yaw
        /// (radians, 0 = looking along +Z, positive toward +X; same convention as <see cref="SimMath.Yaw"/>).
        /// Magnitude is preserved.
        /// </summary>
        public static SimVec3 StickToPitch(ControlVec2 stick, float cameraYaw)
        {
            float s = (float)System.Math.Sin(cameraYaw);
            float c = (float)System.Math.Cos(cameraYaw);
            // forward = (s, 0, c), right = (c, 0, -s)
            return new SimVec3(stick.Y * s + stick.X * c, 0f, stick.Y * c - stick.X * s);
        }

        /// <summary>Inverse of <see cref="StickToPitch"/>: a flat pitch direction to stick space for the camera yaw.</summary>
        public static ControlVec2 PitchToStick(SimVec3 dir, float cameraYaw)
        {
            float s = (float)System.Math.Sin(cameraYaw);
            float c = (float)System.Math.Cos(cameraYaw);
            return new ControlVec2(dir.X * c - dir.Z * s, dir.X * s + dir.Z * c);
        }

        /// <summary>Control context for the user's player from live sim state.</summary>
        public static ControlContext BuildContext(MatchSim sim, SimTeam humanTeam, int controlledId)
        {
            var s = sim.State;
            var ctx = new ControlContext { Time = s.Tick * sim.Dt };
            int ownerTeam = s.Owner >= 0 ? (int)MatchSim.TeamOf(s.Owner) : -1;
            ctx.InPossession = ownerTeam >= 0 ? ownerTeam == (int)humanTeam : s.PossessionTeam == humanTeam;
            if (controlledId < 0) return ctx;

            ref readonly var p = ref s.Players[controlledId];
            bool restartTaker = s.Phase == SimMatchPhase.AwaitingRestart && s.RestartTakerId == controlledId;
            bool live = s.Phase == SimMatchPhase.InPlay;
            if (p.IsFree && (restartTaker || (live && (s.Owner == controlledId || s.Owner < 0))))
            {
                // Window for a typical pass; shots wind up longer, but the buffer keeps them until the window opens.
                ctx.CanStrike = KickTiming.StrikeWindowOpen(sim, controlledId,
                    KickTiming.WindupSeconds(sim.Config.Actions, IntentAction.Pass, 0.5f));
            }

            var pitch = sim.Config.Pitch;
            float attack = sim.AttackCoord(humanTeam, p.Position);
            ctx.InCrossingZone = SimMath.Abs(p.Position.X) > pitch.HalfWidth - CrossingChannel
                                 && attack > pitch.HalfLength - CrossingDepth;
            return ctx;
        }

        public static IntentAction MapAction(ControlAction a)
        {
            switch (a)
            {
                case ControlAction.Pass:
                case ControlAction.DrivenPass: return IntentAction.Pass;
                case ControlAction.ThroughBall:
                case ControlAction.DrivenThrough: return IntentAction.ThroughPass;
                case ControlAction.LobbedThrough:
                case ControlAction.Lob:
                case ControlAction.DrivenLob: return IntentAction.Lob;
                case ControlAction.Cross: return IntentAction.Cross;
                case ControlAction.Shoot:
                case ControlAction.LowShot:
                case ControlAction.ChipShot: return IntentAction.Shoot;
                case ControlAction.FinesseShot: return IntentAction.FinesseShot;
                case ControlAction.DrivenShot: return IntentAction.DrivenShot;
                case ControlAction.Tackle: return IntentAction.Tackle;
                case ControlAction.SlideTackle: return IntentAction.SlideTackle;
                case ControlAction.Jockey:
                case ControlAction.Contain: return IntentAction.Jockey;
                case ControlAction.Shield: return IntentAction.Shield;
                // SkillMove, TeammatePress and SwitchPlayer are handled outside the sim intent.
                default: return IntentAction.None;
            }
        }

        /// <summary>
        /// Translates the control layer's output for the controlled player into a sim intent.
        /// Passes use <see cref="AimAssist.PassDirection"/> over <paramref name="teammates"/>; shots aim at a point on
        /// the opponents' goal line from <see cref="AimAssist.ShotTarget"/>.
        /// </summary>
        public static PlayerIntent ToIntent(in ControlIntent ci, float cameraYaw, MatchSim sim, int controlledId,
            IReadOnlyList<ControlPlayerInfo> teammates, float passAssist, float shotAssist)
        {
            var intent = PlayerIntent.Idle;
            if (controlledId < 0) return intent;
            ref readonly var p = ref sim.State.Players[controlledId];
            SimTeam team = MatchSim.TeamOf(controlledId);

            intent.Move = StickToPitch(ci.Move, cameraYaw);
            intent.Sprint = ci.Sprint || (ci.Held & ControlHeld.Sprint) != 0;

            IntentAction act = MapAction(ci.Action);
            if (act == IntentAction.None) return intent;
            intent.Action = act;
            intent.Power = SimMath.Clamp01(ci.Power);

            SimVec3 stick = StickToPitch(ci.Aim, cameraYaw).Flat;
            if (stick.SqrMagnitude < 1e-6f) stick = intent.Move.Flat;
            if (stick.SqrMagnitude < 1e-6f) stick = p.FacingDir;
            stick = stick.Normalized;

            if (PlayerIntent.IsPassAction(act))
            {
                SimVec3 dir = AimAssist.PassDirection(p.Position, controlledId, stick, teammates, passAssist, out int target);
                intent.Aim = dir;
                intent.TargetPlayerId = target;
                if (target >= 0 && act == IntentAction.Pass)
                {
                    float d = SimMath.FlatDistance(p.Position, sim.State.Players[target].Position);
                    intent.Power = AimAssist.PassPower(intent.Power, d, passAssist);
                }
            }
            else if (PlayerIntent.IsShotAction(act))
            {
                var pitch = sim.Config.Pitch;
                var goal = new SimVec3(0f, 0f, sim.AttackSign(team) * pitch.HalfLength);
                SimVec3 targetPoint = AimAssist.ShotTarget(p.Position, stick, goal, pitch.GoalWidth * 0.5f, shotAssist);
                SimVec3 dir = (targetPoint - p.Position).Flat;
                intent.Aim = dir.SqrMagnitude > 1e-6f ? dir.Normalized : stick;
            }
            else
            {
                intent.Aim = stick;
            }
            return intent;
        }

        /// <summary>
        /// Receive assist: while a pass to the controlled player is live and the stick is neutral, run to meet the ball
        /// (the user takes over as soon as they move the stick).
        /// </summary>
        public static void ApplyReceiveAssist(MatchSim sim, int controlledId, ref PlayerIntent intent)
        {
            var s = sim.State;
            if (controlledId < 0 || !s.PassLive || s.PassTarget != controlledId || s.Owner >= 0) return;
            if (intent.Move.Flat.SqrMagnitude > 0.04f) return;
            SimVec3 meet = KickTiming.InterceptPoint(sim, controlledId, out _);
            SimVec3 to = (meet - s.Players[controlledId].Position).Flat;
            float d = to.Magnitude;
            if (d < 0.4f) return;
            intent.Move = to / d;
            intent.Sprint = d > 6f;
        }
    }
}
