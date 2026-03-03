using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// ScriptableObject weather preset for Inspector-based weather configuration.
    /// References pure C# WeatherConfig values and provides quick swap between presets.
    /// </summary>
    [CreateAssetMenu(fileName = "WeatherPreset", menuName = "OpenFifa/Config/Weather Preset")]
    public class WeatherPreset : ScriptableObject
    {
        [Header("Weather Type")]
        [SerializeField] private WeatherType _weatherType = WeatherType.Clear;

        [Header("Rain Settings")]
        [SerializeField] private int _rainDropletCount = 500;
        [SerializeField] private float _rainSpeed = 8f;
        [SerializeField] private float _rainAngle = 10f;
        [SerializeField] private float _rainSplashRate = 50f;
        [SerializeField] [Range(0f, 1f)] private float _puddleDensity = 0.3f;

        [Header("Snow Settings")]
        [SerializeField] private int _snowFlakeCount = 300;
        [SerializeField] private float _snowSpeed = 2f;
        [SerializeField] private float _snowDriftAmount = 1.5f;
        [SerializeField] private float _snowAccumulationRate = 0.01f;

        [Header("Fog Settings")]
        [SerializeField] [Range(0f, 0.1f)] private float _fogDensity = 0.02f;
        [SerializeField] private Color _fogColor = new Color(0.7f, 0.7f, 0.75f, 1f);
        [SerializeField] private float _fogDistanceMin = 10f;
        [SerializeField] private float _fogDistanceMax = 80f;

        [Header("Wind Settings")]
        [SerializeField] private Vector3 _windDirection = new Vector3(1f, 0f, 0f);
        [SerializeField] private float _windStrength = 0f;
        [SerializeField] private float _windGustFrequency = 0.5f;

        [Header("Transition")]
        [SerializeField] private float _blendDuration = 5f;

        [Header("Pitch Effects")]
        [SerializeField] [Range(0f, 1f)] private float _wetReflectivity = 0.6f;
        [SerializeField] [Range(0f, 1f)] private float _mudSplatterThreshold = 0.5f;
        [SerializeField] [Range(0f, 1f)] private float _snowCoverageMax = 0.8f;

        /// <summary>The weather type of this preset.</summary>
        public WeatherType WeatherType => _weatherType;

        /// <summary>Blend duration in seconds.</summary>
        public float BlendDuration => _blendDuration;

        /// <summary>
        /// Converts this ScriptableObject to a pure C# WeatherConfigData for core logic.
        /// </summary>
        public WeatherConfigData ToConfigData()
        {
            return new WeatherConfigData(
                _weatherType,
                new RainConfig(_rainDropletCount, _rainSpeed, _rainAngle, _rainSplashRate, _puddleDensity),
                new SnowConfig(_snowFlakeCount, _snowSpeed, _snowDriftAmount, _snowAccumulationRate),
                new FogConfig(_fogDensity, _fogColor.r, _fogColor.g, _fogColor.b, _fogDistanceMin, _fogDistanceMax),
                new WindConfig(_windDirection.x, _windDirection.y, _windDirection.z, _windStrength, _windGustFrequency),
                new WeatherTransitionConfig(_blendDuration),
                new PitchEffectConfig(_wetReflectivity, _mudSplatterThreshold, _snowCoverageMax)
            );
        }

        /// <summary>
        /// Initialize this preset from a WeatherType using defaults.
        /// Useful for creating presets via code.
        /// </summary>
        public void InitFromDefaults(WeatherType type)
        {
            _weatherType = type;
            var config = WeatherConfigData.CreatePreset(type);

            _rainDropletCount = config.Rain.DropletCount;
            _rainSpeed = config.Rain.Speed;
            _rainAngle = config.Rain.Angle;
            _rainSplashRate = config.Rain.SplashOnGroundRate;
            _puddleDensity = config.Rain.PuddleDensity;

            _snowFlakeCount = config.Snow.FlakeCount;
            _snowSpeed = config.Snow.Speed;
            _snowDriftAmount = config.Snow.DriftAmount;
            _snowAccumulationRate = config.Snow.AccumulationRate;

            _fogDensity = config.Fog.Density;
            _fogColor = new Color(config.Fog.ColorR, config.Fog.ColorG, config.Fog.ColorB, 1f);
            _fogDistanceMin = config.Fog.DistanceMin;
            _fogDistanceMax = config.Fog.DistanceMax;

            _windDirection = new Vector3(config.Wind.DirectionX, config.Wind.DirectionY, config.Wind.DirectionZ);
            _windStrength = config.Wind.Strength;
            _windGustFrequency = config.Wind.GustFrequency;

            _blendDuration = config.Transition.BlendDuration;

            _wetReflectivity = config.PitchEffect.WetReflectivity;
            _mudSplatterThreshold = config.PitchEffect.MudSplatterThreshold;
            _snowCoverageMax = config.PitchEffect.SnowCoverageMax;
        }
    }
}
