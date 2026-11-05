using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Master weather controller. Manages rain, snow, fog, and wind particle systems
    /// based on WeatherLogic state. Updates the pitch's wet look (URP/Lit _BaseColor and _Smoothness) via a
    /// MaterialPropertyBlock.
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

        /// <summary>
        /// Optional camera to keep the precipitation in front of (broadcast view). Null = emitters stay where they
        /// are under this object.
        /// </summary>
        public Transform FollowCamera;
        /// <summary>
        /// Metres from the camera's ground point to the far touchline along its (flat) forward; the precipitation box
        /// (<see cref="PrecipitationVolume"/>) reaches just past it.
        /// </summary>
        public float FarReach = 100f;
        /// <summary>
        /// Camera-to-far-touchline distance (m) the fog density is solved for (<see cref="BroadcastFog.Density"/>);
        /// 0 = use the preset's exp² density unchanged (close camera).
        /// </summary>
        public float FogReferenceDistance;
        /// <summary>Multiplier on the preset's droplet/flake rate (a wide broadcast view needs denser rain to read).</summary>
        public float EmissionScale = 1f;

        private WeatherLogic _logic;
        private MaterialPropertyBlock _pitchPropertyBlock;
        private bool _broadcast;
        private PrecipitationBox _box;
        private Color _pitchBaseColor = Color.white;
        private float _pitchSmoothness = 1f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private const float MaxRainLifetime = 3f, MaxSnowLifetime = 12f, RainGravityModifier = 1.5f, SnowGravityModifier = 0.1f;

        /// <summary>
        /// Turf renderer (URP/Lit) that gets the wet look. Its material's _BaseColor and _Smoothness are the dry
        /// values; the look is applied on top through a property block, so the shared material is not changed.
        /// </summary>
        public Renderer PitchRenderer
        {
            get => _pitchRenderer;
            set
            {
                if (_pitchRenderer != null && _pitchRenderer != value) _pitchRenderer.SetPropertyBlock(null);
                _pitchRenderer = value;
                var m = value != null ? value.sharedMaterial : null;
                _pitchBaseColor = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
                _pitchSmoothness = m != null && m.HasProperty(SmoothnessId) ? m.GetFloat(SmoothnessId) : 1f;
                UpdatePitchMaterial();
            }
        }

        /// <summary>The wet look currently applied to <see cref="PitchRenderer"/>.</summary>
        public PitchWetnessLook PitchLook { get; private set; }

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
            if (_pitchRenderer != null) PitchRenderer = _pitchRenderer; // capture the dry colour/smoothness
            ApplyInitialState();
        }

        /// <summary>
        /// Sets the particle material and look for a far broadcast camera: stretched rain streaks, larger flakes, the
        /// emitter box from <see cref="PrecipitationVolume"/> (kept clear of the camera, across the whole visible pitch)
        /// and the quality tier's particle budget. Call after the component has woken (e.g. right after AddComponent).
        /// </summary>
        public void ConfigureForBroadcast(Material particleMaterial, int maxRainParticles, int maxSnowParticles)
        {
            _broadcast = true;
            _maxRainParticles = Mathf.Max(100, maxRainParticles);
            _maxSnowParticles = Mathf.Max(100, maxSnowParticles);
            if (_rainParticleSystem != null)
            {
                var rr = _rainParticleSystem.GetComponent<ParticleSystemRenderer>();
                if (particleMaterial != null) rr.sharedMaterial = particleMaterial;
                rr.renderMode = ParticleSystemRenderMode.Stretch;
                rr.velocityScale = 0.04f;
                rr.lengthScale = 1f;
                var main = _rainParticleSystem.main;
                main.startSize = 0.05f;
                main.maxParticles = _maxRainParticles;
                var collision = _rainParticleSystem.collision;
                collision.enabled = false; // no colliders on the procedural pitch; saves the per-particle raycasts
                var subs = _rainParticleSystem.subEmitters;
                subs.enabled = false;
            }
            if (_snowParticleSystem != null)
            {
                var sr = _snowParticleSystem.GetComponent<ParticleSystemRenderer>();
                if (particleMaterial != null) sr.sharedMaterial = particleMaterial;
                var main = _snowParticleSystem.main;
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
                main.maxParticles = _maxSnowParticles;
            }
            FollowBroadcastCamera();
            ApplyInitialState();
        }

        private void LateUpdate()
        {
            FollowBroadcastCamera();
        }

        /// <summary>
        /// Yaws this object with the camera and sits it on the ground under the <see cref="PrecipitationVolume"/> box;
        /// the emitters sit at the box's height and take its size.
        /// </summary>
        private void FollowBroadcastCamera()
        {
            if (FollowCamera == null || _logic == null) return;
            Vector3 fwd = FollowCamera.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            var p = _logic.GetCurrentParticleParams();
            Vector3 cam = FollowCamera.position;
            _box = PrecipitationVolume.Compute(cam.x, cam.y, cam.z, fwd.x, fwd.z, FarReach, p.RainAngle);
            transform.SetPositionAndRotation(new Vector3(_box.CenterX, 0f, _box.CenterZ), Quaternion.LookRotation(fwd, Vector3.up));
            PlaceEmitter(_rainParticleSystem);
            PlaceEmitter(_snowParticleSystem);
        }

        private void PlaceEmitter(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.transform.localPosition = new Vector3(0f, _box.EmitterHeight, 0f);
            ps.transform.localRotation = Quaternion.identity;
            var shape = ps.shape;
            shape.scale = new Vector3(_box.Width, _box.Depth, 1f); // local Y (depth) becomes the forward axis once pitched down
        }

        /// <summary>The current emitter box (broadcast mode).</summary>
        public PrecipitationBox Box => _box;

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
            // Kicking off in rain: the pitch has already been rained on.
            if (weather == WeatherType.LightRain || weather == WeatherType.HeavyRain) _logic.PitchCondition.SetWetness(1f);
            FollowBroadcastCamera();
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
            main.gravityModifier = RainGravityModifier;
            main.playOnAwake = false;

            var emission = _rainParticleSystem.emission;
            emission.enabled = false;
            emission.rateOverTime = 0f;

            var shape = _rainParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 40f, 1f); // local Y becomes world Z once pitched down
            shape.rotation = new Vector3(90f, 0f, 0f); // emit downwards (a Box emits along its local +Z)

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
            main.gravityModifier = SnowGravityModifier;
            main.playOnAwake = false;

            var emission = _snowParticleSystem.emission;
            emission.enabled = false;
            emission.rateOverTime = 0f;

            var shape = _snowParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 40f, 1f); // local Y becomes world Z once pitched down
            shape.rotation = new Vector3(90f, 0f, 0f); // emit downwards (a Box emits along its local +Z)

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
                var main = _rainParticleSystem.main;
                main.startSpeed = particleParams.RainSpeed;
                if (_broadcast)
                {
                    float life = PrecipitationVolume.FallTime(_box.EmitterHeight, particleParams.RainSpeed, particleParams.RainAngle,
                        RainGravityModifier * Physics.gravity.magnitude, MaxRainLifetime);
                    main.startLifetime = life;
                    emission.rateOverTime = PrecipitationVolume.EmissionRate(particleParams.RainDropletCount, EmissionScale,
                        _box.Width * _box.Depth, life, _maxRainParticles);
                }
                else
                {
                    emission.rateOverTime = particleParams.RainDropletCount * EmissionScale;
                }

                // Apply rain angle from wind
                var shape = _rainParticleSystem.shape;
                // A Box shape emits along its local +Z: pitch it 90 deg so drops leave downwards, tilted by the wind angle.
                shape.rotation = new Vector3(90f - particleParams.RainAngle, 0f, 0f);

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
                var main = _snowParticleSystem.main;
                main.startSpeed = particleParams.SnowSpeed;
                if (_broadcast)
                {
                    float life = PrecipitationVolume.FallTime(_box.EmitterHeight, particleParams.SnowSpeed, 0f,
                        SnowGravityModifier * Physics.gravity.magnitude, MaxSnowLifetime);
                    main.startLifetime = life;
                    emission.rateOverTime = PrecipitationVolume.EmissionRate(particleParams.SnowFlakeCount, EmissionScale,
                        _box.Width * _box.Depth, life, _maxSnowParticles);
                }
                else
                {
                    emission.rateOverTime = particleParams.SnowFlakeCount * EmissionScale;
                }

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
            float density = BroadcastFog.Density(particleParams.FogDensity, FogReferenceDistance);
            RenderSettings.fog = particleParams.FogDensity > 0.001f;
            RenderSettings.fogMode = FogMode.ExponentialSquared; // the only fog variant kept in builds (GraphicsSettings)
            RenderSettings.fogDensity = density;
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
            if (_pitchRenderer == null || _logic == null) return;
            if (_pitchPropertyBlock == null) _pitchPropertyBlock = new MaterialPropertyBlock();

            var sm = _logic.StateMachine;
            float reflectivity = sm.CurrentConfig.PitchEffect.WetReflectivity;
            if (sm.IsTransitioning && sm.TargetConfig != null)
                reflectivity = Mathf.Lerp(reflectivity, sm.TargetConfig.PitchEffect.WetReflectivity, sm.TransitionProgress);
            var look = PitchWetnessLook.For(sm.CurrentWeatherType, reflectivity, _logic.PitchCondition.Wetness);
            PitchLook = look;

            _pitchRenderer.GetPropertyBlock(_pitchPropertyBlock);
            var c = _pitchBaseColor;
            _pitchPropertyBlock.SetColor(BaseColorId, new Color(c.r * look.AlbedoScale, c.g * look.AlbedoScale, c.b * look.AlbedoScale, c.a));
            _pitchPropertyBlock.SetFloat(SmoothnessId, _pitchSmoothness * look.SmoothnessMultiplier);
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
            if (_pitchRenderer != null) _pitchRenderer.SetPropertyBlock(null); // dry look
        }

        private void OnEnable()
        {
            if (_logic != null) ApplyInitialState();
        }
    }
}
