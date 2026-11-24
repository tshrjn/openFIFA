using System.Collections.Generic;
using OpenFifa.Core;
using OpenFifa.Gameplay.SinglePlayer;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Hides near-side stadium furniture (dugouts, tunnel canopy) while it fills the bottom of the broadcast frame
    /// (<see cref="NearSideOcclusion"/>: more than 8% of the frame height, shown again below 5%). Runs after the camera
    /// driver (200) and the offside cut (300), so it judges the pose that is rendered. Only acts in the broadcast mode
    /// (including the goal camera); review stills from the other modes show everything.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(400)]
    public sealed class NearSideOccluderCuller : MonoBehaviour
    {
        public ProceduralStadium Stadium;
        public SinglePlayerCamera Rig;

        private Camera _cam;
        private List<WorldBox> _boxes;
        private bool[] _hidden;

        /// <summary>Number of pieces hidden this frame (diagnostics, captures).</summary>
        public int HiddenCount { get; private set; }

        private void LateUpdate()
        {
            if (Stadium == null || Stadium.Layout == null) return;
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_boxes == null)
            {
                _boxes = NearSideOcclusion.Occluders(Stadium.Layout);
                _hidden = new bool[_boxes.Count];
            }
            var renderers = Stadium.NearSideOccluders;
            bool active = Rig == null || Rig.CameraMode == SinglePlayerCamera.Mode.Broadcast;
            var t = transform;
            // Stadium space (the stadium is built unrotated around the pitch centre).
            Vector3 p = Stadium.transform.InverseTransformPoint(t.position);
            Vector3 e = t.eulerAngles;
            float pitch = e.x > 180f ? e.x - 360f : e.x;
            var view = new BroadcastView(p.x, p.y, p.z, e.y, pitch, _cam.fieldOfView, _cam.aspect);
            int hiddenCount = 0;
            for (int i = 0; i < _boxes.Count && i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                _hidden[i] = active && NearSideOcclusion.Hidden(_hidden[i], NearSideOcclusion.Coverage(view, _boxes[i]));
                if (r.enabled == _hidden[i]) r.enabled = !_hidden[i];
                if (_hidden[i]) hiddenCount++;
            }
            HiddenCount = hiddenCount;
        }

        private void OnDisable()
        {
            if (Stadium == null) return;
            var renderers = Stadium.NearSideOccluders;
            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null) renderers[i].enabled = true;
            if (_hidden != null) System.Array.Clear(_hidden, 0, _hidden.Length);
        }
    }
}
