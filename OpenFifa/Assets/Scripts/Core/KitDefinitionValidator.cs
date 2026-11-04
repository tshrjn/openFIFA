using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Validates kit data loaded from JSON or ScriptableObjects. Pure C#.
    /// Returns human-readable errors so bad data (e.g. a typo in a licensed kit pack)
    /// is reported instead of silently rendering wrong.
    /// </summary>
    public static class KitDefinitionValidator
    {
        /// <summary>
        /// Minimum RGB distance between number colour and shirt primary colour.
        /// Below this, numbers are hard to read on broadcast.
        /// </summary>
        public const float MinNumberContrast = 0.25f;

        /// <summary>Largest allowed outline width (glyph stroke units).</summary>
        public const float MaxOutlineWidth = 0.6f;

        /// <summary>Returns all validation errors for a kit (empty list = valid).</summary>
        public static List<string> Validate(KitDefinitionData kit)
        {
            var errors = new List<string>();
            if (kit == null)
            {
                errors.Add("Kit is null.");
                return errors;
            }

            string label = string.IsNullOrEmpty(kit.Id) ? "<no id>" : kit.Id;

            if (string.IsNullOrWhiteSpace(kit.Id)) errors.Add("Kit has no Id.");
            if (string.IsNullOrWhiteSpace(kit.TeamId)) errors.Add($"Kit '{label}' has no TeamId.");

            KitVariant variant;
            if (!KitDefinitionConverter.TryParseEnum(kit.Variant, out variant))
                errors.Add($"Kit '{label}': unknown Variant '{kit.Variant}' (expected Home, Away, Third or Goalkeeper).");

            CheckColor(errors, label, "PrimaryColor", kit.PrimaryColor);
            CheckColor(errors, label, "SecondaryColor", kit.SecondaryColor);
            CheckColor(errors, label, "TertiaryColor", kit.TertiaryColor);
            CheckColor(errors, label, "ShortsColor", kit.ShortsColor);
            CheckColor(errors, label, "SocksColor", kit.SocksColor);
            CheckColor(errors, label, "NumberColor", kit.NumberColor);
            if (kit.NumberOutlineWidth > 0f)
                CheckColor(errors, label, "NumberOutlineColor", kit.NumberOutlineColor);

            JerseyPattern pattern;
            if (!KitDefinitionConverter.TryParseEnum(kit.Pattern, out pattern))
                errors.Add($"Kit '{label}': unknown Pattern '{kit.Pattern}'.");

            if (!JerseyValidation.IsPatternScaleValid(kit.PatternScale))
                errors.Add($"Kit '{label}': PatternScale {kit.PatternScale} outside 0.1-10.");

            CollarStyle collar;
            if (!KitDefinitionConverter.TryParseEnum(kit.Collar, out collar))
                errors.Add($"Kit '{label}': unknown Collar '{kit.Collar}'.");

            KitFontStyle font;
            if (!KitDefinitionConverter.TryParseEnum(kit.FontStyle, out font))
                errors.Add($"Kit '{label}': unknown FontStyle '{kit.FontStyle}'.");

            KitNameLayout layout;
            if (!KitDefinitionConverter.TryParseEnum(kit.NameLayout, out layout))
                errors.Add($"Kit '{label}': unknown NameLayout '{kit.NameLayout}'.");

            if (kit.NumberOutlineWidth < 0f || kit.NumberOutlineWidth > MaxOutlineWidth)
                errors.Add($"Kit '{label}': NumberOutlineWidth {kit.NumberOutlineWidth} outside 0-{MaxOutlineWidth}.");

            if (kit.FabricSheen < 0f || kit.FabricSheen > 1f)
                errors.Add($"Kit '{label}': FabricSheen {kit.FabricSheen} outside 0-1.");

            SimpleColor primary, number;
            if (KitColorUtility.TryParseHex(kit.PrimaryColor, out primary)
                && KitColorUtility.TryParseHex(kit.NumberColor, out number)
                && !HasReadableNumber(kit, primary, number))
            {
                errors.Add($"Kit '{label}': NumberColor too close to PrimaryColor and no contrasting outline; numbers would be illegible.");
            }

            if (kit.Licensed != null)
            {
                bool needsAlbedo = !string.IsNullOrEmpty(kit.Licensed.NormalPath)
                    || !string.IsNullOrEmpty(kit.Licensed.MaskPath);
                if (needsAlbedo && !kit.Licensed.HasAlbedo)
                    errors.Add($"Kit '{label}': licensed Normal/Mask textures require an AlbedoPath.");
            }

            return errors;
        }

        /// <summary>True when the kit has no validation errors.</summary>
        public static bool IsValid(KitDefinitionData kit)
        {
            return Validate(kit).Count == 0;
        }

        private static bool HasReadableNumber(KitDefinitionData kit, SimpleColor primary, SimpleColor number)
        {
            if (JerseyComparer.ColorDistance(primary, number) >= MinNumberContrast) return true;

            // An outline that contrasts with the shirt keeps the number readable.
            SimpleColor outline;
            return kit.NumberOutlineWidth > 0f
                && KitColorUtility.TryParseHex(kit.NumberOutlineColor, out outline)
                && JerseyComparer.ColorDistance(primary, outline) >= MinNumberContrast;
        }

        private static void CheckColor(List<string> errors, string label, string field, string value)
        {
            SimpleColor c;
            if (!KitColorUtility.TryParseHex(value, out c))
                errors.Add($"Kit '{label}': {field} '{value}' is not a hex colour.");
        }
    }
}
