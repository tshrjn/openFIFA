using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-026")]
    public class US026_RunDustTests
    {
        [Test]
        public void RunDustLogic_DefaultWalkThreshold_Is2()
        {
            var logic = new RunDustLogic();
            Assert.AreEqual(2f, logic.WalkThreshold);
        }

        [Test]
        public void RunDustLogic_Stationary_NoEmission()
        {
            var logic = new RunDustLogic();
            logic.Update(0f);
            Assert.IsFalse(logic.ShouldEmit);
        }

        [Test]
        public void RunDustLogic_BelowThreshold_NoEmission()
        {
            var logic = new RunDustLogic();
            logic.Update(1.5f);
            Assert.IsFalse(logic.ShouldEmit, "Should not emit below walk threshold");
        }

        [Test]
        public void RunDustLogic_AboveThreshold_Emits()
        {
            var logic = new RunDustLogic();
            logic.Update(5f);
            Assert.IsTrue(logic.ShouldEmit, "Should emit above walk threshold");
        }

        [Test]
        public void RunDustLogic_EmissionRate_ScalesWithSpeed()
        {
            var logic = new RunDustLogic();
            logic.Update(5f);
            float lowRate = logic.EmissionRate;

            logic.Update(10f);
            float highRate = logic.EmissionRate;

            Assert.Greater(highRate, lowRate, "Higher speed should produce higher emission rate");
        }

        [Test]
        public void RunDustLogic_MaxParticles_Is20()
        {
            var logic = new RunDustLogic();
            Assert.AreEqual(20, logic.MaxParticles);
        }

        [Test]
        public void RunDustLogic_EmissionRate_Range2To15()
        {
            var logic = new RunDustLogic();

            // Just above threshold
            logic.Update(2.1f);
            Assert.GreaterOrEqual(logic.EmissionRate, 2f);

            // Very fast
            logic.Update(15f);
            Assert.LessOrEqual(logic.EmissionRate, 15f);
        }

        [Test]
        public void RunDustLogic_EmissionRate_ZeroBelowThreshold()
        {
            var logic = new RunDustLogic();
            logic.Update(1f);
            Assert.AreEqual(0f, logic.EmissionRate);
        }
    }
}
