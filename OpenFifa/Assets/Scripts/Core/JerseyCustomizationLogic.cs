using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenFifa.Core
{
    /// <summary>
    /// Assembles a complete FullJerseyConfig from individual configuration components.
    /// Pure builder pattern — no engine dependencies.
    /// </summary>
    public class JerseyBuilder
    {
        private JerseyDesign _design;
        private PlayerNameConfig _nameConfig;
        private PlayerNumberConfig _numberConfig;
        private CrestConfig _crest;
        private readonly List<SponsorConfig> _sponsors = new List<SponsorConfig>();

        public JerseyBuilder()
        {
            _design = new JerseyDesign();
            _nameConfig = new PlayerNameConfig();
            _numberConfig = new PlayerNumberConfig();
            _crest = new CrestConfig();
        }

        /// <summary>
        /// Sets the jersey design (colors, pattern, collar, sleeves).
        /// </summary>
        public JerseyBuilder WithDesign(JerseyDesign design)
        {
            _design = design ?? throw new ArgumentNullException(nameof(design));
            return this;
        }

        /// <summary>
        /// Sets the player name configuration.
        /// </summary>
        public JerseyBuilder WithName(string name)
        {
            _nameConfig = new PlayerNameConfig(name);
            return this;
        }

        /// <summary>
        /// Sets the player name configuration with full control.
        /// </summary>
        public JerseyBuilder WithNameConfig(PlayerNameConfig nameConfig)
        {
            _nameConfig = nameConfig ?? throw new ArgumentNullException(nameof(nameConfig));
            return this;
        }

        /// <summary>
        /// Sets the player number.
        /// </summary>
        public JerseyBuilder WithNumber(int number)
        {
            _numberConfig = new PlayerNumberConfig(number);
            return this;
        }

        /// <summary>
        /// Sets the player number configuration with full control.
        /// </summary>
        public JerseyBuilder WithNumberConfig(PlayerNumberConfig numberConfig)
        {
            _numberConfig = numberConfig ?? throw new ArgumentNullException(nameof(numberConfig));
            return this;
        }

        /// <summary>
        /// Sets the team crest configuration.
        /// </summary>
        public JerseyBuilder WithCrest(CrestConfig crest)
        {
            _crest = crest ?? throw new ArgumentNullException(nameof(crest));
            return this;
        }

        /// <summary>
        /// Sets the crest by team name with default position.
        /// </summary>
        public JerseyBuilder WithCrest(string teamName)
        {
            _crest = new CrestConfig(teamName);
            return this;
        }

        /// <summary>
        /// Adds a sponsor configuration.
        /// </summary>
        public JerseyBuilder AddSponsor(SponsorConfig sponsor)
        {
            if (sponsor == null) throw new ArgumentNullException(nameof(sponsor));
            _sponsors.Add(sponsor);
            return this;
        }

        /// <summary>
        /// Builds and returns the fully assembled FullJerseyConfig.
        /// Does NOT validate — call JerseyValidation.IsDesignComplete to check.
        /// </summary>
        public FullJerseyConfig Build()
        {
            return new FullJerseyConfig
            {
                Design = _design,
                NameConfig = _nameConfig,
                NumberConfig = _numberConfig,
                Crest = _crest,
                Sponsors = new List<SponsorConfig>(_sponsors)
            };
        }

        /// <summary>
        /// Builds and validates the jersey config. Returns null if validation fails.
        /// </summary>
        public FullJerseyConfig BuildValidated()
        {
            var config = Build();
            return JerseyValidation.IsDesignComplete(config) ? config : null;
        }
    }

    /// <summary>
    /// Manages home/away/third kit variant selection for a team.
    /// Each variant is a separate JerseyDesign.
    /// </summary>
    public class KitVariantManager
    {
        private readonly Dictionary<KitVariant, JerseyDesign> _kits = new Dictionary<KitVariant, JerseyDesign>();

        /// <summary>
        /// Registers a kit design for the given variant.
        /// </summary>
        public void SetKit(KitVariant variant, JerseyDesign design)
        {
            if (design == null) throw new ArgumentNullException(nameof(design));
            _kits[variant] = design;
        }

        /// <summary>
        /// Returns the design for the given variant, or null if not registered.
        /// </summary>
        public JerseyDesign GetKit(KitVariant variant)
        {
            JerseyDesign design;
            return _kits.TryGetValue(variant, out design) ? design : null;
        }

        /// <summary>
        /// Returns true if the given variant has been registered.
        /// </summary>
        public bool HasKit(KitVariant variant)
        {
            return _kits.ContainsKey(variant);
        }

        /// <summary>
        /// Returns the number of registered kit variants.
        /// </summary>
        public int KitCount => _kits.Count;

        /// <summary>
        /// Selects the best kit variant to avoid clashing with an opponent's kit.
        /// Tries Home first, then Away, then Third.
        /// If all clash, falls back to Home.
        /// </summary>
        /// <param name="opponentPrimary">The opponent's primary jersey color.</param>
        /// <param name="clashThreshold">Color distance threshold below which a clash is detected.</param>
        /// <returns>The selected variant.</returns>
        public KitVariant SelectNonClashingVariant(SimpleColor opponentPrimary, float clashThreshold = 0.3f)
        {
            var priority = new[] { KitVariant.Home, KitVariant.Away, KitVariant.Third };

            foreach (var variant in priority)
            {
                JerseyDesign design;
                if (_kits.TryGetValue(variant, out design))
                {
                    float distance = JerseyComparer.ColorDistance(design.PrimaryColor, opponentPrimary);
                    if (distance > clashThreshold)
                        return variant;
                }
            }

            // Fallback to home if all clash
            return KitVariant.Home;
        }
    }

    /// <summary>
    /// Automatically assigns unique squad numbers to a starting lineup.
    /// Ensures no duplicates and all numbers are in the valid 1-99 range.
    /// </summary>
    public static class SquadNumberAssigner
    {
        /// <summary>
        /// Auto-assigns numbers 1 through playerCount to a starting lineup.
        /// Returns an array of assigned numbers indexed by player position (0-based).
        /// </summary>
        /// <param name="playerCount">Number of players in the lineup (typically 5 for 5v5).</param>
        /// <returns>Array of unique squad numbers [1..playerCount].</returns>
        public static int[] AutoAssign(int playerCount)
        {
            if (playerCount <= 0) return Array.Empty<int>();
            if (playerCount > PlayerNumberConfig.MaxNumber)
                playerCount = PlayerNumberConfig.MaxNumber;

            var numbers = new int[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                numbers[i] = i + 1;
            }
            return numbers;
        }

        /// <summary>
        /// Assigns numbers from a preferred list, filling gaps with auto-assigned numbers.
        /// If a preferred number is invalid or already taken, auto-assigns instead.
        /// </summary>
        /// <param name="playerCount">Number of players to assign.</param>
        /// <param name="preferred">Preferred numbers per position (may contain 0 for auto-assign, or duplicates).</param>
        /// <returns>Array of unique squad numbers.</returns>
        public static int[] AssignWithPreferences(int playerCount, int[] preferred)
        {
            if (playerCount <= 0) return Array.Empty<int>();

            var result = new int[playerCount];
            var used = new HashSet<int>();

            // First pass: assign valid preferred numbers
            for (int i = 0; i < playerCount; i++)
            {
                if (preferred != null && i < preferred.Length)
                {
                    int pref = preferred[i];
                    if (JerseyValidation.IsNumberValid(pref) && !used.Contains(pref))
                    {
                        result[i] = pref;
                        used.Add(pref);
                        continue;
                    }
                }
                result[i] = 0; // mark for auto-assign
            }

            // Second pass: fill in zeros with next available numbers
            int nextNumber = 1;
            for (int i = 0; i < playerCount; i++)
            {
                if (result[i] == 0)
                {
                    while (used.Contains(nextNumber))
                        nextNumber++;
                    result[i] = nextNumber;
                    used.Add(nextNumber);
                    nextNumber++;
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if an array of squad numbers contains any duplicates.
        /// </summary>
        public static bool HasDuplicates(int[] numbers)
        {
            if (numbers == null || numbers.Length == 0) return false;

            var seen = new HashSet<int>();
            foreach (int n in numbers)
            {
                if (!seen.Add(n))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Validates that all numbers in the array are within the valid range and unique.
        /// </summary>
        public static bool ValidateSquad(int[] numbers)
        {
            if (numbers == null || numbers.Length == 0) return false;

            var seen = new HashSet<int>();
            foreach (int n in numbers)
            {
                if (!JerseyValidation.IsNumberValid(n)) return false;
                if (!seen.Add(n)) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Formats player names for display on jerseys.
    /// Handles capitalization, special characters, truncation, and spacing.
    /// </summary>
    public static class NameFormatter
    {
        /// <summary>
        /// Allowed special characters in player names (hyphen, apostrophe, space, period).
        /// </summary>
        private static readonly char[] AllowedSpecialChars = { '-', '\'', ' ', '.' };

        /// <summary>
        /// Formats a player name for jersey display: uppercase, trimmed, truncated to max length.
        /// </summary>
        public static string FormatForJersey(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            var formatted = name.Trim().ToUpperInvariant();

            if (formatted.Length > PlayerNameConfig.MaxCharacters)
                formatted = formatted.Substring(0, PlayerNameConfig.MaxCharacters);

            return formatted;
        }

        /// <summary>
        /// Sanitizes a name by removing characters that are not letters, digits, or allowed special chars.
        /// </summary>
        public static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || Array.IndexOf(AllowedSpecialChars, c) >= 0)
                    sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Formats and sanitizes a name for jersey display in one step.
        /// </summary>
        public static string FormatAndSanitize(string name)
        {
            return FormatForJersey(Sanitize(name));
        }

        /// <summary>
        /// Truncates a name to fit within the max character limit.
        /// If truncated, does not add ellipsis (jersey convention).
        /// </summary>
        public static string Truncate(string name, int maxLength)
        {
            if (string.IsNullOrEmpty(name)) return "";
            if (maxLength <= 0) return "";
            if (name.Length <= maxLength) return name;
            return name.Substring(0, maxLength);
        }

        /// <summary>
        /// Checks if a name contains only allowed characters (letters, digits, and allowed specials).
        /// </summary>
        public static bool ContainsOnlyAllowedChars(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;

            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && Array.IndexOf(AllowedSpecialChars, c) < 0)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Compares two team kits for color clash detection.
    /// Uses Euclidean distance in RGB space to determine similarity.
    /// </summary>
    public static class JerseyComparer
    {
        /// <summary>
        /// Default color distance threshold below which kits are considered clashing.
        /// Value in [0, sqrt(3)] range. 0 = identical, sqrt(3) ~= 1.732 = maximum distance.
        /// </summary>
        public static readonly float DefaultClashThreshold = 0.3f;

        /// <summary>
        /// Computes the Euclidean distance between two colors in RGB space.
        /// Returns a value in [0, sqrt(3)] where 0 = identical.
        /// </summary>
        public static float ColorDistance(SimpleColor a, SimpleColor b)
        {
            float dr = a.R - b.R;
            float dg = a.G - b.G;
            float db = a.B - b.B;
            return (float)Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        /// <summary>
        /// Returns true if the primary colors of two jersey designs are too similar
        /// (distance below the threshold), indicating a kit clash.
        /// </summary>
        public static bool IsClashing(JerseyDesign kitA, JerseyDesign kitB, float threshold)
        {
            if (kitA == null || kitB == null) return false;
            return ColorDistance(kitA.PrimaryColor, kitB.PrimaryColor) < threshold;
        }

        /// <summary>
        /// Returns true if the primary colors clash using the default threshold.
        /// </summary>
        public static bool IsClashing(JerseyDesign kitA, JerseyDesign kitB)
        {
            return IsClashing(kitA, kitB, DefaultClashThreshold);
        }

        /// <summary>
        /// Compares two SimpleColors and returns true if they are perceptually similar
        /// (distance below threshold).
        /// </summary>
        public static bool AreColorsSimilar(SimpleColor a, SimpleColor b, float threshold)
        {
            return ColorDistance(a, b) < threshold;
        }

        /// <summary>
        /// Returns the brightness (luminance approximation) of a color.
        /// Uses the perceptual luminance formula: 0.299R + 0.587G + 0.114B.
        /// </summary>
        public static float GetLuminance(SimpleColor color)
        {
            return 0.299f * color.R + 0.587f * color.G + 0.114f * color.B;
        }

        /// <summary>
        /// Suggests the best text color (black or white) for readability on a given background.
        /// Returns white for dark backgrounds, black for light backgrounds.
        /// </summary>
        public static SimpleColor SuggestTextColor(SimpleColor background)
        {
            float luminance = GetLuminance(background);
            return luminance > 0.5f ? SimpleColor.Black : SimpleColor.White;
        }
    }
}
