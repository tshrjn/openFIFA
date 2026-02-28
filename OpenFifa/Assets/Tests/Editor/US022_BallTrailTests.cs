using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US022")]
    public class US022_BallTrailTests
    {
        [Test]
        public void BallTrailLogic_DefaultThreshold_Is10()
        {
            var logic = new BallTrailLogic();
            Assert.AreEqual(10f, logic.VelocityThreshold);
        }

        [Test]
        public void BallTrailLogic_BelowThreshold_EmissionDisabled()
        {
            var logic = new BallTrailLogic();
            logic.Update(5f);
            Assert.IsFalse(logic.ShouldEmit, "Should not emit below velocity threshold");
        }

        [Test]
        public void BallTrailLogic_AboveThreshold_EmissionEnabled()
        {
            var logic = new BallTrailLogic();
            logic.Update(15f);
            Assert.IsTrue(logic.ShouldEmit, "Should emit above velocity threshold");
        }

        [Test]
        public void BallTrailLogic_AtThreshold_EmissionEnabled()
        {
            var logic = new BallTrailLogic();
            logic.Update(10f);
            Assert.IsTrue(logic.ShouldEmit, "Should emit at exact threshold");
        }

        [Test]
        public void BallTrailLogic_Stationary_EmissionDisabled()
        {
            var logic = new BallTrailLogic();
            logic.Update(0f);
            Assert.IsFalse(logic.ShouldEmit);
        }

        [Test]
        public void BallTrailLogic_Alpha_ScalesWithSpeed()
        {
            var logic = new BallTrailLogic();
            logic.Update(15f);
            float alpha = logic.TrailAlpha;
            Assert.Greater(alpha, 0f);
            Assert.LessOrEqual(alpha, 1f);
        }

        [Test]
        public void BallTrailLogic_Alpha_ZeroBelowThreshold()
        {
            var logic = new BallTrailLogic();
            logic.Update(5f);
            Assert.AreEqual(0f, logic.TrailAlpha);
        }

        [Test]
        public void BallTrailLogic_EmissionRate_ScalesWithSpeed()
        {
            var logic = new BallTrailLogic();
            logic.Update(20f);
            float rate = logic.EmissionRate;
            Assert.Greater(rate, 5f, "Emission rate should be above minimum at high speed");
            Assert.LessOrEqual(rate, 30f, "Emission rate should not exceed max");
        }

        [Test]
        public void BallTrailLogic_MaxParticles_Is50()
        {
            var logic = new BallTrailLogic();
            Assert.AreEqual(50, logic.MaxParticles);
        }

        [Test]
        public void BallTrailLogic_EmissionRate_AtThreshold_IsMinimum()
        {
            var logic = new BallTrailLogic();
            logic.Update(10f);
            float rate = logic.EmissionRate;
            Assert.AreEqual(5f, rate, 0.1f, "At threshold, emission rate should be minimum");
        }
    }
}
