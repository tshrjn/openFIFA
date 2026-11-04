using System.Globalization;
using System.Text;

namespace OpenFifa.Core
{
    /// <summary>Serialises a <see cref="JsonNode"/> to compact JSON (used for saving edited rosters and in tests).</summary>
    public static class JsonWriter
    {
        public static string Serialize(JsonNode node)
        {
            var sb = new StringBuilder();
            Write(node, sb);
            return sb.ToString();
        }

        private static void Write(JsonNode n, StringBuilder sb)
        {
            if (n == null) { sb.Append("null"); return; }
            switch (n.Kind)
            {
                case JsonNodeKind.Null: sb.Append("null"); break;
                case JsonNodeKind.Bool: sb.Append(n.BoolValue ? "true" : "false"); break;
                case JsonNodeKind.Number:
                    double d = n.NumberValue;
                    if (d == System.Math.Floor(d) && System.Math.Abs(d) < 1e15)
                        sb.Append(((long)d).ToString(CultureInfo.InvariantCulture));
                    else sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case JsonNodeKind.String: WriteString(n.StringValue, sb); break;
                case JsonNodeKind.Array:
                    sb.Append('[');
                    for (int i = 0; i < n.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        Write(n[i], sb);
                    }
                    sb.Append(']');
                    break;
                default:
                    sb.Append('{');
                    bool first = true;
                    foreach (var m in n.Members)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(m.Key, sb);
                        sb.Append(':');
                        Write(m.Value, sb);
                    }
                    sb.Append('}');
                    break;
            }
        }

        private static void WriteString(string s, StringBuilder sb)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
