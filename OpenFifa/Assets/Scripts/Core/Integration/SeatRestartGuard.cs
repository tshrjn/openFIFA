namespace OpenFifa.Core
{
    /// <summary>
    /// While play is stopped (stoppage or awaiting a restart), walks a user seat's player who is not the restart taker
    /// and has drifted past a pitch line back on. The team AI repositions its own players for restarts but never
    /// touches user seats, so a seat that chased the ball out would otherwise stand in the run-off (up to the sim's
    /// 4 m clamp) until play resumes. <see cref="SinglePlayerMatchSession"/> applies it to every seat's intent, single
    /// player and local multiplayer alike. In play the intent is untouched.
    /// </summary>
    public static class SeatRestartGuard
    {
        /// <summary>How far inside the lines the player is walked back to (m).</summary>
        public const float Inset = 1.5f;
        /// <summary>Tolerance past a line before the guard steps in (m).</summary>
        public const float Slack = 0.25f;

        /// <summary>Overrides <paramref name="ci"/>'s movement when the seat's player must walk back on. Returns true
        /// when it did.</summary>
        public static bool Apply(MatchSim sim, int controlledId, float cameraYaw, ref ControlIntent ci)
        {
            var s = sim.State;
            int id = controlledId;
            if (id < 0 || s.Phase == SimMatchPhase.InPlay || s.Phase == SimMatchPhase.FullTime) return false;
            if (s.Phase == SimMatchPhase.AwaitingRestart && s.RestartTakerId == id) return false;

            var pitch = sim.Config.Pitch;
            var p = s.Players[id].Position;
            bool outside = SimMath.Abs(p.X) > pitch.HalfWidth + Slack || SimMath.Abs(p.Z) > pitch.HalfLength + Slack;
            if (!outside) return false;

            var target = new SimVec3(
                SimMath.Clamp(p.X, -pitch.HalfWidth + Inset, pitch.HalfWidth - Inset), 0f,
                SimMath.Clamp(p.Z, -pitch.HalfLength + Inset, pitch.HalfLength - Inset));
            var to = (target - p).Flat;
            float d = to.Magnitude;
            if (d < 1e-4f) return false;
            ci.Move = PitchToStick(to / d, cameraYaw);
            ci.Sprint = false;
            ci.Held &= ~ControlHeld.Sprint;
            return true;
        }

        /// <summary>Inverse of the stick-to-pitch camera mapping (unit pitch direction → stick, +Y up the screen).</summary>
        public static ControlVec2 PitchToStick(SimVec3 dir, float cameraYaw)
        {
            float sn = (float)System.Math.Sin(cameraYaw);
            float cs = (float)System.Math.Cos(cameraYaw);
            return new ControlVec2(dir.X * cs - dir.Z * sn, dir.X * sn + dir.Z * cs);
        }
    }
}
