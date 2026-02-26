using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# utility for formatting HUD text.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public static class HUDFormatter
    {
        /// <summary>
        /// Format the score display string.
        /// </summary>
        public static string FormatScore(MatchScore score,
            string teamAName = "TeamA", string teamBName = "TeamB")
        {
            return $"{teamAName} {score.GetScore(TeamIdentifier.TeamA)} - " +
                   $"{score.GetScore(TeamIdentifier.TeamB)} {teamBName}";
        }

        /// <summary>
        /// Format the timer display string as MM:SS.
        /// </summary>
        public static string FormatTimer(float remainingSeconds)
        {
            if (remainingSeconds < 0f) remainingSeconds = 0f;

            var timeSpan = TimeSpan.FromSeconds(remainingSeconds);
            return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }
    }
}
