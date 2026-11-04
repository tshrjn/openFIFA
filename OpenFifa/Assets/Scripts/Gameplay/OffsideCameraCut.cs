using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Hard cut to a high touchline angle looking straight across the pitch along the offside lines
    /// (camera sits at the lines' depth so they read as parallel lines running up the frame), then a
    /// hard cut back. Axis-aware through <see cref="OffsidePitchFrame"/>.
    ///
    /// Two ways to hand back:
    /// - with a <c>suspendDriver</c> (4v4 BroadcastCameraController): the driver is disabled during the cut and the
    ///   exact saved pose is restored afterwards (original behaviour);
    /// - without one (11v11 SinglePlayerCamera): the camera driver keeps running and tracking underneath, and this
    ///   component overrides the pose after it each LateUpdate (execution order 300 &gt; 200). Cutting out just stops the
    ///   override, so the broadcast framing resumes from live state with no stale jump or re-snap.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public class OffsideCameraCut : MonoBehaviour
    {
        private Camera _camera;
        private Behaviour _suspendDriver;
        private OffsidePitchFrame _frame = OffsidePitchFrame.LengthAlongX(15f);
        private float _height = 14f;
        private float _sideOffset = 6f;
        private float _fov = 50f;

        private bool _isCut;
        private bool _driverWasEnabled;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;
        private float _savedFov;
        private Vector3 _cutPosition;
        private Quaternion _cutRotation;

        public bool IsCut => _isCut;
        public OffsidePitchFrame Frame => _frame;

        /// <summary>4v4 scene: pitch length along X; suspends <paramref name="broadcast"/> during the cut.</summary>
        public void Initialize(Camera targetCamera, Behaviour broadcast,
            float pitchHalfWidth, OffsidePresentationConfig config) =>
            Initialize(targetCamera, broadcast, OffsidePitchFrame.LengthAlongX(pitchHalfWidth), config);

        public void Initialize(Camera targetCamera, Behaviour suspendDriver,
            OffsidePitchFrame frame, OffsidePresentationConfig config)
        {
            _camera = targetCamera;
            _suspendDriver = suspendDriver;
            _frame = frame;
            if (config != null)
            {
                _height = config.CameraHeight;
                _sideOffset = config.CameraSideOffset;
                _fov = config.CameraFieldOfView;
            }
        }

        /// <summary>World pose of the cut for lines centred on <paramref name="lineDepth"/> (along the pitch length).</summary>
        public void GetCutPose(float lineDepth, out Vector3 position, out Vector3 lookAt)
        {
            _frame.CameraPose(lineDepth, _height, _sideOffset,
                out float px, out float py, out float pz, out float lx, out float ly, out float lz);
            position = new Vector3(px, py, pz);
            lookAt = new Vector3(lx, ly, lz);
        }

        /// <summary>Cut to the offside angle centred on <paramref name="lineDepth"/> (world X or Z per the frame).</summary>
        public void CutTo(float lineDepth)
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            if (!_isCut)
            {
                var t = _camera.transform;
                _savedPosition = t.position;
                _savedRotation = t.rotation;
                _savedFov = _camera.fieldOfView;
                _driverWasEnabled = _suspendDriver != null && _suspendDriver.enabled;
                if (_suspendDriver != null) _suspendDriver.enabled = false;
                _isCut = true;
            }

            GetCutPose(lineDepth, out var position, out var lookAt);
            _cutPosition = position;
            _cutRotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            ApplyCut();
        }

        /// <summary>Cut back: restore the saved pose and resume a suspended driver, or just stop overriding a live one.</summary>
        public void Restore()
        {
            if (!_isCut) return;
            _isCut = false;
            if (_camera == null) return;

            if (_suspendDriver != null)
            {
                _camera.transform.SetPositionAndRotation(_savedPosition, _savedRotation);
                _camera.fieldOfView = _savedFov;
                if (_driverWasEnabled) _suspendDriver.enabled = true;
            }
            else
            {
                // The live driver sets pose and FOV again in its own LateUpdate; restoring the FOV covers drivers that
                // only set it on mode changes.
                _camera.fieldOfView = _savedFov;
            }
        }

        private void LateUpdate()
        {
            if (_isCut) ApplyCut();
        }

        private void ApplyCut()
        {
            if (_camera == null) return;
            _camera.transform.SetPositionAndRotation(_cutPosition, _cutRotation);
            _camera.fieldOfView = _fov;
        }

        private void OnDisable()
        {
            Restore();
        }
    }
}
