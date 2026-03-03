using UnityEngine;
using TMPro;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// HUD overlay for local multiplayer lobby and in-game player indicators.
    /// Shows join prompts, player slot status, ready indicators, and countdown.
    /// </summary>
    public class LocalMultiplayerHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LocalMultiplayerManager _multiplayerManager;

        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI _joinPromptText;
        [SerializeField] private TextMeshProUGUI _player1StatusText;
        [SerializeField] private TextMeshProUGUI _player2StatusText;
        [SerializeField] private TextMeshProUGUI _countdownText;
        [SerializeField] private TextMeshProUGUI _instructionText;

        [Header("Colors")]
        [SerializeField] private Color _player1Color = new Color(0.2f, 0.4f, 0.9f);
        [SerializeField] private Color _player2Color = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private Color _readyColor = new Color(0.2f, 0.9f, 0.2f);
        [SerializeField] private Color _notReadyColor = new Color(0.6f, 0.6f, 0.6f);

        // Runtime-created UI root (if no serialized references)
        private Canvas _canvas;
        private GameObject _hudRoot;
        private bool _uiCreated;

        /// <summary>Whether the lobby HUD is currently visible.</summary>
        public bool IsVisible { get; private set; }

        private void Start()
        {
            if (_multiplayerManager == null)
                _multiplayerManager = FindAnyObjectByType<LocalMultiplayerManager>();

            if (_joinPromptText == null)
                CreateRuntimeUI();

            if (_multiplayerManager != null)
            {
                _multiplayerManager.Lobby.OnStateChanged += HandleLobbyStateChanged;
                _multiplayerManager.Lobby.OnSlotOccupancyChanged += HandleSlotOccupancyChanged;
                _multiplayerManager.Lobby.OnPlayerReadyChanged += HandlePlayerReadyChanged;
            }

            Show();
            UpdateDisplay();
        }

        private void OnDestroy()
        {
            if (_multiplayerManager != null && _multiplayerManager.Lobby != null)
            {
                _multiplayerManager.Lobby.OnStateChanged -= HandleLobbyStateChanged;
                _multiplayerManager.Lobby.OnSlotOccupancyChanged -= HandleSlotOccupancyChanged;
                _multiplayerManager.Lobby.OnPlayerReadyChanged -= HandlePlayerReadyChanged;
            }
        }

        private void Update()
        {
            if (!IsVisible || _multiplayerManager == null) return;

            // Update countdown display
            if (_multiplayerManager.Lobby.State == LobbyState.CountingDown)
            {
                UpdateCountdown();
            }
        }

        /// <summary>Show the lobby HUD.</summary>
        public void Show()
        {
            IsVisible = true;
            if (_hudRoot != null) _hudRoot.SetActive(true);
        }

        /// <summary>Hide the lobby HUD.</summary>
        public void Hide()
        {
            IsVisible = false;
            if (_hudRoot != null) _hudRoot.SetActive(false);
        }

        /// <summary>
        /// Get the join prompt text for a specific control scheme.
        /// </summary>
        public static string GetJoinPrompt(ControlScheme scheme)
        {
            switch (scheme)
            {
                case ControlScheme.KeyboardMouse:
                    return "Press SPACE to join";
                case ControlScheme.Gamepad:
                    return "Press A to join";
                default:
                    return "Press any button to join";
            }
        }

        /// <summary>
        /// Get the ready prompt text for a specific control scheme.
        /// </summary>
        public static string GetReadyPrompt(ControlScheme scheme)
        {
            switch (scheme)
            {
                case ControlScheme.KeyboardMouse:
                    return "Press ENTER to ready up";
                case ControlScheme.Gamepad:
                    return "Press START to ready up";
                default:
                    return "Press button to ready up";
            }
        }

        /// <summary>
        /// Format a player slot display string.
        /// </summary>
        public static string FormatSlotDisplay(PlayerSlot slot)
        {
            if (slot == null) return "";

            string status = slot.IsOccupied
                ? (slot.IsReady ? "READY" : "Not Ready")
                : "Empty";

            string scheme = slot.Scheme == ControlScheme.KeyboardMouse
                ? "Keyboard"
                : "Gamepad";

            string team = slot.TeamIndex == 0 ? "Team A" : "Team B";

            return $"{slot.DisplayName} [{scheme}] - {team} - {status}";
        }

        /// <summary>
        /// Format the countdown display.
        /// </summary>
        public static string FormatCountdown(float seconds)
        {
            int whole = Mathf.CeilToInt(seconds);
            if (whole <= 0) return "GO!";
            return whole.ToString();
        }

        private void UpdateDisplay()
        {
            if (_multiplayerManager == null) return;

            var lobby = _multiplayerManager.Lobby;
            var slot1 = lobby.GetSlot(0);
            var slot2 = lobby.GetSlot(1);

            // Update slot displays
            if (_player1StatusText != null)
            {
                _player1StatusText.text = FormatSlotDisplay(slot1);
                _player1StatusText.color = slot1.IsReady ? _readyColor : _player1Color;
            }

            if (_player2StatusText != null)
            {
                _player2StatusText.text = FormatSlotDisplay(slot2);
                _player2StatusText.color = slot2.IsReady ? _readyColor : _player2Color;
            }

            // Update join prompt
            if (_joinPromptText != null)
            {
                switch (lobby.State)
                {
                    case LobbyState.WaitingForPlayers:
                        if (!slot1.IsOccupied)
                            _joinPromptText.text = GetJoinPrompt(ControlScheme.KeyboardMouse);
                        else if (!slot2.IsOccupied)
                            _joinPromptText.text = GetJoinPrompt(ControlScheme.Gamepad);
                        break;
                    case LobbyState.AllConnected:
                        _joinPromptText.text = "Press READY to start";
                        break;
                    case LobbyState.AllReady:
                        _joinPromptText.text = "All players ready!";
                        break;
                    case LobbyState.CountingDown:
                        _joinPromptText.text = "";
                        break;
                    case LobbyState.Starting:
                        _joinPromptText.text = "Starting match...";
                        break;
                }
            }

            // Update instruction text
            if (_instructionText != null)
            {
                if (lobby.State == LobbyState.WaitingForPlayers)
                {
                    _instructionText.text = "P1: WASD + Space | P2: Connect gamepad";
                }
                else
                {
                    _instructionText.text = "";
                }
            }
        }

        private void UpdateCountdown()
        {
            if (_countdownText == null || _multiplayerManager == null) return;

            float remaining = _multiplayerManager.Lobby.CountdownRemaining;
            _countdownText.text = FormatCountdown(remaining);
        }

        private void HandleLobbyStateChanged(LobbyState state)
        {
            UpdateDisplay();

            if (state == LobbyState.Starting)
            {
                Hide();
            }
        }

        private void HandleSlotOccupancyChanged(int slotIndex, bool occupied)
        {
            UpdateDisplay();
        }

        private void HandlePlayerReadyChanged(int slotIndex, bool ready)
        {
            UpdateDisplay();
        }

        private void CreateRuntimeUI()
        {
            _uiCreated = true;

            // Create canvas
            _hudRoot = new GameObject("LocalMultiplayerHUD");
            _hudRoot.transform.SetParent(transform);
            _canvas = _hudRoot.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            _hudRoot.AddComponent<UnityEngine.UI.CanvasScaler>();

            // Join prompt (center top)
            _joinPromptText = CreateText(_hudRoot.transform, "JoinPrompt",
                new Vector2(0.5f, 0.85f), 32, TextAlignmentOptions.Center);

            // P1 status (left)
            _player1StatusText = CreateText(_hudRoot.transform, "P1Status",
                new Vector2(0.25f, 0.7f), 24, TextAlignmentOptions.Center);
            _player1StatusText.color = _player1Color;

            // P2 status (right)
            _player2StatusText = CreateText(_hudRoot.transform, "P2Status",
                new Vector2(0.75f, 0.7f), 24, TextAlignmentOptions.Center);
            _player2StatusText.color = _player2Color;

            // Countdown (center)
            _countdownText = CreateText(_hudRoot.transform, "Countdown",
                new Vector2(0.5f, 0.5f), 72, TextAlignmentOptions.Center);
            _countdownText.text = "";

            // Instructions (bottom)
            _instructionText = CreateText(_hudRoot.transform, "Instructions",
                new Vector2(0.5f, 0.15f), 20, TextAlignmentOptions.Center);
        }

        private TextMeshProUGUI CreateText(Transform parent, string name,
            Vector2 anchorPos, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(anchorPos.x - 0.2f, anchorPos.y - 0.05f);
            rect.anchorMax = new Vector2(anchorPos.x + 0.2f, anchorPos.y + 0.05f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.text = "";

            return tmp;
        }
    }
}
