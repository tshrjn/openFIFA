using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Plays the ~2.5 s offside sequence: whistle, assistant referee flag, slow motion into a
    /// freeze, hard cut to a high touchline angle with VAR-style lines on the grass, and a
    /// lower-third banner. Sequencing comes from OffsidePresentationTimeline (pure C#);
    /// this component only maps cues onto the visual pieces. Skippable.
    ///
    /// Driven by any IOffsideDecisionSource via the caller (see OffsideController).
    /// </summary>
    public class OffsidePresentation : MonoBehaviour
    {
        [SerializeField] private OffsidePresentationConfig _config;

        private AssistantRefereeFlag _flag;
        private OffsideLineOverlay _lines;
        private OffsideCameraCut _cameraCut;
        private OffsideBanner _banner;

        private OffsidePresentationTimeline _timeline;
        private OffsideDecision _decision;
        private OffsideBannerInfo _bannerInfo;
        private Action _onComplete;
        private float _restoreTimeScale = 1f;

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
        public OffsideBanner Banner => _banner;
        public OffsidePresentationConfig Config => _config;

        /// <summary>
        /// Build the presentation pieces. Safe to call once after AddComponent.
        /// </summary>
        public void Initialize(OffsidePresentationConfig config, float pitchHalfWidth,
            Camera targetCamera, BroadcastCameraController broadcastCamera)
        {
            if (config != null) _config = config;
            if (_config == null) _config = OffsidePresentationConfig.CreateDefault();

            var flagGo = new GameObject("AssistantReferee");
            flagGo.transform.SetParent(transform, false);
            _flag = flagGo.AddComponent<AssistantRefereeFlag>();
            _flag.Initialize(pitchHalfWidth, _config.AssistantSideOffset, _config.FlagColor, _config.FlagRaiseDuration);

            var linesGo = new GameObject("OffsideLines");
            linesGo.transform.SetParent(transform, false);
            _lines = linesGo.AddComponent<OffsideLineOverlay>();
            _lines.Initialize(pitchHalfWidth, _config);

            _cameraCut = gameObject.AddComponent<OffsideCameraCut>();
            _cameraCut.Initialize(targetCamera, broadcastCamera, pitchHalfWidth, _config);

            var bannerGo = new GameObject("OffsideBanner");
            bannerGo.transform.SetParent(transform, false);
            _banner = bannerGo.AddComponent<OffsideBanner>();
            _banner.Initialize(_config);
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

            OnOffsideCalled?.Invoke(decision);
            Handle(_timeline.Advance(0f));
            return true;
        }

        /// <summary>Jump to the end, restoring time scale, camera and UI.</summary>
        public void Skip()
        {
            if (!IsPlaying) return;
            if (_banner != null) _banner.HideImmediate();
            Handle(_timeline.Skip());
        }

        private void Update()
        {
            if (!IsPlaying) return;

            if (_config.Skippable && _timeline.Elapsed >= _config.SkipGraceTime && SkipPressed())
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
            float midX = (_decision.AttackerLineX + _decision.DefenderLineX) * 0.5f;
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
                    if (_banner != null)
                        _banner.Show(_bannerInfo.ShirtNumber, _bannerInfo.PlayerName,
                            _bannerInfo.TeamLabel, _bannerInfo.TeamColor);
                    break;
                case OffsideCue.FreezeStart:
                    Time.timeScale = 0f;
                    break;
                case OffsideCue.CameraCutIn:
                    if (_cameraCut != null) _cameraCut.CutTo(midX);
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
                    if (_banner != null) _banner.Hide();
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

        private void OnDisable()
        {
            // Never leave the game frozen or the camera hijacked.
            if (IsPlaying) Skip();
        }
    }
}
