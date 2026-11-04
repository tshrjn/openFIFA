namespace OpenFifa.Core
{
    /// <summary>
    /// Parses the single-player weather option (<c>-spWeather</c>) into a <see cref="WeatherType"/> and an optional
    /// pitch-condition override. Accepts every <see cref="WeatherType"/> name (case-insensitive) plus the pitch
    /// shorthands <c>dry</c> (clear sky, firm fast pitch), <c>normal</c>, <c>wet</c>/<c>rain</c> (light rain) and
    /// <c>heavy</c> (snow).
    /// </summary>
    public static class MatchWeatherOption
    {
        public const string Values = "dry|normal|wet|rain|heavy|clear|lightrain|heavyrain|snow|fog|overcast";

        public static bool TryParse(string text, out WeatherType weather, out PitchSurface? surface)
        {
            weather = WeatherType.Clear;
            surface = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            switch (text.Trim().ToLowerInvariant())
            {
                case "dry": surface = PitchSurface.Dry; return true;
                case "normal": surface = PitchSurface.Normal; return true;
                case "wet":
                case "rain": weather = WeatherType.LightRain; return true;
                case "heavy": weather = WeatherType.Snow; return true;
                case "clear": return true;
                case "lightrain": weather = WeatherType.LightRain; return true;
                case "heavyrain": weather = WeatherType.HeavyRain; return true;
                case "snow": weather = WeatherType.Snow; return true;
                case "fog": weather = WeatherType.Fog; return true;
                case "overcast": weather = WeatherType.Overcast; return true;
                default: return false;
            }
        }

        /// <summary>
        /// Sky and lighting for a match's weather, applied on top of the broadcast lighting: the sun and its shadows
        /// fade under cloud and rain, the ambient fill drops a little, and the sky background greys. Clear is the
        /// unchanged broadcast look (all scales 1, no grey). Values are chosen for feel, darkest in heavy rain.
        /// </summary>
        public static WeatherLook LookFor(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Overcast: return new WeatherLook(0.7f, 0.55f, 0.95f, 0.35f, false, false);
                case WeatherType.Fog: return new WeatherLook(0.65f, 0.45f, 0.95f, 0.5f, false, false);
                case WeatherType.LightRain: return new WeatherLook(0.6f, 0.45f, 0.9f, 0.5f, true, false);
                case WeatherType.Snow: return new WeatherLook(0.6f, 0.4f, 1f, 0.6f, false, true);
                case WeatherType.HeavyRain: return new WeatherLook(0.45f, 0.3f, 0.85f, 0.65f, true, false);
                default: return new WeatherLook(1f, 1f, 1f, 0f, false, false);
            }
        }
    }

    /// <summary>Lighting multipliers and precipitation flags for one <see cref="WeatherType"/> (<see cref="MatchWeatherOption.LookFor"/>).</summary>
    public readonly struct WeatherLook
    {
        /// <summary>Multiplier on the broadcast sun's intensity.</summary>
        public readonly float SunScale;
        /// <summary>Multiplier on the sun's shadow strength (cloud softens shadows).</summary>
        public readonly float ShadowScale;
        /// <summary>Ambient intensity (1 = broadcast default).</summary>
        public readonly float AmbientScale;
        /// <summary>0..1 blend of the sky background towards cloud grey.</summary>
        public readonly float SkyGrey;
        public readonly bool Rain;
        public readonly bool Snow;

        public WeatherLook(float sunScale, float shadowScale, float ambientScale, float skyGrey, bool rain, bool snow)
        {
            SunScale = sunScale; ShadowScale = shadowScale; AmbientScale = ambientScale; SkyGrey = skyGrey;
            Rain = rain; Snow = snow;
        }
    }
}
