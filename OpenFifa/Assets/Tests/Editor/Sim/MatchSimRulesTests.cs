using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    [TestFixture]
    [Category("FID_SIM")]
    public class MatchSimRulesTests
    {
        private const float HL = 52.5f;
        private const float HW = 34f;

        [Test]
        public void Constructor_Default_AwaitsHomeKickOffWithEveryoneInOwnHalf()
        {
            var sim = SimTestHelpers.NewSim(1);
            Assert.AreEqual(SimMatchPhase.AwaitingRestart, sim.Phase);
            Assert.AreEqual(RestartType.KickOff, sim.State.PendingRestart);
            Assert.AreEqual(10, sim.State.RestartTakerId);
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                var p = sim.State.Players[i];
                if (i == sim.State.RestartTakerId) continue;
                Assert.LessOrEqual(sim.AttackCoord(p.Team, p.Position), 0f, $"player {i}");
            }
        }

        [Test]
        public void Step_KickOffPass_EmitsWindupThenKickOffAtContactTick()
        {
            var sim = SimTestHelpers.NewSim(1);
            var intents = SimTestHelpers.IdleIntents();
            intents[10] = PlayerIntent.Kick(IntentAction.Pass, 0.3f, new SimVec3(-1f, 0f, -0.3f));
            var ev = sim.Step(intents);
            Assert.AreEqual(MatchEventType.KickWindup, ev[0].Type);
            int contact = ev[0].Detail;
            intents[10] = PlayerIntent.Idle;
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.KickOff, 60, out var ko));
            Assert.AreEqual(contact, ko.Tick, "the ball must leave the foot exactly on the announced contact frame");
            Assert.AreEqual(SimMatchPhase.InPlay, sim.Phase);
        }

        [Test]
        public void Step_BallOverTouchline_ThrowInToOpponentOfLastToucher()
        {
            var sim = SimTestHelpers.NewSim(2);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.SetBall(sim, new SimVec3(HW - 3f, 0.11f, 5f), new SimVec3(12f, 0f, 0f));
            sim.State.LastTouchPlayer = 4;
            sim.State.LastTouchTeam = SimTeam.Home;
            var intents = SimTestHelpers.IdleIntents();
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.ThrowIn, 120, out var e));
            Assert.AreEqual(SimTeam.Away, e.Team);
            Assert.AreEqual(HW, e.Position.X, 1e-3f);
            // After the dead-ball pause an away player stands at the ball.
            for (int i = 0; i < 90; i++) sim.Step(intents);
            Assert.AreEqual(SimMatchPhase.AwaitingRestart, sim.Phase);
            Assert.AreEqual(SimTeam.Away, MatchSim.TeamOf(sim.State.RestartTakerId));
        }

        [Test]
        public void Step_AttackerPlaysBallOverGoalLine_GoalKick()
        {
            var sim = SimTestHelpers.NewSim(3);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.SetBall(sim, new SimVec3(15f, 0.11f, HL - 3f), new SimVec3(0f, 0f, 12f));
            sim.State.LastTouchPlayer = 9;
            sim.State.LastTouchTeam = SimTeam.Home; // home attacks +Z
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, SimTestHelpers.IdleIntents(), MatchEventType.GoalKick, 120, out var e));
            Assert.AreEqual(SimTeam.Away, e.Team);
        }

        [Test]
        public void Step_DefenderPlaysBallOverOwnGoalLine_Corner()
        {
            var sim = SimTestHelpers.NewSim(4);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.SetBall(sim, new SimVec3(-15f, 0.11f, HL - 3f), new SimVec3(0f, 0f, 12f));
            sim.State.LastTouchPlayer = 13;
            sim.State.LastTouchTeam = SimTeam.Away; // away defends +Z
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, SimTestHelpers.IdleIntents(), MatchEventType.Corner, 120, out var e));
            Assert.AreEqual(SimTeam.Home, e.Team);
            Assert.Less(e.Position.X, 0f, "corner on the side the ball went out");
        }

        [Test]
        public void Step_ShotIntoNet_GoalScoredAndConcedingTeamKicksOff()
        {
            var sim = SimTestHelpers.NewSim(5);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.Place(sim, 11, 20f, HL - 2f); // keeper out of the way
            SimTestHelpers.SetBall(sim, new SimVec3(0f, 1f, HL - 8f), new SimVec3(0f, 0f, 25f));
            sim.State.LastTouchPlayer = 9;
            sim.State.LastTouchTeam = SimTeam.Home;
            var intents = SimTestHelpers.IdleIntents();
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.Goal, 120, out var g));
            Assert.AreEqual(SimTeam.Home, g.Team);
            Assert.AreEqual(9, g.PlayerId);
            Assert.AreEqual(0, g.Detail, "not an own goal");
            Assert.AreEqual(1, sim.State.HomeScore);
            for (int i = 0; i < 200; i++) sim.Step(intents);
            Assert.AreEqual(SimMatchPhase.AwaitingRestart, sim.Phase);
            Assert.AreEqual(RestartType.KickOff, sim.State.PendingRestart);
            Assert.AreEqual(SimTeam.Away, sim.State.RestartTeam);
        }

        [Test]
        public void Step_DefenderTouchesIntoOwnNet_OwnGoalFlagged()
        {
            var sim = SimTestHelpers.NewSim(6);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.Place(sim, 11, 20f, HL - 2f);
            SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.5f, HL - 5f), new SimVec3(0f, 0f, 20f));
            sim.State.LastTouchPlayer = 14;
            sim.State.LastTouchTeam = SimTeam.Away;
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, SimTestHelpers.IdleIntents(), MatchEventType.Goal, 120, out var g));
            Assert.AreEqual(SimTeam.Home, g.Team);
            Assert.AreEqual(1, g.Detail);
        }

        private static MatchSim OffsideScenario(float receiverZ, out PlayerIntent[] intents)
        {
            var sim = SimTestHelpers.NewSim(7);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Home, -40f);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, 30f); // the defensive line is at z = 30
            SimTestHelpers.Place(sim, 11, 0f, 50f);           // keeper deepest
            SimTestHelpers.Place(sim, 5, 0f, 10f);            // passer
            SimTestHelpers.Place(sim, 9, 3f, receiverZ);      // receiver
            sim.State.Owner = 5;
            sim.State.PossessionTeam = SimTeam.Home;
            SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, 10.5f), SimVec3.Zero);
            intents = SimTestHelpers.IdleIntents();
            intents[5] = PlayerIntent.Kick(IntentAction.Pass, 0.6f, new SimVec3(0.1f, 0f, 1f), 9);
            return sim;
        }

        [Test]
        public void Step_PassToPlayerBeyondLastDefender_OffsideCalledWithIndirectFreeKick()
        {
            var sim = OffsideScenario(36f, out var intents);
            sim.Step(intents);
            intents[5] = PlayerIntent.Idle;
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.Offside, 400, out var off));
            Assert.AreEqual(9, off.PlayerId);
            Assert.AreEqual(5, off.OtherPlayerId);
            Assert.AreEqual(36f, off.Position.Z, 0.5f, "the call is made at the position at the moment of the pass");
            Assert.Greater(off.Value, 4f);
            Assert.AreEqual(RestartType.IndirectFreeKick, sim.State.PendingRestart);
            Assert.AreEqual(SimTeam.Away, sim.State.RestartTeam);
        }

        [Test]
        public void Step_PassToPlayerBehindLastDefender_NoOffsidePassCompleted()
        {
            var sim = OffsideScenario(25f, out var intents);
            sim.Step(intents);
            intents[5] = PlayerIntent.Idle;
            bool offside = false;
            bool completed = false;
            for (int t = 0; t < 400 && !completed; t++)
            {
                foreach (var e in sim.Step(intents))
                {
                    if (e.Type == MatchEventType.Offside) offside = true;
                    if (e.Type == MatchEventType.PassCompleted && e.PlayerId == 9) completed = true;
                }
            }

            Assert.IsFalse(offside);
            Assert.IsTrue(completed);
        }

        [Test]
        public void Step_ClockExpires_HalfTimeThenSecondHalfKickOffThenFullTime()
        {
            var cfg = new MatchSimConfig { HalfDurationSeconds = 3f };
            var sim = SimTestHelpers.NewSim(8, cfg);
            var intents = SimTestHelpers.IdleIntents();
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.HalfTime, 400, out _));
            for (int i = 0; i < 120 && sim.State.Half == 1; i++) sim.Step(intents);
            Assert.AreEqual(2, sim.State.Half);
            Assert.AreEqual(SimTeam.Away, sim.State.RestartTeam, "the other team kicks off the second half");
            Assert.AreEqual(-1, sim.AttackSign(SimTeam.Home), "ends swap at half time");
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.FullTime, 400, out _));
            Assert.AreEqual(SimMatchPhase.FullTime, sim.Phase);
            Assert.AreEqual(0, sim.Step(intents).Count);
        }

        [Test]
        public void Step_IdleRestartTaker_AutoTakesRestart()
        {
            var cfg = new MatchSimConfig { RestartAutoTakeSeconds = 1f };
            var sim = SimTestHelpers.NewSim(9, cfg);
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, SimTestHelpers.IdleIntents(), MatchEventType.KickOff, 200, out _));
            Assert.AreEqual(SimMatchPhase.InPlay, sim.Phase);
        }

        [Test]
        public void Step_StandingTackleOnCarrier_ResolvesWithTackleOrFoulEvent()
        {
            int resolved = 0, won = 0, fouls = 0;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var sim = SimTestHelpers.NewSim(seed);
                SimTestHelpers.ForceOpenPlay(sim);
                SimTestHelpers.Place(sim, 9, 0f, 0f);
                SimTestHelpers.Place(sim, 15, 0f, 1.0f);
                sim.State.Players[9].Facing = 0f;
                sim.State.Players[15].Facing = SimMath.Pi;
                sim.State.Owner = 9;
                sim.State.PossessionTeam = SimTeam.Home;
                SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, 0.5f), SimVec3.Zero);
                var intents = SimTestHelpers.IdleIntents();
                intents[15] = new PlayerIntent { Action = IntentAction.Tackle, TargetPlayerId = -1 };
                for (int t = 0; t < 30; t++)
                {
                    foreach (var e in sim.Step(intents))
                    {
                        if (e.Type == MatchEventType.Tackle && e.PlayerId == 15) { resolved++; if (e.Detail == 1) won++; }
                        if (e.Type == MatchEventType.Foul && e.PlayerId == 15) fouls++;
                    }

                    intents[15] = PlayerIntent.Idle;
                }
            }

            Assert.AreEqual(40, resolved, "every tackle attempt resolves exactly once");
            Assert.Greater(won, 5);
            Assert.Less(won, 40);
            Assert.Less(fouls, 20);
        }

        [Test]
        public void Step_SlideFromBehindOnLastMan_CanBeRedCard()
        {
            int reds = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var sim = SimTestHelpers.NewSim(seed);
                SimTestHelpers.ForceOpenPlay(sim);
                SimTestHelpers.ParkTeam(sim, SimTeam.Away, -45f);
                SimTestHelpers.Place(sim, 11, 30f, -50f);
                // Home attacker clean through on the away goal (+Z), away defender chasing from behind.
                SimTestHelpers.Place(sim, 9, 0f, HL - 20f);
                SimTestHelpers.Place(sim, 14, 0f, HL - 21.2f);
                sim.State.Players[9].Facing = 0f;
                sim.State.Owner = 9;
                sim.State.PossessionTeam = SimTeam.Home;
                SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, HL - 19.5f), SimVec3.Zero);
                var intents = SimTestHelpers.IdleIntents();
                intents[14] = new PlayerIntent { Action = IntentAction.SlideTackle, Aim = new SimVec3(0f, 0f, 1f), TargetPlayerId = -1 };
                for (int t = 0; t < 60; t++)
                {
                    foreach (var e in sim.Step(intents)) if (e.Type == MatchEventType.RedCard) reds++;
                    intents[14] = PlayerIntent.Idle;
                }
            }

            Assert.Greater(reds, 0, "a DOGSO foul from behind should send the defender off");
        }
    }
}
