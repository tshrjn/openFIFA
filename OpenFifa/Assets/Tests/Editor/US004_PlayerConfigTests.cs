using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US004")]
    public class US004_PlayerConfigTests
    {
        [Test]
        public void PlayerStatsData_DefaultBaseSpeed_Is7()
        {
            var data = new PlayerStatsData();
            Assert.AreEqual(7f, data.BaseSpeed, 0.01f,
                $"Default base speed should be 7 m/s but was {data.BaseSpeed}");
        }

        [Test]
        public void PlayerStatsData_DefaultSprintMultiplier_Is1Point5()
        {
            var data = new PlayerStatsData();
            Assert.AreEqual(1.5f, data.SprintMultiplier, 0.01f,
                $"Default sprint multiplier should be 1.5 but was {data.SprintMultiplier}");
        }

        [Test]
        public void PlayerStatsData_SprintSpeed_IsBaseTimesMultiplier()
        {
            var data = new PlayerStatsData();
            float expected = 7f * 1.5f; // 10.5
            Assert.AreEqual(expected, data.SprintSpeed, 0.01f,
                $"Sprint speed should be {expected} m/s but was {data.SprintSpeed}");
        }

        [Test]
        public void PlayerStatsData_DefaultAcceleration_IsReasonable()
        {
            var data = new PlayerStatsData();
            Assert.Greater(data.Acceleration, 0f,
                "Acceleration should be positive");
            // Should allow reaching top speed in 1-3 seconds
            float timeToTopSpeed = data.BaseSpeed / data.Acceleration;
            Assert.That(timeToTopSpeed, Is.InRange(1f, 3f),
                $"Time to top speed should be 1-3s but was {timeToTopSpeed:F2}s " +
                $"(base={data.BaseSpeed}, accel={data.Acceleration})");
        }

        [Test]
        public void PlayerStatsData_DefaultDeceleration_IsPositive()
        {
            var data = new PlayerStatsData();
            Assert.Greater(data.Deceleration, 0f,
                "Deceleration should be positive");
        }

        [Test]
        public void PlayerStatsData_CustomValues_AreRetained()
        {
            var data = new PlayerStatsData(
                baseSpeed: 8f,
                sprintMultiplier: 1.6f,
                acceleration: 10f,
                deceleration: 15f
            );

            Assert.AreEqual(8f, data.BaseSpeed, 0.01f, "Custom base speed not retained");
            Assert.AreEqual(1.6f, data.SprintMultiplier, 0.01f, "Custom sprint multiplier not retained");
            Assert.AreEqual(10f, data.Acceleration, 0.01f, "Custom acceleration not retained");
            Assert.AreEqual(15f, data.Deceleration, 0.01f, "Custom deceleration not retained");
        }

        [Test]
        public void PlayerStatsData_SprintSpeed_UsesCustomValues()
        {
            var data = new PlayerStatsData(baseSpeed: 8f, sprintMultiplier: 2f);
            Assert.AreEqual(16f, data.SprintSpeed, 0.01f,
                $"Sprint speed should be 16 m/s but was {data.SprintSpeed}");
        }
    }
}
