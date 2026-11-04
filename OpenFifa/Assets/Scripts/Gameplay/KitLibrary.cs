using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Collection of teams and kits built from JSON sources and/or <see cref="KitDefinition"/> assets.
    /// Load order (later wins by id): JSON sources in list order, then teams/kits set on the asset.
    ///
    /// <see cref="LoadDefault"/> resolves the library used by matches:
    ///   1. Resources/Kits/KitLibrary.asset if it exists, else
    ///   2. a runtime library from Resources/Kits/DefaultKits.json (original placeholder teams),
    ///   then Resources/Kits/LicensedKits.json is merged on top when present, so licensed kits
    ///   can ship as data without code changes.
    /// </summary>
    [CreateAssetMenu(fileName = "KitLibrary", menuName = "OpenFifa/Kits/Kit Library")]
    public class KitLibrary : ScriptableObject
    {
        public const string LibraryResourcePath = "Kits/KitLibrary";
        public const string DefaultJsonResourcePath = "Kits/DefaultKits";
        public const string LicensedJsonResourcePath = "Kits/LicensedKits";

        [SerializeField] private List<TextAsset> jsonSources = new List<TextAsset>();
        [SerializeField] private List<KitTeamData> teams = new List<KitTeamData>();
        [SerializeField] private List<KitDefinition> kits = new List<KitDefinition>();
        [SerializeField] private string defaultHomeTeamId = "";
        [SerializeField] private string defaultAwayTeamId = "";

        private static KitLibrary _default;

        private KitLibraryLogic _logic;
        private readonly List<string> _runtimeJson = new List<string>();
        private readonly Dictionary<string, KitDefinition> _definitions =
            new Dictionary<string, KitDefinition>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Validated kit/team index (built lazily).</summary>
        public KitLibraryLogic Logic
        {
            get
            {
                if (_logic == null) Rebuild();
                return _logic;
            }
        }

        /// <summary>
        /// The library used by matches (cached). See class summary for resolution order.
        /// </summary>
        public static KitLibrary LoadDefault()
        {
            if (_default != null) return _default;

            _default = Resources.Load<KitLibrary>(LibraryResourcePath);
            if (_default == null)
            {
                _default = CreateInstance<KitLibrary>();
                _default.name = "RuntimeKitLibrary";
                _default.hideFlags = HideFlags.DontSave;
                var placeholder = Resources.Load<TextAsset>(DefaultJsonResourcePath);
                if (placeholder != null) _default.jsonSources.Add(placeholder);
                else Debug.LogWarning($"[KitLibrary] Missing Resources/{DefaultJsonResourcePath}.json; no kits available.");
            }

            var licensed = Resources.Load<TextAsset>(LicensedJsonResourcePath);
            if (licensed != null && !_default.jsonSources.Contains(licensed))
                _default.jsonSources.Add(licensed);

            _default.Rebuild();
            return _default;
        }

        /// <summary>Parses a kit library JSON document.</summary>
        public static KitLibraryData ParseJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<KitLibraryData>(json);
        }

        /// <summary>Serializes library data to JSON (useful for exporting a template for licensed packs).</summary>
        public static string ToJson(KitLibraryData data, bool prettyPrint = true)
        {
            return JsonUtility.ToJson(data, prettyPrint);
        }

        /// <summary>
        /// Merges an extra JSON document at runtime (e.g. a licensed pack loaded from disk)
        /// and rebuilds. Returns the library-level validation problems.
        /// </summary>
        public List<string> AddJson(string json)
        {
            _runtimeJson.Add(json);
            Rebuild();
            return Logic.ValidateLibrary();
        }

        /// <summary>Rebuilds the index from all sources.</summary>
        public void Rebuild()
        {
            _logic = new KitLibraryLogic();
            _definitions.Clear();

            foreach (var source in jsonSources)
            {
                if (source == null) continue;
                MergeJson(source.text, source.name);
            }
            foreach (var json in _runtimeJson)
                MergeJson(json, "runtime");

            var assetData = new KitLibraryData
            {
                DefaultHomeTeamId = defaultHomeTeamId,
                DefaultAwayTeamId = defaultAwayTeamId,
                Teams = new List<KitTeamData>(teams)
            };
            _logic.Merge(assetData);

            foreach (var kit in kits)
            {
                if (kit == null) continue;
                if (_logic.AddOrReplace(kit.Data))
                    _definitions[kit.Id] = kit;
            }

            foreach (var problem in _logic.ValidateLibrary())
                Debug.LogWarning($"[KitLibrary] {problem}");
        }

        /// <summary>
        /// Definition object for a kit id: the authored asset when one exists, otherwise a
        /// runtime wrapper around the JSON data (cached).
        /// </summary>
        public KitDefinition GetDefinition(string kitId)
        {
            var data = Logic.GetKitById(kitId);
            if (data == null) return null;

            KitDefinition definition;
            if (_definitions.TryGetValue(kitId, out definition) && definition != null && definition.Data == data)
                return definition;

            definition = KitDefinition.CreateRuntime(data);
            _definitions[kitId] = definition;
            return definition;
        }

        /// <summary>Definition for a team's variant, or null.</summary>
        public KitDefinition GetDefinition(string teamId, KitVariant variant)
        {
            var data = Logic.GetKit(teamId, variant);
            return data != null ? GetDefinition(data.Id) : null;
        }

        private void MergeJson(string json, string sourceName)
        {
            try
            {
                var data = ParseJson(json);
                if (data == null)
                {
                    Debug.LogWarning($"[KitLibrary] '{sourceName}' is empty.");
                    return;
                }
                _logic.Merge(data);
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning($"[KitLibrary] '{sourceName}' is not valid JSON: {e.Message}");
            }
        }
    }
}
