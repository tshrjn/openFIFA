using OpenFifa.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// VAR-style offside lines painted flat on the grass across the full pitch width:
    /// one at the attacker's foremost point, one at the second-last defender, with a
    /// translucent band between them. Soft edges come from an alpha-falloff texture across
    /// the line width plus a fade at both touchlines. URP-friendly: vertex-coloured
    /// transparent unlit material cloned from the Resources template
    /// <see cref="LineMaterialTemplateResource"/> (so URP Particles/Unlit and its transparent variant ship in player
    /// builds; a bare Shader.Find material drew nothing there), falling back to Shader.Find in the editor.
    /// Width: at least the configured world width, and never thinner on screen than
    /// <see cref="OffsideLineWidth.DefaultMinScreenFraction"/> of the view height (each end sized for its own distance).
    /// Axis-aware: line depths are positions along the pitch length, mapped to world X/Z by an
    /// <see cref="OffsidePitchFrame"/> (X for the 4v4 scene, Z for the 11v11 match sim).
    /// </summary>
    [DefaultExecutionOrder(310)] // after OffsideCameraCut (300) has posed the camera for this frame
    public class OffsideLineOverlay : MonoBehaviour
    {
        private const int SoftTextureHeight = 32;

        /// <summary>
        /// Where the line's points sit along its length (0 = near touchline, 1 = far). A LineRenderer only samples its
        /// colour gradient at its points, so the touchline fades need points at their keys: with just the two end points
        /// both samples were the fully transparent ends and the whole line drew at alpha 0 (it never showed).
        /// </summary>
        public static readonly float[] PointFractions = { 0f, FadeFraction, 0.5f, 1f - FadeFraction, 1f };
        /// <summary>Length fraction over which each end fades in from the touchline.</summary>
        public const float FadeFraction = 0.06f;

        /// <summary>Alpha-blended URP Particles/Unlit material kept in the build (see SinglePlayerSceneBootstrap).</summary>
        public const string LineMaterialTemplateResource = "OpenFootball/ShaderKeep/KeepParticlesUnlit";

        private LineRenderer _attackerLine;
        private LineRenderer _defenderLine;
        private LineRenderer _gapBand;
        private Material _material;
        private Material _solidMaterial;
        private Texture2D _softTexture;

        private OffsidePitchFrame _frame = OffsidePitchFrame.LengthAlongX(15f);
        private float _height = 0.075f;
        private float _lineWidth = OffsideLineWidth.DefaultBaseWidth;
        private float _minScreenFraction = OffsideLineWidth.DefaultMinScreenFraction;
        private float _reveal;
        private float _attackerDepth;
        private float _defenderDepth;
        private Color _attackerColor = Color.red;
        private Color _defenderColor = Color.cyan;
        private Color _bandColor = new Color(1f, 0f, 0f, 0.18f);

        public bool IsVisible { get; private set; }
        public OffsidePitchFrame Frame => _frame;
        /// <summary>Camera the minimum on-screen width is measured from (null = Camera.main).</summary>
        public Camera ViewCamera { get; set; }
        /// <summary>The line material in use (null if no shader was found).</summary>
        public Material LineMaterial => _material;
        /// <summary>World widths of the attacker line at its near and far ends, as last applied.</summary>
        public float AttackerStartWidth => _attackerLine != null ? _attackerLine.startWidth : 0f;
        public float AttackerEndWidth => _attackerLine != null ? _attackerLine.endWidth : 0f;
        public float DefenderStartWidth => _defenderLine != null ? _defenderLine.startWidth : 0f;
        public float DefenderEndWidth => _defenderLine != null ? _defenderLine.endWidth : 0f;
        public float AttackerDepth => _attackerDepth;
        public float DefenderDepth => _defenderDepth;

        /// <summary>4v4 scene: pitch length along world X.</summary>
        public void Initialize(float pitchHalfWidth, OffsidePresentationConfig config) =>
            Initialize(OffsidePitchFrame.LengthAlongX(pitchHalfWidth), config);

        public void Initialize(OffsidePitchFrame frame, OffsidePresentationConfig config)
        {
            _frame = frame;
            if (config != null)
            {
                _height = config.LineHeight;
                _lineWidth = config.LineWidth;
                _minScreenFraction = config.LineMinScreenFraction;
                _attackerColor = config.AttackerLineColor;
                _defenderColor = config.DefenderLineColor;
                _bandColor = config.GapBandColor;
            }

            _material = config != null && config.LineMaterialOverride != null
                ? config.LineMaterialOverride
                : CreateLineMaterial(GetSoftTexture(), true);

            if (_attackerLine == null)
            {
                _gapBand = CreateLine("OffsideGapBand", 0);
                _defenderLine = CreateLine("OffsideDefenderLine", 1);
                _attackerLine = CreateLine("OffsideAttackerLine", 2);
            }
            Hide();
        }

        /// <summary>
        /// Show both lines at depths along the pitch length (world X or Z per the frame). Call SetReveal to sweep them in.
        /// </summary>
        public void Show(float attackerDepth, float defenderDepth)
        {
            _attackerDepth = attackerDepth;
            _defenderDepth = defenderDepth;
            IsVisible = true;
            SetActive(true);
            SetReveal(0f);
        }

        /// <summary>0..1: how far the lines have swept across from the near touchline.</summary>
        public void SetReveal(float reveal)
        {
            if (!IsVisible) return;
            _reveal = Mathf.Clamp01(reveal);
            float eased = Mathf.Max(0.001f, 1f - Mathf.Pow(1f - _reveal, 3f));

            ApplyLine(_attackerLine, _attackerDepth, eased, _lineWidth, _attackerColor, scaleWithDistance: true);
            ApplyLine(_defenderLine, _defenderDepth, eased, _lineWidth, _defenderColor, scaleWithDistance: true);

            // The band fills the gap between the line centres; lines closer than a line width just overlap
            // (the attacker's on top), which is how a marginal call looks.
            float gap = Mathf.Abs(_attackerDepth - _defenderDepth);
            bool showBand = gap > _lineWidth;
            _gapBand.enabled = showBand;
            if (showBand)
                ApplyLine(_gapBand, (_attackerDepth + _defenderDepth) * 0.5f, eased, gap, _bandColor, solid: true);
        }

        private void LateUpdate()
        {
            // Re-fit the widths to this frame's camera pose (the cut moves the camera after the lines are shown).
            if (IsVisible) SetReveal(_reveal);
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

        /// <summary>World endpoints of a line at <paramref name="depth"/> swept to <paramref name="reveal"/> (0..1).</summary>
        public void GetLineEndpoints(float depth, float reveal, out Vector3 start, out Vector3 end)
        {
            _frame.LineEndpoints(depth, reveal, out float sx, out float sz, out float ex, out float ez);
            start = new Vector3(sx, _height, sz);
            end = new Vector3(ex, _height, ez);
        }

        private void ApplyLine(LineRenderer line, float depth, float reveal, float width,
            Color color, bool solid = false, bool scaleWithDistance = false)
        {
            GetLineEndpoints(depth, reveal, out var start, out var end);
            if (line.positionCount != PointFractions.Length) line.positionCount = PointFractions.Length;
            for (int i = 0; i < PointFractions.Length; i++)
                line.SetPosition(i, Vector3.LerpUnclamped(start, end, PointFractions[i]));
            line.widthMultiplier = 1f;
            float startWidth = width, endWidth = width;
            var cam = ViewCamera != null ? ViewCamera : Camera.main;
            if (scaleWithDistance && cam != null && !cam.orthographic)
            {
                // Each end sized for its own distance, so the far end does not thin to nothing.
                var camPos = cam.transform.position;
                startWidth = OffsideLineWidth.AtDistance(width, Vector3.Distance(camPos, start), cam.fieldOfView, _minScreenFraction);
                endWidth = OffsideLineWidth.AtDistance(width, Vector3.Distance(camPos, end), cam.fieldOfView, _minScreenFraction);
            }
            line.startWidth = startWidth;
            line.endWidth = endWidth;

            // Fade in at both touchlines for soft ends.
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(color.a, FadeFraction),
                    new GradientAlphaKey(color.a, 1f - FadeFraction),
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
                _solidMaterial = _material == null ? null : CreateLineMaterial(Texture2D.whiteTexture, false);
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
            line.positionCount = PointFractions.Length;
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
        /// Transparent, unlit, vertex-coloured, double-sided material for LineRenderers in URP: a copy of the Resources
        /// template (always in the build, with its transparent variant), else URP Particles/Unlit or Sprites/Default by
        /// name (editor). Logs which one it got, and an error if none (the lines would not draw).
        /// </summary>
        private static Material CreateLineMaterial(Texture texture, bool log)
        {
            Material mat = null;
            string source;
            var template = Resources.Load<Material>(LineMaterialTemplateResource);
            if (template != null && template.shader != null && template.shader.isSupported)
            {
                mat = new Material(template);
                source = "Resources/" + LineMaterialTemplateResource;
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader != null && shader.isSupported)
                {
                    mat = new Material(shader);
                    mat.SetFloat("_Surface", 1f); // Transparent
                    mat.SetFloat("_Blend", 0f);   // Alpha
                    mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                    mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0f);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    source = "Shader.Find(URP Particles/Unlit)";
                }
                else
                {
                    shader = Shader.Find("Sprites/Default");
                    if (shader != null) mat = new Material(shader);
                    source = "Shader.Find(Sprites/Default)";
                }
            }
            if (mat == null)
            {
                Debug.LogError("[OffsideLineOverlay] no line shader (template " + LineMaterialTemplateResource +
                               " missing and Shader.Find failed): offside lines will not draw");
                return null;
            }

            mat.name = "OffsideLineMaterial";
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_MainTex")) mat.mainTexture = texture;
            // After the turf, markings and the rest of the transparent queue (crowd cards, rain) so the lines sit on top.
            mat.renderQueue = (int)RenderQueue.Transparent + 10;
            if (log)
                Debug.Log("[OffsideLineOverlay] line material from " + source + ", shader " + mat.shader.name +
                          " (supported " + mat.shader.isSupported + "), queue " + mat.renderQueue);
            return mat;
        }

        private void OnDestroy()
        {
            if (_softTexture != null) Destroy(_softTexture);
            if (_solidMaterial != null) Destroy(_solidMaterial);
        }
    }
}
