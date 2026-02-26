using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Configuration for local multiplayer on a single device.
    /// Two human players, each controlling one team, with AI filling remaining slots.
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
    /// Determines which player zone a touch position belongs to.
    /// Left half = Player 1, Right half = Player 2.
    /// </summary>
    public class SplitTouchZoneLogic
    {
        private readonly int _screenWidth;
        private readonly int _screenHeight;
        private readonly int _midX;

        public SplitTouchZoneLogic(int screenWidth, int screenHeight)
        {
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            _midX = screenWidth / 2;
        }

        /// <summary>Returns true if the touch position is in Player 1's zone (left half).</summary>
        public bool IsPlayer1Zone(float x, float y)
        {
            return x < _midX;
        }

        /// <summary>Returns true if the touch position is in Player 2's zone (right half).</summary>
        public bool IsPlayer2Zone(float x, float y)
        {
            return x >= _midX;
        }

        /// <summary>Returns player index (0 or 1) for the given touch position.</summary>
        public int GetPlayerForPosition(float x, float y)
        {
            return x < _midX ? 0 : 1;
        }
    }

    /// <summary>
    /// Routes touch inputs to the correct player based on finger ID and zone.
    /// Tracks active touches to prevent cross-talk between players.
    /// </summary>
    public class InputRouter
    {
        private readonly SplitTouchZoneLogic _zoneLogic;
        private readonly Dictionary<int, int> _fingerToPlayer;

        public InputRouter(SplitTouchZoneLogic zoneLogic)
        {
            _zoneLogic = zoneLogic;
            _fingerToPlayer = new Dictionary<int, int>();
        }

        /// <summary>
        /// Registers a touch with the given finger ID at the given position.
        /// Assigns it to the correct player based on zone.
        /// </summary>
        public void ProcessTouch(int fingerId, float x, float y)
        {
            int player = _zoneLogic.GetPlayerForPosition(x, y);
            _fingerToPlayer[fingerId] = player;
        }

        /// <summary>
        /// Releases a touch by finger ID.
        /// </summary>
        public void ReleaseTouch(int fingerId)
        {
            _fingerToPlayer.Remove(fingerId);
        }

        /// <summary>
        /// Returns the owning player index for a finger ID.
        /// Returns -1 if the finger is not tracked.
        /// </summary>
        public int GetOwningPlayer(int fingerId)
        {
            if (_fingerToPlayer.TryGetValue(fingerId, out int player))
                return player;
            return -1;
        }

        /// <summary>
        /// Clears all tracked touches.
        /// </summary>
        public void ClearAll()
        {
            _fingerToPlayer.Clear();
        }
    }

    /// <summary>
    /// Layout logic for action button placement in local multiplayer mode.
    /// P1 buttons center-left, P2 buttons center-right.
    /// </summary>
    public class ActionButtonLayout
    {
        private readonly int _screenWidth;
        private readonly int _screenHeight;

        public ActionButtonLayout(int screenWidth, int screenHeight)
        {
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
        }

        /// <summary>
        /// Returns the horizontal center X position for action buttons of the given player.
        /// Player 0 = center-left (25% of width), Player 1 = center-right (75% of width).
        /// </summary>
        public float GetActionButtonCenterX(int playerIndex)
        {
            float quarter = _screenWidth / 4f;
            return playerIndex == 0 ? quarter : quarter * 3f;
        }

        /// <summary>
        /// Returns the vertical center Y position for action buttons (same for both players).
        /// Positioned in the lower third of the screen.
        /// </summary>
        public float GetActionButtonCenterY()
        {
            return _screenHeight * 0.25f;
        }
    }
}
