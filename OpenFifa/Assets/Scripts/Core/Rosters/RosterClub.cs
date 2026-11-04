using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>A club in a roster: identity, colours, ground, default shape and squad.</summary>
    public sealed class RosterClub
    {
        private readonly RosterPlayer[] _players;

        public string Id { get; }
        public string Name { get; }
        /// <summary>Three upper-case letters for scoreboards (e.g. "ASH").</summary>
        public string ShortName { get; }
        /// <summary>"#RRGGBB" home shirt colour.</summary>
        public string PrimaryColour { get; }
        /// <summary>"#RRGGBB" shorts / trim colour.</summary>
        public string SecondaryColour { get; }
        /// <summary>"#RRGGBB" away / clash colour.</summary>
        public string AwayColour { get; }
        public string StadiumName { get; }
        public int StadiumCapacity { get; }
        /// <summary>Default outfield shape, e.g. "4-3-3".</summary>
        public string Formation { get; }
        /// <summary>1-5 club reputation, used by generators, AI difficulty and commentary tone.</summary>
        public int Reputation { get; }
        /// <summary>
        /// Optional playing style ("possession", "high-press", "counter", "long-ball", "wing-play", "park-the-bus",
        /// "balanced"); null when the roster gives none (the match builder then derives one). See <see cref="TeamAIStyle"/>.
        /// </summary>
        public string Style { get; }

        public IReadOnlyList<RosterPlayer> Players => _players;

        public RosterClub(string id, string name, string shortName, string primaryColour, string secondaryColour,
            string awayColour, string stadiumName, int stadiumCapacity, string formation, int reputation,
            RosterPlayer[] players, string style = null)
        {
            Style = style;
            Id = id;
            Name = name;
            ShortName = shortName;
            PrimaryColour = primaryColour;
            SecondaryColour = secondaryColour;
            AwayColour = awayColour;
            StadiumName = stadiumName;
            StadiumCapacity = stadiumCapacity;
            Formation = formation;
            Reputation = reputation;
            _players = (RosterPlayer[])(players ?? new RosterPlayer[0]).Clone();
        }

        public RosterPlayer FindPlayer(string playerId)
        {
            foreach (var p in _players)
                if (p.Id == playerId) return p;
            return null;
        }

        /// <summary>Mean primary-position overall of the best 11 by overall (tenths). Deterministic tie-break by id.</summary>
        public int StartingElevenTenths()
        {
            var list = new List<RosterPlayer>(_players);
            list.Sort((a, b) =>
            {
                int c = b.OverallTenths.CompareTo(a.OverallTenths);
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });
            int n = list.Count < 11 ? list.Count : 11;
            if (n == 0) return 0;
            int sum = 0;
            for (int i = 0; i < n; i++) sum += list[i].OverallTenths;
            return (sum + n / 2) / n;
        }
    }
}
