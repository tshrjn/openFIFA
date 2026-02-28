using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US021")]
    public class US021_CelebrationTests
    {
        [Test]
        public void CelebrationLogic_InitialState_NotPlaying()
        {
            var logic = new CelebrationLogic();
            Assert.IsFalse(logic.IsPlaying);
        }

        [Test]
        public void CelebrationLogic_Start_SetsPlaying()
        {
            var logic = new CelebrationLogic();
            logic.StartCelebration();
            Assert.IsTrue(logic.IsPlaying);
        }

        [Test]
        public void CelebrationLogic_Start_SetsSlowMotionTimeScale()
        {
            var logic = new CelebrationLogic();
            logic.StartCelebration();
            Assert.AreEqual(0.3f, logic.TargetTimeScale, 0.001f);
        }

        [Test]
        public void CelebrationLogic_Complete_RestoresTimeScale()
        {
            var logic = new CelebrationLogic();
            logic.StartCelebration();
            logic.CompleteCelebration();
            Assert.AreEqual(1.0f, logic.TargetTimeScale, 0.001f);
        }

        [Test]
        public void CelebrationLogic_Complete_SetsNotPlaying()
        {
            var logic = new CelebrationLogic();
            logic.StartCelebration();
            logic.CompleteCelebration();
            Assert.IsFalse(logic.IsPlaying);
        }

        [Test]
        public void CelebrationLogic_Duration_Default2Seconds()
        {
            var logic = new CelebrationLogic();
            Assert.AreEqual(2f, logic.CelebrationDuration);
        }

        [Test]
        public void CelebrationLogic_DoubleStart_DoesNotStack()
        {
            var logic = new CelebrationLogic();
            logic.StartCelebration();
            bool secondStartResult = logic.TryStartCelebration();
            Assert.IsFalse(secondStartResult, "Should not allow stacking celebrations");
        }

        [Test]
        public void CelebrationLogic_TryStart_TrueWhenNotPlaying()
        {
            var logic = new CelebrationLogic();
            bool result = logic.TryStartCelebration();
            Assert.IsTrue(result);
            Assert.IsTrue(logic.IsPlaying);
        }

        [Test]
        public void CelebrationLogic_ShouldTriggerKickoff_TrueAfterComplete()
        {
            var logic = new CelebrationLogic();
            logic.StartCelebration();
            logic.CompleteCelebration();
            Assert.IsTrue(logic.ShouldTriggerKickoff,
                "Kickoff sequence should begin after celebration completes");
        }

        [Test]
        public void CelebrationLogic_ShouldTriggerKickoff_ResetsAfterRead()
        {
            var logic = new CelebrationLogic();
            logic.StartCelebration();
            logic.CompleteCelebration();
            bool first = logic.ShouldTriggerKickoff;
            Assert.IsTrue(first);
            // After reading, flag should be consumed
            Assert.IsFalse(logic.ShouldTriggerKickoff);
        }
    }
}
