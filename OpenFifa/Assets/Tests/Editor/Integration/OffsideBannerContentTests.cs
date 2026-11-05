using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Integration
{
    /// <summary>Sim offside decision -> lower-third content (number, known-as name, team label, side).</summary>
    [TestFixture]
    [Category("FID_INTEGRATION")]
    public class OffsideBannerContentTests
    {
        private static OffsideDecision Decision(int id, SimTeam team)
        {
            var e = new MatchEvent
            {
                Type = MatchEventType.Offside, PlayerId = id, OtherPlayerId = -1, Team = team,
                Position = new SimVec3(1f, 0f, 30f), DefenderLineZ = 29f, BallLineZ = 20f
            };
            return SimOffsideDecisionSource.ToDecision(e);
        }

        private static string[] Names()
        {
            var n = new string[MatchSimState.PlayerCount];
            for (int i = 0; i < n.Length; i++) n[i] = "Name" + i;
            n[9] = "Haaland";
            n[20] = "Saka";
            return n;
        }

        private static int[] Numbers()
        {
            var n = new int[MatchSimState.PlayerCount];
            for (int i = 0; i < n.Length; i++) n[i] = i % 11 + 1;
            n[9] = 9;
            n[20] = 7;
            return n;
        }

        [Test]
        public void FromSimDecision_HomeOffender_UsesRosterNumberNameAndHomeLabel()
        {
            var c = OffsideBannerContent.FromSimDecision(Decision(9, SimTeam.Home), Names(), Numbers(), "MCI", "ARS");
            Assert.AreEqual(9, c.ShirtNumber);
            Assert.AreEqual("Haaland", c.PlayerName);
            Assert.AreEqual("MCI", c.TeamLabel);
            Assert.IsTrue(c.OffenderIsHome);
        }

        [Test]
        public void FromSimDecision_AwayOffender_UsesAwaySide()
        {
            var c = OffsideBannerContent.FromSimDecision(Decision(20, SimTeam.Away), Names(), Numbers(), "MCI", "ARS");
            Assert.AreEqual(7, c.ShirtNumber);
            Assert.AreEqual("Saka", c.PlayerName);
            Assert.AreEqual("ARS", c.TeamLabel);
            Assert.IsFalse(c.OffenderIsHome);
        }

        [Test]
        public void FromSimDecision_MissingRosterData_FallsBackToSlot()
        {
            var c = OffsideBannerContent.FromSimDecision(Decision(14, SimTeam.Away), null, null, "H", "A");
            Assert.AreEqual(4, c.ShirtNumber);
            Assert.AreEqual("Player 4", c.PlayerName);
            Assert.AreEqual("A", c.TeamLabel);
        }

        [Test]
        public void FromSimDecision_NoOffence_IsEmpty()
        {
            var c = OffsideBannerContent.FromSimDecision(OffsideDecision.None, Names(), Numbers(), "H", "A");
            Assert.AreEqual(0, c.ShirtNumber);
            Assert.AreEqual(string.Empty, c.PlayerName);
        }

        [Test]
        public void SourceProcess_EveryEventFed_RaisesOnlyOffsides()
        {
            var source = new SimOffsideDecisionSource();
            int raised = 0;
            OffsideDecision last = null;
            source.OnOffence += d => { raised++; last = d; };
            source.Process(new MatchEvent { Type = MatchEventType.Pass, PlayerId = 3 });
            source.Process(new MatchEvent { Type = MatchEventType.Offside, PlayerId = 20, Team = SimTeam.Away, Position = new SimVec3(0f, 0f, -30f), DefenderLineZ = -29f });
            source.Process(new MatchEvent { Type = MatchEventType.Goal, PlayerId = 9 });
            Assert.AreEqual(1, raised);
            Assert.AreEqual(TeamIdentifier.TeamB, last.OffenderTeam);
            Assert.AreEqual(-30f, last.AttackerLineX, 1e-4f);
            Assert.AreEqual(-29f, last.DefenderLineX, 1e-4f);
        }
    }
}
