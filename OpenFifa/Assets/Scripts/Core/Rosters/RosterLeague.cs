using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>Root of a loaded roster file: one league and its clubs.</summary>
    public sealed class RosterLeague
    {
        private readonly RosterClub[] _clubs;

        public int SchemaVersion { get; }
        public string Id { get; }
        public string Name { get; }
        public string ShortName { get; }
        public string Country { get; }
        public string Season { get; }
        /// <summary>Where the data came from ("fictional-generated", "user-import", ...).</summary>
        public string ProvenanceSource { get; }
        public string ProvenanceNotes { get; }
        /// <summary>True when every name in the file is invented (the bundled default).</summary>
        public bool IsFictional { get; }

        public IReadOnlyList<RosterClub> Clubs => _clubs;

        public RosterLeague(int schemaVersion, string id, string name, string shortName, string country,
            string season, string provenanceSource, string provenanceNotes, bool isFictional, RosterClub[] clubs)
        {
            SchemaVersion = schemaVersion;
            Id = id;
            Name = name;
            ShortName = shortName;
            Country = country;
            Season = season;
            ProvenanceSource = provenanceSource;
            ProvenanceNotes = provenanceNotes;
            IsFictional = isFictional;
            _clubs = (RosterClub[])(clubs ?? new RosterClub[0]).Clone();
        }

        public RosterClub FindClub(string clubId)
        {
            foreach (var c in _clubs)
                if (c.Id == clubId) return c;
            return null;
        }

        public RosterPlayer FindPlayer(string playerId)
        {
            foreach (var c in _clubs)
            {
                var p = c.FindPlayer(playerId);
                if (p != null) return p;
            }
            return null;
        }

        public int PlayerCount
        {
            get
            {
                int n = 0;
                foreach (var c in _clubs) n += c.Players.Count;
                return n;
            }
        }
    }
}
