namespace OpenFifa.Core
{
    /// <summary>What one human's controller asked of the AI teammates this tick (for HUD prompts and tests).</summary>
    public enum TeammateCall
    {
        None = 0,
        /// <summary>Off the ball: "give it here" (pass button).</summary>
        PassToFeet,
        /// <summary>Off the ball: "play me in" (through button).</summary>
        PassInBehind,
        /// <summary>LB tap in attack: the best-placed AI teammate makes a run in behind.</summary>
        Run,
        /// <summary>LB held + right-stick flick in attack: a run toward the flicked direction.</summary>
        DirectedRun,
        /// <summary>RB held in defence: nearest AI teammate presses the carrier.</summary>
        TeammatePress,
    }

    /// <summary>
    /// Per-human layer in front of <see cref="GamepadControlResolver"/> that turns teammate-call inputs into
    /// <see cref="TeamAIUserDirectives"/> for the team AI, and strips the inputs it used from the frame so the kick
    /// resolver doesn't also act on them:
    /// <list type="bullet">
    /// <item>Attack, off the ball while a teammate has it: pass button = call for it to feet, through button = call
    ///   for it in behind. Those presses never start a kick charge (no accidental first-time pass on arrival).</item>
    /// <item>Attack, LB tapped on its own: an AI teammate makes a run in behind. LB held + right-stick flick: a run
    ///   toward the flick (the flick is not a skill move). LB is still the kick modifier when a kick is charging.</item>
    /// <item>Defence, RB held: teammate press (refreshed every tick while held).</item>
    /// </list>
    /// </summary>
    public sealed class TeammateCallResolver
    {
        /// <summary>A pass call lasts this long unless the ball arrives first.</summary>
        public const float PassCallSeconds = 1.6f;
        /// <summary>A requested run lasts this long.</summary>
        public const float RunSeconds = 2.5f;
        /// <summary>LB released within this time, with nothing else pressed, counts as a tap.</summary>
        public const float TapSeconds = 0.3f;
        /// <summary>Length of a directed run (m).</summary>
        public const float DirectedRunLength = 18f;
        /// <summary>Teammate press stays on this long after RB is released (smooths a missed tick).</summary>
        public const float PressLinger = 0.15f;

        public ControlPreset Preset { get; set; }
        public ControlSettings Settings { get; }

        /// <summary>Call issued on the latest tick (None when nothing new).</summary>
        public TeammateCall LastCall { get; private set; }

        private GamepadButton _prev;
        private GamepadButton _swallow;
        private float _lbDownAt = -1f;
        private bool _lbSpoiled;
        private bool _flickArmed = true;
        private bool _swallowStick;

        public TeammateCallResolver(ControlPreset preset = null, ControlSettings settings = null)
        {
            Preset = preset ?? ControlPreset.Classic();
            Settings = settings ?? new ControlSettings();
        }

        /// <summary>
        /// Reads the raw frame, writes calls into <paramref name="calls"/> (this human's team entry of
        /// <see cref="TeamAIWorld.Directives"/>) and returns the frame the kick resolver should see.
        /// </summary>
        /// <param name="ctx">The human's control context (attack = his team has the ball).</param>
        /// <param name="selfId">Player this human controls.</param>
        /// <param name="teammateOnBall">A teammate other than <paramref name="selfId"/> has the ball at his feet.</param>
        /// <param name="selfPos">Controlled player's position (directed run targets are relative to the ball carrier,
        /// pass this as the carrier's position when <paramref name="selfId"/> is on the ball).</param>
        /// <param name="cameraYaw">Camera yaw for turning the flick into a pitch direction.</param>
        /// <param name="kickCharging">The kick resolver is charging a kick (LB is a modifier, not a run call).</param>
        public GamepadFrame Filter(in GamepadFrame frame, in ControlContext ctx, int selfId, bool teammateOnBall,
            SimVec3 selfPos, float cameraYaw, bool kickCharging, ref TeamAIUserDirectives calls)
        {
            LastCall = TeammateCall.None;
            GamepadButton held = frame.Buttons;
            GamepadButton pressed = held & ~_prev;
            _prev = held;
            float t = ctx.Time;
            var outFrame = frame;

            // Buttons swallowed on press stay swallowed until released, so the kick resolver never sees a release.
            _swallow &= held;

            bool lb = (held & GamepadButton.LeftShoulder) != 0;
            bool rb = (held & GamepadButton.RightShoulder) != 0;
            bool flick = DetectFlick(frame.RightStick);

            if (ctx.InPossession)
            {
                if (teammateOnBall && !kickCharging)
                {
                    if ((pressed & Preset.PassButton) != 0)
                    {
                        calls.CallForPass(selfId, false, t + PassCallSeconds);
                        _swallow |= Preset.PassButton;
                        LastCall = TeammateCall.PassToFeet;
                    }
                    else if ((pressed & Preset.ThroughButton) != 0)
                    {
                        calls.CallForPass(selfId, true, t + PassCallSeconds);
                        _swallow |= Preset.ThroughButton;
                        LastCall = TeammateCall.PassInBehind;
                    }
                }

                // LB: tap = run, hold + flick = directed run. Spoiled by any kick or face press during the hold.
                if ((pressed & GamepadButton.LeftShoulder) != 0) { _lbDownAt = t; _lbSpoiled = kickCharging; }
                if (lb && (kickCharging || (pressed & FaceMask) != 0)) _lbSpoiled = true;

                if (lb && flick && !_lbSpoiled && LastCall == TeammateCall.None)
                {
                    var dir = StickToPitch(frame.RightStick.Normalized, cameraYaw);
                    if (dir.SqrMagnitude > 1e-6f)
                    {
                        calls.RequestRun(-1, selfPos + dir.Normalized * DirectedRunLength, true, t + RunSeconds);
                        LastCall = TeammateCall.DirectedRun;
                        _lbSpoiled = true; // the release that follows is not also a tap
                    }
                }
                if (!lb && _lbDownAt >= 0f)
                {
                    if (!_lbSpoiled && t - _lbDownAt <= TapSeconds && LastCall == TeammateCall.None)
                    {
                        calls.RequestRun(-1, SimVec3.Zero, false, t + RunSeconds);
                        LastCall = TeammateCall.Run;
                    }
                    _lbDownAt = -1f;
                }
                // While LB is held without a kick, the right stick belongs to run direction, not skill moves.
                _swallowStick = lb && !kickCharging ? true : _swallowStick && frame.RightStick.Magnitude >= Settings.FlickRest;
            }
            else
            {
                _lbDownAt = -1f;
                _swallowStick = false;
                if (rb)
                {
                    calls.RequestPress(t + PressLinger);
                    LastCall = TeammateCall.TeammatePress;
                }
            }

            outFrame.Buttons = held & ~_swallow;
            if (_swallowStick) outFrame.RightStick = default;
            return outFrame;
        }

        /// <summary>Clears this human's pass call once the ball is his (or the team lost it).</summary>
        public static void ResolveArrival(int selfId, int ownerId, bool teamHasBall, ref TeamAIUserDirectives calls)
        {
            if (calls.PassCallerId != selfId) return;
            if (ownerId == selfId || !teamHasBall) calls.ClearPassCall();
        }

        /// <summary>Stick (+Y up the screen) to a flat pitch direction for a camera with the given yaw (radians, 0 = looking
        /// along +Z). Same convention as the single-player glue's HumanControlAdapter.StickToPitch.</summary>
        public static SimVec3 StickToPitch(ControlVec2 stick, float cameraYaw)
        {
            float s = (float)System.Math.Sin(cameraYaw);
            float c = (float)System.Math.Cos(cameraYaw);
            return new SimVec3(stick.Y * s + stick.X * c, 0f, stick.Y * c - stick.X * s);
        }

        private const GamepadButton FaceMask = GamepadButton.South | GamepadButton.East | GamepadButton.West | GamepadButton.North;

        private bool DetectFlick(ControlVec2 rightStick)
        {
            float m = rightStick.Magnitude;
            if (m < Settings.FlickRest)
            {
                _flickArmed = true;
                return false;
            }
            if (_flickArmed && m >= Settings.FlickThreshold)
            {
                _flickArmed = false;
                return true;
            }
            return false;
        }
    }
}
