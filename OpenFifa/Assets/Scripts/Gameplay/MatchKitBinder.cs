using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// One-call kit setup for spawned players. Resolves the match kits once (home team in its
    /// Home kit, away team in the first non-clashing variant via <see cref="KitLibraryLogic"/>),
    /// then adds a <see cref="JerseyRenderer"/> to each player with the roster name/number.
    /// If the kit shader or library is unavailable it leaves the player untouched, so the
    /// caller's flat-colour material remains as a fallback.
    /// </summary>
    public static class MatchKitBinder
    {
        private static KitLibrary _library;
        private static string _homeTeamId;
        private static string _awayTeamId;
        private static MatchKitSelection _selection;

        /// <summary>
        /// Overrides the teams for the next match. Null/empty uses the library defaults.
        /// </summary>
        public static void SetMatchTeams(string homeTeamId, string awayTeamId)
        {
            _homeTeamId = homeTeamId;
            _awayTeamId = awayTeamId;
            _selection = null;
        }

        /// <summary>Current match kit selection (resolves on first use).</summary>
        public static MatchKitSelection Selection
        {
            get
            {
                EnsureSelection();
                return _selection;
            }
        }

        /// <summary>
        /// Attaches and applies the match kit to a player. <paramref name="slotIndex"/> is the
        /// formation slot (selects the roster entry). Returns the renderer, or null when kits
        /// are unavailable.
        /// </summary>
        public static JerseyRenderer Attach(GameObject player, bool isHome, int slotIndex)
        {
            if (player == null || !EnsureSelection()) return null;

            var kitData = isHome ? _selection.HomeKit : _selection.AwayKit;
            if (kitData == null) return null;

            string teamId = isHome ? HomeTeamId : AwayTeamId;
            var kit = _library.GetDefinition(kitData.Id);
            var team = _library.Logic.GetTeam(teamId);
            var roster = _library.Logic.GetRosterEntry(teamId, slotIndex);

            var jersey = player.GetComponent<JerseyRenderer>();
            if (jersey == null) jersey = player.AddComponent<JerseyRenderer>();
            jersey.ApplyKit(kit, roster.Name, roster.Number, team != null ? team.Initials : "");
            return jersey.IsApplied ? jersey : null;
        }

        /// <summary>Drops cached state (e.g. after loading a licensed kit pack).</summary>
        public static void Reset()
        {
            _library = null;
            _selection = null;
        }

        private static string HomeTeamId =>
            string.IsNullOrEmpty(_homeTeamId) ? _library.Logic.DefaultHomeTeamId : _homeTeamId;

        private static string AwayTeamId =>
            string.IsNullOrEmpty(_awayTeamId) ? _library.Logic.DefaultAwayTeamId : _awayTeamId;

        private static bool EnsureSelection()
        {
            if (_library == null)
            {
                _library = KitLibrary.LoadDefault();
                _selection = null;
            }
            if (_library == null) return false;

            if (_selection == null)
            {
                _selection = _library.Logic.ResolveMatchKits(HomeTeamId, AwayTeamId);
                if (_selection.ClashUnresolved)
                    Debug.LogWarning($"[MatchKitBinder] Kits of '{HomeTeamId}' and '{AwayTeamId}' clash in every variant.");
            }
            return _selection.HomeKit != null || _selection.AwayKit != null;
        }
    }
}
