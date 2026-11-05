using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// What the offside lower-third says about the offender: shirt number, name (the roster's known-as name, i.e.
    /// the surname for most players), team label, and which side's colour to use. Built from an
    /// <see cref="OffsideDecision"/>; pure C#, so the mapping is EditMode testable.
    /// </summary>
    public readonly struct OffsideBannerContent
    {
        public int ShirtNumber { get; }
        public string PlayerName { get; }
        public string TeamLabel { get; }
        public bool OffenderIsHome { get; }

        public OffsideBannerContent(int shirtNumber, string playerName, string teamLabel, bool offenderIsHome)
        {
            ShirtNumber = shirtNumber;
            PlayerName = playerName ?? string.Empty;
            TeamLabel = teamLabel ?? string.Empty;
            OffenderIsHome = offenderIsHome;
        }

        /// <summary>
        /// Banner content for a decision from the match sim (<see cref="SimOffsideDecisionSource"/>): OffenderId is a
        /// sim player id (0-10 home, 11-21 away), <paramref name="names"/> and <paramref name="shirtNumbers"/> are
        /// indexed by sim id. Missing data falls back to "Player n" / the slot number (id % 11 + 1).
        /// </summary>
        public static OffsideBannerContent FromSimDecision(OffsideDecision decision, IReadOnlyList<string> names,
            IReadOnlyList<int> shirtNumbers, string homeLabel, string awayLabel)
        {
            if (decision == null || !decision.IsOffence) return new OffsideBannerContent(0, string.Empty, string.Empty, true);
            int id = decision.OffenderId;
            bool home = decision.OffenderTeam == TeamIdentifier.TeamA;
            int slot = id >= 0 ? id % MatchSetup.PlayersPerTeam + 1 : 0;
            int number = shirtNumbers != null && id >= 0 && id < shirtNumbers.Count && shirtNumbers[id] > 0 ? shirtNumbers[id] : slot;
            string name = names != null && id >= 0 && id < names.Count ? names[id] : null;
            if (string.IsNullOrEmpty(name)) name = "Player " + slot;
            return new OffsideBannerContent(number, name, home ? homeLabel : awayLabel, home);
        }
    }
}
