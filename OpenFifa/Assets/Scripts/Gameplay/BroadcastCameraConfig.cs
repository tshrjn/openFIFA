using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// ScriptableObject holding broadcast camera configuration.
    /// </summary>
    [CreateAssetMenu(fileName = "BroadcastCameraConfig", menuName = "OpenFifa/Config/Broadcast Camera")]
    public class BroadcastCameraConfig : ScriptableObject
    {
        [Header("Camera Positioning")]
        [SerializeField] private float _elevationAngle = 35f;
        [SerializeField] private float _distance = 25f;
        [SerializeField] private float _fieldOfView = 60f;
        [SerializeField] private float _minHeight = 5f;

        [Header("Tracking")]
        [SerializeField] private float _followDamping = 1f;
        [SerializeField] private float _ballTrackingWeight = 1f;
        [SerializeField] private float _playerTrackingWeight = 0.5f;

        public float ElevationAngle => _elevationAngle;
        public float Distance => _distance;
        public float FieldOfView => _fieldOfView;
        public float MinHeight => _minHeight;
        public float FollowDamping => _followDamping;
        public float BallTrackingWeight => _ballTrackingWeight;
        public float PlayerTrackingWeight => _playerTrackingWeight;

        public CameraConfigData ToData()
        {
            return new CameraConfigData(
                elevationAngle: _elevationAngle,
                followDamping: _followDamping,
                distance: _distance,
                fieldOfView: _fieldOfView,
                ballTrackingWeight: _ballTrackingWeight,
                playerTrackingWeight: _playerTrackingWeight,
                minHeight: _minHeight
            );
        }
    }
}
