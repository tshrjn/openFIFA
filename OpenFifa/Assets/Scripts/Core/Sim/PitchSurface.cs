namespace OpenFifa.Core
{
    /// <summary>Pitch condition for ball roll and bounce (<see cref="BallSimConfig.ApplySurface"/>).</summary>
    public enum PitchSurface
    {
        Normal = 0,
        Dry = 1,
        Wet = 2,
        Heavy = 3,
    }

    public static class PitchSurfaces
    {
        /// <summary>
        /// The pitch condition a match's weather implies: rain makes the surface wet (the ball skids), snow makes it
        /// heavy (dead bounce, short roll). Clear, overcast and fog play as a normal, watered pitch.
        /// </summary>
        public static PitchSurface ForWeather(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.LightRain:
                case WeatherType.HeavyRain:
                    return PitchSurface.Wet;
                case WeatherType.Snow:
                    return PitchSurface.Heavy;
                default:
                    return PitchSurface.Normal;
            }
        }
    }
}
