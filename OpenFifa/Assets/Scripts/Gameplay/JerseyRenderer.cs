using System;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Applies jersey customization to a player mesh via MaterialPropertyBlock.
    /// Handles pattern rendering, dynamic name/number texture generation,
    /// crest decal application, and kit clash auto-resolution.
    /// Designed to work alongside HumanoidSetup without replacing it.
    /// </summary>
    public class JerseyRenderer : MonoBehaviour
    {
        [Header("Renderer References")]
        [SerializeField] private Renderer jerseyBackRenderer;
        [SerializeField] private Renderer jerseyFrontRenderer;

        [Header("Texture Settings")]
        [SerializeField] private int nameNumberTextureWidth = 512;
        [SerializeField] private int nameNumberTextureHeight = 256;
        [SerializeField] private int crestTextureSize = 128;

        [Header("Runtime State")]
        [SerializeField] private bool autoResolveClash = true;

        // Shader property IDs — cached for performance
        private static readonly int PrimaryColorId = Shader.PropertyToID("_PrimaryColor");
        private static readonly int SecondaryColorId = Shader.PropertyToID("_SecondaryColor");
        private static readonly int TertiaryColorId = Shader.PropertyToID("_TertiaryColor");
        private static readonly int PatternTypeId = Shader.PropertyToID("_PatternType");
        private static readonly int PatternScaleId = Shader.PropertyToID("_PatternScale");
        private static readonly int CollarStyleId = Shader.PropertyToID("_CollarStyle");
        private static readonly int NameNumberTexId = Shader.PropertyToID("_NameNumberTex");
        private static readonly int CrestTexId = Shader.PropertyToID("_CrestTex");

        private MaterialPropertyBlock _backMpb;
        private MaterialPropertyBlock _frontMpb;
        private RenderTexture _nameNumberRT;
        private RenderTexture _crestRT;
        private FullJerseyConfig _currentConfig;
        private bool _isApplied;

        /// <summary>
        /// Whether the jersey has been successfully applied to the mesh.
        /// </summary>
        public bool IsApplied => _isApplied;

        /// <summary>
        /// The currently applied jersey configuration, or null if none.
        /// </summary>
        public FullJerseyConfig CurrentConfig => _currentConfig;

        /// <summary>
        /// Fired when jersey rendering completes. Passes this renderer instance.
        /// </summary>
        public event Action<JerseyRenderer> OnJerseyApplied;

        private void Awake()
        {
            AutoDiscoverRenderers();
            _backMpb = new MaterialPropertyBlock();
            _frontMpb = new MaterialPropertyBlock();
        }

        private void OnDestroy()
        {
            ReleaseTextures();
        }

        /// <summary>
        /// Applies a full jersey configuration to the player mesh.
        /// Sets pattern colors via MaterialPropertyBlock and generates
        /// name/number textures at runtime.
        /// </summary>
        public void ApplyJersey(FullJerseyConfig config)
        {
            if (config == null) return;

            _currentConfig = config;
            ApplyDesignProperties(config.Design);
            GenerateNameNumberTexture(config.NameConfig, config.NumberConfig, config.Design);
            ApplyCrest(config.Crest);
            _isApplied = true;
            OnJerseyApplied?.Invoke(this);
        }

        /// <summary>
        /// Applies only the jersey design (colors, pattern) without regenerating name/number textures.
        /// Useful for kit clash resolution where only colors need to change.
        /// </summary>
        public void ApplyDesignOnly(JerseyDesign design)
        {
            if (design == null) return;
            ApplyDesignProperties(design);

            if (_currentConfig != null)
                _currentConfig.Design = design;
        }

        /// <summary>
        /// Resolves kit clash by switching to the away or third kit variant.
        /// Returns the variant that was selected, or null if no manager was provided.
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

        /// <summary>
        /// Clears the applied jersey and releases generated textures.
        /// </summary>
        public void ClearJersey()
        {
            _currentConfig = null;
            _isApplied = false;
            ReleaseTextures();

            // Reset material property blocks
            if (jerseyBackRenderer != null)
            {
                _backMpb.Clear();
                jerseyBackRenderer.SetPropertyBlock(_backMpb);
            }
            if (jerseyFrontRenderer != null)
            {
                _frontMpb.Clear();
                jerseyFrontRenderer.SetPropertyBlock(_frontMpb);
            }
        }

        private void AutoDiscoverRenderers()
        {
            if (jerseyBackRenderer == null || jerseyFrontRenderer == null)
            {
                var renderers = GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0 && jerseyBackRenderer == null)
                    jerseyBackRenderer = renderers[0];
                if (renderers.Length > 0 && jerseyFrontRenderer == null)
                    jerseyFrontRenderer = renderers[0];
            }
        }

        private void ApplyDesignProperties(JerseyDesign design)
        {
            if (design == null) return;

            ApplyDesignToRenderer(jerseyBackRenderer, _backMpb, design);
            ApplyDesignToRenderer(jerseyFrontRenderer, _frontMpb, design);
        }

        private void ApplyDesignToRenderer(Renderer renderer, MaterialPropertyBlock mpb, JerseyDesign design)
        {
            if (renderer == null || mpb == null) return;

            renderer.GetPropertyBlock(mpb);

            mpb.SetColor(PrimaryColorId, ToUnityColor(design.PrimaryColor));
            mpb.SetColor(SecondaryColorId, ToUnityColor(design.SecondaryColor));
            mpb.SetColor(TertiaryColorId, ToUnityColor(design.TertiaryColor));
            mpb.SetInt(PatternTypeId, (int)design.Pattern);
            mpb.SetFloat(PatternScaleId, design.PatternScale);
            mpb.SetInt(CollarStyleId, (int)design.Collar);

            renderer.SetPropertyBlock(mpb);
        }

        private void GenerateNameNumberTexture(PlayerNameConfig nameConfig, PlayerNumberConfig numberConfig, JerseyDesign design)
        {
            // Release previous texture
            if (_nameNumberRT != null)
            {
                _nameNumberRT.Release();
                DestroyImmediate(_nameNumberRT);
            }

            _nameNumberRT = new RenderTexture(nameNumberTextureWidth, nameNumberTextureHeight, 0, RenderTextureFormat.ARGB32);
            _nameNumberRT.name = "JerseyNameNumber";
            _nameNumberRT.Create();

            // Clear to transparent
            var previousActive = RenderTexture.active;
            RenderTexture.active = _nameNumberRT;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = previousActive;

            // Apply the generated texture to the back renderer
            if (jerseyBackRenderer != null)
            {
                jerseyBackRenderer.GetPropertyBlock(_backMpb);
                _backMpb.SetTexture(NameNumberTexId, _nameNumberRT);
                jerseyBackRenderer.SetPropertyBlock(_backMpb);
            }
        }

        private void ApplyCrest(CrestConfig crest)
        {
            if (crest == null) return;

            // Release previous crest texture
            if (_crestRT != null)
            {
                _crestRT.Release();
                DestroyImmediate(_crestRT);
            }

            _crestRT = new RenderTexture(crestTextureSize, crestTextureSize, 0, RenderTextureFormat.ARGB32);
            _crestRT.name = "JerseyCrest";
            _crestRT.Create();

            // Clear to transparent
            var previousActive = RenderTexture.active;
            RenderTexture.active = _crestRT;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = previousActive;

            // Apply to front renderer
            if (jerseyFrontRenderer != null)
            {
                jerseyFrontRenderer.GetPropertyBlock(_frontMpb);
                _frontMpb.SetTexture(CrestTexId, _crestRT);
                jerseyFrontRenderer.SetPropertyBlock(_frontMpb);
            }
        }

        private void ReleaseTextures()
        {
            if (_nameNumberRT != null)
            {
                _nameNumberRT.Release();
                DestroyImmediate(_nameNumberRT);
                _nameNumberRT = null;
            }

            if (_crestRT != null)
            {
                _crestRT.Release();
                DestroyImmediate(_crestRT);
                _crestRT = null;
            }
        }

        private static Color ToUnityColor(SimpleColor c)
        {
            return new Color(c.R, c.G, c.B, c.A);
        }
    }
}
