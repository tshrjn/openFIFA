using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Tunables for the offside presentation: timing, VAR-style lines, camera cut,
    /// assistant referee flag and lower-third banner.
    /// </summary>
    [CreateAssetMenu(fileName = "OffsidePresentationConfig", menuName = "OpenFifa/Config/Offside Presentation Config")]
    public class OffsidePresentationConfig : ScriptableObject
    {
        [Header("Timing (unscaled seconds from the whistle)")]
        [SerializeField] private float _flagRaiseAt = 0.05f;
        [SerializeField] private float _slowMotionStart = 0f;
        [SerializeField, Range(0f, 1f)] private float _slowMotionScale = 0.25f;
        [SerializeField] private float _freezeStart = 0.45f;
        [SerializeField] private float _cameraCutIn = 0.45f;
        [SerializeField] private float _linesShow = 0.55f;
        [SerializeField] private float _lineRevealDuration = 0.25f;
        [SerializeField] private float _linesHide = 2.0f;
        [SerializeField] private float _cameraCutOut = 2.05f;
        [SerializeField] private float _timeRestore = 2.05f;
        [SerializeField] private float _bannerShow = 0.3f;
        [SerializeField] private float _bannerHide = 2.5f;
        [SerializeField] private float _totalDuration = 2.5f;

        [Header("Offside lines")]
        [SerializeField] private Color _attackerLineColor = new Color(1f, 0.25f, 0.2f, 0.95f);
        [SerializeField] private Color _defenderLineColor = new Color(0.2f, 0.85f, 1f, 0.95f);
        [SerializeField] private Color _gapBandColor = new Color(1f, 0.25f, 0.2f, 0.18f);
        [SerializeField] private float _lineWidth = 0.09f;
        [Tooltip("Height above the grass (markings sit at ~0.065 m).")]
        [SerializeField] private float _lineHeight = 0.075f;
        [Tooltip("Optional material for the lines (vertex-coloured, transparent). Leave empty to build one at runtime.")]
        [SerializeField] private Material _lineMaterialOverride;

        [Header("Camera cut")]
        [SerializeField] private float _cameraHeight = 14f;
        [Tooltip("Distance outside the touchline.")]
        [SerializeField] private float _cameraSideOffset = 6f;
        [SerializeField] private float _cameraFieldOfView = 50f;

        [Header("Assistant referee flag")]
        [SerializeField] private Color _flagColor = new Color(1f, 0.85f, 0.1f);
        [SerializeField] private float _flagRaiseDuration = 0.18f;
        [Tooltip("Distance outside the touchline where the assistant referee stands.")]
        [SerializeField] private float _assistantSideOffset = 1.2f;

        [Header("Banner")]
        [SerializeField] private string _bannerTitle = "OFFSIDE";
        [SerializeField] private Color _bannerAccentColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField] private Color _bannerBackgroundColor = new Color(0.06f, 0.07f, 0.1f, 0.92f);
        [SerializeField] private float _bannerSlideDuration = 0.2f;

        [Header("Skip")]
        [Tooltip("Allow the player to skip the sequence (Enter / gamepad B or Start).")]
        [SerializeField] private bool _skippable = true;
        [Tooltip("Ignore skip input for this long so the button that caused the offence doesn't skip it.")]
        [SerializeField] private float _skipGraceTime = 0.25f;

        public Color AttackerLineColor => _attackerLineColor;
        public Color DefenderLineColor => _defenderLineColor;
        public Color GapBandColor => _gapBandColor;
        public float LineWidth => _lineWidth;
        public float LineHeight => _lineHeight;
        public Material LineMaterialOverride => _lineMaterialOverride;
        public float CameraHeight => _cameraHeight;
        public float CameraSideOffset => _cameraSideOffset;
        public float CameraFieldOfView => _cameraFieldOfView;
        public Color FlagColor => _flagColor;
        public float FlagRaiseDuration => _flagRaiseDuration;
        public float AssistantSideOffset => _assistantSideOffset;
        public string BannerTitle => _bannerTitle;
        public Color BannerAccentColor => _bannerAccentColor;
        public Color BannerBackgroundColor => _bannerBackgroundColor;
        public float BannerSlideDuration => _bannerSlideDuration;
        public bool Skippable => _skippable;
        public float SkipGraceTime => _skipGraceTime;

        public OffsidePresentationTimingData ToTimingData()
        {
            return new OffsidePresentationTimingData(
                flagRaiseAt: _flagRaiseAt,
                slowMotionStart: _slowMotionStart,
                freezeStart: _freezeStart,
                cameraCutIn: _cameraCutIn,
                linesShow: _linesShow,
                linesHide: _linesHide,
                cameraCutOut: _cameraCutOut,
                timeRestore: _timeRestore,
                bannerShow: _bannerShow,
                bannerHide: _bannerHide,
                totalDuration: _totalDuration,
                slowMotionScale: _slowMotionScale,
                lineRevealDuration: _lineRevealDuration);
        }

        /// <summary>Runtime defaults when no asset is assigned.</summary>
        public static OffsidePresentationConfig CreateDefault()
        {
            var cfg = CreateInstance<OffsidePresentationConfig>();
            cfg.name = "OffsidePresentationConfig (Runtime Default)";
            return cfg;
        }
    }
}
