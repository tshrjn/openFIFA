using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Chain of <see cref="IRosterMigration"/> steps that brings an older roster document up to
    /// <see cref="RosterSchema.CurrentVersion"/>. v1 is the first published version, so the default
    /// migrator has no steps; this is the hook future format changes plug into.
    /// </summary>
    public sealed class RosterMigrator
    {
        private readonly SortedDictionary<int, IRosterMigration> _steps = new SortedDictionary<int, IRosterMigration>();

        public int TargetVersion { get; }

        public RosterMigrator(int targetVersion = RosterSchema.CurrentVersion)
        {
            TargetVersion = targetVersion;
        }

        /// <summary>The migrator used by <see cref="RosterLoader"/> when none is supplied.</summary>
        public static RosterMigrator CreateDefault() => new RosterMigrator();

        public RosterMigrator Register(IRosterMigration step)
        {
            _steps[step.FromVersion] = step;
            return this;
        }

        /// <summary>
        /// Migrates <paramref name="root"/> from <paramref name="fromVersion"/> to <see cref="TargetVersion"/>.
        /// Returns null on success, otherwise an error message.
        /// </summary>
        public string Migrate(JsonNode root, int fromVersion)
        {
            if (fromVersion > TargetVersion)
                return "schemaVersion " + fromVersion + " is newer than this build supports (" + TargetVersion +
                       "); update OpenFifa or re-export the roster for version " + TargetVersion;
            for (int v = fromVersion; v < TargetVersion; v++)
            {
                if (!_steps.TryGetValue(v, out var step))
                    return "no migration from schemaVersion " + v + " to " + (v + 1) +
                           "; this roster format is no longer supported";
                string err = step.Apply(root);
                if (err != null) return "migration " + v + "->" + (v + 1) + " failed: " + err;
                root.Set("schemaVersion", JsonNode.FromNumber(v + 1));
            }
            return null;
        }
    }
}
