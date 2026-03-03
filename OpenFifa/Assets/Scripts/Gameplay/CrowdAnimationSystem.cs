using System.Collections.Generic;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Drives crowd animation by updating per-section material properties
    /// based on CrowdDirector state. Subscribes to game events and feeds
    /// them into the pure C# CrowdDirector. Animates crowd quads via
    /// MaterialPropertyBlock for GPU-driven batched updates.
    /// Built on top of CrowdPlacementSystem (US-046) and CrowdReactionLogic (US-025).
    /// </summary>
    public class CrowdAnimationSystem : MonoBehaviour
    {
        [SerializeField] private Transform _ball;
        [SerializeField] private float _pitchHalfLength = 25f;
        [SerializeField] private float _pitchHalfWidth = 15f;
        [SerializeField] private int _sectionCount = 8;
        [SerializeField] private float _idleSwayAmplitude = 0.05f;
        [SerializeField] private float _idleSwayFrequency = 0.8f;
        [SerializeField] private float _cheerVerticalAmplitude = 0.15f;
        [SerializeField] private float _cheerFrequency = 3f;
        [SerializeField] private float _waveVerticalAmplitude = 0.25f;
        [SerializeField] private float _standingYOffset = 0.1f;

        private CrowdDirector _director;
        private CrowdAnimationConfig _config;
        private MexicanWaveController _waveController;

        // Per-section renderer lists for batched material property updates
        private readonly List<List<Renderer>> _sectionRenderers = new List<List<Renderer>>();
        private readonly List<MaterialPropertyBlock> _sectionPropertyBlocks = new List<MaterialPropertyBlock>();

        // Shader property IDs (cached)
        private static readonly int AnimFrameId = Shader.PropertyToID("_AnimFrame");
        private static readonly int AnimSpeedId = Shader.PropertyToID("_AnimSpeed");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int YOffsetId = Shader.PropertyToID("_YOffset");

        /// <summary>The underlying CrowdDirector.</summary>
        public CrowdDirector Director => _director;

        /// <summary>The animation config in use.</summary>
        public CrowdAnimationConfig AnimConfig => _config;

        /// <summary>Number of sections being animated.</summary>
        public int SectionCount => _sectionCount;

        private void Awake()
        {
            _config = new CrowdAnimationConfig(sectionCount: _sectionCount);

            var affiliations = new List<SectionAffiliation>
            {
                SectionAffiliation.Home,    // 0 - North
                SectionAffiliation.Away,    // 1 - South
                SectionAffiliation.Home,    // 2 - East
                SectionAffiliation.Away,    // 3 - West
                SectionAffiliation.Home,    // 4 - NE
                SectionAffiliation.Neutral, // 5 - NW
                SectionAffiliation.Away,    // 6 - SE
                SectionAffiliation.Neutral  // 7 - SW
            };

            _director = new CrowdDirector(_config, affiliations);
        }

        private void Start()
        {
            GatherSectionRenderers();
            InitializePropertyBlocks();

            _waveController = GetComponent<MexicanWaveController>();
        }

        private void OnEnable()
        {
            GoalDetector.OnGoalScored += HandleGoalScored;
        }

        private void OnDisable()
        {
            GoalDetector.OnGoalScored -= HandleGoalScored;
        }

        private void Update()
        {
            if (_director == null) return;

            float deltaTime = Time.deltaTime;
            _director.Update(deltaTime);

            UpdateSectionVisuals(deltaTime);
        }

        /// <summary>
        /// Initialize with custom config and affiliations (for testing).
        /// </summary>
        public void Initialize(CrowdAnimationConfig config, IList<SectionAffiliation> affiliations = null)
        {
            _config = config;
            _sectionCount = config.SectionCount;
            _director = new CrowdDirector(config, affiliations);
        }

        /// <summary>
        /// Feed a game event into the crowd director.
        /// </summary>
        public void NotifyEvent(CrowdGameEvent gameEvent, TeamIdentifier team = TeamIdentifier.TeamA,
            int scoreDifference = 0, float eventX = 0f, float eventZ = 0f)
        {
            _director?.HandleEvent(gameEvent, team, scoreDifference, eventX, eventZ);
        }

        private void HandleGoalScored(TeamIdentifier team)
        {
            float eventX = _ball != null ? _ball.position.x : 0f;
            float eventZ = _ball != null ? _ball.position.z : 0f;

            _director?.HandleEvent(CrowdGameEvent.Goal, team, 0, eventX, eventZ);
        }

        /// <summary>
        /// Gather renderers from CrowdSection_N GameObjects under Crowd root.
        /// </summary>
        private void GatherSectionRenderers()
        {
            _sectionRenderers.Clear();

            for (int i = 0; i < _sectionCount; i++)
            {
                _sectionRenderers.Add(new List<Renderer>());
            }

            // Find the Crowd root
            var crowdRoot = transform.Find("Crowd");
            if (crowdRoot == null)
            {
                // Try finding in scene
                var crowdObj = GameObject.Find("Crowd");
                if (crowdObj != null)
                    crowdRoot = crowdObj.transform;
            }

            if (crowdRoot == null) return;

            for (int s = 0; s < _sectionCount; s++)
            {
                var sectionTransform = crowdRoot.Find($"CrowdSection_{s}");
                if (sectionTransform == null) continue;

                var renderers = sectionTransform.GetComponentsInChildren<Renderer>();
                _sectionRenderers[s].AddRange(renderers);
            }
        }

        private void InitializePropertyBlocks()
        {
            _sectionPropertyBlocks.Clear();
            for (int i = 0; i < _sectionCount; i++)
            {
                _sectionPropertyBlocks.Add(new MaterialPropertyBlock());
            }
        }

        /// <summary>
        /// Update per-section material properties and vertex offsets based on CrowdDirector state.
        /// Batched per section for performance.
        /// </summary>
        private void UpdateSectionVisuals(float deltaTime)
        {
            float time = Time.time;

            for (int s = 0; s < _sectionCount && s < _sectionRenderers.Count; s++)
            {
                if (s >= _director.Sections.Count) break;

                var section = _director.Sections[s];
                var renderers = _sectionRenderers[s];
                if (renderers.Count == 0) continue;

                var propBlock = _sectionPropertyBlocks[s];

                // Calculate animation parameters from section state
                int animFrame;
                float animSpeed;
                float intensity;
                float yOffset;
                CalculateAnimParams(section, time, out animFrame, out animSpeed, out intensity, out yOffset);

                // Set properties on the MaterialPropertyBlock
                propBlock.SetInt(AnimFrameId, animFrame);
                propBlock.SetFloat(AnimSpeedId, animSpeed);
                propBlock.SetFloat(IntensityId, intensity);
                propBlock.SetFloat(YOffsetId, yOffset);

                // Apply to all renderers in this section (batched)
                for (int r = 0; r < renderers.Count; r++)
                {
                    if (renderers[r] != null)
                        renderers[r].SetPropertyBlock(propBlock);
                }
            }
        }

        /// <summary>
        /// Map CrowdAnimState to shader animation parameters.
        /// </summary>
        private void CalculateAnimParams(CrowdSectionBehavior section, float time,
            out int animFrame, out float animSpeed, out float intensity, out float yOffset)
        {
            float sectionIntensity = section.Intensity;
            float blend = section.BlendProgress;

            switch (section.CurrentState)
            {
                case CrowdAnimState.Seated:
                    animFrame = 0;
                    animSpeed = 0f;
                    intensity = sectionIntensity * 0.1f;
                    yOffset = 0f;
                    break;

                case CrowdAnimState.Idle:
                    animFrame = 1;
                    animSpeed = _idleSwayFrequency;
                    intensity = sectionIntensity;
                    // Gentle idle sway using sin wave
                    yOffset = Mathf.Sin(time * _idleSwayFrequency + section.SectionIndex * 0.5f)
                        * _idleSwayAmplitude * sectionIntensity;
                    break;

                case CrowdAnimState.Cheering:
                    animFrame = 2;
                    animSpeed = _cheerFrequency;
                    intensity = sectionIntensity;
                    yOffset = Mathf.Abs(Mathf.Sin(time * _cheerFrequency + section.SectionIndex * 0.3f))
                        * _cheerVerticalAmplitude * sectionIntensity;
                    break;

                case CrowdAnimState.Booing:
                    animFrame = 3;
                    animSpeed = 1.5f;
                    intensity = sectionIntensity;
                    yOffset = Mathf.Sin(time * 2f + section.SectionIndex) * 0.03f * sectionIntensity;
                    break;

                case CrowdAnimState.Standing:
                    animFrame = 4;
                    animSpeed = 0.5f;
                    intensity = sectionIntensity;
                    yOffset = _standingYOffset * sectionIntensity * blend;
                    break;

                case CrowdAnimState.WaveLeft:
                case CrowdAnimState.WaveRight:
                    animFrame = section.CurrentState == CrowdAnimState.WaveLeft ? 5 : 6;
                    animSpeed = 2f;
                    intensity = 1f;
                    // Wave: stand up -> arms up -> sit down over duration
                    float waveProgress = section.StateDuration > 0f
                        ? section.StateElapsed / section.StateDuration
                        : 0f;
                    // Bell curve for wave motion
                    float waveCurve = Mathf.Sin(waveProgress * Mathf.PI);
                    yOffset = waveCurve * _waveVerticalAmplitude;
                    break;

                case CrowdAnimState.Celebrating:
                    animFrame = 7;
                    animSpeed = _cheerFrequency * 1.5f;
                    intensity = sectionIntensity;
                    // Excited bouncing
                    yOffset = Mathf.Abs(Mathf.Sin(time * _cheerFrequency * 1.5f + section.SectionIndex * 0.2f))
                        * _cheerVerticalAmplitude * 1.5f * sectionIntensity;
                    break;

                case CrowdAnimState.Dejected:
                    animFrame = 8;
                    animSpeed = 0.3f;
                    intensity = sectionIntensity * 0.5f;
                    yOffset = -0.02f * sectionIntensity; // Slight droop
                    break;

                default:
                    animFrame = 1;
                    animSpeed = _idleSwayFrequency;
                    intensity = 0.3f;
                    yOffset = 0f;
                    break;
            }

            // Apply blend progress for smooth transitions
            if (blend < 1f)
            {
                yOffset *= blend;
                intensity = Mathf.Lerp(0.3f, intensity, blend);
            }
        }
    }
}
