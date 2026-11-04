using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenFifa.Core
{
    /// <summary>
    /// Minimal JSON DOM used by the roster and commentary loaders (Core has no Newtonsoft and no UnityEngine).
    /// Objects keep their keys in document order so iteration is deterministic.
    /// </summary>
    public sealed class JsonNode
    {
        private readonly List<KeyValuePair<string, JsonNode>> _members;
        private readonly Dictionary<string, int> _index;
        private readonly List<JsonNode> _items;

        public JsonNodeKind Kind { get; }
        public bool BoolValue { get; }
        public double NumberValue { get; }
        public string StringValue { get; }

        /// <summary>Line of the value's first character in the source (1-based, 0 if built in code).</summary>
        public int Line { get; internal set; }

        public static readonly JsonNode Null = new JsonNode(JsonNodeKind.Null);

        private JsonNode(JsonNodeKind kind)
        {
            Kind = kind;
            if (kind == JsonNodeKind.Object)
            {
                _members = new List<KeyValuePair<string, JsonNode>>();
                _index = new Dictionary<string, int>(StringComparer.Ordinal);
            }
            else if (kind == JsonNodeKind.Array)
            {
                _items = new List<JsonNode>();
            }
        }

        private JsonNode(bool value) : this(JsonNodeKind.Bool) { BoolValue = value; }
        private JsonNode(double value) : this(JsonNodeKind.Number) { NumberValue = value; }
        private JsonNode(string value) : this(JsonNodeKind.String) { StringValue = value; }

        public static JsonNode FromBool(bool value) => new JsonNode(value);
        public static JsonNode FromNumber(double value) => new JsonNode(value);
        public static JsonNode FromString(string value) => new JsonNode(value ?? string.Empty);
        public static JsonNode NewObject() => new JsonNode(JsonNodeKind.Object);
        public static JsonNode NewArray() => new JsonNode(JsonNodeKind.Array);

        public bool IsObject => Kind == JsonNodeKind.Object;
        public bool IsArray => Kind == JsonNodeKind.Array;
        public bool IsString => Kind == JsonNodeKind.String;
        public bool IsNumber => Kind == JsonNodeKind.Number;

        /// <summary>Number of array items or object members.</summary>
        public int Count => _items != null ? _items.Count : (_members != null ? _members.Count : 0);

        public JsonNode this[int index] => _items[index];

        public IReadOnlyList<JsonNode> Items =>
            _items ?? (IReadOnlyList<JsonNode>)Array.Empty<JsonNode>();

        /// <summary>Object members in document order.</summary>
        public IReadOnlyList<KeyValuePair<string, JsonNode>> Members =>
            _members ?? (IReadOnlyList<KeyValuePair<string, JsonNode>>)Array.Empty<KeyValuePair<string, JsonNode>>();

        public bool Has(string key) => _index != null && _index.ContainsKey(key);

        /// <summary>Returns the member or null when absent (or when this is not an object).</summary>
        public JsonNode Get(string key)
        {
            if (_index == null) return null;
            return _index.TryGetValue(key, out int i) ? _members[i].Value : null;
        }

        /// <summary>Adds or replaces a member (objects only). Replacing keeps the original position.</summary>
        public void Set(string key, JsonNode value)
        {
            if (_index == null) throw new InvalidOperationException("Set on a non-object JSON value");
            if (_index.TryGetValue(key, out int i))
                _members[i] = new KeyValuePair<string, JsonNode>(key, value);
            else
            {
                _index[key] = _members.Count;
                _members.Add(new KeyValuePair<string, JsonNode>(key, value));
            }
        }

        /// <summary>Removes a member; returns false if absent.</summary>
        public bool Remove(string key)
        {
            if (_index == null || !_index.TryGetValue(key, out int i)) return false;
            _members.RemoveAt(i);
            _index.Clear();
            for (int k = 0; k < _members.Count; k++) _index[_members[k].Key] = k;
            return true;
        }

        public void Add(JsonNode item)
        {
            if (_items == null) throw new InvalidOperationException("Add on a non-array JSON value");
            _items.Add(item);
        }

        /// <summary>Internal: used by the parser to detect duplicate keys.</summary>
        internal bool TryAddMember(string key, JsonNode value)
        {
            if (_index.ContainsKey(key)) return false;
            _index[key] = _members.Count;
            _members.Add(new KeyValuePair<string, JsonNode>(key, value));
            return true;
        }

        public string GetString(string key, string fallback = null)
        {
            var n = Get(key);
            return n != null && n.Kind == JsonNodeKind.String ? n.StringValue : fallback;
        }

        public double GetNumber(string key, double fallback)
        {
            var n = Get(key);
            return n != null && n.Kind == JsonNodeKind.Number ? n.NumberValue : fallback;
        }

        public int GetInt(string key, int fallback)
        {
            var n = Get(key);
            return n != null && n.Kind == JsonNodeKind.Number ? (int)Math.Round(n.NumberValue) : fallback;
        }

        public bool GetBool(string key, bool fallback)
        {
            var n = Get(key);
            return n != null && n.Kind == JsonNodeKind.Bool ? n.BoolValue : fallback;
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case JsonNodeKind.Null: return "null";
                case JsonNodeKind.Bool: return BoolValue ? "true" : "false";
                case JsonNodeKind.Number: return NumberValue.ToString("R", CultureInfo.InvariantCulture);
                case JsonNodeKind.String: return "\"" + StringValue + "\"";
                case JsonNodeKind.Array: return "[" + Count + " items]";
                default: return "{" + Count + " members}";
            }
        }
    }
}
