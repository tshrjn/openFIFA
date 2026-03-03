using System.Collections;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Multi-angle replay camera system that spawns/positions replay cameras
    /// at key angles around an event, sequences through angles during replay
    /// playback, and controls Time.timeScale for slow-motion.
    /// Works in coordination with BroadcastDirector and the existing ReplaySystem.
    /// </summary>
    public class ReplayCameraRig : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private float _slowMoSpeedFirst = 0.25f;
        [SerializeField] private float _slowMoSpeedSecond = 0.5f;
        [SerializeField] private int _multiAngleCount = 3;
        [SerializeField] private float _replayDuration = 5f;
        [SerializeField] private float _overlayOpacity = 0.85f;

        [Header("Camera Positions (Offsets from event position)")]
        [SerializeField] private Vector3 _angle1Offset = new Vector3(5f, 3f, -8f);
        [SerializeField] private Vector3 _angle2Offset = new Vector3(-5f, 6f, -12f);
        [SerializeField] private Vector3 _angle3Offset = new Vector3(0f, 15f, -5f);

        [Header("Scene References")]
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private BroadcastDirector _broadcastDirector;

        private ReplaySequencer _sequencer;
        private ReplayCameraConfig _config;
        private Vector3 _eventPosition;
        private bool _isActive;
        private Coroutine _activeCoroutine;

        // Camera state preservation
        private Vector3 _savedCameraPosition;
        private Quaternion _savedCameraRotation;
        private float _savedCameraFov;
        private float _savedTimeScale;

        /// <summary>Whether the replay camera rig is currently active.</summary>
        public bool IsActive => _isActive;

        /// <summary>The underlying replay sequencer for testing.</summary>
        public ReplaySequencer Sequencer => _sequencer;

        private void Awake()
        {
            _config = new ReplayCameraConfig(
                slowMoSpeedFirst: _slowMoSpeedFirst,
                slowMoSpeedSecond: _slowMoSpeedSecond,
                multiAngleCount: _multiAngleCount,
                overlayOpacity: _overlayOpacity,
                replayDuration: _replayDuration);

            _sequencer = new ReplaySequencer(_config);
            _sequencer.OnReplayComplete += HandleReplayComplete;
            _sequencer.OnStepChanged += HandleStepChanged;
        }

        private void OnEnable()
        {
            if (_broadcastDirector != null)
            {
                _broadcastDirector.OnDirectorStateChanged += HandleDirectorStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_broadcastDirector != null)
            {
                _broadcastDirector.OnDirectorStateChanged -= HandleDirectorStateChanged;
            }

            // Ensure time scale is restored
            if (_isActive)
            {
                Time.timeScale = 1f;
                _isActive = false;
            }
        }

        /// <summary>
        /// Start the replay camera rig at the given event position.
        /// </summary>
        /// <param name="eventPosition">World position of the event being replayed.</param>
        public void StartReplay(Vector3 eventPosition)
        {
            if (_isActive) return;

            _eventPosition = eventPosition;
            _isActive = true;

            SaveCameraState();

            _sequencer.Start();
            ApplyCurrentStep();

            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _activeCoroutine = StartCoroutine(ReplayUpdateCoroutine());
        }

        /// <summary>
        /// Stop the replay and return to live camera.
        /// </summary>
        public void StopReplay()
        {
            if (!_isActive) return;

            _sequencer.Stop();
            RestoreCameraState();
            Time.timeScale = 1f;
            _isActive = false;

            if (_activeCoroutine != null)
            {
                StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }
        }

        /// <summary>
        /// Initialize for testing without Inspector values.
        /// </summary>
        public void Initialize(Camera mainCamera, ReplayCameraConfig config)
        {
            _mainCamera = mainCamera;
            _config = config;
            _sequencer = new ReplaySequencer(config);
            _sequencer.OnReplayComplete += HandleReplayComplete;
            _sequencer.OnStepChanged += HandleStepChanged;
        }

        private IEnumerator ReplayUpdateCoroutine()
        {
            while (_isActive && _sequencer.IsActive)
            {
                _sequencer.Update(Time.unscaledDeltaTime);
                yield return null;
            }

            if (_isActive)
            {
                StopReplay();
            }
        }

        private void HandleDirectorStateChanged(DirectorState oldState, DirectorState newState)
        {
            if (newState == DirectorState.Replay && !_isActive)
            {
                // Use ball position as event position if available
                var eventPos = _broadcastDirector != null
                    ? Vector3.zero // Will be overridden by caller
                    : Vector3.zero;
                // The director will call StartReplay explicitly
            }
            else if (oldState == DirectorState.Replay && _isActive)
            {
                StopReplay();
            }
        }

        private void HandleReplayComplete()
        {
            RestoreCameraState();
            Time.timeScale = 1f;
            _isActive = false;
        }

        private void HandleStepChanged(int newStepIndex)
        {
            ApplyCurrentStep();
        }

        private void ApplyCurrentStep()
        {
            if (_mainCamera == null) return;

            // Set time scale based on current playback speed
            float playbackSpeed = _sequencer.CurrentPlaybackSpeed;
            Time.timeScale = playbackSpeed;

            // Position camera at the offset for this step
            Vector3 offset = GetOffsetForStep(_sequencer.CurrentStepIndex);
            Vector3 targetPosition = _eventPosition + offset;

            _mainCamera.transform.position = targetPosition;
            _mainCamera.transform.LookAt(_eventPosition);
        }

        private Vector3 GetOffsetForStep(int stepIndex)
        {
            switch (stepIndex)
            {
                case 0: return _angle1Offset;
                case 1: return _angle2Offset;
                case 2: return _angle3Offset;
                default:
                    // Rotate around for additional angles
                    float angle = (stepIndex - 2) * 60f;
                    float rad = angle * Mathf.Deg2Rad;
                    return new Vector3(
                        Mathf.Cos(rad) * 10f,
                        8f,
                        Mathf.Sin(rad) * 10f);
            }
        }

        private void SaveCameraState()
        {
            if (_mainCamera == null) return;

            _savedCameraPosition = _mainCamera.transform.position;
            _savedCameraRotation = _mainCamera.transform.rotation;
            _savedCameraFov = _mainCamera.fieldOfView;
            _savedTimeScale = Time.timeScale;
        }

        private void RestoreCameraState()
        {
            if (_mainCamera == null) return;

            _mainCamera.transform.position = _savedCameraPosition;
            _mainCamera.transform.rotation = _savedCameraRotation;
            _mainCamera.fieldOfView = _savedCameraFov;
            Time.timeScale = _savedTimeScale;
        }
    }
}
