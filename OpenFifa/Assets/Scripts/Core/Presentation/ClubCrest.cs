using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Procedural club crest for the scorebug (original, generated from the roster club only): a shield in the primary
    /// colour, a secondary-colour pattern picked from the club's short name, and the club's initials on top.
    /// Coordinates are u (0 left .. 1 right), v (0 bottom .. 1 top).
    /// </summary>
    public static class ClubCrest
    {
        public enum Pattern { Plain = 0, Stripe = 1, Sash = 2, Band = 3, Chevron = 4 }

        public const int PatternCount = 5;

        /// <summary>Half width of the shield body and where its sides start curving to the point.</summary>
        public const float HalfWidth = 0.44f, ShoulderV = 0.48f, TopV = 0.95f;

        /// <summary>Up to two initials: first letters of the first two words that are not "FC"/"AFC"/"CF"; one word gives its first two letters.</summary>
        public static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var words = name.Split(new[] { ' ', '-', '.', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var picked = new char[2];
            int n = 0;
            foreach (var w in words)
            {
                string u = w.ToUpperInvariant();
                if (u == "FC" || u == "AFC" || u == "CF" || u == "SC") continue;
                char c = FirstLetter(w);
                if (c == '\0') continue;
                picked[n++] = c;
                if (n == 2) break;
            }
            if (n == 0) return "?";
            if (n == 1)
            {
                foreach (var w in words)
                {
                    string u = w.ToUpperInvariant();
                    if (u == "FC" || u == "AFC" || u == "CF" || u == "SC") continue;
                    int seen = 0;
                    foreach (char ch in w)
                    {
                        if (!char.IsLetterOrDigit(ch)) continue;
                        if (++seen == 2) return new string(new[] { picked[0], char.ToUpperInvariant(ch) });
                    }
                    break;
                }
                return new string(picked, 0, 1);
            }
            return new string(picked, 0, 2);
        }

        private static char FirstLetter(string w)
        {
            foreach (char ch in w)
                if (char.IsLetterOrDigit(ch)) return char.ToUpperInvariant(ch);
            return '\0';
        }

        /// <summary>Pattern for a club, stable for its short name (FNV-1a hash).</summary>
        public static Pattern PatternFor(string shortName)
        {
            uint h = 2166136261u;
            foreach (char c in shortName ?? string.Empty) { h ^= c; h *= 16777619u; }
            return (Pattern)(h % PatternCount);
        }

        /// <summary>Half width of the shield at height v (0 outside the shield's vertical range).</summary>
        public static float ShieldHalfWidth(float v)
        {
            if (v < 0f || v > TopV) return 0f;
            if (v >= ShoulderV) return HalfWidth;
            float t = (ShoulderV - v) / ShoulderV; // 0 at the shoulder .. 1 at the point
            return HalfWidth * (float)Math.Sqrt(Math.Max(0.0, 1.0 - t * t));
        }

        public static bool InShield(float u, float v) => v >= 0f && v <= TopV && Math.Abs(u - 0.5f) <= ShieldHalfWidth(v);

        /// <summary>
        /// Inside the shield, within <paramref name="border"/> (uv units) of its edge: the outline ring. Measured
        /// horizontally and against the top edge, which is close enough for a 64 px icon.
        /// </summary>
        public static bool OnBorder(float u, float v, float border)
        {
            if (!InShield(u, v)) return false;
            if (v > TopV - border) return true;
            return !InShield(u - border, v) || !InShield(u + border, v) || !InShield(u, v - border);
        }

        /// <summary>True where the secondary colour shows for a pattern (inside the shield).</summary>
        public static bool Secondary(Pattern p, float u, float v)
        {
            switch (p)
            {
                case Pattern.Stripe: return Math.Abs(u - 0.5f) < 0.11f;
                case Pattern.Sash: return Math.Abs((u - 0.5f) + (v - 0.5f)) < 0.12f;
                case Pattern.Band: return v > 0.6f && v < 0.78f;
                case Pattern.Chevron: { float y = 0.62f - Math.Abs(u - 0.5f) * 0.7f; return v > y - 0.09f && v < y + 0.09f; }
                default: return false;
            }
        }
    }

    /// <summary>Shirt numbers per sim player id for the HUD, from the two team sheets (starters are in sim slot order).</summary>
    public static class MatchSheetNumbers
    {
        public static int[] Build(RosterTeamSheet home, RosterTeamSheet away)
        {
            var numbers = new int[MatchSimState.PlayerCount];
            for (int i = 0; i < MatchSetup.PlayersPerTeam; i++)
            {
                numbers[i] = Number(home, i);
                numbers[i + MatchSetup.PlayersPerTeam] = Number(away, i);
            }
            return numbers;
        }

        /// <summary>Default numbering without rosters: 1-11 per team.</summary>
        public static int[] Default()
        {
            var numbers = new int[MatchSimState.PlayerCount];
            for (int i = 0; i < numbers.Length; i++) numbers[i] = i % MatchSetup.PlayersPerTeam + 1;
            return numbers;
        }

        private static int Number(RosterTeamSheet sheet, int slot)
        {
            var p = sheet != null && sheet.Starters != null && slot < sheet.Starters.Length ? sheet.Starters[slot] : null;
            return p != null && p.ShirtNumber > 0 ? p.ShirtNumber : slot + 1;
        }
    }
}
