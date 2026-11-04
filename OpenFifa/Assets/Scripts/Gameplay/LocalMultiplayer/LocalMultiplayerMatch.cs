using System;
using System.Collections.Generic;
using OpenFifa.Core;
using OpenFifa.Gameplay.SinglePlayer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenFifa.Gameplay.LocalMultiplayer
{
    /// <summary>
    /// Local multiplayer on top of <see cref="SinglePlayerMatchController"/>: a side-select screen for every connected
    /// gamepad (push left = Home, right = Away, A / Cross to kick off), then one human seat per pad in the match, each
    /// with its own resolver and teammate calls, P1..P4 markers over the controlled players, and a reconnect prompt
    /// when a pad drops. Put it next to the controller with the controller's AutoStart off.
    /// Command line: -lmBots (scripted bots on both seats, for captures), -lmMode coop|h2h, -lmAutoStart.
    /// </summary>
    public sealed class LocalMultiplayerMatch : MonoBehaviour
    {
        public static readonly Color[] SeatColours =
        {
            new Color(0.2f, 0.55f, 1f), new Color(1f, 0.3f, 0.25f), new Color(0.3f, 0.9f, 0.4f), new Color(1f, 0.85f, 0.2f),
        };

        [SerializeField] private SinglePlayerMatchController _match;
        [Tooltip("Drive both seats with ScriptedGamepadBot (no pads needed; review captures).")]
        [SerializeField] private bool _bots;
        [SerializeField] private LocalMatchMode _botMode = LocalMatchMode.HeadToHead;
        [SerializeField] private bool _autoStart;

        public ControllerAssignment Controllers { get; } = new ControllerAssignment();
        public LocalMatch Match { get; private set; }

        private readonly Dictionary<int, float> _stickX = new Dictionary<int, float>();
        private readonly Transform[] _markers = new Transform[ControllerAssignment.MaxSeats];
        private GUIStyle _big;
        private SinglePlayerMatchSession _attached;
        /// <summary>SinglePlayerCapture (-spCapture) starts the match itself, possibly more than once; we seat the
        /// pads in whatever session it starts.</summary>
        private bool _captureDrivesStart;

        private void Awake()
        {
            if (_match == null) _match = GetComponent<SinglePlayerMatchController>();
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-lmBots") _bots = true;
                else if (args[i] == "-spCapture") _captureDrivesStart = true;
                else if (args[i] == "-lmAutoStart") _autoStart = true;
                else if (args[i] == "-lmMode" && i + 1 < args.Length)
                    _botMode = args[i + 1] == "coop" ? LocalMatchMode.CoopVsAI : LocalMatchMode.HeadToHead;
            }
            if (_match != null)
            {
                _match.AutoStart = false;
                // The capture's probe must use the same seat driver as P1, so P1's side must be set before it runs.
                if (_bots) _match.HumanTeam = SimTeam.Home;
            }
        }

        private void OnEnable() => InputSystem.onDeviceChange += OnDeviceChange;
        private void OnDisable() => InputSystem.onDeviceChange -= OnDeviceChange;

        private void Start()
        {
            if (_bots)
            {
                // Two virtual pads (ids outside the Input System's range).
                Controllers.Connect(90001);
                Controllers.Connect(90002);
                Controllers.Push(90001, -1);
                Controllers.Push(90002, _botMode == LocalMatchMode.CoopVsAI ? -1 : +1);
            }
            foreach (var pad in Gamepad.all) Controllers.Connect(pad.deviceId);
            if ((_autoStart || _bots) && !_captureDrivesStart) Kickoff();
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad)) return;
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                    Controllers.Connect(device.deviceId);
                    break;
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                    Controllers.Disconnect(device.deviceId);
                    break;
            }
        }

        private void Update()
        {
            // Started (or restarted) by someone else: the single-player picker or the review capture.
            if (_match != null && _match.Started && _match.Session != _attached)
            {
                if (Controllers.CanStart && _match.Session.HumanTeam == LocalMatch.PrimaryTeam(Controllers)) Attach();
                return;
            }
            if (Match == null)
            {
                if (_match == null || !_match.Started) SideSelect();
                return;
            }
            if (Controllers.AwaitingReconnect)
            {
                // Any button on an unseated pad claims the empty seat.
                foreach (var pad in Gamepad.all)
                    if (pad.wasUpdatedThisFrame && pad.buttonSouth.wasPressedThisFrame) Controllers.Connect(pad.deviceId);
            }
        }

        private void LateUpdate()
        {
            if (Match == null) return;
            for (int k = 0; k < _markers.Length; k++)
            {
                var slot = Match.SlotFor(k);
                bool show = slot != null && slot.Input != null && slot.ControlledId >= 0;
                if (show && _markers[k] == null) _markers[k] = MakeMarker(SeatColours[k]);
                if (_markers[k] == null) continue;
                _markers[k].gameObject.SetActive(show);
                if (!show) continue;
                var body = _match.PlayerTransform(slot.ControlledId);
                if (body != null) _markers[k].position = body.position + new Vector3(0f, 2.25f, 0f);
            }
        }

        private void SideSelect()
        {
            foreach (var pad in Gamepad.all)
            {
                int id = pad.deviceId;
                if (Controllers.Connect(id) < 0) continue;
                float x = pad.leftStick.ReadValue().x;
                _stickX.TryGetValue(id, out float prev);
                if (x <= -0.6f && prev > -0.6f) Controllers.Push(id, -1);
                if (x >= 0.6f && prev < 0.6f) Controllers.Push(id, +1);
                if (pad.dpad.left.wasPressedThisFrame) Controllers.Push(id, -1);
                if (pad.dpad.right.wasPressedThisFrame) Controllers.Push(id, +1);
                _stickX[id] = x;
                if (pad.buttonSouth.wasPressedThisFrame && Controllers.CanStart)
                {
                    Kickoff();
                    return;
                }
            }
        }

        private void Kickoff()
        {
            if (!Controllers.CanStart || _match == null) return;
            _match.HumanTeam = LocalMatch.PrimaryTeam(Controllers);
            _match.StartMatch(_match.Level);
            Attach();
        }

        private void Attach()
        {
            Match?.End();
            var session = _match.Session;
            _attached = session;
            var settings = new ControlSettings { AssistLevel = session.Profile.PassAssist };
            Match = new LocalMatch(session, Controllers, seat => InputFor(seat, settings));
            Debug.Log($"[{SinglePlayerMatchController.ProductName}] local multiplayer: {Match.Mode}, {Controllers.Seats.Count} pads");
        }

        private IHumanInputSource InputFor(ControllerSeat seat, ControlSettings settings)
        {
            if (_bots && seat.DeviceId >= 90000) return new ScriptedGamepadBot(settings);
            int device = seat.DeviceId;
            return new LocalGamepadInput(settings)
            {
                FrameSource = () => InputSystem.GetDeviceById(device) is Gamepad pad ? SinglePlayerInput.ReadGamepad(pad) : default,
            };
        }

        private static Transform MakeMarker(Color colour)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Seat marker";
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(0.35f, 0.04f, 0.35f);
            var r = go.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader != null) r.material = new Material(shader) { color = colour };
            else r.material.color = colour;
            return go.transform;
        }

        private void OnGUI()
        {
            GUI.depth = -10; // above the single-player HUD
            if (_match != null && _match.Started && Match == null) return;
            if (_big == null) _big = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            if (Match == null) DrawSideSelect();
            else if (Controllers.AwaitingReconnect) DrawReconnect();
        }

        private void DrawSideSelect()
        {
            float w = Screen.width, h = Screen.height;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, h * 0.08f, w, 40), "LOCAL MATCH: push left for HOME, right for AWAY", _big);
            float colW = w / 3f;
            GUI.Label(new Rect(0, h * 0.2f, colW, 40), "HOME", _big);
            GUI.Label(new Rect(colW * 2f, h * 0.2f, colW, 40), "AWAY", _big);
            var rows = new int[3];
            foreach (var seat in Controllers.Seats)
            {
                int col = seat.Side == ControllerSide.Home ? 0 : seat.Side == ControllerSide.Away ? 2 : 1;
                GUI.color = SeatColours[seat.Slot % SeatColours.Length];
                GUI.Label(new Rect(colW * col, h * 0.3f + rows[col]++ * 44f, colW, 40), $"P{seat.Slot + 1}", _big);
            }
            GUI.color = Color.white;
            string mode = Controllers.Mode switch
            {
                LocalMatchMode.SoloVsAI => "1 player vs AI",
                LocalMatchMode.HeadToHead => "Head to head",
                LocalMatchMode.CoopVsAI => "Co-op vs AI",
                LocalMatchMode.CoopHeadToHead => "Co-op head to head",
                _ => Controllers.Seats.Count == 0 ? "Connect a controller" : "Pick a side",
            };
            GUI.Label(new Rect(0, h * 0.8f, w, 40), Controllers.CanStart ? mode + "   (A / Cross to kick off)" : mode, _big);
        }

        private void DrawReconnect()
        {
            float w = Screen.width, h = Screen.height;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, h * 0.4f, w, h * 0.2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            foreach (var seat in Controllers.Seats)
            {
                if (!seat.Disconnected) continue;
                GUI.Label(new Rect(0, h * 0.45f, w, 40), $"P{seat.Slot + 1} controller disconnected: press A / Cross on a controller to take over", _big);
                break;
            }
        }
    }
}
