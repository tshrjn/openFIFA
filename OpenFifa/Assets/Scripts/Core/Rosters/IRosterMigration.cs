namespace OpenFifa.Core
{
    /// <summary>
    /// Upgrades a parsed roster document from <see cref="FromVersion"/> to FromVersion + 1 in place.
    /// Register steps on a <see cref="RosterMigrator"/>; the migrator rewrites "schemaVersion" after each step.
    /// </summary>
    public interface IRosterMigration
    {
        int FromVersion { get; }

        /// <summary>Mutates <paramref name="root"/>. Return null on success or a human-readable error.</summary>
        string Apply(JsonNode root);
    }
}
