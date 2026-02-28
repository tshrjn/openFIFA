using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US008")]
    public class US008_HUDFormatTests
    {
        [Test]
        public void HUDFormatter_FormatScore_DefaultIsTeamA0Minus0TeamB()
        {
            var score = new MatchScore();
            string formatted = HUDFormatter.FormatScore(score);
            Assert.AreEqual("TeamA 0 - 0 TeamB", formatted,
                $"Default score format should be 'TeamA 0 - 0 TeamB' but was '{formatted}'");
        }

        [Test]
        public void HUDFormatter_FormatScore_ReflectsGoals()
        {
            var score = new MatchScore();
            score.AddGoal(TeamIdentifier.TeamA);
            score.AddGoal(TeamIdentifier.TeamA);
            score.AddGoal(TeamIdentifier.TeamB);

            string formatted = HUDFormatter.FormatScore(score);
            Assert.AreEqual("TeamA 2 - 1 TeamB", formatted,
                $"Score format should be 'TeamA 2 - 1 TeamB' but was '{formatted}'");
        }

        [Test]
        public void HUDFormatter_FormatScore_WithCustomTeamNames()
        {
            var score = new MatchScore();
            score.AddGoal(TeamIdentifier.TeamB);

            string formatted = HUDFormatter.FormatScore(score, "Brazil", "Germany");
            Assert.AreEqual("Brazil 0 - 1 Germany", formatted,
                $"Score format should be 'Brazil 0 - 1 Germany' but was '{formatted}'");
        }

        [Test]
        public void HUDFormatter_FormatTimer_180Seconds_Is0300()
        {
            string formatted = HUDFormatter.FormatTimer(180f);
            Assert.AreEqual("03:00", formatted,
                $"180 seconds should format as '03:00' but was '{formatted}'");
        }

        [Test]
        public void HUDFormatter_FormatTimer_0Seconds_Is0000()
        {
            string formatted = HUDFormatter.FormatTimer(0f);
            Assert.AreEqual("00:00", formatted,
                $"0 seconds should format as '00:00' but was '{formatted}'");
        }

        [Test]
        public void HUDFormatter_FormatTimer_90Point5Seconds_Is0130()
        {
            string formatted = HUDFormatter.FormatTimer(90.5f);
            Assert.AreEqual("01:30", formatted,
                $"90.5 seconds should format as '01:30' but was '{formatted}'");
        }

        [Test]
        public void HUDFormatter_FormatTimer_NegativeSeconds_Is0000()
        {
            string formatted = HUDFormatter.FormatTimer(-5f);
            Assert.AreEqual("00:00", formatted,
                $"Negative seconds should format as '00:00' but was '{formatted}'");
        }

        [Test]
        public void HUDFormatter_FormatTimer_59Seconds_Is0059()
        {
            string formatted = HUDFormatter.FormatTimer(59f);
            Assert.AreEqual("00:59", formatted,
                $"59 seconds should format as '00:59' but was '{formatted}'");
        }

        [Test]
        public void HUDFormatter_FormatTimer_600Seconds_Is1000()
        {
            string formatted = HUDFormatter.FormatTimer(600f);
            Assert.AreEqual("10:00", formatted,
                $"600 seconds should format as '10:00' but was '{formatted}'");
        }
    }
}
