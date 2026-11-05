using System;
using System.Collections.Generic;
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
        private static readonly Dictionary<string, KitDefinition> RuntimeKits =
            new Dictionary<string, KitDefinition>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Uses two roster clubs for the next match. Clubs without authored kits in the library (no Home kit for
        /// the club id) get original kits generated from their colours (<see cref="ClubKitFactory"/>), merged into
        /// the library for this session. Resolves outfield kits (away clash rules) and goalkeeper kits.
        /// Returns false when the kit library is unavailable.
        /// </summary>
        public static bool SetMatchClubs(RosterClub home, RosterClub away)
        {
            if (home == null || away == null) return false;
            if (_library == null) _library = KitLibrary.LoadDefault();
            if (_library == null) return false;
            foreach (var club in new[] { home, away })
            {
                if (_library.Logic.GetKit(club.Id, KitVariant.Home) == null)
                    _library.Logic.Merge(ClubKitFactory.Build(club));
            }
            SetMatchTeams(home.Id, away.Id);
            return EnsureSelection();
        }

        /// <summary>
        /// Dresses one player model (any renderer hierarchy; skinned mannequins with the baked body map use the
        /// SkinnedBody mapping) in its side's outfield or goalkeeper kit with the given back name and number,
        /// and a skin tone chosen from the player id. Returns null when kits are unavailable.
        /// </summary>
        public static JerseyRenderer AttachPlayer(GameObject model, bool isHome, bool isKeeper,
            string surname, int number, string playerId)
        {
            if (model == null || !EnsureSelection()) return null;
            var kitData = isKeeper
                ? (isHome ? _selection.HomeKeeperKit : _selection.AwayKeeperKit)
                : (isHome ? _selection.HomeKit : _selection.AwayKit);
            if (kitData == null) return null;

            var team = _library.Logic.GetTeam(isHome ? HomeTeamId : AwayTeamId);
            var jersey = model.GetComponent<JerseyRenderer>();
            if (jersey == null) jersey = model.AddComponent<JerseyRenderer>();
            var look = PlayerLook.ForPlayer(playerId);
            jersey.SetSkinTone(ToColor(look.Skin));
            jersey.SetBootColours(ToColor(look.BootBase), ToColor(look.BootAccent));
            jersey.SetHead(look.Hair, look.Facial, ToColor(look.HairColour), look.EyeTone);
            jersey.ApplyKit(DefinitionFor(kitData), surname, number, team != null ? team.Initials : "");
            if (!jersey.IsApplied) return null;
            if (jersey.MappingMode == KitMappingMode.SkinnedBody)
            {
                HairCapBuilder.Attach(model, look.Hair, ToColor(look.HairColour));
                EarBuilder.Attach(model, ToColor(look.Skin));
            }
            return jersey;
        }

        private static Color ToColor(SimpleColor c) => new Color(c.R, c.G, c.B, 1f);

        private static KitDefinition DefinitionFor(KitDefinitionData data)
        {
            if (_library.Logic.GetKitById(data.Id) == data) return _library.GetDefinition(data.Id);
            if (!RuntimeKits.TryGetValue(data.Id, out var def) || def == null || def.Data != data)
            {
                def = KitDefinition.CreateRuntime(data);
                RuntimeKits[data.Id] = def;
            }
            return def;
        }

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
            RuntimeKits.Clear();
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
                _selection = _library.Logic.ResolveMatchKitsContrasting(HomeTeamId, AwayTeamId);
                if (_selection.ClashUnresolved)
                    Debug.LogWarning($"[MatchKitBinder] Kits of '{HomeTeamId}' and '{AwayTeamId}' clash in every variant.");
                _library.Logic.ResolveGoalkeeperKits(_selection, HomeTeamId, AwayTeamId);
            }
            return _selection.HomeKit != null || _selection.AwayKit != null;
        }
    }
}
