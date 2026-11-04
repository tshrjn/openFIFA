namespace OpenFifa.Core
{
    /// <summary>
    /// Wraps a seat's input so that, while play is stopped (stoppage or awaiting a restart), a seat player who is not
    /// the restart taker and has drifted past a pitch line walks back on. The team AI repositions its own players for
    /// restarts but never touches user seats, so a seat that chased the ball out (pad or bot) would otherwise stand in
    /// the run-off area until play resumes. In play, the input passes through untouched.
    /// </summary>
    public sealed class SeatRestartGuard : IHumanInputSource
    {
        /// <summary>How far inside the lines the player is walked back to (m).</summary>
        public const float Inset = 1.5f;
        /// <summary>Tolerance past a line before the guard steps in (m).</summary>
        public const float Slack = 0.25f;

        public readonly IHumanInputSource Inner;

        public SeatRestartGuard(IHumanInputSource inner)
        {
            Inner = inner ?? throw new System.ArgumentNullException(nameof(inner));
        }

        public ControlIntent Read(SinglePlayerMatchSession session, HumanSlot slot, in ControlContext ctx)
        {
            var ci = Inner.Read(session, slot, ctx);
            var s = session.Sim.State;
            int id = slot.ControlledId;
            if (id < 0 || s.Phase == SimMatchPhase.InPlay || s.Phase == SimMatchPhase.FullTime) return ci;
            if (s.Phase == SimMatchPhase.AwaitingRestart && s.RestartTakerId == id) return ci;

            var pitch = session.Sim.Config.Pitch;
            var p = s.Players[id].Position;
            bool outside = SimMath.Abs(p.X) > pitch.HalfWidth + Slack || SimMath.Abs(p.Z) > pitch.HalfLength + Slack;
            if (!outside) return ci;

            var target = new SimVec3(
                SimMath.Clamp(p.X, -pitch.HalfWidth + Inset, pitch.HalfWidth - Inset), 0f,
                SimMath.Clamp(p.Z, -pitch.HalfLength + Inset, pitch.HalfLength - Inset));
            var to = (target - p).Flat;
            float d = to.Magnitude;
            if (d < 1e-4f) return ci;
            ci.Move = PitchToStick(to / d, session.CameraYaw);
            ci.Sprint = false;
            ci.Held &= ~ControlHeld.Sprint;
            return ci;
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
