using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// MonoBehaviour that controls Unity Light components based on DynamicLightingLogic.
    /// Manages directional sun light, floodlight spot lights, ambient color,
    /// shadow cascades via URP asset, and light probe updates.
    /// </summary>
    public class DynamicLightingSystem : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Light directionalLight;
        [SerializeField] private Light[] floodlightSpots;
        [SerializeField] private FloodlightEffect[] floodlightEffects;

        [Header("Configuration")]
        [SerializeField] private float transitionDuration = 2f;
        [SerializeField] private bool enableTimeProgression;
        [SerializeField] private float timeScale = 60f;
        [SerializeField] private float startHour = 20f;

        // Core logic (pure C#)
        private LightingDirector _director;
        private TimeOfDaySimulator _timeSimulator;
        private FloodlightController _floodlightController;
        private LightingBlender _blender;
        private ShadowManager _shadowManager;
        private FloodlightBehavior _floodlightBehavior;
        private LightingTransitionConfig _transitionConfig;
        private PitchLightmapConfig _pitchLightmap;
        private ShadowCascadeConfig _dayShadowConfig;
        private ShadowCascadeConfig _nightShadowConfig;

        // Cached references
        private UniversalRenderPipelineAsset _urpAsset;

        /// <summary>Expose the lighting director for external control.</summary>
        public LightingDirector Director => _director;

        /// <summary>Expose the time simulator for external queries.</summary>
        public TimeOfDaySimulator TimeSimulator => _timeSimulator;

        /// <summary>Expose the floodlight controller for external control.</summary>
        public FloodlightController FloodlightCtrl => _floodlightController;

        /// <summary>Current lighting phase.</summary>
        public LightingPhase CurrentPhase => _director?.CurrentPhase ?? LightingPhase.Pregame;

        private void Awake()
        {
            InitializeLogic();
        }

        /// <summary>
        /// Initializes all pure C# logic components. Can be called from tests.
        /// </summary>
        public void InitializeLogic()
        {
            _floodlightBehavior = new FloodlightBehavior();
            _transitionConfig = new LightingTransitionConfig();
            _pitchLightmap = new PitchLightmapConfig();
            _dayShadowConfig = new ShadowCascadeConfig(cascadeCount: 4, maxDistance: 80f);
            _nightShadowConfig = new ShadowCascadeConfig(cascadeCount: 2, maxDistance: 60f);

            _director = new LightingDirector();
            _timeSimulator = new TimeOfDaySimulator(
                startTime: new TimeOfDay(startHour),
                transitionConfig: _transitionConfig,
                timeScale: enableTimeProgression ? timeScale : 0f
            );

            int floodlightCount = floodlightSpots != null ? floodlightSpots.Length : 4;
            _floodlightController = new FloodlightController(floodlightCount, _floodlightBehavior);
            _blender = new LightingBlender();
            _shadowManager = new ShadowManager(_dayShadowConfig, _nightShadowConfig);

            // Subscribe to events
            _director.OnPhaseChanged += HandlePhaseChanged;
            _floodlightController.OnFloodlightStateChanged += HandleFloodlightStateChanged;

            // Cache URP asset
            _urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        }

        private void Start()
        {
            // Set initial floodlight states based on time of day
            UpdateFloodlightStatesFromDirector();
            ApplyLightingFromState();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Advance time
            _timeSimulator.Update(dt);

            // Update floodlight warm-up timers
            _floodlightController.Update(dt);

            // Update blender
            _blender.Update(dt);

            // Apply current lighting state to Unity components
            ApplyLightingFromState();
        }

        /// <summary>
        /// Advances the match lighting phase (e.g., Pregame -> MatchStart).
        /// </summary>
        public void AdvancePhase()
        {
            _director.AdvancePhase();
        }

        /// <summary>
        /// Sets the time of day preset: Day, Dusk, or Night.
        /// </summary>
        public void SetTimeOfDayPreset(string preset)
        {
            switch (preset?.ToLowerInvariant())
            {
                case "day":
                    _timeSimulator.SetTime(new TimeOfDay(12f));
                    break;
                case "dusk":
                    _timeSimulator.SetTime(new TimeOfDay(19.5f));
                    break;
                case "night":
                    _timeSimulator.SetTime(new TimeOfDay(22f));
                    break;
            }

            UpdateFloodlightStatesFromDirector();
            ApplyLightingFromState();
        }

        // -----------------------------------------------------------------
        // Event handlers
        // -----------------------------------------------------------------

        private void HandlePhaseChanged(LightingPhase previousPhase, LightingPhase newPhase)
        {
            UpdateFloodlightStatesFromDirector();

            // Create blend between snapshots
            var fromSnapshot = CreateSnapshotFromCurrentState();
            // Apply new state
            UpdateFloodlightStatesFromDirector();
            var toSnapshot = CreateSnapshotFromCurrentState();

            _blender.StartBlend(fromSnapshot, toSnapshot, transitionDuration);
        }

        private void HandleFloodlightStateChanged(int index, FloodlightState newState)
        {
            // Notify floodlight effects if available
            if (floodlightEffects != null && index < floodlightEffects.Length && floodlightEffects[index] != null)
            {
                floodlightEffects[index].OnStateChanged(newState);
            }
        }

        // -----------------------------------------------------------------
        // State management
        // -----------------------------------------------------------------

        private void UpdateFloodlightStatesFromDirector()
        {
            var recommendedState = _director.GetRecommendedFloodlightState(_timeSimulator.CurrentTime);
            _floodlightController.SetAllState(recommendedState);
        }

        private LightingSnapshot CreateSnapshotFromCurrentState()
        {
            _timeSimulator.GetAmbientColor(out float r, out float g, out float b);

            return new LightingSnapshot
            {
                AmbientR = r,
                AmbientG = g,
                AmbientB = b,
                SunElevation = _timeSimulator.GetSunElevation(),
                SunAzimuth = _timeSimulator.GetSunAzimuth(),
                SunIntensity = _timeSimulator.GetSunIntensity(),
                FloodlightIntensityFactor = _floodlightController.GetIntensityFactor(0),
                FloodlightColorTemperature = _floodlightController.GetColorTemperature(0),
                FloodlightStates = _floodlightController.GetAllStates(),
                ShadowMaxDistance = _shadowManager.GetInterpolatedMaxDistance(_timeSimulator.CurrentTime),
                ShadowCascadeCount = _shadowManager.GetConfigForTime(_timeSimulator.CurrentTime).CascadeCount,
                CurrentTimeOfDay = _timeSimulator.CurrentTime,
                CurrentPhase = _director.CurrentPhase,
            };
        }

        // -----------------------------------------------------------------
        // Apply lighting state to Unity components
        // -----------------------------------------------------------------

        private void ApplyLightingFromState()
        {
            // Use blended snapshot if blending, otherwise compute directly
            LightingSnapshot snapshot;
            if (_blender.IsBlending)
            {
                snapshot = _blender.GetCurrentSnapshot();
                if (snapshot == null) return;
            }
            else
            {
                snapshot = CreateSnapshotFromCurrentState();
            }

            ApplyDirectionalLight(snapshot);
            ApplyFloodlights();
            ApplyAmbient(snapshot);
            ApplyShadowConfig(snapshot);
        }

        private void ApplyDirectionalLight(LightingSnapshot snapshot)
        {
            if (directionalLight == null) return;

            // Rotate sun based on elevation and azimuth
            directionalLight.transform.rotation = Quaternion.Euler(
                snapshot.SunElevation,
                snapshot.SunAzimuth,
                0f
            );

            directionalLight.intensity = snapshot.SunIntensity;

            // Warm color at low elevation, white at high
            float warmth = 1f - (snapshot.SunElevation / 90f);
            directionalLight.color = new Color(
                1f,
                1f - warmth * 0.15f,
                1f - warmth * 0.30f
            );

            directionalLight.enabled = snapshot.SunIntensity > 0.01f;
        }

        private void ApplyFloodlights()
        {
            if (floodlightSpots == null) return;

            for (int i = 0; i < floodlightSpots.Length && i < _floodlightController.FloodlightCount; i++)
            {
                if (floodlightSpots[i] == null) continue;

                float intensity = _floodlightController.GetIntensityFactor(i);
                var state = _floodlightController.GetState(i);

                floodlightSpots[i].intensity = intensity * 150f; // Base floodlight intensity

                // Apply color temperature (approximate: warm = orange, cool = blue-white)
                float colorTemp = _floodlightController.GetColorTemperature(i);
                floodlightSpots[i].color = ColorTemperatureToRGB(colorTemp, state);

                floodlightSpots[i].enabled = state != FloodlightState.Off;

                // Shadow casting only at full power or dimmed
                floodlightSpots[i].shadows = (state == FloodlightState.FullPower || state == FloodlightState.Dimmed)
                    ? LightShadows.Soft
                    : LightShadows.None;
            }
        }

        private void ApplyAmbient(LightingSnapshot snapshot)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(
                snapshot.AmbientR,
                snapshot.AmbientG,
                snapshot.AmbientB
            );
        }

        private void ApplyShadowConfig(LightingSnapshot snapshot)
        {
            if (_urpAsset == null) return;

            _urpAsset.shadowDistance = snapshot.ShadowMaxDistance;
            // Note: Cascade count changes require URP asset modification
            // which is done at a higher level (not per-frame)
        }

        // -----------------------------------------------------------------
        // Utilities
        // -----------------------------------------------------------------

        /// <summary>
        /// Approximate Kelvin color temperature to RGB. Simplified for real-time use.
        /// </summary>
        private static Color ColorTemperatureToRGB(float kelvin, FloodlightState state)
        {
            if (state == FloodlightState.Emergency)
            {
                return new Color(1f, 0.2f, 0.1f); // Red emergency tint
            }

            // Simplified Tanner Helland algorithm
            float temp = kelvin / 100f;
            float r, g, b;

            if (temp <= 66f)
            {
                r = 1f;
                g = Mathf.Clamp01((99.4708025861f * Mathf.Log(temp) - 161.1195681661f) / 255f);
            }
            else
            {
                r = Mathf.Clamp01((329.698727446f * Mathf.Pow(temp - 60f, -0.1332047592f)) / 255f);
                g = Mathf.Clamp01((288.1221695283f * Mathf.Pow(temp - 60f, -0.0755148492f)) / 255f);
            }

            if (temp >= 66f)
            {
                b = 1f;
            }
            else if (temp <= 19f)
            {
                b = 0f;
            }
            else
            {
                b = Mathf.Clamp01((138.5177312231f * Mathf.Log(temp - 10f) - 305.0447927307f) / 255f);
            }

            return new Color(r, g, b);
        }

        private void OnDestroy()
        {
            if (_director != null)
            {
                _director.OnPhaseChanged -= HandlePhaseChanged;
            }

            if (_floodlightController != null)
            {
                _floodlightController.OnFloodlightStateChanged -= HandleFloodlightStateChanged;
            }
        }
    }
}
