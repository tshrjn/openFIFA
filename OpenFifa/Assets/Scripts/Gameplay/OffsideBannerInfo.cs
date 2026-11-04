using OpenFifa.Core;
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

        /// <summary>Banner info from pure content plus the two team colours (picks the offender's).</summary>
        public static OffsideBannerInfo From(in OffsideBannerContent content, Color homeColor, Color awayColor) =>
            new OffsideBannerInfo(content.ShirtNumber, content.PlayerName, content.TeamLabel,
                content.OffenderIsHome ? homeColor : awayColor);
    }
}
