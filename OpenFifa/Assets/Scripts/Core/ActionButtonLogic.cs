using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Action types for player commands.
    /// </summary>
    public enum ActionType
    {
        None = 0,
        Pass = 1,
        Shoot = 2,
        Tackle = 3,
        Sprint = 4,
        Switch = 5
    }

    /// <summary>
    /// Pure C# action button logic.
    /// Tracks which actions are pressed and provides consume pattern for single-press actions.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class ActionButtonLogic
    {
        private bool _isPassPressed;
        private bool _isShootPressed;
        private bool _isTacklePressed;
        private bool _isSprintPressed;

        public bool IsPassPressed => _isPassPressed;
        public bool IsShootPressed => _isShootPressed;
        public bool IsTacklePressed => _isTacklePressed;
        public bool IsSprintPressed => _isSprintPressed;

        public void PressPass() { _isPassPressed = true; }
        public void PressShoot() { _isShootPressed = true; }
        public void PressTackle() { _isTacklePressed = true; }

        /// <summary>
        /// Set sprint state (hold behavior - true while held, false on release).
        /// </summary>
        public void SetSprint(bool pressed)
        {
            _isSprintPressed = pressed;
        }

        /// <summary>
        /// Consume single-press actions (Pass, Shoot, Tackle).
        /// Sprint is preserved since it uses hold behavior.
        /// </summary>
        public void ConsumeActions()
        {
            _isPassPressed = false;
            _isShootPressed = false;
            _isTacklePressed = false;
        }
    }

    /// <summary>
    /// Pure C# keyboard action mapping (macOS).
    /// Maps key names to action types.
    /// </summary>
    public class KeyboardActionMapping
    {
        private readonly Dictionary<string, ActionType> _mapping;

        public KeyboardActionMapping()
        {
            _mapping = new Dictionary<string, ActionType>
            {
                { "z", ActionType.Pass },
                { "x", ActionType.Shoot },
                { "c", ActionType.Tackle },
                { "leftshift", ActionType.Sprint },
                { "tab", ActionType.Switch }
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
}
