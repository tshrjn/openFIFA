using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// All commentary categories for one language, loaded from StreamingAssets/Commentary/lines.&lt;lang&gt;.json.
    /// Format: see docs/COMMENTARY.md. Lines may be plain strings (id = categoryId.NN by position) or objects with
    /// an explicit "id" and optional conditions. One "{name}" (player, lead voice only) or "{team}" (club) token may
    /// appear, at the very start or the very end. A line that is only "{name}" is a bare name call.
    /// </summary>
    public sealed class CommentaryLineBank
    {
        public const string FormatTag = "openfootball-commentary-lines";

        /// <summary>Format tag of line banks from before the OpenFootball rename; still accepted on load.</summary>
        public const string LegacyFormatTag = "openfifa-commentary-lines";
        public const int CurrentVersion = 1;
        public const string NameToken = "{name}";
        public const string TeamToken = "{team}";

        private readonly List<CommentaryCategory> _categories;
        private readonly Dictionary<string, CommentaryCategory> _byId;

        public string Language { get; }
        public IReadOnlyList<CommentaryCategory> Categories => _categories;

        public CommentaryLineBank(string language, List<CommentaryCategory> categories)
        {
            Language = language;
            _categories = categories;
            _byId = new Dictionary<string, CommentaryCategory>(StringComparer.Ordinal);
            foreach (var c in categories) _byId[c.Id] = c;
        }

        public CommentaryCategory Find(string id) => _byId.TryGetValue(id, out var c) ? c : null;

        private List<CommentaryCategory>[] _forCache;

        /// <summary>
        /// <see cref="For"/> without the allocation: a cached list per (event, voice), built on first use. The engine
        /// calls this on every event. Do not modify the returned list.
        /// </summary>
        internal List<CommentaryCategory> ForCached(CommentaryEventType evt, CommentaryVoice voice)
        {
            int key = (int)evt * 2 + (voice == CommentaryVoice.Analyst ? 1 : 0);
            if (key < 0) return For(evt, voice);
            if (_forCache == null || key >= _forCache.Length)
            {
                var grown = new List<CommentaryCategory>[System.Math.Max(key + 1, 64)];
                if (_forCache != null) Array.Copy(_forCache, grown, _forCache.Length);
                _forCache = grown;
            }

            return _forCache[key] ?? (_forCache[key] = For(evt, voice));
        }

        /// <summary>Categories for an event and voice, in bank order (a new list each call).</summary>
        public List<CommentaryCategory> For(CommentaryEventType evt, CommentaryVoice voice)
        {
            var list = new List<CommentaryCategory>();
            foreach (var c in _categories)
                if (c.Event == evt && c.Voice == voice) list.Add(c);
            return list;
        }

        public int LineCount
        {
            get
            {
                int n = 0;
                foreach (var c in _categories) n += c.Lines.Count;
                return n;
            }
        }

        /// <summary>Parses and validates a bank. Returns null and fills <paramref name="errors"/> on any error.</summary>
        public static CommentaryLineBank Load(string json, out List<string> errors)
        {
            errors = new List<string>();
            JsonNode root;
            try { root = JsonParser.Parse(json); }
            catch (JsonParseException ex) { errors.Add(ex.Message); return null; }

            if (!root.IsObject) { errors.Add("top level must be an object"); return null; }
            string format = root.GetString("format");
            if (format != FormatTag && format != LegacyFormatTag) errors.Add("format must be \"" + FormatTag + "\"");
            int version = root.GetInt("schemaVersion", -1);
            if (version != CurrentVersion)
                errors.Add("schemaVersion " + version + " is not supported (expected " + CurrentVersion + ")");
            string language = root.GetString("language", "en");
            var cats = root.Get("categories");
            if (cats == null || !cats.IsArray) { errors.Add("categories must be a list"); return null; }

            var list = new List<CommentaryCategory>();
            var catIds = new HashSet<string>(StringComparer.Ordinal);
            var lineIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < cats.Count; i++)
            {
                var c = ParseCategory(cats[i], "categories[" + i + "]", errors, lineIds);
                if (c == null) continue;
                if (!catIds.Add(c.Id)) { errors.Add("categories[" + i + "]: duplicate id \"" + c.Id + "\""); continue; }
                list.Add(c);
            }
            return errors.Count == 0 ? new CommentaryLineBank(language, list) : null;
        }

        private static bool TryParseTone(string s, out CommentaryIntonation tone)
        {
            tone = CommentaryIntonation.Neutral;
            switch (s)
            {
                case null:
                case "neutral": return true;
                case "excited": tone = CommentaryIntonation.Excited; return true;
                case "question": tone = CommentaryIntonation.Question; return true;
                default: return false;
            }
        }

        /// <summary>"OwnGoal" -> "own_goal".</summary>
        public static string SnakeCase(string pascal)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < pascal.Length; i++)
            {
                char ch = pascal[i];
                if (ch >= 'A' && ch <= 'Z')
                {
                    if (i > 0) sb.Append('_');
                    sb.Append((char)(ch + 32));
                }
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        private static CommentaryCategory ParseCategory(JsonNode n, string path, List<string> errors, HashSet<string> lineIds)
        {
            if (!n.IsObject) { errors.Add(path + ": must be an object"); return null; }
            string id = n.GetString("id");
            if (string.IsNullOrEmpty(id)) { errors.Add(path + ": id is required"); return null; }
            path = path + " (" + id + ")";
            int before = errors.Count;

            if (!Enum.TryParse(n.GetString("event") ?? "", false, out CommentaryEventType evt) ||
                !Enum.IsDefined(typeof(CommentaryEventType), evt) || char.IsDigit((n.GetString("event") ?? "0")[0]))
                errors.Add(path + ": unknown event \"" + n.GetString("event") + "\"");

            string voiceS = n.GetString("voice", "pbp");
            var voice = CommentaryVoice.PlayByPlay;
            if (voiceS == "analyst") voice = CommentaryVoice.Analyst;
            else if (voiceS != "pbp") errors.Add(path + ": voice must be \"pbp\" or \"analyst\"");

            string situation = n.GetString("situation");
            if (situation != null)
            {
                bool ok = false;
                foreach (var name in Enum.GetNames(typeof(GoalSituation))) ok |= SnakeCase(name) == situation;
                if (!ok) errors.Add(path + ": unknown situation \"" + situation + "\"");
            }
            string context = n.GetString("context");
            if (context != null && Array.IndexOf(CommentaryEngine.Contexts, context) < 0)
                errors.Add(path + ": unknown context \"" + context + "\" (use " + string.Join(", ", CommentaryEngine.Contexts) + ")");

            int priority = n.GetInt("priority", 50);
            if (priority < 0 || priority > 100) errors.Add(path + ": priority must be 0-100");
            float cooldown = (float)n.GetNumber("cooldown", 0);
            float maxCrowd = (float)n.GetNumber("maxCrowd", 1);
            if (!TryParseTone(n.GetString("tone"), out var tone)) errors.Add(path + ": bad tone");
            if (!TryParseTone(n.GetString("nameTone"), out var nameTone)) errors.Add(path + ": bad nameTone");

            var linesNode = n.Get("lines");
            var lines = new List<CommentaryLine>();
            if (linesNode == null || !linesNode.IsArray || linesNode.Count == 0)
                errors.Add(path + ": lines must be a non-empty list");
            else
            {
                for (int i = 0; i < linesNode.Count; i++)
                {
                    var line = ParseLine(linesNode[i], id, i, path + ".lines[" + i + "]", tone, nameTone, voice, errors);
                    if (line == null) continue;
                    if (!lineIds.Add(line.Id)) errors.Add(path + ".lines[" + i + "]: duplicate line id \"" + line.Id + "\"");
                    lines.Add(line);
                }
            }
            if (errors.Count > before) return null;
            return new CommentaryCategory(id, evt, voice, context, situation, priority,
                n.GetBool("interruptible", true), n.GetBool("resolution", false), cooldown, maxCrowd, lines.ToArray(),
                n.GetBool("live", false));
        }

        private static CommentaryLine ParseLine(JsonNode n, string catId, int index, string path,
            CommentaryIntonation defTone, CommentaryIntonation defNameTone, CommentaryVoice voice, List<string> errors)
        {
            string raw;
            string id = catId + "." + (index + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
            JsonNode obj = null;
            if (n.IsString) raw = n.StringValue;
            else if (n.IsObject)
            {
                obj = n;
                raw = n.GetString("text");
                id = n.GetString("id", id);
            }
            else { errors.Add(path + ": must be text or an object"); return null; }
            if (string.IsNullOrWhiteSpace(raw)) { errors.Add(path + ": empty text"); return null; }

            if (!SplitName(raw.Trim(), out string text, out CommentaryNamePlacement placement, out CommentaryNameKind kind,
                    out string why))
            {
                errors.Add(path + ": " + why);
                return null;
            }
            if (placement != CommentaryNamePlacement.None && kind == CommentaryNameKind.Player && voice == CommentaryVoice.Analyst)
            {
                errors.Add(path + ": analyst lines cannot use {name} (names are rendered for the lead voice only)");
                return null;
            }

            var tone = defTone;
            var nameTone = defNameTone;
            if (obj != null)
            {
                if (!TryParseTone(obj.GetString("tone") ?? ToneName(defTone), out tone)) errors.Add(path + ": bad tone");
                if (!TryParseTone(obj.GetString("nameTone") ?? ToneName(defNameTone), out nameTone)) errors.Add(path + ": bad nameTone");
                string result = obj.GetString("result");
                if (result != null && result != "draw" && result != "win") errors.Add(path + ": result must be \"draw\" or \"win\"");
                return new CommentaryLine(id, text, placement, tone, nameTone,
                    (float)obj.GetNumber("minQuality", 0), (float)obj.GetNumber("maxQuality", 1),
                    (float)obj.GetNumber("minDistance", 0), (float)obj.GetNumber("maxDistance", 1000),
                    (float)obj.GetNumber("minCrowd", 0), obj.GetInt("minPassCount", 0), result, kind);
            }
            return new CommentaryLine(id, text, placement, tone, nameTone, nameKind: kind);
        }

        private static string ToneName(CommentaryIntonation t) => t.ToString().ToLowerInvariant();

        /// <summary>Splits "{name} rises highest!" into ("rises highest!", Before). Exposed for tests and tools parity.</summary>
        public static bool SplitName(string raw, out string text, out CommentaryNamePlacement placement, out string error) =>
            SplitName(raw, out text, out placement, out _, out error);

        /// <summary>
        /// Splits a line at its "{name}" or "{team}" token, which must sit at the start or the end. A line that is only
        /// "{name}" (plus punctuation) is a bare name call with empty text. Mirrors tools/commentary/build_tts_manifest.py.
        /// </summary>
        public static bool SplitName(string raw, out string text, out CommentaryNamePlacement placement,
            out CommentaryNameKind kind, out string error)
        {
            text = raw;
            placement = CommentaryNamePlacement.None;
            kind = CommentaryNameKind.Player;
            error = null;
            int nameAt = raw.IndexOf(NameToken, StringComparison.Ordinal);
            int teamAt = raw.IndexOf(TeamToken, StringComparison.Ordinal);
            if (nameAt >= 0 && teamAt >= 0)
            {
                error = "use either {name} or {team} in a line, not both";
                return false;
            }
            string token = NameToken;
            int first = nameAt;
            if (teamAt >= 0)
            {
                token = TeamToken;
                first = teamAt;
                kind = CommentaryNameKind.Team;
            }
            if (first < 0) return true;
            if (raw.IndexOf(token, first + 1, StringComparison.Ordinal) >= 0)
            {
                error = token + " may appear only once";
                return false;
            }
            if (first == 0)
            {
                placement = CommentaryNamePlacement.Before;
                text = raw.Substring(token.Length).TrimStart(' ', ',', '.', '!', '?', ';', ':', '-');
                // A bare call keeps nothing but punctuation: text stays empty and only the name clip plays.
                if (IsPunctuationOnly(text)) text = string.Empty;
            }
            else
            {
                string tail = raw.Substring(first + token.Length);
                foreach (char ch in tail)
                    if (ch != '.' && ch != '!' && ch != '?' && ch != ' ')
                    {
                        error = token + " must be at the start or the end of the line so the name can be stitched";
                        return false;
                    }
                placement = CommentaryNamePlacement.After;
                text = raw.Substring(0, first).TrimEnd(' ', ',');
                if (text.Length == 0)
                {
                    error = "line has no text besides " + token;
                    return false;
                }
            }
            if (text.Length == 0 && kind == CommentaryNameKind.Team)
            {
                error = "a bare call must be {name}, not {team}";
                return false;
            }
            return true;
        }

        private static bool IsPunctuationOnly(string s)
        {
            foreach (char ch in s)
                if (ch != '.' && ch != '!' && ch != '?' && ch != ' ' && ch != ',') return false;
            return true;
        }
    }
}
