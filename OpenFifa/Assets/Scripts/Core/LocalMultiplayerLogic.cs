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
}
