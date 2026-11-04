using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    [TestFixture]
    [Category("FID_SIM")]
    public class BallIntegratorTests
    {
        private static readonly BallSimConfig Cfg = new BallSimConfig();
        private static readonly PitchSpec Huge = SimTestHelpers.HugePitch;
        private const float Dt = SimTestHelpers.BallDt;

        private static SimBall Launch(float speed, float elevationDeg, SimVec3 spin)
        {
            float e = elevationDeg * SimMath.Deg2Rad;
            return new SimBall
            {
                Position = new SimVec3(0f, Cfg.Radius, 0f),
                Velocity = new SimVec3(0f, speed * (float)System.Math.Sin(e), speed * (float)System.Math.Cos(e)),
                Spin = spin,
                Grounded = false,
            };
        }

        [Test]
        public void Step_GroundPass12ms_RollsBetween25And50m()
        {
            var b = SimBall.AtRest(SimVec3.Zero, Cfg.Radius);
            b.Velocity = new SimVec3(0f, 0f, 12f);
            b.Spin = BallIntegrator.RollingSpin(b.Velocity, Cfg.Radius, 0f);
            b = SimTestHelpers.RollUntilStopped(b, Cfg, Huge);
            Assert.That(b.Position.Z, Is.InRange(25f, 50f), $"rolled {b.Position.Z} m");
            Assert.AreEqual(0f, b.Position.X, 1e-4f);
        }

        [Test]
        public void Step_Lofted25ms30deg_CarriesBetween35And52m()
        {
            var b = SimTestHelpers.FlyUntilFirstBounce(Launch(25f, 30f, SimVec3.Zero), Cfg, Huge);
            // Vacuum range would be 55 m; drag must shorten it.
            Assert.That(b.Position.Z, Is.InRange(35f, 52f), $"carry {b.Position.Z} m");
        }

        [Test]
        public void Step_LongBall30ms20deg_CarriesBetween38And60m()
        {
            var b = SimTestHelpers.FlyUntilFirstBounce(Launch(30f, 20f, SimVec3.Zero), Cfg, Huge);
            Assert.That(b.Position.Z, Is.InRange(38f, 60f), $"carry {b.Position.Z} m");
        }

        [Test]
        public void LoftedSpeed_SolvedForDistance_LandsWithinHalfMetre()
        {
            foreach (float d in new[] { 15f, 30f, 45f })
            {
                float v = KickSolver.LoftedSpeed(d, 35f * SimMath.Deg2Rad, -8f, Cfg);
                float carry = KickSolver.Carry(v, 35f * SimMath.Deg2Rad, -8f, Cfg);
                Assert.AreEqual(d, carry, 0.5f, $"target {d} m");
            }
        }

        [Test]
        public void DragCoefficient_AcrossDragCrisis_DropsFromLowToHighSpeedValue()
        {
            Assert.AreEqual(0.45f, BallIntegrator.DragCoefficient(Cfg, 5f), 1e-6f);
            Assert.AreEqual(0.22f, BallIntegrator.DragCoefficient(Cfg, 25f), 1e-6f);
            float mid = BallIntegrator.DragCoefficient(Cfg, 12f);
            Assert.That(mid, Is.InRange(0.22f, 0.45f));
        }

        [Test]
        public void Step_PositiveYSpinOnForwardBall_CurlsTowardPositiveX()
        {
            var b = Launch(25f, 8f, new SimVec3(0f, 60f, 0f));
            for (int i = 0; i < 120 && b.Position.Z < 25f; i++) BallIntegrator.Step(ref b, Cfg, Huge, Dt);
            Assert.Greater(b.Position.X, 0.5f);
        }

        [Test]
        public void Step_NegativeYSpinOnForwardBall_CurlsTowardNegativeX()
        {
            var b = Launch(25f, 8f, new SimVec3(0f, -60f, 0f));
            for (int i = 0; i < 120 && b.Position.Z < 25f; i++) BallIntegrator.Step(ref b, Cfg, Huge, Dt);
            Assert.Less(b.Position.X, -0.5f);
        }

        [Test]
        public void Step_FreeKick25ms60radPerSec_Bends2To4mOver25m()
        {
            var b = Launch(25f, 10f, new SimVec3(0f, 60f, 0f));
            int guard = 0;
            while (b.Position.Z < 25f && guard++ < 1000) BallIntegrator.Step(ref b, Cfg, Huge, Dt);
            Assert.That(b.Position.X, Is.InRange(2f, 4f), $"bend {b.Position.X} m");
        }

        [Test]
        public void Step_Topspin_DipsShorterThanNoSpin_BackspinFloatsLonger()
        {
            var right = new SimVec3(1f, 0f, 0f); // topspin axis for a +Z ball
            float none = SimTestHelpers.FlyUntilFirstBounce(Launch(25f, 25f, SimVec3.Zero), Cfg, Huge).Position.Z;
            float top = SimTestHelpers.FlyUntilFirstBounce(Launch(25f, 25f, right * 50f), Cfg, Huge).Position.Z;
            float back = SimTestHelpers.FlyUntilFirstBounce(Launch(25f, 25f, right * -50f), Cfg, Huge).Position.Z;
            Assert.Less(top, none - 1f);
            Assert.Greater(back, none + 1f);
        }

        [Test]
        public void Step_DroppedBall_BounceApexesDecay()
        {
            var b = new SimBall { Position = new SimVec3(0f, 3f, 0f) };
            float lastApex = 3f;
            int apexes = 0;
            float prevVy = 0f;
            for (int i = 0; i < 1200 && apexes < 3; i++)
            {
                BallIntegrator.Step(ref b, Cfg, Huge, Dt);
                if (prevVy > 0f && b.Velocity.Y <= 0f)
                {
                    float apex = b.Position.Y;
                    Assert.Less(apex, lastApex * 0.6f, $"apex {apexes}");
                    Assert.Greater(apex, lastApex * 0.2f, $"apex {apexes}");
                    lastApex = apex;
                    apexes++;
                }

                prevVy = b.Velocity.Y;
            }

            Assert.AreEqual(3, apexes);
        }

        [Test]
        public void Step_DroppedBall_EventuallyRestsOnGround()
        {
            var b = new SimBall { Position = new SimVec3(0f, 2f, 0f) };
            for (int i = 0; i < 1200; i++) BallIntegrator.Step(ref b, Cfg, Huge, Dt);
            Assert.IsTrue(b.Grounded);
            Assert.AreEqual(Cfg.Radius, b.Position.Y, 1e-4f);
        }

        [Test]
        public void Step_BounceWithTopspin_KicksOnFasterThanBackspin()
        {
            var right = new SimVec3(1f, 0f, 0f);
            float Bounce(float spin)
            {
                var b = new SimBall { Position = new SimVec3(0f, 0.3f, 0f), Velocity = new SimVec3(0f, -6f, 10f), Spin = right * spin };
                for (int i = 0; i < 60; i++)
                {
                    if ((BallIntegrator.Step(ref b, Cfg, Huge, Dt) & BallStepFlags.Bounced) != 0) return b.Velocity.Z;
                }

                return float.NaN;
            }

            float top = Bounce(150f);
            float back = Bounce(-60f);
            Assert.Greater(top, back + 1f);
        }

        [Test]
        public void Step_BallIntoCrossbar_RebouncesWithCrossbarFlag()
        {
            var pitch = PitchSpec.Standard;
            float barY = pitch.GoalHeight + Cfg.PostRadius;
            var b = new SimBall { Position = new SimVec3(0f, barY, pitch.HalfLength - 1f), Velocity = new SimVec3(0f, 0f, 20f) };
            BallStepFlags all = BallStepFlags.None;
            for (int i = 0; i < 60; i++) all |= BallIntegrator.Step(ref b, Cfg, pitch, Dt);
            Assert.IsTrue((all & BallStepFlags.HitCrossbar) != 0);
            Assert.Less(b.Velocity.Z, 0f, "the bar sends the ball back out");
            Assert.AreEqual(0, b.InNet);
        }

        [Test]
        public void Step_BallIntoPost_ReflectsAndDoesNotScore()
        {
            var pitch = PitchSpec.Standard;
            float px = pitch.GoalWidth * 0.5f + Cfg.PostRadius;
            var b = new SimBall { Position = new SimVec3(px, 1f, pitch.HalfLength - 4f), Velocity = new SimVec3(0f, 0f, 20f) };
            BallStepFlags all = BallStepFlags.None;
            for (int i = 0; i < 40; i++) all |= BallIntegrator.Step(ref b, Cfg, pitch, Dt);
            Assert.IsTrue((all & BallStepFlags.HitPost) != 0);
            Assert.Less(b.Velocity.Z, 0f);
            Assert.AreEqual(0, b.InNet);
        }

        [Test]
        public void Step_ShotIntoGoal_IsCapturedByNet()
        {
            var pitch = PitchSpec.Standard;
            var b = new SimBall { Position = new SimVec3(0f, 1f, pitch.HalfLength - 10f), Velocity = new SimVec3(0f, 0f, 28f) };
            BallStepFlags all = BallStepFlags.None;
            for (int i = 0; i < 360; i++) all |= BallIntegrator.Step(ref b, Cfg, pitch, Dt);
            Assert.IsTrue((all & BallStepFlags.EnteredNet) != 0);
            Assert.AreEqual(1, b.InNet);
            Assert.That(b.Position.Z, Is.InRange(pitch.HalfLength + Cfg.Radius, pitch.HalfLength + Cfg.NetDepth));
            Assert.Less(b.Velocity.Magnitude, 1f);
        }

        [Test]
        public void Step_SameInputs_BitIdentical()
        {
            var a = Launch(27f, 14f, new SimVec3(5f, -40f, 3f));
            var b = a;
            for (int i = 0; i < 2000; i++)
            {
                BallIntegrator.Step(ref a, Cfg, PitchSpec.Standard, Dt);
                BallIntegrator.Step(ref b, Cfg, PitchSpec.Standard, Dt);
            }

            Assert.IsTrue(a.Position.Equals(b.Position) && a.Velocity.Equals(b.Velocity) && a.Spin.Equals(b.Spin));
        }
    }
}
