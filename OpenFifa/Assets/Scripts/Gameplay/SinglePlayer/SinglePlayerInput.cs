using System.Collections.Generic;
using OpenFifa.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace OpenFifa.Gameplay.SinglePlayer
{
    /// <summary>
    /// Reads the Input System devices once per sim tick and runs them through the pure control resolvers:
    /// gamepad (FC Classic preset) when one is connected, otherwise touch (iPad), with a keyboard fallback mapped onto
    /// the gamepad frame for desktop testing. Implements <see cref="IHumanInputSource"/> for the session.
    ///
    /// Touch is edge-based, so it is sampled every rendered frame (<see cref="PollFrame"/>, called by the controller
    /// before it advances the session) into a <see cref="TouchEventQueue"/>, and each sim tick drains the queue. That
    /// keeps every press and release exactly once at 120, 60 or 30 Hz frame rates against the 60 Hz sim.
    /// </summary>
    public sealed class SinglePlayerInput : IHumanInputSource
    {
        public readonly ControlSettings Settings;
        public readonly GamepadControlResolver Gamepad;
        public readonly TouchControlScheme Touch;

        private readonly List<TouchPoint> _touches = new List<TouchPoint>(16);
        private readonly List<TouchPoint> _frameTouches = new List<TouchPoint>(10);
        private readonly TouchEventQueue _touchQueue = new TouchEventQueue();

        /// <summary>True when the last read used touch (no gamepad connected).</summary>
        public bool UsingTouch { get; private set; }
        /// <summary>Last resolved intent (for the HUD power bar and touch overlay).</summary>
        public ControlIntent Last { get; private set; }
        public ControlContext LastContext { get; private set; }

        public SinglePlayerInput(float assistLevel)
        {
            Settings = new ControlSettings { AssistLevel = assistLevel };
            Gamepad = new GamepadControlResolver(ControlPreset.Classic(), Settings);
            Touch = new TouchControlScheme(TouchLayout.ForAspect(Screen.height > 0 ? Screen.width / (float)Screen.height : 4f / 3f), Settings);
        }

        /// <summary>True when a gamepad is connected; the touch overlay is hidden then.</summary>
        public static bool GamepadConnected => UnityEngine.InputSystem.Gamepad.current != null;

        /// <summary>True when this frame's input comes from the touchscreen (no pad, mobile player).</summary>
        private static bool TouchPath =>
            UnityEngine.InputSystem.Gamepad.current == null && Touchscreen.current != null && Application.isMobilePlatform;

        /// <summary>
        /// Samples the touchscreen for this rendered frame. Call once per frame, before the session advances.
        /// <paramref name="simAdvancing"/> false (paused, frozen, half/full time) cancels touches in progress and
        /// ignores fingers on the glass until they lift, so menu taps never reach the match.
        /// </summary>
        public void PollFrame(bool simAdvancing)
        {
            if (!TouchPath)
            {
                _touchQueue.Reset();
                return;
            }
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
            ReadTouches(_frameTouches);
            _touchQueue.PushFrame(_frameTouches);
            if (!simAdvancing) _touchQueue.CancelAll();
        }

        public ControlIntent Read(SinglePlayerMatchSession session, HumanSlot slot, in ControlContext ctx)
        {
            LastContext = ctx;
            ControlIntent intent;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad != null)
            {
                UsingTouch = false;
                intent = Gamepad.Tick(ReadGamepad(pad), ctx);
            }
            else if (TouchPath)
            {
                UsingTouch = true;
                if (Screen.height > 0) Touch.Layout = TouchLayout.ForAspect(Screen.width / (float)Screen.height);
                _touchQueue.Drain(_touches); // every edge since the last tick, exactly once
                intent = Touch.Tick(_touches, ctx);
            }
            else
            {
                // Desktop without a pad: keyboard mapped to a virtual gamepad. Touch overlay is still shown for mouse testing.
                UsingTouch = Touchscreen.current != null;
                intent = Gamepad.Tick(ReadKeyboard(Keyboard.current), ctx);
            }
            Last = intent;
            return intent;
        }

        public static GamepadFrame ReadGamepad(UnityEngine.InputSystem.Gamepad pad)
        {
            GamepadButton b = GamepadButton.None;
            if (pad.buttonSouth.isPressed) b |= GamepadButton.South;
            if (pad.buttonEast.isPressed) b |= GamepadButton.East;
            if (pad.buttonWest.isPressed) b |= GamepadButton.West;
            if (pad.buttonNorth.isPressed) b |= GamepadButton.North;
            if (pad.leftShoulder.isPressed) b |= GamepadButton.LeftShoulder;
            if (pad.rightShoulder.isPressed) b |= GamepadButton.RightShoulder;
            if (pad.dpad.up.isPressed) b |= GamepadButton.DpadUp;
            if (pad.dpad.down.isPressed) b |= GamepadButton.DpadDown;
            if (pad.dpad.left.isPressed) b |= GamepadButton.DpadLeft;
            if (pad.dpad.right.isPressed) b |= GamepadButton.DpadRight;
            if (pad.leftStickButton.isPressed) b |= GamepadButton.LeftStickPress;
            if (pad.rightStickButton.isPressed) b |= GamepadButton.RightStickPress;
            if (pad.startButton.isPressed) b |= GamepadButton.Start;
            if (pad.selectButton.isPressed) b |= GamepadButton.Select;
            Vector2 l = pad.leftStick.ReadValue();
            Vector2 r = pad.rightStick.ReadValue();
            return new GamepadFrame(b, new ControlVec2(l.x, l.y), new ControlVec2(r.x, r.y))
            {
                LeftTrigger = pad.leftTrigger.ReadValue(),
                RightTrigger = pad.rightTrigger.ReadValue(),
            };
        }

        /// <summary>WASD move, Shift sprint, Space pass, F shoot, R through, E lob/cross, Q switch, C jockey/shield, Tab tactics.</summary>
        public static GamepadFrame ReadKeyboard(Keyboard kb)
        {
            if (kb == null) return default;
            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            var stick = new ControlVec2(x, y);
            if (stick.SqrMagnitude > 1f) stick = stick.Normalized;
            GamepadButton b = GamepadButton.None;
            if (kb.spaceKey.isPressed) b |= GamepadButton.South;
            if (kb.fKey.isPressed) b |= GamepadButton.East;
            if (kb.eKey.isPressed) b |= GamepadButton.West;
            if (kb.rKey.isPressed) b |= GamepadButton.North;
            if (kb.qKey.isPressed) b |= GamepadButton.LeftShoulder;
            if (kb.zKey.isPressed) b |= GamepadButton.RightShoulder;
            if (kb.escapeKey.isPressed) b |= GamepadButton.Start;
            return new GamepadFrame(b, stick)
            {
                LeftTrigger = kb.cKey.isPressed ? 1f : 0f,
                RightTrigger = kb.leftShiftKey.isPressed ? 1f : 0f,
            };
        }

        /// <summary>
        /// This frame's touches from EnhancedTouch, which reports a touch that began (and even ended) within one
        /// frame as Began this frame and its end on the next, so no edge is lost between frames.
        /// </summary>
        public static void ReadTouches(List<TouchPoint> into)
        {
            into.Clear();
            float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            var active = ETouch.activeTouches;
            for (int i = 0; i < active.Count; i++)
            {
                var t = active[i];
                var phase = t.phase;
                if (phase == UnityEngine.InputSystem.TouchPhase.None) continue;
                Vector2 p = t.screenPosition;
                into.Add(new TouchPoint(t.touchId, new ControlVec2(p.x / w, p.y / h), MapPhase(phase)));
            }
        }

        private static ControlTouchPhase MapPhase(UnityEngine.InputSystem.TouchPhase p)
        {
            switch (p)
            {
                case UnityEngine.InputSystem.TouchPhase.Began: return ControlTouchPhase.Began;
                case UnityEngine.InputSystem.TouchPhase.Moved: return ControlTouchPhase.Moved;
                case UnityEngine.InputSystem.TouchPhase.Ended: return ControlTouchPhase.Ended;
                case UnityEngine.InputSystem.TouchPhase.Canceled: return ControlTouchPhase.Canceled;
                default: return ControlTouchPhase.Stationary;
            }
        }
    }
}
