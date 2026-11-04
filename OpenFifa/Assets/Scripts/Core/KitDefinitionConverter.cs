using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Converts string-typed <see cref="KitDefinitionData"/> fields into typed values
    /// and into the existing <see cref="JerseyDesign"/> model. Pure C#.
    /// </summary>
    public static class KitDefinitionConverter
    {
        /// <summary>Parses an enum by name (case-insensitive). Numeric strings are rejected.</summary>
        public static bool TryParseEnum<T>(string value, out T result) where T : struct
        {
            result = default(T);
            if (string.IsNullOrWhiteSpace(value)) return false;

            var trimmed = value.Trim();
            if (char.IsDigit(trimmed[0]) || trimmed[0] == '-') return false;

            return Enum.TryParse(trimmed, true, out result) && Enum.IsDefined(typeof(T), result);
        }

        /// <summary>Kit variant, defaulting to Home when unparseable.</summary>
        public static KitVariant GetVariant(KitDefinitionData kit)
        {
            KitVariant v;
            return kit != null && TryParseEnum(kit.Variant, out v) ? v : KitVariant.Home;
        }

        /// <summary>Pattern, defaulting to Solid.</summary>
        public static JerseyPattern GetPattern(KitDefinitionData kit)
        {
            JerseyPattern p;
            return kit != null && TryParseEnum(kit.Pattern, out p) ? p : JerseyPattern.Solid;
        }

        /// <summary>Collar style, defaulting to Round.</summary>
        public static CollarStyle GetCollar(KitDefinitionData kit)
        {
            CollarStyle c;
            return kit != null && TryParseEnum(kit.Collar, out c) ? c : CollarStyle.Round;
        }

        /// <summary>Font style, defaulting to Block.</summary>
        public static KitFontStyle GetFontStyle(KitDefinitionData kit)
        {
            KitFontStyle f;
            return kit != null && TryParseEnum(kit.FontStyle, out f) ? f : KitFontStyle.Block;
        }

        /// <summary>Name layout, defaulting to Straight.</summary>
        public static KitNameLayout GetNameLayout(KitDefinitionData kit)
        {
            KitNameLayout n;
            return kit != null && TryParseEnum(kit.NameLayout, out n) ? n : KitNameLayout.Straight;
        }

        /// <summary>
        /// Builds a <see cref="JerseyDesign"/> (colours, pattern, collar) from kit data
        /// so the existing clash logic (<see cref="KitVariantManager"/>, <see cref="JerseyComparer"/>) applies.
        /// </summary>
        public static JerseyDesign ToJerseyDesign(KitDefinitionData kit)
        {
            if (kit == null) throw new ArgumentNullException(nameof(kit));

            return new JerseyDesign
            {
                PrimaryColor = KitColorUtility.ParseOrDefault(kit.PrimaryColor, SimpleColor.White),
                SecondaryColor = KitColorUtility.ParseOrDefault(kit.SecondaryColor, SimpleColor.Black),
                TertiaryColor = KitColorUtility.ParseOrDefault(kit.TertiaryColor, SimpleColor.White),
                Pattern = GetPattern(kit),
                PatternScale = kit.PatternScale > 0f ? kit.PatternScale : 1f,
                Collar = GetCollar(kit),
                Sleeves = SleeveStyle.Short
            };
        }
    }
}
