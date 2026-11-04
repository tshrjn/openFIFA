using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    /// <summary>Ball-flight regression tests for the PR #5 review (items 2, 11, 12, 13) and the AI prediction API.</summary>
    [TestFixture]
    [Category("FID_SIM")]
    public class BallFlightTests
    {
        private static readonly BallSimConfig Ball = new BallSimConfig();

        private static SimActionConfig NoError()
        {
            var c = new SimActionConfig();
            c.PassErrorDeg = c.ThroughErrorDeg = c.LobErrorDeg = c.CrossErrorDeg = 0f;
            c.ShotErrorDeg = c.FinesseErrorDeg = c.DrivenErrorDeg = 0f;
            return c;
        }

        private static SimKickRequest Request(IntentAction action, float power, SimVec3 target, bool hasTarget, float ballX = 0f)
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
                BallPosition = new SimVec3(ballX, Ball.Radius, 0f),
            };
        }

        /// <summary>Rolls a kicked ball; returns the speed and time when it passes <paramref name="z"/> (speed -1 if it stops short).</summary>
        private static float SpeedAtZ(SimKickResult r, float z, out float time)
        {
            var b = new SimBall
            {
                Position = new SimVec3(0f, Ball.Radius, 0f),
                Velocity = r.Velocity,
                Spin = r.Spin,
                Grounded = r.Velocity.Y <= 0.01f,
            };
            for (int i = 0; i < 2400; i++)
            {
                BallIntegrator.Step(ref b, Ball, SimTestHelpers.HugePitch, SimTestHelpers.BallDt);
                if (b.Position.Z >= z)
                {
                    time = (i + 1) * SimTestHelpers.BallDt;
                    return b.Velocity.Flat.Magnitude;
                }

                if (b.Velocity.SqrMagnitude == 0f) break;
            }

            time = float.PositiveInfinity;
            return -1f;
        }

        // ---- Review item 2: ground passes reach the target at the intended speed. ----

        [Test]
        public void Solve_GroundPass30m_LowMidAndFullPower_ArrivesWithIntendedSpeed()
        {
            foreach (float power in new[] { 0f, 0.5f, 1f }) GroundPass30m(power);
        }

        private static void GroundPass30m(float power)
        {
            var ac = NoError();
            var r = KickSolver.Solve(Request(IntentAction.Pass, power, new SimVec3(0f, 0f, 30f), true),
                new SimPlayerAttributes { ShortPassing = 99, LongPassing = 99 }, ac, Ball, new SimRng(3));
            float arrive = ac.PassArrivalSpeedMin + ac.PassArrivalSpeedRange * power;
            float v = SpeedAtZ(r, 30f, out float time);
            Assert.Greater(v, 0f, $"power {power}: the pass stopped short of 30 m");
            Assert.AreEqual(arrive, v, 0.5f, $"power {power}: arrival speed");
            Assert.Less(time, power >= 1f ? 3.2f : 6f, $"power {power}: travel time");
        }

        [Test]
        public void Solve_GroundPass45m_StillReachesTarget()
        {
            var r = KickSolver.Solve(Request(IntentAction.Pass, 0.2f, new SimVec3(0f, 0f, 45f), true),
                new SimPlayerAttributes { ShortPassing = 99, LongPassing = 99 }, NoError(), Ball, new SimRng(3));
            Assert.Greater(SpeedAtZ(r, 45f, out _), 0f);
        }

        // ---- Review item 11: the weak foot is never stronger than the strong foot. ----

        [Test]
        public void Solve_FiveStarWeakFoot_NotFasterThanStrongFoot()
        {
            var a = new SimPlayerAttributes { WeakFoot = 5, PreferredFoot = SimFoot.Right };
            var strong = KickSolver.Solve(Request(IntentAction.Shoot, 1f, default, false, 0.3f), a, NoError(), Ball, new SimRng(5));
            var weak = KickSolver.Solve(Request(IntentAction.Shoot, 1f, default, false, -0.3f), a, NoError(), Ball, new SimRng(5));
            Assert.IsTrue(weak.UsedWeakFoot);
            Assert.IsFalse(strong.UsedWeakFoot);
            Assert.LessOrEqual(weak.Velocity.Magnitude, strong.Velocity.Magnitude + 1e-4f);
        }

        // ---- Review item 12: no tunnelling through the frame at max speed. ----

        [Test]
        public void Step_45msBallAtPost_NeverTunnelsForAnyPhase()
        {
            var pitch = PitchSpec.Standard;
            float px = pitch.GoalWidth * 0.5f + Ball.PostRadius;
            int tunnelled = 0;
            for (int k = 0; k < 40; k++)
            {
                float offset = k * (45f * SimTestHelpers.BallDt / 40f);
                var b = new SimBall
                {
                    Position = new SimVec3(px, 1.0f, pitch.HalfLength - 3f - offset),
                    Velocity = new SimVec3(0f, 0.3f, 45f),
                };
                bool hit = false;
                for (int i = 0; i < 40; i++)
                    if ((BallIntegrator.Step(ref b, Ball, pitch, SimTestHelpers.BallDt) & BallStepFlags.HitPost) != 0) hit = true;
                if (!hit || b.Velocity.Z > 0f) tunnelled++;
            }

            Assert.AreEqual(0, tunnelled, "phases where the ball passed through the post");
        }

        [Test]
        public void Step_45msBallAtCrossbar_NeverTunnels()
        {
            var pitch = PitchSpec.Standard;
            float barY = pitch.GoalHeight + Ball.PostRadius;
            int tunnelled = 0;
            for (int k = 0; k < 40; k++)
            {
                float offset = k * (45f * SimTestHelpers.BallDt / 40f);
                var b = new SimBall
                {
                    Position = new SimVec3(0f, barY, pitch.HalfLength - 3f - offset),
                    Velocity = new SimVec3(0f, 0.45f, 45f),
                };
                bool hit = false;
                for (int i = 0; i < 40; i++)
                    if ((BallIntegrator.Step(ref b, Ball, pitch, SimTestHelpers.BallDt) & BallStepFlags.HitCrossbar) != 0) hit = true;
                if (!hit || b.Velocity.Z > 0f) tunnelled++;
            }

            Assert.AreEqual(0, tunnelled);
        }

        // ---- AI prediction API (BallFlight) agrees with the integrator. ----

        private static float RollTime(float speed, float distance, out float arrival)
        {
            var v = new SimVec3(0f, 0f, speed);
            var b = new SimBall { Position = new SimVec3(0f, Ball.Radius, 0f), Velocity = v,
                Spin = BallIntegrator.RollingSpin(v, Ball.Radius, 0f), Grounded = true };
            for (int i = 0; i < 6000; i++)
            {
                BallIntegrator.Step(ref b, Ball, SimTestHelpers.HugePitch, SimTestHelpers.BallDt);
                if (b.Position.Z >= distance) { arrival = b.Velocity.Magnitude; return (i + 1) * SimTestHelpers.BallDt; }
                if (b.Velocity.SqrMagnitude == 0f) break;
            }

            arrival = 0f;
            return float.PositiveInfinity;
        }

        [Test]
        public void GroundPassTime_MatchesDirectIntegration()
        {
            foreach (var c in new[] { (12f, 20f), (15f, 30f), (24f, 40f), (8f, 10f) })
            {
                float direct = RollTime(c.Item1, c.Item2, out float arrival);
                Assert.AreEqual(direct, BallFlight.GroundPassTime(Ball, c.Item2, c.Item1), 0.02f, $"v={c.Item1} d={c.Item2}");
                Assert.AreEqual(arrival, BallFlight.GroundSpeedAfter(Ball, c.Item1, c.Item2), 0.1f);
            }
        }

        [Test]
        public void GroundPassTime_BallStopsShort_IsInfinite()
        {
            Assert.IsTrue(float.IsPositiveInfinity(BallFlight.GroundPassTime(Ball, 60f, 5f)));
            Assert.Less(BallFlight.GroundRollDistance(Ball, 5f), 60f);
        }

        [Test]
        public void GroundPassSpeedFor_RoundTripsThroughIntegrator()
        {
            foreach (var c in new[] { (10f, 4f), (30f, 4f), (30f, 9f), (45f, 6f) })
            {
                float v0 = BallFlight.GroundPassSpeedFor(Ball, c.Item1, c.Item2);
                RollTime(v0, c.Item1, out float arrival);
                Assert.AreEqual(c.Item2, arrival, 0.15f, $"d={c.Item1} arrive={c.Item2}");
            }
        }

        [Test]
        public void PredictBall_EqualsSteppingTheIntegrator()
        {
            var b = new SimBall { Position = new SimVec3(1f, 0.5f, 2f), Velocity = new SimVec3(3f, 8f, 20f), Spin = new SimVec3(0f, 20f, 0f) };
            var manual = b;
            for (int i = 0; i < 120; i++) BallIntegrator.Step(ref manual, Ball, PitchSpec.Standard, SimTestHelpers.BallDt);
            var predicted = BallFlight.PredictBall(b, Ball, PitchSpec.Standard, 1f);
            Assert.AreEqual(manual.Position, predicted.Position);
            Assert.AreEqual(manual.Velocity, predicted.Velocity);
            float t = BallFlight.TimeToFlatDistance(b, Ball, PitchSpec.Standard, 10f, 3f, out var at);
            Assert.Less(t, 1f);
            Assert.GreaterOrEqual((at.Position - b.Position).Flat.Magnitude, 10f);
        }

        // ---- Review item 13: grounded Magnus (documented as not applied). ----

        [Test]
        public void Step_RollingBallWithSideSpin_NoMagnusCurlOnTheGround()
        {
            var v = new SimVec3(0f, 0f, 12f);
            SimVec3 spin = BallIntegrator.RollingSpin(v, Ball.Radius, 40f);
            var b = new SimBall { Position = new SimVec3(0f, Ball.Radius, 0f), Velocity = v, Spin = spin, Grounded = true };
            for (int i = 0; i < 240; i++) BallIntegrator.Step(ref b, Ball, SimTestHelpers.HugePitch, SimTestHelpers.BallDt);
            Assert.AreEqual(0f, b.Position.X, 1e-3f);
        }
    }
}
