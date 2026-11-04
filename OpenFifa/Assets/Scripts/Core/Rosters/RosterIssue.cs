namespace OpenFifa.Core
{
    /// <summary>One validation finding. Errors stop a roster from loading; warnings do not.</summary>
    public sealed class RosterIssue
    {
        public bool IsError { get; }
        /// <summary>JSON path such as "clubs[3].players[12].attributes.finishing" ("" for the whole file).</summary>
        public string Path { get; }
        public string Message { get; }
        /// <summary>Source line (1-based) when known, else 0.</summary>
        public int Line { get; }

        public RosterIssue(bool isError, string path, string message, int line)
        {
            IsError = isError;
            Path = path ?? string.Empty;
            Message = message;
            Line = line;
        }

        public override string ToString()
        {
            string where = Path.Length > 0 ? Path : "(file)";
            string at = Line > 0 ? " [line " + Line + "]" : string.Empty;
            return (IsError ? "error: " : "warning: ") + where + ": " + Message + at;
        }
    }
}
