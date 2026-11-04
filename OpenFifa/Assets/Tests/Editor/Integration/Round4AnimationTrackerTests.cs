using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Integration
{
    /// <summary>Regression tests for the fourth-round review: dribble-touch prediction timing and touch hold.</summary>
    [TestFixture]
    [Category("FID_INTEGRATION")] // Unity NUnit rejects "-" in category names
    public class Round4AnimationTrackerTests
    {
        private const float HL = 52.5f;

        /// <summary>Scenario helpers (kept local so this file compiles with the Integration tests alone).</summary>
        private static class H
        {
            public static PlayerIntent[] IdleIntents()
            {
                var a = new PlayerIntent[MatchSimState.PlayerCount];
                for (int i = 0; i < a.Length; i++) a[i] = PlayerIntent.Idle;
                return a;
            }

            public static void ForceOpenPlay(MatchSim sim)
            {
                var s = sim.State;
                s.Phase = SimMatchPhase.InPlay;
                s.PendingRestart = RestartType.None;
                s.RestartTakerId = -1;
                s.Owner = -1;
                s.KeeperHolding = false;
                s.PassLive = false;
                s.ShotLive = false;
                s.OffsideMask = 0;
            }

            public static void Place(MatchSim sim, int id, float x, float z)
            {
                sim.State.Players[id].Position = new SimVec3(x, 0f, z);
                sim.State.Players[id].Velocity = SimVec3.Zero;
            }

            public static void ParkTeam(MatchSim sim, SimTeam team, float z)
            {
                int first = MatchSim.FirstId(team);
                for (int i = 0; i < MatchSetup.PlayersPerTeam; i++) Place(sim, first + i, -30f + i * 2f, z);
            }

            public static void SetBall(MatchSim sim, SimVec3 pos, SimVec3 vel)
            {
                var b = sim.State.Ball;
                b.Position = pos;
                b.Velocity = vel;
                b.Spin = SimVec3.Zero;
                b.InNet = 0;
                b.Grounded = pos.Y <= 0.111f && vel.Y <= 0f;
                sim.State.Ball = b;
            }
        }

        /// <summary>Steps the sim and the tracker, keeping what the tracker reported for every player each tick.</summary>
        private sealed class Recorder
        {
            public readonly MatchSim Sim;
            public readonly SimAnimActionTracker Tracker = new SimAnimActionTracker();
            public readonly Dictionary<int, SimAnimAction>[] Reported = new Dictionary<int, SimAnimAction>[MatchSimState.PlayerCount];
            public readonly List<MatchEvent> Events = new List<MatchEvent>();

            public Recorder(MatchSim sim)
            {
                Sim = sim;
                for (int i = 0; i < Reported.Length; i++) Reported[i] = new Dictionary<int, SimAnimAction>();
            }

            public void Step(PlayerIntent[] intents)
            {
                var ev = Sim.Step(intents);
                Tracker.Update(Sim, ev);
                for (int i = 0; i < ev.Count; i++) Events.Add(ev[i]);
                for (int i = 0; i < MatchSimState.PlayerCount; i++) Reported[i][Sim.State.Tick] = Tracker.Get(i);
            }

            /// <summary>Earliest tick at which action <paramref name="id"/> was reported for the player, and that report.</summary>
            public bool FirstReport(int player, int id, out int tick, out SimAnimAction a)
            {
                tick = int.MaxValue;
                a = default;
                foreach (var kv in Reported[player])
                {
                    if (kv.Value.Id != id || kv.Key >= tick) continue;
                    tick = kv.Key;
                    a = kv.Value;
                }
                return tick != int.MaxValue;
            }
        }

        private static Recorder SprintDribble(ulong seed, int dribbling, bool sprint, int ticks)
        {
            var setup = MatchSetup.CreateDefault(seed);
            setup.Home[9].Dribbling = dribbling;
            var sim = new MatchSim(new MatchSimConfig(), setup);
            H.ForceOpenPlay(sim);
            H.ParkTeam(sim, SimTeam.Home, -HL + 1f);
            H.ParkTeam(sim, SimTeam.Away, HL - 1f);
            H.Place(sim, 9, 0f, -40f);
            sim.State.Owner = 9;
            sim.State.PossessionTeam = SimTeam.Home;
            H.SetBall(sim, new SimVec3(0f, 0.11f, -39.6f), SimVec3.Zero);
            var rec = new Recorder(sim);
            var intents = H.IdleIntents();
            intents[9] = PlayerIntent.MoveOnly(new SimVec3(0f, 0f, 1f), sprint);
            for (int t = 0; t < ticks; t++) rec.Step(intents);
            return rec;
        }

        /// <summary>
        /// Item 1: while sprinting the ball runs 1.4-3 m ahead, so the touch is not taken at NextTouchTick but on the first
        /// tick after it at which the player has caught up to within 1.3 x ControlRadius. The very first prediction of
        /// every touch must already name that tick (not NextTouchTick), and the foot target must be within reach.
        /// </summary>
        [Test]
        public void Tracker_SprintDribble_FirstPredictionNamesTheRealTouchTickWithinReach()
        {
            int touches = 0, maxErr = 0;
            float maxGap = 0f;
            foreach (int dribbling in new[] { 70, 80, 95 })
            {
                var rec = SprintDribble(7, dribbling, true, 480);
                float reach = rec.Sim.Config.Actions.ControlRadius * MatchSim.DribbleCatchUpFactor;
                foreach (var e in rec.Events)
                {
                    if (e.Type != MatchEventType.DribbleTouch || e.PlayerId != 9 || e.Tick < 90) continue;
                    var at = rec.Reported[9][e.Tick];
                    Assert.AreEqual(e.Tick, at.ContactTick, "contact snaps to the resolved tick");
                    Assert.IsTrue(rec.FirstReport(9, at.Id, out int firstTick, out var first));
                    if (firstTick >= e.Tick) continue; // resolved without a prediction (sharp-turn retouch)
                    touches++;
                    int err = System.Math.Abs(first.ContactTick - e.Tick);
                    if (err > maxErr) maxErr = err;
                    float gap = (first.BallAtContact - first.RootAtContact).Flat.Magnitude;
                    if (gap > maxGap) maxGap = gap;
                    Assert.LessOrEqual(err, 1,
                        $"dribbling {dribbling}: touch at tick {e.Tick} first predicted at tick {firstTick} for tick {first.ContactTick}");
                    Assert.LessOrEqual(gap, reach + 0.25f,
                        $"dribbling {dribbling}: touch at tick {e.Tick}: predicted foot target {gap:F2} m from the predicted root");
                }
            }

            Assert.GreaterOrEqual(touches, 10, "too few predicted touches: the scenario is broken");
            try { TestContext.WriteLine($"touches {touches}, max tick error {maxErr}, max root-ball gap {maxGap:F2} m"); } catch (System.NullReferenceException) { }
        }

        [Test]
        public void Tracker_JogDribble_PredictionStillWithinOneTick()
        {
            int touches = 0;
            var rec = SprintDribble(3, 70, false, 360);
            foreach (var e in rec.Events)
            {
                if (e.Type != MatchEventType.DribbleTouch || e.PlayerId != 9 || e.Tick < 60) continue;
                var at = rec.Reported[9][e.Tick];
                if (!rec.FirstReport(9, at.Id, out int firstTick, out var first) || firstTick >= e.Tick) continue;
                touches++;
                Assert.LessOrEqual(System.Math.Abs(first.ContactTick - e.Tick), 1, $"touch at tick {e.Tick}");
            }
            Assert.GreaterOrEqual(touches, 5);
        }

        /// <summary>
        /// Item 2: the next touch's prediction (a new id) must not replace a resolved touch while its IK is still blending
        /// out, plus one tick of presentation lag (the driver renders up to one tick behind the sim).
        /// </summary>
        [Test]
        public void TouchHold_CoversTheIkBlendOutPlusOneTick()
        {
            Assert.GreaterOrEqual(SimAnimActionTracker.TouchHold, DribbleTouchSolver.BlendOut + 1f / 60f - 1e-6f);
        }

        [Test]
        public void Tracker_SprintDribble_NextTouchIdNotReportedBeforePreviousBlendOutEnds()
        {
            var rec = SprintDribble(11, 85, true, 480);
            float dt = rec.Sim.Dt;
            int lastId = 0;
            float lastContact = float.NegativeInfinity;
            bool lastResolved = false;
            int switches = 0;
            for (int tick = 1; tick <= rec.Sim.State.Tick; tick++)
            {
                var a = rec.Reported[9][tick];
                if (a.IsNone || !a.IsDribbleTouch) continue;
                if (a.Id != lastId)
                {
                    if (lastResolved)
                    {
                        switches++;
                        Assert.GreaterOrEqual(tick * dt, lastContact + DribbleTouchSolver.BlendOut + dt - 1e-4f,
                            $"tick {tick}: touch {a.Id} replaced touch {lastId} (contact {lastContact:F3}s) while it was still blending out");
                    }
                    lastId = a.Id;
                }
                lastContact = a.ContactTime;
                lastResolved = a.ContactTick <= tick;
            }
            Assert.GreaterOrEqual(switches, 5);
        }
    }
}
