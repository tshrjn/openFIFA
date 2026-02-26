using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Camera shake effect on goal scored.
    /// Uses CameraShakeLogic for pure C# shake calculation.
    /// Optionally integrates with Cinemachine Impulse if available.
    /// </summary>
    public class GoalCameraShake : MonoBehaviour
    {
        [SerializeField] private float _shakeIntensity = 1f;
        [SerializeField] private float _shakeDuration = 0.7f;
        [SerializeField] private Camera _targetCamera;

        private CameraShakeLogic _logic;
        private Vector3 _originalCameraPosition;

        /// <summary>The underlying shake logic.</summary>
        public CameraShakeLogic ShakeLogic => _logic;

        /// <summary>Whether the camera is currently shaking.</summary>
        public bool IsShaking => _logic != null && _logic.IsShaking;

        private void Awake()
        {
            var config = new CameraShakeConfigData
            {
                ShakeIntensity = _shakeIntensity,
                ShakeDuration = _shakeDuration
            };
            _logic = new CameraShakeLogic(config);

            if (_targetCamera == null)
                _targetCamera = Camera.main;
        }

        private void OnEnable()
        {
            GoalDetector.OnGoalScored += HandleGoalScored;
        }

        private void OnDisable()
        {
            GoalDetector.OnGoalScored -= HandleGoalScored;
        }

        private void LateUpdate()
        {
            if (_logic == null || !_logic.IsShaking) return;

            _logic.Update(Time.deltaTime);

            if (_targetCamera != null)
            {
                _targetCamera.transform.localPosition += new Vector3(
                    _logic.OffsetX,
                    _logic.OffsetY,
                    0f
                );
            }
        }

        private void HandleGoalScored(TeamIdentifier team)
        {
            TriggerShake();
        }

        /// <summary>
        /// Trigger camera shake manually (for testing).
        /// </summary>
        public void TriggerShake()
        {
            _logic.TriggerShake();
        }
    }
}
