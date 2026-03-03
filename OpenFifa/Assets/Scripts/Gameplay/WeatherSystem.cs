using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Master weather controller. Manages rain, snow, fog, and wind particle systems
    /// based on WeatherLogic state. Updates pitch material properties via MaterialPropertyBlock.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        [Header("Weather Preset")]
        [SerializeField] private WeatherPreset _activePreset;
        [SerializeField] private WeatherType _initialWeather = WeatherType.Clear;

        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem _rainParticleSystem;
        [SerializeField] private ParticleSystem _snowParticleSystem;

        [Header("Wind")]
        [SerializeField] private WindZone _windZone;

        [Header("Pitch Material")]
        [SerializeField] private Renderer _pitchRenderer;

        [Header("Performance")]
        [SerializeField] private int _maxRainParticles = 2000;
        [SerializeField] private int _maxSnowParticles = 1500;

        private WeatherLogic _logic;
        private MaterialPropertyBlock _pitchPropertyBlock;

        // Shader property IDs cached for performance
        private static readonly int WetnessPropertyId = Shader.PropertyToID("_Wetness");
        private static readonly int SnowCoveragePropertyId = Shader.PropertyToID("_SnowCoverage");
        private static readonly int MudLevelPropertyId = Shader.PropertyToID("_MudLevel");

        /// <summary>The underlying weather logic (pure C#).</summary>
        public WeatherLogic Logic => _logic;

        /// <summary>Current weather type.</summary>
        public WeatherType CurrentWeather => _logic != null ? _logic.CurrentWeather : _initialWeather;

        /// <summary>Whether a weather transition is in progress.</summary>
        public bool IsTransitioning => _logic != null && _logic.IsTransitioning;

        private void Awake()
        {
            _logic = new WeatherLogic(_initialWeather);
            _pitchPropertyBlock = new MaterialPropertyBlock();

            if (_activePreset != null)
            {
                var configData = _activePreset.ToConfigData();
                _logic.StateMachine.TransitionTo(configData);
                // Immediately complete transition if starting with a preset
                _logic.StateMachine.SetImmediate(_activePreset.WeatherType);
            }

            EnsureRainParticleSystem();
            EnsureSnowParticleSystem();
            ApplyInitialState();
        }

        private void Update()
        {
            _logic.UpdateWeather(Time.deltaTime);

            var particleParams = _logic.GetCurrentParticleParams();
            UpdateRainSystem(particleParams);
            UpdateSnowSystem(particleParams);
            UpdateFog(particleParams);
            UpdateWind(particleParams);
            UpdatePitchMaterial();
        }

        /// <summary>
        /// Transition to a new weather type with blend.
        /// </summary>
        public void SetWeather(WeatherType weather)
        {
            _logic.TransitionTo(weather);
        }

        /// <summary>
        /// Apply a weather preset immediately or with transition.
        /// </summary>
        /// <param name="preset">The weather preset ScriptableObject.</param>
        /// <param name="immediate">If true, apply instantly without blend.</param>
        public void ApplyPreset(WeatherPreset preset, bool immediate = false)
        {
            if (preset == null) return;

            _activePreset = preset;
            var configData = preset.ToConfigData();

            if (immediate)
            {
                _logic.SetImmediate(preset.WeatherType);
            }
            else
            {
                _logic.StateMachine.TransitionTo(configData);
            }
        }

        /// <summary>
        /// Set weather immediately without transition.
        /// </summary>
        public void SetWeatherImmediate(WeatherType weather)
        {
            _logic.SetImmediate(weather);
            ApplyInitialState();
        }

        private void EnsureRainParticleSystem()
        {
            if (_rainParticleSystem != null) return;

            var rainGO = new GameObject("RainParticleSystem");
            rainGO.transform.SetParent(transform);
            rainGO.transform.localPosition = new Vector3(0f, 30f, 0f);
            _rainParticleSystem = rainGO.AddComponent<ParticleSystem>();

            ConfigureRainParticleSystem();
        }

        private void ConfigureRainParticleSystem()
        {
            var main = _rainParticleSystem.main;
            main.startLifetime = 2f;
            main.startSize = 0.03f;
            main.startSpeed = 8f;
            main.maxParticles = _maxRainParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.7f, 0.75f, 0.85f, 0.6f);
            main.gravityModifier = 1.5f;
            main.playOnAwake = false;

            var emission = _rainParticleSystem.emission;
            emission.enabled = false;
            emission.rateOverTime = 0f;

            var shape = _rainParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 1f, 40f);
            shape.rotation = new Vector3(0f, 0f, 0f);

            // Collision for splash effect
            var collision = _rainParticleSystem.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.bounce = 0f;
            collision.lifetimeLoss = 1f;
            collision.sendCollisionMessages = true;

            // Sub-emitter for splash on ground
            ConfigureRainSplashSubEmitter();

            // Color over lifetime: fade at end
            var colorOverLifetime = _rainParticleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.6f, 0f),
                    new GradientAlphaKey(0.3f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;
        }

        private void ConfigureRainSplashSubEmitter()
        {
            // Create splash sub-emitter
            var splashGO = new GameObject("RainSplash");
            splashGO.transform.SetParent(_rainParticleSystem.transform);
            splashGO.transform.localPosition = Vector3.zero;
            var splashPS = splashGO.AddComponent<ParticleSystem>();

            var splashMain = splashPS.main;
            splashMain.startLifetime = 0.2f;
            splashMain.startSize = 0.05f;
            splashMain.startSpeed = 1f;
            splashMain.maxParticles = 200;
            splashMain.simulationSpace = ParticleSystemSimulationSpace.World;
            splashMain.startColor = new Color(0.8f, 0.85f, 0.9f, 0.4f);
            splashMain.playOnAwake = false;

            var splashEmission = splashPS.emission;
            splashEmission.enabled = false;

            var splashShape = splashPS.shape;
            splashShape.shapeType = ParticleSystemShapeType.Hemisphere;
            splashShape.radius = 0.05f;

            // Register as sub-emitter
            var subEmitters = _rainParticleSystem.subEmitters;
            subEmitters.enabled = true;
            subEmitters.AddSubEmitter(splashPS, ParticleSystemSubEmitterType.Collision,
                ParticleSystemSubEmitterProperties.InheritNothing);
        }

        private void EnsureSnowParticleSystem()
        {
            if (_snowParticleSystem != null) return;

            var snowGO = new GameObject("SnowParticleSystem");
            snowGO.transform.SetParent(transform);
            snowGO.transform.localPosition = new Vector3(0f, 25f, 0f);
            _snowParticleSystem = snowGO.AddComponent<ParticleSystem>();

            ConfigureSnowParticleSystem();
        }

        private void ConfigureSnowParticleSystem()
        {
            var main = _snowParticleSystem.main;
            main.startLifetime = 8f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startSpeed = 1.5f;
            main.maxParticles = _maxSnowParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.95f, 0.95f, 1f, 0.8f);
            main.gravityModifier = 0.1f;
            main.playOnAwake = false;

            var emission = _snowParticleSystem.emission;
            emission.enabled = false;
            emission.rateOverTime = 0f;

            var shape = _snowParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 1f, 40f);

            // Noise module for flutter/drift
            var noise = _snowParticleSystem.noise;
            noise.enabled = true;
            noise.strength = 1.5f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.3f;
            noise.octaveCount = 2;

            // Size over lifetime: shrink slightly near end
            var sizeOverLifetime = _snowParticleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.8f, 1f),
                new Keyframe(1f, 0.3f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Color over lifetime
            var colorOverLifetime = _snowParticleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.8f, 0.1f),
                    new GradientAlphaKey(0.8f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;
        }

        private void UpdateRainSystem(WeatherParticleParams particleParams)
        {
            if (_rainParticleSystem == null) return;

            var emission = _rainParticleSystem.emission;
            bool shouldRain = particleParams.RainDropletCount > 0;
            emission.enabled = shouldRain;

            if (shouldRain)
            {
                emission.rateOverTime = particleParams.RainDropletCount;

                var main = _rainParticleSystem.main;
                main.startSpeed = particleParams.RainSpeed;

                // Apply rain angle from wind
                var shape = _rainParticleSystem.shape;
                shape.rotation = new Vector3(particleParams.RainAngle, 0f, 0f);

                if (!_rainParticleSystem.isPlaying)
                    _rainParticleSystem.Play();
            }
            else
            {
                if (_rainParticleSystem.isPlaying)
                    _rainParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void UpdateSnowSystem(WeatherParticleParams particleParams)
        {
            if (_snowParticleSystem == null) return;

            var emission = _snowParticleSystem.emission;
            bool shouldSnow = particleParams.SnowFlakeCount > 0;
            emission.enabled = shouldSnow;

            if (shouldSnow)
            {
                emission.rateOverTime = particleParams.SnowFlakeCount;

                var main = _snowParticleSystem.main;
                main.startSpeed = particleParams.SnowSpeed;

                // Update noise for drift
                var noise = _snowParticleSystem.noise;
                noise.strength = particleParams.SnowDriftAmount;

                if (!_snowParticleSystem.isPlaying)
                    _snowParticleSystem.Play();
            }
            else
            {
                if (_snowParticleSystem.isPlaying)
                    _snowParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void UpdateFog(WeatherParticleParams particleParams)
        {
            RenderSettings.fog = particleParams.FogDensity > 0.001f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = particleParams.FogDensity;
            RenderSettings.fogColor = new Color(
                particleParams.FogColorR,
                particleParams.FogColorG,
                particleParams.FogColorB,
                1f
            );
        }

        private void UpdateWind(WeatherParticleParams particleParams)
        {
            if (_windZone == null) return;

            _windZone.windMain = particleParams.WindStrength;
            _windZone.transform.forward = new Vector3(
                particleParams.WindDirectionX,
                particleParams.WindDirectionY,
                particleParams.WindDirectionZ
            ).normalized;
        }

        private void UpdatePitchMaterial()
        {
            if (_pitchRenderer == null) return;

            _pitchRenderer.GetPropertyBlock(_pitchPropertyBlock);
            _pitchPropertyBlock.SetFloat(WetnessPropertyId, _logic.PitchCondition.Wetness);
            _pitchPropertyBlock.SetFloat(SnowCoveragePropertyId, _logic.PitchCondition.SnowCoverage);
            _pitchPropertyBlock.SetFloat(MudLevelPropertyId, _logic.PitchCondition.MudLevel);
            _pitchRenderer.SetPropertyBlock(_pitchPropertyBlock);
        }

        private void ApplyInitialState()
        {
            var particleParams = _logic.GetCurrentParticleParams();
            UpdateRainSystem(particleParams);
            UpdateSnowSystem(particleParams);
            UpdateFog(particleParams);
            UpdateWind(particleParams);
            UpdatePitchMaterial();
        }

        private void OnDisable()
        {
            // Restore fog to disabled when weather system is turned off
            RenderSettings.fog = false;
        }
    }
}
