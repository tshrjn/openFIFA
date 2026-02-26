using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// A single replay frame containing positions, rotations, and timestamp.
    /// </summary>
    public struct ReplayFrame
    {
        /// <summary>Flat array of positions: [x0, y0, z0, x1, y1, z1, ...].</summary>
        public float[] Positions;

        /// <summary>Flat array of rotations: [x0, y0, z0, w0, x1, y1, z1, w1, ...].</summary>
        public float[] Rotations;

        /// <summary>Timestamp when this frame was recorded.</summary>
        public float Timestamp;
    }

    /// <summary>
    /// Pre-allocated ring buffer for replay frame storage.
    /// Records at 30fps, stores 5 seconds (150 frames).
    /// Zero garbage collection during recording.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class ReplayBuffer
    {
        private readonly ReplayFrame[] _frames;
        private readonly int _capacity;
        private readonly int _objectCount;
        private int _writeIndex;
        private int _frameCount;

        /// <summary>Buffer capacity in frames.</summary>
        public int Capacity => _capacity;

        /// <summary>Number of frames currently stored (up to capacity).</summary>
        public int FrameCount => Math.Min(_frameCount, _capacity);

        public ReplayBuffer(int objectCount, int capacity)
        {
            _objectCount = objectCount;
            _capacity = capacity;
            _writeIndex = 0;
            _frameCount = 0;

            // Pre-allocate all frames
            _frames = new ReplayFrame[capacity];
            for (int i = 0; i < capacity; i++)
            {
                _frames[i] = new ReplayFrame
                {
                    Positions = new float[objectCount * 3],
                    Rotations = new float[objectCount * 4],
                    Timestamp = 0f
                };
            }
        }

        /// <summary>
        /// Record a frame. Copies position and rotation data into the pre-allocated buffer.
        /// No allocation occurs.
        /// </summary>
        public void RecordFrame(float[] positions, float[] rotations, float timestamp)
        {
            int posLength = Math.Min(positions.Length, _frames[_writeIndex].Positions.Length);
            int rotLength = Math.Min(rotations.Length, _frames[_writeIndex].Rotations.Length);

            Array.Copy(positions, _frames[_writeIndex].Positions, posLength);
            Array.Copy(rotations, _frames[_writeIndex].Rotations, rotLength);
            _frames[_writeIndex].Timestamp = timestamp;

            _writeIndex = (_writeIndex + 1) % _capacity;
            _frameCount++;
        }

        /// <summary>
        /// Get frame by index (0 = oldest available frame).
        /// </summary>
        public ReplayFrame GetFrame(int index)
        {
            int count = FrameCount;
            if (index < 0 || index >= count)
                throw new IndexOutOfRangeException($"Frame index {index} out of range (0..{count - 1})");

            int startIndex;
            if (_frameCount <= _capacity)
            {
                startIndex = 0;
            }
            else
            {
                startIndex = _writeIndex; // Oldest frame is at write index after wrap
            }

            int actualIndex = (startIndex + index) % _capacity;
            return _frames[actualIndex];
        }

        /// <summary>
        /// Get frames within a time range.
        /// Returns an array of matching frames (may allocate for return array only).
        /// </summary>
        public ReplayFrame[] GetFramesFromTime(float startTime, float endTime)
        {
            int count = FrameCount;
            if (count == 0) return Array.Empty<ReplayFrame>();

            // Count matching frames first
            int matchCount = 0;
            for (int i = 0; i < count; i++)
            {
                var frame = GetFrame(i);
                if (frame.Timestamp >= startTime && frame.Timestamp <= endTime)
                    matchCount++;
            }

            if (matchCount == 0) return Array.Empty<ReplayFrame>();

            var result = new ReplayFrame[matchCount];
            int writeIdx = 0;
            for (int i = 0; i < count; i++)
            {
                var frame = GetFrame(i);
                if (frame.Timestamp >= startTime && frame.Timestamp <= endTime)
                {
                    result[writeIdx++] = frame;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Pure C# replay state logic.
    /// Manages recording/playback state and timing.
    /// </summary>
    public class ReplayLogic
    {
        private readonly float _replayDuration;
        private readonly float _playbackSpeed;

        private bool _isRecording;
        private bool _isPlaying;
        private float _playbackStartTime;
        private float _playbackElapsed;

        /// <summary>Duration of replay in seconds.</summary>
        public float ReplayDuration => _replayDuration;

        /// <summary>Playback speed multiplier (0.5 = half speed).</summary>
        public float PlaybackSpeed => _playbackSpeed;

        /// <summary>Whether currently recording.</summary>
        public bool IsRecording => _isRecording;

        /// <summary>Whether currently playing back.</summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>Current playback elapsed time.</summary>
        public float PlaybackElapsed => _playbackElapsed;

        public ReplayLogic(float replayDuration, float playbackSpeed)
        {
            _replayDuration = replayDuration;
            _playbackSpeed = playbackSpeed;
            _isRecording = true;
            _isPlaying = false;
        }

        /// <summary>
        /// Start playback from the given game time.
        /// Pauses recording.
        /// </summary>
        public void StartPlayback(float currentGameTime)
        {
            _isRecording = false;
            _isPlaying = true;
            _playbackStartTime = currentGameTime - _replayDuration;
            _playbackElapsed = 0f;
        }

        /// <summary>
        /// Update playback progress.
        /// Returns true if playback is still active.
        /// </summary>
        public bool UpdatePlayback(float unscaledDeltaTime)
        {
            if (!_isPlaying) return false;

            _playbackElapsed += unscaledDeltaTime * _playbackSpeed;

            if (_playbackElapsed >= _replayDuration)
            {
                StopPlayback();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Stop playback and resume recording.
        /// </summary>
        public void StopPlayback()
        {
            _isPlaying = false;
            _isRecording = true;
            _playbackElapsed = 0f;
        }
    }
}
