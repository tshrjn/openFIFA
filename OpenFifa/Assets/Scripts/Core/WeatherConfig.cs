namespace OpenFifa.Core
{
    /// <summary>
    /// Weather type enumeration for match conditions.
    /// </summary>
    public enum WeatherType
    {
        Clear = 0,
        LightRain = 1,
        HeavyRain = 2,
        Snow = 3,
        Fog = 4,
        Overcast = 5
    }

    /// <summary>
    /// Pure C# configuration for rain particle parameters.
    /// No Unity dependency.
    /// </summary>
    public class RainConfig
    {
        public int DropletCount = 500;
        public float Speed = 8f;
        public float Angle = 10f;
        public float SplashOnGroundRate = 50f;
        public float PuddleDensity = 0.3f;

        /// <summary>Maximum droplet count allowed.</summary>
        public const int MaxDropletCount = 5000;

        /// <summary>Minimum droplet count allowed.</summary>
        public const int MinDropletCount = 0;

        /// <summary>Maximum speed allowed.</summary>
        public const float MaxSpeed = 20f;

        /// <summary>Maximum puddle density (0-1).</summary>
        public const float MaxPuddleDensity = 1f;

        public RainConfig() { }

        public RainConfig(int dropletCount, float speed, float angle, float splashRate, float puddleDensity)
        {
            DropletCount = Clamp(dropletCount, MinDropletCount, MaxDropletCount);
            Speed = Clamp(speed, 0f, MaxSpeed);
            Angle = Clamp(angle, 0f, 90f);
            SplashOnGroundRate = Clamp(splashRate, 0f, 200f);
            PuddleDensity = Clamp(puddleDensity, 0f, MaxPuddleDensity);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    /// <summary>
    /// Pure C# configuration for snow particle parameters.
    /// No Unity dependency.
    /// </summary>
    public class SnowConfig
    {
        public int FlakeCount = 300;
        public float Speed = 2f;
        public float DriftAmount = 1.5f;
        public float AccumulationRate = 0.01f;

        /// <summary>Maximum flake count allowed.</summary>
        public const int MaxFlakeCount = 3000;

        /// <summary>Minimum flake count allowed.</summary>
        public const int MinFlakeCount = 0;

        /// <summary>Maximum accumulation rate.</summary>
        public const float MaxAccumulationRate = 0.1f;

        public SnowConfig() { }

        public SnowConfig(int flakeCount, float speed, float driftAmount, float accumulationRate)
        {
            FlakeCount = Clamp(flakeCount, MinFlakeCount, MaxFlakeCount);
            Speed = Clamp(speed, 0f, 10f);
            DriftAmount = Clamp(driftAmount, 0f, 5f);
            AccumulationRate = Clamp(accumulationRate, 0f, MaxAccumulationRate);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    /// <summary>
    /// Pure C# configuration for fog parameters.
    /// No Unity dependency.
    /// </summary>
    public class FogConfig
    {
        public float Density = 0.02f;
        public float ColorR = 0.7f;
        public float ColorG = 0.7f;
        public float ColorB = 0.75f;
        public float DistanceMin = 10f;
        public float DistanceMax = 80f;

        /// <summary>Maximum fog density allowed.</summary>
        public const float MaxDensity = 0.1f;

        /// <summary>Minimum fog density allowed.</summary>
        public const float MinDensity = 0f;

        public FogConfig() { }

        public FogConfig(float density, float colorR, float colorG, float colorB, float distMin, float distMax)
        {
            Density = Clamp(density, MinDensity, MaxDensity);
            ColorR = Clamp(colorR, 0f, 1f);
            ColorG = Clamp(colorG, 0f, 1f);
            ColorB = Clamp(colorB, 0f, 1f);
            DistanceMin = distMin > 0f ? distMin : 0f;
            DistanceMax = distMax > distMin ? distMax : distMin + 1f;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    /// <summary>
    /// Pure C# configuration for wind parameters.
    /// No Unity dependency.
    /// </summary>
    public class WindConfig
    {
        public float DirectionX = 1f;
        public float DirectionY = 0f;
        public float DirectionZ = 0f;
        public float Strength = 0f;
        public float GustFrequency = 0.5f;

        /// <summary>Maximum wind strength.</summary>
        public const float MaxStrength = 20f;

        /// <summary>Maximum gust frequency (Hz).</summary>
        public const float MaxGustFrequency = 5f;

        public WindConfig() { }

        public WindConfig(float dirX, float dirY, float dirZ, float strength, float gustFrequency)
        {
            // Normalize direction
            float mag = Sqrt(dirX * dirX + dirY * dirY + dirZ * dirZ);
            if (mag > 0.0001f)
            {
                DirectionX = dirX / mag;
                DirectionY = dirY / mag;
                DirectionZ = dirZ / mag;
            }
            else
            {
                DirectionX = 1f;
                DirectionY = 0f;
                DirectionZ = 0f;
            }

            Strength = Clamp(strength, 0f, MaxStrength);
            GustFrequency = Clamp(gustFrequency, 0f, MaxGustFrequency);
        }

        /// <summary>
        /// Returns the normalized wind direction magnitude (should be ~1.0 after construction).
        /// </summary>
        public float DirectionMagnitude()
        {
            return Sqrt(DirectionX * DirectionX + DirectionY * DirectionY + DirectionZ * DirectionZ);
        }

        private static float Sqrt(float value)
        {
            if (value <= 0f) return 0f;
            // Newton's method for square root (no System.Math dependency for portability)
            float guess = value;
            for (int i = 0; i < 10; i++)
            {
                guess = (guess + value / guess) * 0.5f;
            }
            return guess;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    /// <summary>
    /// Configuration for weather transitions between states.
    /// </summary>
    public class WeatherTransitionConfig
    {
        /// <summary>Duration in seconds for blending between weather states.</summary>
        public float BlendDuration = 5f;

        /// <summary>Minimum blend duration allowed.</summary>
        public const float MinBlendDuration = 0.1f;

        /// <summary>Maximum blend duration allowed.</summary>
        public const float MaxBlendDuration = 60f;

        public WeatherTransitionConfig() { }

        public WeatherTransitionConfig(float blendDuration)
        {
            BlendDuration = blendDuration < MinBlendDuration ? MinBlendDuration :
                            blendDuration > MaxBlendDuration ? MaxBlendDuration : blendDuration;
        }
    }

    /// <summary>
    /// Configuration for pitch surface effects caused by weather.
    /// </summary>
    public class PitchEffectConfig
    {
        /// <summary>Wet pitch reflectivity (0-1).</summary>
        public float WetReflectivity = 0.6f;

        /// <summary>Mud splatter threshold — wetness above which mud appears (0-1).</summary>
        public float MudSplatterThreshold = 0.5f;

        /// <summary>Snow coverage intensity multiplier (0-1).</summary>
        public float SnowCoverageMax = 0.8f;

        /// <summary>Maximum pitch reflectivity.</summary>
        public const float MaxReflectivity = 1f;

        public PitchEffectConfig() { }

        public PitchEffectConfig(float wetReflectivity, float mudThreshold, float snowCoverageMax)
        {
            WetReflectivity = Clamp(wetReflectivity, 0f, MaxReflectivity);
            MudSplatterThreshold = Clamp(mudThreshold, 0f, 1f);
            SnowCoverageMax = Clamp(snowCoverageMax, 0f, 1f);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    /// <summary>
    /// Aggregate weather configuration holding all sub-configs for a weather preset.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class WeatherConfigData
    {
        public readonly WeatherType Type;
        public readonly RainConfig Rain;
        public readonly SnowConfig Snow;
        public readonly FogConfig Fog;
        public readonly WindConfig Wind;
        public readonly WeatherTransitionConfig Transition;
        public readonly PitchEffectConfig PitchEffect;

        public WeatherConfigData(WeatherType type)
            : this(type, new RainConfig(), new SnowConfig(), new FogConfig(),
                   new WindConfig(), new WeatherTransitionConfig(), new PitchEffectConfig())
        {
        }

        public WeatherConfigData(
            WeatherType type,
            RainConfig rain,
            SnowConfig snow,
            FogConfig fog,
            WindConfig wind,
            WeatherTransitionConfig transition,
            PitchEffectConfig pitchEffect)
        {
            Type = type;
            Rain = rain ?? new RainConfig();
            Snow = snow ?? new SnowConfig();
            Fog = fog ?? new FogConfig();
            Wind = wind ?? new WindConfig();
            Transition = transition ?? new WeatherTransitionConfig();
            PitchEffect = pitchEffect ?? new PitchEffectConfig();
        }

        /// <summary>
        /// Creates a default preset for the given weather type.
        /// </summary>
        public static WeatherConfigData CreatePreset(WeatherType type)
        {
            switch (type)
            {
                case WeatherType.Clear:
                    return new WeatherConfigData(
                        WeatherType.Clear,
                        new RainConfig(0, 0f, 0f, 0f, 0f),
                        new SnowConfig(0, 0f, 0f, 0f),
                        new FogConfig(0f, 0.8f, 0.85f, 0.9f, 100f, 500f),
                        new WindConfig(1f, 0f, 0f, 2f, 0.3f),
                        new WeatherTransitionConfig(3f),
                        new PitchEffectConfig(0f, 1f, 0f));

                case WeatherType.LightRain:
                    return new WeatherConfigData(
                        WeatherType.LightRain,
                        new RainConfig(200, 6f, 5f, 20f, 0.15f),
                        new SnowConfig(0, 0f, 0f, 0f),
                        new FogConfig(0.005f, 0.6f, 0.6f, 0.65f, 30f, 120f),
                        new WindConfig(1f, 0f, 0.3f, 3f, 0.5f),
                        new WeatherTransitionConfig(5f),
                        new PitchEffectConfig(0.3f, 0.7f, 0f));

                case WeatherType.HeavyRain:
                    return new WeatherConfigData(
                        WeatherType.HeavyRain,
                        new RainConfig(1000, 10f, 15f, 80f, 0.6f),
                        new SnowConfig(0, 0f, 0f, 0f),
                        new FogConfig(0.015f, 0.5f, 0.5f, 0.55f, 15f, 80f),
                        new WindConfig(0.8f, 0f, 0.6f, 8f, 1.5f),
                        new WeatherTransitionConfig(5f),
                        new PitchEffectConfig(0.8f, 0.4f, 0f));

                case WeatherType.Snow:
                    return new WeatherConfigData(
                        WeatherType.Snow,
                        new RainConfig(0, 0f, 0f, 0f, 0f),
                        new SnowConfig(400, 1.5f, 2f, 0.02f),
                        new FogConfig(0.01f, 0.85f, 0.87f, 0.9f, 20f, 100f),
                        new WindConfig(0.5f, 0f, 0.5f, 4f, 0.8f),
                        new WeatherTransitionConfig(8f),
                        new PitchEffectConfig(0.1f, 1f, 0.8f));

                case WeatherType.Fog:
                    return new WeatherConfigData(
                        WeatherType.Fog,
                        new RainConfig(0, 0f, 0f, 0f, 0f),
                        new SnowConfig(0, 0f, 0f, 0f),
                        new FogConfig(0.05f, 0.7f, 0.72f, 0.75f, 5f, 40f),
                        new WindConfig(1f, 0f, 0f, 1f, 0.2f),
                        new WeatherTransitionConfig(10f),
                        new PitchEffectConfig(0.15f, 0.8f, 0f));

                case WeatherType.Overcast:
                    return new WeatherConfigData(
                        WeatherType.Overcast,
                        new RainConfig(0, 0f, 0f, 0f, 0f),
                        new SnowConfig(0, 0f, 0f, 0f),
                        new FogConfig(0.003f, 0.65f, 0.67f, 0.7f, 50f, 200f),
                        new WindConfig(1f, 0f, 0.2f, 5f, 1f),
                        new WeatherTransitionConfig(4f),
                        new PitchEffectConfig(0.05f, 0.9f, 0f));

                default:
                    return new WeatherConfigData(WeatherType.Clear);
            }
        }
    }
}
