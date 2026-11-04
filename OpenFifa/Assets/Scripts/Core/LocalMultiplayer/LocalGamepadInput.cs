using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// One local player's gamepad as a session input: raw frames go through <see cref="TeammateCallResolver"/> (calls to
    /// AI teammates, written into the team's <see cref="TeamAIWorld.Directives"/>) and then the normal
    /// <see cref="GamepadControlResolver"/>. The Unity layer supplies frames with <see cref="FrameSource"/> (one physical
    /// pad per seat); tests and bots set <see cref="NextFrame"/>.
    /// </summary>
    public sealed class LocalGamepadInput : IHumanInputSource
    {
        public readonly GamepadControlResolver Resolver;
        public readonly TeammateCallResolver Calls;
        /// <summary>Reads this seat's pad. Null = use <see cref="NextFrame"/>.</summary>
        public Func<GamepadFrame> FrameSource;
        public GamepadFrame NextFrame;
        /// <summary>Last control intent (HUD power bar).</summary>
        public ControlIntent Last { get; private set; }

        public LocalGamepadInput(ControlSettings settings = null, ControlPreset preset = null)
        {
            settings = settings ?? new ControlSettings();
            preset = preset ?? ControlPreset.Classic();
            Resolver = new GamepadControlResolver(preset, settings);
            Calls = new TeammateCallResolver(preset, settings);
        }

        public ControlIntent Read(SinglePlayerMatchSession session, HumanSlot slot, in ControlContext ctx)
        {
            var frame = FrameSource != null ? FrameSource() : NextFrame;
            var s = session.Sim.State;
            int id = slot.ControlledId;
            ref var calls = ref session.AI.World.Directives[SimTeamAIAdapter.TeamIndex(slot.Team)];
            TeammateCallResolver.ResolveArrival(id, s.Owner, ctx.InPossession, ref calls);

            bool ourBall = s.Owner >= 0 && MatchSim.TeamOf(s.Owner) == slot.Team;
            bool mateOnBall = ourBall && s.Owner != id;
            // Directed runs are aimed from the ball carrier (or from us when nobody has it).
            SimVec3 from = ourBall ? s.Players[s.Owner].Position : (id >= 0 ? s.Players[id].Position : s.Ball.Position);
            var filtered = id >= 0
                ? Calls.Filter(frame, ctx, id, mateOnBall, from, session.CameraYaw, Resolver.IsCharging, ref calls)
                : frame;
            Last = Resolver.Tick(filtered, ctx);
            return Last;
        }
    }
}
