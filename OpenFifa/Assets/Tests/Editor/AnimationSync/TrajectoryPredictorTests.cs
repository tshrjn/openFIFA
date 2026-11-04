using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID-ANIM")]
    public class TrajectoryPredictorTests
    {
        [Test]
        public void Predict_ConstantVelocityAtIntent_ExtrapolatesLinearly()
        {
            var p = new TrajectoryPredictor();
            var v = new SimVec3(0f, 0f, 5f);
            TrajectorySample s = p.Predict(SimVec3.Zero, v, SimVec3.Zero, v, 0f, 0f, 0f, 1f);
            Assert.AreEqual(5f, s.Position.Z, 1e-3f);
            Assert.AreEqual(0f, s.Position.X, 1e-4f);
            Assert.AreEqual(5f, s.Velocity.Z, 1e-3f);
        }

        [Test]
        public void Predict_ZeroHorizon_ReturnsCurrentState()
        {
            var p = new TrajectoryPredictor();
            TrajectorySample s = p.Predict(new SimVec3(1f, 0f, 2f), new SimVec3(3f, 0f, 0f), SimVec3.Zero,
                new SimVec3(0f, 0f, 6f), 30f, 0f, 90f, 0f);
            Assert.AreEqual(1f, s.Position.X, 1e-4f);
            Assert.AreEqual(2f, s.Position.Z, 1e-4f);
            Assert.AreEqual(30f, s.Yaw, 1e-3f);
        }

        [Test]
        public void Predict_FromRestTowardSprint_ConvergesAndLagsBehindIntent()
        {
            var p = new TrajectoryPredictor();
            var goal = new SimVec3(0f, 0f, 7f);
            TrajectorySample s = p.Predict(SimVec3.Zero, SimVec3.Zero, SimVec3.Zero, goal, 0f, 0f, 0f, 1f);
            Assert.Less(s.Position.Z, 7f);
            Assert.Greater(s.Position.Z, 4f);
            Assert.AreEqual(7f, s.Velocity.Z, 0.1f); // 5 velocity halflives: within ~1.5% of intent
        }

        [Test]
        public void Predict_Turn_YawConvergesToIntentAcrossWrap()
        {
            var p = new TrajectoryPredictor();
            TrajectorySample s = p.Predict(SimVec3.Zero, SimVec3.Zero, SimVec3.Zero, SimVec3.Zero, 170f, 0f, -170f, 2f);
            Assert.AreEqual(0f, AnimMath.DeltaAngle(s.Yaw, -170f), 0.1f);
        }
    }
}
