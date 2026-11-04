using System.Collections.Generic;
using System.Text;

namespace OpenFifa.Core
{
    /// <summary>Outcome of loading a roster: the league (null on failure) plus every error and warning.</summary>
    public sealed class RosterLoadResult
    {
        private readonly List<RosterIssue> _issues;

        public RosterLeague League { get; }
        public string SourceName { get; }
        /// <summary>schemaVersion found in the file before migration (0 if unreadable).</summary>
        public int OriginalSchemaVersion { get; }
        public IReadOnlyList<RosterIssue> Issues => _issues;

        public bool Success => League != null;

        public RosterLoadResult(RosterLeague league, string sourceName, int originalSchemaVersion,
            List<RosterIssue> issues)
        {
            League = league;
            SourceName = sourceName ?? string.Empty;
            OriginalSchemaVersion = originalSchemaVersion;
            _issues = issues ?? new List<RosterIssue>();
        }

        public int ErrorCount
        {
            get
            {
                int n = 0;
                foreach (var i in _issues) if (i.IsError) n++;
                return n;
            }
        }

        public int WarningCount => _issues.Count - ErrorCount;

        /// <summary>Multi-line, user-facing report: a header line plus one line per issue.</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append(Success ? "Loaded " : "Could not load ").Append(SourceName.Length > 0 ? SourceName : "roster");
            sb.Append(" (").Append(ErrorCount).Append(" errors, ").Append(WarningCount).Append(" warnings)");
            foreach (var i in _issues) sb.Append('\n').Append(i);
            return sb.ToString();
        }

        public static RosterLoadResult Failure(string sourceName, string path, string message, int line = 0)
        {
            var issues = new List<RosterIssue> { new RosterIssue(true, path, message, line) };
            return new RosterLoadResult(null, sourceName, 0, issues);
        }
    }
}
