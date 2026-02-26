using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-029")]
    public class US029_TeamSelectTests
    {
        [Test]
        public void TeamSelectLogic_InitialState_NoSelection()
        {
            var logic = new TeamSelectLogic(4);
            Assert.AreEqual(-1, logic.SelectedTeamIndex);
        }

        [Test]
        public void TeamSelectLogic_SelectTeam_SetsIndex()
        {
            var logic = new TeamSelectLogic(4);
            logic.SelectTeam(2);
            Assert.AreEqual(2, logic.SelectedTeamIndex);
        }

        [Test]
        public void TeamSelectLogic_CanConfirm_FalseBeforeSelection()
        {
            var logic = new TeamSelectLogic(4);
            Assert.IsFalse(logic.CanConfirm);
        }

        [Test]
        public void TeamSelectLogic_CanConfirm_TrueAfterSelection()
        {
            var logic = new TeamSelectLogic(4);
            logic.SelectTeam(1);
            Assert.IsTrue(logic.CanConfirm);
        }

        [Test]
        public void TeamSelectLogic_AITeam_DifferentFromPlayerTeam()
        {
            var logic = new TeamSelectLogic(4);
            logic.SelectTeam(0);
            int aiTeam = logic.GetAITeamIndex();
            Assert.AreNotEqual(0, aiTeam, "AI team should differ from player team");
        }

        [Test]
        public void TeamSelectLogic_MinimumTeams_IsFour()
        {
            var logic = new TeamSelectLogic(4);
            Assert.AreEqual(4, logic.TeamCount);
        }

        [Test]
        public void TeamDataDefaults_HasNameAndColors()
        {
            var data = new TeamDataEntry("Lions", 0f, 0f, 1f, 1f, 1f, 0f);
            Assert.AreEqual("Lions", data.Name);
            Assert.AreEqual(0f, data.PrimaryR);
            Assert.AreEqual(1f, data.SecondaryG);
        }

        [Test]
        public void TeamSelectLogic_SelectOutOfRange_Ignored()
        {
            var logic = new TeamSelectLogic(4);
            logic.SelectTeam(10);
            Assert.AreEqual(-1, logic.SelectedTeamIndex, "Out-of-range selection should be ignored");
        }

        [Test]
        public void TeamSelectLogic_ChangeSelection_UpdatesIndex()
        {
            var logic = new TeamSelectLogic(4);
            logic.SelectTeam(0);
            logic.SelectTeam(3);
            Assert.AreEqual(3, logic.SelectedTeamIndex);
        }
    }
}
