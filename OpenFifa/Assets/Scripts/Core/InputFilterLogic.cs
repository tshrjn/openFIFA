using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# input filtering logic with dead zone and normalization.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class InputFilterLogic
    {
        private const float DefaultDeadZone = 0.1f;
        private readonly float _deadZone;

        /// <summary>Dead zone threshold (0-1).</summary>
        public float DeadZone => _deadZone;

        public InputFilterLogic() : this(DefaultDeadZone) { }

        public InputFilterLogic(float deadZone)
        {
            _deadZone = deadZone;
        }

        /// <summary>
        /// Apply dead zone. Input below threshold is zeroed.
        /// Input above threshold is remapped to 0-1 range.
        /// </summary>
        public void ApplyDeadZone(float inX, float inY, out float outX, out float outY)
        {
            float magnitude = (float)Math.Sqrt(inX * inX + inY * inY);

            if (magnitude < _deadZone)
            {
                outX = 0f;
                outY = 0f;
                return;
            }

            // Remap from [deadZone, 1] to [0, 1]
            float remapped = (magnitude - _deadZone) / (1f - _deadZone);
            if (remapped > 1f) remapped = 1f;

            float scale = remapped / magnitude;
            outX = inX * scale;
            outY = inY * scale;
        }

        /// <summary>
        /// Normalize input to unit circle. Clamps magnitude to 1.
        /// </summary>
        public void Normalize(float inX, float inY, out float outX, out float outY)
        {
            float magnitude = (float)Math.Sqrt(inX * inX + inY * inY);

            if (magnitude <= 1f)
            {
                outX = inX;
                outY = inY;
                return;
            }

            outX = inX / magnitude;
            outY = inY / magnitude;
        }
    }

    /// <summary>
    /// Pure C# virtual joystick logic.
    /// Tracks pointer down position and calculates normalized output.
    /// </summary>
    public class VirtualJoystickLogic
    {
        private readonly float _outerRadius;
        private readonly float _deadZone;

        private float _centerX;
        private float _centerY;
        private float _outputX;
        private float _outputY;
        private bool _isActive;

        /// <summary>Current X output (-1 to 1).</summary>
        public float OutputX => _outputX;

        /// <summary>Current Y output (-1 to 1).</summary>
        public float OutputY => _outputY;

        /// <summary>Whether the joystick is currently being touched.</summary>
        public bool IsActive => _isActive;

        public VirtualJoystickLogic(float outerRadius, float deadZone)
        {
            _outerRadius = outerRadius;
            _deadZone = deadZone;
        }

        /// <summary>Handle pointer down at screen position.</summary>
        public void OnPointerDown(float screenX, float screenY)
        {
            _centerX = screenX;
            _centerY = screenY;
            _isActive = true;
            _outputX = 0f;
            _outputY = 0f;
        }

        /// <summary>Handle drag to screen position.</summary>
        public void OnDrag(float screenX, float screenY)
        {
            if (!_isActive) return;

            float dx = screenX - _centerX;
            float dy = screenY - _centerY;

            // Normalize by outer radius
            float normalizedX = dx / _outerRadius;
            float normalizedY = dy / _outerRadius;

            // Clamp to unit circle
            float magnitude = (float)Math.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
            if (magnitude > 1f)
            {
                normalizedX /= magnitude;
                normalizedY /= magnitude;
                magnitude = 1f;
            }

            // Apply dead zone
            if (magnitude < _deadZone)
            {
                _outputX = 0f;
                _outputY = 0f;
                return;
            }

            float remapped = (magnitude - _deadZone) / (1f - _deadZone);
            float scale = remapped / magnitude;
            _outputX = normalizedX * scale;
            _outputY = normalizedY * scale;
        }

        /// <summary>Handle pointer up.</summary>
        public void OnPointerUp()
        {
            _isActive = false;
            _outputX = 0f;
            _outputY = 0f;
        }
    }
}
