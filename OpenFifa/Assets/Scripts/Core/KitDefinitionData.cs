using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Data for one kit (home, away or third) of one team. This is the JSON schema
    /// for kit libraries: enum-like values are strings so the files stay readable and
    /// licensed kits can be added as data. Public fields so UnityEngine.JsonUtility
    /// can (de)serialize it. Validate with <see cref="KitDefinitionValidator"/>.
    /// </summary>
    [Serializable]
    public class KitDefinitionData
    {
        /// <summary>Unique kit id, e.g. "harbour-city-home".</summary>
        public string Id = "";

        /// <summary>Owning team id (see <see cref="KitTeamData.Id"/>).</summary>
        public string TeamId = "";

        /// <summary>"Home", "Away" or "Third" (<see cref="KitVariant"/>).</summary>
        public string Variant = "Home";

        /// <summary>Shirt base colour, "#RRGGBB" or "#RRGGBBAA".</summary>
        public string PrimaryColor = "#FFFFFF";

        /// <summary>Pattern colour (stripes, hoops, sash...).</summary>
        public string SecondaryColor = "#000000";

        /// <summary>Trim colour (collar, cuffs) and crest outline.</summary>
        public string TertiaryColor = "#FFFFFF";

        /// <summary>Shorts colour (used by the capsule/full-body mapping).</summary>
        public string ShortsColor = "#FFFFFF";

        /// <summary>Socks colour (used by the capsule/full-body mapping).</summary>
        public string SocksColor = "#FFFFFF";

        /// <summary><see cref="JerseyPattern"/> name, e.g. "Stripes".</summary>
        public string Pattern = "Solid";

        /// <summary>Pattern density multiplier (0.1 - 10).</summary>
        public float PatternScale = 1f;

        /// <summary><see cref="CollarStyle"/> name.</summary>
        public string Collar = "Round";

        /// <summary><see cref="KitFontStyle"/> name for name and number printing.</summary>
        public string FontStyle = "Block";

        /// <summary><see cref="KitNameLayout"/> name.</summary>
        public string NameLayout = "Straight";

        /// <summary>Number and name fill colour.</summary>
        public string NumberColor = "#000000";

        /// <summary>Outline colour around name and number.</summary>
        public string NumberOutlineColor = "#FFFFFF";

        /// <summary>Outline width in glyph stroke units (0 = no outline, max 0.6).</summary>
        public float NumberOutlineWidth = 0f;

        /// <summary>Print a small number on the front of the shirt.</summary>
        public bool ShowFrontNumber = true;

        /// <summary>Fabric sheen strength 0-1 (soft grazing-angle highlight of polyester knit).</summary>
        public float FabricSheen = 0.5f;

        /// <summary>Optional licensed textures. Empty = fully procedural.</summary>
        public LicensedKitTextureSet Licensed = new LicensedKitTextureSet();
    }
}
