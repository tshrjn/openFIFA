using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenFifa.Core
{
    /// <summary>
    /// Position-weighted overall rating on OpenFifa's 1-20 scale, computed with integer maths so it is identical on
    /// every platform. Each position family has integer weights that sum to 100:
    /// overallTenths = round(sum(weight * attribute) / 10), giving 10..200 (i.e. 1.0..20.0).
    /// The table is documented in docs/ROSTER_FORMAT.md; change both together.
    /// </summary>
    public static class OverallCalculator
    {
        private struct W
        {
            public readonly RosterAttribute A;
            public readonly int Weight;
            public W(RosterAttribute a, int weight) { A = a; Weight = weight; }
        }

        private static readonly W[] Goalkeeper =
        {
            new W(RosterAttribute.GkDiving, 22), new W(RosterAttribute.GkReflexes, 22),
            new W(RosterAttribute.GkPositioning, 20), new W(RosterAttribute.GkHandling, 16),
            new W(RosterAttribute.GkDistribution, 8), new W(RosterAttribute.Reactions, 8),
            new W(RosterAttribute.Composure, 4)
        };

        private static readonly W[] CentreBack =
        {
            new W(RosterAttribute.Marking, 16), new W(RosterAttribute.StandingTackle, 16),
            new W(RosterAttribute.Interceptions, 14), new W(RosterAttribute.Heading, 12),
            new W(RosterAttribute.Strength, 10), new W(RosterAttribute.SlidingTackle, 8),
            new W(RosterAttribute.Jumping, 6), new W(RosterAttribute.Reactions, 6),
            new W(RosterAttribute.Composure, 4), new W(RosterAttribute.ShortPassing, 4),
            new W(RosterAttribute.TopSpeed, 4)
        };

        private static readonly W[] FullBack =
        {
            new W(RosterAttribute.StandingTackle, 12), new W(RosterAttribute.TopSpeed, 10),
            new W(RosterAttribute.Stamina, 10), new W(RosterAttribute.Crossing, 10),
            new W(RosterAttribute.Interceptions, 10), new W(RosterAttribute.Acceleration, 8),
            new W(RosterAttribute.SlidingTackle, 8), new W(RosterAttribute.Marking, 8),
            new W(RosterAttribute.ShortPassing, 8), new W(RosterAttribute.Dribbling, 6),
            new W(RosterAttribute.Reactions, 6), new W(RosterAttribute.Agility, 4)
        };

        private static readonly W[] DefensiveMid =
        {
            new W(RosterAttribute.Interceptions, 14), new W(RosterAttribute.ShortPassing, 14),
            new W(RosterAttribute.StandingTackle, 12), new W(RosterAttribute.LongPassing, 10),
            new W(RosterAttribute.Marking, 8), new W(RosterAttribute.Strength, 8),
            new W(RosterAttribute.Stamina, 8), new W(RosterAttribute.Composure, 8),
            new W(RosterAttribute.Reactions, 8), new W(RosterAttribute.Vision, 6),
            new W(RosterAttribute.Aggression, 4)
        };

        private static readonly W[] CentralMid =
        {
            new W(RosterAttribute.ShortPassing, 16), new W(RosterAttribute.LongPassing, 12),
            new W(RosterAttribute.Vision, 12), new W(RosterAttribute.FirstTouch, 10),
            new W(RosterAttribute.Dribbling, 8), new W(RosterAttribute.Stamina, 8),
            new W(RosterAttribute.Composure, 8), new W(RosterAttribute.Reactions, 8),
            new W(RosterAttribute.Interceptions, 6), new W(RosterAttribute.StandingTackle, 4),
            new W(RosterAttribute.LongShots, 4), new W(RosterAttribute.OffTheBall, 4)
        };

        private static readonly W[] AttackingMid =
        {
            new W(RosterAttribute.Vision, 14), new W(RosterAttribute.ShortPassing, 14),
            new W(RosterAttribute.FirstTouch, 12), new W(RosterAttribute.Dribbling, 12),
            new W(RosterAttribute.Composure, 8), new W(RosterAttribute.Reactions, 8),
            new W(RosterAttribute.LongShots, 8), new W(RosterAttribute.Finishing, 8),
            new W(RosterAttribute.Agility, 6), new W(RosterAttribute.OffTheBall, 6),
            new W(RosterAttribute.Acceleration, 4)
        };

        private static readonly W[] WideMid =
        {
            new W(RosterAttribute.Crossing, 14), new W(RosterAttribute.Dribbling, 12),
            new W(RosterAttribute.Acceleration, 10), new W(RosterAttribute.TopSpeed, 10),
            new W(RosterAttribute.ShortPassing, 10), new W(RosterAttribute.Stamina, 10),
            new W(RosterAttribute.FirstTouch, 8), new W(RosterAttribute.Vision, 6),
            new W(RosterAttribute.Agility, 6), new W(RosterAttribute.Reactions, 6),
            new W(RosterAttribute.Curl, 4), new W(RosterAttribute.OffTheBall, 4)
        };

        private static readonly W[] Winger =
        {
            new W(RosterAttribute.Dribbling, 16), new W(RosterAttribute.Acceleration, 12),
            new W(RosterAttribute.TopSpeed, 10), new W(RosterAttribute.FirstTouch, 10),
            new W(RosterAttribute.Finishing, 10), new W(RosterAttribute.Agility, 8),
            new W(RosterAttribute.Crossing, 8), new W(RosterAttribute.OffTheBall, 8),
            new W(RosterAttribute.ShortPassing, 6), new W(RosterAttribute.Reactions, 6),
            new W(RosterAttribute.Composure, 6)
        };

        private static readonly W[] CentreForward =
        {
            new W(RosterAttribute.Finishing, 14), new W(RosterAttribute.FirstTouch, 12),
            new W(RosterAttribute.OffTheBall, 10), new W(RosterAttribute.Dribbling, 10),
            new W(RosterAttribute.ShortPassing, 10), new W(RosterAttribute.Reactions, 10),
            new W(RosterAttribute.Vision, 8), new W(RosterAttribute.Composure, 8),
            new W(RosterAttribute.LongShots, 6), new W(RosterAttribute.Acceleration, 6),
            new W(RosterAttribute.ShotPower, 6)
        };

        private static readonly W[] Striker =
        {
            new W(RosterAttribute.Finishing, 20), new W(RosterAttribute.OffTheBall, 12),
            new W(RosterAttribute.Reactions, 10), new W(RosterAttribute.Heading, 8),
            new W(RosterAttribute.ShotPower, 8), new W(RosterAttribute.Composure, 8),
            new W(RosterAttribute.FirstTouch, 8), new W(RosterAttribute.Acceleration, 8),
            new W(RosterAttribute.TopSpeed, 6), new W(RosterAttribute.Strength, 6),
            new W(RosterAttribute.Dribbling, 6)
        };

        private static W[] TableFor(RosterPosition position)
        {
            switch (position)
            {
                case RosterPosition.GK: return Goalkeeper;
                case RosterPosition.CB: return CentreBack;
                case RosterPosition.RB:
                case RosterPosition.LB:
                case RosterPosition.RWB:
                case RosterPosition.LWB: return FullBack;
                case RosterPosition.CDM: return DefensiveMid;
                case RosterPosition.CM: return CentralMid;
                case RosterPosition.CAM: return AttackingMid;
                case RosterPosition.RM:
                case RosterPosition.LM: return WideMid;
                case RosterPosition.RW:
                case RosterPosition.LW: return Winger;
                case RosterPosition.CF: return CentreForward;
                case RosterPosition.ST: return Striker;
                default: throw new ArgumentOutOfRangeException(nameof(position));
            }
        }

        /// <summary>Weights for a position, in table order (for docs, UI and tests).</summary>
        public static IReadOnlyList<KeyValuePair<RosterAttribute, int>> WeightsFor(RosterPosition position)
        {
            var t = TableFor(position);
            var list = new List<KeyValuePair<RosterAttribute, int>>(t.Length);
            foreach (var w in t) list.Add(new KeyValuePair<RosterAttribute, int>(w.A, w.Weight));
            return list;
        }

        /// <summary>Overall in tenths (10..200) for an attribute array indexed by <see cref="RosterAttribute"/>.</summary>
        public static int OverallTenths(int[] attributes, RosterPosition position)
        {
            if (attributes == null || attributes.Length != RosterSchema.AttributeCount)
                throw new ArgumentException("attributes must have " + RosterSchema.AttributeCount + " entries");
            int sum = 0;
            foreach (var w in TableFor(position)) sum += w.Weight * attributes[(int)w.A];
            // sum is in [100, 2000]; tenths = sum / 10 rounded half up.
            return (sum + 5) / 10;
        }

        /// <summary>Plain mean of a display group, in tenths, rounded half up.</summary>
        public static int GroupTenths(int[] attributes, AttributeGroup group)
        {
            int sum = 0, n = 0;
            for (int i = 0; i < attributes.Length; i++)
            {
                if (RosterSchema.GroupOf((RosterAttribute)i) != group) continue;
                sum += attributes[i];
                n++;
            }
            return n == 0 ? 0 : (sum * 10 * 2 + n) / (2 * n);
        }

        /// <summary>"15.4" for 154.</summary>
        public static string Format(int tenths) =>
            (tenths / 10).ToString(CultureInfo.InvariantCulture) + "." +
            (tenths % 10).ToString(CultureInfo.InvariantCulture);
    }
}
