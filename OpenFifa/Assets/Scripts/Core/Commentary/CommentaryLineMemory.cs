using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Which commentary lines were already said in this play session, across matches. Hand the same instance to each
    /// match's <see cref="CommentaryEngine"/> (<see cref="CommentaryEngine.SessionMemory"/>): the engine then prefers
    /// lines not yet heard this session, and when every fitting line has been heard, the one heard longest ago. Without
    /// it a new match starts with a blank memory and the analyst can repeat the line he used for the last match's goal.
    /// Pure C#; the order of uses is a counter, so it is deterministic.
    /// </summary>
    public sealed class CommentaryLineMemory
    {
        private readonly Dictionary<string, long> _lastUse = new Dictionary<string, long>(StringComparer.Ordinal);
        private long _counter;

        /// <summary>Number of distinct lines heard so far.</summary>
        public int Count => _lastUse.Count;

        /// <summary>Records that a line was said now.</summary>
        public void Note(string lineId)
        {
            if (string.IsNullOrEmpty(lineId)) return;
            _lastUse[lineId] = ++_counter;
        }

        /// <summary>True when the line was said earlier in the session.</summary>
        public bool Heard(string lineId) => lineId != null && _lastUse.ContainsKey(lineId);

        /// <summary>Order of the line's last use (higher = more recent), or 0 when never heard.</summary>
        public long LastUse(string lineId) =>
            lineId != null && _lastUse.TryGetValue(lineId, out long n) ? n : 0;

        public void Clear()
        {
            _lastUse.Clear();
            _counter = 0;
        }

        /// <summary>
        /// Commentary RNG seed for one match: the match seed mixed with both club ids and the match's index in the
        /// session (splitmix64 finaliser, then FNV-1a over the ids), so different fixtures on the same seed, or a replay
        /// of a fixture later in the session, pick different lines. Never 0.
        /// </summary>
        public static uint MatchSeed(ulong seed, string homeId, string awayId, int matchIndex = 0)
        {
            ulong h = 14695981039346656037UL;
            foreach (var s in new[] { homeId ?? "", "|", awayId ?? "" })
                foreach (char c in s)
                {
                    h ^= c;
                    h *= 1099511628211UL;
                }
            ulong z = seed + 0x9E3779B97F4A7C15UL * (ulong)(matchIndex + 1) + h;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            uint r = (uint)(z ^ (z >> 32));
            return r == 0 ? 0x9E3779B9u : r;
        }
    }
}
