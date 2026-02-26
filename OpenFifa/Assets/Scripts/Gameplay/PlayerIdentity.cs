using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Identifies a player with a unique ID and team affiliation.
    /// Attached to each player GameObject.
    /// </summary>
    public class PlayerIdentity : MonoBehaviour
    {
        [SerializeField] private int _playerId;
        [SerializeField] private TeamIdentifier _team;
        [SerializeField] private string _playerName = "Player";

        /// <summary>Unique player ID.</summary>
        public int PlayerId => _playerId;

        /// <summary>Which team this player belongs to.</summary>
        public TeamIdentifier Team => _team;

        /// <summary>Display name.</summary>
        public string PlayerName => _playerName;

        /// <summary>
        /// Configure this player identity at runtime.
        /// </summary>
        public void Configure(int id, TeamIdentifier team, string playerName)
        {
            _playerId = id;
            _team = team;
            _playerName = playerName;
        }
    }
}
