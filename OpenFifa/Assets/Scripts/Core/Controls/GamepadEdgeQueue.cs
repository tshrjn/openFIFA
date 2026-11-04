namespace OpenFifa.Core
{
    /// <summary>
    /// Decouples render-frame gamepad sampling from fixed sim ticks, like <see cref="TouchEventQueue"/> does for touch.
    /// The device layer calls <see cref="PushFrame"/> once per rendered frame with the held state and that frame's
    /// pressed / released edges (Input System wasPressedThisFrame / wasReleasedThisFrame); each sim tick calls
    /// <see cref="Drain"/> and gets a <see cref="GamepadFrame"/> for <see cref="GamepadControlResolver.Tick"/> in which every
    /// press and release since the previous drain is visible exactly once.
    ///
    /// Why: reading isPressed once per tick loses a press that starts and ends between two ticks (at 120 Hz half the
    /// frames run no tick; a 30 Hz frame can hide a quick tap entirely), and a release-and-repress between ticks.
    ///
    /// Rules, per button (trigger presses use the LeftTrigger / RightTrigger bits):
    /// - Up at the last drain and pressed since: reported held now, even if already released (a tap); the release then
    ///   shows on the next drain.
    /// - Held at the last drain and released since: reported up now; if it was pressed again after that release, it is
    ///   reported held on the next drain (two kicks, not one long hold).
    /// - Otherwise the latest held state. Two complete taps between the same two drains count as one.
    /// - Sticks and analog triggers are the latest values; a trigger reported held reads above 0.5, one reported up reads
    ///   at most 0.5 (the resolver's threshold).
    /// Edges are also inferred from held-state changes between frames, for devices that do not report them.
    /// No allocation.
    /// </summary>
    public sealed class GamepadEdgeQueue
    {
        private const GamepadButton TriggerBits = GamepadButton.LeftTrigger | GamepadButton.RightTrigger;

        private GamepadFrame _latest;
        private GamepadButton _held;
        private GamepadButton _sawPress;
        private GamepadButton _sawRelease;
        private GamepadButton _releasedThenPressed;
        private GamepadButton _pending;
        private GamepadButton _reported;

        /// <summary>Buttons the last drain reported held.</summary>
        public GamepadButton Reported => _reported;

        /// <summary>Record one rendered frame.</summary>
        /// <param name="state">Held buttons, sticks and analog triggers sampled this frame.</param>
        /// <param name="pressedThisFrame">Buttons pressed during this frame (may also be released again).</param>
        /// <param name="releasedThisFrame">Buttons released during this frame (may also be pressed again).</param>
        public void PushFrame(in GamepadFrame state, GamepadButton pressedThisFrame, GamepadButton releasedThisFrame)
        {
            GamepadButton held = state.Buttons;
            if (state.LeftTrigger > 0.5f) held |= GamepadButton.LeftTrigger;
            if (state.RightTrigger > 0.5f) held |= GamepadButton.RightTrigger;

            GamepadButton pressed = pressedThisFrame | (held & ~_held);
            GamepadButton released = releasedThisFrame | (_held & ~held);
            // Pressed after a release since the last drain: an earlier frame's release, or both edges in this frame
            // with the button down at the sample.
            _releasedThenPressed |= pressed & (_sawRelease | (released & held));
            _sawPress |= pressed;
            _sawRelease |= released;
            _held = held;
            _latest = state;
        }

        /// <summary>The frame for one sim tick: every edge since the previous drain, exactly once.</summary>
        public GamepadFrame Drain()
        {
            GamepadButton wasUp = ~_reported;
            GamepadButton nowDown = wasUp & (_pending | _sawPress | _held);
            GamepadButton dropped = _reported & (_sawRelease | ~_held);
            GamepadButton stillDown = _reported & ~dropped;
            _pending = dropped & _releasedThenPressed;
            _reported = nowDown | stillDown;
            _sawPress = _sawRelease = _releasedThenPressed = GamepadButton.None;

            var f = _latest;
            f.Buttons = _reported & ~TriggerBits;
            f.LeftTrigger = TriggerValue(_latest.LeftTrigger, (_reported & GamepadButton.LeftTrigger) != 0);
            f.RightTrigger = TriggerValue(_latest.RightTrigger, (_reported & GamepadButton.RightTrigger) != 0);
            return f;
        }

        private static float TriggerValue(float latest, bool down)
        {
            if (down) return latest > 0.5f ? latest : 1f;
            return latest > 0.5f ? 0f : latest;
        }

        /// <summary>
        /// The sim is not advancing (paused, frozen): forget edges collected so far, so a button tapped on a menu never
        /// reaches the match. Held state is kept, so a button still held on resume reads as a press then, as before.
        /// </summary>
        public void DiscardEdges()
        {
            _sawPress = _sawRelease = _releasedThenPressed = _pending = GamepadButton.None;
        }

        /// <summary>Forget everything (device switched or lost).</summary>
        public void Reset()
        {
            _latest = default;
            _held = _sawPress = _sawRelease = _releasedThenPressed = _pending = _reported = GamepadButton.None;
        }
    }
}
