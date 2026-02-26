using UnityEngine;
using UnityEngine.InputSystem;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Manages local multiplayer via controller-based input assignment.
    /// Player 1 = keyboard/mouse (default), Player 2 = gamepad.
    /// Both players can also use separate gamepads.
    /// Each player gets independent movement and action input.
    /// </summary>
    public class LocalMultiplayerManager : MonoBehaviour
    {
        [SerializeField] private PlayerInput player1Input;
        [SerializeField] private PlayerInput player2Input;

        private ControlSchemeAssigner _schemeAssigner;
        private DeviceInputRouter _deviceRouter;
        private LocalMultiplayerConfig _config;

        /// <summary>Current movement input for Player 1.</summary>
        public Vector2 Player1Input { get; private set; }

        /// <summary>Current movement input for Player 2.</summary>
        public Vector2 Player2Input { get; private set; }

        /// <summary>Control scheme assigner for test access.</summary>
        public ControlSchemeAssigner SchemeAssigner => _schemeAssigner;

        /// <summary>Device router for test access.</summary>
        public DeviceInputRouter DeviceRouter => _deviceRouter;

        private void Awake()
        {
            _config = new LocalMultiplayerConfig();
            _schemeAssigner = new ControlSchemeAssigner();
            _deviceRouter = new DeviceInputRouter();

            // Default assignment: keyboard = player 0, first gamepad = player 1
            _deviceRouter.AssignDevice(0, 0); // Keyboard device
            if (Gamepad.all.Count > 0)
            {
                _deviceRouter.AssignDevice(Gamepad.all[0].deviceId, 1);
            }
        }

        private void Update()
        {
            ReadPlayerInputs();
        }

        private void ReadPlayerInputs()
        {
            // Player 1: read from assigned PlayerInput component
            if (player1Input != null)
            {
                var moveAction = player1Input.actions.FindAction("Move");
                if (moveAction != null)
                    Player1Input = moveAction.ReadValue<Vector2>();
            }

            // Player 2: read from assigned PlayerInput component
            if (player2Input != null)
            {
                var moveAction = player2Input.actions.FindAction("Move");
                if (moveAction != null)
                    Player2Input = moveAction.ReadValue<Vector2>();
            }
        }
    }
}
