using UnityEngine;
using UnityEngine.Rendering;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// VAR-style offside lines painted flat on the grass across the full pitch width:
    /// one at the attacker's foremost point, one at the second-last defender, with a
    /// translucent band between them. Soft edges come from an alpha-falloff texture across
    /// the line width plus a fade at both touchlines. URP-friendly: vertex-coloured
    /// transparent unlit material (Particles/Unlit), falling back to Sprites/Default.
    /// </summary>
    public class OffsideLineOverlay : MonoBehaviour
    {
        private const int SoftTextureHeight = 32;

        private LineRenderer _attackerLine;
        private LineRenderer _defenderLine;
        private LineRenderer _gapBand;
        private Material _material;
        private Material _solidMaterial;
        private Texture2D _softTexture;

        private float _halfWidth = 15f;
        private float _height = 0.075f;
        private float _lineWidth = 0.09f;
        private float _attackerX;
        private float _defenderX;
        private Color _attackerColor = Color.red;
        private Color _defenderColor = Color.cyan;
        private Color _bandColor = new Color(1f, 0f, 0f, 0.18f);

        public bool IsVisible { get; private set; }

        public void Initialize(float pitchHalfWidth, OffsidePresentationConfig config)
        {
            _halfWidth = pitchHalfWidth;
            if (config != null)
            {
                _height = config.LineHeight;
                _lineWidth = config.LineWidth;
                _attackerColor = config.AttackerLineColor;
                _defenderColor = config.DefenderLineColor;
                _bandColor = config.GapBandColor;
            }

            _material = config != null && config.LineMaterialOverride != null
                ? config.LineMaterialOverride
                : CreateLineMaterial(GetSoftTexture());

            if (_attackerLine == null)
            {
                _gapBand = CreateLine("OffsideGapBand", 0);
                _defenderLine = CreateLine("OffsideDefenderLine", 1);
                _attackerLine = CreateLine("OffsideAttackerLine", 2);
            }
            Hide();
        }

        /// <summary>Show both lines at world X positions. Call SetReveal to sweep them in.</summary>
        public void Show(float attackerLineX, float defenderLineX)
        {
            _attackerX = attackerLineX;
            _defenderX = defenderLineX;
            IsVisible = true;
            SetActive(true);
            SetReveal(0f);
        }

        /// <summary>0..1: how far the lines have swept across from the near touchline.</summary>
        public void SetReveal(float reveal)
        {
            if (!IsVisible) return;
            reveal = Mathf.Clamp01(reveal);
            float eased = 1f - Mathf.Pow(1f - reveal, 3f);
            float zStart = -_halfWidth;
            float zEnd = Mathf.Lerp(-_halfWidth, _halfWidth, Mathf.Max(0.001f, eased));

            ApplyLine(_attackerLine, _attackerX, zStart, zEnd, _lineWidth, _attackerColor);
            ApplyLine(_defenderLine, _defenderX, zStart, zEnd, _lineWidth, _defenderColor);

            float gap = Mathf.Abs(_attackerX - _defenderX);
            bool showBand = gap > _lineWidth;
            _gapBand.enabled = showBand;
            if (showBand)
                ApplyLine(_gapBand, (_attackerX + _defenderX) * 0.5f, zStart, zEnd, gap, _bandColor, solid: true);
        }

        public void Hide()
        {
            IsVisible = false;
            SetActive(false);
        }

        private void SetActive(bool active)
        {
            if (_attackerLine != null) _attackerLine.enabled = active;
            if (_defenderLine != null) _defenderLine.enabled = active;
            if (_gapBand != null) _gapBand.enabled = active;
        }

        private void ApplyLine(LineRenderer line, float x, float zStart, float zEnd, float width,
            Color color, bool solid = false)
        {
            line.SetPosition(0, new Vector3(x, _height, zStart));
            line.SetPosition(1, new Vector3(x, _height, zEnd));
            line.widthMultiplier = width;

            // Fade in at both touchlines for soft ends.
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(color.a, 0.06f),
                    new GradientAlphaKey(color.a, 0.94f),
                    new GradientAlphaKey(0f, 1f)
                });
            line.colorGradient = gradient;
            // The band is wide; the soft texture would make it look like a third line.
            line.textureMode = LineTextureMode.Stretch;
            line.sharedMaterial = solid ? GetSolidMaterial() : _material;
        }

        private Material GetSolidMaterial()
        {
            if (_solidMaterial == null)
                _solidMaterial = _material == null ? null : CreateLineMaterial(Texture2D.whiteTexture);
            return _solidMaterial;
        }

        private LineRenderer CreateLine(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            // Lay the line flat: with TransformZ alignment the quad faces the transform's Z axis,
            // which this rotation points straight down at the grass.
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.alignment = LineAlignment.TransformZ;
            line.numCapVertices = 0;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = sortingOrder;
            line.sharedMaterial = _material;
            line.enabled = false;
            return line;
        }

        private Texture2D GetSoftTexture()
        {
            if (_softTexture != null) return _softTexture;

            // Alpha falls off toward both edges across the line width (V axis).
            _softTexture = new Texture2D(4, SoftTextureHeight, TextureFormat.RGBA32, false);
            _softTexture.wrapMode = TextureWrapMode.Clamp;
            _softTexture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[4 * SoftTextureHeight];
            for (int y = 0; y < SoftTextureHeight; y++)
            {
                float v = (y + 0.5f) / SoftTextureHeight;
                float edge = 1f - Mathf.Abs(v * 2f - 1f);   // 0 at edges, 1 at centre
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge * 2.2f));
                for (int x = 0; x < 4; x++) pixels[y * 4 + x] = new Color(1f, 1f, 1f, alpha);
            }
            _softTexture.SetPixels(pixels);
            _softTexture.Apply(false, true);
            return _softTexture;
        }

        /// <summary>
        /// Transparent, unlit, vertex-coloured, double-sided material for LineRenderers in URP.
        /// </summary>
        private static Material CreateLineMaterial(Texture texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            bool urpParticles = shader != null;
            if (!urpParticles) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader) { name = "OffsideLineMaterial" };
            if (urpParticles)
            {
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetFloat("_Blend", 0f);   // Alpha
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_Cull", (float)CullMode.Off);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetTexture("_BaseMap", texture);
                mat.SetColor("_BaseColor", Color.white);
            }
            else
            {
                mat.mainTexture = texture;
            }
            mat.renderQueue = (int)RenderQueue.Transparent + 10;
            return mat;
        }

        private void OnDestroy()
        {
            if (_softTexture != null) Destroy(_softTexture);
        }
    }
}
