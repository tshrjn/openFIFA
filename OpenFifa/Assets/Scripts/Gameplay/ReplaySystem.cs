using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Replay system that continuously records object transforms and
    /// plays back the last 5 seconds on goal scored at 0.5x speed.
    /// Uses pre-allocated ring buffer for zero GC during recording.
    /// </summary>
    public class ReplaySystem : MonoBehaviour
    {
        [SerializeField] private float _replayDuration = 5f;
        [SerializeField] private float _playbackSpeed = 0.5f;
        [SerializeField] private float _recordFps = 30f;
        [SerializeField] private List<Transform> _trackedObjects = new List<Transform>();

        private ReplayBuffer _buffer;
        private ReplayLogic _logic;
        private float _recordInterval;
        private float _timeSinceLastRecord;
        private float _gameTime;

        // Reusable arrays (no allocation during recording)
        private float[] _positionBuffer;
        private float[] _rotationBuffer;

        // Original positions for restore after replay
        private Vector3[] _originalPositions;
        private Quaternion[] _originalRotations;

        /// <summary>Whether currently recording.</summary>
        public bool IsRecording => _logic != null && _logic.IsRecording;

        /// <summary>Whether currently playing replay.</summary>
        public bool IsPlaying => _logic != null && _logic.IsPlaying;

        private void Awake()
        {
            int objectCount = _trackedObjects.Count;
            int capacity = Mathf.CeilToInt(_replayDuration * _recordFps);

            _buffer = new ReplayBuffer(objectCount, capacity);
            _logic = new ReplayLogic(_replayDuration, _playbackSpeed);
            _recordInterval = 1f / _recordFps;
            _timeSinceLastRecord = 0f;
            _gameTime = 0f;

            // Pre-allocate reusable buffers
            _positionBuffer = new float[objectCount * 3];
            _rotationBuffer = new float[objectCount * 4];
            _originalPositions = new Vector3[objectCount];
            _originalRotations = new Quaternion[objectCount];
        }

        private void OnEnable()
        {
            GoalDetector.OnGoalScored += HandleGoalScored;
        }

        private void OnDisable()
        {
            GoalDetector.OnGoalScored -= HandleGoalScored;
        }

        private void FixedUpdate()
        {
            if (!_logic.IsRecording) return;

            _gameTime += Time.fixedDeltaTime;
            _timeSinceLastRecord += Time.fixedDeltaTime;

            if (_timeSinceLastRecord >= _recordInterval)
            {
                RecordCurrentFrame();
                _timeSinceLastRecord = 0f;
            }
        }

        private void HandleGoalScored(TeamIdentifier team)
        {
            if (_logic.IsPlaying) return;
            StartCoroutine(PlayReplayCoroutine());
        }

        /// <summary>
        /// Trigger replay manually (for testing).
        /// </summary>
        public void TriggerReplay()
        {
            if (_logic.IsPlaying) return;
            StartCoroutine(PlayReplayCoroutine());
        }

        private IEnumerator PlayReplayCoroutine()
        {
            // Save current state
            SaveOriginalTransforms();

            _logic.StartPlayback(_gameTime);
            float startTime = _gameTime - _replayDuration;

            var frames = _buffer.GetFramesFromTime(startTime, _gameTime);
            if (frames.Length == 0)
            {
                _logic.StopPlayback();
                yield break;
            }

            int frameIndex = 0;
            float elapsed = 0f;

            while (_logic.IsPlaying && frameIndex < frames.Length)
            {
                // Apply frame data
                ApplyFrame(frames[frameIndex]);

                elapsed += Time.unscaledDeltaTime * _playbackSpeed;
                float frameTime = frames[frameIndex].Timestamp - frames[0].Timestamp;

                if (elapsed >= frameTime && frameIndex < frames.Length - 1)
                {
                    frameIndex++;
                }

                if (!_logic.UpdatePlayback(Time.unscaledDeltaTime))
                    break;

                yield return null;
            }

            // Restore original transforms
            RestoreOriginalTransforms();
            _logic.StopPlayback();
        }

        private void RecordCurrentFrame()
        {
            for (int i = 0; i < _trackedObjects.Count; i++)
            {
                if (_trackedObjects[i] == null) continue;

                int posIdx = i * 3;
                _positionBuffer[posIdx] = _trackedObjects[i].position.x;
                _positionBuffer[posIdx + 1] = _trackedObjects[i].position.y;
                _positionBuffer[posIdx + 2] = _trackedObjects[i].position.z;

                int rotIdx = i * 4;
                _rotationBuffer[rotIdx] = _trackedObjects[i].rotation.x;
                _rotationBuffer[rotIdx + 1] = _trackedObjects[i].rotation.y;
                _rotationBuffer[rotIdx + 2] = _trackedObjects[i].rotation.z;
                _rotationBuffer[rotIdx + 3] = _trackedObjects[i].rotation.w;
            }

            _buffer.RecordFrame(_positionBuffer, _rotationBuffer, _gameTime);
        }

        private void ApplyFrame(ReplayFrame frame)
        {
            for (int i = 0; i < _trackedObjects.Count; i++)
            {
                if (_trackedObjects[i] == null) continue;

                int posIdx = i * 3;
                _trackedObjects[i].position = new Vector3(
                    frame.Positions[posIdx],
                    frame.Positions[posIdx + 1],
                    frame.Positions[posIdx + 2]
                );

                int rotIdx = i * 4;
                _trackedObjects[i].rotation = new Quaternion(
                    frame.Rotations[rotIdx],
                    frame.Rotations[rotIdx + 1],
                    frame.Rotations[rotIdx + 2],
                    frame.Rotations[rotIdx + 3]
                );
            }
        }

        private void SaveOriginalTransforms()
        {
            for (int i = 0; i < _trackedObjects.Count; i++)
            {
                if (_trackedObjects[i] == null) continue;
                _originalPositions[i] = _trackedObjects[i].position;
                _originalRotations[i] = _trackedObjects[i].rotation;
            }
        }

        private void RestoreOriginalTransforms()
        {
            for (int i = 0; i < _trackedObjects.Count; i++)
            {
                if (_trackedObjects[i] == null) continue;
                _trackedObjects[i].position = _originalPositions[i];
                _trackedObjects[i].rotation = _originalRotations[i];
            }
        }
    }
}
