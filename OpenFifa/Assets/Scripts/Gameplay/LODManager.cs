using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Registered LOD object entry, storing the object's transform, renderer, profile,
    /// current LOD level, and whether it is a character (subject to LOD0 limits).
    /// </summary>
    public class LODRegistration
    {
        public Transform ObjectTransform;
        public Renderer[] Renderers;
        public LODProfile Profile;
        public LODLevel CurrentLevel;
        public bool IsCharacter;
        public float ObjectHeight;
        public int[] TrianglesPerLevel;
        public LODGroup UnityLODGroup;
        public float CrossFadeAmount;
    }

    /// <summary>
    /// Master LOD controller MonoBehaviour. Runs per-frame LOD evaluation for all
    /// registered LOD-capable objects. Configures Unity LODGroup components at runtime
    /// and applies cross-fade dithering via MaterialPropertyBlock.
    /// </summary>
    public class LODManager : MonoBehaviour
    {
        [SerializeField] private float _verticalFOV = 60f;
        [SerializeField] private float _biasStep = 0.005f;
        [SerializeField] private float _minBias = -0.15f;
        [SerializeField] private float _maxBias = 0.10f;
        [SerializeField] private int _maxActiveLOD0Characters = 6;
        [SerializeField] private int _maxTotalTriangles = 500000;
        [SerializeField] private float _targetFPS = 60f;

        private static readonly int DitherAmountId = Shader.PropertyToID("_DitherAmount");

        private readonly List<LODRegistration> _registrations = new List<LODRegistration>();
        private LODSystemConfig _config;
        private LODBudgetManager _budgetManager;
        private LODSelector _selector;
        private DynamicLODAdjuster _adjuster;
        private Camera _mainCamera;
        private LODStatistics _lastStatistics;
        private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

        /// <summary>Number of currently registered LOD objects.</summary>
        public int RegisteredCount => _registrations.Count;

        /// <summary>Most recent LOD statistics snapshot.</summary>
        public LODStatistics LastStatistics => _lastStatistics;

        /// <summary>The active LOD system configuration.</summary>
        public LODSystemConfig Config => _config;

        /// <summary>Event fired after each LOD evaluation pass completes.</summary>
        public event Action<LODStatistics> OnLODEvaluated;

        private void Awake()
        {
            InitializeSystem();
        }

        /// <summary>
        /// Initializes the LOD system from serialized fields.
        /// Can be called externally to re-initialize with new settings.
        /// </summary>
        public void InitializeSystem()
        {
            var budget = new LODBudgetConfig(
                maxActiveLOD0Characters: _maxActiveLOD0Characters,
                maxTotalTriangles: _maxTotalTriangles,
                targetFPS: _targetFPS);

            _config = new LODSystemConfig(budget: budget);
            _budgetManager = new LODBudgetManager(budget);
            _selector = new LODSelector(budget, _config.Transition);
            _adjuster = new DynamicLODAdjuster(budget, _biasStep, _minBias, _maxBias);
        }

        /// <summary>
        /// Initializes the LOD system with an explicit config (useful for testing).
        /// </summary>
        public void InitializeSystem(LODSystemConfig config)
        {
            _config = config ?? LODSystemConfig.CreateDefault();
            _budgetManager = new LODBudgetManager(_config.Budget);
            _selector = new LODSelector(_config.Budget, _config.Transition);
            _adjuster = new DynamicLODAdjuster(_config.Budget, _biasStep, _minBias, _maxBias);
        }

        private void LateUpdate()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            float frameTime = Time.unscaledDeltaTime;
            _adjuster.Update(frameTime);

            EvaluateAllLODs();
        }

        /// <summary>
        /// Registers an LOD-capable object with the manager.
        /// </summary>
        /// <param name="objectTransform">Transform of the object.</param>
        /// <param name="renderers">All renderers associated with the object.</param>
        /// <param name="profile">LOD profile for this object.</param>
        /// <param name="isCharacter">Whether this is a player character (subject to LOD0 limits).</param>
        /// <param name="objectHeight">World-space bounding height of the object.</param>
        /// <param name="trianglesPerLevel">Triangle count at each LOD level (indexed by LODLevel).</param>
        /// <param name="lodGroup">Optional Unity LODGroup component for hardware LOD support.</param>
        /// <returns>The registration handle for later unregistration.</returns>
        public LODRegistration Register(
            Transform objectTransform,
            Renderer[] renderers,
            LODProfile profile,
            bool isCharacter,
            float objectHeight,
            int[] trianglesPerLevel,
            LODGroup lodGroup = null)
        {
            if (objectTransform == null) return null;

            var reg = new LODRegistration
            {
                ObjectTransform = objectTransform,
                Renderers = renderers ?? new Renderer[0],
                Profile = profile,
                CurrentLevel = LODLevel.LOD0_High,
                IsCharacter = isCharacter,
                ObjectHeight = objectHeight,
                TrianglesPerLevel = trianglesPerLevel ?? new int[5],
                UnityLODGroup = lodGroup,
                CrossFadeAmount = 0f
            };

            _registrations.Add(reg);
            return reg;
        }

        /// <summary>
        /// Unregisters an LOD object from the manager.
        /// </summary>
        public void Unregister(LODRegistration registration)
        {
            if (registration != null)
            {
                _registrations.Remove(registration);
            }
        }

        /// <summary>
        /// Evaluates LOD for all registered objects based on camera distance and budget.
        /// </summary>
        private void EvaluateAllLODs()
        {
            if (_budgetManager == null || _selector == null || _mainCamera == null) return;

            _budgetManager.Reset();

            // Apply dynamic bias to all profiles
            _adjuster.ApplyBias(_config.CharacterProfile);
            _adjuster.ApplyBias(_config.StadiumProfile);
            _adjuster.ApplyBias(_config.CrowdProfile);

            float fov = _verticalFOV;
            if (_mainCamera != null)
            {
                fov = _mainCamera.fieldOfView;
            }

            Vector3 cameraPos = _mainCamera.transform.position;

            for (int i = 0; i < _registrations.Count; i++)
            {
                var reg = _registrations[i];
                if (reg.ObjectTransform == null) continue;

                float distance = Vector3.Distance(cameraPos, reg.ObjectTransform.position);
                float screenHeight = LODDistanceCalculator.ComputeScreenHeight(
                    reg.ObjectHeight, distance, fov);

                int lod0Tris = reg.TrianglesPerLevel.Length > 0 ? reg.TrianglesPerLevel[0] : 0;

                LODLevel newLevel = _selector.SelectLevelWithBudget(
                    reg.Profile,
                    screenHeight,
                    _budgetManager.LOD0CharacterCount,
                    _budgetManager.TotalTriangles,
                    lod0Tris);

                // Compute cross-fade
                if (_config.Transition.CrossFadeDuration > 0f)
                {
                    reg.CrossFadeAmount = _selector.ComputeCrossFadeAmount(
                        reg.Profile, screenHeight, newLevel);
                }

                // Update level tracking
                int newLevelIdx = (int)newLevel;
                int triCount = newLevelIdx < reg.TrianglesPerLevel.Length
                    ? reg.TrianglesPerLevel[newLevelIdx] : 0;

                _budgetManager.Register(newLevel, triCount, reg.IsCharacter);

                // Apply LOD change
                if (newLevel != reg.CurrentLevel)
                {
                    reg.CurrentLevel = newLevel;
                }

                // Apply dithering via MaterialPropertyBlock
                ApplyDithering(reg);
            }

            _lastStatistics = _budgetManager.GetStatistics(
                _adjuster.CurrentBias,
                Time.unscaledDeltaTime);

            OnLODEvaluated?.Invoke(_lastStatistics);
        }

        /// <summary>
        /// Applies dither amount to all renderers on the object for cross-fade transition.
        /// </summary>
        private void ApplyDithering(LODRegistration reg)
        {
            if (reg.Renderers == null) return;

            for (int i = 0; i < reg.Renderers.Length; i++)
            {
                var renderer = reg.Renderers[i];
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(DitherAmountId, reg.CrossFadeAmount);
                renderer.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>
        /// Configures a Unity LODGroup component at runtime based on a LODProfile.
        /// </summary>
        /// <param name="lodGroup">The Unity LODGroup to configure.</param>
        /// <param name="profile">LOD profile providing screen height thresholds.</param>
        /// <param name="lodRenderers">Array of renderer arrays, one per LOD level.</param>
        public static void ConfigureLODGroup(LODGroup lodGroup, LODProfile profile, Renderer[][] lodRenderers)
        {
            if (lodGroup == null || profile == null || lodRenderers == null) return;

            int levelCount = Math.Min(lodRenderers.Length, 4);
            var lods = new LOD[levelCount];

            float[] thresholds = new float[]
            {
                profile.LOD0ScreenHeight,
                profile.LOD1ScreenHeight,
                profile.LOD2ScreenHeight,
                profile.CullScreenHeight
            };

            for (int i = 0; i < levelCount; i++)
            {
                lods[i] = new LOD(thresholds[i], lodRenderers[i] ?? new Renderer[0]);
            }

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            if (profile.CrossFadeWidth > 0f)
            {
                lodGroup.fadeMode = LODFadeMode.CrossFade;
                lodGroup.animateCrossFading = true;
            }
        }

        /// <summary>
        /// Removes all registrations and resets the budget tracker.
        /// </summary>
        public void Clear()
        {
            _registrations.Clear();
            _budgetManager?.Reset();
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
