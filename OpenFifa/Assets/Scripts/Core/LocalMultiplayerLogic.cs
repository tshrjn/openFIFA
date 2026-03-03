using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Configuration for local multiplayer.
    /// Two human players, each controlling one team, with AI filling remaining slots.
    /// Player 1 uses keyboard/mouse, Player 2 uses gamepad (or both on gamepads).
    /// </summary>
    public class LocalMultiplayerConfig
    {
        public int HumanPlayerCount = 2;
        public int PlayersPerTeam = 5;
        public int Player1TeamIndex = 0;
        public int Player2TeamIndex = 1;

        /// <summary>Total AI players needed (10 total - 2 human).</summary>
        public int AIPlayerCount => (PlayersPerTeam * 2) - HumanPlayerCount;

        /// <summary>Total players in the match.</summary>
        public int TotalPlayerCount => PlayersPerTeam * 2;
    }

    /// <summary>
    /// Assigns control schemes to players for local multiplayer.
    /// Player 1 = keyboard/mouse (or gamepad index 0).
    /// Player 2 = gamepad (index 0 or 1 depending on Player 1's choice).
    /// </summary>
    public class ControlSchemeAssigner
    {
        private readonly Dictionary<int, ControlScheme> _playerSchemes;

        public ControlSchemeAssigner()
        {
            _playerSchemes = new Dictionary<int, ControlScheme>
            {
                { 0, ControlScheme.KeyboardMouse },
                { 1, ControlScheme.Gamepad }
            };
        }

        /// <summary>
        /// Override control scheme for a player.
        /// Allows both players on gamepads if desired.
        /// </summary>
        public void SetScheme(int playerIndex, ControlScheme scheme)
        {
            _playerSchemes[playerIndex] = scheme;
        }

        /// <summary>Returns the control scheme for the given player index.</summary>
        public ControlScheme GetScheme(int playerIndex)
        {
            if (_playerSchemes.TryGetValue(playerIndex, out ControlScheme scheme))
                return scheme;
            return ControlScheme.Gamepad;
        }

        /// <summary>Returns true if the two players have different control schemes (no conflict).</summary>
        public bool AreSchemesSeparate()
        {
            return _playerSchemes.ContainsKey(0) && _playerSchemes.ContainsKey(1)
                && _playerSchemes[0] != _playerSchemes[1];
        }

        /// <summary>Returns the number of registered player schemes.</summary>
        public int PlayerCount => _playerSchemes.Count;
    }

    /// <summary>
    /// Routes controller inputs to the correct player based on device assignment.
    /// Tracks which input device is assigned to which player.
    /// </summary>
    public class DeviceInputRouter
    {
        private readonly Dictionary<int, int> _deviceToPlayer;

        public DeviceInputRouter()
        {
            _deviceToPlayer = new Dictionary<int, int>();
        }

        /// <summary>
        /// Assigns an input device to a player.
        /// deviceId: unique device identifier.
        /// playerIndex: 0 or 1.
        /// </summary>
        public void AssignDevice(int deviceId, int playerIndex)
        {
            _deviceToPlayer[deviceId] = playerIndex;
        }

        /// <summary>
        /// Removes a device assignment.
        /// </summary>
        public void UnassignDevice(int deviceId)
        {
            _deviceToPlayer.Remove(deviceId);
        }

        /// <summary>
        /// Returns the owning player index for a device ID.
        /// Returns -1 if the device is not assigned.
        /// </summary>
        public int GetOwningPlayer(int deviceId)
        {
            if (_deviceToPlayer.TryGetValue(deviceId, out int player))
                return player;
            return -1;
        }

        /// <summary>
        /// Returns true if the given device is assigned to any player.
        /// </summary>
        public bool IsDeviceAssigned(int deviceId)
        {
            return _deviceToPlayer.ContainsKey(deviceId);
        }

        /// <summary>
        /// Returns true if the given player has at least one device assigned.
        /// </summary>
        public bool HasDeviceForPlayer(int playerIndex)
        {
            foreach (var kvp in _deviceToPlayer)
            {
                if (kvp.Value == playerIndex)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the device ID assigned to a specific player, or -1 if none.
        /// If multiple devices are assigned, returns the first found.
        /// </summary>
        public int GetDeviceForPlayer(int playerIndex)
        {
            foreach (var kvp in _deviceToPlayer)
            {
                if (kvp.Value == playerIndex)
                    return kvp.Key;
            }
            return -1;
        }

        /// <summary>
        /// Clears all device assignments.
        /// </summary>
        public void ClearAll()
        {
            _deviceToPlayer.Clear();
        }

        /// <summary>
        /// Returns the number of assigned devices.
        /// </summary>
        public int AssignedDeviceCount => _deviceToPlayer.Count;
    }

    /// <summary>
    /// Represents one player slot in the local multiplayer lobby.
    /// Each slot has an index, a control scheme, a team assignment, and a ready flag.
    /// </summary>
    public class PlayerSlot
    {
        /// <summary>Slot index (0 = P1, 1 = P2).</summary>
        public int SlotIndex { get; }

        /// <summary>Control scheme assigned to this slot.</summary>
        public ControlScheme Scheme { get; set; }

        /// <summary>Team index (0 = TeamA, 1 = TeamB).</summary>
        public int TeamIndex { get; set; }

        /// <summary>Whether this player is ready to start the match.</summary>
        public bool IsReady { get; set; }

        /// <summary>Whether a device is connected and assigned to this slot.</summary>
        public bool IsOccupied { get; set; }

        /// <summary>Display name for the slot (e.g., "Player 1").</summary>
        public string DisplayName { get; set; }

        public PlayerSlot(int slotIndex, ControlScheme scheme, int teamIndex)
        {
            SlotIndex = slotIndex;
            Scheme = scheme;
            TeamIndex = teamIndex;
            IsReady = false;
            IsOccupied = false;
            DisplayName = $"Player {slotIndex + 1}";
        }
    }

    /// <summary>
    /// Lobby state for local multiplayer match setup.
    /// </summary>
    public enum LobbyState
    {
        /// <summary>Waiting for players to join slots.</summary>
        WaitingForPlayers,

        /// <summary>All players are connected but not all ready.</summary>
        AllConnected,

        /// <summary>All players have marked themselves ready.</summary>
        AllReady,

        /// <summary>Countdown to match start in progress.</summary>
        CountingDown,

        /// <summary>Match is starting (transitioning to gameplay).</summary>
        Starting
    }

    /// <summary>
    /// Pure C# lobby logic for local multiplayer.
    /// Manages up to 2 player slots, ready state, and countdown.
    /// No Unity dependency.
    /// </summary>
    public class LobbyLogic
    {
        public const int MaxPlayers = 2;
        public const float DefaultCountdownDuration = 3f;

        private readonly PlayerSlot[] _slots;
        private LobbyState _state;
        private float _countdownRemaining;
        private readonly float _countdownDuration;

        /// <summary>Current lobby state.</summary>
        public LobbyState State => _state;

        /// <summary>Remaining countdown time in seconds.</summary>
        public float CountdownRemaining => _countdownRemaining;

        /// <summary>Countdown duration in seconds.</summary>
        public float CountdownDuration => _countdownDuration;

        /// <summary>Fired when the lobby state changes.</summary>
        public event Action<LobbyState> OnStateChanged;

        /// <summary>Fired when a player joins or leaves a slot.</summary>
        public event Action<int, bool> OnSlotOccupancyChanged;

        /// <summary>Fired when a player toggles ready.</summary>
        public event Action<int, bool> OnPlayerReadyChanged;

        /// <summary>Fired when countdown finishes and match should start.</summary>
        public event Action OnMatchStart;

        public LobbyLogic() : this(DefaultCountdownDuration) { }

        public LobbyLogic(float countdownDuration)
        {
            _countdownDuration = countdownDuration;
            _countdownRemaining = countdownDuration;
            _state = LobbyState.WaitingForPlayers;

            _slots = new PlayerSlot[MaxPlayers];
            _slots[0] = new PlayerSlot(0, ControlScheme.KeyboardMouse, 0);
            _slots[1] = new PlayerSlot(1, ControlScheme.Gamepad, 1);
        }

        /// <summary>Get a player slot by index.</summary>
        public PlayerSlot GetSlot(int index)
        {
            if (index < 0 || index >= MaxPlayers) return null;
            return _slots[index];
        }

        /// <summary>Number of occupied slots.</summary>
        public int OccupiedSlotCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < MaxPlayers; i++)
                {
                    if (_slots[i].IsOccupied) count++;
                }
                return count;
            }
        }

        /// <summary>Number of ready players.</summary>
        public int ReadyPlayerCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < MaxPlayers; i++)
                {
                    if (_slots[i].IsReady) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// Player joins a slot.
        /// Returns true if successfully joined.
        /// </summary>
        public bool JoinSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxPlayers) return false;
            if (_slots[slotIndex].IsOccupied) return false;
            if (_state == LobbyState.Starting) return false;

            _slots[slotIndex].IsOccupied = true;
            OnSlotOccupancyChanged?.Invoke(slotIndex, true);
            UpdateState();
            return true;
        }

        /// <summary>
        /// Player leaves a slot.
        /// </summary>
        public void LeaveSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxPlayers) return;
            if (!_slots[slotIndex].IsOccupied) return;

            _slots[slotIndex].IsOccupied = false;
            _slots[slotIndex].IsReady = false;
            OnSlotOccupancyChanged?.Invoke(slotIndex, false);
            UpdateState();
        }

        /// <summary>
        /// Toggle ready state for a player slot.
        /// Returns the new ready state.
        /// </summary>
        public bool ToggleReady(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxPlayers) return false;
            if (!_slots[slotIndex].IsOccupied) return false;

            _slots[slotIndex].IsReady = !_slots[slotIndex].IsReady;
            OnPlayerReadyChanged?.Invoke(slotIndex, _slots[slotIndex].IsReady);
            UpdateState();
            return _slots[slotIndex].IsReady;
        }

        /// <summary>
        /// Set ready state directly for a player slot.
        /// </summary>
        public void SetReady(int slotIndex, bool ready)
        {
            if (slotIndex < 0 || slotIndex >= MaxPlayers) return;
            if (!_slots[slotIndex].IsOccupied) return;

            _slots[slotIndex].IsReady = ready;
            OnPlayerReadyChanged?.Invoke(slotIndex, ready);
            UpdateState();
        }

        /// <summary>
        /// Tick the countdown timer.
        /// Call this each frame (deltaTime in seconds) while state is CountingDown.
        /// </summary>
        public void TickCountdown(float deltaTime)
        {
            if (_state != LobbyState.CountingDown) return;

            _countdownRemaining -= deltaTime;
            if (_countdownRemaining <= 0f)
            {
                _countdownRemaining = 0f;
                TransitionTo(LobbyState.Starting);
                OnMatchStart?.Invoke();
            }
        }

        /// <summary>
        /// Check if the match can be started (all slots occupied and all ready).
        /// </summary>
        public bool CanStartMatch()
        {
            return AreAllSlotsOccupied() && AreAllPlayersReady();
        }

        /// <summary>
        /// Returns true if all slots have players assigned.
        /// </summary>
        public bool AreAllSlotsOccupied()
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                if (!_slots[i].IsOccupied) return false;
            }
            return true;
        }

        /// <summary>
        /// Returns true if all occupied slots are ready.
        /// </summary>
        public bool AreAllPlayersReady()
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                if (_slots[i].IsOccupied && !_slots[i].IsReady) return false;
            }
            return true;
        }

        /// <summary>
        /// Force-start the countdown.
        /// Only works when CanStartMatch() returns true.
        /// </summary>
        public bool StartCountdown()
        {
            if (!CanStartMatch()) return false;
            _countdownRemaining = _countdownDuration;
            TransitionTo(LobbyState.CountingDown);
            return true;
        }

        /// <summary>
        /// Cancel an in-progress countdown (e.g., if a player un-readies).
        /// </summary>
        public void CancelCountdown()
        {
            if (_state != LobbyState.CountingDown) return;
            _countdownRemaining = _countdownDuration;
            UpdateState();
        }

        /// <summary>
        /// Reset the lobby to initial state.
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                _slots[i].IsOccupied = false;
                _slots[i].IsReady = false;
            }
            _countdownRemaining = _countdownDuration;
            TransitionTo(LobbyState.WaitingForPlayers);
        }

        private void UpdateState()
        {
            if (_state == LobbyState.Starting) return;

            if (!AreAllSlotsOccupied())
            {
                TransitionTo(LobbyState.WaitingForPlayers);
            }
            else if (AreAllPlayersReady())
            {
                TransitionTo(LobbyState.AllReady);
            }
            else
            {
                TransitionTo(LobbyState.AllConnected);
            }
        }

        private void TransitionTo(LobbyState newState)
        {
            if (_state == newState) return;
            _state = newState;
            OnStateChanged?.Invoke(_state);
        }
    }

    /// <summary>
    /// Maps a device type (keyboard or gamepad) to a player slot.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class DeviceAssignment
    {
        /// <summary>Device type: "Keyboard" or "Gamepad".</summary>
        public string DeviceType { get; }

        /// <summary>Unique device identifier.</summary>
        public int DeviceId { get; }

        /// <summary>Player slot index this device is assigned to.</summary>
        public int PlayerSlotIndex { get; }

        public DeviceAssignment(string deviceType, int deviceId, int playerSlotIndex)
        {
            DeviceType = deviceType;
            DeviceId = deviceId;
            PlayerSlotIndex = playerSlotIndex;
        }
    }

    /// <summary>
    /// Detects input conflicts between players.
    /// Ensures no two players share the same input device when they should not.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class InputConflictDetector
    {
        private readonly DeviceInputRouter _router;
        private readonly ControlSchemeAssigner _assigner;

        public InputConflictDetector(DeviceInputRouter router, ControlSchemeAssigner assigner)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _assigner = assigner ?? throw new ArgumentNullException(nameof(assigner));
        }

        /// <summary>
        /// Check if a device can be assigned to a player without causing a conflict.
        /// A conflict occurs when a device is already assigned to another player.
        /// </summary>
        public bool CanAssignDevice(int deviceId, int playerIndex)
        {
            int currentOwner = _router.GetOwningPlayer(deviceId);
            // Device is free or already assigned to the same player
            return currentOwner == -1 || currentOwner == playerIndex;
        }

        /// <summary>
        /// Check if there are any conflicts in the current device assignments.
        /// Returns true if no conflicts exist (all assignments valid).
        /// </summary>
        public bool IsConflictFree()
        {
            // Both players need at least one device assigned
            bool p1Has = _router.HasDeviceForPlayer(0);
            bool p2Has = _router.HasDeviceForPlayer(1);
            return p1Has && p2Has;
        }

        /// <summary>
        /// Validate that a device is available for assignment.
        /// Returns true if the device is not assigned to anyone.
        /// </summary>
        public bool IsDeviceAvailable(int deviceId)
        {
            return !_router.IsDeviceAssigned(deviceId);
        }

        /// <summary>
        /// Validate the full configuration: both players have separate devices
        /// and their control schemes are properly configured.
        /// </summary>
        public bool ValidateConfiguration()
        {
            // Both players must have devices
            if (!_router.HasDeviceForPlayer(0) || !_router.HasDeviceForPlayer(1))
                return false;

            // If both use same scheme type, ensure they have distinct devices
            if (!_assigner.AreSchemesSeparate())
            {
                int p1Device = _router.GetDeviceForPlayer(0);
                int p2Device = _router.GetDeviceForPlayer(1);
                if (p1Device == p2Device) return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Configuration for split keyboard mode where both players
    /// share the same keyboard with different key sets.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class SplitControlConfig
    {
        /// <summary>Control mode for the local multiplayer setup.</summary>
        public SplitControlMode Mode { get; set; }

        /// <summary>Player 1 key config (defaults to WASD+actions).</summary>
        public SplitKeyboardPlayerConfig Player1Keys { get; set; }

        /// <summary>Player 2 key config (defaults to Arrows+actions).</summary>
        public SplitKeyboardPlayerConfig Player2Keys { get; set; }

        public SplitControlConfig()
        {
            Mode = SplitControlMode.KeyboardAndGamepad;
            Player1Keys = SplitKeyboardPlayerConfig.CreatePlayer1Default();
            Player2Keys = SplitKeyboardPlayerConfig.CreatePlayer2Default();
        }

        /// <summary>
        /// Create a keyboard+gamepad split (default mode).
        /// Player 1 = keyboard, Player 2 = gamepad.
        /// </summary>
        public static SplitControlConfig CreateKeyboardGamepadSplit()
        {
            return new SplitControlConfig
            {
                Mode = SplitControlMode.KeyboardAndGamepad
            };
        }

        /// <summary>
        /// Create a split keyboard mode.
        /// Player 1 = WASD side, Player 2 = Arrow keys side.
        /// </summary>
        public static SplitControlConfig CreateSplitKeyboard()
        {
            return new SplitControlConfig
            {
                Mode = SplitControlMode.SplitKeyboard
            };
        }

        /// <summary>
        /// Create a dual gamepad mode.
        /// Both players use separate gamepads.
        /// </summary>
        public static SplitControlConfig CreateDualGamepad()
        {
            return new SplitControlConfig
            {
                Mode = SplitControlMode.DualGamepad
            };
        }
    }

    /// <summary>
    /// Specifies the control mode for local multiplayer.
    /// </summary>
    public enum SplitControlMode
    {
        /// <summary>Player 1 on keyboard/mouse, Player 2 on gamepad (default).</summary>
        KeyboardAndGamepad,

        /// <summary>Both players share the keyboard with split key zones.</summary>
        SplitKeyboard,

        /// <summary>Both players on separate gamepads.</summary>
        DualGamepad
    }

    /// <summary>
    /// Key binding configuration for one player in split keyboard mode.
    /// Pure C# — no Unity dependency. Uses string key names that map to
    /// Unity's Key enum names at the Gameplay layer.
    /// </summary>
    public class SplitKeyboardPlayerConfig
    {
        /// <summary>Key for moving up.</summary>
        public string MoveUp { get; set; }

        /// <summary>Key for moving down.</summary>
        public string MoveDown { get; set; }

        /// <summary>Key for moving left.</summary>
        public string MoveLeft { get; set; }

        /// <summary>Key for moving right.</summary>
        public string MoveRight { get; set; }

        /// <summary>Key for pass action.</summary>
        public string Pass { get; set; }

        /// <summary>Key for shoot action.</summary>
        public string Shoot { get; set; }

        /// <summary>Key for sprint action (hold).</summary>
        public string Sprint { get; set; }

        /// <summary>Key for tackle action.</summary>
        public string Tackle { get; set; }

        /// <summary>Key for switch player action.</summary>
        public string Switch { get; set; }

        /// <summary>Key for through ball action.</summary>
        public string ThroughBall { get; set; }

        /// <summary>Key for lob pass action.</summary>
        public string LobPass { get; set; }

        /// <summary>
        /// Create default Player 1 key bindings (WASD + left-side keys).
        /// </summary>
        public static SplitKeyboardPlayerConfig CreatePlayer1Default()
        {
            return new SplitKeyboardPlayerConfig
            {
                MoveUp = "W",
                MoveDown = "S",
                MoveLeft = "A",
                MoveRight = "D",
                Pass = "Space",
                Shoot = "F",
                Sprint = "LeftShift",
                Tackle = "G",
                Switch = "Q",
                ThroughBall = "R",
                LobPass = "E"
            };
        }

        /// <summary>
        /// Create default Player 2 key bindings (Arrow keys + right-side keys).
        /// </summary>
        public static SplitKeyboardPlayerConfig CreatePlayer2Default()
        {
            return new SplitKeyboardPlayerConfig
            {
                MoveUp = "UpArrow",
                MoveDown = "DownArrow",
                MoveLeft = "LeftArrow",
                MoveRight = "RightArrow",
                Pass = "Enter",
                Shoot = "RightShift",
                Sprint = "RightControl",
                Tackle = "Period",
                Switch = "Comma",
                ThroughBall = "Slash",
                LobPass = "Semicolon"
            };
        }

        /// <summary>
        /// Returns an array of all bound key names for this player.
        /// Useful for conflict detection.
        /// </summary>
        public string[] GetAllBoundKeys()
        {
            return new[]
            {
                MoveUp, MoveDown, MoveLeft, MoveRight,
                Pass, Shoot, Sprint, Tackle, Switch, ThroughBall, LobPass
            };
        }

        /// <summary>
        /// Check if a key name is used in this config.
        /// </summary>
        public bool UsesKey(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return false;
            var keys = GetAllBoundKeys();
            for (int i = 0; i < keys.Length; i++)
            {
                if (string.Equals(keys[i], keyName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Detects key binding conflicts between two split keyboard configs.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class SplitKeyboardConflictChecker
    {
        /// <summary>
        /// Check whether two player configs have any overlapping key bindings.
        /// Returns true if a conflict is found.
        /// </summary>
        public bool HasConflict(SplitKeyboardPlayerConfig player1, SplitKeyboardPlayerConfig player2)
        {
            if (player1 == null || player2 == null) return false;

            var p1Keys = player1.GetAllBoundKeys();
            var p2Keys = player2.GetAllBoundKeys();

            for (int i = 0; i < p1Keys.Length; i++)
            {
                for (int j = 0; j < p2Keys.Length; j++)
                {
                    if (string.Equals(p1Keys[i], p2Keys[j], StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get the list of conflicting key names.
        /// </summary>
        public List<string> GetConflictingKeys(SplitKeyboardPlayerConfig player1, SplitKeyboardPlayerConfig player2)
        {
            var conflicts = new List<string>();
            if (player1 == null || player2 == null) return conflicts;

            var p1Keys = player1.GetAllBoundKeys();
            var p2Keys = player2.GetAllBoundKeys();

            for (int i = 0; i < p1Keys.Length; i++)
            {
                for (int j = 0; j < p2Keys.Length; j++)
                {
                    if (string.Equals(p1Keys[i], p2Keys[j], StringComparison.OrdinalIgnoreCase))
                    {
                        if (!conflicts.Contains(p1Keys[i]))
                            conflicts.Add(p1Keys[i]);
                    }
                }
            }
            return conflicts;
        }
    }
}
