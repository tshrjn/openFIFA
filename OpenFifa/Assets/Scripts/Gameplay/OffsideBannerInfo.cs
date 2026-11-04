using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// What the lower-third shows about the offending player.
    /// </summary>
    public readonly struct OffsideBannerInfo
    {
        public int ShirtNumber { get; }
        public string PlayerName { get; }
        public string TeamLabel { get; }
        public Color TeamColor { get; }

        public OffsideBannerInfo(int shirtNumber, string playerName, string teamLabel, Color teamColor)
        {
            ShirtNumber = shirtNumber;
            PlayerName = playerName;
            TeamLabel = teamLabel;
            TeamColor = teamColor;
        }
    }
}
