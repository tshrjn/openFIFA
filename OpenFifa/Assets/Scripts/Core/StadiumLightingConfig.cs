using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// A keyframe on an intensity curve (time 0-1, value 0+).
    /// </summary>
    public class IntensityCurveKeyframe
    {
        public float Time { get; }
        public float Value { get; }

        public IntensityCurveKeyframe(float time, float value)
        {
            if (time < 0f || time > 1f) throw new ArgumentException("Time must be between 0 and 1.", nameof(time));
            if (value < 0f) throw new ArgumentException("Value must be non-negative.", nameof(value));

            Time = time;
            Value = value;
        }
    }

    /// <summary>
    /// Ambient occlusion settings for stadium rendering.
    /// </summary>
    public class AmbientOcclusionSettings
    {
        public float Intensity { get; }
        public float Radius { get; }

        public AmbientOcclusionSettings(float intensity = 0.5f, float radius = 0.3f)
        {
            if (intensity < 0f || intensity > 1f) throw new ArgumentException("Intensity must be between 0 and 1.", nameof(intensity));
            if (radius <= 0f) throw new ArgumentException("Radius must be positive.", nameof(radius));

            Intensity = intensity;
            Radius = radius;
        }
    }

    /// <summary>
    /// Shadow rendering settings for stadium lighting.
    /// </summary>
    public class ShadowSettings
    {
        public float Strength { get; }
        public float Distance { get; }
        public int Resolution { get; }

        public ShadowSettings(float strength = 0.8f, float distance = 80f, int resolution = 2048)
        {
            if (strength < 0f || strength > 1f) throw new ArgumentException("Strength must be between 0 and 1.", nameof(strength));
            if (distance <= 0f) throw new ArgumentException("Distance must be positive.", nameof(distance));
            if (resolution <= 0) throw new ArgumentException("Resolution must be positive.", nameof(resolution));

            Strength = strength;
            Distance = distance;
            Resolution = resolution;
        }

        /// <summary>Whether the resolution is a power of two (recommended for shadow maps).</summary>
        public bool IsPowerOfTwo
        {
            get
            {
                int v = Resolution;
                return v > 0 && (v & (v - 1)) == 0;
            }
        }
    }

    /// <summary>
    /// Pure C# config for stadium floodlight lighting. No Unity dependencies.
    /// Approximates EA FC 26 Rush Mode warm night stadium atmosphere.
    /// </summary>
    public class StadiumLightingConfig
    {
        // Floodlights (4 corner spots)
        public float FloodlightHeight { get; }
        public float FloodlightIntensity { get; }
        public float FloodlightRange { get; }
        public float FloodlightSpotAngle { get; }
        public float FloodlightColorR { get; }
        public float FloodlightColorG { get; }
        public float FloodlightColorB { get; }

        // Fill light (directional)
        public float FillIntensity { get; }
        public float FillColorR { get; }
        public float FillColorG { get; }
        public float FillColorB { get; }

        // Ambient
        public float AmbientR { get; }
        public float AmbientG { get; }
        public float AmbientB { get; }

        // Floodlight tower positions (xyz per tower, 4 towers)
        public readonly List<float[]> FloodlightPositions;

        // Intensity curve keyframes for time-of-day
        public readonly List<IntensityCurveKeyframe> IntensityCurve;

        // Ambient occlusion
        public readonly AmbientOcclusionSettings AmbientOcclusion;

        // Shadow settings
        public readonly ShadowSettings Shadows;

        public StadiumLightingConfig(
            float floodlightHeight = 25f,
            float floodlightIntensity = 150f,
            float floodlightRange = 70f,
            float floodlightSpotAngle = 100f,
            float floodlightColorR = 1f,
            float floodlightColorG = 0.96f,
            float floodlightColorB = 0.88f,
            float fillIntensity = 1.2f,
            float fillColorR = 0.8f,
            float fillColorG = 0.85f,
            float fillColorB = 0.9f,
            float ambientR = 0.15f,
            float ambientG = 0.17f,
            float ambientB = 0.22f,
            List<float[]> floodlightPositions = null,
            List<IntensityCurveKeyframe> intensityCurve = null,
            AmbientOcclusionSettings ambientOcclusion = null,
            ShadowSettings shadows = null)
        {
            FloodlightHeight = floodlightHeight;
            FloodlightIntensity = floodlightIntensity;
            FloodlightRange = floodlightRange;
            FloodlightSpotAngle = floodlightSpotAngle;
            FloodlightColorR = floodlightColorR;
            FloodlightColorG = floodlightColorG;
            FloodlightColorB = floodlightColorB;
            FillIntensity = fillIntensity;
            FillColorR = fillColorR;
            FillColorG = fillColorG;
            FillColorB = fillColorB;
            AmbientR = ambientR;
            AmbientG = ambientG;
            AmbientB = ambientB;

            // Default 4 tower positions (corners of the stadium)
            FloodlightPositions = floodlightPositions ?? new List<float[]>
            {
                new float[] {  40f, floodlightHeight,  30f },
                new float[] { -40f, floodlightHeight,  30f },
                new float[] {  40f, floodlightHeight, -30f },
                new float[] { -40f, floodlightHeight, -30f },
            };

            // Default intensity curve: dawn -> midday -> dusk -> night
            IntensityCurve = intensityCurve ?? new List<IntensityCurveKeyframe>
            {
                new IntensityCurveKeyframe(0.0f,  0.2f),  // midnight
                new IntensityCurveKeyframe(0.25f, 0.6f),  // dawn
                new IntensityCurveKeyframe(0.5f,  1.0f),  // midday
                new IntensityCurveKeyframe(0.75f, 0.6f),  // dusk
                new IntensityCurveKeyframe(1.0f,  0.2f),  // midnight again
            };

            AmbientOcclusion = ambientOcclusion ?? new AmbientOcclusionSettings();
            Shadows = shadows ?? new ShadowSettings();
        }

        /// <summary>Number of floodlight tower positions configured.</summary>
        public int FloodlightTowerCount => FloodlightPositions.Count;

        /// <summary>
        /// Linearly interpolates the intensity curve at a given time (0-1).
        /// </summary>
        public float EvaluateIntensity(float time)
        {
            if (IntensityCurve == null || IntensityCurve.Count == 0)
                return 1f;

            // Clamp time
            if (time <= IntensityCurve[0].Time)
                return IntensityCurve[0].Value;
            if (time >= IntensityCurve[IntensityCurve.Count - 1].Time)
                return IntensityCurve[IntensityCurve.Count - 1].Value;

            // Find the two keyframes surrounding this time
            for (int i = 0; i < IntensityCurve.Count - 1; i++)
            {
                var a = IntensityCurve[i];
                var b = IntensityCurve[i + 1];
                if (time >= a.Time && time <= b.Time)
                {
                    float t = (time - a.Time) / (b.Time - a.Time);
                    return a.Value + (b.Value - a.Value) * t;
                }
            }

            return 1f;
        }

        public static StadiumLightingConfig CreateNightStadium()
        {
            return new StadiumLightingConfig();
        }

        public static StadiumLightingConfig CreateDayStadium()
        {
            return new StadiumLightingConfig(
                floodlightIntensity: 50f,
                fillIntensity: 2.0f,
                fillColorR: 1f,
                fillColorG: 0.98f,
                fillColorB: 0.95f,
                ambientR: 0.35f,
                ambientG: 0.38f,
                ambientB: 0.42f
            );
        }

        public static StadiumLightingConfig CreateDuskStadium()
        {
            return new StadiumLightingConfig(
                floodlightIntensity: 100f,
                floodlightColorR: 1f,
                floodlightColorG: 0.88f,
                floodlightColorB: 0.7f,
                fillIntensity: 1.0f,
                fillColorR: 1f,
                fillColorG: 0.75f,
                fillColorB: 0.55f,
                ambientR: 0.25f,
                ambientG: 0.22f,
                ambientB: 0.30f
            );
        }
    }
}
