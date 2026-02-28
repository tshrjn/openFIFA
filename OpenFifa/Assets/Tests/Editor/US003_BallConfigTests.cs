using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US003")]
    public class US003_BallConfigTests
    {
        [Test]
        public void BallPhysicsData_DefaultMass_Is0Point43()
        {
            var data = new BallPhysicsData();
            Assert.AreEqual(0.43f, data.Mass, 0.001f,
                $"Default ball mass should be 0.43 kg but was {data.Mass} kg");
        }

        [Test]
        public void BallPhysicsData_DefaultDrag_Is0Point1()
        {
            var data = new BallPhysicsData();
            Assert.AreEqual(0.1f, data.Drag, 0.001f,
                $"Default ball drag should be 0.1 but was {data.Drag}");
        }

        [Test]
        public void BallPhysicsData_DefaultAngularDrag_Is0Point5()
        {
            var data = new BallPhysicsData();
            Assert.AreEqual(0.5f, data.AngularDrag, 0.001f,
                $"Default ball angular drag should be 0.5 but was {data.AngularDrag}");
        }

        [Test]
        public void BallPhysicsData_DefaultBounciness_Is0Point8()
        {
            var data = new BallPhysicsData();
            Assert.AreEqual(0.8f, data.Bounciness, 0.001f,
                $"Default ball bounciness should be 0.8 but was {data.Bounciness}");
        }

        [Test]
        public void BallPhysicsData_DefaultDynamicFriction_Is0Point5()
        {
            var data = new BallPhysicsData();
            Assert.AreEqual(0.5f, data.DynamicFriction, 0.001f,
                $"Default ball dynamic friction should be 0.5 but was {data.DynamicFriction}");
        }

        [Test]
        public void BallPhysicsData_DefaultStaticFriction_Is0Point5()
        {
            var data = new BallPhysicsData();
            Assert.AreEqual(0.5f, data.StaticFriction, 0.001f,
                $"Default ball static friction should be 0.5 but was {data.StaticFriction}");
        }

        [Test]
        public void BallState_DefaultState_IsFree()
        {
            Assert.AreEqual(BallState.Free, default(BallState),
                "Default BallState should be Free (0)");
        }

        [Test]
        public void BallState_EnumValues_HasAllExpectedValues()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(BallState), BallState.Free));
            Assert.IsTrue(System.Enum.IsDefined(typeof(BallState), BallState.Possessed));
            Assert.IsTrue(System.Enum.IsDefined(typeof(BallState), BallState.InFlight));
        }

        [Test]
        public void BallPhysicsData_CustomValues_AreRetained()
        {
            var data = new BallPhysicsData(
                mass: 0.5f,
                drag: 0.2f,
                angularDrag: 0.8f,
                bounciness: 0.7f,
                dynamicFriction: 0.6f,
                staticFriction: 0.7f
            );

            Assert.AreEqual(0.5f, data.Mass, 0.001f, "Custom mass not retained");
            Assert.AreEqual(0.2f, data.Drag, 0.001f, "Custom drag not retained");
            Assert.AreEqual(0.8f, data.AngularDrag, 0.001f, "Custom angular drag not retained");
            Assert.AreEqual(0.7f, data.Bounciness, 0.001f, "Custom bounciness not retained");
            Assert.AreEqual(0.6f, data.DynamicFriction, 0.001f, "Custom dynamic friction not retained");
            Assert.AreEqual(0.7f, data.StaticFriction, 0.001f, "Custom static friction not retained");
        }
    }
}
