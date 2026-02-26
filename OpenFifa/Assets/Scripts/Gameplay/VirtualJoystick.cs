using UnityEngine;
using UnityEngine.InputSystem;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Standard input provider for FIFA-style controls.
    /// Reads movement and actions from Unity Input System.
    /// Supports both Keyboard/Mouse and Gamepad control schemes.
    /// </summary>
    public class InputProvider : MonoBehaviour
    {
        [SerializeField] private PlayerInput _playerInput;

        private InputFilterLogic _filter;
        private Vector2 _moveInput;
        private bool _sprintInput;

        /// <summary>Current movement vector (-1 to 1 on each axis), with dead zone applied.</summary>
        public Vector2 Movement
        {
            get
            {
                if (_filter == null) return _moveInput;
                _filter.ApplyDeadZone(_moveInput.x, _moveInput.y, out float x, out float y);
                return new Vector2(x, y);
            }
        }

        /// <summary>Whether any movement input is active.</summary>
        public bool IsActive => _moveInput.sqrMagnitude > 0.01f;

        /// <summary>Whether sprint is held.</summary>
        public bool IsSprinting => _sprintInput;

        /// <summary>Current control scheme name.</summary>
        public string ActiveControlScheme =>
            _playerInput != null ? _playerInput.currentControlScheme : "Keyboard&Mouse";

        private void Awake()
        {
            _filter = new InputFilterLogic();

            if (_playerInput == null)
                _playerInput = GetComponent<PlayerInput>();
        }

        /// <summary>Input System callback: Movement (WASD / Left Stick).</summary>
        public void OnMove(InputAction.CallbackContext context)
        {
            _moveInput = context.ReadValue<Vector2>();
        }

        /// <summary>Input System callback: Sprint (Left Shift / RT).</summary>
        public void OnSprint(InputAction.CallbackContext context)
        {
            _sprintInput = context.ReadValueAsButton();
        }

        /// <summary>
        /// Set movement input programmatically (for tests).
        /// </summary>
        public void SetMoveInput(Vector2 input)
        {
            _moveInput = input;
        }

        /// <summary>
        /// Set sprint input programmatically (for tests).
        /// </summary>
        public void SetSprintInput(bool sprinting)
        {
            _sprintInput = sprinting;
        }
    }
}
