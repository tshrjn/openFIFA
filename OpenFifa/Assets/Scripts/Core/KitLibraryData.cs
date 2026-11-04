using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Root object of a kit library JSON file. Several files can be merged
    /// (e.g. the placeholder library plus a licensed pack) with
    /// <see cref="KitLibraryLogic.Merge"/>; later files override by id.
    /// </summary>
    [Serializable]
    public class KitLibraryData
    {
        /// <summary>Current schema version written by this code.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Schema version of the file.</summary>
        public int SchemaVersion = CurrentSchemaVersion;

        /// <summary>Team used for the home side when the match does not specify one.</summary>
        public string DefaultHomeTeamId = "";

        /// <summary>Team used for the away side when the match does not specify one.</summary>
        public string DefaultAwayTeamId = "";

        /// <summary>Teams.</summary>
        public List<KitTeamData> Teams = new List<KitTeamData>();

        /// <summary>Kits; each references a team.</summary>
        public List<KitDefinitionData> Kits = new List<KitDefinitionData>();
    }
}
