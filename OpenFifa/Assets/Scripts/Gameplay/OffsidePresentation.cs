using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Plays the ~2.5 s offside sequence: whistle, assistant referee flag, slow motion into a
    /// freeze, hard cut to a high touchline angle with VAR-style lines on the grass, and a
    /// lower-third banner. Sequencing comes from OffsidePresentationTimeline (pure C#);
    /// this component only maps cues onto the visual pieces. Skippable.
    ///
    /// Driven by any IOffsideDecisionSource via the caller: OffsideController (4v4 scene, pitch length along X)
    /// or SinglePlayerMatchController (11v11 sim, length along Z; presentation only, the sim owns the rule).
    /// </summary>
    public class OffsidePresentation : MonoBehaviour
    {
        [SerializeField] private OffsidePresentationConfig _config;

        private AssistantRefereeFlag _flag;
        private OffsideLineOverlay _lines;
        private OffsideCameraCut _cameraCut;
        private OffsideBanner _banner;
        private IOffsideBannerView _bannerView;
        private OffsidePitchFrame _frame = OffsidePitchFrame.LengthAlongX(15f);

        private OffsidePresentationTimeline _timeline;
        private OffsideDecision _decision;
        private OffsideBannerInfo _bannerInfo;
        private Action _onComplete;
        private float _restoreTimeScale = 1f;
        private readonly OffsideSkipGate _skipGate = new OffsideSkipGate();

        /// <summary>Referee whistle for the offside call (hook audio here).</summary>
        public static event Action OnWhistle;

        /// <summary>Fired when an offside sequence starts.</summary>
        public static event Action<OffsideDecision> OnOffsideCalled;

        /// <summary>Fired for every presentation cue (for audio, commentary, haptics...).</summary>
        public event Action<OffsideCue> OnCue;

        /// <summary>Fired when the sequence ends (naturally or skipped).</summary>
        public event Action OnCompleted;

        public bool IsPlaying => _timeline != null && !_timeline.IsComplete;
        public AssistantRefereeFlag Flag => _flag;
        public OffsideLineOverlay Lines => _lines;
        public OffsideCameraCut CameraCut => _cameraCut;
        /// <summary>The built-in overlay banner (null when another view was supplied).</summary>
        public OffsideBanner Banner => _banner;
        /// <summary>Where the lower-third is drawn (the built-in banner unless replaced).</summary>
        public IOffsideBannerView BannerView { get => _bannerView; set => _bannerView = value; }
        public OffsidePresentationConfig Config => _config;
        public OffsidePitchFrame Frame => _frame;
        /// <summary>Elapsed unscaled seconds of the current sequence (0 when idle).</summary>
        public float Elapsed => _timeline != null ? _timeline.Elapsed : 0f;
        /// <summary>The current (or last) sequence's timeline; null before the first.</summary>
        public OffsidePresentationTimeline Timeline => _timeline;

        /// <summary>
        /// Skip on any button, key, click or tap (single player) instead of Enter / gamepad B or Start (4v4 default).
        /// </summary>
        public bool SkipOnAnyInput { get; set; }

        /// <summary>
        /// Build the presentation pieces for the 4v4 scene (pitch length along X, broadcast camera suspended
        /// during the cut, built-in overlay banner). Safe to call once after AddComponent.
        /// </summary>
        public void Initialize(OffsidePresentationConfig config, float pitchHalfWidth,
            Camera targetCamera, BroadcastCameraController broadcastCamera)
        {
            Initialize(config, OffsidePitchFrame.LengthAlongX(pitchHalfWidth), targetCamera, broadcastCamera,
                assistantOnFarSide: false, bannerView: null);
        }

        /// <summary>
        /// Build the presentation pieces for any pitch orientation.
        /// <paramref name="suspendCameraDriver"/>: a camera driver to disable during the cut (null = leave the driver
        /// running and override after it; see <see cref="OffsideCameraCut"/>).
        /// <paramref name="bannerView"/>: where the lower-third goes (null = build the built-in overlay banner).
        /// </summary>
        public void Initialize(OffsidePresentationConfig config, OffsidePitchFrame frame, Camera targetCamera,
            Behaviour suspendCameraDriver, bool assistantOnFarSide, IOffsideBannerView bannerView)
        {
            if (config != null) _config = config;
            if (_config == null) _config = OffsidePresentationConfig.CreateDefault();
            _frame = frame;

            var flagGo = new GameObject("AssistantReferee");
            flagGo.transform.SetParent(transform, false);
            _flag = flagGo.AddComponent<AssistantRefereeFlag>();
            _flag.Initialize(frame, _config.AssistantSideOffset, assistantOnFarSide, _config.FlagColor, _config.FlagRaiseDuration);

            var linesGo = new GameObject("OffsideLines");
            linesGo.transform.SetParent(transform, false);
            _lines = linesGo.AddComponent<OffsideLineOverlay>();
            _lines.Initialize(frame, _config);

            _cameraCut = gameObject.AddComponent<OffsideCameraCut>();
            _cameraCut.Initialize(targetCamera, suspendCameraDriver, frame, _config);

            if (bannerView != null)
            {
                _bannerView = bannerView;
            }
            else
            {
                var bannerGo = new GameObject("OffsideBanner");
                bannerGo.transform.SetParent(transform, false);
                _banner = bannerGo.AddComponent<OffsideBanner>();
                _banner.Initialize(_config);
                _bannerView = _banner;
            }
        }

        /// <summary>
        /// Start the sequence. <paramref name="onComplete"/> runs once it ends (after time and
        /// camera are restored). Ignored if a sequence is already playing.
        /// </summary>
        public bool Play(OffsideDecision decision, OffsideBannerInfo bannerInfo, Action onComplete)
        {
            if (decision == null || !decision.IsOffence || IsPlaying) return false;

            _decision = decision;
            _bannerInfo = bannerInfo;
            _onComplete = onComplete;
            _restoreTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            _timeline = new OffsidePresentationTimeline(_config != null ? _config.ToTimingData() : null);
            _skipGate.GraceSeconds = _config != null ? _config.SkipGraceTime : 0.25f;
            _skipGate.Begin(SkipControlsDown()); // anything already held is not a skip

            OnOffsideCalled?.Invoke(decision);
            Handle(_timeline.Advance(0f));
            return true;
        }

        /// <summary>Jump to the end, restoring time scale, camera and UI.</summary>
        public void Skip()
        {
            if (!IsPlaying) return;
            if (_bannerView != null) _bannerView.HideImmediate();
            Handle(_timeline.Skip());
        }

        private void Update()
        {
            if (!IsPlaying) return;

            if (_config.Skippable && (SkipOnAnyInput
                    ? _skipGate.Update(SkipControlsDown(), _timeline.Elapsed)
                    : _timeline.Elapsed >= _config.SkipGraceTime && SkipPressed()))
            {
                Skip();
                return;
            }

            Handle(_timeline.Advance(Time.unscaledDeltaTime));
            if (_lines != null && _lines.IsVisible) _lines.SetReveal(_timeline.GetLineReveal());
        }

        private void Handle(IReadOnlyList<OffsideCue> cues)
        {
            // Copy: handlers may re-enter the timeline (e.g. Skip from a listener).
            var fired = new List<OffsideCue>(cues);
            for (int i = 0; i < fired.Count; i++)
            {
                Apply(fired[i]);
                OnCue?.Invoke(fired[i]);
            }
        }

        private void Apply(OffsideCue cue)
        {
            // Line values are depths along the pitch length; the frame maps them to world X or Z.
            float midDepth = (_decision.AttackerLineX + _decision.DefenderLineX) * 0.5f;
            switch (cue)
            {
                case OffsideCue.Whistle:
                    OnWhistle?.Invoke();
                    break;
                case OffsideCue.FlagRaise:
                    if (_flag != null) _flag.RaiseFlag(_decision.DefenderLineX);
                    break;
                case OffsideCue.SlowMotionStart:
                    Time.timeScale = _timeline.Timing.SlowMotionScale * _restoreTimeScale;
                    break;
                case OffsideCue.BannerShow:
                    if (_bannerView != null) _bannerView.Show(_bannerInfo);
                    break;
                case OffsideCue.FreezeStart:
                    Time.timeScale = 0f;
                    break;
                case OffsideCue.CameraCutIn:
                    if (_cameraCut != null) _cameraCut.CutTo(midDepth);
                    break;
                case OffsideCue.LinesShow:
                    if (_lines != null) _lines.Show(_decision.AttackerLineX, _decision.DefenderLineX);
                    break;
                case OffsideCue.LinesHide:
                    if (_lines != null) _lines.Hide();
                    break;
                case OffsideCue.CameraCutOut:
                    if (_cameraCut != null) _cameraCut.Restore();
                    break;
                case OffsideCue.TimeRestore:
                    Time.timeScale = _restoreTimeScale;
                    break;
                case OffsideCue.BannerHide:
                    if (_bannerView != null) _bannerView.Hide();
                    if (_flag != null) _flag.LowerFlag();
                    break;
                case OffsideCue.Complete:
                    var callback = _onComplete;
                    _onComplete = null;
                    callback?.Invoke();
                    OnCompleted?.Invoke();
                    break;
            }
        }

        private static bool SkipPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                return true;

            var gamepad = Gamepad.current;
            return gamepad != null &&
                   (gamepad.buttonEast.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
        }

        /// <summary>
        /// Skip controls that are down this frame, one bit each (see <see cref="OffsideSkipGate"/>): every gamepad
        /// button (not the sticks), left/right mouse, any keyboard key, and a one-frame pulse for a touch in phase
        /// Began. The gate turns these into "new press" edges, so held input never skips.
        /// </summary>
        private static ulong SkipControlsDown()
        {
            ulong m = 0;
            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Bit(ref m, 0, gamepad.buttonSouth); Bit(ref m, 1, gamepad.buttonEast);
                Bit(ref m, 2, gamepad.buttonWest); Bit(ref m, 3, gamepad.buttonNorth);
                Bit(ref m, 4, gamepad.startButton); Bit(ref m, 5, gamepad.selectButton);
                Bit(ref m, 6, gamepad.leftShoulder); Bit(ref m, 7, gamepad.rightShoulder);
                Bit(ref m, 8, gamepad.leftTrigger); Bit(ref m, 9, gamepad.rightTrigger);
                Bit(ref m, 10, gamepad.leftStickButton); Bit(ref m, 11, gamepad.rightStickButton);
                Bit(ref m, 12, gamepad.dpad.up); Bit(ref m, 13, gamepad.dpad.down);
                Bit(ref m, 14, gamepad.dpad.left); Bit(ref m, 15, gamepad.dpad.right);
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Bit(ref m, 16, mouse.leftButton);
                Bit(ref m, 17, mouse.rightButton);
            }
            var keyboard = Keyboard.current;
            if (keyboard != null) Bit(ref m, 18, keyboard.anyKey);
            var touch = Touchscreen.current;
            if (touch != null)
            {
                var touches = touch.touches;
                for (int i = 0; i < touches.Count; i++)
                    if (touches[i].phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        m |= 1UL << 19;
                        break;
                    }
            }
            return m;
        }

        private static void Bit(ref ulong mask, int bit, ButtonControl button)
        {
            if (button != null && button.isPressed) mask |= 1UL << bit;
        }

        private void OnDisable()
        {
            // Never leave the game frozen or the camera hijacked.
            if (IsPlaying) Skip();
        }
    }
}
