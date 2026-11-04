namespace OpenFifa.Core
{
    /// <summary>
    /// A swappable supplier of roster data. The game asks a source to load and gets a
    /// <see cref="RosterLoadResult"/>; it never needs to know whether the data is bundled or user-supplied.
    /// </summary>
    public interface IRosterSource
    {
        /// <summary>Stable id, e.g. "bundled:default_league" or "local:my_league.roster.json".</summary>
        string SourceId { get; }

        /// <summary>Label for menus.</summary>
        string DisplayName { get; }

        RosterSourceKind Kind { get; }

        RosterLoadResult Load();
    }
}
