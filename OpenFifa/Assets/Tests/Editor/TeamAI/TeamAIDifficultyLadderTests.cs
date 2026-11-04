using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID_TEAMAI")]
    public class TeamAIDifficultyLadderTests
    {
        [Test]
        public void For_EachLevelUp_OpponentGetsSharper()
        {
            for (int i = 1; i < TeamAIDifficultyLadder.LevelCount; i++)
            {
                var lower = TeamAIDifficultyLadder.For((TeamAIDifficultyLevel)(i - 1)).OpponentDifficulty;
                var higher = TeamAIDifficultyLadder.For((TeamAIDifficultyLevel)i).OpponentDifficulty;
                Assert.That(higher.ReactionDelay, Is.LessThan(lower.ReactionDelay), $"Level {i} reaction");
                Assert.That(higher.AimError, Is.LessThan(lower.AimError), $"Level {i} aim error");
            }
        }

        [Test]
        public void For_Ultimate_IsMaximumTheta()
        {
            Assert.That(TeamAIDifficultyLadder.For(TeamAIDifficultyLevel.Ultimate).OpponentTheta, Is.EqualTo(1f));
        }

        [Test]
        public void For_OutOfRangeLevel_ClampsToUltimate()
        {
            Assert.That(TeamAIDifficultyLadder.For((TeamAIDifficultyLevel)42).Level,
                Is.EqualTo(TeamAIDifficultyLevel.Ultimate));
        }

        [Test]
        public void DisplayName_SemiPro_HasHyphen()
        {
            Assert.That(TeamAIDifficultyLadder.DisplayName(TeamAIDifficultyLevel.SemiPro), Is.EqualTo("Semi-Pro"));
        }
    }
}
