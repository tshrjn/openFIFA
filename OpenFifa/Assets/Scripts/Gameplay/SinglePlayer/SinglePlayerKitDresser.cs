using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay.SinglePlayer
{
    /// <summary>
    /// Puts the 22 single-player mannequins in football kits: home / away outfield kits with clash resolution and
    /// distinct goalkeeper kits (<see cref="MatchKitBinder"/>), squad numbers and surnames from the roster the
    /// session uses, and a per-player skin tone. One kit material instance per player (SRP Batcher friendly, no
    /// MaterialPropertyBlocks, no per-player keywords).
    /// </summary>
    public static class SinglePlayerKitDresser
    {
        /// <summary>Resolves the match kits for two roster clubs. False = kits unavailable (keep the tint fallback).</summary>
        public static bool Prepare(RosterClub home, RosterClub away) =>
            home != null && away != null && MatchKitBinder.SetMatchClubs(home, away);

        /// <summary>Dresses player <paramref name="id"/> (0-10 home, 11-21 away; slot 0 of each side is the keeper).</summary>
        public static bool Dress(GameObject model, SinglePlayerMatchSession session, RosterClub home, RosterClub away, int id)
        {
            if (model == null) return false;
            bool isHome = id < MatchSetup.PlayersPerTeam;
            bool keeper = id == 0 || id == MatchSetup.PlayersPerTeam;
            var ids = session != null ? session.Options.PlayerIds : null;
            string playerId = ids != null && id < ids.Length ? ids[id] : null;
            var player = (isHome ? home : away)?.FindPlayer(playerId);
            string surname = player != null ? player.KnownAs : "";
            int number = player != null && player.ShirtNumber > 0 ? player.ShirtNumber : id % MatchSetup.PlayersPerTeam + 1;
            return MatchKitBinder.AttachPlayer(model, isHome, keeper, surname, number, playerId ?? id.ToString()) != null;
        }
    }
}
