namespace OpenFifa.Core
{
    /// <summary>
    /// When a kick can start. The sim stops dribble touches during a wind-up, and a sprint touch puts the ball up to
    /// ~3 m ahead, so a kick requested at the wrong moment reaches its contact tick with the ball out of reach (a Whiff).
    /// Both the user's kick buffer (via ControlContext.CanStrike) and the AI's kick latch wait for a strike window.
    /// </summary>
    public static class KickTiming
    {
        /// <summary>Fraction of KickReach the predicted contact distance must be within.</summary>
        public const float ReachMargin = 0.85f;

        public static float WindupSeconds(SimActionConfig ac, IntentAction act, float power)
        {
            switch (act)
            {
                case IntentAction.Lob: return ac.LobWindup;
                case IntentAction.Cross: return ac.CrossWindup;
                case IntentAction.Shoot: return ac.ShotWindup + ac.ShotWindupPerPower * power;
                case IntentAction.FinesseShot: return ac.FinesseWindup;
                case IntentAction.DrivenShot: return ac.DrivenWindup + ac.ShotWindupPerPower * power * 0.5f;
                default: return ac.PassWindup + ac.PassWindupPerPower * power;
            }
        }

        /// <summary>
        /// True when a kick started now would most likely find the ball at contact: the ball (rolling on) and the
        /// player (slowing to ~97% per tick, at least 1.5 m/s) end up within reach of each other.
        /// </summary>
        public static bool StrikeWindowOpen(MatchSim sim, int id, float windupSeconds)
        {
            var s = sim.State;
            if (s.Phase == SimMatchPhase.AwaitingRestart) return s.RestartTakerId == id;
            if (s.Phase != SimMatchPhase.InPlay) return false;
            ref readonly var p = ref s.Players[id];
            if (!p.IsFree) return false;
            var ac = sim.Config.Actions;
            if (s.Owner == id && s.KeeperHolding) return true;
            float h = s.Ball.Position.Y;
            if (h > ac.HeaderMaxHeight) return false;
            float t = windupSeconds;
            SimVec3 ball = (s.Ball.Position + s.Ball.Velocity.Flat * t).Flat;
            // Player keeps most of his speed (the sim caps it at max(1.5, 0.97 * speed) per tick); approximate the
            // distance covered with the average speed over the wind-up, steering at the ball.
            float speed = p.Speed;
            int ticks = sim.Config.SecondsToTicks(t);
            float covered = 0f, v = speed;
            for (int i = 0; i < ticks; i++)
            {
                v = SimMath.Max(1.5f, v * 0.97f);
                covered += v * sim.Dt;
            }
            float gap = SimMath.FlatDistance(p.Position, ball);
            float reach = ac.KickReach * ReachMargin;
            return gap - covered <= reach || gap <= reach;
        }

        /// <summary>True when a tackle started now would reach the ball: free, in play, ball within range at contact.</summary>
        public static bool TackleWindowOpen(MatchSim sim, int id, IntentAction act)
        {
            var s = sim.State;
            if (s.Phase != SimMatchPhase.InPlay || s.Owner == id) return false;
            ref readonly var p = ref s.Players[id];
            if (!p.IsFree) return false;
            var ac = sim.Config.Actions;
            float t = act == IntentAction.SlideTackle ? ac.SlideWindup : ac.TackleWindup;
            float range = act == IntentAction.SlideTackle ? ac.SlideTackleRange * 2f : ac.StandingTackleRange;
            SimVec3 ball = (s.Ball.Position + s.Ball.Velocity.Flat * t).Flat;
            SimVec3 me = (p.Position + p.Velocity.Flat * t).Flat;
            return SimMath.FlatDistance(me, ball) <= range * 0.9f;
        }

        /// <summary>
        /// Where <paramref name="id"/> should run to meet a moving ball: samples the sim's own ball prediction
        /// (drag, bounce, spin) every <paramref name="step"/> s up to <paramref name="horizon"/> and returns the first
        /// point (at a playable height) the player can reach in time; otherwise the last sampled grounded point.
        /// </summary>
        public static SimVec3 InterceptPoint(MatchSim sim, int id, out float time, float horizon = 2.4f, float step = 0.15f)
        {
            var s = sim.State;
            ref readonly var p = ref s.Players[id];
            float speed = PlayerKinematics.SprintSpeed(sim.AttributesOf(id), sim.Config.Kinematics, p.Stamina);
            if (speed < 0.5f) speed = 0.5f;
            SimVec3 fallback = s.Ball.Position.Flat;
            time = 0f;
            for (float t = step; t <= horizon + 1e-4f; t += step)
            {
                var b = sim.PredictBall(t);
                if (b.Position.Y > 1.8f) continue;
                SimVec3 at = b.Position.Flat;
                fallback = at;
                float reach = SimMath.Max(0f, SimMath.FlatDistance(p.Position, at) - 0.6f) / speed + 0.15f;
                if (reach <= t)
                {
                    time = t;
                    return at;
                }
            }
            time = horizon;
            return fallback;
        }
    }
}
