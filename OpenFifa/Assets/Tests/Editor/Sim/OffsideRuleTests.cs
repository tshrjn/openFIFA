using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    [TestFixture]
    [Category("FID_SIM")]
    public class OffsideRuleTests
    {
        [Test]
        public void IsOffsidePosition_BeyondSecondLastDefenderAndBall_True()
        {
            Assert.IsTrue(OffsideRule.IsOffsidePosition(30f, 10f, 28f));
        }

        [Test]
        public void IsOffsidePosition_LevelWithSecondLastDefender_False()
        {
            Assert.IsFalse(OffsideRule.IsOffsidePosition(28f, 10f, 28f));
        }

        [Test]
        public void IsOffsidePosition_BehindTheBall_False()
        {
            Assert.IsFalse(OffsideRule.IsOffsidePosition(30f, 31f, 20f));
        }

        [Test]
        public void IsOffsidePosition_InOwnHalf_False()
        {
            Assert.IsFalse(OffsideRule.IsOffsidePosition(-1f, -20f, -10f));
            Assert.IsFalse(OffsideRule.IsOffsidePosition(0f, -20f, -10f));
        }

        [Test]
        public void SecondLastDefender_KeeperDeepest_ReturnsLastOutfielder()
        {
            var coords = new[] { 52f, 30f, 28f, 20f };
            Assert.AreEqual(30f, OffsideRule.SecondLastDefender(coords, coords.Length));
        }

        [Test]
        public void SecondLastDefender_KeeperUpfield_ReturnsSecondDeepest()
        {
            var coords = new[] { 10f, 40f, 45f, 20f };
            Assert.AreEqual(40f, OffsideRule.SecondLastDefender(coords, coords.Length));
        }

        [Test]
        public void AppliesToRestart_ThrowInGoalKickCorner_False_FreeKick_True()
        {
            Assert.IsFalse(OffsideRule.AppliesToRestart(RestartType.ThrowIn));
            Assert.IsFalse(OffsideRule.AppliesToRestart(RestartType.GoalKick));
            Assert.IsFalse(OffsideRule.AppliesToRestart(RestartType.Corner));
            Assert.IsTrue(OffsideRule.AppliesToRestart(RestartType.DirectFreeKick));
            Assert.IsTrue(OffsideRule.AppliesToRestart(RestartType.None));
        }
    }
}
