using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Manages local multiplayer via controller-based input assignment.
    /// Player 1 = keyboard/mouse (default), Player 2 = gamepad.
    /// Both players can also use separate gamepads or split keyboard.
    /// Each player gets independent movement and action input.
    /// </summary>
    public class LocalMultiplayerManager : MonoBehaviour
    {
        [SerializeField] private PlayerInput player1Input;
        [SerializeField] private PlayerInput player2Input;

        private ControlSchemeAssigner _schemeAssigner;
        private DeviceInputRouter _deviceRouter;
        private LocalMultiplayerConfig _config;
        private LobbyLogic _lobbyLogic;
        private InputConflictDetector _conflictDetector;
        private SplitControlConfig _splitControlConfig;
        private SplitKeyboardConfig _splitKeyboardConfig;

        // Active player references per human player
        [SerializeField] private GameObject _player1ActivePlayer;
        [SerializeField] private GameObject _player2ActivePlayer;

        // Team player lists for player switching
        private readonly List<GameObject> _team1Players = new List<GameObject>();
        private readonly List<GameObject> _team2Players = new List<GameObject>();

        /// <summary>Current movement input for Player 1.</summary>
        public Vector2 Player1Input { get; private set; }

        /// <summary>Current movement input for Player 2.</summary>
        public Vector2 Player2Input { get; private set; }

        /// <summary>Current action state for Player 1.</summary>
        public ActionButtonLogic Player1Actions { get; private set; }

        /// <summary>Current action state for Player 2.</summary>
        public ActionButtonLogic Player2Actions { get; private set; }

        /// <summary>Control scheme assigner for test access.</summary>
        public ControlSchemeAssigner SchemeAssigner => _schemeAssigner;

        /// <summary>Device router for test access.</summary>
        public DeviceInputRouter DeviceRouter => _deviceRouter;

        /// <summary>Lobby logic for test access.</summary>
        public LobbyLogic Lobby => _lobbyLogic;

        /// <summary>Conflict detector for test access.</summary>
        public InputConflictDetector ConflictDetector => _conflictDetector;

        /// <summary>Split control config for test access.</summary>
        public SplitControlConfig SplitConfig => _splitControlConfig;

        /// <summary>Player 1 active player.</summary>
        public GameObject Player1ActivePlayer => _player1ActivePlayer;

        /// <summary>Player 2 active player.</summary>
        public GameObject Player2ActivePlayer => _player2ActivePlayer;

        /// <summary>Fired when a human player joins.</summary>
        public event Action<int> OnPlayerJoined;

        /// <summary>Fired when a human player leaves.</summary>
        public event Action<int> OnPlayerLeft;

        /// <summary>Fired when all players are ready and match should start.</summary>
        public event Action OnMatchReady;

        private void Awake()
        {
            _config = new LocalMultiplayerConfig();
            _schemeAssigner = new ControlSchemeAssigner();
            _deviceRouter = new DeviceInputRouter();
            _lobbyLogic = new LobbyLogic();
            _conflictDetector = new InputConflictDetector(_deviceRouter, _schemeAssigner);
            _splitControlConfig = SplitControlConfig.CreateKeyboardGamepadSplit();
            _splitKeyboardConfig = new SplitKeyboardConfig();
            Player1Actions = new ActionButtonLogic();
            Player2Actions = new ActionButtonLogic();

            // Default assignment: keyboard = player 0, first gamepad = player 1
            _deviceRouter.AssignDevice(0, 0); // Keyboard device
            if (Gamepad.all.Count > 0)
            {
                _deviceRouter.AssignDevice(Gamepad.all[0].deviceId, 1);
            }

            // Wire lobby events
            _lobbyLogic.OnMatchStart += HandleLobbyMatchStart;

            // Auto-join P1 slot (keyboard is always available)
            _lobbyLogic.JoinSlot(0);
        }

        private void OnEnable()
        {
            InputSystem.onDeviceChange += HandleDeviceChange;
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= HandleDeviceChange;
        }

        private void OnDestroy()
        {
            _lobbyLogic.OnMatchStart -= HandleLobbyMatchStart;
        }

        private void Update()
        {
            ReadPlayerInputs();

            // Tick lobby countdown if active
            if (_lobbyLogic.State == LobbyState.CountingDown)
            {
                _lobbyLogic.TickCountdown(Time.deltaTime);
            }
        }

        /// <summary>
        /// Configure the control mode for this multiplayer session.
        /// </summary>
        public void SetControlMode(SplitControlMode mode)
        {
            switch (mode)
            {
                case SplitControlMode.KeyboardAndGamepad:
                    _splitControlConfig = SplitControlConfig.CreateKeyboardGamepadSplit();
                    _schemeAssigner.SetScheme(0, ControlScheme.KeyboardMouse);
                    _schemeAssigner.SetScheme(1, ControlScheme.Gamepad);
                    break;
                case SplitControlMode.SplitKeyboard:
                    _splitControlConfig = SplitControlConfig.CreateSplitKeyboard();
                    _schemeAssigner.SetScheme(0, ControlScheme.KeyboardMouse);
                    _schemeAssigner.SetScheme(1, ControlScheme.KeyboardMouse);
                    break;
                case SplitControlMode.DualGamepad:
                    _splitControlConfig = SplitControlConfig.CreateDualGamepad();
                    _schemeAssigner.SetScheme(0, ControlScheme.Gamepad);
                    _schemeAssigner.SetScheme(1, ControlScheme.Gamepad);
                    break;
            }
        }

        /// <summary>
        /// Register a team's player GameObjects for player switching.
        /// </summary>
        public void RegisterTeamPlayers(int teamIndex, List<GameObject> players)
        {
            if (teamIndex == 0)
            {
                _team1Players.Clear();
                _team1Players.AddRange(players);
            }
            else
            {
                _team2Players.Clear();
                _team2Players.AddRange(players);
            }
        }

        /// <summary>
        /// Set the active player for a human-controlled slot.
        /// </summary>
        public void SetActivePlayer(int playerIndex, GameObject activePlayer)
        {
            if (playerIndex == 0)
                _player1ActivePlayer = activePlayer;
            else if (playerIndex == 1)
                _player2ActivePlayer = activePlayer;
        }

        /// <summary>
        /// Handle player joining (called by PlayerInputManager or manually).
        /// </summary>
        public void HandlePlayerJoin(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= LobbyLogic.MaxPlayers) return;

            _lobbyLogic.JoinSlot(playerIndex);
            OnPlayerJoined?.Invoke(playerIndex);
        }

        /// <summary>
        /// Handle player leaving.
        /// </summary>
        public void HandlePlayerLeave(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= LobbyLogic.MaxPlayers) return;

            _lobbyLogic.LeaveSlot(playerIndex);
            OnPlayerLeft?.Invoke(playerIndex);
        }

        /// <summary>
        /// Toggle ready state for a player.
        /// </summary>
        public void TogglePlayerReady(int playerIndex)
        {
            _lobbyLogic.ToggleReady(playerIndex);
        }

        /// <summary>
        /// Switch the active player to the nearest teammate for a human player.
        /// </summary>
        public void SwitchActivePlayer(int humanPlayerIndex, Transform ballTransform)
        {
            var teamPlayers = humanPlayerIndex == 0 ? _team1Players : _team2Players;
            var currentActive = humanPlayerIndex == 0 ? _player1ActivePlayer : _player2ActivePlayer;

            if (currentActive == null || ballTransform == null || teamPlayers.Count == 0) return;

            float nearestDist = float.MaxValue;
            GameObject nearest = null;

            foreach (var player in teamPlayers)
            {
                if (player == currentActive) continue;
                if (player == null) continue;

                float dist = Vector3.Distance(player.transform.position, ballTransform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = player;
                }
            }

            if (nearest != null)
            {
                // Deactivate old player's controller, enable AI
                DeactivateHumanControl(currentActive);

                // Activate new player's controller, disable AI
                ActivateHumanControl(nearest);

                SetActivePlayer(humanPlayerIndex, nearest);
            }
        }

        private void ReadPlayerInputs()
        {
            // Determine input mode
            if (_splitControlConfig.Mode == SplitControlMode.SplitKeyboard)
            {
                ReadSplitKeyboardInputs();
            }
            else
            {
                ReadStandardInputs();
            }

            // Apply inputs to active players
            ApplyInputToActivePlayer(0, Player1Input, Player1Actions);
            ApplyInputToActivePlayer(1, Player2Input, Player2Actions);
        }

        private void ReadStandardInputs()
        {
            // Player 1: read from assigned PlayerInput component or keyboard directly
            if (player1Input != null)
            {
                var moveAction = player1Input.actions.FindAction("Move");
                if (moveAction != null)
                    Player1Input = moveAction.ReadValue<Vector2>();
            }
            else if (_schemeAssigner.GetScheme(0) == ControlScheme.KeyboardMouse)
            {
                // Direct keyboard reading for P1
                Player1Input = ReadKeyboardMovement();
                ReadKeyboardActions(Player1Actions);
            }

            // Player 2: read from assigned PlayerInput component or gamepad directly
            if (player2Input != null)
            {
                var moveAction = player2Input.actions.FindAction("Move");
                if (moveAction != null)
                    Player2Input = moveAction.ReadValue<Vector2>();
            }
            else if (_schemeAssigner.GetScheme(1) == ControlScheme.Gamepad)
            {
                // Direct gamepad reading for P2
                Player2Input = ReadGamepadMovement();
                ReadGamepadActions(Player2Actions);
            }
        }

        private void ReadSplitKeyboardInputs()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // P1: WASD movement
            float p1H = 0f, p1V = 0f;
            if (keyboard.wKey.isPressed) p1V += 1f;
            if (keyboard.sKey.isPressed) p1V -= 1f;
            if (keyboard.aKey.isPressed) p1H -= 1f;
            if (keyboard.dKey.isPressed) p1H += 1f;
            Player1Input = new Vector2(p1H, p1V);

            // P1 actions
            if (keyboard.spaceKey.wasPressedThisFrame) Player1Actions.PressPass();
            if (keyboard.fKey.wasPressedThisFrame) Player1Actions.PressShoot();
            if (keyboard.gKey.wasPressedThisFrame) Player1Actions.PressTackle();
            if (keyboard.qKey.wasPressedThisFrame) Player1Actions.PressSwitch();
            if (keyboard.rKey.wasPressedThisFrame) Player1Actions.PressThroughBall();
            if (keyboard.eKey.wasPressedThisFrame) Player1Actions.PressLobPass();
            Player1Actions.SetSprint(keyboard.leftShiftKey.isPressed);

            // P2: Arrow key movement
            float p2H = 0f, p2V = 0f;
            if (keyboard.upArrowKey.isPressed) p2V += 1f;
            if (keyboard.downArrowKey.isPressed) p2V -= 1f;
            if (keyboard.leftArrowKey.isPressed) p2H -= 1f;
            if (keyboard.rightArrowKey.isPressed) p2H += 1f;
            Player2Input = new Vector2(p2H, p2V);

            // P2 actions
            if (keyboard.enterKey.wasPressedThisFrame) Player2Actions.PressPass();
            if (keyboard.rightShiftKey.wasPressedThisFrame) Player2Actions.PressShoot();
            if (keyboard.periodKey.wasPressedThisFrame) Player2Actions.PressTackle();
            if (keyboard.commaKey.wasPressedThisFrame) Player2Actions.PressSwitch();
            if (keyboard.slashKey.wasPressedThisFrame) Player2Actions.PressThroughBall();
            if (keyboard.semicolonKey.wasPressedThisFrame) Player2Actions.PressLobPass();
            Player2Actions.SetSprint(keyboard.rightCtrlKey.isPressed);
        }

        private Vector2 ReadKeyboardMovement()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;

            float h = 0f, v = 0f;
            if (keyboard.wKey.isPressed) v += 1f;
            if (keyboard.sKey.isPressed) v -= 1f;
            if (keyboard.aKey.isPressed) h -= 1f;
            if (keyboard.dKey.isPressed) h += 1f;
            return new Vector2(h, v);
        }

        private void ReadKeyboardActions(ActionButtonLogic actions)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame) actions.PressPass();
            if (keyboard.fKey.wasPressedThisFrame) actions.PressShoot();
            if (keyboard.eKey.wasPressedThisFrame) actions.PressTackle();
            if (keyboard.qKey.wasPressedThisFrame) actions.PressSwitch();
            actions.SetSprint(keyboard.leftShiftKey.isPressed);
        }

        private Vector2 ReadGamepadMovement()
        {
            if (Gamepad.all.Count == 0) return Vector2.zero;

            // Use the gamepad assigned to P2
            int gamepadIndex = 0;
            if (_schemeAssigner.GetScheme(0) == ControlScheme.Gamepad)
            {
                // If P1 is also on gamepad, P2 uses the second gamepad
                gamepadIndex = Gamepad.all.Count > 1 ? 1 : 0;
            }

            if (gamepadIndex >= Gamepad.all.Count) return Vector2.zero;
            return Gamepad.all[gamepadIndex].leftStick.ReadValue();
        }

        private void ReadGamepadActions(ActionButtonLogic actions)
        {
            if (Gamepad.all.Count == 0) return;

            int gamepadIndex = 0;
            if (_schemeAssigner.GetScheme(0) == ControlScheme.Gamepad)
            {
                gamepadIndex = Gamepad.all.Count > 1 ? 1 : 0;
            }

            if (gamepadIndex >= Gamepad.all.Count) return;
            var gamepad = Gamepad.all[gamepadIndex];

            if (gamepad.buttonSouth.wasPressedThisFrame) actions.PressPass();
            if (gamepad.buttonEast.wasPressedThisFrame) actions.PressShoot();
            if (gamepad.buttonWest.wasPressedThisFrame) actions.PressTackle();
            if (gamepad.leftShoulder.wasPressedThisFrame) actions.PressSwitch();
            if (gamepad.buttonNorth.wasPressedThisFrame) actions.PressThroughBall();
            if (gamepad.rightShoulder.wasPressedThisFrame) actions.PressLobPass();
            actions.SetSprint(gamepad.rightTrigger.ReadValue() > 0.5f);
        }

        private void ApplyInputToActivePlayer(int playerIndex, Vector2 moveInput, ActionButtonLogic actions)
        {
            var activePlayer = playerIndex == 0 ? _player1ActivePlayer : _player2ActivePlayer;
            if (activePlayer == null) return;

            var controller = activePlayer.GetComponent<PlayerController>();
            if (controller != null && controller.enabled)
            {
                controller.SetMoveInput(moveInput);
                controller.SetSprinting(actions.IsSprintPressed);
            }

            // Handle action presses
            if (actions.IsPassPressed)
            {
                var kicker = activePlayer.GetComponent<PlayerKicker>();
                if (kicker != null) { kicker.Pass(); kicker.OnKickContact(); }
            }

            if (actions.IsShootPressed)
            {
                var kicker = activePlayer.GetComponent<PlayerKicker>();
                if (kicker != null) { kicker.Shoot(); kicker.OnKickContact(); }
            }

            if (actions.IsTacklePressed)
            {
                var tackle = activePlayer.GetComponent<TackleSystem>();
                if (tackle != null) tackle.AttemptTackle();
            }

            if (actions.IsSwitchPressed)
            {
                // Find the ball transform for switching
                var ball = GameObject.FindWithTag("Ball");
                if (ball != null)
                {
                    SwitchActivePlayer(playerIndex, ball.transform);
                }
            }

            actions.ConsumeActions();
        }

        private void DeactivateHumanControl(GameObject player)
        {
            if (player == null) return;

            var controller = player.GetComponent<PlayerController>();
            if (controller != null) controller.enabled = false;

            var aiController = player.GetComponent<OpenFifa.AI.AIController>();
            if (aiController != null)
            {
                aiController.enabled = true;
                // Try to set ball reference
                var ball = GameObject.FindWithTag("Ball");
                if (ball != null)
                    aiController.SetBallReference(ball.transform);
            }
        }

        private void ActivateHumanControl(GameObject player)
        {
            if (player == null) return;

            var controller = player.GetComponent<PlayerController>();
            if (controller != null) controller.enabled = true;

            var aiController = player.GetComponent<OpenFifa.AI.AIController>();
            if (aiController != null)
                aiController.enabled = false;
        }

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad gamepad)
            {
                switch (change)
                {
                    case InputDeviceChange.Added:
                        // Auto-assign first gamepad to P2 if no gamepad assigned yet
                        if (!_deviceRouter.HasDeviceForPlayer(1))
                        {
                            _deviceRouter.AssignDevice(gamepad.deviceId, 1);
                            HandlePlayerJoin(1);
                        }
                        break;

                    case InputDeviceChange.Removed:
                        int owningPlayer = _deviceRouter.GetOwningPlayer(gamepad.deviceId);
                        if (owningPlayer >= 0)
                        {
                            _deviceRouter.UnassignDevice(gamepad.deviceId);
                            HandlePlayerLeave(owningPlayer);
                        }
                        break;
                }
            }
        }

        private void HandleLobbyMatchStart()
        {
            OnMatchReady?.Invoke();
        }
    }
}
