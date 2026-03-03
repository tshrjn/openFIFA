using System;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Master broadcast camera director MonoBehaviour.
    /// Controls Cinemachine-style virtual camera priorities to achieve
    /// TV-style camera presentation with auto-cuts, replays, and celebrations.
    /// Delegates all logic to BroadcastDirectorLogic (pure C#).
    /// </summary>
    public class BroadcastDirector : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Transform _ballTransform;
        [SerializeField] private Camera _mainCamera;

        [Header("Virtual Camera Transforms")]
        [SerializeField] private Transform _wideCameraTransform;
        [SerializeField] private Transform _mediumCameraTransform;
        [SerializeField] private Transform _closeCameraTransform;
        [SerializeField] private Transform _behindGoalCameraTransform;
        [SerializeField] private Transform _highAngleCameraTransform;
        [SerializeField] private Transform _tacticalCameraTransform;
        [SerializeField] private Transform _celebrationCameraTransform;
        [SerializeField] private Transform _replayCameraTransform;

        [Header("Director Config")]
        [SerializeField] private float _baseCutFrequency = 5f;
        [SerializeField] private float _maxCutFrequency = 12f;
        [SerializeField] private float _momentumThreshold = 0.5f;
        [SerializeField] private float _momentumDecayRate = 0.15f;
        [SerializeField] private float _celebrationDuration = 3f;

        [Header("Auto-Cut Config")]
        [SerializeField] private float _minCutDuration = 3f;
        [SerializeField] private float _maxCutDuration = 12f;
        [SerializeField] private float _smoothBlendDuration = 0.8f;
        [SerializeField] private bool _eventTriggeredCutsEnabled = true;

        [Header("Replay Config")]
        [SerializeField] private float _slowMoSpeedFirst = 0.25f;
        [SerializeField] private float _slowMoSpeedSecond = 0.5f;
        [SerializeField] private int _multiAngleCount = 3;
        [SerializeField] private float _replayOverlayOpacity = 0.85f;
        [SerializeField] private float _replayDuration = 5f;

        [Header("Pitch Dimensions")]
        [SerializeField] private float _pitchLength = 50f;
        [SerializeField] private float _pitchWidth = 30f;
        [SerializeField] private float _goalAreaDepth = 4f;

        [Header("Blend Settings")]
        [SerializeField] private float _cameraBlendSpeed = 3f;

        private BroadcastDirectorLogic _logic;
        private Transform _activeCameraTarget;
        private float _blendProgress;
        private Vector3 _blendStartPos;
        private Quaternion _blendStartRot;
        private float _blendStartFov;

        /// <summary>The underlying pure C# director logic for testing.</summary>
        public BroadcastDirectorLogic Logic => _logic;

        /// <summary>Current director state.</summary>
        public DirectorState CurrentState => _logic != null ? _logic.State : DirectorState.Live;

        /// <summary>Current active camera angle.</summary>
        public CameraAngle CurrentAngle => _logic != null ? _logic.ActiveAngle : CameraAngle.Wide;

        /// <summary>Fired when the director state changes.</summary>
        public event Action<DirectorState, DirectorState> OnDirectorStateChanged;

        /// <summary>Fired when the camera angle changes.</summary>
        public event Action<CameraAngle> OnCameraAngleChanged;

        private void Awake()
        {
            InitializeLogic();
        }

        private void OnEnable()
        {
            GoalDetector.OnGoalScored += HandleGoalScored;

            if (_logic != null)
            {
                _logic.OnStateChanged += HandleLogicStateChanged;
                _logic.OnAngleChanged += HandleLogicAngleChanged;
            }
        }

        private void OnDisable()
        {
            GoalDetector.OnGoalScored -= HandleGoalScored;

            if (_logic != null)
            {
                _logic.OnStateChanged -= HandleLogicStateChanged;
                _logic.OnAngleChanged -= HandleLogicAngleChanged;
            }
        }

        private void LateUpdate()
        {
            if (_logic == null || _ballTransform == null) return;

            float ballX = _ballTransform.position.x;
            float ballZ = _ballTransform.position.z;

            _logic.Update(Time.unscaledDeltaTime, ballX, ballZ);

            UpdateCameraBlend();
        }

        /// <summary>
        /// Initialize the director logic with configured parameters.
        /// Can be called externally for test setup.
        /// </summary>
        public void InitializeLogic()
        {
            var directorConfig = new DirectorConfig(
                baseCutFrequency: _baseCutFrequency,
                maxCutFrequency: _maxCutFrequency,
                momentumThreshold: _momentumThreshold,
                momentumDecayRate: _momentumDecayRate);

            var autoCutConfig = new AutoCutConfig(
                minCutDuration: _minCutDuration,
                maxCutDuration: _maxCutDuration,
                eventTriggeredCutsEnabled: _eventTriggeredCutsEnabled,
                smoothBlendDuration: _smoothBlendDuration);

            var replayConfig = new ReplayCameraConfig(
                slowMoSpeedFirst: _slowMoSpeedFirst,
                slowMoSpeedSecond: _slowMoSpeedSecond,
                multiAngleCount: _multiAngleCount,
                overlayOpacity: _replayOverlayOpacity,
                replayDuration: _replayDuration);

            _logic = new BroadcastDirectorLogic(
                directorConfig: directorConfig,
                autoCutConfig: autoCutConfig,
                replayConfig: replayConfig,
                pitchLength: _pitchLength,
                pitchWidth: _pitchWidth,
                goalAreaDepth: _goalAreaDepth);

            _logic.SetCelebrationDuration(_celebrationDuration);
            _logic.OnStateChanged += HandleLogicStateChanged;
            _logic.OnAngleChanged += HandleLogicAngleChanged;
        }

        /// <summary>
        /// Initialize with explicit parameters for testing.
        /// </summary>
        public void Initialize(
            BroadcastDirectorLogic logic,
            Transform ballTransform,
            Camera mainCamera)
        {
            _logic = logic;
            _ballTransform = ballTransform;
            _mainCamera = mainCamera;

            _logic.OnStateChanged += HandleLogicStateChanged;
            _logic.OnAngleChanged += HandleLogicAngleChanged;
        }

        /// <summary>
        /// Notify the director of a game event.
        /// </summary>
        public void NotifyEvent(BroadcastGameEvent gameEvent)
        {
            _logic?.NotifyEvent(gameEvent);
        }

        /// <summary>
        /// Start a goal replay.
        /// </summary>
        public void StartReplay()
        {
            _logic?.StartReplay();
        }

        /// <summary>
        /// Return to live camera.
        /// </summary>
        public void ReturnToLive()
        {
            _logic?.ReturnToLive();
        }

        /// <summary>
        /// Start tactical overhead view.
        /// </summary>
        public void StartTacticalView(float duration = 5f)
        {
            _logic?.StartTacticalView(duration);
        }

        private void HandleGoalScored(TeamIdentifier team)
        {
            _logic?.NotifyEvent(BroadcastGameEvent.Goal);
        }

        private void HandleLogicStateChanged(DirectorState oldState, DirectorState newState)
        {
            // Control Time.timeScale for replay slow-motion
            if (newState == DirectorState.Replay)
            {
                // Time scale will be managed by ReplayCameraRig
            }
            else if (oldState == DirectorState.Replay)
            {
                Time.timeScale = 1f;
            }

            OnDirectorStateChanged?.Invoke(oldState, newState);
        }

        private void HandleLogicAngleChanged(CameraAngle newAngle)
        {
            var targetTransform = GetTransformForAngle(newAngle);
            if (targetTransform != null)
            {
                StartBlendToCamera(targetTransform, newAngle);
            }

            OnCameraAngleChanged?.Invoke(newAngle);
        }

        private void StartBlendToCamera(Transform target, CameraAngle angle)
        {
            _activeCameraTarget = target;
            _blendProgress = 0f;

            if (_mainCamera != null)
            {
                _blendStartPos = _mainCamera.transform.position;
                _blendStartRot = _mainCamera.transform.rotation;
                _blendStartFov = _mainCamera.fieldOfView;
            }
        }

        private void UpdateCameraBlend()
        {
            if (_activeCameraTarget == null || _mainCamera == null) return;

            _blendProgress += Time.unscaledDeltaTime * _cameraBlendSpeed;
            float t = _blendProgress > 1f ? 1f : SmoothStep(_blendProgress);

            _mainCamera.transform.position = Vector3.Lerp(_blendStartPos, _activeCameraTarget.position, t);
            _mainCamera.transform.rotation = Quaternion.Slerp(_blendStartRot, _activeCameraTarget.rotation, t);

            // Blend FOV based on preset
            var preset = CameraPresetFactory.GetDefaultPreset(_logic.ActiveAngle);
            _mainCamera.fieldOfView = Mathf.Lerp(_blendStartFov, preset.FieldOfView, t);
        }

        private Transform GetTransformForAngle(CameraAngle angle)
        {
            switch (angle)
            {
                case CameraAngle.Wide: return _wideCameraTransform;
                case CameraAngle.Medium: return _mediumCameraTransform;
                case CameraAngle.Close: return _closeCameraTransform;
                case CameraAngle.BehindGoal: return _behindGoalCameraTransform;
                case CameraAngle.HighAngle: return _highAngleCameraTransform;
                case CameraAngle.Tactical: return _tacticalCameraTransform;
                case CameraAngle.Celebration: return _celebrationCameraTransform;
                case CameraAngle.Replay: return _replayCameraTransform;
                default: return _wideCameraTransform;
            }
        }

        private static float SmoothStep(float t)
        {
            // Hermite smoothstep
            return t * t * (3f - 2f * t);
        }
    }
}
