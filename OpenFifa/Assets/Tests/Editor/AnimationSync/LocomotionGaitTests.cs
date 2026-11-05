using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID_ANIM")]
    public class LocomotionGaitTests
    {
        private static float Mag(float x, float z) => (float)Math.Sqrt(x * x + z * z);

        [Test]
        public void Solve_NearlyStationary_Idles()
        {
            var g = new LocomotionGaitSolver();
            g.Solve(0.05f, 0.1f, false, out float s, out float f);
            Assert.AreEqual(LocomotionMode.Idle, g.Mode);
            Assert.AreEqual(0f, s, 1e-6f);
            Assert.AreEqual(0f, f, 1e-6f);
        }

        [Test]
        public void Solve_RunningForward_NeverStrafes()
        {
            var g = new LocomotionGaitSolver();
            // A curved run: velocity 20 deg off the sim facing.
            float r = 5f * (float)Math.Sin(20 * AnimMath.Deg2Rad), z = 5f * (float)Math.Cos(20 * AnimMath.Deg2Rad);
            g.Solve(r, z, false, out float s, out float f);
            Assert.AreEqual(LocomotionMode.Forward, g.Mode);
            Assert.AreEqual(0f, s, 1e-6f);
            Assert.Greater(f, 0f);
        }

        [Test]
        public void Solve_SimMovesSideways_Strafes_WithHysteresis()
        {
            var g = new LocomotionGaitSolver();
            g.Solve(1.5f, 0f, false, out float s, out _);
            Assert.AreEqual(LocomotionMode.Strafe, g.Mode);
            Assert.Greater(s, 0f);
            // 30 deg off: still strafing (exit at 25), but a fresh solver would run forward.
            float r = 2f * (float)Math.Sin(30 * AnimMath.Deg2Rad), z = 2f * (float)Math.Cos(30 * AnimMath.Deg2Rad);
            g.Solve(r, z, false, out _, out _);
            Assert.AreEqual(LocomotionMode.Strafe, g.Mode);
            var fresh = new LocomotionGaitSolver();
            fresh.Solve(r, z, false, out _, out _);
            Assert.AreEqual(LocomotionMode.Forward, fresh.Mode);
        }

        [Test]
        public void Solve_Backpedal_UsesNegativeForward()
        {
            var g = new LocomotionGaitSolver();
            g.Solve(0f, -1.2f, false, out float s, out float f);
            Assert.AreEqual(LocomotionMode.Strafe, g.Mode);
            Assert.Less(f, 0f);
            Assert.AreEqual(0f, s, 1e-6f);
        }

        [Test]
        public void SprintWeight_JogSpeeds_Zero_AboveSprintFull_One()
        {
            var g = new LocomotionGaitSolver();
            Assert.AreEqual(0f, g.SprintWeight(6.0f, true), 1e-6f);
            Assert.AreEqual(0f, g.SprintWeight(g.SprintOnset, true), 1e-6f);
            Assert.AreEqual(1f, g.SprintWeight(g.SprintFull + 0.5f, true), 1e-6f);
        }

        [Test]
        public void SprintWeight_NotSprinting_IsCapped()
        {
            var g = new LocomotionGaitSolver();
            Assert.LessOrEqual(g.SprintWeight(8f, false), g.NonSprintMaxSprintWeight + 1e-6f);
        }

        [Test]
        public void SprintWeight_IsMonotonic()
        {
            var g = new LocomotionGaitSolver();
            float prev = -1f;
            for (float v = 0f; v < 10f; v += 0.05f)
            {
                float w = g.SprintWeight(v, true);
                Assert.GreaterOrEqual(w, prev - 1e-6f);
                prev = w;
            }
        }

        [Test]
        public void RateFor_ForwardAtEverySpeed_FeetMatchGround()
        {
            // Foot speed = blend magnitude (natural speed of the blend) * rate; it must equal the ground speed
            // wherever the rate is not clamped, i.e. across the whole sim speed range from a slow walk to 9.7 m/s.
            var g = new LocomotionGaitSolver();
            for (float v = 0.8f; v <= 9.7f; v += 0.1f)
            {
                g.Solve(0f, v, true, out float s, out float f);
                float m = Mag(s, f);
                float rate = g.RateFor(v, m);
                Assert.AreEqual(v, m * rate, 0.02f, $"slide at {v:F1} m/s (blend {m:F2}, rate {rate:F2})");
            }
        }

        [Test]
        public void ForwardMagnitude_JogRange_StaysOnJogClip()
        {
            var g = new LocomotionGaitSolver();
            // The sim's top jog (~6.0 m/s) stays on the jog clip (time-scaled), never the sprint clip.
            Assert.AreEqual(g.JogSpeed, g.ForwardMagnitude(6.0f, false), 1e-4f);
            Assert.AreEqual(4f, g.ForwardMagnitude(4f, false), 1e-4f);
            Assert.AreEqual(g.SprintSpeed, g.ForwardMagnitude(9f, true), 1e-4f);
        }

        [Test]
        public void RateFor_Idle_IsOne()
        {
            Assert.AreEqual(1f, new LocomotionGaitSolver().RateFor(0f, 0f), 1e-6f);
        }
    }

    [TestFixture]
    [Category("FID_ANIM")]
    public class BodyFacingSmootherTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void Step_StandingReversal_TakesAHumanTurnTime()
        {
            var f = new BodyFacingSmoother();
            f.Snap(0f);
            int frames = 0;
            while (Math.Abs(AnimMath.DeltaAngle(f.Yaw, 180f)) > 2f && frames < 600) { f.Step(180f, 0f, Dt); frames++; }
            float t = frames * Dt;
            Assert.Greater(t, 0.25f, "spins on the spot");
            Assert.Less(t, 0.7f, "lags a standing turn");
        }

        [Test]
        public void Step_RateIsLowerAtSprint()
        {
            var f = new BodyFacingSmoother();
            Assert.Less(f.MaxRate(8f), f.MaxRate(0f));
            f.Snap(0f);
            f.Step(90f, 8f, Dt);
            Assert.LessOrEqual(Math.Abs(f.YawRate), f.MaxRateFast + 1e-3f);
        }

        [Test]
        public void Step_SteadySimTurn_IsFollowedWithSmallLag()
        {
            // The sim turns a sprinting player at 150 deg/s: the visual must not fall behind by more than a few degrees.
            var f = new BodyFacingSmoother();
            f.Snap(0f);
            float sim = 0f;
            for (int i = 0; i < 60; i++) { sim += 150f * Dt; f.Step(sim, 7f, Dt); }
            Assert.Less(Math.Abs(AnimMath.DeltaAngle(f.Yaw, sim)), 12f);
        }

        [Test]
        public void Step_WrapsAcrossPlusMinus180()
        {
            var f = new BodyFacingSmoother();
            f.Snap(170f);
            for (int i = 0; i < 60; i++) f.Step(-170f, 0f, Dt);
            Assert.AreEqual(0f, AnimMath.DeltaAngle(f.Yaw, -170f), 0.5f);
            // It went the short way (through 180), never through 0.
        }

        [Test]
        public void Step_ExactReversal_KeepsCurrentTurnDirection()
        {
            var f = new BodyFacingSmoother();
            f.Snap(0f);
            f.Step(-30f, 0f, Dt); // turning left (negative)
            float startYaw = f.Yaw;
            f.Step(startYaw + 180f, 0f, Dt);
            Assert.Less(f.YawRate, 0f);
        }

        [Test]
        public void Step_ConvergesWithoutOvershoot()
        {
            var f = new BodyFacingSmoother();
            f.Snap(0f);
            float max = 0f;
            for (int i = 0; i < 120; i++) { f.Step(45f, 2f, Dt); max = Math.Max(max, f.Yaw); }
            Assert.AreEqual(45f, f.Yaw, 0.1f);
            Assert.LessOrEqual(max, 45f + 1e-3f);
        }
    }

    [TestFixture]
    [Category("FID_ANIM")]
    public class LocomotionLeanTests
    {
        [Test]
        public void Step_RightCurveAtSpeed_LeansRight()
        {
            var l = new LocomotionLean();
            for (int i = 0; i < 60; i++) l.Step(7f, 90f, 0f, 1f / 60f);
            Assert.Greater(l.RightDeg, 3f);
            Assert.LessOrEqual(l.RightDeg, l.MaxBankDeg + 1e-4f);
        }

        [Test]
        public void Step_LeftCurve_LeansLeft()
        {
            var l = new LocomotionLean();
            for (int i = 0; i < 60; i++) l.Step(5f, -90f, 0f, 1f / 60f);
            Assert.Less(l.RightDeg, -1f);
        }

        [Test]
        public void Step_PivotWhileStanding_NoBank()
        {
            var l = new LocomotionLean();
            for (int i = 0; i < 60; i++) l.Step(0.2f, 600f, 0f, 1f / 60f);
            Assert.AreEqual(0f, l.RightDeg, 1e-3f);
        }

        [Test]
        public void Step_Accelerating_LeansForward_Braking_Back()
        {
            var a = new LocomotionLean();
            var b = new LocomotionLean();
            for (int i = 0; i < 60; i++) { a.Step(4f, 0f, 5f, 1f / 60f); b.Step(4f, 0f, -6f, 1f / 60f); }
            Assert.Greater(a.ForwardDeg, 1f);
            Assert.Less(b.ForwardDeg, -1f);
        }
    }
}
