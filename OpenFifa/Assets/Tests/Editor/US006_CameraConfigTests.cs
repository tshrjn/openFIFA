using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-006")]
    public class US006_CameraConfigTests
    {
        [Test]
        public void CameraConfigData_DefaultElevationAngle_IsApproximately35()
        {
            var config = new CameraConfigData();
            Assert.AreEqual(35f, config.ElevationAngle, 5f,
                $"Default elevation angle should be ~35 degrees but was {config.ElevationAngle}");
        }

        [Test]
        public void CameraConfigData_DefaultFollowDamping_IsPositive()
        {
            var config = new CameraConfigData();
            Assert.Greater(config.FollowDamping, 0f,
                $"Follow damping should be positive but was {config.FollowDamping}");
        }

        [Test]
        public void CameraConfigData_DefaultDistance_IsReasonable()
        {
            var config = new CameraConfigData();
            Assert.That(config.Distance, Is.InRange(15f, 50f),
                $"Camera distance should be in [15, 50] but was {config.Distance}");
        }

        [Test]
        public void CameraConfigData_DefaultFieldOfView_IsReasonable()
        {
            var config = new CameraConfigData();
            Assert.That(config.FieldOfView, Is.InRange(40f, 80f),
                $"Camera FOV should be in [40, 80] but was {config.FieldOfView}");
        }

        [Test]
        public void CameraConfigData_BallTrackingWeight_IsGreaterThanPlayerWeight()
        {
            var config = new CameraConfigData();
            Assert.Greater(config.BallTrackingWeight, config.PlayerTrackingWeight,
                $"Ball tracking weight ({config.BallTrackingWeight}) should be greater " +
                $"than player tracking weight ({config.PlayerTrackingWeight})");
        }

        [Test]
        public void CameraConfigData_MinHeight_IsPositive()
        {
            var config = new CameraConfigData();
            Assert.Greater(config.MinHeight, 0f,
                $"Min camera height should be > 0 but was {config.MinHeight}");
        }

        [Test]
        public void CameraConfigData_CustomValues_AreRetained()
        {
            var config = new CameraConfigData(
                elevationAngle: 40f,
                followDamping: 2f,
                distance: 30f,
                fieldOfView: 55f,
                ballTrackingWeight: 1.5f,
                playerTrackingWeight: 0.8f,
                minHeight: 5f
            );

            Assert.AreEqual(40f, config.ElevationAngle, 0.01f);
            Assert.AreEqual(2f, config.FollowDamping, 0.01f);
            Assert.AreEqual(30f, config.Distance, 0.01f);
            Assert.AreEqual(55f, config.FieldOfView, 0.01f);
            Assert.AreEqual(1.5f, config.BallTrackingWeight, 0.01f);
            Assert.AreEqual(0.8f, config.PlayerTrackingWeight, 0.01f);
            Assert.AreEqual(5f, config.MinHeight, 0.01f);
        }
    }
}
