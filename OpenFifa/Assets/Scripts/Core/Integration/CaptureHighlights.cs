using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenFifa.Core
{
    /// <summary>Kinds of ball moment the review capture films as a frame sequence, in order of preference.</summary>
    public enum CaptureHighlightKind
    {
        /// <summary>A pass of at least <see cref="CaptureHighlights.LongPassMetres"/> to its receiver, or a lob or cross.</summary>
        LongPass = 0,
        Shot = 1,
        /// <summary>A falling ball hitting the turf and bouncing up.</summary>
        Bounce = 2,
        /// <summary>A ball in the air settling into a roll without a visible bounce.</summary>
        Roll = 3,
        Goal = 4,
    }

    /// <summary>One candidate moment. Metric: pass distance (m), shot xG, bounce impact speed (m/s) or roll speed (m/s).</summary>
    public struct CaptureHighlight
    {
        public CaptureHighlightKind Kind;
        public int Tick;
        public int Half;
        public float Clock;
        public int PlayerId;
        public int OtherPlayerId;
        public SimTeam Team;
        public SimVec3 Position;
        public float Metric;
        public IntentAction Action;
        public KickStyle Style;
    }

    /// <summary>
    /// Collects ball moments from a headless sim run (long passes, shots, bounces, rolls, goals) and picks a few that fit
    /// a recorded window, for the review capture's sequences/ (8-12 frames at ~10 fps around each). Bounces and rolls
    /// are not sim events: they are read from the ball state tick to tick. Call <see cref="Observe"/> after every step.
    /// </summary>
    public sealed class CaptureHighlights
    {
        public const float LongPassMetres = 25f;
        /// <summary>Downward speed (m/s) a bounce needs to count (a visible hop, not a skid).</summary>
        public const float MinBounceSpeed = 2f;
        /// <summary>Seconds in the air before landing counts as a roll.</summary>
        public const float MinRollAirSeconds = 0.3f;
        /// <summary>Seconds after one bounce or roll before another is recorded (one per flight).</summary>
        public const float ContactCooldownSeconds = 1f;

        private readonly List<CaptureHighlight> _all;
        private SimBall _prevBall;
        private bool _hasPrev;
        private int _airTicks;
        private int _lastContactTick = int.MinValue / 2;

        public CaptureHighlights(int capacity = 1024)
        {
            _all = new List<CaptureHighlight>(capacity);
        }

        public IReadOnlyList<CaptureHighlight> All => _all;

        public void Observe(MatchSim sim, IReadOnlyList<MatchEvent> events)
        {
            var s = sim.State;
            bool kicked = false;
            if (events != null)
            {
                for (int i = 0; i < events.Count; i++)
                {
                    var e = events[i];
                    switch (e.Type)
                    {
                        case MatchEventType.Pass:
                        {
                            kicked = true;
                            float dist = 0f;
                            if (e.OtherPlayerId >= 0)
                            {
                                var r = s.Players[e.OtherPlayerId].Position;
                                dist = new SimVec3(r.X - e.Position.X, 0f, r.Z - e.Position.Z).Magnitude;
                            }
                            bool lofted = e.Action == IntentAction.Lob || e.Action == IntentAction.Cross;
                            if (dist >= LongPassMetres || lofted)
                                Add(CaptureHighlightKind.LongPass, e, dist);
                            break;
                        }
                        case MatchEventType.Shot:
                            kicked = true;
                            Add(CaptureHighlightKind.Shot, e, e.Value);
                            break;
                        case MatchEventType.Goal:
                            Add(CaptureHighlightKind.Goal, e, e.Speed);
                            break;
                        case MatchEventType.Deflection:
                        case MatchEventType.Save:
                            kicked = true; // the ball changed direction off a body, not the turf
                            break;
                    }
                }
            }

            var b = s.Ball;
            float r0 = sim.Config.Ball.Radius;
            if (_hasPrev && s.Phase == SimMatchPhase.InPlay && !kicked && b.InNet == 0 &&
                s.Tick - _lastContactTick >= sim.Config.SecondsToTicks(ContactCooldownSeconds))
            {
                if (_prevBall.Velocity.Y < -MinBounceSpeed && b.Velocity.Y > 0f && b.Position.Y < r0 + 0.4f)
                    AddContact(CaptureHighlightKind.Bounce, s, -_prevBall.Velocity.Y);
                else if (!_prevBall.Grounded && b.Grounded && _airTicks >= sim.Config.SecondsToTicks(MinRollAirSeconds))
                    AddContact(CaptureHighlightKind.Roll, s, b.Velocity.Flat.Magnitude);
            }
            _airTicks = b.Grounded ? 0 : _airTicks + 1;
            _prevBall = b;
            _hasPrev = true;
        }

        private void Add(CaptureHighlightKind kind, in MatchEvent e, float metric)
        {
            _all.Add(new CaptureHighlight
            {
                Kind = kind, Tick = e.Tick, Half = e.Half, Clock = e.Time, PlayerId = e.PlayerId,
                OtherPlayerId = e.OtherPlayerId, Team = e.Team, Position = e.Position, Metric = metric,
                Action = e.Action, Style = e.Style,
            });
        }

        private void AddContact(CaptureHighlightKind kind, MatchSimState s, float metric)
        {
            _lastContactTick = s.Tick;
            int last = s.LastTouchPlayer;
            _all.Add(new CaptureHighlight
            {
                Kind = kind, Tick = s.Tick, Half = s.Half, Clock = s.Clock, PlayerId = last, OtherPlayerId = -1,
                Team = s.LastTouchTeam, Position = s.Ball.Position, Metric = metric,
            });
        }

        /// <summary>
        /// Picks up to <paramref name="max"/> moments whose sequence (<paramref name="before"/> ticks before to
        /// <paramref name="after"/> ticks after) fits in [<paramref name="windowStart"/>, <paramref name="windowEnd"/>],
        /// without overlapping: first the best long pass (longest), shot (highest xG) and bounce (hardest, else a roll),
        /// then any others by kind and metric. The result is in tick order.
        /// </summary>
        public static void Pick(IReadOnlyList<CaptureHighlight> all, int windowStart, int windowEnd, int before, int after,
            int max, List<CaptureHighlight> result)
        {
            result.Clear();
            if (all == null || max <= 0) return;
            int gap = before + after + 1;
            // Preferred one per category: long pass, shot, bounce-or-roll.
            for (int cat = 0; cat < 3 && result.Count < max; cat++)
            {
                int best = -1;
                for (int i = 0; i < all.Count; i++)
                {
                    var c = all[i];
                    if (!Fits(c, windowStart, windowEnd, before, after) || Overlaps(c, result, gap)) continue;
                    if (Category(c.Kind) != cat) continue;
                    if (best < 0 || Better(c, all[best])) best = i;
                }
                if (best >= 0) result.Add(all[best]);
            }
            // Fill: any kind (goal last), best first.
            while (result.Count < max)
            {
                int best = -1;
                for (int i = 0; i < all.Count; i++)
                {
                    var c = all[i];
                    if (!Fits(c, windowStart, windowEnd, before, after) || Overlaps(c, result, gap)) continue;
                    if (best < 0 || c.Kind < all[best].Kind || (c.Kind == all[best].Kind && Better(c, all[best]))) best = i;
                }
                if (best < 0) break;
                result.Add(all[best]);
            }
            // Tick order (n <= max: insertion sort).
            for (int i = 1; i < result.Count; i++)
            {
                var v = result[i];
                int j = i - 1;
                while (j >= 0 && result[j].Tick > v.Tick) { result[j + 1] = result[j]; j--; }
                result[j + 1] = v;
            }
        }

        private static int Category(CaptureHighlightKind k) =>
            k == CaptureHighlightKind.LongPass ? 0 : k == CaptureHighlightKind.Shot ? 1 :
            (k == CaptureHighlightKind.Bounce || k == CaptureHighlightKind.Roll) ? 2 : 3;

        /// <summary>Bounce beats roll; otherwise the larger metric, then the earlier tick.</summary>
        private static bool Better(in CaptureHighlight a, in CaptureHighlight b)
        {
            if (a.Kind != b.Kind) return a.Kind < b.Kind;
            if (a.Metric != b.Metric) return a.Metric > b.Metric;
            return a.Tick < b.Tick;
        }

        private static bool Fits(in CaptureHighlight c, int start, int end, int before, int after) =>
            c.Tick - before >= start && c.Tick + after <= end;

        private static bool Overlaps(in CaptureHighlight c, List<CaptureHighlight> chosen, int gap)
        {
            for (int i = 0; i < chosen.Count; i++)
            {
                int d = c.Tick - chosen[i].Tick;
                if (d < 0) d = -d;
                if (d < gap) return true;
            }
            return false;
        }

        /// <summary>
        /// One sequences.txt line: "seqA | LongPass 31.4 m (Lob) | tick 4210 | 23:41.2 | Saka (ARS) -> Havertz (ARS)".
        /// <paramref name="names"/> is indexed by sim player id (may be null).
        /// </summary>
        public static string Describe(string label, in CaptureHighlight h, float halfDurationSeconds, string[] names,
            string homeShort, string awayShort)
        {
            var sb = new StringBuilder(128);
            sb.Append(label).Append(" | ").Append(h.Kind);
            switch (h.Kind)
            {
                case CaptureHighlightKind.LongPass:
                    sb.Append(' ').Append(h.Metric.ToString("F1", CultureInfo.InvariantCulture)).Append(" m");
                    break;
                case CaptureHighlightKind.Shot:
                    sb.Append(" xG ").Append(h.Metric.ToString("F2", CultureInfo.InvariantCulture));
                    break;
                case CaptureHighlightKind.Bounce:
                    sb.Append(" impact ").Append(h.Metric.ToString("F1", CultureInfo.InvariantCulture)).Append(" m/s");
                    break;
                case CaptureHighlightKind.Roll:
                    sb.Append(" rolling ").Append(h.Metric.ToString("F1", CultureInfo.InvariantCulture)).Append(" m/s");
                    break;
            }
            if (h.Action != IntentAction.None) sb.Append(" (").Append(h.Action).Append(h.Style != KickStyle.Default ? "/" + h.Style : "").Append(')');
            sb.Append(" | tick ").Append(h.Tick).Append(" | ").Append(CaptureText.Clock(h.Clock, h.Half, halfDurationSeconds));
            sb.Append(" | ").Append(PlayerLabel(h.PlayerId, names, homeShort, awayShort));
            if (h.OtherPlayerId >= 0)
                sb.Append(h.Kind == CaptureHighlightKind.Goal ? " assisted by " : " -> ").Append(PlayerLabel(h.OtherPlayerId, names, homeShort, awayShort));
            return sb.ToString();
        }

        /// <summary>"Name (SHT)" for a sim id, "#id (SHT)" without names, "nobody" for -1.</summary>
        public static string PlayerLabel(int id, string[] names, string homeShort, string awayShort)
        {
            if (id < 0 || id >= MatchSimState.PlayerCount) return "nobody";
            string team = MatchSim.TeamOf(id) == SimTeam.Home ? homeShort : awayShort;
            string name = names != null && id < names.Length && !string.IsNullOrEmpty(names[id]) ? names[id] : "#" + id;
            return name + " (" + team + ")";
        }
    }
}
