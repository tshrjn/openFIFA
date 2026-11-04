using System.Collections.Generic;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// ScriptableObject wrapper for one kit (home, away or third). The serialized
    /// <see cref="KitDefinitionData"/> is the same schema as kit library JSON, so a kit can be
    /// authored as an asset or as data. Licensed texture references set here win over the
    /// Resources paths in <see cref="KitDefinitionData.Licensed"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "KitDefinition", menuName = "OpenFifa/Kits/Kit Definition")]
    public class KitDefinition : ScriptableObject
    {
        [SerializeField] private KitDefinitionData data = new KitDefinitionData();

        [Header("Licensed Textures (optional, override data paths)")]
        [SerializeField] private Texture2D licensedAlbedo;
        [SerializeField] private Texture2D licensedNormal;
        [SerializeField] private Texture2D licensedMask;
        [SerializeField] private Texture2D crestTexture;
        [SerializeField] private Texture2D sponsorTexture;

        private static readonly Dictionary<string, Texture2D> ResourceTextureCache = new Dictionary<string, Texture2D>();

        /// <summary>Kit data (JSON schema).</summary>
        public KitDefinitionData Data => data;

        /// <summary>Kit id.</summary>
        public string Id => data != null ? data.Id : "";

        /// <summary>Variant parsed from data.</summary>
        public KitVariant Variant => KitDefinitionConverter.GetVariant(data);

        /// <summary>Design for the existing clash/customization logic.</summary>
        public JerseyDesign ToJerseyDesign() => KitDefinitionConverter.ToJerseyDesign(data);

        /// <summary>Validation errors (empty = valid).</summary>
        public List<string> Validate() => KitDefinitionValidator.Validate(data);

        /// <summary>Serializes the kit data to JSON (same schema as a library "Kits" entry).</summary>
        public string ToJson(bool prettyPrint = true) => JsonUtility.ToJson(data, prettyPrint);

        /// <summary>Creates a runtime (non-asset) definition from data.</summary>
        public static KitDefinition CreateRuntime(KitDefinitionData kitData)
        {
            var definition = CreateInstance<KitDefinition>();
            definition.data = kitData ?? new KitDefinitionData();
            definition.name = string.IsNullOrEmpty(definition.data.Id) ? "RuntimeKit" : definition.data.Id;
            definition.hideFlags = HideFlags.DontSave;
            return definition;
        }

        /// <summary>Creates a runtime definition from a single-kit JSON object.</summary>
        public static KitDefinition FromJson(string json)
        {
            return CreateRuntime(JsonUtility.FromJson<KitDefinitionData>(json));
        }

        /// <summary>
        /// Resolves licensed textures: direct references first, then Resources paths from data.
        /// </summary>
        public KitTextureSet ResolveTextures()
        {
            var licensed = data != null ? data.Licensed : null;
            return new KitTextureSet
            {
                Albedo = licensedAlbedo != null ? licensedAlbedo : LoadResourceTexture(licensed?.AlbedoPath),
                Normal = licensedNormal != null ? licensedNormal : LoadResourceTexture(licensed?.NormalPath),
                Mask = licensedMask != null ? licensedMask : LoadResourceTexture(licensed?.MaskPath),
                Crest = crestTexture != null ? crestTexture : LoadResourceTexture(licensed?.CrestPath),
                Sponsor = sponsorTexture != null ? sponsorTexture : LoadResourceTexture(licensed?.SponsorPath)
            };
        }

        private static Texture2D LoadResourceTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            Texture2D texture;
            if (ResourceTextureCache.TryGetValue(path, out texture) && texture != null)
                return texture;

            texture = Resources.Load<Texture2D>(path);
            if (texture == null)
                Debug.LogWarning($"[KitDefinition] Licensed texture not found in Resources: '{path}'.");
            ResourceTextureCache[path] = texture;
            return texture;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (data == null) return;
            var errors = KitDefinitionValidator.Validate(data);
            if (errors.Count > 0)
                Debug.LogWarning($"[KitDefinition] '{name}': {string.Join(" | ", errors)}", this);
        }
#endif
    }
}
