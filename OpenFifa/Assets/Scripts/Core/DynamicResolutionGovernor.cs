using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Adaptive render-resolution governor for the external display. Feed it frame times; it lowers the
    /// render scale when frames run over budget and raises it again when there is headroom, staying in
    /// [minScale, maxScale].
    /// </summary>
    public sealed class DynamicResolutionGovernor
    {
        private readonly ExternalDisplayConfig _config;
        private readonly float _minScale;
        private readonly float _maxScale;
        private readonly float _budget;
        private float _sum;
        private int _count;

        public float Scale { get; private set; }

        public DynamicResolutionGovernor(ExternalDisplayConfig config, float startScale, float minScale, float maxScale, int frameRate)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _minScale = Math.Min(minScale, maxScale);
            _maxScale = maxScale;
            _budget = 1f / Math.Max(1, frameRate);
            Scale = Math.Max(_minScale, Math.Min(_maxScale, startScale));
        }

        /// <summary>
        /// Record one frame's duration in seconds. Returns true when <see cref="Scale"/> changed.
        /// Frames longer than 0.25 s (loading, app switch) are ignored.
        /// </summary>
        public bool AddFrame(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || deltaSeconds > 0.25f) return false;

            _sum += deltaSeconds;
            _count++;
            if (_count < Math.Max(1, _config.SampleFrames)) return false;

            float avg = _sum / _count;
            _sum = 0f;
            _count = 0;

            float next = Scale;
            if (avg > _budget * _config.DownscaleRatio)
                next = Math.Max(_minScale, Scale - _config.ScaleStep);
            else if (avg < _budget * _config.UpscaleRatio)
                next = Math.Min(_maxScale, Scale + _config.ScaleStep);

            if (Math.Abs(next - Scale) < 0.0001f) return false;
            Scale = next;
            return true;
        }
    }
}
