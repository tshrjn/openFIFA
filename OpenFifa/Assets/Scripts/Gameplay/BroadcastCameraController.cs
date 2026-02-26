using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Broadcast-style camera that smoothly follows the ball (and optionally active player).
    /// This is a fallback/standalone controller. In production, Cinemachine is preferred.
    /// Can work alongside or independently of Cinemachine.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BroadcastCameraController : MonoBehaviour
    {
        [SerializeField] private BroadcastCameraConfig _configAsset;
        [SerializeField] private Transform _ballTarget;
        [SerializeField] private Transform _playerTarget;

        private CameraConfigData _config;
        private Camera _camera;
        private Vector3 _currentVelocity;

        private void Awake()
        {
            _camera = GetComponent<Camera>();

            if (_configAsset != null)
            {
                _config = _configAsset.ToData();
            }
            else if (_config == null)
            {
                _config = new CameraConfigData();
            }

            ApplyFieldOfView();
        }

        /// <summary>
        /// Initialize for tests or runtime setup without ScriptableObject.
        /// </summary>
        public void Initialize(CameraConfigData config, Transform ballTarget, Transform playerTarget)
        {
            _config = config;
            _ballTarget = ballTarget;
            _playerTarget = playerTarget;

            if (_camera == null) _camera = GetComponent<Camera>();
            ApplyFieldOfView();
            UpdateCameraPosition(immediate: true);
        }

        private void LateUpdate()
        {
            if (_config == null) return;
            UpdateCameraPosition(immediate: false);
        }

        private void UpdateCameraPosition(bool immediate)
        {
            // Calculate weighted target position
            Vector3 targetPosition = CalculateTrackingTarget();

            // Calculate camera offset from elevation angle and distance
            float elevationRad = _config.ElevationAngle * Mathf.Deg2Rad;
            float horizontalDist = _config.Distance * Mathf.Cos(elevationRad);
            float height = _config.Distance * Mathf.Sin(elevationRad);

            // Camera offset: behind and above the target (negative Z in world space for broadcast view)
            Vector3 desiredPosition = targetPosition + new Vector3(0f, height, -horizontalDist);

            // Enforce minimum height
            if (desiredPosition.y < _config.MinHeight)
            {
                desiredPosition.y = _config.MinHeight;
            }

            // Apply smooth damping or immediate
            if (immediate)
            {
                transform.position = desiredPosition;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    desiredPosition,
                    ref _currentVelocity,
                    _config.FollowDamping);
            }

            // Always look at the target
            transform.LookAt(targetPosition);
        }

        private Vector3 CalculateTrackingTarget()
        {
            if (_ballTarget == null) return Vector3.zero;

            Vector3 target = _ballTarget.position * _config.BallTrackingWeight;
            float totalWeight = _config.BallTrackingWeight;

            if (_playerTarget != null)
            {
                target += _playerTarget.position * _config.PlayerTrackingWeight;
                totalWeight += _config.PlayerTrackingWeight;
            }

            if (totalWeight > 0f)
            {
                target /= totalWeight;
            }

            return target;
        }

        private void ApplyFieldOfView()
        {
            if (_camera != null && _config != null)
            {
                _camera.fieldOfView = _config.FieldOfView;
            }
        }

        /// <summary>
        /// Set the ball tracking target at runtime.
        /// </summary>
        public void SetBallTarget(Transform target)
        {
            _ballTarget = target;
        }

        /// <summary>
        /// Set the active player tracking target at runtime.
        /// </summary>
        public void SetPlayerTarget(Transform target)
        {
            _playerTarget = target;
        }
    }
}
