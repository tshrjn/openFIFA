using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// ScriptableObject holding player stats configuration.
    /// Exposes tunable values in the Unity Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStatsConfig", menuName = "OpenFifa/Config/Player Stats")]
    public class PlayerStatsConfig : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField] private float _baseSpeed = 7f;
        [SerializeField] private float _sprintMultiplier = 1.5f;
        [SerializeField] private float _acceleration = 5f;
        [SerializeField] private float _deceleration = 8f;

        public float BaseSpeed => _baseSpeed;
        public float SprintMultiplier => _sprintMultiplier;
        public float Acceleration => _acceleration;
        public float Deceleration => _deceleration;

        public PlayerStatsData ToData()
        {
            return new PlayerStatsData(
                baseSpeed: _baseSpeed,
                sprintMultiplier: _sprintMultiplier,
                acceleration: _acceleration,
                deceleration: _deceleration
            );
        }
    }
}
