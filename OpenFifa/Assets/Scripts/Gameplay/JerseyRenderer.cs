using System;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Renders a football kit on a player with OpenFifaKit.shader ("OpenFifa/Kit").
    ///
    /// - Creates one material instance per player (keeps the SRP Batcher path; per-renderer
    ///   MaterialPropertyBlocks would opt every player out of it).
    /// - Generates the name/number print atlas on the CPU from the built-in stroke font
    ///   (no TMP/font assets), caching it: it is regenerated only when name, number,
    ///   layout, font style or outline width change. Colour changes never re-rasterize.
    /// - Applies licensed textures from a <see cref="KitDefinition"/> when present.
    /// - On capsule/proxy bodies (object-space mapping) it turns the kit to face the movement
    ///   direction, so back numbers read correctly even though the capsule never rotates.
    ///
    /// Works with the legacy <see cref="FullJerseyConfig"/> API used by JerseyCustomizationUI.
    /// </summary>
    public class JerseyRenderer : MonoBehaviour
    {
        [Header("Renderer References")]
        [SerializeField] private Renderer jerseyBackRenderer;
        [SerializeField] private Renderer jerseyFrontRenderer;

        [Header("Kit Material")]
        [Tooltip("Optional material using OpenFifa/Kit. If empty, the shader is found by name.")]
        [SerializeField] private Material kitMaterialTemplate;
        [SerializeField] private KitMappingMode mappingMode = KitMappingMode.ObjectSpace;
        [Tooltip("MeshUV mode only: back print panel as UV rect (min x, min y, width, height).")]
        [SerializeField] private Vector4 meshBackPrintRect = new Vector4(0.55f, 0.35f, 0.4f, 0.4f);
        [Tooltip("MeshUV mode only: front print panel as UV rect (min x, min y, width, height).")]
        [SerializeField] private Vector4 meshFrontPrintRect = new Vector4(0.05f, 0.35f, 0.4f, 0.4f);

        [Header("Texture Settings")]
        [SerializeField] private int nameNumberTextureWidth = 1024;
        [SerializeField] private int nameNumberTextureHeight = 512;
        [SerializeField] private int crestTextureSize = 128;

        [Header("Runtime State")]
        [SerializeField] private bool autoResolveClash = true;
        [Tooltip("Object-space mapping only: rotate the kit towards the Rigidbody's horizontal velocity.")]
        [SerializeField] private bool faceMovementDirection = true;
        [SerializeField] private float facingTurnSpeed = 540f;
        [SerializeField] private float facingMinSpeed = 0.4f;

        [Header("Match Wear")]
        [SerializeField, Range(0f, 1f)] private float sweat;
        [SerializeField, Range(0f, 1f)] private float dirt;

        private static bool _loggedMissingShader;

        private Material _material;
        private Texture2D _printTexture;
        private byte[] _printScratch;
        private string _printKey;
        private FullJerseyConfig _currentConfig;
        private KitDefinition _currentKit;
        private Rigidbody _rigidbody;
        private float _facingDegrees;
        private bool _isApplied;

        /// <summary>Whether a jersey has been applied.</summary>
        public bool IsApplied => _isApplied;

        /// <summary>The applied legacy configuration (also filled when a kit is applied).</summary>
        public FullJerseyConfig CurrentConfig => _currentConfig;

        /// <summary>The applied kit definition, or null when applied from a FullJerseyConfig.</summary>
        public KitDefinition CurrentKit => _currentKit;

        /// <summary>Whether this renderer auto-resolves kit clashes (read by match setup code).</summary>
        public bool AutoResolveClash => autoResolveClash;

        /// <summary>Per-player kit material instance (null until a jersey is applied).</summary>
        public Material KitMaterial => _material;

        /// <summary>Fired when jersey rendering completes.</summary>
        public event Action<JerseyRenderer> OnJerseyApplied;

        private void Awake()
        {
            AutoDiscoverRenderers();
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void OnDestroy()
        {
            ReleaseResources();
        }

        private void LateUpdate()
        {
            if (!_isApplied || _material == null || !faceMovementDirection
                || mappingMode != KitMappingMode.ObjectSpace || _rigidbody == null)
                return;

            Vector3 velocity = _rigidbody.linearVelocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude < facingMinSpeed * facingMinSpeed) return;

            Vector3 local = transform.InverseTransformDirection(velocity);
            float target = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float next = Mathf.MoveTowardsAngle(_facingDegrees, target, facingTurnSpeed * Time.deltaTime);
            if (!Mathf.Approximately(next, _facingDegrees))
            {
                _facingDegrees = next;
                _material.SetFloat(KitShaderIds.FacingAngle, _facingDegrees * Mathf.Deg2Rad);
            }
        }

        /// <summary>
        /// Applies a kit with a player's name and number. <paramref name="teamInitials"/> is used
        /// for the placeholder crest when the kit has no crest texture.
        /// </summary>
        public void ApplyKit(KitDefinition kit, string playerName, int number, string teamInitials)
        {
            if (kit == null || !EnsureMaterial()) return;

            var data = kit.Data;
            var design = kit.ToJerseyDesign();
            _currentKit = kit;
            _currentConfig = new JerseyBuilder()
                .WithDesign(design)
                .WithName(playerName ?? "")
                .WithNumber(number)
                .WithCrest(teamInitials ?? "")
                .Build();

            ApplyDesignProperties(design);
            _material.SetColor(KitShaderIds.ShortsColor, ToColor(KitColorUtility.ParseOrDefault(data.ShortsColor, SimpleColor.White)));
            _material.SetColor(KitShaderIds.SocksColor, ToColor(KitColorUtility.ParseOrDefault(data.SocksColor, SimpleColor.White)));
            _material.SetFloat(KitShaderIds.SheenStrength, data.FabricSheen);

            var numberColor = KitColorUtility.ParseOrDefault(data.NumberColor, JerseyComparer.SuggestTextColor(design.PrimaryColor));
            var outlineColor = KitColorUtility.ParseOrDefault(data.NumberOutlineColor, SimpleColor.Black);
            ApplyPrint(playerName, number, KitDefinitionConverter.GetNameLayout(data),
                KitDefinitionConverter.GetFontStyle(data), data.NumberOutlineWidth, data.ShowFrontNumber,
                numberColor, outlineColor);

            var textures = kit.ResolveTextures();
            ApplyLicensedTextures(textures);
            ApplyCrest(textures.Crest, teamInitials, design, KitDefinitionConverter.GetFontStyle(data));
            ApplySponsor(textures.Sponsor);
            ApplyWear();

            FinishApply();
        }

        /// <summary>
        /// Applies a legacy customization config (used by JerseyCustomizationUI).
        /// </summary>
        public void ApplyJersey(FullJerseyConfig config)
        {
            if (config == null || !EnsureMaterial()) return;

            _currentConfig = config;
            _currentKit = null;
            ApplyDesignProperties(config.Design);

            var design = config.Design ?? new JerseyDesign();
            var numberConfig = config.NumberConfig ?? new PlayerNumberConfig();
            var numberColor = JerseyComparer.SuggestTextColor(design.PrimaryColor);
            ApplyPrint(config.NameConfig != null ? config.NameConfig.Name : "", numberConfig.Number,
                KitNameLayout.Straight, KitFontStyle.Block, numberConfig.OutlineEnabled ? 0.3f : 0f,
                numberConfig.ShowOnFront, numberColor, numberConfig.OutlineColor);
            if (!numberConfig.ShowOnBack && !numberConfig.ShowOnFront)
                _material.SetFloat(KitShaderIds.UseNameNumber, 0f);

            ApplyLicensedTextures(default(KitTextureSet));
            ApplyCrest(null, config.Crest != null ? Initials(config.Crest.TeamName) : "", design, KitFontStyle.Block);
            ApplySponsor(null);
            ApplyWear();

            FinishApply();
        }

        /// <summary>
        /// Applies only colours/pattern/collar without touching prints (used for clash resolution).
        /// </summary>
        public void ApplyDesignOnly(JerseyDesign design)
        {
            if (design == null || !EnsureMaterial()) return;
            ApplyDesignProperties(design);
            if (_currentConfig != null)
                _currentConfig.Design = design;
        }

        /// <summary>
        /// Resolves a kit clash by switching to the first non-clashing variant (Home, Away, Third).
        /// Returns the variant selected, or null if no manager was provided.
        /// </summary>
        public KitVariant? ResolveClash(KitVariantManager kitManager, SimpleColor opponentPrimary)
        {
            if (kitManager == null) return null;

            var variant = kitManager.SelectNonClashingVariant(opponentPrimary);
            var design = kitManager.GetKit(variant);
            if (design != null)
            {
                ApplyDesignOnly(design);
                return variant;
            }
            return null;
        }

        /// <summary>Sets match wear (0-1). Cheap: two material floats.</summary>
        public void SetMatchWear(float sweatAmount, float dirtAmount)
        {
            sweat = Mathf.Clamp01(sweatAmount);
            dirt = Mathf.Clamp01(dirtAmount);
            ApplyWear();
        }

        /// <summary>Clears the jersey and releases generated resources.</summary>
        public void ClearJersey()
        {
            _currentConfig = null;
            _currentKit = null;
            _isApplied = false;
            ReleaseResources();
        }

        private void FinishApply()
        {
            _isApplied = true;
            OnJerseyApplied?.Invoke(this);
        }

        private void AutoDiscoverRenderers()
        {
            if (jerseyBackRenderer != null && jerseyFrontRenderer != null) return;

            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            if (jerseyBackRenderer == null) jerseyBackRenderer = renderers[0];
            if (jerseyFrontRenderer == null) jerseyFrontRenderer = renderers[0];
        }

        private bool EnsureMaterial()
        {
            if (_material != null) return true;

            AutoDiscoverRenderers();
            if (jerseyBackRenderer == null && jerseyFrontRenderer == null) return false;

            // Resources/Kits/OpenFifaKit.mat references the shader so it is included in player builds.
            var template = kitMaterialTemplate != null ? kitMaterialTemplate : Resources.Load<Material>(KitShaderIds.MaterialResourcePath);
            if (template != null && template.shader != null && template.shader.isSupported)
            {
                _material = new Material(template);
            }
            else
            {
                var shader = Shader.Find(KitShaderIds.ShaderName);
                if (shader == null || !shader.isSupported)
                {
                    if (!_loggedMissingShader)
                    {
                        Debug.LogWarning($"[JerseyRenderer] Shader '{KitShaderIds.ShaderName}' not found or unsupported; keeping the existing material.");
                        _loggedMissingShader = true;
                    }
                    return false;
                }
                _material = new Material(shader);
            }

            _material.name = $"Kit_{gameObject.name}";
            _material.hideFlags = HideFlags.DontSave;
            _material.SetFloat(KitShaderIds.MappingMode, (float)mappingMode);
            _material.SetFloat(KitShaderIds.FacingAngle, _facingDegrees * Mathf.Deg2Rad);
            if (mappingMode == KitMappingMode.MeshUV)
            {
                _material.SetVector(KitShaderIds.BackPrintRect, meshBackPrintRect);
                _material.SetVector(KitShaderIds.FrontPrintRect, meshFrontPrintRect);
            }

            if (jerseyBackRenderer != null) jerseyBackRenderer.sharedMaterial = _material;
            if (jerseyFrontRenderer != null && jerseyFrontRenderer != jerseyBackRenderer)
                jerseyFrontRenderer.sharedMaterial = _material;
            return true;
        }

        private void ApplyDesignProperties(JerseyDesign design)
        {
            if (design == null || _material == null) return;

            _material.SetColor(KitShaderIds.PrimaryColor, ToColor(design.PrimaryColor));
            _material.SetColor(KitShaderIds.SecondaryColor, ToColor(design.SecondaryColor));
            _material.SetColor(KitShaderIds.TertiaryColor, ToColor(design.TertiaryColor));
            _material.SetFloat(KitShaderIds.PatternType, (float)design.Pattern);
            _material.SetFloat(KitShaderIds.PatternScale, design.PatternScale);
            _material.SetFloat(KitShaderIds.CollarStyle, (float)design.Collar);
        }

        private void ApplyPrint(string playerName, int number, KitNameLayout layout, KitFontStyle style,
            float outlineWidth, bool showFrontNumber, SimpleColor numberColor, SimpleColor outlineColor)
        {
            _material.SetColor(KitShaderIds.NumberColor, ToColor(numberColor));
            _material.SetColor(KitShaderIds.NumberOutlineColor, ToColor(outlineColor));

            string key = string.Concat(playerName ?? "", "|", number.ToString(), "|", ((int)layout).ToString(), "|",
                ((int)style).ToString(), "|", outlineWidth.ToString("F3"), "|", showFrontNumber ? "1" : "0", "|",
                nameNumberTextureWidth.ToString(), "x", nameNumberTextureHeight.ToString());

            if (_printTexture == null || _printTexture.width != nameNumberTextureWidth
                || _printTexture.height != nameNumberTextureHeight)
            {
                DestroyTexture(ref _printTexture);
                _printTexture = KitPrintTextureBuilder.CreatePrintTexture(nameNumberTextureWidth, nameNumberTextureHeight);
                _printKey = null;
            }

            if (key != _printKey)
            {
                var printLayout = KitPrintLayoutLogic.Build(playerName, number, layout, style, outlineWidth,
                    showFrontNumber, nameNumberTextureWidth, nameNumberTextureHeight);
                KitPrintTextureBuilder.Write(_printTexture, printLayout, ref _printScratch);
                _printKey = key;
            }

            _material.SetTexture(KitShaderIds.NameNumberTex, _printTexture);
            _material.SetFloat(KitShaderIds.UseNameNumber, 1f);
        }

        private void ApplyLicensedTextures(KitTextureSet textures)
        {
            bool useLicensed = textures.HasAlbedo;
            _material.SetFloat(KitShaderIds.UseLicensedTextures, useLicensed ? 1f : 0f);
            _material.SetTexture(KitShaderIds.KitAlbedo, useLicensed ? textures.Albedo : Texture2D.whiteTexture);
            _material.SetTexture(KitShaderIds.KitNormal, useLicensed && textures.Normal != null ? textures.Normal : Texture2D.normalTexture);
            _material.SetTexture(KitShaderIds.KitMask, useLicensed && textures.Mask != null ? textures.Mask : Texture2D.whiteTexture);
            _material.SetFloat(KitShaderIds.LicensedHasNormal, useLicensed && textures.Normal != null ? 1f : 0f);
            _material.SetFloat(KitShaderIds.LicensedHasMask, useLicensed && textures.Mask != null ? 1f : 0f);
        }

        private void ApplyCrest(Texture2D crest, string initials, JerseyDesign design, KitFontStyle style)
        {
            if (crest == null && !string.IsNullOrEmpty(initials))
            {
                // Original placeholder crest until licensed crest art is supplied.
                crest = KitPrintTextureBuilder.GetPlaceholderCrest(initials, design.SecondaryColor,
                    design.PrimaryColor, design.TertiaryColor, style, crestTextureSize);
            }

            _material.SetTexture(KitShaderIds.CrestTex, crest != null ? crest : Texture2D.blackTexture);
            _material.SetFloat(KitShaderIds.UseCrest, crest != null ? 1f : 0f);
        }

        private void ApplySponsor(Texture2D sponsor)
        {
            _material.SetTexture(KitShaderIds.SponsorTex, sponsor != null ? sponsor : Texture2D.blackTexture);
            _material.SetFloat(KitShaderIds.UseSponsor, sponsor != null ? 1f : 0f);
        }

        private void ApplyWear()
        {
            if (_material == null) return;
            _material.SetFloat(KitShaderIds.Sweat, sweat);
            _material.SetFloat(KitShaderIds.Dirt, dirt);
        }

        private void ReleaseResources()
        {
            DestroyTexture(ref _printTexture);
            _printKey = null;
            if (_material != null)
            {
                DestroySafely(_material);
                _material = null;
            }
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null) return;
            DestroySafely(texture);
            texture = null;
        }

        private static void DestroySafely(UnityEngine.Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private static string Initials(string teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName)) return "";
            var parts = teamName.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var result = "";
            foreach (var part in parts)
            {
                if (result.Length >= 3) break;
                result += char.ToUpperInvariant(part[0]);
            }
            return result;
        }

        private static Color ToColor(SimpleColor c)
        {
            return new Color(c.R, c.G, c.B, c.A);
        }
    }
}
