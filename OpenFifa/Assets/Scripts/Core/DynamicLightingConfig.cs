using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Represents a time of day as a float hour (0-24).
    /// Provides sunrise/sunset calculations and period detection.
    /// </summary>
    public class TimeOfDay
    {
        /// <summary>Current hour as a float (0.0 = midnight, 12.0 = noon, 23.99 = just before midnight).</summary>
        public float Hour { get; }

        /// <summary>Hour at which sunrise occurs (default 6.0 = 6:00 AM).</summary>
        public float SunriseHour { get; }

        /// <summary>Hour at which sunset occurs (default 20.0 = 8:00 PM).</summary>
        public float SunsetHour { get; }

        /// <summary>Duration of the dawn transition period in hours.</summary>
        public float DawnDuration { get; }

        /// <summary>Duration of the dusk transition period in hours.</summary>
        public float DuskDuration { get; }

        public TimeOfDay(
            float hour = 12f,
            float sunriseHour = 6f,
            float sunsetHour = 20f,
            float dawnDuration = 1f,
            float duskDuration = 1f)
        {
            if (hour < 0f || hour >= 24f)
                throw new ArgumentException("Hour must be in range [0, 24).", nameof(hour));
            if (sunriseHour < 0f || sunriseHour >= 24f)
                throw new ArgumentException("SunriseHour must be in range [0, 24).", nameof(sunriseHour));
            if (sunsetHour < 0f || sunsetHour >= 24f)
                throw new ArgumentException("SunsetHour must be in range [0, 24).", nameof(sunsetHour));
            if (sunsetHour <= sunriseHour)
                throw new ArgumentException("SunsetHour must be after SunriseHour.", nameof(sunsetHour));
            if (dawnDuration <= 0f)
                throw new ArgumentException("DawnDuration must be positive.", nameof(dawnDuration));
            if (duskDuration <= 0f)
                throw new ArgumentException("DuskDuration must be positive.", nameof(duskDuration));

            Hour = hour;
            SunriseHour = sunriseHour;
            SunsetHour = sunsetHour;
            DawnDuration = dawnDuration;
            DuskDuration = duskDuration;
        }

        /// <summary>True when it is fully night (before dawn start or after dusk end).</summary>
        public bool IsNightTime => Hour < (SunriseHour - DawnDuration) || Hour >= (SunsetHour + DuskDuration);

        /// <summary>True during the dawn transition period (sunrise - dawnDuration to sunrise).</summary>
        public bool IsDawn => Hour >= (SunriseHour - DawnDuration) && Hour < SunriseHour;

        /// <summary>True during the dusk transition period (sunset to sunset + duskDuration).</summary>
        public bool IsDusk => Hour >= SunsetHour && Hour < (SunsetHour + DuskDuration);

        /// <summary>True during full daylight (after sunrise, before sunset).</summary>
        public bool IsDaytime => Hour >= SunriseHour && Hour < SunsetHour;

        /// <summary>
        /// Normalized sun elevation factor (0 = horizon, 1 = zenith).
        /// Returns 0 during night.
        /// </summary>
        public float SunElevationFactor
        {
            get
            {
                if (IsNightTime) return 0f;

                if (IsDawn)
                {
                    float dawnStart = SunriseHour - DawnDuration;
                    return (Hour - dawnStart) / DawnDuration * 0.1f;
                }

                if (IsDusk)
                {
                    float duskEnd = SunsetHour + DuskDuration;
                    return (duskEnd - Hour) / DuskDuration * 0.1f;
                }

                // Daytime: peak at solar noon (midpoint between sunrise and sunset)
                float solarNoon = (SunriseHour + SunsetHour) / 2f;
                float halfDay = (SunsetHour - SunriseHour) / 2f;
                float distFromNoon = Math.Abs(Hour - solarNoon);
                return 1f - (distFromNoon / halfDay) * 0.9f;
            }
        }

        /// <summary>
        /// Normalized time as a 0-1 fraction of the 24-hour day.
        /// </summary>
        public float NormalizedTime => Hour / 24f;

        /// <summary>
        /// Creates a new TimeOfDay advanced by the given delta hours, wrapping at 24.
        /// </summary>
        public TimeOfDay Advance(float deltaHours)
        {
            float newHour = (Hour + deltaHours) % 24f;
            if (newHour < 0f) newHour += 24f;
            return new TimeOfDay(newHour, SunriseHour, SunsetHour, DawnDuration, DuskDuration);
        }
    }

    /// <summary>
    /// Possible states for a stadium floodlight.
    /// </summary>
    public enum FloodlightState
    {
        Off,
        WarmingUp,
        FullPower,
        Dimmed,
        Emergency
    }

    /// <summary>
    /// Behavior parameters for a single floodlight: warm-up timing, flicker, color temperature.
    /// </summary>
    public class FloodlightBehavior
    {
        /// <summary>Duration in seconds for the floodlight to reach full power from off.</summary>
        public float WarmUpDuration { get; }

        /// <summary>Frequency of flicker during warm-up (Hz). 0 = no flicker.</summary>
        public float WarmUpFlickerFrequency { get; }

        /// <summary>Amplitude of flicker during warm-up (0-1 fraction of current intensity).</summary>
        public float WarmUpFlickerAmplitude { get; }

        /// <summary>Color temperature in Kelvin at full power (e.g. 5500K daylight, 3200K warm).</summary>
        public float ColorTemperatureFull { get; }

        /// <summary>Color temperature in Kelvin during warm-up (typically warmer/lower).</summary>
        public float ColorTemperatureWarmUp { get; }

        /// <summary>Intensity multiplier when in dimmed mode (0-1).</summary>
        public float DimmedIntensityFactor { get; }

        /// <summary>Intensity multiplier when in emergency mode (0-1).</summary>
        public float EmergencyIntensityFactor { get; }

        public FloodlightBehavior(
            float warmUpDuration = 3f,
            float warmUpFlickerFrequency = 8f,
            float warmUpFlickerAmplitude = 0.3f,
            float colorTemperatureFull = 5500f,
            float colorTemperatureWarmUp = 3200f,
            float dimmedIntensityFactor = 0.4f,
            float emergencyIntensityFactor = 0.6f)
        {
            if (warmUpDuration < 0f)
                throw new ArgumentException("WarmUpDuration must be non-negative.", nameof(warmUpDuration));
            if (warmUpFlickerFrequency < 0f)
                throw new ArgumentException("WarmUpFlickerFrequency must be non-negative.", nameof(warmUpFlickerFrequency));
            if (warmUpFlickerAmplitude < 0f || warmUpFlickerAmplitude > 1f)
                throw new ArgumentException("WarmUpFlickerAmplitude must be between 0 and 1.", nameof(warmUpFlickerAmplitude));
            if (colorTemperatureFull <= 0f)
                throw new ArgumentException("ColorTemperatureFull must be positive.", nameof(colorTemperatureFull));
            if (colorTemperatureWarmUp <= 0f)
                throw new ArgumentException("ColorTemperatureWarmUp must be positive.", nameof(colorTemperatureWarmUp));
            if (dimmedIntensityFactor < 0f || dimmedIntensityFactor > 1f)
                throw new ArgumentException("DimmedIntensityFactor must be between 0 and 1.", nameof(dimmedIntensityFactor));
            if (emergencyIntensityFactor < 0f || emergencyIntensityFactor > 1f)
                throw new ArgumentException("EmergencyIntensityFactor must be between 0 and 1.", nameof(emergencyIntensityFactor));

            WarmUpDuration = warmUpDuration;
            WarmUpFlickerFrequency = warmUpFlickerFrequency;
            WarmUpFlickerAmplitude = warmUpFlickerAmplitude;
            ColorTemperatureFull = colorTemperatureFull;
            ColorTemperatureWarmUp = colorTemperatureWarmUp;
            DimmedIntensityFactor = dimmedIntensityFactor;
            EmergencyIntensityFactor = emergencyIntensityFactor;
        }

        /// <summary>
        /// Calculates the intensity multiplier during warm-up based on elapsed time.
        /// Returns 0 at time 0, and 1 at warm-up completion.
        /// Includes flicker effect if configured.
        /// </summary>
        public float CalculateWarmUpIntensity(float elapsedSeconds)
        {
            if (WarmUpDuration <= 0f) return 1f;

            float t = Math.Min(elapsedSeconds / WarmUpDuration, 1f);
            if (t < 0f) t = 0f;

            // Smooth ramp (ease-in)
            float baseIntensity = t * t;

            // Apply flicker during warm-up (not at full power)
            if (t < 1f && WarmUpFlickerFrequency > 0f && WarmUpFlickerAmplitude > 0f)
            {
                float flickerPhase = elapsedSeconds * WarmUpFlickerFrequency * 2f * (float)Math.PI;
                float flickerValue = (float)Math.Sin(flickerPhase) * WarmUpFlickerAmplitude * (1f - t);
                baseIntensity = Math.Max(0f, baseIntensity + flickerValue);
            }

            return Math.Min(baseIntensity, 1f);
        }

        /// <summary>
        /// Linearly interpolates color temperature from warm-up to full based on progress (0-1).
        /// </summary>
        public float InterpolateColorTemperature(float progress)
        {
            float t = Math.Max(0f, Math.Min(1f, progress));
            return ColorTemperatureWarmUp + (ColorTemperatureFull - ColorTemperatureWarmUp) * t;
        }
    }

    /// <summary>
    /// Configuration for shadow cascade rendering.
    /// </summary>
    public class ShadowCascadeConfig
    {
        /// <summary>Number of shadow cascades (1, 2, or 4).</summary>
        public int CascadeCount { get; }

        /// <summary>Split ratios for cascade boundaries. Length = CascadeCount - 1.</summary>
        public readonly List<float> SplitRatios;

        /// <summary>Maximum shadow rendering distance in meters.</summary>
        public float MaxDistance { get; }

        /// <summary>Depth bias to reduce shadow acne.</summary>
        public float DepthBias { get; }

        /// <summary>Normal bias to reduce shadow acne.</summary>
        public float NormalBias { get; }

        public ShadowCascadeConfig(
            int cascadeCount = 4,
            List<float> splitRatios = null,
            float maxDistance = 80f,
            float depthBias = 1f,
            float normalBias = 1f)
        {
            if (cascadeCount != 1 && cascadeCount != 2 && cascadeCount != 4)
                throw new ArgumentException("CascadeCount must be 1, 2, or 4.", nameof(cascadeCount));
            if (maxDistance <= 0f)
                throw new ArgumentException("MaxDistance must be positive.", nameof(maxDistance));
            if (depthBias < 0f)
                throw new ArgumentException("DepthBias must be non-negative.", nameof(depthBias));
            if (normalBias < 0f)
                throw new ArgumentException("NormalBias must be non-negative.", nameof(normalBias));

            CascadeCount = cascadeCount;
            MaxDistance = maxDistance;
            DepthBias = depthBias;
            NormalBias = normalBias;

            if (splitRatios != null)
            {
                if (splitRatios.Count != cascadeCount - 1)
                    throw new ArgumentException(
                        $"SplitRatios count ({splitRatios.Count}) must be CascadeCount-1 ({cascadeCount - 1}).",
                        nameof(splitRatios));
                SplitRatios = new List<float>(splitRatios);
            }
            else
            {
                SplitRatios = GenerateDefaultSplitRatios(cascadeCount);
            }
        }

        private static List<float> GenerateDefaultSplitRatios(int cascadeCount)
        {
            switch (cascadeCount)
            {
                case 1: return new List<float>();
                case 2: return new List<float> { 0.25f };
                case 4: return new List<float> { 0.067f, 0.2f, 0.467f };
                default: return new List<float>();
            }
        }

        /// <summary>Whether the cascade config is valid.</summary>
        public bool IsValid => SplitRatios.Count == CascadeCount - 1 && MaxDistance > 0f;
    }

    /// <summary>
    /// A color keyframe for ambient lighting transitions (RGB + time).
    /// </summary>
    public class AmbientColorKeyframe
    {
        /// <summary>Normalized time (0-1 over a 24-hour day).</summary>
        public float Time { get; }

        public float R { get; }
        public float G { get; }
        public float B { get; }

        public AmbientColorKeyframe(float time, float r, float g, float b)
        {
            if (time < 0f || time > 1f)
                throw new ArgumentException("Time must be between 0 and 1.", nameof(time));
            if (r < 0f || r > 1f)
                throw new ArgumentException("R must be between 0 and 1.", nameof(r));
            if (g < 0f || g > 1f)
                throw new ArgumentException("G must be between 0 and 1.", nameof(g));
            if (b < 0f || b > 1f)
                throw new ArgumentException("B must be between 0 and 1.", nameof(b));

            Time = time;
            R = r;
            G = g;
            B = b;
        }
    }

    /// <summary>
    /// Configuration for lighting transitions over time of day.
    /// Contains ambient color keyframes and directional light rotation parameters.
    /// </summary>
    public class LightingTransitionConfig
    {
        /// <summary>Ambient color keyframes for time-of-day interpolation.</summary>
        public readonly List<AmbientColorKeyframe> AmbientKeyframes;

        /// <summary>Sun rotation X angle at sunrise (horizon = 0).</summary>
        public float SunAngleAtSunrise { get; }

        /// <summary>Sun rotation X angle at solar noon (zenith).</summary>
        public float SunAngleAtNoon { get; }

        /// <summary>Sun rotation X angle at sunset (horizon = 0).</summary>
        public float SunAngleAtSunset { get; }

        /// <summary>Y-axis rotation of the sun path (compass heading).</summary>
        public float SunAzimuthOffset { get; }

        public LightingTransitionConfig(
            List<AmbientColorKeyframe> ambientKeyframes = null,
            float sunAngleAtSunrise = 5f,
            float sunAngleAtNoon = 60f,
            float sunAngleAtSunset = 5f,
            float sunAzimuthOffset = 170f)
        {
            if (sunAngleAtSunrise < 0f || sunAngleAtSunrise > 90f)
                throw new ArgumentException("SunAngleAtSunrise must be between 0 and 90.", nameof(sunAngleAtSunrise));
            if (sunAngleAtNoon < 0f || sunAngleAtNoon > 90f)
                throw new ArgumentException("SunAngleAtNoon must be between 0 and 90.", nameof(sunAngleAtNoon));
            if (sunAngleAtSunset < 0f || sunAngleAtSunset > 90f)
                throw new ArgumentException("SunAngleAtSunset must be between 0 and 90.", nameof(sunAngleAtSunset));

            SunAngleAtSunrise = sunAngleAtSunrise;
            SunAngleAtNoon = sunAngleAtNoon;
            SunAngleAtSunset = sunAngleAtSunset;
            SunAzimuthOffset = sunAzimuthOffset;

            AmbientKeyframes = ambientKeyframes ?? new List<AmbientColorKeyframe>
            {
                new AmbientColorKeyframe(0.00f, 0.05f, 0.05f, 0.10f),  // midnight: deep blue
                new AmbientColorKeyframe(0.20f, 0.10f, 0.08f, 0.15f),  // pre-dawn
                new AmbientColorKeyframe(0.25f, 0.40f, 0.30f, 0.25f),  // dawn: warm orange
                new AmbientColorKeyframe(0.30f, 0.60f, 0.55f, 0.50f),  // morning
                new AmbientColorKeyframe(0.50f, 0.80f, 0.82f, 0.85f),  // midday: bright neutral
                new AmbientColorKeyframe(0.75f, 0.60f, 0.40f, 0.30f),  // dusk: warm
                new AmbientColorKeyframe(0.85f, 0.20f, 0.15f, 0.25f),  // twilight: purple
                new AmbientColorKeyframe(1.00f, 0.05f, 0.05f, 0.10f),  // midnight again
            };
        }

        /// <summary>
        /// Evaluates the ambient color at a normalized time (0-1) by interpolating between keyframes.
        /// Returns (r, g, b) tuple.
        /// </summary>
        public void EvaluateAmbientColor(float normalizedTime, out float r, out float g, out float b)
        {
            r = 0f; g = 0f; b = 0f;

            if (AmbientKeyframes == null || AmbientKeyframes.Count == 0)
            {
                r = 0.5f; g = 0.5f; b = 0.5f;
                return;
            }

            float t = Math.Max(0f, Math.Min(1f, normalizedTime));

            // Find surrounding keyframes
            if (t <= AmbientKeyframes[0].Time)
            {
                r = AmbientKeyframes[0].R;
                g = AmbientKeyframes[0].G;
                b = AmbientKeyframes[0].B;
                return;
            }

            if (t >= AmbientKeyframes[AmbientKeyframes.Count - 1].Time)
            {
                var last = AmbientKeyframes[AmbientKeyframes.Count - 1];
                r = last.R; g = last.G; b = last.B;
                return;
            }

            for (int i = 0; i < AmbientKeyframes.Count - 1; i++)
            {
                var a = AmbientKeyframes[i];
                var bk = AmbientKeyframes[i + 1];
                if (t >= a.Time && t <= bk.Time)
                {
                    float frac = (t - a.Time) / (bk.Time - a.Time);
                    r = a.R + (bk.R - a.R) * frac;
                    g = a.G + (bk.G - a.G) * frac;
                    b = a.B + (bk.B - a.B) * frac;
                    return;
                }
            }

            // Fallback
            r = 0.5f; g = 0.5f; b = 0.5f;
        }

        /// <summary>
        /// Calculates directional sun X-angle for a given normalized time (0-1).
        /// Returns elevation angle (0 = horizon, up to SunAngleAtNoon at solar noon).
        /// Returns 0 at night.
        /// </summary>
        public float EvaluateSunElevation(float normalizedTime, float sunriseNormalized, float sunsetNormalized)
        {
            float t = Math.Max(0f, Math.Min(1f, normalizedTime));

            if (t < sunriseNormalized || t > sunsetNormalized)
                return 0f;

            float solarNoonNormalized = (sunriseNormalized + sunsetNormalized) / 2f;
            float halfDayNormalized = (sunsetNormalized - sunriseNormalized) / 2f;

            if (halfDayNormalized <= 0f) return 0f;

            float distFromNoon = Math.Abs(t - solarNoonNormalized);
            float dayProgress = 1f - (distFromNoon / halfDayNormalized);

            if (t < solarNoonNormalized)
            {
                // Morning: interpolate sunrise angle -> noon angle
                return SunAngleAtSunrise + (SunAngleAtNoon - SunAngleAtSunrise) * dayProgress;
            }
            else
            {
                // Afternoon: interpolate noon angle -> sunset angle
                return SunAngleAtSunset + (SunAngleAtNoon - SunAngleAtSunset) * dayProgress;
            }
        }
    }

    /// <summary>
    /// Per-area intensity multipliers for pitch lighting.
    /// </summary>
    public class PitchLightmapConfig
    {
        /// <summary>Intensity multiplier for the center circle area.</summary>
        public float CenterIntensityMultiplier { get; }

        /// <summary>Intensity multiplier for corner areas.</summary>
        public float CornerIntensityMultiplier { get; }

        /// <summary>Intensity multiplier for goal area (penalty box).</summary>
        public float GoalAreaIntensityMultiplier { get; }

        /// <summary>Intensity multiplier for touchline (sideline) areas.</summary>
        public float TouchlineIntensityMultiplier { get; }

        public PitchLightmapConfig(
            float centerIntensityMultiplier = 1.0f,
            float cornerIntensityMultiplier = 0.85f,
            float goalAreaIntensityMultiplier = 0.95f,
            float touchlineIntensityMultiplier = 0.90f)
        {
            if (centerIntensityMultiplier < 0f)
                throw new ArgumentException("CenterIntensityMultiplier must be non-negative.", nameof(centerIntensityMultiplier));
            if (cornerIntensityMultiplier < 0f)
                throw new ArgumentException("CornerIntensityMultiplier must be non-negative.", nameof(cornerIntensityMultiplier));
            if (goalAreaIntensityMultiplier < 0f)
                throw new ArgumentException("GoalAreaIntensityMultiplier must be non-negative.", nameof(goalAreaIntensityMultiplier));
            if (touchlineIntensityMultiplier < 0f)
                throw new ArgumentException("TouchlineIntensityMultiplier must be non-negative.", nameof(touchlineIntensityMultiplier));

            CenterIntensityMultiplier = centerIntensityMultiplier;
            CornerIntensityMultiplier = cornerIntensityMultiplier;
            GoalAreaIntensityMultiplier = goalAreaIntensityMultiplier;
            TouchlineIntensityMultiplier = touchlineIntensityMultiplier;
        }

        /// <summary>
        /// Returns the intensity multiplier for a given pitch zone name.
        /// </summary>
        public float GetMultiplierForZone(string zoneName)
        {
            if (string.IsNullOrEmpty(zoneName)) return 1f;

            switch (zoneName.ToLowerInvariant())
            {
                case "center": return CenterIntensityMultiplier;
                case "corner": return CornerIntensityMultiplier;
                case "goal": return GoalAreaIntensityMultiplier;
                case "touchline": return TouchlineIntensityMultiplier;
                default: return 1f;
            }
        }
    }
}
