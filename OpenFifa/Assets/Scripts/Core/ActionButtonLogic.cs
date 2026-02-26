using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Action types for player commands.
    /// FIFA-style controls: Pass, ThroughBall, Shoot, Tackle, Sprint, Switch, LobPass.
    /// </summary>
    public enum ActionType
    {
        None = 0,
        Pass = 1,
        Shoot = 2,
        Tackle = 3,
        Sprint = 4,
        Switch = 5,
        ThroughBall = 6,
        LobPass = 7
    }

    /// <summary>
    /// Pure C# action button logic.
    /// Tracks which actions are pressed and provides consume pattern for single-press actions.
    /// Supports FIFA-style controls for both keyboard/mouse and gamepad.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class ActionButtonLogic
    {
        private bool _isPassPressed;
        private bool _isShootPressed;
        private bool _isTacklePressed;
        private bool _isSprintPressed;
        private bool _isSwitchPressed;
        private bool _isThroughBallPressed;
        private bool _isLobPassPressed;

        public bool IsPassPressed => _isPassPressed;
        public bool IsShootPressed => _isShootPressed;
        public bool IsTacklePressed => _isTacklePressed;
        public bool IsSprintPressed => _isSprintPressed;
        public bool IsSwitchPressed => _isSwitchPressed;
        public bool IsThroughBallPressed => _isThroughBallPressed;
        public bool IsLobPassPressed => _isLobPassPressed;

        public void PressPass() { _isPassPressed = true; }
        public void PressShoot() { _isShootPressed = true; }
        public void PressTackle() { _isTacklePressed = true; }
        public void PressThroughBall() { _isThroughBallPressed = true; }
        public void PressLobPass() { _isLobPassPressed = true; }
        public void PressSwitch() { _isSwitchPressed = true; }

        /// <summary>
        /// Set sprint state (hold behavior - true while held, false on release).
        /// </summary>
        public void SetSprint(bool pressed)
        {
            _isSprintPressed = pressed;
        }

        /// <summary>
        /// Consume single-press actions (Pass, Shoot, Tackle, ThroughBall, LobPass, Switch).
        /// Sprint is preserved since it uses hold behavior.
        /// </summary>
        public void ConsumeActions()
        {
            _isPassPressed = false;
            _isShootPressed = false;
            _isTacklePressed = false;
            _isSwitchPressed = false;
            _isThroughBallPressed = false;
            _isLobPassPressed = false;
        }
    }

    /// <summary>
    /// Pure C# FIFA-style keyboard action mapping.
    /// Maps key names to action types following standard FIFA controls.
    /// </summary>
    public class KeyboardActionMapping
    {
        private readonly Dictionary<string, ActionType> _mapping;

        public KeyboardActionMapping()
        {
            _mapping = new Dictionary<string, ActionType>
            {
                { "space", ActionType.Pass },           // Space = Pass (FIFA A)
                { "w", ActionType.ThroughBall },        // W = Through ball (FIFA Y)
                { "d", ActionType.Shoot },              // D = Shoot (FIFA B)
                { "s", ActionType.Tackle },             // S = Tackle/Slide (FIFA X)
                { "leftshift", ActionType.Sprint },     // Left Shift = Sprint (FIFA RT)
                { "q", ActionType.Switch },             // Q = Switch player (FIFA LB)
                { "e", ActionType.LobPass },            // E = Lob pass (FIFA RB)
                { "mouse0", ActionType.Shoot },         // Left Click = Shoot
            };
        }

        /// <summary>
        /// Get the action type for a key name.
        /// </summary>
        public ActionType GetAction(string keyName)
        {
            if (_mapping.TryGetValue(keyName.ToLowerInvariant(), out ActionType action))
                return action;
            return ActionType.None;
        }
    }

    /// <summary>
    /// Pure C# FIFA-style gamepad action mapping.
    /// Maps Xbox-like controller buttons to action types following standard FIFA controls.
    /// </summary>
    public class GamepadActionMapping
    {
        private readonly Dictionary<string, ActionType> _mapping;

        public GamepadActionMapping()
        {
            _mapping = new Dictionary<string, ActionType>
            {
                { "buttonsouth", ActionType.Pass },         // A button = Pass
                { "buttonnorth", ActionType.ThroughBall },  // Y button = Through ball
                { "buttoneast", ActionType.Shoot },         // B button = Shoot
                { "buttonwest", ActionType.Tackle },        // X button = Tackle/Slide
                { "righttrigger", ActionType.Sprint },      // RT = Sprint
                { "leftshoulder", ActionType.Switch },      // LB = Switch player
                { "rightshoulder", ActionType.LobPass },    // RB = Lob pass
            };
        }

        /// <summary>
        /// Get the action type for a gamepad button name.
        /// </summary>
        public ActionType GetAction(string buttonName)
        {
            if (_mapping.TryGetValue(buttonName.ToLowerInvariant(), out ActionType action))
                return action;
            return ActionType.None;
        }
    }
}
