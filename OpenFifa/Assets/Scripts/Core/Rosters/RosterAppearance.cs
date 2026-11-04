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
        /// <summary>One of <see cref="HairStyles"/>.</summary>
        public string HairStyle { get; }
        /// <summary>"#RRGGBB".</summary>
        public string HairColour { get; }
        /// <summary>One of <see cref="FacialHairStyles"/>.</summary>
        public string FacialHair { get; }
        public int HeightCm { get; }
        /// <summary>One of <see cref="Builds"/>.</summary>
        public string Build { get; }
        /// <summary>"#RRGGBB".</summary>
        public string BootColour { get; }

        public RosterAppearance(float skinTone, string hairStyle, string hairColour, string facialHair, int heightCm,
            string build, string bootColour)
        {
            SkinTone = skinTone < 0f ? 0f : (skinTone > 1f ? 1f : skinTone);
            HairStyle = string.IsNullOrEmpty(hairStyle) ? "short" : hairStyle;
            HairColour = string.IsNullOrEmpty(hairColour) ? "#2B1D14" : hairColour;
            FacialHair = string.IsNullOrEmpty(facialHair) ? "none" : facialHair;
            HeightCm = heightCm;
            Build = string.IsNullOrEmpty(build) ? "average" : build;
            BootColour = string.IsNullOrEmpty(bootColour) ? "#111111" : bootColour;
        }

        public static bool IsToken(string[] allowed, string value) => System.Array.IndexOf(allowed, value) >= 0;
    }
}
