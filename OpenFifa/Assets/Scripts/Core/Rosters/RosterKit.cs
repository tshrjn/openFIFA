namespace OpenFifa.Core
{
    /// <summary>
    /// One kit of a roster club. Field names follow KitDefinitionData (Scripts/Core, kits PR #3) so a club's
    /// roster kits convert one to one: Primary -> PrimaryColor, Pattern -> Pattern (capitalised), and so on.
    /// Colours are "#RRGGBB". Immutable once loaded.
    /// </summary>
    public sealed class RosterKit
    {
        /// <summary>Slots a club may define, in the order menus show them.</summary>
        public static readonly string[] Slots = { "home", "away", "third", "gk" };
        /// <summary>Pattern tokens (lower case; JerseyPattern names in title case).</summary>
        public static readonly string[] Patterns = { "solid", "stripes", "hoops", "halves", "sash", "pinstripes", "gradient", "chevron" };

        public string Slot { get; }
        public string Primary { get; }
        public string Secondary { get; }
        public string Tertiary { get; }
        public string Pattern { get; }
        public string ShortsColour { get; }
        public string SocksColour { get; }
        public string NumberColour { get; }
        public string NumberOutline { get; }

        public RosterKit(string slot, string primary, string secondary, string tertiary, string pattern,
            string shortsColour, string socksColour, string numberColour, string numberOutline)
        {
            Slot = slot;
            Primary = primary;
            Secondary = secondary;
            Tertiary = tertiary;
            Pattern = string.IsNullOrEmpty(pattern) ? "solid" : pattern;
            ShortsColour = shortsColour;
            SocksColour = socksColour;
            NumberColour = numberColour;
            NumberOutline = numberOutline;
        }

        /// <summary>"stripes" -> "Stripes", the JerseyPattern / KitDefinitionData.Pattern spelling.</summary>
        public string PatternName => Pattern.Length == 0 ? "Solid" : char.ToUpperInvariant(Pattern[0]) + Pattern.Substring(1);

        /// <summary>Plain kit made from a club's legacy colours block, for files without "kits".</summary>
        public static RosterKit FromColours(string slot, string shirt, string trim)
        {
            string number = IsDark(shirt) ? "#FFFFFF" : "#111111";
            return new RosterKit(slot, shirt, trim, trim, "solid", trim, shirt, number, trim);
        }

        /// <summary>True when a "#RRGGBB" colour is dark enough to need light print (Rec. 601 luma below 0.5).</summary>
        public static bool IsDark(string hex)
        {
            if (!RosterSchema.IsHexColour(hex)) return false;
            int r = System.Convert.ToInt32(hex.Substring(1, 2), 16);
            int g = System.Convert.ToInt32(hex.Substring(3, 2), 16);
            int b = System.Convert.ToInt32(hex.Substring(5, 2), 16);
            return (299 * r + 587 * g + 114 * b) < 127500;
        }
    }
}
