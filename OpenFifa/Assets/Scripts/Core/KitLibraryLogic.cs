using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Indexed, validated view over <see cref="KitLibraryData"/>: kit lookup by team and
    /// variant, merging of extra kit packs (e.g. licensed kits added as data), and
    /// match kit selection using the existing <see cref="KitVariantManager"/> clash rules.
    /// Invalid kits are excluded from lookups and reported in <see cref="Errors"/>.
    /// Pure C#.
    /// </summary>
    public class KitLibraryLogic
    {
        private readonly Dictionary<string, KitTeamData> _teams =
            new Dictionary<string, KitTeamData>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, KitDefinitionData> _kitsById =
            new Dictionary<string, KitDefinitionData>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _errors = new List<string>();

        private string _defaultHomeTeamId = "";
        private string _defaultAwayTeamId = "";

        public KitLibraryLogic()
        {
        }

        public KitLibraryLogic(KitLibraryData data)
        {
            Merge(data);
        }

        /// <summary>Validation errors collected while merging data.</summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>Number of valid kits.</summary>
        public int KitCount => _kitsById.Count;

        /// <summary>Number of teams.</summary>
        public int TeamCount => _teams.Count;

        /// <summary>Default home team id (from the last merged file that set one).</summary>
        public string DefaultHomeTeamId => _defaultHomeTeamId;

        /// <summary>Default away team id (from the last merged file that set one).</summary>
        public string DefaultAwayTeamId => _defaultAwayTeamId;

        /// <summary>
        /// Merges a library file. Teams and kits with an existing id are replaced, so a
        /// licensed pack loaded after the placeholder library overrides it.
        /// </summary>
        public void Merge(KitLibraryData data)
        {
            if (data == null) return;

            if (data.SchemaVersion > KitLibraryData.CurrentSchemaVersion)
                _errors.Add($"Kit library schema {data.SchemaVersion} is newer than supported {KitLibraryData.CurrentSchemaVersion}; loading best-effort.");

            if (!string.IsNullOrEmpty(data.DefaultHomeTeamId)) _defaultHomeTeamId = data.DefaultHomeTeamId;
            if (!string.IsNullOrEmpty(data.DefaultAwayTeamId)) _defaultAwayTeamId = data.DefaultAwayTeamId;

            if (data.Teams != null)
            {
                foreach (var team in data.Teams)
                {
                    if (team == null || string.IsNullOrWhiteSpace(team.Id))
                    {
                        _errors.Add("Team entry without Id skipped.");
                        continue;
                    }
                    _teams[team.Id] = team;
                }
            }

            if (data.Kits != null)
            {
                var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kit in data.Kits)
                {
                    if (kit != null && !string.IsNullOrEmpty(kit.Id) && !seenInFile.Add(kit.Id))
                        _errors.Add($"Duplicate kit id '{kit.Id}' in the same file; last one wins.");
                    AddOrReplace(kit);
                }
            }
        }

        /// <summary>
        /// Adds a single kit (or replaces the kit with the same id, or the same team+variant).
        /// Returns false and records errors when the kit is invalid.
        /// </summary>
        public bool AddOrReplace(KitDefinitionData kit)
        {
            var kitErrors = KitDefinitionValidator.Validate(kit);
            if (kitErrors.Count > 0)
            {
                _errors.AddRange(kitErrors);
                return false;
            }

            // One kit per team+variant: a new kit for the same slot replaces the old one.
            var variant = KitDefinitionConverter.GetVariant(kit);
            var existing = GetKit(kit.TeamId, variant);
            if (existing != null && !string.Equals(existing.Id, kit.Id, StringComparison.OrdinalIgnoreCase))
                _kitsById.Remove(existing.Id);

            _kitsById[kit.Id] = kit;
            return true;
        }

        /// <summary>
        /// Library-level consistency problems: kits for unknown teams and teams without a home kit.
        /// Combined with <see cref="Errors"/>.
        /// </summary>
        public List<string> ValidateLibrary()
        {
            var problems = new List<string>(_errors);

            foreach (var kit in _kitsById.Values)
            {
                if (!_teams.ContainsKey(kit.TeamId))
                    problems.Add($"Kit '{kit.Id}' references unknown team '{kit.TeamId}'.");
            }

            foreach (var team in _teams.Values)
            {
                if (GetKit(team.Id, KitVariant.Home) == null)
                    problems.Add($"Team '{team.Id}' has no Home kit.");
            }

            if (!string.IsNullOrEmpty(_defaultHomeTeamId) && !_teams.ContainsKey(_defaultHomeTeamId))
                problems.Add($"DefaultHomeTeamId '{_defaultHomeTeamId}' is not a team.");
            if (!string.IsNullOrEmpty(_defaultAwayTeamId) && !_teams.ContainsKey(_defaultAwayTeamId))
                problems.Add($"DefaultAwayTeamId '{_defaultAwayTeamId}' is not a team.");

            return problems;
        }

        /// <summary>Kit by id, or null.</summary>
        public KitDefinitionData GetKitById(string kitId)
        {
            if (string.IsNullOrEmpty(kitId)) return null;
            KitDefinitionData kit;
            return _kitsById.TryGetValue(kitId, out kit) ? kit : null;
        }

        /// <summary>Kit for a team and variant, or null.</summary>
        public KitDefinitionData GetKit(string teamId, KitVariant variant)
        {
            if (string.IsNullOrEmpty(teamId)) return null;
            foreach (var kit in _kitsById.Values)
            {
                if (string.Equals(kit.TeamId, teamId, StringComparison.OrdinalIgnoreCase)
                    && KitDefinitionConverter.GetVariant(kit) == variant)
                {
                    return kit;
                }
            }
            return null;
        }

        /// <summary>Team by id, or null.</summary>
        public KitTeamData GetTeam(string teamId)
        {
            if (string.IsNullOrEmpty(teamId)) return null;
            KitTeamData team;
            return _teams.TryGetValue(teamId, out team) ? team : null;
        }

        /// <summary>
        /// Builds a <see cref="KitVariantManager"/> with every kit the team owns, so existing
        /// clash logic (and <c>JerseyRenderer.ResolveClash</c>) works on library kits.
        /// </summary>
        public KitVariantManager BuildVariantManager(string teamId)
        {
            var manager = new KitVariantManager();
            foreach (KitVariant variant in Enum.GetValues(typeof(KitVariant)))
            {
                var kit = GetKit(teamId, variant);
                if (kit != null)
                    manager.SetKit(variant, KitDefinitionConverter.ToJerseyDesign(kit));
            }
            return manager;
        }

        /// <summary>
        /// Chooses kits for a match. The home side wears its Home kit (falling back to any kit
        /// it has). The away side uses <see cref="KitVariantManager.SelectNonClashingVariant"/>
        /// against the home primary colour: Home, then Away, then Third.
        /// </summary>
        public MatchKitSelection ResolveMatchKits(string homeTeamId, string awayTeamId, float clashThreshold)
        {
            var result = new MatchKitSelection();

            result.HomeVariant = KitVariant.Home;
            result.HomeKit = GetKit(homeTeamId, KitVariant.Home) ?? FirstKitOf(homeTeamId, out result.HomeVariant);

            var awayManager = BuildVariantManager(awayTeamId);
            if (awayManager.KitCount == 0)
            {
                result.AwayVariant = KitVariant.Home;
                return result;
            }

            if (result.HomeKit == null)
            {
                result.AwayVariant = KitVariant.Home;
                result.AwayKit = GetKit(awayTeamId, KitVariant.Home) ?? FirstKitOf(awayTeamId, out result.AwayVariant);
                return result;
            }

            var homePrimary = KitDefinitionConverter.ToJerseyDesign(result.HomeKit).PrimaryColor;
            var chosen = awayManager.SelectNonClashingVariant(homePrimary, clashThreshold);

            result.AwayVariant = chosen;
            result.AwayKit = GetKit(awayTeamId, chosen);
            if (result.AwayKit == null)
            {
                result.AwayKit = FirstKitOf(awayTeamId, out result.AwayVariant);
            }

            var awayPrimary = KitDefinitionConverter.ToJerseyDesign(result.AwayKit).PrimaryColor;
            result.ClashUnresolved = JerseyComparer.ColorDistance(homePrimary, awayPrimary) <= clashThreshold;
            return result;
        }

        /// <summary>Resolves match kits with <see cref="JerseyComparer.DefaultClashThreshold"/>.</summary>
        public MatchKitSelection ResolveMatchKits(string homeTeamId, string awayTeamId)
        {
            return ResolveMatchKits(homeTeamId, awayTeamId, JerseyComparer.DefaultClashThreshold);
        }

        /// <summary>
        /// Name and number for a formation slot: the team roster entry when present,
        /// otherwise an empty name and number slot+1.
        /// </summary>
        public KitRosterEntry GetRosterEntry(string teamId, int slotIndex)
        {
            var team = GetTeam(teamId);
            if (team != null && team.Roster != null && slotIndex >= 0 && slotIndex < team.Roster.Count
                && team.Roster[slotIndex] != null)
            {
                return team.Roster[slotIndex];
            }
            return new KitRosterEntry { Name = "", Number = Math.Max(1, Math.Min(99, slotIndex + 1)) };
        }

        private KitDefinitionData FirstKitOf(string teamId, out KitVariant variant)
        {
            foreach (KitVariant v in Enum.GetValues(typeof(KitVariant)))
            {
                var kit = GetKit(teamId, v);
                if (kit != null)
                {
                    variant = v;
                    return kit;
                }
            }
            variant = KitVariant.Home;
            return null;
        }
    }
}
