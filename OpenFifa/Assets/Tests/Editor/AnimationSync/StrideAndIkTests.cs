using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID-ANIM")]
    public class StrideAndIkTests
    {
        [Test]
        public void Plan_ExactMultipleWithRightParity_KeepsNominalStride()
        {
            var sa = new StrideAdjuster();
            float l = sa.NominalStepLength(5f);
            StridePlan p = sa.Plan(4f * l, 4f * l / 5f, 5f, ActionSide.Right, ActionSide.Left);
            Assert.AreEqual(4, p.Steps);
            Assert.AreEqual(1f, p.StrideScale, 1e-4f);
            Assert.AreEqual(1f, p.CadenceScale, 1e-4f);
            Assert.IsTrue(p.Feasible);
        }

        [Test]
        public void Plan_WrongParity_ChoosesOddStepCountSoPlantFootLandsLast()
        {
            var sa = new StrideAdjuster();
            float l = sa.NominalStepLength(5f);
            StridePlan p = sa.Plan(4f * l, 4f * l / 5f, 5f, ActionSide.Left, ActionSide.Left);
            Assert.AreEqual(1, p.Steps % 2);
            Assert.IsTrue(p.Feasible);
        }

        [Test]
        public void Plan_ImplausibleSpeed_IsInfeasible()
        {
            var sa = new StrideAdjuster();
            StridePlan p = sa.Plan(14f, 0.2f, 5f, ActionSide.Right, ActionSide.Left);
            Assert.IsFalse(p.Feasible);
        }

        [Test]
        public void Plan_ZeroDistance_NoSteps()
        {
            StridePlan p = new StrideAdjuster().Plan(0f, 0.5f, 3f, ActionSide.Right, ActionSide.Left);
            Assert.AreEqual(0, p.Steps);
            Assert.IsTrue(p.Feasible);
        }

        private static ActionSyncPlan ShotPlan() => new AnimationSyncPlanner().Plan(new ActionSyncRequest(
            AnimActionKind.Shot, ActionSide.Right, 0f, 1f, new SimVec3(0f, 0f, -4f), SimVec3.Zero, 0f,
            new SimVec3(0.08f, 0.11f, 0.38f), new SimVec3(0f, 0f, 1f), 5f, ActionSide.Right));

        [Test]
        public void Planner_RightFootShot_LeftFootPlantsBesideBall()
        {
            ActionSyncPlan p = ShotPlan();
            Assert.AreEqual(ContactEffector.RightFoot, p.Ik.StrikeEffector);
            Assert.AreEqual(ContactEffector.LeftFoot, p.Ik.PlantEffector);
            Assert.Less(p.Ik.PlantTarget.X, 0f);
            Assert.AreEqual(ContactIkSolver.DefaultAnkleHeight, p.Ik.PlantTarget.Y, 1e-5f);
        }

        [Test]
        public void Planner_StrikeTarget_IsOnBallSurfaceBehindKickDirection()
        {
            ActionSyncPlan p = ShotPlan();
            Assert.AreEqual(0.38f - ContactIkSolver.DefaultBallRadius, p.Ik.StrikeTarget.Z, 1e-4f);
            Assert.AreEqual(0.11f, p.Ik.StrikeTarget.Y, 1e-5f);
        }

        [Test]
        public void Planner_IkWeights_PeakAtContactAndZeroOutside()
        {
            ActionSyncPlan p = ShotPlan();
            Assert.AreEqual(1f, p.Ik.StrikeWeightAt(1f), 1e-5f);
            Assert.AreEqual(0f, p.Ik.StrikeWeightAt(0f), 1e-6f);
            Assert.AreEqual(0f, p.Ik.StrikeWeightAt(2f), 1e-6f);
            Assert.AreEqual(1f, p.Ik.PlantWeightAt(p.Ik.PlantTime + 0.01f), 1e-5f);
        }

        [Test]
        public void Planner_RunUpShot_HasFeasibleStridePlan()
        {
            ActionSyncPlan p = ShotPlan();
            Assert.IsTrue(p.HasStridePlan);
            Assert.GreaterOrEqual(p.Stride.Steps, 1);
            Assert.IsTrue(p.Feasible);
            Assert.GreaterOrEqual(p.EndTime, p.Warp.ReleaseEnd);
        }

        [Test]
        public void Planner_Header_UsesHeadAndNoRunUp()
        {
            ActionSyncPlan p = new AnimationSyncPlanner().Plan(new ActionSyncRequest(
                AnimActionKind.Header, ActionSide.Right, 0f, 0.6f, SimVec3.Zero, SimVec3.Zero, 0f,
                new SimVec3(0f, 1.8f, 0.18f), new SimVec3(0f, -0.2f, 1f), 0f, ActionSide.Right));
            Assert.AreEqual(ContactEffector.Head, p.Ik.StrikeEffector);
            Assert.AreEqual(ContactEffector.None, p.Ik.PlantEffector);
            Assert.IsFalse(p.HasStridePlan);
            Assert.IsTrue(p.Warp.Feasible);
        }

        [Test]
        public void Planner_SameRequestTwice_IsDeterministic()
        {
            ActionSyncPlan a = ShotPlan();
            ActionSyncPlan b = ShotPlan();
            Assert.AreEqual(a.Warp.TranslationDelta, b.Warp.TranslationDelta);
            Assert.AreEqual(a.Timing.StartTime, b.Timing.StartTime);
            Assert.AreEqual(a.Stride.Steps, b.Stride.Steps);
        }

        [Test]
        public void FromTicks_ConvertsTimes()
        {
            ActionSyncRequest r = ActionSyncRequest.FromTicks(AnimActionKind.Pass, ActionSide.Right, 120, 180, 120f,
                SimVec3.Zero, SimVec3.Zero, 0f, SimVec3.Zero, SimVec3.Zero, 0f, ActionSide.Right);
            Assert.AreEqual(1f, r.Now, 1e-6f);
            Assert.AreEqual(1.5f, r.ContactTime, 1e-6f);
        }

        [Test]
        public void RateFor_InsideAndOutsideAuthoredRange_ScalesOnlyOutside()
        {
            var lr = new LocomotionRateSolver(new[] { 1.4f, 3.4f, 6.5f });
            Assert.AreEqual(1f, lr.RateFor(0f), 1e-6f);
            Assert.AreEqual(1f, lr.RateFor(2f), 1e-6f);
            Assert.AreEqual(0.6f, lr.RateFor(0.7f), 1e-5f);
            Assert.AreEqual(1.2f, lr.RateFor(7.8f), 1e-4f);
            Assert.AreEqual(1.4f, lr.RateFor(20f), 1e-6f);
        }
    }
}
