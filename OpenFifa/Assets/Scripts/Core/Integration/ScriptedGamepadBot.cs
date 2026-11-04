namespace OpenFifa.Core
{
    /// <summary>
    /// A deterministic stand-in for a human holding a gamepad. It produces raw <see cref="GamepadFrame"/>s (sticks and
    /// button presses/releases) and runs them through a real <see cref="GamepadControlResolver"/>, so tests, soak runs
    /// and attract mode exercise the same path as a player. Skill is deliberately average and fixed, so match results
    /// across difficulty levels measure the AI, not the bot.
    /// </summary>
    public sealed class ScriptedGamepadBot : IHumanInputSource
    {
        public readonly GamepadControlResolver Resolver;
        /// <summary>Shoot when this close to the goal line (m along the attack direction from the goal).</summary>
        public float ShootRange = 16f;
        /// <summary>Seconds of carrying before the bot looks for a pass.</summary>
        public float CarrySeconds = 0.4f;
        /// <summary>When false the bot holds a goal-side position on defence instead of hunting the ball (diagnostics).</summary>
        public bool PressBall = true;

        private GamepadButton _holding;
        private int _holdTicks;
        private int _cooldown;
        private int _ownedTicks;
        private int _lastOwnerId = -1;
        private int _switchCooldown;

        public ScriptedGamepadBot(ControlSettings settings = null)
        {
            Resolver = new GamepadControlResolver(ControlPreset.Classic(), settings);
        }

        public ControlIntent Read(SinglePlayerMatchSession session, HumanSlot slot, in ControlContext ctx)
        {
            var frame = Decide(session, slot, ctx);
            return Resolver.Tick(frame, ctx);
        }

        private GamepadFrame Decide(SinglePlayerMatchSession session, HumanSlot slot, in ControlContext ctx)
        {
            var sim = session.Sim;
            var s = sim.State;
            int id = slot.ControlledId;
            var frame = new GamepadFrame();
            if (_cooldown > 0) _cooldown--;
            if (_switchCooldown > 0) _switchCooldown--;
            if (id < 0) return frame;

            // Finish a press in progress: hold the button for the chosen number of ticks, then release.
            if (_holding != GamepadButton.None)
            {
                if (_holdTicks-- > 0) frame.Buttons |= _holding;
                else _holding = GamepadButton.None;
            }

            ref readonly var me = ref s.Players[id];
            SimTeam team = slot.Team;
            var pitch = sim.Config.Pitch;
            int sign = sim.AttackSign(team);
            var goal = new SimVec3(0f, 0f, sign * pitch.HalfLength);
            float yaw = session.CameraYaw;

            if (s.Owner == id) _ownedTicks = _lastOwnerId == id ? _ownedTicks + 1 : 0;
            _lastOwnerId = s.Owner;

            if (ctx.InPossession && s.Owner != id && s.Owner < 0)
            {
                // Our ball in flight or loose: go and meet it like a player receiving a pass.
                SimVec3 meet = (s.Ball.Position + s.Ball.Velocity * 0.35f - me.Position).Flat;
                float dm = meet.Magnitude;
                if (dm > 0.3f) frame.LeftStick = HumanControlAdapter.PitchToStick(meet / dm, yaw);
                if (dm > 8f) frame.RightTrigger = 1f;
            }
            else if (ctx.InPossession)
            {
                SimVec3 toGoal = (goal - me.Position).Flat;
                float distGoal = toGoal.Magnitude;
                // Dribble at goal, drifting toward the centre.
                SimVec3 run = (toGoal.Normalized + new SimVec3(-me.Position.X * 0.02f, 0f, 0f)).Normalized;
                frame.LeftStick = HumanControlAdapter.PitchToStick(run, yaw);
                if (s.Owner == id && distGoal > 30f) frame.RightTrigger = 1f;

                if (ctx.CanStrike && _holding == GamepadButton.None && _cooldown == 0)
                {
                    bool restart = s.Phase == SimMatchPhase.AwaitingRestart;
                    if (!restart && distGoal < ShootRange)
                        Press(GamepadButton.East, 18 + (int)(distGoal * 0.6f)); // shot, power by distance
                    else if (restart || _ownedTicks > (int)(CarrySeconds / sim.Dt))
                        Press(GamepadButton.South, 10);                        // pass, aimed up the stick
                }
            }
            else
            {
                // Defend: run at the ball, tackle when close, switch when far.
                SimVec3 ballAhead = (s.Ball.Position + s.Ball.Velocity * 0.25f).Flat;
                if (!PressBall)
                {
                    // Sit between the ball and our goal, 8 m off it.
                    SimVec3 ownGoal = new SimVec3(0f, 0f, -sign * pitch.HalfLength);
                    SimVec3 back = (ownGoal - ballAhead).Flat;
                    float bl = back.Magnitude;
                    if (bl > 1e-3f) ballAhead = ballAhead + back / bl * SimMath.Min(8f, bl * 0.5f);
                }
                SimVec3 to = (ballAhead - me.Position).Flat;
                float d = to.Magnitude;
                if (d > 0.3f) frame.LeftStick = HumanControlAdapter.PitchToStick(to / d, yaw);
                if (d > 6f) frame.RightTrigger = 1f;
                bool oppOwns = s.Owner >= 0 && MatchSim.TeamOf(s.Owner) != team;
                if (oppOwns && d < 1.8f && _cooldown == 0 && _holding == GamepadButton.None)
                    Press(GamepadButton.East, 2); // standing tackle
                else if (d > 15f && _switchCooldown == 0 && _holding == GamepadButton.None)
                {
                    Press(GamepadButton.LeftShoulder, 2);
                    _switchCooldown = 90;
                }
            }

            if (_holding != GamepadButton.None) frame.Buttons |= _holding;
            return frame;
        }

        private void Press(GamepadButton b, int ticks)
        {
            _holding = b;
            _holdTicks = ticks;
            _cooldown = ticks + 30;
        }
    }
}
