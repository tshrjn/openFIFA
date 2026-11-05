namespace OpenFifa.Core
{
    /// <summary>
    /// Optional look of a roster player, as data for the player-appearance pipeline (Mama-Fifa's contract):
    /// skin tone, hair, facial hair, height, build and boots. Strings are the schema tokens so renderers can map
    /// them without this layer knowing about meshes or materials. Immutable once loaded.
    /// </summary>
    public sealed class RosterAppearance
    {
        public static readonly string[] HairStyles = { "buzz", "short", "curly", "afro", "long_tied", "bald", "braids" };
        public static readonly string[] FacialHairStyles = { "none", "stubble", "beard", "moustache" };
        public static readonly string[] Builds = { "slim", "average", "stocky" };

        /// <summary>0 (lightest) to 1 (darkest).</summary>
        public float SkinTone { get; }
        /// <summary>One of <see cref="HairStyles"/>, or null when unknown (the renderer picks a seeded style).</summary>
        public string HairStyle { get; }
        /// <summary>"#RRGGBB".</summary>
        public string HairColour { get; }
        /// <summary>One of <see cref="FacialHairStyles"/>, or null when unknown (the renderer picks a seeded one).</summary>
        public string FacialHair { get; }
        public int HeightCm { get; }
        /// <summary>One of <see cref="Builds"/>.</summary>
        public string Build { get; }
        /// <summary>"#RRGGBB".</summary>
        public string BootColour { get; }

        /// <summary>False when the roster left the field out (the value above is then a default).</summary>
        public bool HasSkinTone { get; private set; } = true;
        public bool HasHairColour { get; private set; } = true;
        public bool HasBootColour { get; private set; } = true;
        public bool HasHeight { get; private set; } = true;
        public bool HasBuild { get; private set; } = true;

        /// <summary>
        /// Builds an appearance recording which fields the roster actually gave (null = left out), so renderers fill
        /// only the missing ones from their own seeded choice.
        /// </summary>
        public static RosterAppearance FromFields(float? skinTone, string hairStyle, string hairColour, string facialHair,
            int? heightCm, string build, string bootColour)
        {
            return new RosterAppearance(skinTone ?? 0.5f, hairStyle, hairColour, facialHair, heightCm ?? 0, build, bootColour)
            {
                HasSkinTone = skinTone.HasValue,
                HasHairColour = !string.IsNullOrEmpty(hairColour),
                HasBootColour = !string.IsNullOrEmpty(bootColour),
                HasHeight = heightCm.HasValue && heightCm.Value > 0,
                HasBuild = !string.IsNullOrEmpty(build),
            };
        }

        public RosterAppearance(float skinTone, string hairStyle, string hairColour, string facialHair, int heightCm,
            string build, string bootColour)
        {
            SkinTone = skinTone < 0f ? 0f : (skinTone > 1f ? 1f : skinTone);
            HairStyle = string.IsNullOrEmpty(hairStyle) ? null : hairStyle;
            HairColour = string.IsNullOrEmpty(hairColour) ? "#2B1D14" : hairColour;
            FacialHair = string.IsNullOrEmpty(facialHair) ? null : facialHair;
            HeightCm = heightCm;
            Build = string.IsNullOrEmpty(build) ? "average" : build;
            BootColour = string.IsNullOrEmpty(bootColour) ? "#111111" : bootColour;
        }

        public static bool IsToken(string[] allowed, string value) => System.Array.IndexOf(allowed, value) >= 0;
    }
}
