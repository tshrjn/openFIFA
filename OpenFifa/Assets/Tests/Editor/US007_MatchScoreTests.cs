using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US007")]
    public class US007_MatchScoreTests
    {
        [Test]
        public void MatchScore_InitialScores_AreZero()
        {
            var score = new MatchScore();
            Assert.AreEqual(0, score.GetScore(TeamIdentifier.TeamA),
                "Initial TeamA score should be 0");
            Assert.AreEqual(0, score.GetScore(TeamIdentifier.TeamB),
                "Initial TeamB score should be 0");
        }

        [Test]
        public void MatchScore_AddGoal_IncrementsTeamAScore()
        {
            var score = new MatchScore();
            score.AddGoal(TeamIdentifier.TeamA);
            Assert.AreEqual(1, score.GetScore(TeamIdentifier.TeamA),
                "TeamA score should be 1 after one goal");
        }

        [Test]
        public void MatchScore_AddGoal_IncrementsTeamBScore()
        {
            var score = new MatchScore();
            score.AddGoal(TeamIdentifier.TeamB);
            Assert.AreEqual(1, score.GetScore(TeamIdentifier.TeamB),
                "TeamB score should be 1 after one goal");
        }

        [Test]
        public void MatchScore_AddGoal_DoesNotAffectOtherTeam()
        {
            var score = new MatchScore();
            score.AddGoal(TeamIdentifier.TeamA);
            Assert.AreEqual(0, score.GetScore(TeamIdentifier.TeamB),
                "Adding TeamA goal should not affect TeamB score");
        }

        [Test]
        public void MatchScore_MultipleGoals_AccumulateCorrectly()
        {
            var score = new MatchScore();
            score.AddGoal(TeamIdentifier.TeamA);
            score.AddGoal(TeamIdentifier.TeamA);
            score.AddGoal(TeamIdentifier.TeamB);
            score.AddGoal(TeamIdentifier.TeamA);

            Assert.AreEqual(3, score.GetScore(TeamIdentifier.TeamA),
                "TeamA should have 3 goals");
            Assert.AreEqual(1, score.GetScore(TeamIdentifier.TeamB),
                "TeamB should have 1 goal");
        }

        [Test]
        public void MatchScore_OnScoreChanged_FiresOnGoal()
        {
            var score = new MatchScore();
            TeamIdentifier? scoredTeam = null;
            int? newScore = null;

            score.OnScoreChanged += (team, s) =>
            {
                scoredTeam = team;
                newScore = s;
            };

            score.AddGoal(TeamIdentifier.TeamA);

            Assert.AreEqual(TeamIdentifier.TeamA, scoredTeam,
                "OnScoreChanged should fire with TeamA");
            Assert.AreEqual(1, newScore,
                "OnScoreChanged should fire with new score 1");
        }

        [Test]
        public void MatchScore_Reset_ClearsAllScores()
        {
            var score = new MatchScore();
            score.AddGoal(TeamIdentifier.TeamA);
            score.AddGoal(TeamIdentifier.TeamB);
            score.Reset();

            Assert.AreEqual(0, score.GetScore(TeamIdentifier.TeamA),
                "TeamA score should be 0 after reset");
            Assert.AreEqual(0, score.GetScore(TeamIdentifier.TeamB),
                "TeamB score should be 0 after reset");
        }

        [Test]
        public void MatchScore_ScoreString_FormatsCorrectly()
        {
            var score = new MatchScore();
            score.AddGoal(TeamIdentifier.TeamA);
            score.AddGoal(TeamIdentifier.TeamA);
            score.AddGoal(TeamIdentifier.TeamB);

            string display = score.GetScoreDisplay();
            Assert.AreEqual("2 - 1", display,
                $"Score display should be '2 - 1' but was '{display}'");
        }
    }
}
