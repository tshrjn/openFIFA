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
    }
}
