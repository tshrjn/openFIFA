namespace OpenFifa.Core
{
    /// <summary>Read-only queries for the presentation layer (animation prediction). Never change the state.</summary>
    public sealed partial class MatchSim
    {
        /// <summary>
        /// A due dribble touch is only taken once the owner is within ControlRadius x this of the ball
        /// (<see cref="UpdateDribble"/>: the player must catch up with a ball pushed ahead first).
        /// </summary>
        public const float DribbleCatchUpFactor = 1.3f;

        /// <summary>
        /// Predicts the owner's next dribble touch with <see cref="UpdateDribble"/>'s own rule: the first tick at or after
        /// <see cref="SimPlayer.NextTouchTick"/> at which the ball (as it will be when UpdateDribble runs on that tick, i.e.
        /// after the previous tick's ball step) is grounded and within <see cref="DribbleCatchUpFactor"/> x ControlRadius of
        /// the player (extrapolated at his current velocity). Players and the ball are assumed undisturbed (no collisions,
        /// no tackles, no sharp-turn retouch). Returns false when no touch is due within <paramref name="maxTicks"/> or the
        /// ball would get loose first. Read-only and allocation-free.
        /// </summary>
        public bool TryPredictDribbleTouch(int playerId, int maxTicks, out int touchTick, out SimVec3 ballAtTouch)
        {
            touchTick = -1;
            ballAtTouch = SimVec3.Zero;
            if (playerId < 0 || playerId >= MatchSimState.PlayerCount || _s.Owner != playerId || _s.KeeperHolding) return false;
            ref readonly SimPlayer p = ref _s.Players[playerId];
            if (p.SentOff) return false;
            var ac = _cfg.Actions;
            float reach = ac.ControlRadius * DribbleCatchUpFactor;
            float dt = _cfg.Dt, bdt = _cfg.BallDt;
            SimVec3 v = p.Velocity.Flat;
            SimBall b = _s.Ball; // UpdateDribble at tick+1 sees the ball as it is now
            for (int k = 1; k <= maxTicks; k++)
            {
                int tick = _s.Tick + k;
                SimVec3 at = p.Position + v * (k * dt);
                float d = SimMath.FlatDistance(at, b.Position);
                if (d > ac.LooseRadius) return false; // possession is lost before any touch
                if (tick >= p.NextTouchTick && b.Position.Y <= 0.5f && d <= reach)
                {
                    touchTick = tick;
                    ballAtTouch = new SimVec3(b.Position.X, _cfg.Ball.Radius, b.Position.Z);
                    return true;
                }
                for (int sub = 0; sub < _cfg.BallSubsteps; sub++) BallIntegrator.Step(ref b, _cfg.Ball, _cfg.Pitch, bdt);
            }
            return false;
        }
    }
}
