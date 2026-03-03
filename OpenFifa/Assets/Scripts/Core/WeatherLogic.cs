namespace OpenFifa.Core
{
    /// <summary>
    /// Interpolated particle parameters returned by WeatherLogic.
    /// All values are blended between source and target weather states.
    /// </summary>
    public struct WeatherParticleParams
    {
        public int RainDropletCount;
        public float RainSpeed;
        public float RainAngle;
        public float RainSplashRate;
        public int SnowFlakeCount;
        public float SnowSpeed;
        public float SnowDriftAmount;
        public float FogDensity;
        public float FogColorR;
        public float FogColorG;
        public float FogColorB;
        public float WindDirectionX;
        public float WindDirectionY;
        public float WindDirectionZ;
        public float WindStrength;
    }

    /// <summary>
    /// Tracks pitch surface conditions (wetness, snow coverage, mud level)
    /// over time based on the active weather. Pure C# — no Unity dependency.
    /// </summary>
    public class PitchConditionTracker
    {
        private float _wetness;
        private float _snowCoverage;
        private float _mudLevel;

        /// <summary>Pitch wetness (0-1). Increases during rain, decreases during clear.</summary>
        public float Wetness => _wetness;

        /// <summary>Snow coverage on pitch (0-1). Increases during snow.</summary>
        public float SnowCoverage => _snowCoverage;

        /// <summary>Mud level near goal areas (0-1). Increases when wetness is high.</summary>
        public float MudLevel => _mudLevel;

        /// <summary>Rate at which wetness increases per second during rain.</summary>
        public const float WetnessIncreaseRate = 0.05f;

        /// <summary>Rate at which wetness decreases per second when not raining.</summary>
        public const float WetnessDecreaseRate = 0.02f;

        /// <summary>Rate at which mud increases when wetness exceeds threshold.</summary>
        public const float MudIncreaseRate = 0.03f;

        /// <summary>Rate at which mud decreases when wetness is below threshold.</summary>
        public const float MudDecreaseRate = 0.01f;

        public PitchConditionTracker()
        {
            _wetness = 0f;
            _snowCoverage = 0f;
            _mudLevel = 0f;
        }

        /// <summary>
        /// Update pitch conditions based on current weather config and elapsed time.
        /// </summary>
        /// <param name="weather">Current weather config data.</param>
        /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
        public void Update(WeatherConfigData weather, float deltaTime)
        {
            if (weather == null || deltaTime <= 0f) return;

            UpdateWetness(weather, deltaTime);
            UpdateSnowCoverage(weather, deltaTime);
            UpdateMudLevel(weather, deltaTime);
        }

        /// <summary>Reset all pitch conditions to dry/clean state.</summary>
        public void Reset()
        {
            _wetness = 0f;
            _snowCoverage = 0f;
            _mudLevel = 0f;
        }

        private void UpdateWetness(WeatherConfigData weather, float deltaTime)
        {
            bool isRaining = weather.Type == WeatherType.LightRain ||
                             weather.Type == WeatherType.HeavyRain;

            if (isRaining)
            {
                // Heavier rain = faster wetness increase
                float intensityMultiplier = weather.Type == WeatherType.HeavyRain ? 2f : 1f;
                _wetness += WetnessIncreaseRate * intensityMultiplier * deltaTime;
            }
            else
            {
                _wetness -= WetnessDecreaseRate * deltaTime;
            }

            _wetness = Clamp(_wetness, 0f, 1f);
        }

        private void UpdateSnowCoverage(WeatherConfigData weather, float deltaTime)
        {
            if (weather.Type == WeatherType.Snow)
            {
                _snowCoverage += weather.Snow.AccumulationRate * deltaTime;
            }
            else
            {
                // Snow melts slowly when not snowing
                _snowCoverage -= 0.005f * deltaTime;
            }

            _snowCoverage = Clamp(_snowCoverage, 0f, weather.PitchEffect.SnowCoverageMax);
        }

        private void UpdateMudLevel(WeatherConfigData weather, float deltaTime)
        {
            if (_wetness > weather.PitchEffect.MudSplatterThreshold)
            {
                _mudLevel += MudIncreaseRate * deltaTime;
            }
            else
            {
                _mudLevel -= MudDecreaseRate * deltaTime;
            }

            _mudLevel = Clamp(_mudLevel, 0f, 1f);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    /// <summary>
    /// Weather state machine managing transitions between weather types.
    /// Handles smooth blending between source and target states.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class WeatherStateMachine
    {
        private WeatherConfigData _currentConfig;
        private WeatherConfigData _targetConfig;
        private float _transitionProgress;
        private bool _isTransitioning;

        /// <summary>The current (or source, during transition) weather config.</summary>
        public WeatherConfigData CurrentConfig => _currentConfig;

        /// <summary>The target weather config during transition; null if not transitioning.</summary>
        public WeatherConfigData TargetConfig => _targetConfig;

        /// <summary>Transition progress (0-1). 0 = at current, 1 = at target.</summary>
        public float TransitionProgress => _transitionProgress;

        /// <summary>Whether a weather transition is in progress.</summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>Current effective weather type (target if transitioning past 50%).</summary>
        public WeatherType CurrentWeatherType =>
            _isTransitioning && _transitionProgress >= 0.5f && _targetConfig != null
                ? _targetConfig.Type
                : _currentConfig.Type;

        public WeatherStateMachine() : this(WeatherType.Clear) { }

        public WeatherStateMachine(WeatherType initialWeather)
        {
            _currentConfig = WeatherConfigData.CreatePreset(initialWeather);
            _targetConfig = null;
            _transitionProgress = 0f;
            _isTransitioning = false;
        }

        /// <summary>
        /// Begin transitioning to a new weather type.
        /// </summary>
        /// <param name="targetWeather">The target weather type.</param>
        public void TransitionTo(WeatherType targetWeather)
        {
            if (targetWeather == _currentConfig.Type && !_isTransitioning)
                return;

            _targetConfig = WeatherConfigData.CreatePreset(targetWeather);
            _transitionProgress = 0f;
            _isTransitioning = true;
        }

        /// <summary>
        /// Begin transitioning to a specific weather config.
        /// </summary>
        /// <param name="targetConfig">The target weather config data.</param>
        public void TransitionTo(WeatherConfigData targetConfig)
        {
            if (targetConfig == null) return;

            _targetConfig = targetConfig;
            _transitionProgress = 0f;
            _isTransitioning = true;
        }

        /// <summary>
        /// Immediately set weather without transition.
        /// </summary>
        /// <param name="weather">Weather type to set.</param>
        public void SetImmediate(WeatherType weather)
        {
            _currentConfig = WeatherConfigData.CreatePreset(weather);
            _targetConfig = null;
            _transitionProgress = 0f;
            _isTransitioning = false;
        }

        /// <summary>
        /// Advance the transition by deltaTime. Completes when progress reaches 1.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
        public void Update(float deltaTime)
        {
            if (!_isTransitioning || _targetConfig == null || deltaTime <= 0f)
                return;

            float blendDuration = _targetConfig.Transition.BlendDuration;
            if (blendDuration <= 0f) blendDuration = 1f;

            _transitionProgress += deltaTime / blendDuration;

            if (_transitionProgress >= 1f)
            {
                _transitionProgress = 1f;
                _currentConfig = _targetConfig;
                _targetConfig = null;
                _isTransitioning = false;
                _transitionProgress = 0f;
            }
        }

        /// <summary>
        /// Returns interpolated particle parameters between current and target states.
        /// </summary>
        public WeatherParticleParams GetCurrentParticleParams()
        {
            if (!_isTransitioning || _targetConfig == null)
            {
                return ParamsFromConfig(_currentConfig);
            }

            var from = ParamsFromConfig(_currentConfig);
            var to = ParamsFromConfig(_targetConfig);
            return LerpParams(from, to, _transitionProgress);
        }

        /// <summary>Whether puddles should be visible based on current weather.</summary>
        public bool ShouldShowPuddles()
        {
            var config = _isTransitioning && _transitionProgress >= 0.5f && _targetConfig != null
                ? _targetConfig : _currentConfig;
            return config.Rain.PuddleDensity > 0.1f;
        }

        /// <summary>Whether snow coverage overlay should be visible.</summary>
        public bool ShouldShowSnowCoverage()
        {
            var config = _isTransitioning && _transitionProgress >= 0.5f && _targetConfig != null
                ? _targetConfig : _currentConfig;
            return config.Type == WeatherType.Snow;
        }

        /// <summary>Current effective fog density (interpolated during transition).</summary>
        public float GetFogDensity()
        {
            if (!_isTransitioning || _targetConfig == null)
                return _currentConfig.Fog.Density;

            return Lerp(_currentConfig.Fog.Density, _targetConfig.Fog.Density, _transitionProgress);
        }

        /// <summary>Current effective wind strength (interpolated during transition).</summary>
        public float GetWindStrength()
        {
            if (!_isTransitioning || _targetConfig == null)
                return _currentConfig.Wind.Strength;

            return Lerp(_currentConfig.Wind.Strength, _targetConfig.Wind.Strength, _transitionProgress);
        }

        private static WeatherParticleParams ParamsFromConfig(WeatherConfigData config)
        {
            return new WeatherParticleParams
            {
                RainDropletCount = config.Rain.DropletCount,
                RainSpeed = config.Rain.Speed,
                RainAngle = config.Rain.Angle,
                RainSplashRate = config.Rain.SplashOnGroundRate,
                SnowFlakeCount = config.Snow.FlakeCount,
                SnowSpeed = config.Snow.Speed,
                SnowDriftAmount = config.Snow.DriftAmount,
                FogDensity = config.Fog.Density,
                FogColorR = config.Fog.ColorR,
                FogColorG = config.Fog.ColorG,
                FogColorB = config.Fog.ColorB,
                WindDirectionX = config.Wind.DirectionX,
                WindDirectionY = config.Wind.DirectionY,
                WindDirectionZ = config.Wind.DirectionZ,
                WindStrength = config.Wind.Strength
            };
        }

        private static WeatherParticleParams LerpParams(WeatherParticleParams from, WeatherParticleParams to, float t)
        {
            return new WeatherParticleParams
            {
                RainDropletCount = LerpInt(from.RainDropletCount, to.RainDropletCount, t),
                RainSpeed = Lerp(from.RainSpeed, to.RainSpeed, t),
                RainAngle = Lerp(from.RainAngle, to.RainAngle, t),
                RainSplashRate = Lerp(from.RainSplashRate, to.RainSplashRate, t),
                SnowFlakeCount = LerpInt(from.SnowFlakeCount, to.SnowFlakeCount, t),
                SnowSpeed = Lerp(from.SnowSpeed, to.SnowSpeed, t),
                SnowDriftAmount = Lerp(from.SnowDriftAmount, to.SnowDriftAmount, t),
                FogDensity = Lerp(from.FogDensity, to.FogDensity, t),
                FogColorR = Lerp(from.FogColorR, to.FogColorR, t),
                FogColorG = Lerp(from.FogColorG, to.FogColorG, t),
                FogColorB = Lerp(from.FogColorB, to.FogColorB, t),
                WindDirectionX = Lerp(from.WindDirectionX, to.WindDirectionX, t),
                WindDirectionY = Lerp(from.WindDirectionY, to.WindDirectionY, t),
                WindDirectionZ = Lerp(from.WindDirectionZ, to.WindDirectionZ, t),
                WindStrength = Lerp(from.WindStrength, to.WindStrength, t)
            };
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private static int LerpInt(int a, int b, float t)
        {
            return (int)(a + (b - a) * t);
        }
    }

    /// <summary>
    /// Master weather logic coordinating state machine and pitch conditions.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class WeatherLogic
    {
        private readonly WeatherStateMachine _stateMachine;
        private readonly PitchConditionTracker _pitchCondition;

        /// <summary>Weather state machine.</summary>
        public WeatherStateMachine StateMachine => _stateMachine;

        /// <summary>Pitch condition tracker.</summary>
        public PitchConditionTracker PitchCondition => _pitchCondition;

        /// <summary>Shortcut: current weather type.</summary>
        public WeatherType CurrentWeather => _stateMachine.CurrentWeatherType;

        /// <summary>Whether a transition is in progress.</summary>
        public bool IsTransitioning => _stateMachine.IsTransitioning;

        public WeatherLogic() : this(WeatherType.Clear) { }

        public WeatherLogic(WeatherType initialWeather)
        {
            _stateMachine = new WeatherStateMachine(initialWeather);
            _pitchCondition = new PitchConditionTracker();
        }

        /// <summary>
        /// Update weather state and pitch conditions.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
        public void UpdateWeather(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            _stateMachine.Update(deltaTime);

            // Update pitch condition using the effective current config
            var effectiveConfig = _stateMachine.IsTransitioning && _stateMachine.TargetConfig != null
                ? _stateMachine.TargetConfig
                : _stateMachine.CurrentConfig;
            _pitchCondition.Update(effectiveConfig, deltaTime);
        }

        /// <summary>
        /// Get interpolated particle parameters for the current weather state.
        /// </summary>
        public WeatherParticleParams GetCurrentParticleParams()
        {
            return _stateMachine.GetCurrentParticleParams();
        }

        /// <summary>Begin transitioning to a new weather type.</summary>
        public void TransitionTo(WeatherType weather)
        {
            _stateMachine.TransitionTo(weather);
        }

        /// <summary>Immediately set weather without transition.</summary>
        public void SetImmediate(WeatherType weather)
        {
            _stateMachine.SetImmediate(weather);
        }

        /// <summary>Whether puddles should be visible.</summary>
        public bool ShouldShowPuddles => _stateMachine.ShouldShowPuddles();

        /// <summary>Whether snow coverage should be rendered.</summary>
        public bool ShouldShowSnowCoverage => _stateMachine.ShouldShowSnowCoverage();

        /// <summary>Current fog density.</summary>
        public float FogDensity => _stateMachine.GetFogDensity();

        /// <summary>Current wind strength.</summary>
        public float WindStrength => _stateMachine.GetWindStrength();
    }
}
