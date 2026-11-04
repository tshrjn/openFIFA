using System;
using System.Collections.Generic;
using OpenFifa.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Monitor handling for the macOS (and Windows) build, e.g. a MacBook driving an external monitor.
    ///
    /// Runs full screen (borderless) at the chosen monitor's native resolution, Retina-aware. With the
    /// default "Auto" preference the game moves to the largest connected display, so plugging a 1440p/4K
    /// monitor into a MacBook moves the game onto it and unplugging brings it back to the built-in screen.
    /// A specific monitor can be pinned with <see cref="SelectDisplay"/>. Render cost is held at 60 fps by
    /// stepping URP's render scale between quantized 1440p/1800p/2160p tiers (FSR upscaled), so a 4K/5K panel
    /// stays sharp and smooth. Frame pacing stays with <see cref="FramePacer"/> (vSync on desktop).
    ///
    /// Bootstraps itself in macOS/Windows players. The iPad uses <see cref="ExternalDisplayManager"/> instead.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class DesktopDisplayManager : MonoBehaviour
    {
        private const string PreferencePrefKey = "OpenFootball.PreferredDisplay";
        private const string PreferenceOccurrencePrefKey = "OpenFootball.PreferredDisplayOccurrence";
        private const string FullScreenPrefKey = "OpenFootball.FullScreen";
        private const float PollInterval = 1f;

        public static DesktopDisplayManager Instance { get; private set; }

        /// <summary>Raised after the game has moved to another display (argument: display name).</summary>
        public static event Action<string> DisplayChanged;

        private readonly ExternalDisplayConfig _config = new ExternalDisplayConfig();
        private readonly List<DisplayInfo> _layout = new List<DisplayInfo>();
        private readonly List<DisplayCandidate> _candidates = new List<DisplayCandidate>();
        private readonly FrameTiming[] _timings = new FrameTiming[1];

        private readonly Dictionary<UniversalRenderPipelineAsset, (float scale, UpscalingFilterSelection filter)> _originals =
            new Dictionary<UniversalRenderPipelineAsset, (float, UpscalingFilterSelection)>();

        private string _preferredName;
        private int _preferredOccurrence;
        private bool _fullScreen;
        private string _currentName;
        private int _currentWidth;
        private int _currentHeight;
        private int _layoutSignature;
        private float _nextPoll;
        private bool _moving;
        private DynamicResolutionGovernor _governor;

        /// <summary>Connected displays, in OS order (index 0 is the main / built-in display).</summary>
        public IReadOnlyList<DisplayCandidate> Displays => _candidates;

        /// <summary>Name of the display the game is on.</summary>
        public string CurrentDisplayName => _currentName;

        /// <summary>Pinned display name, or empty for Auto (largest display).</summary>
        public string PreferredDisplayName => _preferredName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (Application.platform != RuntimePlatform.OSXPlayer && Application.platform != RuntimePlatform.WindowsPlayer) return;
            if (HasArg("-ofCapture") || HasArg("-ofSimulateExternalDisplay")) return;
            new GameObject("[DesktopDisplayManager]").AddComponent<DesktopDisplayManager>();
        }

        /// <summary>Pins the game to a display (index into <see cref="Displays"/>), or -1 for Auto.</summary>
        public void SelectDisplay(int index)
        {
            bool pinned = index >= 0 && index < _candidates.Count;
            _preferredName = pinned ? _candidates[index].Name : DesktopDisplayPolicy.AutoPreference;
            _preferredOccurrence = pinned ? DesktopDisplayPolicy.Occurrence(_candidates, index) : 0;
            PlayerPrefs.SetString(PreferencePrefKey, _preferredName);
            PlayerPrefs.SetInt(PreferenceOccurrencePrefKey, _preferredOccurrence);
            PlaceWindow(force: true);
        }

        /// <summary>Switches between borderless full screen (default) and a window.</summary>
        public void SetFullScreen(bool fullScreen)
        {
            _fullScreen = fullScreen;
            PlayerPrefs.SetInt(FullScreenPrefKey, fullScreen ? 1 : 0);
            PlaceWindow(force: true);
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

            _preferredName = PlayerPrefs.GetString(PreferencePrefKey, DesktopDisplayPolicy.AutoPreference);
            _preferredOccurrence = PlayerPrefs.GetInt(PreferenceOccurrencePrefKey, 0);
            _fullScreen = PlayerPrefs.GetInt(FullScreenPrefKey, 1) == 1;

            RefreshLayout();
            PlaceWindow(force: true);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            RestoreRenderScale();
            Instance = null;
        }

        private void OnApplicationQuit() => RestoreRenderScale();

        private void Update()
        {
            if (Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + PollInterval;
                if (RefreshLayout()) PlaceWindow(force: false);
            }

            if (_governor != null && !_moving && _governor.AddFrame(FrameWorkSeconds()))
                ApplyRenderScale();
        }

        /// <summary>Re-reads the display layout. Returns true when monitors were added, removed or changed.</summary>
        private bool RefreshLayout()
        {
            _layout.Clear();
            Screen.GetDisplayLayout(_layout);
            _candidates.Clear();
            int signature = 17;
            foreach (var d in _layout)
            {
                _candidates.Add(new DisplayCandidate(d.name, d.width, d.height));
                signature = signature * 31 + (d.name ?? string.Empty).GetHashCode();
                signature = signature * 31 + d.width * 7 + d.height;
            }
            bool changed = signature != _layoutSignature;
            _layoutSignature = signature;
            return changed;
        }

        private void PlaceWindow(bool force)
        {
            int index = DesktopDisplayPolicy.ChooseDisplay(_candidates, _preferredName, _preferredOccurrence, MinExternalHeight);
            if (index < 0) return;
            var target = _layout[index];
            var current = Screen.mainWindowDisplayInfo;

            bool sameDisplay = current.name == target.name && current.width == target.width && current.height == target.height;
            if (!sameDisplay)
            {
                _moving = true;
                // Window position is relative to the target display's work area; centre it there.
                var position = new Vector2Int(Math.Max(0, (target.width - Screen.width) / 2), Math.Max(0, (target.height - Screen.height) / 2));
                var op = Screen.MoveMainWindowTo(target, position);
                op.completed += _ => OnMoved(target);
                return;
            }
            if (force || current.width != _currentWidth || current.height != _currentHeight)
                OnMoved(target);
        }

        private void OnMoved(DisplayInfo display)
        {
            _moving = false;
            _currentName = display.name;
            _currentWidth = display.width;
            _currentHeight = display.height;

            if (_fullScreen)
                Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow);
            else
                Screen.fullScreenMode = FullScreenMode.Windowed;

            // Budget only: FramePacer keeps vSync pacing on desktop, so frames come at the display's refresh.
            int frameRate = ExternalDisplayPolicy.FrameRate(_config, (int)Math.Round(display.refreshRate.value));
            _governor = new DynamicResolutionGovernor(_config, display.height, frameRate);
            float scale = ApplyRenderScale();

            Debug.Log($"[DesktopDisplay] on '{display.name}' {display.width}x{display.height}, budget {frameRate} fps, " +
                      $"render {_governor.RenderHeight}p (scale {scale:0.00}), full screen {_fullScreen}");
            DisplayChanged?.Invoke(display.name);
        }

        /// <summary>
        /// An external display at least this tall is preferred over the built-in panel. 1080 rather than 1440 because
        /// macOS may report a HiDPI monitor in points (a 4K panel set to "looks like 1080p"); still to be verified
        /// on a real Mac.
        /// </summary>
        private int MinExternalHeight => _config.EmergencyHeight;

        /// <summary>
        /// Applies the governor's render height as URP render scale to the active pipeline asset (looked up each
        /// time, so a quality-level change is picked up). Returns the scale.
        /// </summary>
        private float ApplyRenderScale()
        {
            float scale = _currentHeight > 0 ? Mathf.Clamp01((float)_governor.RenderHeight / _currentHeight) : 1f;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return scale;
            if (!_originals.ContainsKey(urp)) _originals[urp] = (urp.renderScale, urp.upscalingFilter);
            urp.renderScale = scale;
            urp.upscalingFilter = scale < 0.999f ? UpscalingFilterSelection.FSR : _originals[urp].filter;
            return scale;
        }

        private void RestoreRenderScale()
        {
            foreach (var pair in _originals)
            {
                if (pair.Key == null) continue;
                pair.Key.renderScale = pair.Value.scale;
                pair.Key.upscalingFilter = pair.Value.filter;
            }
            _originals.Clear();
        }

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

        private static bool HasArg(string arg)
        {
            foreach (var a in Environment.GetCommandLineArgs())
                if (a == arg) return true;
            return false;
        }
    }
}
