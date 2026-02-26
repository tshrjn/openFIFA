using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-005")]
    public class US005_GoalDetectionTests
    {
        [Test]
        public void TeamIdentifier_HasTeamA()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(TeamIdentifier), TeamIdentifier.TeamA),
                "TeamIdentifier should have TeamA value");
        }

        [Test]
        public void TeamIdentifier_HasTeamB()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(TeamIdentifier), TeamIdentifier.TeamB),
                "TeamIdentifier should have TeamB value");
        }

        [Test]
        public void TeamIdentifier_TeamA_IsNotEqualToTeamB()
        {
            Assert.AreNotEqual(TeamIdentifier.TeamA, TeamIdentifier.TeamB,
                "TeamA and TeamB should be different values");
        }

        [Test]
        public void GoalEventData_ContainsScoringTeam()
        {
            var data = new GoalEventData(TeamIdentifier.TeamA);
            Assert.AreEqual(TeamIdentifier.TeamA, data.ScoringTeam,
                "GoalEventData should contain the scoring team");
        }

        [Test]
        public void GoalEventData_ContainsDefendingTeam()
        {
            var data = new GoalEventData(TeamIdentifier.TeamA);
            Assert.AreEqual(TeamIdentifier.TeamB, data.DefendingTeam,
                "When TeamA scores, defending team should be TeamB");
        }

        [Test]
        public void GoalEventData_TeamB_Scores_DefenderIsTeamA()
        {
            var data = new GoalEventData(TeamIdentifier.TeamB);
            Assert.AreEqual(TeamIdentifier.TeamA, data.DefendingTeam,
                "When TeamB scores, defending team should be TeamA");
        }
    }
}
