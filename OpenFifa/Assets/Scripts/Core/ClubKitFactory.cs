using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Builds a full set of original kits (Home, Away, Third, Goalkeeper) for a roster club from its three
    /// colours, for clubs that have no authored kits in the kit library. Design choices (pattern, collar, shorts,
    /// keeper colour, name layout) are picked deterministically from the club id, so a club always wears the
    /// same kits. Authored or licensed kits with the same team id take priority (see MatchKitBinder).
    /// Pure C#.
    /// </summary>
    public static class ClubKitFactory
    {
        /// <summary>Goalkeeper shirt colours, chosen to stand apart from typical outfield kits.</summary>
        public static readonly string[] KeeperPalette =
        {
            "#2BB673", // green
            "#F2A900", // amber
            "#E6007E", // magenta
            "#00A6D6", // cyan
            "#1B1B1F", // black
            "#B8E600", // lime
            "#7E57C2", // purple
            "#FF6F3C", // orange
        };

        private static readonly JerseyPattern[] HomePatterns =
        {
            JerseyPattern.Solid, JerseyPattern.Stripes, JerseyPattern.Solid, JerseyPattern.Hoops,
            JerseyPattern.Pinstripes, JerseyPattern.Solid, JerseyPattern.Sash, JerseyPattern.HalfAndHalf,
            JerseyPattern.Stripes, JerseyPattern.Solid,
        };

        private static readonly JerseyPattern[] AwayPatterns =
        {
            JerseyPattern.Solid, JerseyPattern.Sash, JerseyPattern.Chevron, JerseyPattern.Solid,
            JerseyPattern.Hoops, JerseyPattern.Solid,
        };

        private static readonly JerseyPattern[] ThirdPatterns =
        {
            JerseyPattern.Pinstripes, JerseyPattern.Solid, JerseyPattern.Gradient, JerseyPattern.Solid,
        };

        private static readonly CollarStyle[] Collars = { CollarStyle.Round, CollarStyle.VNeck, CollarStyle.Polo, CollarStyle.Round };

        /// <summary>Stable 32-bit FNV-1a hash (string.GetHashCode is randomised per process).</summary>
        public static uint StableHash(string text)
        {
            uint h = 2166136261u;
            if (text == null) return h;
            foreach (char c in text)
            {
                h ^= c;
                h *= 16777619u;
            }
            return h;
        }

        /// <summary>
        /// Library data (one team, four kits) for a club. Kit ids are "&lt;clubId&gt;-home" etc.
        /// </summary>
        public static KitLibraryData Build(string clubId, string clubName, string shortName,
            string primaryHex, string secondaryHex, string awayHex)
        {
            if (string.IsNullOrEmpty(clubId)) throw new ArgumentException("Club id required.", nameof(clubId));

            // Club colours are toned down a little (fabric under stadium light rarely reads fully saturated).
            var primary = Soften(KitColorUtility.ParseOrDefault(primaryHex, new SimpleColor(0.75f, 0.1f, 0.15f)));
            var secondary = Soften(KitColorUtility.ParseOrDefault(secondaryHex, SimpleColor.White));
            var away = Soften(KitColorUtility.ParseOrDefault(awayHex, SimpleColor.White));
            uint h = StableHash(clubId);

            var data = new KitLibraryData
            {
                Teams = new List<KitTeamData>
                {
                    new KitTeamData
                    {
                        Id = clubId,
                        Name = clubName ?? clubId,
                        Initials = Initials(shortName, clubName),
                    },
                },
                Kits = new List<KitDefinitionData>(),
            };

            // Home: club colours. Shorts in the secondary colour for about half the clubs, else the primary.
            var home = NewKit(clubId, KitVariant.Home, primary, secondary, secondary, h);
            home.Pattern = HomePatterns[h % (uint)HomePatterns.Length].ToString();
            bool contrastShorts = ((h >> 4) & 1u) == 0u;
            home.ShortsColor = KitColorUtility.ToHex(contrastShorts ? secondary : primary);
            home.SocksColor = KitColorUtility.ToHex(((h >> 5) & 1u) == 0u ? primary : secondary);
            data.Kits.Add(home);

            // Away: the club's clash colour with the home colour as trim.
            var awayKit = NewKit(clubId, KitVariant.Away, away, primary, primary, h >> 8);
            awayKit.Pattern = AwayPatterns[(h >> 8) % (uint)AwayPatterns.Length].ToString();
            awayKit.ShortsColor = KitColorUtility.ToHex(((h >> 12) & 1u) == 0u ? away : primary);
            awayKit.SocksColor = KitColorUtility.ToHex(away);
            data.Kits.Add(awayKit);

            // Third: a neutral (charcoal or off-white, whichever contrasts with home) with club trim.
            bool lightHome = JerseyComparer.GetLuminance(primary) > 0.5f;
            var neutral = lightHome ? new SimpleColor(0.086f, 0.094f, 0.114f) : new SimpleColor(0.93f, 0.92f, 0.89f);
            var third = NewKit(clubId, KitVariant.Third, neutral, primary, primary, h >> 16);
            third.Pattern = ThirdPatterns[(h >> 16) % (uint)ThirdPatterns.Length].ToString();
            third.ShortsColor = KitColorUtility.ToHex(neutral);
            third.SocksColor = KitColorUtility.ToHex(neutral);
            data.Kits.Add(third);

            data.Kits.Add(BuildKeeperKit(clubId, KeeperPalette[(h >> 20) % (uint)KeeperPalette.Length], h >> 20));
            return data;
        }

        /// <summary>Builds library data for a roster club.</summary>
        public static KitLibraryData Build(RosterClub club)
        {
            if (club == null) throw new ArgumentNullException(nameof(club));
            return Build(club.Id, club.Name, club.ShortName, club.PrimaryColour, club.SecondaryColour, club.AwayColour);
        }

        /// <summary>Maximum HSV saturation of generated kit colours.</summary>
        public const float MaxSaturation = 0.72f;

        private static readonly JerseyPattern[] KeeperPatterns =
        {
            JerseyPattern.Gradient, JerseyPattern.Chevron, JerseyPattern.Pinstripes, JerseyPattern.Hoops, JerseyPattern.Sash,
        };

        /// <summary>Scales saturation by 0.85 and caps it at <see cref="MaxSaturation"/>, keeping hue and value.</summary>
        public static SimpleColor Soften(SimpleColor c)
        {
            float max = Math.Max(c.R, Math.Max(c.G, c.B));
            float min = Math.Min(c.R, Math.Min(c.G, c.B));
            if (max <= 1e-5f) return c;
            float sat = (max - min) / max;
            float target = Math.Min(sat * 0.85f, MaxSaturation);
            if (sat <= 1e-5f || target >= sat) return c;
            float k = target / sat;
            // Pull every channel towards the max (value) by the same factor: hue kept, saturation scaled.
            return new SimpleColor(max - (max - c.R) * k, max - (max - c.G) * k, max - (max - c.B) * k, c.A);
        }

        /// <summary>A goalkeeper kit in one base colour: long sleeves, gloves, a patterned darker trim, matching shorts and socks.</summary>
        public static KitDefinitionData BuildKeeperKit(string teamId, string baseHex, uint seed)
        {
            var baseColor = Soften(KitColorUtility.ParseOrDefault(baseHex, new SimpleColor(0.17f, 0.71f, 0.45f)));
            bool dark = JerseyComparer.GetLuminance(baseColor) < 0.25f;
            var trim = dark ? new SimpleColor(0.75f, 0.75f, 0.78f) : Scale(baseColor, 0.45f);
            var kit = NewKit(teamId, KitVariant.Goalkeeper, baseColor, trim, trim, seed);
            kit.Id = teamId + "-goalkeeper";
            kit.Pattern = KeeperPatterns[seed % (uint)KeeperPatterns.Length].ToString();
            kit.SecondaryColor = KitColorUtility.ToHex(dark ? new SimpleColor(0.32f, 0.33f, 0.36f) : Scale(baseColor, 0.72f));
            kit.PatternScale = 1f;
            kit.ShortsColor = KitColorUtility.ToHex(dark ? baseColor : Scale(baseColor, 0.6f));
            kit.SocksColor = KitColorUtility.ToHex(baseColor);
            kit.Sleeves = "Long";
            kit.GloveColor = dark ? "#E8E8E8" : "#F4F4F4";
            return kit;
        }

        /// <summary>Shallow copy (licensed texture paths shared).</summary>
        public static KitDefinitionData Clone(KitDefinitionData k)
        {
            if (k == null) return null;
            return new KitDefinitionData
            {
                Id = k.Id,
                TeamId = k.TeamId,
                Variant = k.Variant,
                PrimaryColor = k.PrimaryColor,
                SecondaryColor = k.SecondaryColor,
                TertiaryColor = k.TertiaryColor,
                ShortsColor = k.ShortsColor,
                SocksColor = k.SocksColor,
                Pattern = k.Pattern,
                PatternScale = k.PatternScale,
                Collar = k.Collar,
                FontStyle = k.FontStyle,
                NameLayout = k.NameLayout,
                NumberColor = k.NumberColor,
                NumberOutlineColor = k.NumberOutlineColor,
                NumberOutlineWidth = k.NumberOutlineWidth,
                ShowFrontNumber = k.ShowFrontNumber,
                FabricSheen = k.FabricSheen,
                Sleeves = k.Sleeves,
                GloveColor = k.GloveColor,
                Licensed = k.Licensed,
            };
        }

        private static KitDefinitionData NewKit(string clubId, KitVariant variant, SimpleColor shirt,
            SimpleColor second, SimpleColor trim, uint seed)
        {
            // Numbers: black or white for contrast with the shirt, outlined in the other one so they stay
            // readable over stripes and hoops.
            var number = JerseyComparer.SuggestTextColor(shirt);
            bool whiteNumber = JerseyComparer.GetLuminance(number) > 0.5f;
            var outline = whiteNumber ? new SimpleColor(0.05f, 0.05f, 0.07f) : SimpleColor.White;
            return new KitDefinitionData
            {
                Id = clubId + "-" + variant.ToString().ToLowerInvariant(),
                TeamId = clubId,
                Variant = variant.ToString(),
                PrimaryColor = KitColorUtility.ToHex(shirt),
                SecondaryColor = KitColorUtility.ToHex(second),
                TertiaryColor = KitColorUtility.ToHex(trim),
                ShortsColor = KitColorUtility.ToHex(shirt),
                SocksColor = KitColorUtility.ToHex(shirt),
                Pattern = JerseyPattern.Solid.ToString(),
                PatternScale = 1f,
                Collar = Collars[seed % (uint)Collars.Length].ToString(),
                FontStyle = KitFontStyle.Block.ToString(),
                NameLayout = ((seed >> 3) & 1u) == 0u ? KitNameLayout.Straight.ToString() : KitNameLayout.Arched.ToString(),
                NumberColor = KitColorUtility.ToHex(number),
                NumberOutlineColor = KitColorUtility.ToHex(outline),
                NumberOutlineWidth = 0.25f,
                ShowFrontNumber = true,
                FabricSheen = 0.5f,
            };
        }

        private static SimpleColor Scale(SimpleColor c, float s) => new SimpleColor(c.R * s, c.G * s, c.B * s, 1f);

        private static string Initials(string shortName, string name)
        {
            if (!string.IsNullOrWhiteSpace(shortName)) return shortName.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(name)) return "";
            var parts = name.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
            string result = "";
            foreach (var part in parts)
            {
                if (result.Length >= 3) break;
                result += char.ToUpperInvariant(part[0]);
            }
            return result;
        }
    }
}
