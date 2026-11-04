namespace OpenFifa.Core
{
    /// <summary>
    /// One sampled gamepad state. Triggers are analog 0..1; the resolver counts one as held from
    /// <see cref="GamepadControlResolver.TriggerPressThreshold"/> until it drops below
    /// <see cref="GamepadControlResolver.TriggerReleaseThreshold"/> (hysteresis).
    /// </summary>
    public struct GamepadFrame
    {
        public GamepadButton Buttons;
        public ControlVec2 LeftStick;
        public ControlVec2 RightStick;
        public float LeftTrigger;
        public float RightTrigger;

        public GamepadFrame(GamepadButton buttons, ControlVec2 leftStick = default, ControlVec2 rightStick = default)
        {
            Buttons = buttons;
            LeftStick = leftStick;
            RightStick = rightStick;
            LeftTrigger = (buttons & GamepadButton.LeftTrigger) != 0 ? 1f : 0f;
            RightTrigger = (buttons & GamepadButton.RightTrigger) != 0 ? 1f : 0f;
        }
    }

    /// <summary>
    /// Turns gamepad frames into <see cref="ControlIntent"/>s using a <see cref="ControlPreset"/>.
    /// Call <see cref="Tick"/> once per sim tick with the current frame and context. Behaviour:
    /// - Kick buttons charge while held; on release the action is resolved from the shoulder modifiers held at
    ///   any point during the hold (LT counts only when pressed after the kick press, during the charge, because
    ///   LT held beforehand is Shield), then queued in a <see cref="ControlKickBuffer"/> and fired on the first
    ///   tick the player can strike (inside the buffer window).
    /// - Pressing shoot again while a plain shot is still queued (within the double-tap window) makes it a low shot.
    /// - Defence: shoot button = tackle, lob button = slide, pass button held = contain, RB held = teammate
    ///   press, LT held = jockey, LB = smart switch, right-stick flick = directional switch.
    /// - Attack: RT sprint, LT shield, right-stick flick = skill move. D-pad pairs issue tactics.
    /// </summary>
    public sealed class GamepadControlResolver
    {
        private static readonly GamepadButton[] FaceButtons =
        {
            GamepadButton.South, GamepadButton.East, GamepadButton.West, GamepadButton.North,
        };

        private static readonly GamepadButton[] DpadButtons =
        {
            GamepadButton.DpadUp, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadRight,
        };

        /// <summary>An analog trigger counts as pressed above this...</summary>
        public const float TriggerPressThreshold = 0.55f;
        /// <summary>...and stays held until it falls below this, so a trigger resting near one value does not chatter
        /// between Shield/jockey/knuckleball (LT) or sprint (RT) on and off.</summary>
        public const float TriggerReleaseThreshold = 0.35f;

        private readonly ChargeMeter _meter = new ChargeMeter();
        private readonly ControlKickBuffer _buffer = new ControlKickBuffer();

        private GamepadButton _prev;
        private FaceRole _chargeRole;
        private GamepadButton _chargeButton;
        private bool _latchLeft;
        private bool _latchRight;
        private bool _latchTrigger;
        private bool _chargeStartedThisTick;
        private float _lastShootRelease = float.NegativeInfinity;
        private bool _swallowShootRelease;
        private bool _flickArmed = true;
        private GamepadButton _tacticFirst;
        private float _tacticFirstTime;

        public ControlPreset Preset { get; set; }
        public ControlSettings Settings { get; }

        public GamepadControlResolver(ControlPreset preset = null, ControlSettings settings = null)
        {
            Preset = preset ?? ControlPreset.Classic();
            Settings = settings ?? new ControlSettings();
        }

        public bool IsCharging => _meter.IsCharging;

        /// <summary>Trigger hysteresis: a trigger that was held stays held down to the release threshold.</summary>
        public static bool TriggerHeld(float value, bool wasHeld) =>
            value > (wasHeld ? TriggerReleaseThreshold : TriggerPressThreshold);

        public ControlIntent Tick(in GamepadFrame frame, in ControlContext ctx)
        {
            var s = Settings;
            GamepadButton held = frame.Buttons;
            if (TriggerHeld(frame.LeftTrigger, (_prev & GamepadButton.LeftTrigger) != 0)) held |= GamepadButton.LeftTrigger;
            if (TriggerHeld(frame.RightTrigger, (_prev & GamepadButton.RightTrigger) != 0)) held |= GamepadButton.RightTrigger;
            GamepadButton pressed = held & ~_prev;
            GamepadButton released = _prev & ~held;
            _prev = held;

            bool lb = (held & GamepadButton.LeftShoulder) != 0;
            bool rb = (held & GamepadButton.RightShoulder) != 0;
            bool lt = (held & GamepadButton.LeftTrigger) != 0;
            bool rt = (held & GamepadButton.RightTrigger) != 0;

            var intent = new ControlIntent
            {
                Move = ControlVec2.ApplyRadialDeadZone(frame.LeftStick, s.StickDeadZone),
                Sprint = rt,
            };
            if (rt) intent.Held |= ControlHeld.Sprint;

            ControlAction oneShot = ControlAction.None;
            bool flick = DetectFlick(frame.RightStick);
            bool attack = ctx.InPossession;

            if (!attack)
            {
                // Losing the ball cancels any kick in progress.
                _meter.Cancel();
                _buffer.Clear();
                _swallowShootRelease = false;
            }

            _chargeStartedThisTick = false;
            if (attack)
            {
                for (int i = 0; i < FaceButtons.Length; i++)
                {
                    GamepadButton b = FaceButtons[i];
                    if ((pressed & b) == 0) continue;
                    FaceRole role = Preset.RoleOf(b);
                    if (role == FaceRole.Shoot && !_meter.IsCharging
                        && ctx.Time - _lastShootRelease <= s.DoubleTapSeconds
                        && _buffer.TryUpgrade(ControlAction.Shoot, ControlAction.LowShot))
                    {
                        _swallowShootRelease = true;
                        continue;
                    }
                    if (!_meter.IsCharging && role != FaceRole.None)
                    {
                        bool shot = role == FaceRole.Shoot;
                        _meter.Begin(ctx.Time, shot ? s.ShotFullChargeSeconds : s.PassFullChargeSeconds, s.MinPower);
                        _chargeRole = role;
                        _chargeButton = b;
                        _latchLeft = lb;
                        _latchRight = rb;
                        // LT is also Shield, so a trigger already held when the kick starts is the player shielding,
                        // not asking for a knuckleball: only a fresh LT press after the kick press counts (below).
                        _latchTrigger = false;
                        _chargeStartedThisTick = true;
                    }
                }

                if (_meter.IsCharging)
                {
                    _latchLeft |= lb;
                    _latchRight |= rb;
                    if (!_chargeStartedThisTick && (pressed & GamepadButton.LeftTrigger) != 0) _latchTrigger = true;
                    if ((released & _chargeButton) != 0)
                    {
                        float power = _meter.Release(ctx.Time);
                        ControlAction action = Preset.ResolveAttack(_chargeRole, _latchLeft, _latchRight, ctx.InCrossingZone, _latchTrigger);
                        _buffer.Push(action, power, intent.Move.Normalized, ctx.Time);
                        if (_chargeRole == FaceRole.Shoot) _lastShootRelease = ctx.Time;
                    }
                    else
                    {
                        intent.Charge = _meter.Current(ctx.Time);
                    }
                }

                if (_swallowShootRelease && (released & Preset.ShootButton) != 0)
                    _swallowShootRelease = false;

                if (_buffer.TryConsume(ctx, s.BufferWindowSeconds, out var kick, out var kickPower, out var kickAim))
                {
                    oneShot = kick;
                    intent.Power = kickPower;
                    intent.Aim = kickAim;
                }
                intent.KickBuffered = _buffer.HasPending;

                if (lt) intent.Held |= ControlHeld.Shield;

                if (flick && oneShot == ControlAction.None)
                {
                    oneShot = ControlAction.SkillMove;
                    intent.SkillDirection = ControlVec2.ToDirection8(frame.RightStick);
                    intent.Aim = frame.RightStick.Normalized;
                }
            }
            else
            {
                for (int i = 0; i < FaceButtons.Length && oneShot == ControlAction.None; i++)
                {
                    GamepadButton b = FaceButtons[i];
                    if ((pressed & b) == 0) continue;
                    oneShot = ControlPreset.ResolveDefencePress(Preset.RoleOf(b));
                    if (oneShot != ControlAction.None) intent.Aim = intent.Move.Normalized;
                }

                if ((held & Preset.PassButton) != 0) intent.Held |= ControlHeld.Contain;
                if (rb) intent.Held |= ControlHeld.TeammatePress;
                if (lt) intent.Held |= ControlHeld.Jockey;

                if (oneShot == ControlAction.None && (pressed & GamepadButton.LeftShoulder) != 0)
                {
                    oneShot = ControlAction.SwitchPlayer;
                }
                else if (oneShot == ControlAction.None && flick)
                {
                    oneShot = ControlAction.SwitchPlayer;
                    intent.DirectionalSwitch = true;
                    intent.Aim = frame.RightStick.Normalized;
                }
            }

            intent.Tactic = ResolveTactic(pressed, ctx.Time);
            intent.Action = oneShot != ControlAction.None ? oneShot : HeldToAction(intent.Held);
            return intent;
        }

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

        private TacticCommand ResolveTactic(GamepadButton pressed, float time)
        {
            if (_tacticFirst != GamepadButton.None && time - _tacticFirstTime > Settings.TacticSequenceSeconds)
                _tacticFirst = GamepadButton.None;

            for (int i = 0; i < DpadButtons.Length; i++)
            {
                GamepadButton d = DpadButtons[i];
                if ((pressed & d) == 0) continue;
                if (_tacticFirst == GamepadButton.None)
                {
                    _tacticFirst = d;
                    _tacticFirstTime = time;
                    return TacticCommand.None;
                }
                TacticCommand cmd = TacticPair(_tacticFirst, d);
                _tacticFirst = GamepadButton.None;
                return cmd;
            }
            return TacticCommand.None;
        }

        /// <summary>D-pad two-press sequences, following the reference layout's quick tactics and mentality.</summary>
        public static TacticCommand TacticPair(GamepadButton first, GamepadButton second)
        {
            if (first == GamepadButton.DpadUp)
            {
                switch (second)
                {
                    case GamepadButton.DpadUp: return TacticCommand.OffsideTrap;
                    case GamepadButton.DpadLeft: return TacticCommand.TeamPress;
                    case GamepadButton.DpadRight: return TacticCommand.ExtraStriker;
                    case GamepadButton.DpadDown: return TacticCommand.GetInTheBox;
                }
            }
            else if (first == GamepadButton.DpadRight)
            {
                switch (second)
                {
                    case GamepadButton.DpadUp: return TacticCommand.MentalityBalanced;
                    case GamepadButton.DpadLeft: return TacticCommand.MentalityDefensive;
                    case GamepadButton.DpadRight: return TacticCommand.MentalityAttacking;
                }
            }
            else if (first == GamepadButton.DpadDown && second == GamepadButton.DpadDown)
            {
                return TacticCommand.Substitution;
            }
            return TacticCommand.None;
        }

        internal static ControlAction HeldToAction(ControlHeld held)
        {
            if ((held & ControlHeld.Contain) != 0) return ControlAction.Contain;
            if ((held & ControlHeld.TeammatePress) != 0) return ControlAction.TeammatePress;
            if ((held & ControlHeld.Jockey) != 0) return ControlAction.Jockey;
            if ((held & ControlHeld.Shield) != 0) return ControlAction.Shield;
            return ControlAction.None;
        }
    }
}
