using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    [TestFixture]
    [Category("FID-SIM")]
    public class KickSolverTests
    {
        private static readonly BallSimConfig Ball = new BallSimConfig();

        private static SimActionConfig NoError()
        {
            var c = new SimActionConfig();
            c.PassErrorDeg = c.ThroughErrorDeg = c.LobErrorDeg = c.CrossErrorDeg = 0f;
            c.ShotErrorDeg = c.FinesseErrorDeg = c.DrivenErrorDeg = 0f;
            return c;
        }

        private static SimKickRequest Request(IntentAction action, float power, SimVec3 target, bool hasTarget)
        {
            return new SimKickRequest
            {
                Action = action,
                Power = power,
                Aim = new SimVec3(0f, 0f, 1f),
                TargetPoint = target,
                HasTarget = hasTarget,
                KickerPosition = new SimVec3(0f, 0f, -0.4f),
                KickerFacing = 0f,
                BallPosition = new SimVec3(0f, Ball.Radius, 0f),
            };
        }

        private static SimBall Kick(SimKickResult r)
        {
            return new SimBall
            {
                Position = new SimVec3(0f, Ball.Radius, 0f),
                Velocity = r.Velocity,
                Spin = r.Spin,
                Grounded = r.Velocity.Y <= 0.01f,
            };
        }

        [Test]
        public void Solve_AssistedGroundPass20m_ArrivesNearTargetStillMoving()
        {
            var target = new SimVec3(6f, 0f, 19f);
            var r = KickSolver.Solve(Request(IntentAction.Pass, 0.5f, target, true), new SimPlayerAttributes(), NoError(), Ball, new SimRng(1));
            var b = Kick(r);
            float closest = float.MaxValue;
            float speedAtClosest = 0f;
            for (int i = 0; i < 1200; i++)
            {
                BallIntegrator.Step(ref b, Ball, SimTestHelpers.HugePitch, SimTestHelpers.BallDt);
                float d = SimMath.FlatDistance(b.Position, target);
                if (d < closest) { closest = d; speedAtClosest = b.Velocity.Magnitude; }
            }

            Assert.Less(closest, 0.5f);
            Assert.That(speedAtClosest, Is.InRange(2.5f, 10f), "pass should arrive firm but receivable");
        }

        [Test]
        public void Solve_FinesseRightFoot_CurlsBackOntoTarget()
        {
            var target = new SimVec3(0f, 1.5f, 20f);
            var req = Request(IntentAction.FinesseShot, 0.6f, target, true);
            var r = KickSolver.Solve(req, new SimPlayerAttributes { PreferredFoot = SimFoot.Right }, NoError(), Ball, new SimRng(1));
            Assert.Less(r.Spin.Y, 0f, "right-footed finesse curls right to left");
            Assert.Greater(r.Velocity.X, 0f, "pre-aimed outside the target");
            var b = Kick(r);
            int guard = 0;
            while (b.Position.Z < 20f && guard++ < 1000) BallIntegrator.Step(ref b, Ball, SimTestHelpers.HugePitch, SimTestHelpers.BallDt);
            Assert.AreEqual(0f, b.Position.X, 1.0f, "ball should curl back to the target line");
        }

        [Test]
        public void ErrorSigma_WeakFoot_LargerThanStrongFoot()
        {
            var a = new SimPlayerAttributes { WeakFoot = 2 };
            var req = Request(IntentAction.Shoot, 0.6f, SimVec3.Zero, false);
            var cfg = new SimActionConfig();
            Assert.Greater(KickSolver.ErrorSigma(req, a, cfg, 0.6f, true, 18f), KickSolver.ErrorSigma(req, a, cfg, 0.6f, false, 18f) * 1.5f);
        }

        [Test]
        public void ErrorSigma_UnderPressure_LargerThanFree()
        {
            var a = new SimPlayerAttributes();
            var cfg = new SimActionConfig();
            var free = Request(IntentAction.Pass, 0.5f, SimVec3.Zero, false);
            var pressed = free;
            pressed.Pressure = 1f;
            Assert.Greater(KickSolver.ErrorSigma(pressed, a, cfg, 0.5f, false, 10f), KickSolver.ErrorSigma(free, a, cfg, 0.5f, false, 10f));
        }

        [Test]
        public void ErrorSigma_HigherSkill_Smaller()
        {
            var cfg = new SimActionConfig();
            var req = Request(IntentAction.Pass, 0.5f, SimVec3.Zero, false);
            Assert.Less(KickSolver.ErrorSigma(req, new SimPlayerAttributes { ShortPassing = 92 }, cfg, 0.5f, false, 10f),
                KickSolver.ErrorSigma(req, new SimPlayerAttributes { ShortPassing = 45 }, cfg, 0.5f, false, 10f) * 0.5f);
        }

        [Test]
        public void Solve_ManyShots_SpreadMatchesSkill()
        {
            float Spread(int finishing)
            {
                var rng = new SimRng(99);
                var a = new SimPlayerAttributes { Finishing = finishing, LongShots = finishing };
                float sum = 0f;
                for (int i = 0; i < 400; i++)
                {
                    var r = KickSolver.Solve(Request(IntentAction.Shoot, 0.6f, SimVec3.Zero, false), a, new SimActionConfig(), Ball, rng);
                    sum += SimMath.Abs(r.HorizontalError);
                }

                return sum / 400f;
            }

            Assert.Less(Spread(95), Spread(40));
        }

        [Test]
        public void Solve_FullPowerShot_FasterThanHalfPower()
        {
            var a = new SimPlayerAttributes();
            var hi = KickSolver.Solve(Request(IntentAction.Shoot, 1f, SimVec3.Zero, false), a, NoError(), Ball, new SimRng(3));
            var lo = KickSolver.Solve(Request(IntentAction.Shoot, 0.5f, SimVec3.Zero, false), a, NoError(), Ball, new SimRng(3));
            Assert.Greater(hi.Velocity.Magnitude, lo.Velocity.Magnitude + 3f);
            Assert.That(hi.Velocity.Magnitude, Is.InRange(25f, 36f));
        }

        [Test]
        public void ShotQuality_CloseCentral_HigherThanLongRangeAndTightAngle()
        {
            var pitch = PitchSpec.Standard;
            float close = ShotQuality.Estimate(new SimVec3(0f, 0f, pitch.HalfLength - 6f), 1, pitch, 0f, false);
            float spot = ShotQuality.Estimate(new SimVec3(0f, 0f, pitch.HalfLength - 11f), 1, pitch, 0f, false);
            float far = ShotQuality.Estimate(new SimVec3(0f, 0f, pitch.HalfLength - 25f), 1, pitch, 0f, false);
            float tight = ShotQuality.Estimate(new SimVec3(15f, 0f, pitch.HalfLength - 2f), 1, pitch, 0f, false);
            Assert.That(close, Is.InRange(0.55f, 0.9f));
            Assert.That(spot, Is.InRange(0.25f, 0.5f));
            Assert.That(far, Is.InRange(0.03f, 0.12f));
            Assert.Less(tight, spot);
        }

        [Test]
        public void SimRng_SameSeed_SameSequence_DifferentSeed_Differs()
        {
            var a = new SimRng(42);
            var b = new SimRng(42);
            var c = new SimRng(43);
            bool anyDiff = false;
            for (int i = 0; i < 100; i++)
            {
                uint x = a.NextUInt();
                Assert.AreEqual(x, b.NextUInt());
                if (x != c.NextUInt()) anyDiff = true;
            }

            Assert.IsTrue(anyDiff);
        }

        [Test]
        public void SimRng_NextFloat_InUnitRangeWithSaneMean()
        {
            var r = new SimRng(7);
            float sum = 0f;
            for (int i = 0; i < 10000; i++)
            {
                float f = r.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                sum += f;
            }

            Assert.AreEqual(0.5f, sum / 10000f, 0.02f);
        }
    }
}
