using UnityEngine;
using UnityEngine.InputSystem;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// FIFA-style action input handler.
    /// Reads action inputs (Pass, Shoot, Tackle, ThroughBall, LobPass, Switch, Sprint)
    /// from Unity Input System for both keyboard/mouse and gamepad control schemes.
    /// </summary>
    public class ActionButtons : MonoBehaviour
    {
        private ActionButtonLogic _logic;

        /// <summary>The underlying action logic.</summary>
        public ActionButtonLogic Logic => _logic;

        /// <summary>Which actions are currently pressed (for test verification).</summary>
        public bool IsPassPressed => _logic != null && _logic.IsPassPressed;
        public bool IsShootPressed => _logic != null && _logic.IsShootPressed;
        public bool IsTacklePressed => _logic != null && _logic.IsTacklePressed;
        public bool IsSprintPressed => _logic != null && _logic.IsSprintPressed;
        public bool IsSwitchPressed => _logic != null && _logic.IsSwitchPressed;
        public bool IsThroughBallPressed => _logic != null && _logic.IsThroughBallPressed;
        public bool IsLobPassPressed => _logic != null && _logic.IsLobPassPressed;

        private void Awake()
        {
            _logic = new ActionButtonLogic();
        }

        // Input System callback methods (called by PlayerInput component)

        /// <summary>Input System callback: Pass (Space / A button).</summary>
        public void OnPass(InputAction.CallbackContext context)
        {
            if (context.performed)
                _logic.PressPass();
        }

        /// <summary>Input System callback: Shoot (D / Left Click / B button).</summary>
        public void OnShoot(InputAction.CallbackContext context)
        {
            if (context.performed)
                _logic.PressShoot();
        }

        /// <summary>Input System callback: Tackle (S / X button).</summary>
        public void OnTackle(InputAction.CallbackContext context)
        {
            if (context.performed)
                _logic.PressTackle();
        }

        /// <summary>Input System callback: Through Ball (W / Y button).</summary>
        public void OnThroughBall(InputAction.CallbackContext context)
        {
            if (context.performed)
                _logic.PressThroughBall();
        }

        /// <summary>Input System callback: Lob Pass (E / RB).</summary>
        public void OnLobPass(InputAction.CallbackContext context)
        {
            if (context.performed)
                _logic.PressLobPass();
        }

        /// <summary>Input System callback: Switch Player (Q / LB).</summary>
        public void OnSwitch(InputAction.CallbackContext context)
        {
            if (context.performed)
                _logic.PressSwitch();
        }

        /// <summary>Input System callback: Sprint (Left Shift / RT). Hold behavior.</summary>
        public void OnSprint(InputAction.CallbackContext context)
        {
            _logic.SetSprint(context.ReadValueAsButton());
        }

        /// <summary>Consume actions after processing in PlayerController.</summary>
        public void ConsumeActions()
        {
            _logic.ConsumeActions();
        }
    }
}
