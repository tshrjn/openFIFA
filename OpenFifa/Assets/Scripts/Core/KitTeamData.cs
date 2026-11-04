using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// A team entry in the kit library. Kits reference teams by <see cref="Id"/>.
    /// Public fields so UnityEngine.JsonUtility can (de)serialize it.
    /// </summary>
    [Serializable]
    public class KitTeamData
    {
        /// <summary>Stable identifier, e.g. "harbour-city".</summary>
        public string Id = "";

        /// <summary>Display name.</summary>
        public string Name = "";

        /// <summary>Up to three letters used on the placeholder crest when no crest texture is set.</summary>
        public string Initials = "";

        /// <summary>Optional squad list used to print names/numbers. Index = formation slot.</summary>
        public List<KitRosterEntry> Roster = new List<KitRosterEntry>();
    }
}
