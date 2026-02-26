using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Configuration data for camera shake effect.
    /// </summary>
    public class CameraShakeConfigData
    {
        /// <summary>Shake intensity multiplier.</summary>
        public float ShakeIntensity = 1f;

        /// <summary>Shake duration in seconds (0.5 to 1.0).</summary>
        public float ShakeDuration = 0.7f;

        /// <summary>Decay rate for the shake (1 = linear decay).</summary>
        public float DecayRate = 1f;
    }

    /// <summary>
    /// Pure C# camera shake logic with decay.
    /// Generates pseudo-random offsets that decay over the shake duration.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class CameraShakeLogic
    {
        private readonly CameraShakeConfigData _config;
        private float _elapsed;
        private bool _isShaking;
        private float _currentIntensity;
        private float _offsetX;
        private float _offsetY;
        private int _seed;

        /// <summary>Whether the camera is currently shaking.</summary>
        public bool IsShaking => _isShaking;

        /// <summary>Current shake intensity (decays over time).</summary>
        public float CurrentIntensity => _currentIntensity;

        /// <summary>Current X offset for camera position.</summary>
        public float OffsetX => _offsetX;

        /// <summary>Current Y offset for camera position.</summary>
        public float OffsetY => _offsetY;

        public CameraShakeLogic(CameraShakeConfigData config)
        {
            _config = config;
            _isShaking = false;
            _currentIntensity = 0f;
            _offsetX = 0f;
            _offsetY = 0f;
            _seed = 0;
        }

        /// <summary>
        /// Trigger a new camera shake.
        /// </summary>
        public void TriggerShake()
        {
            _isShaking = true;
            _elapsed = 0f;
            _currentIntensity = _config.ShakeIntensity;
            _seed = Environment.TickCount;
        }

        /// <summary>
        /// Update the shake. Called each frame with deltaTime.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!_isShaking)
            {
                _offsetX = 0f;
                _offsetY = 0f;
                return;
            }

            _elapsed += deltaTime;

            if (_elapsed >= _config.ShakeDuration)
            {
                _isShaking = false;
                _currentIntensity = 0f;
                _offsetX = 0f;
                _offsetY = 0f;
                return;
            }

            // Decay intensity
            float progress = _elapsed / _config.ShakeDuration;
            _currentIntensity = _config.ShakeIntensity * (1f - progress * _config.DecayRate);
            if (_currentIntensity < 0f) _currentIntensity = 0f;

            // Generate pseudo-random offset using simple hash
            _seed = (_seed * 1103515245 + 12345) & 0x7fffffff;
            float randX = (_seed % 2000 - 1000) / 1000f;
            _seed = (_seed * 1103515245 + 12345) & 0x7fffffff;
            float randY = (_seed % 2000 - 1000) / 1000f;

            _offsetX = randX * _currentIntensity * 0.5f;
            _offsetY = randY * _currentIntensity * 0.5f;
        }
    }
}
