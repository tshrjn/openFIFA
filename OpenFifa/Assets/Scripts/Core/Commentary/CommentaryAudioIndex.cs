using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// The rendered commentary audio index (StreamingAssets/Commentary/audio/&lt;lang&gt;/index.json, written by
    /// tools/commentary/render_tts.py): which clips exist per voice and how long each is. Pure C#.
    /// Folder layout: &lt;lang&gt;/lead/&lt;clipId&gt;.ogg and &lt;lang&gt;/analyst/&lt;clipId&gt;.ogg.
    /// </summary>
    public sealed class CommentaryAudioIndex : ICommentaryClipCatalog
    {
        public const string FormatTag = "openfootball-commentary-audio";

        private readonly Dictionary<string, double>[] _byVoice =
        {
            new Dictionary<string, double>(StringComparer.Ordinal),
            new Dictionary<string, double>(StringComparer.Ordinal)
        };

        public int SampleRate { get; private set; }

        public int Count => _byVoice[0].Count + _byVoice[1].Count;

        /// <summary>Folder name of a voice inside the language folder.</summary>
        public static string VoiceFolder(CommentaryVoice voice) => voice == CommentaryVoice.Analyst ? "analyst" : "lead";

        /// <summary>Path of a clip relative to the language folder, e.g. "lead/line.goal.open_play.01.ogg".</summary>
        public static string RelativePath(CommentaryVoice voice, string clipId) => VoiceFolder(voice) + "/" + clipId + ".ogg";

        public bool Has(CommentaryVoice voice, string clipId) =>
            clipId != null && _byVoice[voice == CommentaryVoice.Analyst ? 1 : 0].ContainsKey(clipId);

        public double Duration(CommentaryVoice voice, string clipId) =>
            clipId != null && _byVoice[voice == CommentaryVoice.Analyst ? 1 : 0].TryGetValue(clipId, out double d) ? d : 0;

        /// <summary>All clip ids of a voice (for preloading).</summary>
        public IEnumerable<string> ClipIds(CommentaryVoice voice) => _byVoice[voice == CommentaryVoice.Analyst ? 1 : 0].Keys;

        /// <summary>Parses index.json. Returns null and sets <paramref name="error"/> when it is not a valid index.</summary>
        public static CommentaryAudioIndex Parse(string json, out string error)
        {
            error = null;
            JsonNode root;
            try { root = JsonParser.Parse(json); }
            catch (JsonParseException ex)
            {
                error = ex.Message;
                return null;
            }
            if (!root.IsObject || root.GetString("format") != FormatTag)
            {
                error = "format must be \"" + FormatTag + "\"";
                return null;
            }
            var voices = root.Get("voices");
            if (voices == null || !voices.IsObject)
            {
                error = "voices must be an object";
                return null;
            }
            var index = new CommentaryAudioIndex { SampleRate = root.GetInt("sampleRate", 24000) };
            foreach (var v in voices.Members)
            {
                int slot;
                if (v.Key == "lead") slot = 0;
                else if (v.Key == "analyst") slot = 1;
                else continue;
                if (!v.Value.IsObject) continue;
                foreach (var clip in v.Value.Members)
                {
                    var entry = clip.Value;
                    double d = entry.IsArray && entry.Count > 0 && entry[0].IsNumber ? entry[0].NumberValue
                        : entry.IsNumber ? entry.NumberValue : 0;
                    index._byVoice[slot][clip.Key] = d;
                }
            }
            return index;
        }
    }
}
