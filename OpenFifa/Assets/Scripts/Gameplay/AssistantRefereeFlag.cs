using System;
using System.Collections;
using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Placeholder assistant referee on the near touchline. Raises a flag when offside is
    /// called. Exposes events so an animated official can replace the placeholder later
    /// (listen to OnFlagRaised / OnFlagLowered and drive the rig instead).
    /// All motion uses unscaled time so it plays during slow motion and freeze.
    /// </summary>
    public class AssistantRefereeFlag : MonoBehaviour
    {
        [SerializeField] private float _raisedAngle = 165f;
        [SerializeField] private bool _buildPlaceholder = true;

        private Transform _armPivot;
        private Coroutine _animation;
        private OffsidePitchFrame _frame = OffsidePitchFrame.LengthAlongX(15f);
        private float _sideOffset = 1.2f;
        private bool _farSide;
        private Color _flagColor = new Color(1f, 0.85f, 0.1f);
        private float _raiseDuration = 0.18f;

        /// <summary>Fired when the flag goes up. Arg: depth along the pitch length the official is standing at.</summary>
        public event Action<float> OnFlagRaised;

        /// <summary>Fired when the flag comes down.</summary>
        public event Action OnFlagLowered;

        public bool IsRaised { get; private set; }

        /// <summary>
        /// Place the official on the near touchline (4v4 scene: pitch length along X).
        /// </summary>
        public void Initialize(float pitchHalfWidth, float sideOffset, Color flagColor, float raiseDuration) =>
            Initialize(OffsidePitchFrame.LengthAlongX(pitchHalfWidth), sideOffset, false, flagColor, raiseDuration);

        /// <summary>
        /// Place the official outside the near (camera) touchline, or the far one when <paramref name="farSide"/>
        /// (so the broadcast camera sees the flag across the pitch).
        /// </summary>
        public void Initialize(OffsidePitchFrame frame, float sideOffset, bool farSide, Color flagColor, float raiseDuration)
        {
            _frame = frame;
            _sideOffset = sideOffset;
            _farSide = farSide;
            _flagColor = flagColor;
            _raiseDuration = Mathf.Max(0.01f, raiseDuration);
            PlaceAt(0f);
            transform.rotation = Quaternion.Euler(0f, _frame.AssistantFacingYaw(_farSide), 0f); // facing the pitch

            if (_buildPlaceholder && _armPivot == null) BuildPlaceholder();
        }

        /// <summary>Move level with the offside line (depth along the pitch length) and raise the flag.</summary>
        public void RaiseFlag(float lineDepth)
        {
            PlaceAt(lineDepth);
            IsRaised = true;
            Animate(_raisedAngle);
            OnFlagRaised?.Invoke(lineDepth);
        }

        private void PlaceAt(float depth)
        {
            _frame.AssistantPosition(depth, _sideOffset, _farSide, out float x, out float z);
            transform.position = new Vector3(x, 0f, z);
        }

        public void LowerFlag()
        {
            if (!IsRaised) return;
            IsRaised = false;
            Animate(0f);
            OnFlagLowered?.Invoke();
        }

        private void Animate(float targetAngle)
        {
            if (_armPivot == null) return;
            if (_animation != null) StopCoroutine(_animation);
            if (!isActiveAndEnabled)
            {
                _armPivot.localRotation = Quaternion.Euler(-targetAngle, 0f, 0f);
                return;
            }
            _animation = StartCoroutine(AnimateArm(targetAngle));
        }

        private IEnumerator AnimateArm(float targetAngle)
        {
            Quaternion from = _armPivot.localRotation;
            // Negative X rotation swings the arm forward and up toward the pitch.
            Quaternion to = Quaternion.Euler(-targetAngle, 0f, 0f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / _raiseDuration;
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                _armPivot.localRotation = Quaternion.Slerp(from, to, eased);
                yield return null;
            }
            _animation = null;
        }

        private void BuildPlaceholder()
        {
            var kit = CreateUnlitMaterial(new Color(0.12f, 0.12f, 0.14f));
            var flagMat = CreateUnlitMaterial(_flagColor);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "AssistantBody";
            StripCollider(body);
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.45f, 0.9f, 0.45f);
            body.GetComponent<MeshRenderer>().sharedMaterial = kit;

            // Shoulder pivot (pitch side is +Z because the official faces the pitch).
            var pivot = new GameObject("FlagArmPivot").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(0.28f, 1.45f, 0f);
            _armPivot = pivot;

            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Arm";
            StripCollider(arm);
            arm.transform.SetParent(pivot, false);
            arm.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            arm.transform.localScale = new Vector3(0.1f, 0.6f, 0.1f);
            arm.GetComponent<MeshRenderer>().sharedMaterial = kit;

            var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stick.name = "FlagStick";
            StripCollider(stick);
            stick.transform.SetParent(pivot, false);
            stick.transform.localPosition = new Vector3(0f, -0.85f, 0f);
            stick.transform.localScale = new Vector3(0.025f, 0.3f, 0.025f);
            stick.GetComponent<MeshRenderer>().sharedMaterial = CreateUnlitMaterial(new Color(0.85f, 0.85f, 0.85f));

            var cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cloth.name = "FlagCloth";
            StripCollider(cloth);
            cloth.transform.SetParent(pivot, false);
            cloth.transform.localPosition = new Vector3(0f, -1.0f, 0.2f);
            cloth.transform.localScale = new Vector3(0.01f, 0.28f, 0.38f);
            cloth.GetComponent<MeshRenderer>().sharedMaterial = flagMat;
        }

        private static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private static Material CreateUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            return mat;
        }
    }
}
