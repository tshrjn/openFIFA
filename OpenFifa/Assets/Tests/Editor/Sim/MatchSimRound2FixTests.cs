using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    /// <summary>Regression tests for the second-round review findings on feature/fidelity-core (match sim).</summary>
    [TestFixture]
    [Category("FID_SIM")]
    public class MatchSimRound2FixTests
    {
        private const float HL = 52.5f;
        private const float HW = 34f;

        // ---- (a) Pass lead uses the solver's real flight time. ----

        /// <summary>
        /// Home 5 passes to Home 9, who runs at a constant velocity (forced every tick). Error cones are off, so the
        /// only miss is the lead. Returns the closest the ball gets (while receivable, below control height) to the
        /// meeting spot: the receiver himself, or for a through ball the point <c>ThroughLeadDistance</c> ahead of
        /// him on his run (the ball is played into his path). Collecting the ball counts as arriving at him.
        /// </summary>
        private static float LeadMiss(ulong seed, IntentAction act, float power, float distance, SimVec3 run, out string info)
        {
            var cfg = new MatchSimConfig();
            cfg.Actions.PassErrorDeg = 0f;
            cfg.Actions.ThroughErrorDeg = 0f;
            cfg.Actions.LobErrorDeg = 0f;
            var sim = SimTestHelpers.NewSim(seed, cfg);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Home, -HL + 1f);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, HL - 1f);

            SimVec3 passer = new SimVec3(0f, 0f, -35f);
            SimVec3 q0 = new SimVec3(-6f, 0f, -35f + distance);
            SimTestHelpers.Place(sim, 5, passer.X, passer.Z);
            SimVec3 aim = (q0 - passer).Flat.Normalized;
            sim.State.Players[5].Facing = SimMath.Yaw(aim);
            sim.State.Owner = 5;
            sim.State.PossessionTeam = SimTeam.Home;
            SimTestHelpers.SetBall(sim, passer + aim * 0.35f + new SimVec3(0f, 0.11f, 0f), SimVec3.Zero);

            float dt = sim.Config.Dt;
            SimVec3 dir = run.Normalized;
            SimVec3 extra = act == IntentAction.ThroughPass ? dir * cfg.Actions.ThroughLeadDistance : SimVec3.Zero;
            var intents = SimTestHelpers.IdleIntents();
            intents[5] = PlayerIntent.Kick(act, power, aim, 9);
            bool kicked = false;
            float best = float.MaxValue;
            float bestT = -1f;
            int kickTick = -1;
            for (int t = 0; t < 1200; t++)
            {
                SimVec3 q = q0 + run * (t * dt);
                sim.State.Players[9].Position = q;
                sim.State.Players[9].Velocity = run;
                sim.State.Players[9].Facing = SimMath.Yaw(run);
                foreach (var e in sim.Step(intents))
                    if (e.Type == MatchEventType.Pass && e.PlayerId == 5) { kicked = true; kickTick = t; }
                intents[5] = PlayerIntent.Idle;
                if (!kicked) continue;

                SimVec3 meet = q0 + run * ((t + 1) * dt) + extra;
                var b = sim.State.Ball;
                if (b.Position.Y <= cfg.Actions.ControlMaxHeight)
                {
                    float d = SimMath.FlatDistance(b.Position, meet);
                    if (d < best) { best = d; bestT = (t - kickTick) * dt; }
                }

                if (sim.State.Owner == 9)
                {
                    // He collected it: the ball reached him (at most his control radius away).
                    float d = SimMath.FlatDistance(b.Position, sim.State.Players[9].Position);
                    if (d < best) { best = d; bestT = (t - kickTick) * dt; }
                    break;
                }

                if (sim.State.Owner >= 0 && sim.State.Owner != 5) break;
                if (b.Grounded && b.Velocity.SqrMagnitude < 0.01f) break;
            }

            info = $"{act} p={power} d={distance}: closest {best:F2} m at {bestT:F2} s after the kick, owner {sim.State.Owner}";
            Assert.IsTrue(kicked, "the pass must be played");
            return best;
        }

        [Test]
        public void Pass_ToRunningReceiver_ArrivesAtTheReceiver()
        {
            SimVec3 run = new SimVec3(4f, 0f, 1f);
            foreach (float d in new[] { 15f, 30f, 40f })
            foreach (float power in new[] { 0f, 1f })
            {
                float miss = LeadMiss(31, IntentAction.Pass, power, d, run, out string info);
                Assert.Less(miss, 1.5f, info);
            }
        }

        [Test]
        public void Pass_ToFastLateralRunner_ArrivesAtTheReceiver()
        {
            float miss = LeadMiss(32, IntentAction.Pass, 0.3f, 22f, new SimVec3(6.5f, 0f, 0f), out string info);
            Assert.Less(miss, 1.5f, info);
        }

        [Test]
        public void ThroughPass_ToRunningReceiver_ArrivesInHisPathAtTheLeadDistance()
        {
            SimVec3 run = new SimVec3(2f, 0f, 5f);
            foreach (float d in new[] { 15f, 30f })
            foreach (float power in new[] { 0f, 1f })
            {
                float miss = LeadMiss(33, IntentAction.ThroughPass, power, d, run, out string info);
                Assert.Less(miss, 1.5f, info);
            }
        }

        [Test]
        public void Lob_ToRunningReceiver_LandsAtTheReceiver()
        {
            float miss = LeadMiss(34, IntentAction.Lob, 0.5f, 30f, new SimVec3(4f, 0f, 1f), out string info);
            Assert.Less(miss, 1.5f, info);
        }

        // ---- (c) A pass deflected off an opponent is no longer a live pass. ----

        private static MatchSim PassIntoBlocker(int blocker, out PlayerIntent[] intents)
        {
            var sim = SimTestHelpers.NewSim(41);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Home, -45f);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, 45f);
            SimTestHelpers.Place(sim, 11, 0f, HL - 1f); // away keeper, deepest
            SimTestHelpers.Place(sim, 5, 0f, 8f);       // passer
            SimTestHelpers.Place(sim, blocker, 0f, 20f);
            sim.State.Players[blocker].ControlBlockedUntilTick = 100000; // cannot trap it: the ball hits his body
            var s = sim.State;
            var v = new SimVec3(0f, 0f, 12f);
            s.Ball = new SimBall { Position = new SimVec3(0f, 0.11f, 10f), Velocity = v,
                Spin = BallIntegrator.RollingSpin(v, 0.11f, 0f), Grounded = true };
            s.LastTouchPlayer = 5;
            s.LastTouchTeam = SimTeam.Home;
            s.PossessionTeam = SimTeam.Home;
            s.KickSerial = 1;
            s.PassLive = true;
            s.PassFrom = 5;
            s.PassTarget = 9;
            s.PassTeam = SimTeam.Home;
            s.AssistCandidate = 7; // passed to 5 earlier
            intents = SimTestHelpers.IdleIntents();
            return sim;
        }

        [Test]
        public void Deflection_PassOffOpponent_PassNoLongerLiveAndAssistCleared()
        {
            var sim = PassIntoBlocker(16, out var intents);
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.Deflection, 120, out var e));
            Assert.AreEqual(16, e.PlayerId);
            Assert.IsFalse(sim.State.PassLive, "a pass deflected off an opponent is not a completed pass when a teammate picks it up");
            Assert.AreEqual(-1, sim.State.AssistCandidate, "no assist through an opponent's deflection");

            // A home player collecting the rebound is not credited with a completed pass from 5.
            SimTestHelpers.Place(sim, 9, sim.State.Ball.Position.X, sim.State.Ball.Position.Z - 0.4f);
            bool completed = false;
            for (int t = 0; t < 60 && sim.State.Owner < 0; t++)
                foreach (var ev in sim.Step(intents)) if (ev.Type == MatchEventType.PassCompleted) completed = true;
            Assert.AreEqual(9, sim.State.Owner);
            Assert.IsFalse(completed);
        }

        [Test]
        public void Deflection_PassOffTeammate_PassStaysLive()
        {
            var sim = PassIntoBlocker(7, out var intents);
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.Deflection, 120, out var e));
            Assert.AreEqual(7, e.PlayerId);
            Assert.IsTrue(sim.State.PassLive);
        }

        // ---- (d) A deflection off a teammate re-evaluates offside, as a new pass by that teammate. ----

        [Test]
        public void Deflection_OffTeammate_ReevaluatesOffsideFromTheDeflection()
        {
            var sim = PassIntoBlocker(7, out var intents);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, 30f); // defensive line at z = 30
            SimTestHelpers.Place(sim, 11, 0f, HL - 1f);
            SimTestHelpers.Place(sim, 9, 6f, 36f);            // ran beyond the line after the pass was played
            sim.State.OffsideMask = 0;                        // onside when 5 played it
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.Deflection, 120, out var e));
            Assert.AreEqual(7, e.PlayerId);
            Assert.AreNotEqual(0, sim.State.OffsideMask & (1 << 9), "9 is offside relative to the deflection by 7");
            Assert.AreEqual(7, sim.State.OffsidePasser);
        }

        [Test]
        public void Deflection_OffTeammate_ClearsOffsideOfPlayerNowOnside()
        {
            var sim = PassIntoBlocker(7, out var intents);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, 30f);
            SimTestHelpers.Place(sim, 11, 0f, HL - 1f);
            SimTestHelpers.Place(sim, 9, 6f, 25f);            // came back onside
            sim.State.OffsideMask = 1 << 9;                    // offside when 5 played it
            sim.State.OffsidePasser = 5;
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.Deflection, 120, out _));
            Assert.AreEqual(0, sim.State.OffsideMask & (1 << 9));
        }

        [Test]
        public void Deflection_OffTeammateInOffsidePosition_OffsideCalled()
        {
            var sim = PassIntoBlocker(7, out var intents);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, 15f);  // 7 at z = 20 is beyond the line
            SimTestHelpers.Place(sim, 11, 0f, HL - 1f);
            sim.State.OffsideMask = 1 << 7;
            sim.State.OffsidePositions[7] = sim.State.Players[7].Position;
            sim.State.OffsideMargins[7] = 5f;
            sim.State.OffsidePasser = 5;
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.Offside, 120, out var e));
            Assert.AreEqual(7, e.PlayerId);
            Assert.AreEqual(RestartType.IndirectFreeKick, sim.State.PendingRestart);
        }

        // ---- (e) Separation never pushes a player out of the playing area. ----

        [Test]
        public void Players_OverlappingAtTheRunOffEdge_StayInsideTheArea()
        {
            var sim = SimTestHelpers.NewSim(51);
            SimTestHelpers.ForceOpenPlay(sim);
            float mx = HW + 4f, mz = HL + 4f;
            SimTestHelpers.Place(sim, 3, mx, 0f);
            SimTestHelpers.Place(sim, 15, mx, 0f);
            SimTestHelpers.Place(sim, 4, -mx + 0.1f, mz);
            SimTestHelpers.Place(sim, 16, -mx, mz - 0.1f);
            SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, 0f), SimVec3.Zero);
            sim.Step(SimTestHelpers.IdleIntents());
            foreach (int id in new[] { 3, 15, 4, 16 })
            {
                SimVec3 p = sim.State.Players[id].Position;
                Assert.LessOrEqual(SimMath.Abs(p.X), mx + 1e-4f, $"player {id} x");
                Assert.LessOrEqual(SimMath.Abs(p.Z), mz + 1e-4f, $"player {id} z");
            }
        }

        // ---- (14) Simultaneous contacts do not always favour one team. ----

        [Test]
        public void Contacts_SimultaneousKicksOnOneBall_WinnerNotFixedByPlayerId()
        {
            int home = 0, away = 0;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var sim = SimTestHelpers.NewSim(seed);
                SimTestHelpers.ForceOpenPlay(sim);
                SimTestHelpers.ParkTeam(sim, SimTeam.Home, -45f);
                SimTestHelpers.ParkTeam(sim, SimTeam.Away, 45f);
                SimTestHelpers.Place(sim, 5, 0f, -0.5f);
                SimTestHelpers.Place(sim, 16, 0f, 0.5f);
                sim.State.Players[5].Facing = 0f;
                sim.State.Players[16].Facing = SimMath.Pi;
                SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, 0f), SimVec3.Zero);
                var intents = SimTestHelpers.IdleIntents();
                intents[5] = PlayerIntent.Kick(IntentAction.Pass, 0.5f, new SimVec3(0f, 0f, 1f));
                intents[16] = PlayerIntent.Kick(IntentAction.Pass, 0.5f, new SimVec3(0f, 0f, -1f));
                sim.Step(intents);
                intents[5] = PlayerIntent.Idle;
                intents[16] = PlayerIntent.Idle;
                Assert.AreEqual(sim.State.Players[5].ContactTick, sim.State.Players[16].ContactTick, "same contact frame");
                // Both strike on the same frame; the kick resolved last is the one the ball leaves with.
                int lastKicker = -1;
                for (int t = 0; t < 60 && lastKicker < 0; t++)
                    foreach (var e in sim.Step(intents))
                        if (e.Type == MatchEventType.Pass) lastKicker = e.PlayerId;
                if (lastKicker == 5) home++; else if (lastKicker == 16) away++;
            }

            Assert.Greater(home, 8, $"home won {home}/40");
            Assert.Greater(away, 8, $"away won {away}/40");
        }

        [Test]
        public void Contacts_SimultaneousKicks_SameSeedSameWinner()
        {
            float RunOnce()
            {
                var sim = SimTestHelpers.NewSim(77);
                SimTestHelpers.ForceOpenPlay(sim);
                SimTestHelpers.Place(sim, 5, 0f, -0.5f);
                SimTestHelpers.Place(sim, 16, 0f, 0.5f);
                SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, 0f), SimVec3.Zero);
                var intents = SimTestHelpers.IdleIntents();
                intents[5] = PlayerIntent.Kick(IntentAction.Pass, 0.5f, new SimVec3(0f, 0f, 1f));
                intents[16] = PlayerIntent.Kick(IntentAction.Pass, 0.5f, new SimVec3(0f, 0f, -1f));
                float order = 0f;
                for (int t = 0; t < 30; t++)
                {
                    foreach (var e in sim.Step(intents)) if (e.Type == MatchEventType.Pass) order = order * 100f + e.PlayerId;
                    intents[5] = intents[16] = PlayerIntent.Idle;
                }

                return order * 1000f + sim.State.Ball.Velocity.Z;
            }

            Assert.AreEqual(RunOnce(), RunOnce());
        }
    }
}
