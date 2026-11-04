using System;
using NUnit.Framework;
using OpenFifa.Core;
using OpenFifa.Gameplay;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID_ANIM")]
    public class AnimSnapshotInterpolatorTests
    {
        private const float Dt = 1f / 60f;

        private static AnimSnapshotInterpolator TwoSteps(float yaw0 = 0f, float yaw1 = 0f)
        {
            var i = new AnimSnapshotInterpolator();
            i.Push(0f, new SimVec3(0f, 0f, 0f), yaw0);
            i.Push(Dt, new SimVec3(0.1f, 0f, 0f), yaw1);
            return i;
        }

        [Test]
        public void Sample_AlphaZero_ReturnsPreviousSnapshot()
        {
            var s = TwoSteps().Sample(0f, Dt);
            Assert.AreEqual(0f, s.Position.X, 1e-6f);
            Assert.AreEqual(0f, s.Time, 1e-6f);
        }

        [Test]
        public void Sample_AlphaOne_ReturnsCurrentSnapshot()
        {
            var s = TwoSteps().Sample(1f, Dt);
            Assert.AreEqual(0.1f, s.Position.X, 1e-6f);
            Assert.AreEqual(Dt, s.Time, 1e-6f);
        }

        [Test]
        public void Sample_120HzFrames_MoveEvenlyBetween60HzSteps()
        {
            // Two render frames per sim step: alpha 0.5 then 1.0 must give evenly spaced positions (no stutter).
            var i = new AnimSnapshotInterpolator();
            float prevX = 0f;
            i.Push(0f, SimVec3.Zero, 0f);
            for (int step = 1; step <= 4; step++)
            {
                i.Push(step * Dt, new SimVec3(step * 0.1f, 0f, 0f), 0f);
                foreach (float alpha in new[] { 0.5f, 1f })
                {
                    float x = i.Sample(alpha, Dt).Position.X;
                    if (step > 1 || alpha > 0.5f) Assert.AreEqual(0.05f, x - prevX, 1e-5f);
                    prevX = x;
                }
            }
        }

        [Test]
        public void Sample_SkippedSteps_InterpolatesInTime()
        {
            // 30 Hz render of a 60 Hz sim: the frame saw t=0 then t=2dt. Alpha 0.5 means render time 1.5 dt.
            var i = new AnimSnapshotInterpolator();
            i.Push(0f, SimVec3.Zero, 0f);
            i.Push(2 * Dt, new SimVec3(0.2f, 0f, 0f), 0f);
            var s = i.Sample(0.5f, Dt);
            Assert.AreEqual(0.15f, s.Position.X, 1e-5f);
            Assert.AreEqual(1.5f * Dt, s.Time, 1e-6f);
        }

        [Test]
        public void Sample_YawAcrossPi_TakesShortestWay()
        {
            float a = (float)Math.PI - 0.1f;
            float b = -(float)Math.PI + 0.1f;
            float yaw = TwoSteps(a, b).Sample(0.5f, Dt).YawRad;
            Assert.AreEqual(0f, AnimSnapshotInterpolator.WrapPi(yaw - (float)Math.PI), 1e-4f);
        }

        [Test]
        public void Push_Teleport_SnapsWithoutBlending()
        {
            var i = new AnimSnapshotInterpolator();
            i.Push(0f, SimVec3.Zero, 0f);
            i.Push(Dt, new SimVec3(30f, 0f, 0f), 0f);
            Assert.AreEqual(30f, i.Sample(0f, Dt).Position.X, 1e-6f);
        }

        [Test]
        public void Push_SameTime_KeepsPreviousSnapshot()
        {
            var i = TwoSteps();
            i.Push(Dt, new SimVec3(0.1f, 0f, 0f), 0f);
            Assert.AreEqual(0.05f, i.Sample(0.5f, Dt).Position.X, 1e-6f);
        }

        [Test]
        public void Push_ClockGoesBack_Restarts()
        {
            var i = TwoSteps();
            i.Push(0f, new SimVec3(5f, 0f, 0f), 0f);
            Assert.AreEqual(5f, i.Sample(0f, Dt).Position.X, 1e-6f);
        }

        [Test]
        public void SimYawToDegrees_ConvertsRadians()
        {
            Assert.AreEqual(90f, PlayerAnimationDriver.SimYawToDegrees((float)(Math.PI / 2)), 1e-4f);
            Assert.AreEqual(-180f, PlayerAnimationDriver.SimYawToDegrees(-(float)Math.PI), 1e-4f);
            // Same orientation convention as AnimMath: sim yaw of +X is +90 degrees.
            Assert.AreEqual(AnimMath.YawOf(new SimVec3(1f, 0f, 0f)),
                PlayerAnimationDriver.SimYawToDegrees(SimMath.Yaw(new SimVec3(1f, 0f, 0f))), 1e-4f);
        }
    }
}
