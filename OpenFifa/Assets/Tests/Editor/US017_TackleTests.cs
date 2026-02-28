using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US017")]
    public class US017_TackleTests
    {
        [Test]
        public void TackleLogic_InitialState_NotCoolingDown()
        {
            var logic = new TackleLogic(1.5f, 1.0f, 0.5f);
            Assert.IsFalse(logic.IsCoolingDown);
        }

        [Test]
        public void TackleLogic_InitialState_NotLunging()
        {
            var logic = new TackleLogic(1.5f, 1.0f, 0.5f);
            Assert.IsFalse(logic.IsLunging);
        }

        [Test]
        public void TackleLogic_CanTackle_WhenInRangeAndNotCooling()
        {
            var logic = new TackleLogic(1.5f, 1.0f, 0.5f);
            bool canTackle = logic.CanAttemptTackle(1.0f, 0f);
            Assert.IsTrue(canTackle, "Should be able to tackle when in range and not cooling down");
        }

        [Test]
        public void TackleLogic_CannotTackle_WhenOutOfRange()
        {
            var logic = new TackleLogic(1.5f, 1.0f, 0.5f);
            bool canTackle = logic.CanAttemptTackle(3.0f, 0f);
            Assert.IsFalse(canTackle, "Should not be able to tackle when beyond range");
        }

        [Test]
        public void TackleLogic_AttemptTackle_SetsLunging()
        {
            var logic = new TackleLogic(1.5f, 1.0f, 0.5f);
            var result = logic.AttemptTackle(1.0f, 0f);
            Assert.IsTrue(result.DidLunge, "Should lunge when in range");
            Assert.IsTrue(logic.IsLunging);
        }

        [Test]
        public void TackleLogic_AttemptTackle_Dispossesses_WhenInRange()
        {
            var logic = new TackleLogic(1.5f, 1.0f, 0.5f);
            var result = logic.AttemptTackle(1.0f, 0f);
            Assert.IsTrue(result.DidDispossess, "Should dispossess when within tackle radius");
        }

        [Test]
        public void TackleLogic_AttemptTackle_DoesNotDispossess_WhenOutOfRange()
        {
            var logic = new TackleLogic(1.5f, 1.0f, 0.5f);
            var result = logic.AttemptTackle(2.0f, 0f);
            Assert.IsFalse(result.DidLunge, "Should not lunge when out of range");
            Assert.IsFalse(result.DidDispossess);
        }

        [Test]
        public void TackleLogic_Cooldown_PreventsTackling()
        {
            var logic = new TackleLogic(1.5f, 1.0f, 0.5f);
            logic.AttemptTackle(1.0f, 0f);
            logic.CompleteLunge();

            // Immediately after tackle, should be cooling down
            Assert.IsTrue(logic.IsCoolingDown, "Should be cooling down after tackle");

            bool canTackle = logic.CanAttemptTackle(1.0f, 0.1f);
            Assert.IsFalse(canTackle, "Should not be able to tackle during cooldown");
        }

        [Test]
        public void TackleLogic_Cooldown_ExpiresAfterDuration()
        {
            var logic = new TackleLogic(1.5f, 1.0f, 0.5f);
            logic.AttemptTackle(1.0f, 0f);
            logic.CompleteLunge();

            // After cooldown duration elapses
            bool canTackle = logic.CanAttemptTackle(1.0f, 1.5f);
            Assert.IsTrue(canTackle, "Should be able to tackle after cooldown expires");
        }

        [Test]
        public void TackleLogic_StunDuration_IsConfigurable()
        {
            float stunDuration = 0.5f;
            var logic = new TackleLogic(1.5f, 1.0f, stunDuration);
            Assert.AreEqual(stunDuration, logic.StunDuration);
        }

        [Test]
        public void TackleLogic_TackleResult_ContainsStunDuration()
        {
            var logic = new TackleLogic(1.5f, 1.0f, 0.5f);
            var result = logic.AttemptTackle(1.0f, 0f);
            Assert.AreEqual(0.5f, result.StunDuration, "Result should contain the stun duration for the victim");
        }

        [Test]
        public void TackleLogic_TackleRadius_DefaultIs1_5()
        {
            var logic = new TackleLogic();
            Assert.AreEqual(1.5f, logic.TackleRadius);
        }
    }
}
