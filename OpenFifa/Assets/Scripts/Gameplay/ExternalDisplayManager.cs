using System;
using System.Collections.Generic;
using OpenFifa.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Drives an external monitor connected to the iPad (USB-C / Thunderbolt / AirPlay-less HDMI adapters).
    ///
    /// When a monitor connects, the game takes it over instead of being mirrored: the broadcast camera and
    /// HUD move to the monitor, which renders at 1440p or higher (up to 4K, adapted to frame time) at 60 fps,
    /// pillarboxed on super-ultrawides and letterboxed on 4:3 screens. The iPad becomes a second screen
    /// (<see cref="ExternalDisplayMode"/>). Unplugging puts everything back on the iPad without a restart.
    ///
    /// Bootstraps itself on startup; there is nothing to add to a scene. Canvases that should stay on the
    /// iPad carry <see cref="DeviceScreenCanvas"/>.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class ExternalDisplayManager : MonoBehaviour
    {
        private const string ModePrefKey = "OpenFootball.ExternalDisplayMode";
        private const int ExternalIndex = 1;
        private const string SimulateArg = "-ofSimulateExternalDisplay";
        private const float RescanInterval = 0.5f;
        private static readonly Color BarColor = Color.black;
        private static readonly Color DeviceBackground = new Color(0.04f, 0.05f, 0.08f);

        public static ExternalDisplayManager Instance { get; private set; }

        /// <summary>Raised with true when the game moves onto a monitor and false when it comes back to the iPad.</summary>
        public static event Action<bool> ExternalDisplayChanged;

        private readonly ExternalDisplayConfig _config = new ExternalDisplayConfig();
        private readonly Dictionary<Camera, CameraState> _routedCameras = new Dictionary<Camera, CameraState>();
        private readonly Dictionary<Canvas, int> _routedCanvases = new Dictionary<Canvas, int>();
        private readonly FrameTiming[] _timings = new FrameTiming[1];

        private ExternalDisplayMode _mode;
        private bool _active;
        private int _knownDisplayCount;
        private float _nextRescan;
        private int _previousTargetFrameRate;
        private int _frameRate;
        private PixelSize _nativeSize;
        private PixelSize _renderSize;
        private ViewportRect _viewport = ViewportRect.Full;
        private DynamicResolutionGovernor _governor;
        private Camera _barsCamera;
        private Camera _deviceCamera;
        private Camera _primaryGameCamera;
        private float _pitchLength = 50f;
        private float _pitchWidth = 30f;

        // Desktop review mode (-ofSimulateExternalDisplay 2560x1440): the window plays the monitor and the
        // iPad view is drawn as a picture-in-picture inset, so routing, HUD layout, aspect handling and the
        // companion view can be captured on a machine with one screen.
        private bool _simulated;
        private int _gameDisplay = ExternalIndex;

        private struct CameraState
        {
            public int TargetDisplay;
            public Rect Rect;
        }

        /// <summary>True while the game is rendering on an external monitor.</summary>
        public bool IsExternalActive => _active;

        /// <summary>Native pixel size of the connected monitor (0x0 when none).</summary>
        public PixelSize NativeSize => _nativeSize;

        /// <summary>Current render size on the monitor.</summary>
        public PixelSize RenderSize => _renderSize;

        /// <summary>Tunables (resolution floor/ceiling, frame rate, aspect limits).</summary>
        public ExternalDisplayConfig Config => _config;

        /// <summary>What the iPad shows while a monitor is connected. Persisted across launches.</summary>
        public ExternalDisplayMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                PlayerPrefs.SetInt(ModePrefKey, (int)value);
                if (_active) BuildDeviceScreen();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            // iPad only: on a Mac/PC an extra monitor is a normal desktop (DesktopDisplayManager handles it).
            if (Application.platform != RuntimePlatform.IPhonePlayer && !TryParseSimulation(out _, out _)) return;
            var go = new GameObject("[ExternalDisplayManager]");
            go.AddComponent<ExternalDisplayManager>();
        }

        /// <summary>Pitch size used to frame the iPad's tactical view (length along world X, width along Z).</summary>
        public void SetPitchSize(float length, float width)
        {
            if (length <= 0f || width <= 0f) return;
            _pitchLength = length;
            _pitchWidth = width;
            if (_deviceCamera != null && _mode == ExternalDisplayMode.Companion) FrameCompanionCamera();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _mode = (ExternalDisplayMode)Mathf.Clamp(PlayerPrefs.GetInt(ModePrefKey, 0), 0, 2);
            Display.onDisplaysUpdated += OnDisplaysUpdated;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _knownDisplayCount = Display.displays.Length;
            if (TryParseSimulation(out int simW, out int simH))
            {
                _simulated = true;
                _gameDisplay = 0;
                Screen.SetResolution(simW, simH, FullScreenMode.Windowed);
                _nativeSize = new PixelSize(simW, simH);
                Activate();
                return;
            }
            Evaluate();
        }

        private static bool TryParseSimulation(out int width, out int height)
        {
            width = 0;
            height = 0;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != SimulateArg) continue;
                var parts = args[i + 1].ToLowerInvariant().Split('x');
                return parts.Length == 2 && int.TryParse(parts[0], out width) && int.TryParse(parts[1], out height)
                       && width > 0 && height > 0;
            }
            return false;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Display.onDisplaysUpdated -= OnDisplaysUpdated;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_active) Deactivate();
            Instance = null;
        }

        private void OnDisplaysUpdated() => Evaluate();

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_active) RouteScene();
        }

        private void Update()
        {
            // onDisplaysUpdated is not raised on every platform/version for hot-plug; poll as a backstop.
            if (!_simulated && Display.displays.Length != _knownDisplayCount) Evaluate();
            if (!_active) return;

            if (Time.unscaledTime >= _nextRescan)
            {
                _nextRescan = Time.unscaledTime + RescanInterval;
                RouteScene();
            }

            if (_governor != null && _governor.AddFrame(FrameWorkSeconds()))
                ApplyRenderResolution(_governor.Scale);
        }

        private void LateUpdate()
        {
            if (!_active || _mode != ExternalDisplayMode.Mirror || _deviceCamera == null) return;
            if (_primaryGameCamera == null) _primaryGameCamera = FindPrimaryGameCamera();
            if (_primaryGameCamera == null) return;

            var src = _primaryGameCamera.transform;
            _deviceCamera.transform.SetPositionAndRotation(src.position, src.rotation);
            _deviceCamera.fieldOfView = _primaryGameCamera.fieldOfView;
        }

        private void Evaluate()
        {
            if (_simulated) return;
            _knownDisplayCount = Display.displays.Length;
            bool connected = _knownDisplayCount > ExternalIndex;
            if (connected && !_active) Activate();
            else if (!connected && _active) Deactivate();
        }

        private void Activate()
        {
            int w, h;
            if (_simulated)
            {
                w = _nativeSize.Width;
                h = _nativeSize.Height;
            }
            else
            {
                var external = Display.displays[ExternalIndex];
                external.Activate();
                ExternalDisplayNative.AttachWindow();

                ExternalDisplayNative.NativeSize(out w, out h);
                if (w <= 0 || h <= 0)
                {
                    w = external.systemWidth;
                    h = external.systemHeight;
                }
                _nativeSize = new PixelSize(w, h);
            }
            _viewport = ExternalDisplayPolicy.Viewport(_config, w, h);

            _previousTargetFrameRate = Application.targetFrameRate;
            _frameRate = ExternalDisplayPolicy.FrameRate(_config, ExternalDisplayNative.MaxFramesPerSecond);
            Application.targetFrameRate = _frameRate;
            FrameTimingManager.CaptureFrameTimings();

            _governor = new DynamicResolutionGovernor(
                _config,
                ExternalDisplayPolicy.StartScale(_config, h),
                ExternalDisplayPolicy.MinScale(_config, h),
                ExternalDisplayPolicy.MaxScale(_config, h),
                _frameRate);
            _active = true;
            ApplyRenderResolution(_governor.Scale);

            if (!_simulated)
            {
                var deviceSize = ExternalDisplayPolicy.DeviceScreenResolution(_config, Display.main.systemWidth, Display.main.systemHeight);
                Display.main.SetRenderingResolution(deviceSize.Width, deviceSize.Height);
            }

            CreateBarsCamera();
            RouteScene();
            BuildDeviceScreen();

            Debug.Log($"[ExternalDisplay] monitor connected{(_simulated ? " (simulated)" : "")}: native {_nativeSize}, rendering {_renderSize} @ {_frameRate} fps, " +
                      $"mode {_mode}, scene attached {ExternalDisplayNative.HasExternalScene}");
            ExternalDisplayChanged?.Invoke(true);
        }

        private void Deactivate()
        {
            _active = false;
            _governor = null;

            foreach (var pair in _routedCameras)
            {
                if (pair.Key == null) continue;
                pair.Key.targetDisplay = pair.Value.TargetDisplay;
                pair.Key.rect = pair.Value.Rect;
            }
            _routedCameras.Clear();

            foreach (var pair in _routedCanvases)
            {
                if (pair.Key != null) pair.Key.targetDisplay = pair.Value;
            }
            _routedCanvases.Clear();

            DestroyCamera(ref _deviceCamera);
            DestroyCamera(ref _barsCamera);
            _primaryGameCamera = null;

            if (!_simulated) Display.main.SetRenderingResolution(Display.main.systemWidth, Display.main.systemHeight);
            Application.targetFrameRate = _previousTargetFrameRate;
            _nativeSize = new PixelSize(0, 0);
            _renderSize = new PixelSize(0, 0);

            Debug.Log("[ExternalDisplay] monitor disconnected: back on the iPad screen");
            ExternalDisplayChanged?.Invoke(false);
        }

        private void ApplyRenderResolution(float scale)
        {
            var size = ExternalDisplayPolicy.RenderResolution(_config, _nativeSize.Width, _nativeSize.Height, scale);
            if (size.Width <= 0 || size.Equals(_renderSize)) return;
            if (_simulated)
            {
                _renderSize = size;
                return;
            }
            if (Display.displays.Length <= ExternalIndex) return;
            _renderSize = size;
            Display.displays[ExternalIndex].SetRenderingResolution(size.Width, size.Height);
        }

        /// <summary>
        /// Seconds of real work in the last frame (max of GPU and main-thread CPU) when frame timing stats
        /// are available, otherwise the frame delta. Work time shows headroom under the vsync cap.
        /// </summary>
        private float FrameWorkSeconds()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
            {
                double ms = Math.Max(_timings[0].gpuFrameTime, _timings[0].cpuMainThreadFrameTime);
                if (ms > 0.0) return (float)(ms / 1000.0);
            }
            return Time.unscaledDeltaTime;
        }

        /// <summary>Moves game cameras and overlay canvases created since the last pass onto the monitor.</summary>
        private void RouteScene()
        {
            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam == _deviceCamera || cam == _barsCamera || cam.targetTexture != null) continue;
                if (IsOverlayCamera(cam)) continue;
                if (!_routedCameras.ContainsKey(cam))
                {
                    if (cam.targetDisplay != 0) continue;
                    _routedCameras[cam] = new CameraState { TargetDisplay = cam.targetDisplay, Rect = cam.rect };
                }
                cam.targetDisplay = _gameDisplay;
                cam.rect = new Rect(_viewport.X, _viewport.Y, _viewport.Width, _viewport.Height);
            }

            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (canvas.GetComponent<DeviceScreenCanvas>() != null) continue;
                if (!_routedCanvases.ContainsKey(canvas))
                {
                    if (canvas.targetDisplay != 0) continue;
                    _routedCanvases[canvas] = canvas.targetDisplay;
                }
                canvas.targetDisplay = _gameDisplay;
            }

            PruneDestroyed();
            if (_primaryGameCamera == null) _primaryGameCamera = FindPrimaryGameCamera();
        }

        private void PruneDestroyed()
        {
            List<Camera> deadCams = null;
            foreach (var cam in _routedCameras.Keys)
                if (cam == null) (deadCams ??= new List<Camera>()).Add(cam);
            if (deadCams != null) foreach (var c in deadCams) _routedCameras.Remove(c);

            List<Canvas> deadCanvases = null;
            foreach (var canvas in _routedCanvases.Keys)
                if (canvas == null) (deadCanvases ??= new List<Canvas>()).Add(canvas);
            if (deadCanvases != null) foreach (var c in deadCanvases) _routedCanvases.Remove(c);
        }

        private Camera FindPrimaryGameCamera()
        {
            var main = Camera.main;
            if (main != null && main != _deviceCamera && main != _barsCamera) return main;
            Camera best = null;
            foreach (var cam in _routedCameras.Keys)
                if (cam != null && cam.isActiveAndEnabled && (best == null || cam.depth > best.depth)) best = cam;
            return best;
        }

        private static bool IsOverlayCamera(Camera cam)
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            return data != null && data.renderType == CameraRenderType.Overlay;
        }

        /// <summary>Clears the whole monitor to black so pillarbox / letterbox bars are clean.</summary>
        private void CreateBarsCamera()
        {
            if (_viewport.IsFull) return;
            _barsCamera = CreateCamera("[ExternalDisplay] Bars", _gameDisplay, -1000f);
            _barsCamera.clearFlags = CameraClearFlags.SolidColor;
            _barsCamera.backgroundColor = BarColor;
            _barsCamera.cullingMask = 0;
        }

        /// <summary>Builds what the iPad shows for the current <see cref="Mode"/>.</summary>
        private void BuildDeviceScreen()
        {
            DestroyCamera(ref _deviceCamera);
            float deviceDepth = _simulated ? 100f : -100f;
            _deviceCamera = CreateCamera("[ExternalDisplay] iPad " + _mode, 0, deviceDepth);

            switch (_mode)
            {
                case ExternalDisplayMode.Companion:
                    _deviceCamera.orthographic = true;
                    _deviceCamera.clearFlags = CameraClearFlags.SolidColor;
                    _deviceCamera.backgroundColor = DeviceBackground;
                    _deviceCamera.transform.SetPositionAndRotation(new Vector3(0f, 120f, 0f), Quaternion.Euler(90f, 0f, 0f));
                    _deviceCamera.nearClipPlane = 1f;
                    _deviceCamera.farClipPlane = 250f;
                    FrameCompanionCamera();
                    DisablePostProcessing(_deviceCamera);
                    break;

                case ExternalDisplayMode.Mirror:
                    if (_primaryGameCamera == null) _primaryGameCamera = FindPrimaryGameCamera();
                    if (_primaryGameCamera != null)
                    {
                        _deviceCamera.CopyFrom(_primaryGameCamera);
                        _deviceCamera.targetDisplay = 0;
                        _deviceCamera.depth = deviceDepth;
                    }
                    break;

                default:
                    _deviceCamera.clearFlags = CameraClearFlags.SolidColor;
                    _deviceCamera.backgroundColor = DeviceBackground;
                    _deviceCamera.cullingMask = 0;
                    DisablePostProcessing(_deviceCamera);
                    break;
            }

            _deviceCamera.rect = _simulated ? SimulatedDeviceInset() : new Rect(0f, 0f, 1f, 1f);
        }

        /// <summary>Bottom-right inset with the iPad's 4:3 landscape shape, a quarter of the window wide.</summary>
        private Rect SimulatedDeviceInset()
        {
            float w = 0.25f;
            float h = w * _nativeSize.Width / _nativeSize.Height * 3f / 4f;
            return new Rect(1f - w - 0.015f, 0.025f, w, h);
        }

        private void FrameCompanionCamera()
        {
            float aspect = !_simulated && Display.main.systemHeight > 0
                ? (float)Display.main.systemWidth / Display.main.systemHeight
                : 4f / 3f;
            _deviceCamera.orthographicSize = ExternalDisplayPolicy.CompanionOrthoSize(_pitchLength, _pitchWidth, aspect);
        }

        private Camera CreateCamera(string name, int display, float depth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var cam = go.AddComponent<Camera>();
            cam.targetDisplay = display;
            cam.depth = depth;
            cam.allowMSAA = false;
            cam.useOcclusionCulling = false;
            return cam;
        }

        private static void DisablePostProcessing(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
        }

        private static void DestroyCamera(ref Camera cam)
        {
            if (cam != null) Destroy(cam.gameObject);
            cam = null;
        }
    }
}
