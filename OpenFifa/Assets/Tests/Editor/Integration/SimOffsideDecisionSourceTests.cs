using NUnit.Framework;
using OpenFifa.Core;
using OpenFifa.Tests.Editor.Sim;

namespace OpenFifa.Tests.Editor.Integration
{
    /// <summary>
    /// The sim's offside calls reach the offside presentation as <see cref="OffsideDecision"/>s through
    /// <see cref="SimOffsideDecisionSource"/>, and both rule paths share one <see cref="RestartType"/>.
    /// </summary>
    [TestFixture]
    [Category("FID_INTEGRATION")]
    public class SimOffsideDecisionSourceTests
    {
        [Test]
        public void SimOffside_RaisesDecisionWithLinesAndFreeKickSpot()
        {
            var sim = SimTestHelpers.NewSim(7);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Home, -40f);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, 30f); // second-last defender line at z = 30
            SimTestHelpers.Place(sim, 11, 0f, 50f);           // keeper deepest
            SimTestHelpers.Place(sim, 5, 0f, 10f);            // passer
            SimTestHelpers.Place(sim, 9, 3f, 36f);            // receiver, 6 m beyond the line
            sim.State.Owner = 5;
            sim.State.PossessionTeam = SimTeam.Home;
            SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, 10.5f), SimVec3.Zero);
            var intents = SimTestHelpers.IdleIntents();
            intents[5] = PlayerIntent.Kick(IntentAction.Pass, 0.6f, new SimVec3(0.1f, 0f, 1f), 9);

            var source = new SimOffsideDecisionSource();
            IOffsideDecisionSource asInterface = source;
            OffsideDecision raised = null;
            int count = 0;
            asInterface.OnOffence += d => { raised = d; count++; };

            for (int t = 0; t < 400 && count == 0; t++)
            {
                sim.Step(intents);
                intents[5] = PlayerIntent.Idle;
                foreach (var e in sim.Events) source.Process(e);
            }

            Assert.AreEqual(1, count);
            Assert.IsTrue(raised.IsOffence);
            Assert.AreEqual(OffsideReason.PlayingBall, raised.Reason);
            Assert.AreEqual(TeamIdentifier.TeamA, raised.OffenderTeam);
            Assert.AreEqual(TeamIdentifier.TeamB, raised.DefendingTeam, "Away takes the indirect free kick");
            Assert.AreEqual(9, raised.OffenderId);
            Assert.AreEqual(5, raised.PasserId);
            Assert.AreEqual(36f, raised.AttackerLineX, 0.5f);
            Assert.AreEqual(30f, raised.DefenderLineX, 0.5f);
            Assert.AreEqual(10.5f, raised.BallLineX, 1f);
            Assert.AreEqual(36f, raised.FreeKickZ, 0.5f);
            Assert.AreSame(raised, source.Last);
            Assert.AreEqual(RestartType.IndirectFreeKick, sim.State.PendingRestart);
        }

        [Test]
        public void ToDecision_NonOffsideEvent_IsNone()
        {
            var e = new MatchEvent { Type = MatchEventType.Pass, PlayerId = 3 };
            Assert.AreSame(OffsideDecision.None, SimOffsideDecisionSource.ToDecision(e));
        }

        [Test]
        public void ToTeamIdentifier_HomeIsTeamA_AwayIsTeamB()
        {
            Assert.AreEqual(TeamIdentifier.TeamA, SimOffsideDecisionSource.ToTeamIdentifier(SimTeam.Home));
            Assert.AreEqual(TeamIdentifier.TeamB, SimOffsideDecisionSource.ToTeamIdentifier(SimTeam.Away));
        }

        [Test]
        public void OffsideLogicAndSimRule_AgreeOnLaw11Exemptions()
        {
            foreach (RestartType restart in System.Enum.GetValues(typeof(RestartType)))
            {
                bool exempt = restart == RestartType.GoalKick || restart == RestartType.ThrowIn || restart == RestartType.Corner;
                Assert.AreEqual(exempt, OffsideLogic.IsExemptRestart(restart), restart.ToString());
                Assert.AreEqual(!exempt, OffsideRule.AppliesToRestart(restart), restart.ToString());
            }
        }
    }
}
