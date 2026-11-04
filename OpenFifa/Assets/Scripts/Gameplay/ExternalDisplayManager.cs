using System;
using System.Collections.Generic;
using OpenFifa.Core;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Drives an external monitor connected to the iPad.
    ///
    /// When a monitor connects, the game takes it over instead of being mirrored: the full-screen game
    /// cameras and display-only canvases (HUD, scoreboard) move to the monitor, which renders at 1440p or
    /// higher (quantized tiers up to 4K, chosen from GPU/CPU frame time and thermal state) at 60 fps through
    /// <see cref="FramePacer"/>, pillarboxed on super-ultrawides and letterboxed on 4:3 screens. Canvases
    /// people touch (anything with a Selectable or on-screen control, or tagged <see cref="DeviceScreenCanvas"/>)
    /// stay on the iPad, which shows <see cref="ExternalDisplayMode"/>. Unplugging puts everything back.
    ///
    /// Bootstraps itself on iPad; there is nothing to add to a scene.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class ExternalDisplayManager : MonoBehaviour
    {
        private const string ModePrefKey = "OpenFootball.ExternalDisplayMode";
        private const string SimulateArg = "-ofSimulateExternalDisplay";
        private const int ExternalIndex = 1;
        private const float PollInterval = 2f;
        private const int MaxAttachAttempts = 180;
        private static readonly Color BarColor = Color.black;
        private static readonly Color DeviceBackground = new Color(0.04f, 0.05f, 0.08f);

        public static ExternalDisplayManager Instance { get; private set; }

        /// <summary>Raised with true when the game moves onto a monitor and false when it comes back to the iPad.</summary>
        public static event Action<bool> ExternalDisplayChanged;

        /// <summary>Raised when a canvas is moved to another display (args: canvas, new target display), e.g. to redo safe-area layout.</summary>
        public static event Action<Canvas, int> CanvasRouted;

        /// <summary>Test seam: overrides Display.displays.Length and skips native display calls.</summary>
        internal static Func<int> DisplayCountOverride;

        /// <summary>Test seam: native size reported for the fake monitor.</summary>
        internal static PixelSize FakeNativeSize = new PixelSize(2560, 1440);

        private readonly ExternalDisplayConfig _config = new ExternalDisplayConfig();
        private readonly Dictionary<Camera, CameraState> _routedCameras = new Dictionary<Camera, CameraState>();
        private readonly Dictionary<Canvas, int> _routedCanvases = new Dictionary<Canvas, int>();
        private readonly HashSet<Canvas> _deviceCanvases = new HashSet<Canvas>();
        private readonly List<Camera> _cameraKeys = new List<Camera>();
        private readonly List<Canvas> _canvasKeys = new List<Canvas>();
        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private Camera[] _cameraBuffer = new Camera[16];

        private ExternalDisplayMode _mode;
        private bool _active;
        private bool _simulated;
        private int _gameDisplay = ExternalIndex;
        private int _knownDisplayCount;
        private int _lastCameraCount = -1;
        private float _nextPoll;
        private float _delayedRouteAt = -1f;
        private int _attachAttemptsLeft;
        private bool _attachFailed;
        private int _frameRate;
        private DeviceThermalLevel _thermal;
        private PixelSize _nativeSize;
        private PixelSize _renderSize;
        private ViewportRect _viewport = ViewportRect.Full;
        private DynamicResolutionGovernor _governor;
        private Camera _barsCamera;
        private Camera _deviceCamera;
        private Camera _primaryGameCamera;
        private float _pitchLength = 50f;
        private float _pitchWidth = 30f;

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

        /// <summary>Tunables (resolution tiers, frame rate, aspect limits).</summary>
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

        private bool UsesNativeDisplays => !_simulated && DisplayCountOverride == null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            // iPad only: on a Mac/PC an extra monitor is a normal desktop (DesktopDisplayManager handles it).
            if (Application.platform != RuntimePlatform.IPhonePlayer && !TryParseSimulation(out _, out _)) return;
            new GameObject("[ExternalDisplayManager]").AddComponent<ExternalDisplayManager>();
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

            // Default: the iPad shows only its own UI, so all GPU time goes to the monitor.
            _mode = (ExternalDisplayMode)Mathf.Clamp(PlayerPrefs.GetInt(ModePrefKey, (int)ExternalDisplayMode.ControllerOnly), 0, 2);
            // Review/QA override: -ofExternalDisplayMode companion|mirror|controlleronly (not persisted).
            var modeArg = ArgValue("-ofExternalDisplayMode");
            if (modeArg != null && Enum.TryParse(modeArg, true, out ExternalDisplayMode argMode)) _mode = argMode;
            Display.onDisplaysUpdated += OnDisplaysUpdated;
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (DisplayCountOverride == null && TryParseSimulation(out int simW, out int simH))
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

        private void OnDestroy()
        {
            if (Instance != this) return;
            Display.onDisplaysUpdated -= OnDisplaysUpdated;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_active) Deactivate();
            Instance = null;
        }

        private static string ArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
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

        private int DisplayCount => DisplayCountOverride != null ? DisplayCountOverride() : Display.displays.Length;

        private void OnDisplaysUpdated() => Evaluate();

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_active) return;
            RouteScene();
            // Match setup builds cameras and the HUD in Start(); catch them shortly after.
            _delayedRouteAt = Time.unscaledTime + 1f;
        }

        private void Update()
        {
            // Display.displays is a cached array: this compare is a field read, no native call or allocation.
            if (!_simulated && DisplayCount != _knownDisplayCount) Evaluate();
            if (!_active)
            {
                // Monitor present but no external scene yet (it can connect a moment after the screen): keep checking.
                if (!_simulated && _knownDisplayCount > ExternalIndex && !_attachFailed && Time.unscaledTime >= _nextPoll)
                {
                    _nextPoll = Time.unscaledTime + PollInterval;
                    Evaluate();
                }
                return;
            }

            if (_attachAttemptsLeft > 0 && UsesNativeDisplays)
            {
                if (ExternalDisplayNative.IsAttached || ExternalDisplayNative.AttachWindow())
                {
                    _attachAttemptsLeft = 0;
                }
                else if (--_attachAttemptsLeft == 0)
                {
                    // Never reached the external scene (mirroring, Stage Manager, or a shell without the role):
                    // put the game back on the iPad rather than rendering to a display nobody sees.
                    Debug.LogWarning("[ExternalDisplay] Unity's external window never reached the external-display scene " +
                                     $"(scene present: {ExternalDisplayNative.HasExternalScene}); staying on the iPad screen");
                    _attachFailed = true;
                    Deactivate();
                    return;
                }
            }

            if (_delayedRouteAt > 0f && Time.unscaledTime >= _delayedRouteAt)
            {
                _delayedRouteAt = -1f;
                RouteScene();
            }

            if (Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + PollInterval;

                // The monitor can go away without UIKit posting a screen-disconnect (e.g. when we registered it
                // through the scene). The external scene disappearing is the reliable signal.
                if (UsesNativeDisplays && ExternalDisplayNative.IsSupported && !ExternalDisplayNative.HasExternalScene)
                {
                    Deactivate();
                    return;
                }

                // New full-screen cameras are cheap to detect; canvases are rerouted on scene load, connect and
                // RequestRoute(), and existing ones are re-checked for touchability below without a scene search.
                if (Camera.allCamerasCount != _lastCameraCount) RouteScene();
                else RecheckKnownCanvases();

                var thermal = (DeviceThermalLevel)Mathf.Clamp(ExternalDisplayNative.ThermalState, 0, 3);
                if (thermal != _thermal)
                {
                    _thermal = thermal;
                    if (_governor.SetThermal(thermal)) ApplyRenderResolution();
                }
            }

            if (_governor.AddFrame(FrameWorkSeconds())) ApplyRenderResolution();
        }

        /// <summary>
        /// Ask for a routing pass now, e.g. right after building a HUD or menu canvas while a monitor is connected.
        /// </summary>
        public void RequestRoute()
        {
            if (_active) RouteScene();
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
            int count = DisplayCount;
            if (count != _knownDisplayCount) _attachFailed = false; // a new plug event gets a fresh attempt
            _knownDisplayCount = count;
            bool connected = count > ExternalIndex;
            // On iPad, only take over when iPadOS handed us an external-display scene. Without one the system is
            // mirroring or the monitor is a Stage Manager desktop, where the game window lives wherever the user puts it.
            if (connected && UsesNativeDisplays && ExternalDisplayNative.IsSupported && !ExternalDisplayNative.HasExternalScene)
                connected = false;
            if (connected && !_active && !_attachFailed) Activate();
            else if (!connected && _active) Deactivate();
        }

        private void Activate()
        {
            if (UsesNativeDisplays)
            {
                var external = Display.displays[ExternalIndex];
                external.Activate();
                _attachAttemptsLeft = ExternalDisplayNative.AttachWindow() ? 0 : MaxAttachAttempts;

                ExternalDisplayNative.NativeSize(out int w, out int h);
                if (w <= 0 || h <= 0)
                {
                    w = external.systemWidth;
                    h = external.systemHeight;
                }
                _nativeSize = new PixelSize(w, h);
            }
            else if (!_simulated)
            {
                _nativeSize = FakeNativeSize;
            }

            _viewport = ExternalDisplayPolicy.Viewport(_config, _nativeSize.Width, _nativeSize.Height);
            _frameRate = ExternalDisplayPolicy.FrameRate(_config, ExternalDisplayNative.MaxFramesPerSecond);
            FramePacer.SetOverride(_frameRate);
            FrameTimingManager.CaptureFrameTimings();

            _thermal = (DeviceThermalLevel)Mathf.Clamp(ExternalDisplayNative.ThermalState, 0, 3);
            _governor = new DynamicResolutionGovernor(_config, _nativeSize.Height, _frameRate);
            _governor.SetThermal(_thermal);
            _renderSize = new PixelSize(0, 0);
            _active = true;
            ApplyRenderResolution();

            if (UsesNativeDisplays)
            {
                var deviceSize = ExternalDisplayPolicy.DeviceScreenResolution(_config, Display.main.systemWidth, Display.main.systemHeight);
                Display.main.SetRenderingResolution(deviceSize.Width, deviceSize.Height);
            }

            CreateBarsCamera();
            RouteScene();
            BuildDeviceScreen();

            Debug.Log($"[ExternalDisplay] monitor connected{(_simulated ? " (simulated)" : "")}: native {_nativeSize}, " +
                      $"rendering {_renderSize} (tiers {string.Join("/", _governor.Tiers)}) @ {_frameRate} fps, mode {_mode}, " +
                      $"scene attached {ExternalDisplayNative.HasExternalScene}");
            ExternalDisplayChanged?.Invoke(true);
        }

        private void Deactivate()
        {
            _active = false;
            _governor = null;
            _attachAttemptsLeft = 0;
            _delayedRouteAt = -1f;

            foreach (var pair in _routedCameras)
            {
                if (pair.Key == null) continue;
                pair.Key.targetDisplay = pair.Value.TargetDisplay;
                pair.Key.rect = pair.Value.Rect;
            }
            _routedCameras.Clear();

            foreach (var pair in _routedCanvases)
            {
                if (pair.Key != null) MoveCanvas(pair.Key, pair.Value);
            }
            _routedCanvases.Clear();
            _deviceCanvases.Clear();

            DestroyCamera(ref _deviceCamera);
            DestroyCamera(ref _barsCamera);
            _primaryGameCamera = null;

            if (UsesNativeDisplays) Display.main.SetRenderingResolution(Display.main.systemWidth, Display.main.systemHeight);
            FramePacer.ClearOverride();
            _nativeSize = new PixelSize(0, 0);
            _renderSize = new PixelSize(0, 0);

            Debug.Log("[ExternalDisplay] monitor disconnected: back on the iPad screen");
            ExternalDisplayChanged?.Invoke(false);
        }

        /// <summary>Applies the governor's current tier. Only reallocates when the size actually changed.</summary>
        private void ApplyRenderResolution()
        {
            var size = ExternalDisplayPolicy.RenderResolution(_nativeSize.Width, _nativeSize.Height, _governor.RenderHeight);
            if (size.Width <= 0 || size.Equals(_renderSize)) return;
            _renderSize = size;
            if (UsesNativeDisplays && Display.displays.Length > ExternalIndex)
                Display.displays[ExternalIndex].SetRenderingResolution(size.Width, size.Height);
            Debug.Log($"[ExternalDisplay] render {size} (thermal {_thermal})");
        }

        /// <summary>
        /// Seconds of real work in the last frame (max of GPU and main-thread CPU, covering both displays)
        /// when frame timing stats are available, otherwise the frame delta. Work time shows headroom under
        /// the vsync cap.
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

        /// <summary>
        /// Moves full-screen game cameras and display-only overlay canvases onto the monitor. Cameras with
        /// a partial viewport (minimap, split screen) and touchable canvases stay where they are.
        /// </summary>
        private void RouteScene()
        {
            int count = Camera.allCamerasCount;
            if (_cameraBuffer.Length < count) _cameraBuffer = new Camera[count * 2];
            count = Camera.GetAllCameras(_cameraBuffer);
            for (int i = 0; i < count; i++)
            {
                var cam = _cameraBuffer[i];
                _cameraBuffer[i] = null;
                if (cam == _deviceCamera || cam == _barsCamera || cam.targetTexture != null) continue;
                if (_routedCameras.ContainsKey(cam) || cam.targetDisplay != 0 || IsOverlayCamera(cam) || !IsFullRect(cam.rect)) continue;
                _routedCameras[cam] = new CameraState { TargetDisplay = cam.targetDisplay, Rect = cam.rect };
                cam.targetDisplay = _gameDisplay;
                cam.rect = new Rect(_viewport.X, _viewport.Y, _viewport.Width, _viewport.Height);
            }

            _lastCameraCount = Camera.allCamerasCount;

            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (_routedCanvases.ContainsKey(canvas) || _deviceCanvases.Contains(canvas)) continue;
                if (canvas.targetDisplay != 0) continue;
                if (StaysOnDevice(canvas))
                {
                    _deviceCanvases.Add(canvas);
                    continue;
                }
                _routedCanvases[canvas] = canvas.targetDisplay;
                MoveCanvas(canvas, _gameDisplay);
            }

            PruneDestroyed();
            RecheckKnownCanvases();
            if (_primaryGameCamera == null) _primaryGameCamera = FindPrimaryGameCamera();
        }

        /// <summary>
        /// Touchability can change after a canvas was placed (a HUD gains a button, a menu loses its buttons), so
        /// known canvases are re-checked on every routing pass and poll without searching the scene.
        /// </summary>
        private void RecheckKnownCanvases()
        {
            _canvasKeys.Clear();
            foreach (var canvas in _routedCanvases.Keys)
                if (canvas != null && StaysOnDevice(canvas)) _canvasKeys.Add(canvas);
            foreach (var canvas in _canvasKeys)
            {
                MoveCanvas(canvas, _routedCanvases[canvas]);
                _routedCanvases.Remove(canvas);
                _deviceCanvases.Add(canvas);
            }

            _canvasKeys.Clear();
            foreach (var canvas in _deviceCanvases)
                if (canvas != null && !StaysOnDevice(canvas)) _canvasKeys.Add(canvas);
            foreach (var canvas in _canvasKeys)
            {
                _deviceCanvases.Remove(canvas);
                _routedCanvases[canvas] = canvas.targetDisplay;
                MoveCanvas(canvas, _gameDisplay);
            }
        }

        private static void MoveCanvas(Canvas canvas, int display)
        {
            if (canvas.targetDisplay == display) return;
            canvas.targetDisplay = display;
            CanvasRouted?.Invoke(canvas, display);
        }

        /// <summary>
        /// Canvases people touch stay on the iPad: GraphicRaycaster only takes touches from display 0, so
        /// moving them would make buttons, menus and on-screen sticks untappable.
        /// </summary>
        internal static bool StaysOnDevice(Canvas canvas) =>
            canvas.GetComponent<DeviceScreenCanvas>() != null ||
            canvas.GetComponentInChildren<Selectable>(true) != null ||
            canvas.GetComponentInChildren<OnScreenControl>(true) != null;

        private static bool IsFullRect(Rect r) =>
            r.x <= 0.001f && r.y <= 0.001f && r.width >= 0.999f && r.height >= 0.999f;

        private void PruneDestroyed()
        {
            _cameraKeys.Clear();
            foreach (var cam in _routedCameras.Keys)
                if (cam == null) _cameraKeys.Add(cam);
            foreach (var c in _cameraKeys) _routedCameras.Remove(c);

            _canvasKeys.Clear();
            foreach (var canvas in _routedCanvases.Keys)
                if (canvas == null) _canvasKeys.Add(canvas);
            foreach (var c in _canvasKeys) _routedCanvases.Remove(c);
            _deviceCanvases.RemoveWhere(c => c == null);
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
            DisablePostProcessing(_barsCamera);
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
                        CopyUrpCameraData(_primaryGameCamera, _deviceCamera);
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

        /// <summary>
        /// Camera.CopyFrom skips URP's per-camera data. Copy what a second base camera can share; the overlay
        /// camera stack is not copied (an overlay camera can belong to one stack only).
        /// </summary>
        private static void CopyUrpCameraData(Camera from, Camera to)
        {
            var src = from.GetComponent<UniversalAdditionalCameraData>();
            if (src == null) return;
            var dst = to.GetUniversalAdditionalCameraData();
            dst.renderPostProcessing = src.renderPostProcessing;
            dst.antialiasing = src.antialiasing;
            dst.antialiasingQuality = src.antialiasingQuality;
            dst.renderShadows = src.renderShadows;
            dst.volumeLayerMask = src.volumeLayerMask;
            dst.volumeTrigger = src.volumeTrigger;
            dst.stopNaN = src.stopNaN;
            dst.dithering = src.dithering;
            dst.requiresColorOption = src.requiresColorOption;
            dst.requiresDepthOption = src.requiresDepthOption;
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
            float aspect = UsesNativeDisplays && Display.main.systemHeight > 0
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
