using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID_ANIM")]
    public class KickWarpSolverTests
    {
        private static ActionClipProfile Pass => ActionClipLibrary.Default(AnimActionKind.Pass);
        private static readonly SimVec3 AuthoredBall = new SimVec3(0.10f, 0.11f, 0.32f);

        private static WarpPlan SolveRight(SimVec3 ball, float dirYaw) =>
            KickWarpSolver.Solve(Pass, ActionSide.Right, SimVec3.Zero, 0f, ball, AnimMath.Forward(dirYaw), 1f);

        [Test]
        public void Solve_BallAtAuthoredContact_NoWarp()
        {
            WarpPlan w = SolveRight(AuthoredBall, Pass.StrikeYawOffset);
            Assert.Less(w.TranslationDelta.Magnitude, 1e-4f);
            Assert.AreEqual(0f, w.YawDelta, 1e-3f);
            Assert.IsTrue(w.Feasible);
        }

        [Test]
        public void Solve_BallFurtherAhead_TranslatesVisualForward()
        {
            WarpPlan w = SolveRight(AuthoredBall + new SimVec3(0f, 0f, 0.3f), Pass.StrikeYawOffset);
            Assert.AreEqual(0.3f, w.TranslationDelta.Z, 1e-4f);
            Assert.AreEqual(0f, w.TranslationDelta.Y, 1e-6f);
            Assert.IsTrue(w.Feasible);
        }

        [Test]
        public void Solve_WarpedContactPoint_MeetsBall()
        {
            SimVec3 ball = new SimVec3(0.35f, 0.11f, 0.2f);
            WarpPlan w = SolveRight(ball, 25f);
            SimVec3 contact = w.VisualRootAtContact + AnimMath.LocalToWorld(Pass.ContactOffset, w.VisualYawAtContact);
            Assert.Less((contact - ball).Flat.Magnitude, 1e-3f);
        }

        [Test]
        public void Solve_BallTooFar_ClampsAndReportsResidual()
        {
            WarpPlan w = SolveRight(AuthoredBall + new SimVec3(0f, 0f, 1.5f), Pass.StrikeYawOffset);
            Assert.AreEqual(Pass.MaxWarpDistance, w.TranslationDelta.Magnitude, 1e-4f);
            Assert.Greater(w.ResidualDistance, 1f);
            Assert.IsFalse(w.Feasible);
        }

        [Test]
        public void Solve_PassToTheRight_RotatesTowardTarget()
        {
            WarpPlan w = SolveRight(new SimVec3(0.3f, 0.11f, 0.3f), 30f);
            Assert.AreEqual(30f - Pass.StrikeYawOffset, w.YawDelta, 1e-3f);
        }

        [Test]
        public void Solve_BackwardsPass_ClampsYawAndIsInfeasible()
        {
            WarpPlan w = SolveRight(new SimVec3(0f, 0.11f, -0.3f), 180f);
            Assert.LessOrEqual(System.Math.Abs(w.YawDelta), Pass.MaxWarpYaw + 1e-3f);
            Assert.IsFalse(w.Feasible);
        }

        [Test]
        public void Solve_LeftFootMirroredBall_NoWarp()
        {
            WarpPlan w = KickWarpSolver.Solve(Pass, ActionSide.Left, SimVec3.Zero, 0f,
                new SimVec3(-0.10f, 0.11f, 0.32f), AnimMath.Forward(-Pass.StrikeYawOffset), 1f);
            Assert.Less(w.TranslationDelta.Magnitude, 1e-4f);
            Assert.AreEqual(0f, w.YawDelta, 1e-3f);
        }

        [Test]
        public void WeightAt_Envelope_RampsInHoldsAndReleases()
        {
            WarpPlan w = SolveRight(AuthoredBall + new SimVec3(0f, 0f, 0.3f), Pass.StrikeYawOffset);
            Assert.AreEqual(0f, w.WeightAt(w.WindowStart), 1e-6f);
            Assert.AreEqual(1f, w.WeightAt(w.ContactTime), 1e-6f);
            Assert.AreEqual(1f, w.WeightAt(w.HoldEnd), 1e-6f);
            Assert.AreEqual(0f, w.WeightAt(w.ReleaseEnd), 1e-6f);
            float mid = w.WeightAt((w.WindowStart + w.ContactTime) * 0.5f);
            Assert.Greater(mid, 0f);
            Assert.Less(mid, 1f);
        }

        [Test]
        public void Solve_HeaderBallTooHigh_ReportsInfeasible()
        {
            ActionClipProfile header = ActionClipLibrary.Default(AnimActionKind.Header);
            WarpPlan w = KickWarpSolver.Solve(header, ActionSide.Right, SimVec3.Zero, 0f,
                new SimVec3(0f, 2.4f, 0.18f), new SimVec3(0f, 0f, 1f), 1f);
            Assert.IsFalse(w.Feasible);
        }

        [Test]
        public void AnimMath_YawConvention_MatchesUnity()
        {
            SimVec3 f = AnimMath.Forward(90f);
            Assert.AreEqual(1f, f.X, 1e-5f);
            Assert.AreEqual(0f, f.Z, 1e-5f);
            Assert.AreEqual(-1f, AnimMath.LocalToWorld(new SimVec3(1f, 0f, 0f), 90f).Z, 1e-5f);
            Assert.AreEqual(20f, AnimMath.DeltaAngle(170f, -170f), 1e-4f);
            Assert.AreEqual(90f, AnimMath.YawOf(new SimVec3(2f, 0f, 0f)), 1e-4f);
        }
    }
}
