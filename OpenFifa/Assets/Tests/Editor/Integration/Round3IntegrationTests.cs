using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Integration
{
    /// <summary>Regression tests for the third-round review of PR #5 (integration layer: control variants, animation timing).</summary>
    [TestFixture]
    [Category("FID_INTEGRATION")] // Unity NUnit rejects "-" in category names
    public class Round3IntegrationTests
    {
        private static MatchSim NewSim(ulong seed = 1) => new MatchSim(new MatchSimConfig(), MatchSetup.CreateDefault(seed));

        // ------------------------------------------------------------------ 9: control variants reach the sim

        private static readonly (ControlAction control, IntentAction act, KickStyle style)[] VariantCases =
        {
            (ControlAction.Pass, IntentAction.Pass, KickStyle.Default),
            (ControlAction.DrivenPass, IntentAction.Pass, KickStyle.Driven),
            (ControlAction.ThroughBall, IntentAction.ThroughPass, KickStyle.Default),
            (ControlAction.DrivenThrough, IntentAction.ThroughPass, KickStyle.Driven),
            (ControlAction.Lob, IntentAction.Lob, KickStyle.Default),
            (ControlAction.LobbedThrough, IntentAction.Lob, KickStyle.LobbedThrough),
            (ControlAction.DrivenLob, IntentAction.Lob, KickStyle.Driven),
            (ControlAction.Cross, IntentAction.Cross, KickStyle.Default),
            (ControlAction.Shoot, IntentAction.Shoot, KickStyle.Default),
            (ControlAction.ChipShot, IntentAction.Shoot, KickStyle.Chip),
            (ControlAction.LowShot, IntentAction.Shoot, KickStyle.Low),
            (ControlAction.FinesseShot, IntentAction.FinesseShot, KickStyle.Default),
            (ControlAction.DrivenShot, IntentAction.DrivenShot, KickStyle.Default),
        };

        [Test]
        public void HumanToIntent_ControlVariants_KeepActionAndStyle()
        {
            var sim = NewSim();
            var s = sim.State;
            s.Players[9].Position = new SimVec3(0f, 0f, 30f);
            s.Players[10].Position = new SimVec3(3f, 0f, 40f);
            var mates = new List<ControlPlayerInfo>
            {
                new ControlPlayerInfo(9, s.Players[9].Position), new ControlPlayerInfo(10, s.Players[10].Position),
            };
            foreach (var c in VariantCases)
            {
                var ci = new ControlIntent { Action = c.control, Power = 0.6f, Aim = new ControlVec2(0f, 1f) };
                var intent = HumanControlAdapter.ToIntent(ci, 0f, sim, 9, mates, 1f, 1f);
                Assert.AreEqual(c.act, intent.Action, c.control.ToString());
                Assert.AreEqual(c.style, intent.Style, c.control.ToString());
            }
        }

        [Test]
        public void HumanToIntent_NonKick_StyleDefault()
        {
            var sim = NewSim();
            var ci = new ControlIntent { Action = ControlAction.Tackle, Aim = new ControlVec2(0f, 1f) };
            Assert.AreEqual(KickStyle.Default, HumanControlAdapter.ToIntent(ci, 0f, sim, 9, null, 1f, 1f).Style);
        }

        // ------------------------------------------------------------------ 4: contact-synchronised touches and saves

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

        /// <summary>Steps the sim and the tracker, keeping what the tracker reported for <paramref name="watch"/> each tick.</summary>
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

            /// <summary>What was reported for the player <paramref name="ticksBefore"/> ticks before tick <paramref name="tick"/>.</summary>
            public SimAnimAction Before(int player, int tick, int ticksBefore) =>
                Reported[player].TryGetValue(tick - ticksBefore, out var a) ? a : default;
        }

        [Test]
        public void Tracker_DribbleTouch_AnimatedWithContactOnTheTouchTick()
        {
            var sim = NewSim(3);
            H.ForceOpenPlay(sim);
            H.ParkTeam(sim, SimTeam.Home, -HL + 1f);
            H.ParkTeam(sim, SimTeam.Away, HL - 1f);
            H.Place(sim, 9, 0f, -30f);
            sim.State.Owner = 9;
            sim.State.PossessionTeam = SimTeam.Home;
            H.SetBall(sim, new SimVec3(0f, 0.11f, -29.6f), SimVec3.Zero);
            var rec = new Recorder(sim);
            var intents = H.IdleIntents();
            intents[9] = PlayerIntent.MoveOnly(new SimVec3(0f, 0f, 1f));
            for (int t = 0; t < 360; t++) rec.Step(intents);

            int touches = 0;
            foreach (var e in rec.Events)
            {
                if (e.Type != MatchEventType.DribbleTouch || e.PlayerId != 9) continue;
                if (e.Tick < 60) continue; // let the run settle
                touches++;
                var early = rec.Before(9, e.Tick, 6);
                Assert.IsFalse(early.IsNone, $"touch at tick {e.Tick}: no action 6 ticks before the contact");
                Assert.AreEqual(AnimActionKind.FirstTouch, early.Kind);
                Assert.IsTrue(early.IsDribbleTouch);
                Assert.LessOrEqual(System.Math.Abs(early.ContactTick - e.Tick), 1, $"touch at tick {e.Tick}");
                var at = rec.Before(9, e.Tick, 0);
                Assert.AreEqual(early.Id, at.Id, "same action from the prediction to the contact");
                Assert.AreEqual(e.Tick, at.ContactTick, "contact snaps to the resolved tick");
                Assert.AreEqual(e.Tick * sim.Dt, at.ContactTime, 1e-5f);
            }

            Assert.GreaterOrEqual(touches, 5);
        }

        [Test]
        public void Tracker_FirstTouch_StartsBeforeContactAtThePredictedTick()
        {
            var sim = NewSim(4);
            H.ForceOpenPlay(sim);
            H.ParkTeam(sim, SimTeam.Home, -HL + 1f);
            H.ParkTeam(sim, SimTeam.Away, HL - 1f);
            H.Place(sim, 5, 0f, -30f);
            H.Place(sim, 9, 4f, -12f);
            SimVec3 aim = (sim.State.Players[9].Position - sim.State.Players[5].Position).Flat.Normalized;
            sim.State.Players[5].Facing = SimMath.Yaw(aim);
            sim.State.Owner = 5;
            sim.State.PossessionTeam = SimTeam.Home;
            H.SetBall(sim, sim.State.Players[5].Position + aim * 0.35f + new SimVec3(0f, 0.11f, 0f), SimVec3.Zero);
            var rec = new Recorder(sim);
            var intents = H.IdleIntents();
            intents[5] = PlayerIntent.Kick(IntentAction.Pass, 0.5f, aim, 9);
            for (int t = 0; t < 240; t++)
            {
                rec.Step(intents);
                intents[5] = PlayerIntent.Idle;
            }

            MatchEvent touch = default;
            bool found = false;
            foreach (var e in rec.Events)
                if (e.Type == MatchEventType.FirstTouch && e.PlayerId == 9) { touch = e; found = true; break; }
            Assert.IsTrue(found, "receiver never took the ball");
            var early = rec.Before(9, touch.Tick, 12);
            Assert.AreEqual(AnimActionKind.FirstTouch, early.Kind, "first touch must be announced ahead of the contact");
            Assert.IsFalse(early.IsDribbleTouch);
            Assert.LessOrEqual(System.Math.Abs(early.ContactTick - touch.Tick), 1);
            var at = rec.Before(9, touch.Tick, 0);
            Assert.AreEqual(early.Id, at.Id);
            Assert.AreEqual(touch.Tick, at.ContactTick);
        }

        [Test]
        public void Tracker_KeeperSave_DiveStartsBeforeContactAtTheSaveTick()
        {
            int saves = 0;
            for (ulong seed = 1; seed <= 8; seed++)
            {
                var cfg = new MatchSimConfig();
                cfg.Actions.ShotErrorDeg = 0f;
                var sim = new MatchSim(cfg, MatchSetup.CreateDefault(seed));
                H.ForceOpenPlay(sim);
                H.ParkTeam(sim, SimTeam.Home, -HL + 1f);
                H.ParkTeam(sim, SimTeam.Away, -20f);
                H.Place(sim, 11, 0.8f, HL - 1.5f);
                sim.State.Players[11].Facing = SimMath.Pi;
                H.Place(sim, 9, 0f, HL - 24.5f);
                sim.State.Players[9].Facing = 0f;
                sim.State.Owner = 9;
                sim.State.PossessionTeam = SimTeam.Home;
                H.SetBall(sim, new SimVec3(0f, 0.11f, HL - 24f), SimVec3.Zero);
                var rec = new Recorder(sim);
                var intents = H.IdleIntents();
                intents[9] = PlayerIntent.Kick(IntentAction.Shoot, 0.3f, new SimVec3(0f, 0f, 1f), -1, KickStyle.Low);
                for (int t = 0; t < 150; t++)
                {
                    rec.Step(intents);
                    intents[9] = PlayerIntent.Idle;
                }

                foreach (var e in rec.Events)
                {
                    if (e.Type != MatchEventType.Save) continue;
                    saves++;
                    var early = rec.Before(11, e.Tick, 10);
                    Assert.AreEqual(AnimActionKind.GoalkeeperDive, early.Kind, $"seed {seed}: dive must start before the save");
                    Assert.LessOrEqual(System.Math.Abs(early.ContactTick - e.Tick), 1, $"seed {seed}");
                    var at = rec.Before(11, e.Tick, 0);
                    Assert.AreEqual(early.Id, at.Id);
                    Assert.AreEqual(e.Tick, at.ContactTick);
                }
            }

            Assert.Greater(saves, 0, "no save in any seed: the scenario is broken");
        }
    }
}
