using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>One finger sample. Position is normalised (0..1, origin bottom-left).</summary>
    public struct TouchPoint
    {
        public int FingerId;
        public ControlVec2 Position;
        public ControlTouchPhase Phase;

        public TouchPoint(int fingerId, ControlVec2 position, ControlTouchPhase phase)
        {
            FingerId = fingerId;
            Position = position;
            Phase = phase;
        }
    }

    /// <summary>
    /// iPad touch controls: a floating left stick plus four context buttons whose meaning flips between
    /// attack and defence. A button keeps the meaning it had when the finger went down; if possession changes
    /// before release, the press is dropped.
    ///
    /// Attack: Pass (tap pass, swipe up lob / cross), Shoot (tap shot, swipe up chip, swipe left/right finesse,
    /// swipe down low shot, swipe down + driven knuckleball), Through (tap through ball, swipe up lobbed through), Sprint. Pass, Shoot and Through
    /// charge power while held. A kick is driven only when Sprint is pressed (a new touch) while that kick button
    /// is already down and has been for at least <see cref="DrivenSprintMinDelaySeconds"/>. Running with Sprint
    /// held and then pressing a kick button gives a normal kick, and pressing Sprint and the kick button together
    /// does too, so sprinting never turns kicks driven by accident.
    /// Defence: Switch (tap smart switch, swipe directional switch), Tackle (tap tackle, swipe slide),
    /// Press (hold contain, hold after swipe up teammate press, hold after swipe down jockey), Sprint.
    /// </summary>
    public sealed class TouchControlScheme
    {
        private struct Finger
        {
            public int Id;
            public TouchSlot Slot;
            public ControlVec2 Start;
            public ControlVec2 Current;
            public bool Attack;
            /// <summary>Sim time the finger went down.</summary>
            public float BeganAt;
            /// <summary>Kick fingers only: Sprint was pressed during this kick's charge window.</summary>
            public bool Driven;
        }

        /// <summary>
        /// Default for <see cref="DrivenSprintMinDelaySeconds"/>: 0.05 s is three 60 Hz frames, longer than the skew
        /// between two fingers landing "together" but well under a deliberate press-then-tap (about 0.15 s or more).
        /// </summary>
        public const float DefaultDrivenSprintMinDelaySeconds = 0.05f;

        private readonly List<Finger> _fingers = new List<Finger>();
        private readonly ChargeMeter _meter = new ChargeMeter();
        private readonly ControlKickBuffer _buffer = new ControlKickBuffer();
        private int _chargeFinger = -1;
        private int _stickFinger = -1;
        private ControlVec2 _stickOrigin;
        private ControlVec2 _stickCurrent;

        public TouchLayout Layout { get; set; }
        public ControlSettings Settings { get; }

        /// <summary>
        /// How long a kick button must already be down before a new Sprint press makes the kick driven.
        /// Presses closer together than this count as simultaneous and give a normal kick.
        /// </summary>
        public float DrivenSprintMinDelaySeconds { get; set; } = DefaultDrivenSprintMinDelaySeconds;

        public TouchControlScheme(TouchLayout layout = null, ControlSettings settings = null)
        {
            Layout = layout ?? TouchLayout.IPad4x3();
            Settings = settings ?? new ControlSettings();
        }

        /// <summary>Where the floating stick base currently is (valid while <see cref="StickActive"/>).</summary>
        public ControlVec2 StickOrigin => _stickOrigin;
        public bool StickActive => _stickFinger >= 0;

        /// <summary>Button label for the HUD.</summary>
        public static string LabelFor(TouchSlot slot, bool attacking)
        {
            switch (slot)
            {
                case TouchSlot.Primary: return attacking ? "Pass" : "Switch";
                case TouchSlot.Strike: return attacking ? "Shoot" : "Tackle";
                case TouchSlot.Through: return attacking ? "Through" : "Press";
                case TouchSlot.Sprint: return "Sprint";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// Resolves an attacking button release into an action. <paramref name="driven"/> is true only when the
        /// deliberate driven gesture was made (Sprint pressed during the charge), not whenever Sprint is held.
        /// </summary>
        public static ControlAction ResolveAttack(TouchSlot slot, TouchGesture g, bool driven, bool inCrossingZone)
        {
            switch (slot)
            {
                case TouchSlot.Primary:
                    if (g == TouchGesture.SwipeUp) return inCrossingZone ? ControlAction.Cross : ControlAction.Lob;
                    return driven ? ControlAction.DrivenPass : ControlAction.Pass;
                case TouchSlot.Strike:
                    switch (g)
                    {
                        case TouchGesture.SwipeUp: return ControlAction.ChipShot;
                        case TouchGesture.SwipeLeft:
                        case TouchGesture.SwipeRight: return ControlAction.FinesseShot;
                        // Swipe down + the driven gesture (Sprint tapped during the charge): a hard, straight, spinless
                        // strike, i.e. a knuckleball. Plain swipe down stays the low shot.
                        case TouchGesture.SwipeDown: return driven ? ControlAction.KnuckleShot : ControlAction.LowShot;
                        default: return driven ? ControlAction.DrivenShot : ControlAction.Shoot;
                    }
                case TouchSlot.Through:
                    if (g == TouchGesture.SwipeUp) return ControlAction.LobbedThrough;
                    return driven ? ControlAction.DrivenThrough : ControlAction.ThroughBall;
                default:
                    return ControlAction.None;
            }
        }

        /// <summary>Resolves a defensive button release into a one-shot action.</summary>
        public static ControlAction ResolveDefence(TouchSlot slot, TouchGesture g)
        {
            switch (slot)
            {
                case TouchSlot.Primary: return ControlAction.SwitchPlayer;
                case TouchSlot.Strike: return g == TouchGesture.Tap ? ControlAction.Tackle : ControlAction.SlideTackle;
                default: return ControlAction.None;
            }
        }

        public ControlIntent Tick(IReadOnlyList<TouchPoint> touches, in ControlContext ctx)
        {
            var s = Settings;
            var intent = new ControlIntent();
            ControlAction oneShot = ControlAction.None;
            bool attack = ctx.InPossession;

            if (!attack)
            {
                _meter.Cancel();
                _chargeFinger = -1;
                _buffer.Clear();
            }

            if (touches != null)
            {
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    switch (t.Phase)
                    {
                        case ControlTouchPhase.Began:
                            Begin(t, ctx);
                            break;
                        case ControlTouchPhase.Moved:
                        case ControlTouchPhase.Stationary:
                            Move(t);
                            break;
                        case ControlTouchPhase.Ended:
                        case ControlTouchPhase.Canceled:
                            ControlAction a = End(t, ctx, t.Phase == ControlTouchPhase.Canceled, ref intent);
                            if (oneShot == ControlAction.None) oneShot = a;
                            break;
                    }
                }
            }

            // Floating stick.
            if (_stickFinger >= 0)
            {
                ControlVec2 delta = Layout.ToHeightUnits(_stickCurrent - _stickOrigin) * (1f / Layout.StickRadius);
                if (delta.Magnitude > 1f)
                {
                    // Drag the base so the stick stays under the thumb.
                    ControlVec2 excess = delta - delta.Normalized;
                    _stickOrigin = _stickOrigin + new ControlVec2(excess.X * Layout.StickRadius / Layout.Aspect, excess.Y * Layout.StickRadius);
                    delta = delta.Normalized;
                }
                intent.Move = ControlVec2.ApplyRadialDeadZone(delta, Layout.StickDeadZone);
            }

            bool sprint = HasFinger(TouchSlot.Sprint, -1)
                || (s.AutoSprintFromStick && intent.Move.Magnitude >= s.AutoSprintThreshold);
            intent.Sprint = sprint;
            if (sprint) intent.Held |= ControlHeld.Sprint;

            if (attack)
            {
                if (_meter.IsCharging) intent.Charge = _meter.Current(ctx.Time);
                if (oneShot == ControlAction.None
                    && _buffer.TryConsume(ctx, s.BufferWindowSeconds, out var kick, out var power, out var aim))
                {
                    oneShot = kick;
                    intent.Power = power;
                    intent.Aim = aim;
                }
                intent.KickBuffered = _buffer.HasPending;
            }
            else
            {
                for (int i = 0; i < _fingers.Count; i++)
                {
                    var f = _fingers[i];
                    if (f.Slot != TouchSlot.Through || f.Attack) continue;
                    TouchGesture g = Layout.Classify(f.Current - f.Start);
                    if (g == TouchGesture.SwipeUp) intent.Held |= ControlHeld.TeammatePress;
                    else if (g == TouchGesture.SwipeDown) intent.Held |= ControlHeld.Jockey;
                    else intent.Held |= ControlHeld.Contain;
                }
            }

            intent.Action = oneShot != ControlAction.None ? oneShot : GamepadControlResolver.HeldToAction(intent.Held);
            return intent;
        }

        private void Begin(TouchPoint t, in ControlContext ctx)
        {
            RemoveFinger(t.FingerId);
            TouchSlot slot = Layout.HitTest(t.Position);
            if (slot != TouchSlot.None)
            {
                if (slot == TouchSlot.Sprint && ctx.InPossession) MarkDriven(ctx.Time);
                _fingers.Add(new Finger
                {
                    Id = t.FingerId, Slot = slot, Start = t.Position, Current = t.Position,
                    Attack = ctx.InPossession, BeganAt = ctx.Time,
                });
                bool kickSlot = IsKickSlot(slot);
                if (ctx.InPossession && kickSlot && !_meter.IsCharging)
                {
                    float full = slot == TouchSlot.Strike ? Settings.ShotFullChargeSeconds : Settings.PassFullChargeSeconds;
                    _meter.Begin(ctx.Time, full, Settings.MinPower);
                    _chargeFinger = t.FingerId;
                }
                return;
            }
            if (_stickFinger < 0 && Layout.StickZone.Contains(t.Position))
            {
                _stickFinger = t.FingerId;
                _stickOrigin = t.Position;
                _stickCurrent = t.Position;
            }
        }

        private void Move(TouchPoint t)
        {
            if (t.FingerId == _stickFinger)
            {
                _stickCurrent = t.Position;
                return;
            }
            for (int i = 0; i < _fingers.Count; i++)
            {
                if (_fingers[i].Id != t.FingerId) continue;
                var f = _fingers[i];
                f.Current = t.Position;
                _fingers[i] = f;
                return;
            }
        }

        private ControlAction End(TouchPoint t, in ControlContext ctx, bool canceled, ref ControlIntent intent)
        {
            if (t.FingerId == _stickFinger)
            {
                _stickFinger = -1;
                return ControlAction.None;
            }

            int idx = IndexOf(t.FingerId);
            if (idx < 0) return ControlAction.None;
            Finger f = _fingers[idx];
            f.Current = t.Position;
            _fingers.RemoveAt(idx);

            bool wasCharging = t.FingerId == _chargeFinger;
            float power = wasCharging ? _meter.Release(ctx.Time) : Settings.MinPower;
            if (wasCharging) _chargeFinger = -1;

            // Context changed since the press, or the OS cancelled the touch: drop it.
            if (canceled || f.Attack != ctx.InPossession) return ControlAction.None;

            ControlVec2 travel = f.Current - f.Start;
            TouchGesture g = Layout.Classify(travel);

            if (f.Attack)
            {
                ControlAction a = ResolveAttack(f.Slot, g, f.Driven, ctx.InCrossingZone);
                if (a == ControlAction.None) return ControlAction.None;
                ControlVec2 aim = _stickFinger >= 0 ? Layout.ToHeightUnits(_stickCurrent - _stickOrigin).Normalized : ControlVec2.Zero;
                _buffer.Push(a, power, aim, ctx.Time);
                return ControlAction.None; // fires from the buffer on the contact frame
            }

            ControlAction d = ResolveDefence(f.Slot, g);
            if (d == ControlAction.SwitchPlayer && g != TouchGesture.Tap)
            {
                intent.DirectionalSwitch = true;
                intent.Aim = Layout.ToHeightUnits(travel).Normalized;
            }
            else if (d == ControlAction.SlideTackle)
            {
                intent.Aim = Layout.ToHeightUnits(travel).Normalized;
            }
            return d;
        }

        private static bool IsKickSlot(TouchSlot slot)
            => slot == TouchSlot.Primary || slot == TouchSlot.Strike || slot == TouchSlot.Through;

        /// <summary>A new Sprint press marks every attacking kick finger that has been down long enough.</summary>
        private void MarkDriven(float now)
        {
            for (int i = 0; i < _fingers.Count; i++)
            {
                var f = _fingers[i];
                if (!f.Attack || !IsKickSlot(f.Slot)) continue;
                if (now - f.BeganAt + 1e-5f < DrivenSprintMinDelaySeconds) continue;
                f.Driven = true;
                _fingers[i] = f;
            }
        }

        private bool HasFinger(TouchSlot slot, int excludeId)
        {
            for (int i = 0; i < _fingers.Count; i++)
                if (_fingers[i].Slot == slot && _fingers[i].Id != excludeId) return true;
            return false;
        }

        private int IndexOf(int id)
        {
            for (int i = 0; i < _fingers.Count; i++)
                if (_fingers[i].Id == id) return i;
            return -1;
        }

        private void RemoveFinger(int id)
        {
            int idx = IndexOf(id);
            if (idx >= 0) _fingers.RemoveAt(idx);
            if (_stickFinger == id) _stickFinger = -1;
        }
    }
}
