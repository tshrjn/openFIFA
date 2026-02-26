using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# ball ownership tracking logic.
    /// Tracks which player (by ID) owns the ball.
    /// -1 indicates no owner (loose ball).
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class BallOwnershipLogic
    {
        private int _currentOwnerId = -1;

        /// <summary>Current owner player ID. -1 means no owner (loose ball).</summary>
        public int CurrentOwnerId => _currentOwnerId;

        /// <summary>Whether the ball is currently owned by any player.</summary>
        public bool IsOwned => _currentOwnerId >= 0;

        /// <summary>Fired when ownership changes. Args: (previousOwnerId, newOwnerId).</summary>
        public event Action<int, int> OnOwnerChanged;

        /// <summary>
        /// Set a new owner. Does nothing if the owner is already the same.
        /// </summary>
        public void SetOwner(int playerId)
        {
            if (_currentOwnerId == playerId) return;

            int previousOwner = _currentOwnerId;
            _currentOwnerId = playerId;
            OnOwnerChanged?.Invoke(previousOwner, _currentOwnerId);
        }

        /// <summary>
        /// Release ownership. Ball becomes loose (-1).
        /// </summary>
        public void Release()
        {
            if (_currentOwnerId == -1) return;

            int previousOwner = _currentOwnerId;
            _currentOwnerId = -1;
            OnOwnerChanged?.Invoke(previousOwner, -1);
        }

        /// <summary>
        /// Transfer ownership directly from current owner to another player.
        /// </summary>
        public void Transfer(int newPlayerId)
        {
            if (_currentOwnerId == newPlayerId) return;

            int previousOwner = _currentOwnerId;
            _currentOwnerId = newPlayerId;
            OnOwnerChanged?.Invoke(previousOwner, _currentOwnerId);
        }

        /// <summary>
        /// Check if a player can claim the ball.
        /// Requires: ball is not owned and player is within claim radius.
        /// </summary>
        /// <param name="distanceToBall">Distance from the player to the ball.</param>
        /// <param name="claimRadius">Maximum distance to claim the ball.</param>
        public bool CanClaim(float distanceToBall, float claimRadius)
        {
            return !IsOwned && distanceToBall <= claimRadius;
        }
    }
}
