using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenFifa.Core
{
    /// <summary>Per-team totals gathered by <see cref="MatchTacticsStats"/>.</summary>
    public struct TeamTacticsStats
    {
        public int Goals;
        public int Shots;
        /// <summary>Shots that the opposing keeper saved or that went in (at most one per shot; own goals excluded).</summary>
        public int ShotsOnTarget;
        public float ExpectedGoals;
        public int Passes;
        public int PassesCompleted;
        /// <summary>Live-play ticks with this team in possession.</summary>
        public int PossessionTicks;
        public int TacklesAttempted;
        public int TacklesWon;
        public int Interceptions;
        public int Fouls;
        /// <summary>Opposition passes made in the zone this team presses (the opposition's own 60% of the pitch).</summary>
        public int OppositionPassesInPressZone;
        /// <summary>This team's tackles, interceptions and fouls in that zone.</summary>
        public int DefensiveActionsInPressZone;

        // Shape samples (outfield players), split by whether the team had the ball.
        public int SamplesWithBall, SamplesWithoutBall;
        public double WidthWithBall, WidthWithoutBall;
        public double DepthWithBall, DepthWithoutBall;
        public double LineWithBall, LineWithoutBall;

        public float PassCompletion => Passes > 0 ? PassesCompleted / (float)Passes : 0f;
        /// <summary>Passes allowed per defensive action; lower = more aggressive pressing. Negative when undefined.</summary>
        public float Ppda => DefensiveActionsInPressZone > 0 ? OppositionPassesInPressZone / (float)DefensiveActionsInPressZone : -1f;
        public int Samples => SamplesWithBall + SamplesWithoutBall;
        public float Width => Avg(WidthWithBall + WidthWithoutBall, Samples);
        public float Depth => Avg(DepthWithBall + DepthWithoutBall, Samples);
        public float LineHeight => Avg(LineWithBall + LineWithoutBall, Samples);

        internal static float Avg(double sum, int n) => n > 0 ? (float)(sum / n) : 0f;
    }

    /// <summary>
    /// Headless tactical summary of a <see cref="MatchSim"/> run, for the review capture's tactics.txt: possession,
    /// passes and completion, shots and xG, team shape (width across the pitch and depth along it, outfield players),
    /// defensive line height (mean distance of the four deepest outfield players from their own goal line) and PPDA
    /// (opposition passes in their own 60% of the pitch per tackle, interception or foul there).
    /// Call <see cref="Observe"/> once after every sim step with that step's events. Allocation-free after construction.
    /// </summary>
    public sealed class MatchTacticsStats
    {
        /// <summary>Shape is sampled every this many ticks of live play.</summary>
        public readonly int SampleEveryTicks;
        /// <summary>Players counted in the defensive line.</summary>
        public const int BackLinePlayers = 4;
        /// <summary>The pressing zone: the opposition's own 60% of the pitch.</summary>
        public const float PressZoneFraction = 0.6f;

        private readonly TeamTacticsStats[] _teams = new TeamTacticsStats[2];
        private readonly float[] _coords = new float[MatchSetup.PlayersPerTeam];
        private int _liveTicks;
        // A shot is waiting for its outcome (a save or goal counts it on target once); cleared by the next pass or shot.
        private readonly bool[] _shotPending = new bool[2];

        public MatchTacticsStats(int sampleEveryTicks = 30)
        {
            SampleEveryTicks = sampleEveryTicks < 1 ? 1 : sampleEveryTicks;
        }

        public TeamTacticsStats Home => _teams[0];
        public TeamTacticsStats Away => _teams[1];
        public TeamTacticsStats this[SimTeam team] => _teams[team == SimTeam.Away ? 1 : 0];
        public int LiveTicks => _liveTicks;

        /// <summary>Fraction of live play the team had the ball (0.5 each before any possession).</summary>
        public float Possession(SimTeam team)
        {
            int total = _teams[0].PossessionTicks + _teams[1].PossessionTicks;
            return total > 0 ? this[team].PossessionTicks / (float)total : 0.5f;
        }

        /// <summary>Attack sign of a team in a half (+1 = attacks +Z), as <see cref="MatchSim.AttackSign"/>.</summary>
        public static int AttackSign(SimTeam team, int half)
        {
            int s = team == SimTeam.Home ? 1 : -1;
            return half <= 1 ? s : -s;
        }

        /// <summary>True when <paramref name="pos"/> is in the zone <paramref name="presser"/> presses in that half.</summary>
        public static bool InPressZone(SimTeam presser, int half, SimVec3 pos, PitchSpec pitch)
        {
            // Opposition's own 60% = everything beyond 40% of the pitch from the presser's own goal line.
            float fromOwnGoal = pos.Z * AttackSign(presser, half) + pitch.HalfLength;
            return fromOwnGoal >= (1f - PressZoneFraction) * pitch.Length;
        }

        public void Observe(MatchSim sim, IReadOnlyList<MatchEvent> events)
        {
            var s = sim.State;
            var pitch = sim.Config.Pitch;
            if (events != null)
                for (int i = 0; i < events.Count; i++) Count(events[i], pitch);

            if (s.Phase != SimMatchPhase.InPlay) return;
            _liveTicks++;
            if (s.PossessionTeam == SimTeam.Home) _teams[0].PossessionTicks++;
            else if (s.PossessionTeam == SimTeam.Away) _teams[1].PossessionTicks++;
            if (s.Tick % SampleEveryTicks != 0) return;
            SampleShape(s, SimTeam.Home, pitch);
            SampleShape(s, SimTeam.Away, pitch);
        }

        private void Count(in MatchEvent e, PitchSpec pitch)
        {
            int t = e.Team == SimTeam.Away ? 1 : 0;
            bool known = e.Team == SimTeam.Home || e.Team == SimTeam.Away;
            if (!known) return;
            var opp = MatchSim.Opponent(e.Team);
            int o = 1 - t;
            switch (e.Type)
            {
                case MatchEventType.Pass:
                    _shotPending[0] = _shotPending[1] = false;
                    _teams[t].Passes++;
                    if (InPressZone(opp, e.Half, e.Position, pitch)) _teams[o].OppositionPassesInPressZone++;
                    break;
                case MatchEventType.PassCompleted:
                    _teams[t].PassesCompleted++;
                    break;
                case MatchEventType.Shot:
                    _shotPending[0] = _shotPending[1] = false;
                    _shotPending[t] = true;
                    _teams[t].Shots++;
                    _teams[t].ExpectedGoals += e.Value;
                    break;
                case MatchEventType.Save: // Team = keeper's team: the shot was the opponent's
                    if (_shotPending[o]) _teams[o].ShotsOnTarget++;
                    _shotPending[o] = false;
                    break;
                case MatchEventType.Goal:
                    _teams[t].Goals++;
                    if (e.Detail == 0 && _shotPending[t]) _teams[t].ShotsOnTarget++;
                    _shotPending[t] = false;
                    break;
                case MatchEventType.Tackle:
                    _teams[t].TacklesAttempted++;
                    if (e.Detail == 1) _teams[t].TacklesWon++;
                    if (InPressZone(e.Team, e.Half, e.Position, pitch)) _teams[t].DefensiveActionsInPressZone++;
                    break;
                case MatchEventType.Interception:
                    _teams[t].Interceptions++;
                    if (InPressZone(e.Team, e.Half, e.Position, pitch)) _teams[t].DefensiveActionsInPressZone++;
                    break;
                case MatchEventType.Foul:
                    _teams[t].Fouls++;
                    if (InPressZone(e.Team, e.Half, e.Position, pitch)) _teams[t].DefensiveActionsInPressZone++;
                    break;
            }
        }

        private void SampleShape(MatchSimState s, SimTeam team, PitchSpec pitch)
        {
            int first = MatchSim.FirstId(team), keeper = MatchSim.KeeperId(team);
            int sign = AttackSign(team, s.Half);
            float minX = float.MaxValue, maxX = float.MinValue;
            int n = 0;
            for (int id = first; id < first + MatchSetup.PlayersPerTeam; id++)
            {
                if (id == keeper || s.Players[id].SentOff) continue;
                var p = s.Players[id].Position;
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                _coords[n++] = p.Z * sign + pitch.HalfLength; // metres from own goal line
            }
            if (n < 2) return;
            // Insertion sort (n <= 10): deepest first.
            for (int i = 1; i < n; i++)
            {
                float v = _coords[i];
                int j = i - 1;
                while (j >= 0 && _coords[j] > v) { _coords[j + 1] = _coords[j]; j--; }
                _coords[j + 1] = v;
            }
            int line = n < BackLinePlayers ? n : BackLinePlayers;
            float lineSum = 0f;
            for (int i = 0; i < line; i++) lineSum += _coords[i];
            float width = maxX - minX, depth = _coords[n - 1] - _coords[0], lineHeight = lineSum / line;

            int k = team == SimTeam.Away ? 1 : 0;
            if (s.PossessionTeam == team)
            {
                _teams[k].SamplesWithBall++;
                _teams[k].WidthWithBall += width;
                _teams[k].DepthWithBall += depth;
                _teams[k].LineWithBall += lineHeight;
            }
            else
            {
                _teams[k].SamplesWithoutBall++;
                _teams[k].WidthWithoutBall += width;
                _teams[k].DepthWithoutBall += depth;
                _teams[k].LineWithoutBall += lineHeight;
            }
        }

        /// <summary>Two-column table (home, away) for tactics.txt.</summary>
        public string Format(string homeLabel, string awayLabel)
        {
            var sb = new StringBuilder(2048);
            var h = _teams[0];
            var a = _teams[1];
            Row(sb, "", homeLabel, awayLabel);
            Row(sb, "goals", I(h.Goals), I(a.Goals));
            Row(sb, "possession %", P(Possession(SimTeam.Home)), P(Possession(SimTeam.Away)));
            Row(sb, "passes", I(h.Passes), I(a.Passes));
            Row(sb, "passes completed", I(h.PassesCompleted), I(a.PassesCompleted));
            Row(sb, "pass completion %", P(h.PassCompletion), P(a.PassCompletion));
            Row(sb, "shots", I(h.Shots), I(a.Shots));
            Row(sb, "shots on target", I(h.ShotsOnTarget), I(a.ShotsOnTarget));
            Row(sb, "xG", F(h.ExpectedGoals, 2), F(a.ExpectedGoals, 2));
            Row(sb, "tackles (won/attempted)", h.TacklesWon + "/" + h.TacklesAttempted, a.TacklesWon + "/" + a.TacklesAttempted);
            Row(sb, "interceptions", I(h.Interceptions), I(a.Interceptions));
            Row(sb, "fouls", I(h.Fouls), I(a.Fouls));
            Row(sb, "PPDA", h.Ppda < 0f ? "n/a" : F(h.Ppda, 1), a.Ppda < 0f ? "n/a" : F(a.Ppda, 1));
            Row(sb, "  opp. passes in press zone", I(h.OppositionPassesInPressZone), I(a.OppositionPassesInPressZone));
            Row(sb, "  def. actions in press zone", I(h.DefensiveActionsInPressZone), I(a.DefensiveActionsInPressZone));
            Row(sb, "shape width m (all)", F(h.Width, 1), F(a.Width, 1));
            Row(sb, "  with ball", F(TeamTacticsStats.Avg(h.WidthWithBall, h.SamplesWithBall), 1), F(TeamTacticsStats.Avg(a.WidthWithBall, a.SamplesWithBall), 1));
            Row(sb, "  without ball", F(TeamTacticsStats.Avg(h.WidthWithoutBall, h.SamplesWithoutBall), 1), F(TeamTacticsStats.Avg(a.WidthWithoutBall, a.SamplesWithoutBall), 1));
            Row(sb, "shape depth m (all)", F(h.Depth, 1), F(a.Depth, 1));
            Row(sb, "  with ball", F(TeamTacticsStats.Avg(h.DepthWithBall, h.SamplesWithBall), 1), F(TeamTacticsStats.Avg(a.DepthWithBall, a.SamplesWithBall), 1));
            Row(sb, "  without ball", F(TeamTacticsStats.Avg(h.DepthWithoutBall, h.SamplesWithoutBall), 1), F(TeamTacticsStats.Avg(a.DepthWithoutBall, a.SamplesWithoutBall), 1));
            Row(sb, "def. line height m (all)", F(h.LineHeight, 1), F(a.LineHeight, 1));
            Row(sb, "  with ball", F(TeamTacticsStats.Avg(h.LineWithBall, h.SamplesWithBall), 1), F(TeamTacticsStats.Avg(a.LineWithBall, a.SamplesWithBall), 1));
            Row(sb, "  without ball", F(TeamTacticsStats.Avg(h.LineWithoutBall, h.SamplesWithoutBall), 1), F(TeamTacticsStats.Avg(a.LineWithoutBall, a.SamplesWithoutBall), 1));
            Row(sb, "shape samples", I(h.Samples), I(a.Samples));
            return sb.ToString();
        }

        private static void Row(StringBuilder sb, string label, string home, string away)
        {
            sb.Append(label.PadRight(30)).Append(home.PadLeft(10)).Append(away.PadLeft(10)).Append('\n');
        }

        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
        private static string F(float v, int dp) => v.ToString("F" + dp, CultureInfo.InvariantCulture);
        private static string P(float v) => (v * 100f).ToString("F1", CultureInfo.InvariantCulture);
    }
}
