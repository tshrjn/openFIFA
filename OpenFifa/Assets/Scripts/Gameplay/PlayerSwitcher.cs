using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Handles switching player control to the teammate nearest to the ball.
    /// Integrates PlayerSwitchLogic (pure C#) with Unity GameObjects.
    /// Only one player on the team is human-controlled at any time.
    /// </summary>
    public class PlayerSwitcher : MonoBehaviour
    {
        [SerializeField] private Transform _ball;
        [SerializeField] private List<Transform> _teamPlayers = new List<Transform>();
        [SerializeField] private InputActionReference _switchAction;

        private PlayerSwitchLogic _logic;
        private int _activePlayerIndex;

        /// <summary>Currently controlled player index.</summary>
        public int ActivePlayerIndex => _activePlayerIndex;

        /// <summary>Currently controlled player Transform.</summary>
        public Transform ActivePlayer =>
            _teamPlayers != null && _activePlayerIndex >= 0 && _activePlayerIndex < _teamPlayers.Count
                ? _teamPlayers[_activePlayerIndex]
                : null;

        /// <summary>Fired when player switch occurs. Args: (previousIndex, newIndex).</summary>
        public event Action<int, int> OnPlayerSwitched;

        private void Awake()
        {
            _logic = new PlayerSwitchLogic();
            _activePlayerIndex = 0;

            // Enable the first player's controller, disable AI on them
            UpdatePlayerStates();
        }

        private void OnEnable()
        {
            if (_switchAction != null && _switchAction.action != null)
            {
                _switchAction.action.Enable();
                _switchAction.action.performed += OnSwitchPerformed;
            }
        }

        private void OnDisable()
        {
            if (_switchAction != null && _switchAction.action != null)
            {
                _switchAction.action.performed -= OnSwitchPerformed;
            }
        }

        /// <summary>
        /// Programmatic switch trigger (for tests and AI coordination).
        /// </summary>
        public void SwitchPlayer()
        {
            if (_teamPlayers == null || _teamPlayers.Count < 2 || _ball == null)
                return;

            var positions = BuildPositionArray();
            var result = _logic.PerformSwitch(
                positions,
                _ball.position.x,
                _ball.position.z,
                _activePlayerIndex
            );

            if (result.SwitchOccurred)
            {
                int previousIndex = _activePlayerIndex;
                _activePlayerIndex = result.NewActiveIndex;
                UpdatePlayerStates();
                OnPlayerSwitched?.Invoke(previousIndex, _activePlayerIndex);
            }
        }

        private void OnSwitchPerformed(InputAction.CallbackContext ctx)
        {
            SwitchPlayer();
        }

        private PositionData[] BuildPositionArray()
        {
            var positions = new PositionData[_teamPlayers.Count];
            for (int i = 0; i < _teamPlayers.Count; i++)
            {
                positions[i] = new PositionData
                {
                    Id = i,
                    X = _teamPlayers[i].position.x,
                    Z = _teamPlayers[i].position.z
                };
            }
            return positions;
        }

        private void UpdatePlayerStates()
        {
            for (int i = 0; i < _teamPlayers.Count; i++)
            {
                if (_teamPlayers[i] == null) continue;

                var playerController = _teamPlayers[i].GetComponent<PlayerController>();
                // AIController type accessed by name to avoid hard dependency
                var aiComponent = _teamPlayers[i].GetComponent("AIController") as MonoBehaviour;

                bool isActive = (i == _activePlayerIndex);

                if (playerController != null)
                    playerController.enabled = isActive;

                if (aiComponent != null)
                    aiComponent.enabled = !isActive;

                // Visual indicator: enable/disable highlight child
                var highlight = _teamPlayers[i].Find("ActiveIndicator");
                if (highlight != null)
                    highlight.gameObject.SetActive(isActive);
            }
        }
    }
}
