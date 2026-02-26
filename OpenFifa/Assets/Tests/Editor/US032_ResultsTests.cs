using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-032")]
    public class US032_ResultsTests
    {
        [Test]
        public void MatchResultsLogic_FormatScore_CorrectFormat()
        {
            var logic = new MatchResultsLogic("Lions", 3, "Tigers", 1, 90f);
            string score = logic.FinalScoreDisplay;
            Assert.AreEqual("Lions 3 - 1 Tigers", score);
        }

        [Test]
        public void MatchResultsLogic_MatchDuration_Stored()
        {
            var logic = new MatchResultsLogic("A", 0, "B", 0, 120f);
            Assert.AreEqual(120f, logic.MatchDuration);
        }

        [Test]
        public void MatchResultsLogic_ManOfTheMatch_MostGoals()
        {
            var logic = new MatchResultsLogic("A", 2, "B", 1, 90f);
            logic.AddGoalRecord("Player1", 10f, "A");
            logic.AddGoalRecord("Player1", 45f, "A");
            logic.AddGoalRecord("Player2", 60f, "B");

            string motm = logic.GetManOfTheMatch();
            Assert.AreEqual("Player1", motm, "Player with most goals should be MOTM");
        }

        [Test]
        public void MatchResultsLogic_ManOfTheMatch_NoGoals_ReturnsDefault()
        {
            var logic = new MatchResultsLogic("A", 0, "B", 0, 90f);
            string motm = logic.GetManOfTheMatch();
            Assert.AreEqual("N/A", motm);
        }

        [Test]
        public void MatchResultsLogic_GoalRecords_CountCorrect()
        {
            var logic = new MatchResultsLogic("A", 2, "B", 0, 90f);
            logic.AddGoalRecord("P1", 10f, "A");
            logic.AddGoalRecord("P2", 30f, "A");
            Assert.AreEqual(2, logic.GoalCount);
        }

        [Test]
        public void MatchResultsLogic_WinnerTeam_HigherScore()
        {
            var logic = new MatchResultsLogic("A", 3, "B", 1, 90f);
            Assert.AreEqual("A", logic.WinnerTeam);
        }

        [Test]
        public void MatchResultsLogic_WinnerTeam_Draw()
        {
            var logic = new MatchResultsLogic("A", 2, "B", 2, 90f);
            Assert.AreEqual("Draw", logic.WinnerTeam);
        }

        [Test]
        public void MatchResultsLogic_DurationDisplay_FormatsCorrectly()
        {
            var logic = new MatchResultsLogic("A", 0, "B", 0, 125.5f);
            string display = logic.DurationDisplay;
            Assert.AreEqual("02:05", display);
        }
    }
}
