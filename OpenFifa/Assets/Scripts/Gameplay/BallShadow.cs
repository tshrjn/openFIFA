using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Projects a dark circle onto the pitch surface directly below the ball.
    /// Helps players judge ball height when airborne. Scales smaller as ball
    /// rises for a natural shadow perspective effect.
    /// </summary>
    public class BallShadow : MonoBehaviour
    {
        private Transform _shadow;
        private float _baseScale = 0.5f;

        private void Start()
        {
            var shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadow.name = "BallShadow";
            shadow.transform.localScale = new Vector3(_baseScale, 0.005f, _baseScale);

            // Dark semi-transparent material
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            mat.color = new Color(0f, 0f, 0f, 0.4f);
            if (shader != null && shader.name.Contains("Lit"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = 3000;
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            shadow.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Remove collider
            var col = shadow.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _shadow = shadow.transform;
        }

        private void LateUpdate()
        {
            if (_shadow == null) return;

            // Position shadow on pitch surface below ball
            float ballHeight = Mathf.Max(transform.position.y - 0.06f, 0f);
            _shadow.position = new Vector3(transform.position.x, 0.07f, transform.position.z);

            // Scale down as ball rises — min 30% at max height
            float scaleFactor = Mathf.Lerp(1f, 0.3f, ballHeight / 10f);
            float s = _baseScale * scaleFactor;
            _shadow.localScale = new Vector3(s, 0.005f, s);
        }
    }
}
