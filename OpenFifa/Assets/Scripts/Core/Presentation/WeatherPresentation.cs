using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Fog for the broadcast camera. The weather presets' exp² densities (<see cref="FogConfig.Density"/>) were tuned
    /// for a close camera; the broadcast camera sits ~19 m up and 28 m back from the near touchline, so the near
    /// touchline is ~34 m away and the far one ~98 m, and Fog's 0.05 would leave the near touchline ~89% fogged.
    /// Instead each preset maps to a fog fraction at the far touchline (Fog, the densest preset, gives
    /// <see cref="MaxFarTouchlineFog"/>; lighter presets fall off with the square root of their density so light rain
    /// still reads), and the exp² density is solved from the camera's distance to the far touchline. The mode stays
    /// ExponentialSquared (the only fog variant kept in player builds, see ProjectSettings/GraphicsSettings.asset).
    /// Distances are Euclidean; URP fogs by view depth, which is never larger, so these fractions are upper bounds.
    /// </summary>
    public static class BroadcastFog
    {
        /// <summary>Densest preset density (<see cref="WeatherType.Fog"/>); maps to <see cref="MaxFarTouchlineFog"/>.</summary>
        public const float ReferencePresetDensity = 0.05f;
        /// <summary>Fog fraction at the far touchline for the densest preset (target: at most 40%).</summary>
        public const float MaxFarTouchlineFog = 0.38f;

        /// <summary>Unity's exp² fog amount (0 = clear, 1 = fully fog colour) at <paramref name="distance"/> metres.</summary>
        public static float FogFactorExp2(float density, float distance)
        {
            double x = density * distance;
            return (float)(1.0 - Math.Exp(-x * x));
        }

        /// <summary>Camera-to-touchline distance for a camera <paramref name="height"/> up and <paramref name="setback"/> back from the near touchline.</summary>
        public static float NearTouchlineDistance(float height, float setback) =>
            (float)Math.Sqrt(height * height + setback * setback);

        /// <summary>Camera-to-far-touchline distance (across a pitch <paramref name="pitchWidth"/> wide).</summary>
        public static float FarTouchlineDistance(float height, float setback, float pitchWidth)
        {
            float h = setback + pitchWidth;
            return (float)Math.Sqrt(height * height + h * h);
        }

        /// <summary>Fog fraction at the far touchline for a preset density: 0 for 0, <see cref="MaxFarTouchlineFog"/> for the reference.</summary>
        public static float FarTouchlineFog(float presetDensity)
        {
            if (presetDensity <= 0f) return 0f;
            float r = presetDensity / ReferencePresetDensity;
            if (r > 1f) r = 1f;
            return MaxFarTouchlineFog * (float)Math.Sqrt(r);
        }

        /// <summary>
        /// Exp² density that gives <see cref="FarTouchlineFog"/> at <paramref name="farTouchlineDistance"/> metres.
        /// A non-positive distance returns the preset density unchanged (no broadcast camera).
        /// </summary>
        public static float Density(float presetDensity, float farTouchlineDistance)
        {
            if (presetDensity <= 0f) return 0f;
            if (farTouchlineDistance <= 0f) return presetDensity;
            float f = FarTouchlineFog(presetDensity);
            return (float)(Math.Sqrt(-Math.Log(1.0 - f)) / farTouchlineDistance);
        }
    }

    /// <summary>Where the rain/snow emitter sits and how long its drops live (<see cref="PrecipitationVolume.Compute"/>).</summary>
    public struct PrecipitationBox
    {
        /// <summary>Ground point under the emitter's centre (world metres) and the emitter height above the ground.</summary>
        public float CenterX, CenterZ, EmitterHeight;
        /// <summary>Box size across the view and along the (flattened) camera forward.</summary>
        public float Width, Depth;
        /// <summary>Horizontal distance from the camera to the box's near edge (wind drift included in the clearance).</summary>
        public float NearEdge;
    }

    /// <summary>
    /// Keeps the precipitation out of the camera's face and across the whole visible pitch. The emitter is a flat box
    /// above the pitch, yawed with the broadcast camera; drops fall straight down (plus the wind tilt), so the falling
    /// volume is the prism under it. Its near edge is at least <see cref="MinCameraClearance"/> metres from the camera
    /// horizontally (plus the worst wind drift over the fall), so no drop comes within that distance and none streak
    /// across the near plane; it spans <see cref="Width"/> metres across the view and reaches past the far touchline.
    /// Drops live just long enough to reach the ground (no wasted particles under the pitch), and the emission rate is
    /// capped so the live count never exceeds the quality tier's budget.
    /// </summary>
    public static class PrecipitationVolume
    {
        public const float MinCameraClearance = 10f;
        /// <summary>Across-view width: a 105 m pitch plus margin (the broadcast view's yaw is followed as well).</summary>
        public const float Width = 110f;
        /// <summary>Metres past the far touchline the box reaches (from the camera's ground point).</summary>
        public const float FarMargin = 8f;
        /// <summary>Emitter height above the camera: high enough that drops are born above the top of the view.</summary>
        public const float HeightAboveCamera = 4f;
        /// <summary>Extra clearance for snow's noise wander (m).</summary>
        public const float WanderMargin = 2f;
        /// <summary>Box area the preset rates (scaled by the caller's emission scale) were tuned on: the old 90 x 63 m box.</summary>
        public const float ReferenceArea = 90f * 63f;

        /// <summary>
        /// Emitter box for a camera at (camX, camY, camZ) looking along the flattened forward (fwdX, fwdZ), with the far
        /// touchline <paramref name="farReach"/> metres from the camera's ground point along that forward, and drops
        /// tilted up to <paramref name="maxTiltDegrees"/> by the wind.
        /// </summary>
        public static PrecipitationBox Compute(float camX, float camY, float camZ, float fwdX, float fwdZ, float farReach,
            float maxTiltDegrees)
        {
            float len = (float)Math.Sqrt(fwdX * fwdX + fwdZ * fwdZ);
            if (len < 1e-4f) { fwdX = 0f; fwdZ = 1f; }
            else { fwdX /= len; fwdZ /= len; }
            float height = Math.Max(1f, camY + HeightAboveCamera);
            float near = MinCameraClearance + WindDrift(height, maxTiltDegrees) + WanderMargin;
            float far = Math.Max(near + 10f, farReach + FarMargin);
            float depth = far - near;
            float mid = near + depth * 0.5f;
            return new PrecipitationBox
            {
                CenterX = camX + fwdX * mid,
                CenterZ = camZ + fwdZ * mid,
                EmitterHeight = height,
                Width = Width,
                Depth = depth,
                NearEdge = near,
            };
        }

        /// <summary>Horizontal drift (m) of a drop falling <paramref name="height"/> metres at up to <paramref name="tiltDegrees"/> from vertical (straight-line bound; gravity only steepens it).</summary>
        public static float WindDrift(float height, float tiltDegrees)
        {
            if (tiltDegrees <= 0f) return 0f;
            if (tiltDegrees > 60f) tiltDegrees = 60f;
            return height * (float)Math.Tan(tiltDegrees * Math.PI / 180.0);
        }

        /// <summary>
        /// Seconds for a drop leaving at <paramref name="speed"/> m/s (tilted <paramref name="tiltDegrees"/> from
        /// vertical) under <paramref name="gravity"/> m/s² to fall <paramref name="height"/> metres; capped at
        /// <paramref name="maxLifetime"/>.
        /// </summary>
        public static float FallTime(float height, float speed, float tiltDegrees, float gravity, float maxLifetime)
        {
            if (height <= 0f) return 0f;
            double v = Math.Max(0f, speed) * Math.Cos(tiltDegrees * Math.PI / 180.0);
            double t;
            if (gravity <= 1e-4f) t = v > 1e-4 ? height / v : maxLifetime;
            else t = (-v + Math.Sqrt(v * v + 2.0 * gravity * height)) / gravity;
            return (float)Math.Min(t, maxLifetime);
        }

        /// <summary>
        /// Particles per second: the preset rate times <paramref name="emissionScale"/>, scaled by the box area against
        /// <see cref="ReferenceArea"/> (same density on a bigger box), capped so rate x lifetime stays within
        /// <paramref name="maxParticles"/>.
        /// </summary>
        public static float EmissionRate(float presetRate, float emissionScale, float area, float lifetime, int maxParticles)
        {
            if (presetRate <= 0f || emissionScale <= 0f) return 0f;
            float rate = presetRate * emissionScale * Math.Max(0f, area) / ReferenceArea;
            if (lifetime > 0f && maxParticles > 0) rate = Math.Min(rate, maxParticles / lifetime);
            return rate;
        }
    }

    /// <summary>
    /// Wet-turf look on stock URP/Lit: the turf's smoothness multiplier rises (wet grass catches the floodlights) and
    /// the albedo darkens a little (water darkens soil and blades). The turf's base-map alpha (smoothness) tops out at
    /// ~0.31 (<see cref="TurfSettings"/>), so <see cref="MaxSmoothnessMultiplier"/> keeps the result well under 1.
    /// </summary>
    public readonly struct PitchWetnessLook
    {
        public const float MaxSmoothnessMultiplier = 2f;
        public const float MaxDarkening = 0.25f;

        /// <summary>0..1 how wet the turf looks.</summary>
        public readonly float Amount;
        /// <summary>Multiplier on URP/Lit _Smoothness (1 = dry).</summary>
        public readonly float SmoothnessMultiplier;
        /// <summary>Multiplier on the turf's _BaseColor RGB (1 = dry).</summary>
        public readonly float AlbedoScale;

        private PitchWetnessLook(float amount)
        {
            Amount = amount;
            SmoothnessMultiplier = 1f + (MaxSmoothnessMultiplier - 1f) * amount;
            AlbedoScale = 1f - MaxDarkening * amount;
        }

        public static PitchWetnessLook FromAmount(float amount)
        {
            if (!(amount > 0f)) amount = 0f;
            if (amount > 1f) amount = 1f;
            return new PitchWetnessLook(amount);
        }

        /// <summary>
        /// Surface dampness of a weather with no rain (the pitch tracker only accumulates wetness while it rains): fog
        /// leaves dew on the grass. 0 for the rest.
        /// </summary>
        public static float Dampness(WeatherType weather) => weather == WeatherType.Fog ? 1f : 0f;

        /// <summary>
        /// Look for a pitch with tracked <paramref name="wetness"/> (0..1, <see cref="PitchConditionTracker"/>) under a
        /// weather whose <see cref="PitchEffectConfig.WetReflectivity"/> sets how wet it can look (heavy rain 0.8,
        /// light rain 0.3, fog 0.15, clear 0).
        /// </summary>
        public static PitchWetnessLook For(WeatherType weather, float wetReflectivity, float wetness)
        {
            float w = Math.Max(wetness, Dampness(weather));
            if (w > 1f) w = 1f;
            return FromAmount(wetReflectivity * w);
        }
    }
}
