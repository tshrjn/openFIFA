using System;

namespace OpenFifa.Core
{
    /// <summary>Thrown by <see cref="JsonParser"/> with a 1-based line and column.</summary>
    public sealed class JsonParseException : Exception
    {
        public int Line { get; }
        public int Column { get; }

        public JsonParseException(string message, int line, int column)
            : base($"JSON syntax error at line {line}, column {column}: {message}")
        {
            Line = line;
            Column = column;
        }
    }
}
