using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Hard cut to a high touchline angle looking straight down the offside lines
    /// (camera sits at the lines' X so they read as vertical in frame), then a hard cut
    /// back to the exact broadcast pose. Suspends BroadcastCameraController meanwhile.
    /// </summary>
    public class OffsideCameraCut : MonoBehaviour
    {
        private Camera _camera;
        private BroadcastCameraController _broadcast;
        private float _halfWidth = 15f;
        private float _height = 14f;
        private float _sideOffset = 6f;
        private float _fov = 50f;

        private bool _isCut;
        private bool _broadcastWasEnabled;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;
        private float _savedFov;

        public bool IsCut => _isCut;

        public void Initialize(Camera targetCamera, BroadcastCameraController broadcast,
            float pitchHalfWidth, OffsidePresentationConfig config)
        {
            _camera = targetCamera;
            _broadcast = broadcast;
            _halfWidth = pitchHalfWidth;
            if (config != null)
            {
                _height = config.CameraHeight;
                _sideOffset = config.CameraSideOffset;
                _fov = config.CameraFieldOfView;
            }
        }

        /// <summary>Cut to the offside angle centred on world X <paramref name="lineX"/>.</summary>
        public void CutTo(float lineX)
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            if (!_isCut)
            {
                var t = _camera.transform;
                _savedPosition = t.position;
                _savedRotation = t.rotation;
                _savedFov = _camera.fieldOfView;
                _broadcastWasEnabled = _broadcast != null && _broadcast.enabled;
                if (_broadcast != null) _broadcast.enabled = false;
                _isCut = true;
            }

            var cam = _camera.transform;
            cam.position = new Vector3(lineX, _height, -(_halfWidth + _sideOffset));
            // Aim at the far side of the pitch along the line so it runs up the centre of frame.
            cam.LookAt(new Vector3(lineX, 0f, _halfWidth * 0.25f));
            _camera.fieldOfView = _fov;
        }

        /// <summary>Cut back to the saved broadcast pose and resume the broadcast camera.</summary>
        public void Restore()
        {
            if (!_isCut) return;
            _isCut = false;
            if (_camera == null) return;

            _camera.transform.SetPositionAndRotation(_savedPosition, _savedRotation);
            _camera.fieldOfView = _savedFov;
            if (_broadcast != null && _broadcastWasEnabled) _broadcast.enabled = true;
        }

        private void OnDisable()
        {
            Restore();
        }
    }
}
