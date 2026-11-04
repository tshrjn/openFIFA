using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>One player in a roster. Immutable once loaded. Attributes are on OpenFifa's 1-20 scale.</summary>
    public sealed class RosterPlayer
    {
        private readonly int[] _attributes;
        private readonly RosterPosition[] _positions;

        public string Id { get; }
        public string FirstName { get; }
        public string LastName { get; }
        /// <summary>Name shown on HUD and spoken by commentary (usually the surname).</summary>
        public string KnownAs { get; }
        public int ShirtNumber { get; }
        /// <summary>Same as <see cref="ShirtNumber"/>; the appearance contract calls it "squadNumber".</summary>
        public int SquadNumber => ShirtNumber;
        /// <summary>Name printed on the back of the shirt, as printed (e.g. "B.FERNANDES"). Defaults to KnownAs in capitals.</summary>
        public string ShirtName { get; }
        /// <summary>Optional look for the appearance pipeline; null when the file has none.</summary>
        public RosterAppearance Appearance { get; }
        public string Nationality { get; }
        public int Age { get; }
        public int HeightCm { get; }
        public PreferredFoot PreferredFoot { get; }
        /// <summary>1-5 stars.</summary>
        public int WeakFoot { get; }
        /// <summary>1-5 stars.</summary>
        public int SkillMoves { get; }

        public IReadOnlyList<RosterPosition> Positions => _positions;
        public RosterPosition PrimaryPosition => _positions[0];

        /// <summary>Overall at the primary position, in tenths of a point (154 = 15.4 on the 1-20 scale).</summary>
        public int OverallTenths { get; }

        public string FullName => string.IsNullOrEmpty(FirstName) ? LastName : FirstName + " " + LastName;

        public RosterPlayer(string id, string firstName, string lastName, string knownAs, int shirtNumber,
            string nationality, int age, int heightCm, PreferredFoot preferredFoot, int weakFoot, int skillMoves,
            RosterPosition[] positions, int[] attributes, string shirtName = null, RosterAppearance appearance = null)
        {
            if (positions == null || positions.Length == 0) throw new ArgumentException("at least one position");
            if (attributes == null || attributes.Length != RosterSchema.AttributeCount)
                throw new ArgumentException("attributes must have " + RosterSchema.AttributeCount + " entries");
            Id = id;
            FirstName = firstName ?? string.Empty;
            LastName = lastName ?? string.Empty;
            KnownAs = string.IsNullOrEmpty(knownAs) ? LastName : knownAs;
            ShirtNumber = shirtNumber;
            ShirtName = string.IsNullOrEmpty(shirtName) ? KnownAs.ToUpperInvariant() : shirtName;
            Appearance = appearance;
            Nationality = nationality ?? string.Empty;
            Age = age;
            HeightCm = heightCm;
            PreferredFoot = preferredFoot;
            WeakFoot = weakFoot;
            SkillMoves = skillMoves;
            _positions = (RosterPosition[])positions.Clone();
            _attributes = (int[])attributes.Clone();
            OverallTenths = OverallCalculator.OverallTenths(_attributes, _positions[0]);
        }

        public int Get(RosterAttribute attribute) => _attributes[(int)attribute];

        /// <summary>Copy of the raw attribute array indexed by <see cref="RosterAttribute"/>.</summary>
        public int[] CopyAttributes() => (int[])_attributes.Clone();

        public bool CanPlay(RosterPosition position) => Array.IndexOf(_positions, position) >= 0;

        /// <summary>Overall if this player were used at another position (tenths).</summary>
        public int OverallTenthsAt(RosterPosition position) => OverallCalculator.OverallTenths(_attributes, position);

        /// <summary>Average of a display group, in tenths.</summary>
        public int GroupTenths(AttributeGroup group) => OverallCalculator.GroupTenths(_attributes, group);

        public override string ToString() => $"{KnownAs} ({PrimaryPosition}, {OverallCalculator.Format(OverallTenths)})";
    }
}
