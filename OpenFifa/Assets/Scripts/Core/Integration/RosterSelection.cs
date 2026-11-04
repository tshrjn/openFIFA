using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFifa.Core
{
    /// <summary>
    /// Resolves command-line club choices (<c>-spLeague</c>, <c>-spHome</c>, <c>-spAway</c>: ids from the roster JSON) to
    /// a league index and club indices in that league. Ids match case-insensitively. Both clubs must be in one league
    /// (the match picker selects clubs by index within a league).
    /// </summary>
    public static class RosterSelection
    {
        /// <summary>
        /// Updates the indices in place from the ids that are given (null or empty = keep). Returns false, with a reason
        /// in <paramref name="problems"/>, when an id is unknown, the clubs are in different leagues or are the same club;
        /// the indices are then left unchanged.
        /// </summary>
        public static bool TryResolve(IReadOnlyList<RosterLeague> leagues, string leagueId, string homeId, string awayId,
            ref int leagueIndex, ref int homeIndex, ref int awayIndex, List<string> problems)
        {
            bool hasLeague = !string.IsNullOrEmpty(leagueId), hasHome = !string.IsNullOrEmpty(homeId), hasAway = !string.IsNullOrEmpty(awayId);
            if (!hasLeague && !hasHome && !hasAway) return true;
            if (leagues == null || leagues.Count == 0)
            {
                problems?.Add("no roster leagues loaded; club selection ignored");
                return false;
            }

            int li = -1;
            if (hasLeague)
            {
                for (int i = 0; i < leagues.Count && li < 0; i++)
                    if (Same(leagues[i].Id, leagueId)) li = i;
                if (li < 0)
                {
                    problems?.Add("unknown league id '" + leagueId + "'");
                    return false;
                }
            }
            else
            {
                string first = hasHome ? homeId : awayId;
                for (int i = 0; i < leagues.Count && li < 0; i++)
                    if (IndexOf(leagues[i], first) >= 0) li = i;
                if (li < 0)
                {
                    problems?.Add("unknown club id '" + first + "'");
                    return false;
                }
            }

            var league = leagues[li];
            int hi = leagueIndex == li ? homeIndex : 0, ai = leagueIndex == li ? awayIndex : 1;
            if (hasHome && !Find(leagues, league, homeId, ref hi, problems)) return false;
            if (hasAway && !Find(leagues, league, awayId, ref ai, problems)) return false;
            int n = league.Clubs.Count;
            if (n < 2)
            {
                problems?.Add("league '" + league.Id + "' has fewer than two clubs");
                return false;
            }
            hi = Clamp(hi, n);
            ai = Clamp(ai, n);
            if (hi == ai)
            {
                if (hasHome && hasAway)
                {
                    problems?.Add("home and away are the same club ('" + league.Clubs[hi].Id + "')");
                    return false;
                }
                // Only one side named and it collides with the other side's default: move the default along.
                if (hasHome) ai = (hi + 1) % n; else hi = (ai + 1) % n;
            }
            leagueIndex = li;
            homeIndex = hi;
            awayIndex = ai;
            return true;
        }

        /// <summary>Index of the club with this id in the league, or -1.</summary>
        public static int IndexOf(RosterLeague league, string clubId)
        {
            if (league == null || string.IsNullOrEmpty(clubId)) return -1;
            for (int i = 0; i < league.Clubs.Count; i++)
                if (Same(league.Clubs[i].Id, clubId)) return i;
            return -1;
        }

        /// <summary>One line per league: "league &lt;id&gt; (&lt;name&gt;, N clubs): id1, id2, ...", for capture.txt and logs.</summary>
        public static string Describe(IReadOnlyList<RosterLeague> leagues)
        {
            var sb = new StringBuilder(1024);
            if (leagues == null) return string.Empty;
            for (int i = 0; i < leagues.Count; i++)
            {
                var l = leagues[i];
                sb.Append("league ").Append(l.Id).Append(" (").Append(l.Name).Append(", ").Append(l.Clubs.Count).Append(" clubs): ");
                for (int c = 0; c < l.Clubs.Count; c++)
                {
                    if (c > 0) sb.Append(", ");
                    sb.Append(l.Clubs[c].Id);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private static bool Find(IReadOnlyList<RosterLeague> leagues, RosterLeague league, string id, ref int index, List<string> problems)
        {
            int i = IndexOf(league, id);
            if (i >= 0)
            {
                index = i;
                return true;
            }
            for (int k = 0; k < leagues.Count; k++)
            {
                if (IndexOf(leagues[k], id) < 0) continue;
                problems?.Add("club '" + id + "' is in league '" + leagues[k].Id + "', not '" + league.Id + "'; pick two clubs from one league");
                return false;
            }
            problems?.Add("unknown club id '" + id + "' in league '" + league.Id + "'");
            return false;
        }

        private static int Clamp(int i, int n) => i < 0 ? 0 : (i >= n ? n - 1 : i);

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
