using System.Globalization;
using System.Text;

namespace OpenFifa.Core
{
    /// <summary>
    /// Strict RFC 8259 JSON reader (no comments, no trailing commas) producing <see cref="JsonNode"/>.
    /// Duplicate object keys are rejected. Errors carry line and column. Culture-invariant.
    /// </summary>
    public sealed class JsonParser
    {
        private const int MaxDepth = 64;

        private readonly string _text;
        private int _pos;
        private int _line = 1;
        private int _lineStart;

        private JsonParser(string text)
        {
            _text = text ?? string.Empty;
        }

        public static JsonNode Parse(string text)
        {
            var p = new JsonParser(text);
            // Tolerate a UTF-8 BOM decoded as U+FEFF.
            if (p._text.Length > 0 && p._text[0] == '﻿') p._pos = 1;
            p.SkipWhitespace();
            if (p._pos >= p._text.Length) throw p.Error("document is empty");
            var root = p.ParseValue(0);
            p.SkipWhitespace();
            if (p._pos < p._text.Length) throw p.Error("unexpected text after the end of the document");
            return root;
        }

        private JsonParseException Error(string message) =>
            new JsonParseException(message, _line, _pos - _lineStart + 1);

        private void SkipWhitespace()
        {
            while (_pos < _text.Length)
            {
                char c = _text[_pos];
                if (c == '\n') { _pos++; _line++; _lineStart = _pos; }
                else if (c == ' ' || c == '\t' || c == '\r') _pos++;
                else break;
            }
        }

        private JsonNode ParseValue(int depth)
        {
            if (depth > MaxDepth) throw Error("nesting deeper than " + MaxDepth);
            if (_pos >= _text.Length) throw Error("unexpected end of document");
            int line = _line;
            JsonNode node;
            char c = _text[_pos];
            switch (c)
            {
                case '{': node = ParseObject(depth); break;
                case '[': node = ParseArray(depth); break;
                case '"': node = JsonNode.FromString(ParseString()); break;
                case 't': ExpectWord("true"); node = JsonNode.FromBool(true); break;
                case 'f': ExpectWord("false"); node = JsonNode.FromBool(false); break;
                case 'n': ExpectWord("null"); return JsonNode.Null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) { node = ParseNumber(); break; }
                    throw Error("unexpected character '" + c + "'");
            }
            node.Line = line;
            return node;
        }

        private void ExpectWord(string word)
        {
            if (string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0)
                throw Error("expected '" + word + "'");
            _pos += word.Length;
        }

        private JsonNode ParseObject(int depth)
        {
            var obj = JsonNode.NewObject();
            _pos++; // {
            SkipWhitespace();
            if (_pos < _text.Length && _text[_pos] == '}') { _pos++; return obj; }
            while (true)
            {
                SkipWhitespace();
                if (_pos >= _text.Length || _text[_pos] != '"') throw Error("expected a quoted key");
                string key = ParseString();
                SkipWhitespace();
                if (_pos >= _text.Length || _text[_pos] != ':') throw Error("expected ':' after key \"" + key + "\"");
                _pos++;
                SkipWhitespace();
                var value = ParseValue(depth + 1);
                if (!obj.TryAddMember(key, value)) throw Error("duplicate key \"" + key + "\"");
                SkipWhitespace();
                if (_pos >= _text.Length) throw Error("unterminated object");
                char c = _text[_pos++];
                if (c == '}') return obj;
                if (c != ',') { _pos--; throw Error("expected ',' or '}' in object"); }
            }
        }

        private JsonNode ParseArray(int depth)
        {
            var arr = JsonNode.NewArray();
            _pos++; // [
            SkipWhitespace();
            if (_pos < _text.Length && _text[_pos] == ']') { _pos++; return arr; }
            while (true)
            {
                SkipWhitespace();
                arr.Add(ParseValue(depth + 1));
                SkipWhitespace();
                if (_pos >= _text.Length) throw Error("unterminated array");
                char c = _text[_pos++];
                if (c == ']') return arr;
                if (c != ',') { _pos--; throw Error("expected ',' or ']' in array"); }
            }
        }

        private string ParseString()
        {
            _pos++; // opening quote
            var sb = new StringBuilder();
            while (true)
            {
                if (_pos >= _text.Length) throw Error("unterminated string");
                char c = _text[_pos++];
                if (c == '"') return sb.ToString();
                if (c < 0x20) { _pos--; throw Error("control character in string (escape it)"); }
                if (c != '\\') { sb.Append(c); continue; }
                if (_pos >= _text.Length) throw Error("unterminated escape");
                char e = _text[_pos++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (_pos + 4 > _text.Length) throw Error("truncated \\u escape");
                        if (!int.TryParse(_text.Substring(_pos, 4), NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture, out int code))
                            throw Error("invalid \\u escape");
                        sb.Append((char)code);
                        _pos += 4;
                        break;
                    default:
                        _pos--;
                        throw Error("invalid escape '\\" + e + "'");
                }
            }
        }

        private JsonNode ParseNumber()
        {
            int start = _pos;
            if (_text[_pos] == '-') _pos++;
            if (_pos >= _text.Length || !IsDigit(_text[_pos])) throw Error("invalid number");
            if (_text[_pos] == '0') _pos++;
            else while (_pos < _text.Length && IsDigit(_text[_pos])) _pos++;
            if (_pos < _text.Length && _text[_pos] == '.')
            {
                _pos++;
                if (_pos >= _text.Length || !IsDigit(_text[_pos])) throw Error("digit expected after '.'");
                while (_pos < _text.Length && IsDigit(_text[_pos])) _pos++;
            }
            if (_pos < _text.Length && (_text[_pos] == 'e' || _text[_pos] == 'E'))
            {
                _pos++;
                if (_pos < _text.Length && (_text[_pos] == '+' || _text[_pos] == '-')) _pos++;
                if (_pos >= _text.Length || !IsDigit(_text[_pos])) throw Error("digit expected in exponent");
                while (_pos < _text.Length && IsDigit(_text[_pos])) _pos++;
            }
            string s = _text.Substring(start, _pos - start);
            return JsonNode.FromNumber(double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture));
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';
    }
}
