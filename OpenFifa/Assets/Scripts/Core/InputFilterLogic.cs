using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# input filtering logic with dead zone and normalization.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class InputFilterLogic
    {
        private const float DefaultDeadZone = 0.1f;
        private readonly float _deadZone;

        /// <summary>Dead zone threshold (0-1).</summary>
        public float DeadZone => _deadZone;

        public InputFilterLogic() : this(DefaultDeadZone) { }

        public InputFilterLogic(float deadZone)
        {
            _deadZone = deadZone;
        }

        /// <summary>
        /// Apply dead zone. Input below threshold is zeroed.
        /// Input above threshold is remapped to 0-1 range.
        /// </summary>
        public void ApplyDeadZone(float inX, float inY, out float outX, out float outY)
        {
            float magnitude = (float)Math.Sqrt(inX * inX + inY * inY);

            if (magnitude < _deadZone)
            {
                outX = 0f;
                outY = 0f;
                return;
            }

            // Remap from [deadZone, 1] to [0, 1]
            float remapped = (magnitude - _deadZone) / (1f - _deadZone);
            if (remapped > 1f) remapped = 1f;

            float scale = remapped / magnitude;
            outX = inX * scale;
            outY = inY * scale;
        }

        /// <summary>
        /// Normalize input to unit circle. Clamps magnitude to 1.
        /// </summary>
        public void Normalize(float inX, float inY, out float outX, out float outY)
        {
            float magnitude = (float)Math.Sqrt(inX * inX + inY * inY);

            if (magnitude <= 1f)
            {
                outX = inX;
                outY = inY;
                return;
            }

            outX = inX / magnitude;
            outY = inY / magnitude;
        }
    }

    /// <summary>
    /// Control scheme identifier for input routing.
    /// </summary>
    public enum ControlScheme
    {
        KeyboardMouse = 0,
        Gamepad = 1
    }

    /// <summary>
    /// Pure C# FIFA-style input mapping configuration.
    /// Maps physical inputs to game actions for both Keyboard/Mouse and Gamepad control schemes.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class InputMappingLogic
    {
        private readonly Dictionary<string, ActionType> _keyboardMapping;
        private readonly Dictionary<string, ActionType> _gamepadMapping;
        private readonly Dictionary<string, string> _movementMapping;

        public InputMappingLogic()
        {
            // FIFA-style keyboard mapping
            _keyboardMapping = new Dictionary<string, ActionType>
            {
                { "space", ActionType.Pass },           // Space = Pass (A button equivalent)
                { "w", ActionType.ThroughBall },        // W = Through ball (Y button equivalent)
                { "d", ActionType.Shoot },              // D = Shoot (B button equivalent)
                { "s", ActionType.Tackle },             // S = Tackle/Slide (X button equivalent)
                { "leftshift", ActionType.Sprint },     // Left Shift = Sprint (RT equivalent)
                { "q", ActionType.Switch },             // Q = Switch player (LB equivalent)
                { "e", ActionType.LobPass },            // E = Lob pass (RB equivalent)
                { "mouse0", ActionType.Shoot },         // Left Click = Shoot
            };

            // FIFA-style gamepad mapping (Xbox layout)
            _gamepadMapping = new Dictionary<string, ActionType>
            {
                { "buttonsouth", ActionType.Pass },         // A button = Pass
                { "buttonnorth", ActionType.ThroughBall },  // Y button = Through ball
                { "buttoneast", ActionType.Shoot },         // B button = Shoot
                { "buttonwest", ActionType.Tackle },        // X button = Tackle/Slide
                { "righttrigger", ActionType.Sprint },      // RT = Sprint
                { "leftshoulder", ActionType.Switch },      // LB = Switch player
                { "rightshoulder", ActionType.LobPass },    // RB = Lob pass
            };

            // Movement input names for keyboard (WASD already handled by Unity Input System)
            _movementMapping = new Dictionary<string, string>
            {
                { "keyboard", "wasd" },     // WASD for keyboard movement
                { "gamepad", "leftstick" },  // Left stick for gamepad movement
            };
        }

        /// <summary>
        /// Get the action type for a keyboard key name.
        /// </summary>
        public ActionType GetKeyboardAction(string keyName)
        {
            if (_keyboardMapping.TryGetValue(keyName.ToLowerInvariant(), out ActionType action))
                return action;
            return ActionType.None;
        }

        /// <summary>
        /// Get the action type for a gamepad button name.
        /// </summary>
        public ActionType GetGamepadAction(string buttonName)
        {
            if (_gamepadMapping.TryGetValue(buttonName.ToLowerInvariant(), out ActionType action))
                return action;
            return ActionType.None;
        }

        /// <summary>
        /// Get the movement input source for a control scheme.
        /// </summary>
        public string GetMovementSource(ControlScheme scheme)
        {
            string key = scheme == ControlScheme.KeyboardMouse ? "keyboard" : "gamepad";
            return _movementMapping.TryGetValue(key, out string source) ? source : "";
        }

        /// <summary>
        /// Returns the number of keyboard action bindings.
        /// </summary>
        public int KeyboardBindingCount => _keyboardMapping.Count;

        /// <summary>
        /// Returns the number of gamepad action bindings.
        /// </summary>
        public int GamepadBindingCount => _gamepadMapping.Count;
    }
}
