using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-024")]
    public class US024_CameraShakeTests
    {
        [Test]
        public void CameraShakeConfig_DefaultIntensity_IsOne()
        {
            var config = new CameraShakeConfigData();
            Assert.AreEqual(1f, config.ShakeIntensity);
        }

        [Test]
        public void CameraShakeConfig_DefaultDuration_Between05And10()
        {
            var config = new CameraShakeConfigData();
            Assert.GreaterOrEqual(config.ShakeDuration, 0.5f);
            Assert.LessOrEqual(config.ShakeDuration, 1.0f);
        }

        [Test]
        public void CameraShakeConfig_DefaultDecay_IsOne()
        {
            var config = new CameraShakeConfigData();
            Assert.AreEqual(1f, config.DecayRate);
        }

        [Test]
        public void CameraShakeLogic_InitialState_NotShaking()
        {
            var logic = new CameraShakeLogic(new CameraShakeConfigData());
            Assert.IsFalse(logic.IsShaking);
        }

        [Test]
        public void CameraShakeLogic_Trigger_SetsShaking()
        {
            var logic = new CameraShakeLogic(new CameraShakeConfigData());
            logic.TriggerShake();
            Assert.IsTrue(logic.IsShaking);
        }

        [Test]
        public void CameraShakeLogic_Update_DecaysOverTime()
        {
            var config = new CameraShakeConfigData { ShakeDuration = 0.5f };
            var logic = new CameraShakeLogic(config);
            logic.TriggerShake();

            float initialIntensity = logic.CurrentIntensity;
            logic.Update(0.25f); // Half duration
            float midIntensity = logic.CurrentIntensity;

            Assert.Less(midIntensity, initialIntensity, "Intensity should decay over time");
        }

        [Test]
        public void CameraShakeLogic_Update_StopsAfterDuration()
        {
            var config = new CameraShakeConfigData { ShakeDuration = 0.5f };
            var logic = new CameraShakeLogic(config);
            logic.TriggerShake();

            logic.Update(0.6f); // Past duration
            Assert.IsFalse(logic.IsShaking, "Should stop shaking after duration");
            Assert.AreEqual(0f, logic.CurrentIntensity, 0.001f);
        }

        [Test]
        public void CameraShakeLogic_Offset_NonZeroDuringShake()
        {
            var logic = new CameraShakeLogic(new CameraShakeConfigData());
            logic.TriggerShake();
            logic.Update(0.01f);

            // At least one axis should have non-zero offset
            float totalOffset = System.Math.Abs(logic.OffsetX) + System.Math.Abs(logic.OffsetY);
            Assert.Greater(totalOffset, 0f, "Should have non-zero camera offset during shake");
        }

        [Test]
        public void CameraShakeLogic_Offset_ZeroWhenNotShaking()
        {
            var logic = new CameraShakeLogic(new CameraShakeConfigData());
            Assert.AreEqual(0f, logic.OffsetX);
            Assert.AreEqual(0f, logic.OffsetY);
        }

        [Test]
        public void CameraShakeLogic_Offset_ZeroAfterShakeEnds()
        {
            var config = new CameraShakeConfigData { ShakeDuration = 0.5f };
            var logic = new CameraShakeLogic(config);
            logic.TriggerShake();
            logic.Update(0.6f);
            Assert.AreEqual(0f, logic.OffsetX);
            Assert.AreEqual(0f, logic.OffsetY);
        }
    }
}
