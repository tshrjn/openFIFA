using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Checks a parsed (and migrated) roster document against schema v1. Collects every problem rather than
    /// stopping at the first, so a user fixing a hand-edited file sees the full list. Errors are capped at
    /// <see cref="MaxIssues"/> to keep messages readable.
    /// </summary>
    public sealed class RosterValidator
    {
        public const int MaxIssues = 200;

        private static readonly HashSet<string> RootKeys = Set("format", "schemaVersion", "league", "provenance", "clubs");
        private static readonly HashSet<string> LeagueKeys = Set("id", "name", "shortName", "country", "season");
        private static readonly HashSet<string> ProvenanceKeys = Set("source", "fictional", "generator", "seed", "notes", "license");
        private static readonly HashSet<string> ClubKeys = Set("id", "name", "shortName", "colours", "stadium", "formation", "reputation", "players", "style");
        private static readonly HashSet<string> ColourKeys = Set("primary", "secondary", "away");
        private static readonly HashSet<string> StadiumKeys = Set("name", "capacity");
        private static readonly HashSet<string> PlayerKeys = Set("id", "firstName", "lastName", "knownAs", "shirtNumber",
            "nationality", "age", "heightCm", "positions", "preferredFoot", "weakFoot", "skillMoves", "attributes");

        private readonly List<RosterIssue> _issues = new List<RosterIssue>();
        private readonly HashSet<string> _clubIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _clubShortNames = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _playerIds = new HashSet<string>(StringComparer.Ordinal);

        private static HashSet<string> Set(params string[] keys) => new HashSet<string>(keys, StringComparer.Ordinal);

        /// <summary>Validates <paramref name="root"/> and returns all issues (errors and warnings).</summary>
        public static List<RosterIssue> Validate(JsonNode root)
        {
            var v = new RosterValidator();
            v.ValidateRoot(root);
            return v._issues;
        }

        private void Error(string path, string message, JsonNode at = null)
        {
            if (_issues.Count >= MaxIssues) return;
            _issues.Add(new RosterIssue(true, path, message, at?.Line ?? 0));
            if (_issues.Count == MaxIssues)
                _issues.Add(new RosterIssue(true, "", "too many problems; stopped after " + MaxIssues, 0));
        }

        private void Warn(string path, string message, JsonNode at = null)
        {
            if (_issues.Count >= MaxIssues) return;
            _issues.Add(new RosterIssue(false, path, message, at?.Line ?? 0));
        }

        private void WarnUnknownKeys(JsonNode obj, HashSet<string> known, string path)
        {
            foreach (var m in obj.Members)
                if (!known.Contains(m.Key))
                    Warn(Join(path, m.Key), "unknown field (ignored)", m.Value);
        }

        private static string Join(string path, string key) => path.Length == 0 ? key : path + "." + key;

        private static string Describe(JsonNode n)
        {
            if (n == null) return "missing";
            return n.ToString();
        }

        // ---- primitive checks ----

        private JsonNode RequireObject(JsonNode parent, string key, string path)
        {
            var n = parent.Get(key);
            if (n == null) { Error(Join(path, key), "required object is missing", parent); return null; }
            if (!n.IsObject) { Error(Join(path, key), "must be an object (got " + Describe(n) + ")", n); return null; }
            return n;
        }

        private string RequireString(JsonNode parent, string key, string path, int maxLength, bool allowEmpty = false)
        {
            var n = parent.Get(key);
            string p = Join(path, key);
            if (n == null) { Error(p, "required text is missing", parent); return null; }
            if (!n.IsString) { Error(p, "must be text in quotes (got " + Describe(n) + ")", n); return null; }
            string s = n.StringValue;
            if (!allowEmpty && s.Trim().Length == 0) { Error(p, "must not be empty", n); return null; }
            if (s.Length > maxLength) { Error(p, "must be at most " + maxLength + " characters (got " + s.Length + ")", n); return null; }
            return s;
        }

        private string OptionalString(JsonNode parent, string key, string path, int maxLength)
        {
            var n = parent.Get(key);
            if (n == null) return null;
            return RequireString(parent, key, path, maxLength, allowEmpty: true);
        }

        private int? RequireInt(JsonNode parent, string key, string path, int min, int max)
        {
            var n = parent.Get(key);
            string p = Join(path, key);
            if (n == null) { Error(p, "required number is missing", parent); return null; }
            return CheckInt(n, p, min, max);
        }

        private int? CheckInt(JsonNode n, string p, int min, int max)
        {
            if (!n.IsNumber) { Error(p, "must be a whole number from " + min + " to " + max + " (got " + Describe(n) + ")", n); return null; }
            double d = n.NumberValue;
            if (d != Math.Floor(d)) { Error(p, "must be a whole number (got " + Describe(n) + ")", n); return null; }
            if (d < min || d > max) { Error(p, "must be from " + min + " to " + max + " (got " + Describe(n) + ")", n); return null; }
            return (int)d;
        }

        private static bool IsId(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 48) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || (i > 0 && (c == '-' || c == '_' || c == '.'));
                if (!ok) return false;
            }
            return true;
        }

        private string RequireId(JsonNode parent, string path)
        {
            string id = RequireString(parent, "id", path, 48);
            if (id != null && !IsId(id))
            {
                Error(Join(path, "id"), "must use lower-case letters, digits, '-', '_' or '.' and start with a letter or digit (got \"" + id + "\")", parent.Get("id"));
                return null;
            }
            return id;
        }

        // ---- structure ----

        private void ValidateRoot(JsonNode root)
        {
            if (root == null || !root.IsObject) { Error("", "the file must contain a JSON object at the top level"); return; }
            WarnUnknownKeys(root, RootKeys, "");

            var format = root.Get("format");
            if (format == null || !format.IsString
                || (format.StringValue != RosterSchema.FormatTag && format.StringValue != RosterSchema.LegacyFormatTag))
                Error("format", "must be \"" + RosterSchema.FormatTag + "\" (got " + Describe(format) + ")", format ?? root);

            RequireInt(root, "schemaVersion", "", RosterSchema.CurrentVersion, RosterSchema.CurrentVersion);

            var league = RequireObject(root, "league", "");
            if (league != null)
            {
                WarnUnknownKeys(league, LeagueKeys, "league");
                RequireId(league, "league");
                RequireString(league, "name", "league", 60);
                RequireString(league, "shortName", "league", 8);
                OptionalString(league, "country", "league", 40);
                OptionalString(league, "season", "league", 20);
            }

            var prov = RequireObject(root, "provenance", "");
            if (prov != null)
            {
                WarnUnknownKeys(prov, ProvenanceKeys, "provenance");
                RequireString(prov, "source", "provenance", 60);
                var fictional = prov.Get("fictional");
                if (fictional != null && fictional.Kind != JsonNodeKind.Bool)
                    Error("provenance.fictional", "must be true or false (got " + Describe(fictional) + ")", fictional);
                OptionalString(prov, "notes", "provenance", 2000);
            }

            var clubs = root.Get("clubs");
            if (clubs == null) { Error("clubs", "required list of clubs is missing", root); return; }
            if (!clubs.IsArray) { Error("clubs", "must be a list [...] of clubs (got " + Describe(clubs) + ")", clubs); return; }
            if (clubs.Count < 2 || clubs.Count > RosterSchema.MaxClubs)
                Error("clubs", "must contain from 2 to " + RosterSchema.MaxClubs + " clubs (got " + clubs.Count + ")", clubs);
            for (int i = 0; i < clubs.Count; i++) ValidateClub(clubs[i], "clubs[" + i + "]");
        }

        private void ValidateClub(JsonNode club, string path)
        {
            if (!club.IsObject) { Error(path, "each club must be an object", club); return; }
            WarnUnknownKeys(club, ClubKeys, path);

            string id = RequireId(club, path);
            if (id != null && !_clubIds.Add(id)) Error(Join(path, "id"), "duplicate club id \"" + id + "\"", club.Get("id"));
            RequireString(club, "name", path, 40);

            string sn = RequireString(club, "shortName", path, 3);
            if (sn != null)
            {
                bool ok = sn.Length == 3;
                foreach (char c in sn) ok &= (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
                if (!ok) Error(Join(path, "shortName"), "must be exactly 3 upper-case letters or digits (got \"" + sn + "\")", club.Get("shortName"));
                else if (!_clubShortNames.Add(sn)) Error(Join(path, "shortName"), "duplicate club shortName \"" + sn + "\"", club.Get("shortName"));
            }

            var colours = RequireObject(club, "colours", path);
            if (colours != null)
            {
                string cp = Join(path, "colours");
                WarnUnknownKeys(colours, ColourKeys, cp);
                foreach (var key in new[] { "primary", "secondary", "away" })
                {
                    string c = RequireString(colours, key, cp, 7);
                    if (c != null && !RosterSchema.IsHexColour(c))
                        Error(Join(cp, key), "must be a colour like \"#1A2B3C\" (got \"" + c + "\")", colours.Get(key));
                }
            }

            var stadium = RequireObject(club, "stadium", path);
            if (stadium != null)
            {
                string sp = Join(path, "stadium");
                WarnUnknownKeys(stadium, StadiumKeys, sp);
                RequireString(stadium, "name", sp, 60);
                RequireInt(stadium, "capacity", sp, 1000, 150000);
            }

            string formation = RequireString(club, "formation", path, 12);
            if (formation != null && !RosterSchema.IsFormation(formation))
                Error(Join(path, "formation"), "must be an outfield shape whose lines add up to 10, like \"4-3-3\" (got \"" + formation + "\")", club.Get("formation"));

            RequireInt(club, "reputation", path, RosterSchema.StarMin, RosterSchema.StarMax);

            var style = club.Get("style");
            if (style != null && (style.Kind != JsonNodeKind.String || !TeamAITactics.TryParseStyle(style.StringValue, out _)))
                Error(Join(path, "style"), "must be one of \"balanced\", \"possession\", \"high-press\", \"counter\", \"long-ball\", \"wing-play\", \"park-the-bus\"", style);

            var players = club.Get("players");
            string pp = Join(path, "players");
            if (players == null) { Error(pp, "required list of players is missing", club); return; }
            if (!players.IsArray) { Error(pp, "must be a list [...] of players", players); return; }
            if (players.Count < RosterSchema.MinPlayersPerClub || players.Count > RosterSchema.MaxPlayersPerClub)
                Error(pp, "must contain from " + RosterSchema.MinPlayersPerClub + " to " + RosterSchema.MaxPlayersPerClub + " players (got " + players.Count + ")", players);

            var shirts = new HashSet<int>();
            int keepers = 0;
            for (int i = 0; i < players.Count; i++)
            {
                string ip = pp + "[" + i + "]";
                var p = players[i];
                if (!p.IsObject) { Error(ip, "each player must be an object", p); continue; }
                ValidatePlayer(p, ip, shirts, ref keepers);
            }
            if (players.Count > 0 && keepers == 0)
                Error(pp, "needs at least one player whose first position is GK", players);
        }

        private void ValidatePlayer(JsonNode p, string path, HashSet<int> shirts, ref int keepers)
        {
            WarnUnknownKeys(p, PlayerKeys, path);

            string id = RequireId(p, path);
            if (id != null && !_playerIds.Add(id)) Error(Join(path, "id"), "duplicate player id \"" + id + "\" (ids must be unique across the whole file)", p.Get("id"));

            RequireString(p, "firstName", path, 40, allowEmpty: true);
            RequireString(p, "lastName", path, 40);
            OptionalString(p, "knownAs", path, 40);
            RequireString(p, "nationality", path, 40);

            int? shirt = RequireInt(p, "shirtNumber", path, RosterSchema.ShirtMin, RosterSchema.ShirtMax);
            if (shirt.HasValue && !shirts.Add(shirt.Value))
                Error(Join(path, "shirtNumber"), "shirt number " + shirt.Value + " is already used in this club", p.Get("shirtNumber"));

            RequireInt(p, "age", path, RosterSchema.AgeMin, RosterSchema.AgeMax);
            RequireInt(p, "heightCm", path, RosterSchema.HeightMinCm, RosterSchema.HeightMaxCm);
            RequireInt(p, "weakFoot", path, RosterSchema.StarMin, RosterSchema.StarMax);
            RequireInt(p, "skillMoves", path, RosterSchema.StarMin, RosterSchema.StarMax);

            string foot = RequireString(p, "preferredFoot", path, 5);
            if (foot != null && !RosterSchema.TryParseFoot(foot, out _))
                Error(Join(path, "preferredFoot"), "must be \"left\" or \"right\" (got \"" + foot + "\")", p.Get("preferredFoot"));

            var positions = p.Get("positions");
            string pp = Join(path, "positions");
            if (positions == null) Error(pp, "required list of positions is missing", p);
            else if (!positions.IsArray) Error(pp, "must be a list like [\"CB\", \"RB\"] (got " + Describe(positions) + ")", positions);
            else if (positions.Count < 1 || positions.Count > RosterSchema.MaxPositionsPerPlayer)
                Error(pp, "must list from 1 to " + RosterSchema.MaxPositionsPerPlayer + " positions (got " + positions.Count + ")", positions);
            else
            {
                var seen = new HashSet<RosterPosition>();
                for (int i = 0; i < positions.Count; i++)
                {
                    var n = positions[i];
                    string ip = pp + "[" + i + "]";
                    if (!n.IsString || !RosterSchema.TryParsePosition(n.StringValue, out var pos))
                    {
                        Error(ip, "unknown position " + Describe(n) + "; use one of " + string.Join(", ", Enum.GetNames(typeof(RosterPosition))), n);
                        continue;
                    }
                    if (!seen.Add(pos)) Error(ip, "position " + pos + " is listed twice", n);
                    if (i == 0 && pos == RosterPosition.GK) keepers++;
                }
            }

            var attrs = p.Get("attributes");
            string ap = Join(path, "attributes");
            if (attrs == null) { Error(ap, "required attributes object is missing", p); return; }
            if (!attrs.IsObject) { Error(ap, "must be an object of name: value pairs", attrs); return; }
            var missing = new List<string>();
            for (int i = 0; i < RosterSchema.AttributeCount; i++)
            {
                string key = RosterSchema.AttributeKeys[i];
                var n = attrs.Get(key);
                if (n == null) { missing.Add(key); continue; }
                CheckInt(n, Join(ap, key), RosterSchema.AttributeMin, RosterSchema.AttributeMax);
            }
            if (missing.Count > 0)
                Error(ap, "missing " + missing.Count + " attribute(s): " + string.Join(", ", missing.ToArray()), attrs);
            foreach (var m in attrs.Members)
                if (!RosterSchema.TryParseAttribute(m.Key, out _))
                    Warn(Join(ap, m.Key), "unknown attribute (ignored); check the spelling against docs/ROSTER_FORMAT.md", m.Value);
        }
    }
}
