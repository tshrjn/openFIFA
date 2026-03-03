using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Makes a world-space UI element always face the main camera.
    /// Attach to player name labels so they're readable from any angle.
    /// </summary>
    public class BillboardLabel : MonoBehaviour
    {
        private Transform _cam;

        private void Start()
        {
            var mainCam = Camera.main;
            if (mainCam != null) _cam = mainCam.transform;
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - _cam.position);
        }
    }
}
