using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US020")]
    public class US020_KickAnimationTests
    {
        [Test]
        public void KickData_PassForce_DefaultIs8()
        {
            var data = new KickConfigData();
            Assert.AreEqual(8f, data.PassForce);
        }

        [Test]
        public void KickData_ShootForce_DefaultIs15()
        {
            var data = new KickConfigData();
            Assert.AreEqual(15f, data.ShootForce);
        }

        [Test]
        public void KickData_ContactFrameTime_DefaultUnder100ms()
        {
            var data = new KickConfigData();
            Assert.Less(data.ContactFrameTime, 0.1f,
                "Contact frame should be under 100ms for responsive feel");
        }

        [Test]
        public void KickLogic_PrepareKick_StoresPendingKick()
        {
            var logic = new KickLogic(new KickConfigData());
            logic.PrepareKick(KickType.Pass, 1f, 0f, 0f, 1f);
            Assert.IsTrue(logic.HasPendingKick, "Should have a pending kick after prepare");
        }

        [Test]
        public void KickLogic_PreparePass_UsesPassForce()
        {
            var config = new KickConfigData();
            var logic = new KickLogic(config);
            logic.PrepareKick(KickType.Pass, 0f, 0f, 0f, 1f);
            Assert.AreEqual(config.PassForce, logic.PendingForce);
        }

        [Test]
        public void KickLogic_PrepareShoot_UsesShootForce()
        {
            var config = new KickConfigData();
            var logic = new KickLogic(config);
            logic.PrepareKick(KickType.Shoot, 0f, 0f, 0f, 1f);
            Assert.AreEqual(config.ShootForce, logic.PendingForce);
        }

        [Test]
        public void KickLogic_ExecuteKick_ClearsPending()
        {
            var logic = new KickLogic(new KickConfigData());
            logic.PrepareKick(KickType.Pass, 0f, 0f, 0f, 1f);
            var result = logic.ExecuteKick();
            Assert.IsFalse(logic.HasPendingKick, "Pending kick should be cleared after execution");
            Assert.IsTrue(result.Applied);
        }

        [Test]
        public void KickLogic_ExecuteKick_NoPending_ReturnsNotApplied()
        {
            var logic = new KickLogic(new KickConfigData());
            var result = logic.ExecuteKick();
            Assert.IsFalse(result.Applied, "Should not apply if no pending kick");
        }

        [Test]
        public void KickLogic_Direction_MatchesFacingDirection()
        {
            var logic = new KickLogic(new KickConfigData());
            logic.PrepareKick(KickType.Pass, 0f, 0f, 1f, 0f); // facing (1, 0)
            var result = logic.ExecuteKick();
            Assert.AreEqual(1f, result.DirectionX, 0.001f);
            Assert.AreEqual(0f, result.DirectionZ, 0.001f);
        }

        [Test]
        public void KickLogic_Direction_Normalized()
        {
            var logic = new KickLogic(new KickConfigData());
            logic.PrepareKick(KickType.Shoot, 0f, 0f, 3f, 4f); // non-unit direction
            var result = logic.ExecuteKick();
            float magnitude = result.DirectionX * result.DirectionX + result.DirectionZ * result.DirectionZ;
            Assert.AreEqual(1f, magnitude, 0.01f, "Direction should be normalized");
        }
    }
}
