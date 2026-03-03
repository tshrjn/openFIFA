using System;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Applies team colors to character models using MaterialPropertyBlock
    /// for GPU instancing compatibility. Supports multi-renderer uniforms
    /// (jersey/shorts/socks) with per-slot color assignment and player numbers.
    /// Configures humanoid avatar and LODGroup references.
    /// </summary>
    public class HumanoidSetup : MonoBehaviour
    {
        [Header("Team Configuration")]
        [SerializeField] private int teamIndex;
        [SerializeField] private int playerNumber = 1;

        [Header("Primary Renderer (fallback if slot renderers not assigned)")]
        [SerializeField] private Renderer targetRenderer;

        [Header("Per-Slot Renderers (optional — enables per-piece uniform coloring)")]
        [SerializeField] private Renderer jerseyRenderer;
        [SerializeField] private Renderer shortsRenderer;
        [SerializeField] private Renderer socksRenderer;

        [Header("Animation")]
        [SerializeField] private Animator animator;

        [Header("LOD")]
        [SerializeField] private LODGroup lodGroup;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int PlayerNumberId = Shader.PropertyToID("_PlayerNumber");

        private MaterialPropertyBlock _jerseyMpb;
        private MaterialPropertyBlock _shortsMpb;
        private MaterialPropertyBlock _socksMpb;
        private MaterialPropertyBlock _fallbackMpb;

        private bool _isSetupComplete;

        /// <summary>
        /// True once all renderers have been configured with team colors.
        /// </summary>
        public bool IsSetupComplete => _isSetupComplete;

        /// <summary>
        /// Fired when setup completes successfully. Passes this HumanoidSetup instance.
        /// </summary>
        public event Action<HumanoidSetup> OnSetupComplete;

        /// <summary>
        /// The team index (0 = Team A, 1 = Team B).
        /// </summary>
        public int TeamIndex => teamIndex;

        /// <summary>
        /// The player's jersey number.
        /// </summary>
        public int PlayerNumber => playerNumber;

        /// <summary>
        /// Whether this setup uses per-slot renderers (jersey/shorts/socks)
        /// rather than a single fallback renderer.
        /// </summary>
        public bool HasSlotRenderers =>
            jerseyRenderer != null || shortsRenderer != null || socksRenderer != null;

        /// <summary>
        /// Whether the Animator component is valid and has a runtime controller assigned.
        /// </summary>
        public bool IsAnimatorValid =>
            animator != null && animator.runtimeAnimatorController != null;

        /// <summary>
        /// Whether a LODGroup is assigned and configured.
        /// </summary>
        public bool HasLODGroup => lodGroup != null;

        private void Awake()
        {
            AutoDiscoverComponents();
            InitializePropertyBlocks();
            ApplyAllTeamColors();
        }

        /// <summary>
        /// Sets the team index and re-applies all team colors.
        /// </summary>
        public void SetTeam(int team)
        {
            teamIndex = team;
            ApplyAllTeamColors();
        }

        /// <summary>
        /// Sets the player number and applies it to the jersey material.
        /// </summary>
        public void SetPlayerNumber(int number)
        {
            playerNumber = number;
            ApplyPlayerNumber();
        }

        /// <summary>
        /// Sets both team and player number in one call, applying all colors.
        /// </summary>
        public void Configure(int team, int number)
        {
            teamIndex = team;
            playerNumber = number;
            ApplyAllTeamColors();
        }

        /// <summary>
        /// Returns the renderer assigned to the given uniform slot,
        /// or the fallback renderer if no slot renderer is assigned.
        /// </summary>
        public Renderer GetRendererForSlot(UniformSlot slot)
        {
            switch (slot)
            {
                case UniformSlot.Jersey: return jerseyRenderer != null ? jerseyRenderer : targetRenderer;
                case UniformSlot.Shorts: return shortsRenderer != null ? shortsRenderer : targetRenderer;
                case UniformSlot.Socks:  return socksRenderer != null ? socksRenderer : targetRenderer;
                default: return targetRenderer;
            }
        }

        /// <summary>
        /// Manually triggers a re-application of all team colors and player number.
        /// Useful after dynamically swapping renderers or changing config.
        /// </summary>
        public void RefreshSetup()
        {
            InitializePropertyBlocks();
            ApplyAllTeamColors();
        }

        private void AutoDiscoverComponents()
        {
            // Auto-discover primary renderer if not assigned
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();

            // Auto-discover animator if not assigned
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            // Auto-discover LODGroup if not assigned
            if (lodGroup == null)
                lodGroup = GetComponent<LODGroup>();
        }

        private void InitializePropertyBlocks()
        {
            _jerseyMpb = new MaterialPropertyBlock();
            _shortsMpb = new MaterialPropertyBlock();
            _socksMpb = new MaterialPropertyBlock();
            _fallbackMpb = new MaterialPropertyBlock();
        }

        private void ApplyAllTeamColors()
        {
            var config = new CharacterModelConfig();
            var assigner = new TeamColorAssigner();

            if (HasSlotRenderers)
            {
                // Per-slot coloring for multi-renderer models
                ApplySlotColor(jerseyRenderer, UniformSlot.Jersey, _jerseyMpb, assigner, config);
                ApplySlotColor(shortsRenderer, UniformSlot.Shorts, _shortsMpb, assigner, config);
                ApplySlotColor(socksRenderer, UniformSlot.Socks, _socksMpb, assigner, config);
            }
            else
            {
                // Fallback: single renderer gets the team's primary jersey color
                ApplySlotColor(targetRenderer, UniformSlot.Jersey, _fallbackMpb, assigner, config);
            }

            ApplyPlayerNumber();
            MarkSetupComplete();
        }

        private void ApplySlotColor(
            Renderer renderer,
            UniformSlot slot,
            MaterialPropertyBlock mpb,
            TeamColorAssigner assigner,
            CharacterModelConfig config)
        {
            if (renderer == null || mpb == null) return;

            SimpleColor c = assigner.GetSlotColor(teamIndex, slot, config);

            renderer.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, new Color(c.R, c.G, c.B, c.A));
            renderer.SetPropertyBlock(mpb);
        }

        private void ApplyPlayerNumber()
        {
            // Apply player number to jersey renderer (or fallback)
            var jersey = jerseyRenderer != null ? jerseyRenderer : targetRenderer;
            if (jersey == null) return;

            var mpb = jerseyRenderer != null ? _jerseyMpb : _fallbackMpb;
            jersey.GetPropertyBlock(mpb);
            mpb.SetFloat(PlayerNumberId, playerNumber);
            jersey.SetPropertyBlock(mpb);
        }

        private void MarkSetupComplete()
        {
            _isSetupComplete = true;
            OnSetupComplete?.Invoke(this);
        }
    }
}
