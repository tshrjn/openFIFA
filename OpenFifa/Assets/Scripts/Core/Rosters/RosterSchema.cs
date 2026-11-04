using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Constants and name tables for the OpenFifa roster JSON format. docs/ROSTER_FORMAT.md is the human spec;
    /// keep both in step and bump <see cref="CurrentVersion"/> (adding a migration) on breaking changes.
    /// </summary>
    public static class RosterSchema
    {
        /// <summary>The schema version this build writes and reads natively.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Format tag every roster file must carry in its "format" member.</summary>
        public const string FormatTag = "openfootball-roster";

        /// <summary>Format tag written by builds from before the OpenFootball rename; still accepted on load.</summary>
        public const string LegacyFormatTag = "openfifa-roster";

        public const int AttributeMin = 1;
        public const int AttributeMax = 20;
        public const int StarMin = 1;
        public const int StarMax = 5;
        public const int AgeMin = 15;
        public const int AgeMax = 45;
        public const int HeightMinCm = 150;
        public const int HeightMaxCm = 215;
        public const int ShirtMin = 1;
        public const int ShirtMax = 99;
        public const int MinPlayersPerClub = 11;
        public const int MaxPlayersPerClub = 40;
        public const int MaxPositionsPerPlayer = 4;
        public const int MaxClubs = 64;

        /// <summary>JSON keys, indexed by (int)<see cref="RosterAttribute"/>.</summary>
        public static readonly string[] AttributeKeys =
        {
            "acceleration", "topSpeed",
            "finishing", "shotPower", "longShots", "offTheBall", "volleys", "penalties",
            "shortPassing", "longPassing", "vision", "crossing", "curl", "setPieces",
            "dribbling", "firstTouch", "agility", "balance", "composure",
            "marking", "standingTackle", "slidingTackle", "interceptions", "heading",
            "stamina", "strength", "jumping", "aggression", "reactions",
            "gkDiving", "gkHandling", "gkDistribution", "gkReflexes", "gkPositioning"
        };

        public static int AttributeCount => AttributeKeys.Length;

        private static readonly AttributeGroup[] Groups =
        {
            AttributeGroup.Pace, AttributeGroup.Pace,
            AttributeGroup.Shooting, AttributeGroup.Shooting, AttributeGroup.Shooting,
            AttributeGroup.Shooting, AttributeGroup.Shooting, AttributeGroup.Shooting,
            AttributeGroup.Passing, AttributeGroup.Passing, AttributeGroup.Passing,
            AttributeGroup.Passing, AttributeGroup.Passing, AttributeGroup.Passing,
            AttributeGroup.Dribbling, AttributeGroup.Dribbling, AttributeGroup.Dribbling,
            AttributeGroup.Dribbling, AttributeGroup.Dribbling,
            AttributeGroup.Defending, AttributeGroup.Defending, AttributeGroup.Defending,
            AttributeGroup.Defending, AttributeGroup.Defending,
            AttributeGroup.Physical, AttributeGroup.Physical, AttributeGroup.Physical,
            AttributeGroup.Physical, AttributeGroup.Physical,
            AttributeGroup.Goalkeeping, AttributeGroup.Goalkeeping, AttributeGroup.Goalkeeping,
            AttributeGroup.Goalkeeping, AttributeGroup.Goalkeeping
        };

        private static readonly Dictionary<string, RosterAttribute> KeyLookup = BuildKeyLookup();

        private static Dictionary<string, RosterAttribute> BuildKeyLookup()
        {
            var d = new Dictionary<string, RosterAttribute>(StringComparer.Ordinal);
            for (int i = 0; i < AttributeKeys.Length; i++) d[AttributeKeys[i]] = (RosterAttribute)i;
            return d;
        }

        public static string KeyOf(RosterAttribute attribute) => AttributeKeys[(int)attribute];

        public static AttributeGroup GroupOf(RosterAttribute attribute) => Groups[(int)attribute];

        public static bool TryParseAttribute(string key, out RosterAttribute attribute) =>
            KeyLookup.TryGetValue(key ?? string.Empty, out attribute);

        public static bool TryParsePosition(string code, out RosterPosition position)
        {
            position = RosterPosition.GK;
            if (string.IsNullOrEmpty(code)) return false;
            // Enum.TryParse accepts numbers ("3"); reject those explicitly.
            if (char.IsDigit(code[0]) || code[0] == '-') return false;
            return Enum.TryParse(code, false, out position) && Enum.IsDefined(typeof(RosterPosition), position);
        }

        public static bool TryParseFoot(string value, out PreferredFoot foot)
        {
            foot = PreferredFoot.Right;
            if (value == "right") return true;
            if (value == "left") { foot = PreferredFoot.Left; return true; }
            return false;
        }

        /// <summary>True for "#RRGGBB" hex colours.</summary>
        public static bool IsHexColour(string value)
        {
            if (value == null || value.Length != 7 || value[0] != '#') return false;
            for (int i = 1; i < 7; i++)
            {
                char c = value[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }

        /// <summary>True for outfield shapes like "4-3-3" or "4-2-3-1" whose lines add up to 10.</summary>
        public static bool IsFormation(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var parts = value.Split('-');
            if (parts.Length < 3 || parts.Length > 5) return false;
            int sum = 0;
            foreach (var p in parts)
            {
                if (p.Length != 1 || p[0] < '1' || p[0] > '6') return false;
                sum += p[0] - '0';
            }
            return sum == 10;
        }

        /// <summary>
        /// Lower-case ASCII slug ("Dele Okonkwo-Hart" -> "dele_okonkwo_hart", "Ødegaard" -> "odegaard",
        /// "Kadıoğlu" -> "kadioglu"); used for commentary clip ids and file names. Accents are dropped (Unicode
        /// decomposition), a few letters without a decomposition are spelled out, anything else separates words.
        /// tools/commentary/build_tts_manifest.py implements the same rule.
        /// </summary>
        public static string Slug(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(text.Length);
            bool pendingSep = false;
            foreach (char raw in decomposed)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(raw) ==
                    System.Globalization.UnicodeCategory.NonSpacingMark)
                    continue;
                string mapped = Transliterate(raw);
                if (mapped == null)
                {
                    pendingSep = true;
                    continue;
                }
                if (pendingSep && sb.Length > 0) sb.Append('_');
                pendingSep = false;
                sb.Append(mapped);
            }
            return sb.ToString();
        }

        /// <summary>ASCII spelling of one character, or null for a separator.</summary>
        private static string Transliterate(char raw)
        {
            char c = raw >= 'A' && raw <= 'Z' ? (char)(raw + 32) : raw;
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) return c.ToString();
            switch (raw)
            {
                case 'ø': case 'Ø': return "o";
                case 'ı': return "i";
                case 'ł': case 'Ł': return "l";
                case 'đ': case 'Đ': return "d";
                case 'ß': return "ss";
                case 'æ': case 'Æ': return "ae";
                case 'œ': case 'Œ': return "oe";
                case 'þ': case 'Þ': return "th";
                case 'ð': case 'Ð': return "d";
                default: return null;
            }
        }
    }
}
