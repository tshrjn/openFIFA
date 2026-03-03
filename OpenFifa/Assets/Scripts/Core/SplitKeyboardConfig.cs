using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Full split keyboard configuration that holds both players' key mappings
    /// and provides validation and action resolution.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class SplitKeyboardConfig
    {
        private readonly SplitKeyboardPlayerConfig _player1;
        private readonly SplitKeyboardPlayerConfig _player2;
        private readonly SplitKeyboardConflictChecker _conflictChecker;

        /// <summary>Player 1 key bindings.</summary>
        public SplitKeyboardPlayerConfig Player1 => _player1;

        /// <summary>Player 2 key bindings.</summary>
        public SplitKeyboardPlayerConfig Player2 => _player2;

        /// <summary>
        /// Create split keyboard config with default bindings.
        /// P1: WASD + Space/Shift/Q/E/F/G/R
        /// P2: Arrows + Enter/RShift/RCtrl/Comma/Period/Slash/Semicolon
        /// </summary>
        public SplitKeyboardConfig()
            : this(
                SplitKeyboardPlayerConfig.CreatePlayer1Default(),
                SplitKeyboardPlayerConfig.CreatePlayer2Default())
        {
        }

        /// <summary>
        /// Create split keyboard config with custom bindings.
        /// </summary>
        public SplitKeyboardConfig(SplitKeyboardPlayerConfig player1, SplitKeyboardPlayerConfig player2)
        {
            _player1 = player1 ?? throw new ArgumentNullException(nameof(player1));
            _player2 = player2 ?? throw new ArgumentNullException(nameof(player2));
            _conflictChecker = new SplitKeyboardConflictChecker();
        }

        /// <summary>
        /// Returns the player config for a given player index (0 or 1).
        /// </summary>
        public SplitKeyboardPlayerConfig GetPlayerConfig(int playerIndex)
        {
            if (playerIndex == 0) return _player1;
            if (playerIndex == 1) return _player2;
            return null;
        }

        /// <summary>
        /// Determine which player a key belongs to.
        /// Returns 0 for P1, 1 for P2, -1 if unmapped.
        /// </summary>
        public int GetOwningPlayer(string keyName)
        {
            if (_player1.UsesKey(keyName)) return 0;
            if (_player2.UsesKey(keyName)) return 1;
            return -1;
        }

        /// <summary>
        /// Resolve a key press to a movement direction for the owning player.
        /// Returns (playerIndex, horizontal, vertical).
        /// playerIndex is -1 if the key is not a movement key.
        /// </summary>
        public (int playerIndex, float horizontal, float vertical) ResolveMovement(string keyName)
        {
            // Check P1 movement
            if (IsKeyMatch(_player1.MoveUp, keyName)) return (0, 0f, 1f);
            if (IsKeyMatch(_player1.MoveDown, keyName)) return (0, 0f, -1f);
            if (IsKeyMatch(_player1.MoveLeft, keyName)) return (0, -1f, 0f);
            if (IsKeyMatch(_player1.MoveRight, keyName)) return (0, 1f, 0f);

            // Check P2 movement
            if (IsKeyMatch(_player2.MoveUp, keyName)) return (1, 0f, 1f);
            if (IsKeyMatch(_player2.MoveDown, keyName)) return (1, 0f, -1f);
            if (IsKeyMatch(_player2.MoveLeft, keyName)) return (1, -1f, 0f);
            if (IsKeyMatch(_player2.MoveRight, keyName)) return (1, 1f, 0f);

            return (-1, 0f, 0f);
        }

        /// <summary>
        /// Resolve a key press to an action for the owning player.
        /// Returns (playerIndex, actionType).
        /// playerIndex is -1 if the key is not an action key.
        /// </summary>
        public (int playerIndex, ActionType action) ResolveAction(string keyName)
        {
            // Check P1 actions
            if (IsKeyMatch(_player1.Pass, keyName)) return (0, ActionType.Pass);
            if (IsKeyMatch(_player1.Shoot, keyName)) return (0, ActionType.Shoot);
            if (IsKeyMatch(_player1.Sprint, keyName)) return (0, ActionType.Sprint);
            if (IsKeyMatch(_player1.Tackle, keyName)) return (0, ActionType.Tackle);
            if (IsKeyMatch(_player1.Switch, keyName)) return (0, ActionType.Switch);
            if (IsKeyMatch(_player1.ThroughBall, keyName)) return (0, ActionType.ThroughBall);
            if (IsKeyMatch(_player1.LobPass, keyName)) return (0, ActionType.LobPass);

            // Check P2 actions
            if (IsKeyMatch(_player2.Pass, keyName)) return (1, ActionType.Pass);
            if (IsKeyMatch(_player2.Shoot, keyName)) return (1, ActionType.Shoot);
            if (IsKeyMatch(_player2.Sprint, keyName)) return (1, ActionType.Sprint);
            if (IsKeyMatch(_player2.Tackle, keyName)) return (1, ActionType.Tackle);
            if (IsKeyMatch(_player2.Switch, keyName)) return (1, ActionType.Switch);
            if (IsKeyMatch(_player2.ThroughBall, keyName)) return (1, ActionType.ThroughBall);
            if (IsKeyMatch(_player2.LobPass, keyName)) return (1, ActionType.LobPass);

            return (-1, ActionType.None);
        }

        /// <summary>
        /// Validate that the config has no key conflicts between players.
        /// </summary>
        public bool IsValid()
        {
            return !_conflictChecker.HasConflict(_player1, _player2);
        }

        /// <summary>
        /// Get the list of conflicting key names, if any.
        /// </summary>
        public List<string> GetConflicts()
        {
            return _conflictChecker.GetConflictingKeys(_player1, _player2);
        }

        private static bool IsKeyMatch(string configKey, string inputKey)
        {
            return string.Equals(configKey, inputKey, StringComparison.OrdinalIgnoreCase);
        }
    }
}
