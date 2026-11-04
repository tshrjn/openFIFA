using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID_ANIM")]
    public class DribbleTouchTests
    {
        private static ActionClipProfile Touch => ActionClipLibrary.Default(AnimActionKind.DribbleTouch);

        private static ActionSyncPlan PlanTouch(float now, float contact, ActionSide side, float speed = 6f)
        {
            // Running along +Z at 'speed', ball 0.35 m ahead of the root at the contact tick.
            SimVec3 rootAtContact = new SimVec3(0f, 0f, speed * contact);
            SimVec3 ball = rootAtContact + new SimVec3(0f, 0.11f, 0.35f);
            var r = new ActionSyncRequest(AnimActionKind.DribbleTouch, side, now, contact, new SimVec3(0f, 0f, speed * now),
                rootAtContact, 0f, ball, new SimVec3(0f, 0f, 1f), speed, ActionSide.Right);
            return new AnimationSyncPlanner().Plan(r);
        }

        [Test]
        public void PresentationKind_DribbleFirstTouch_MapsToDribbleTouch()
        {
            Assert.AreEqual(AnimActionKind.DribbleTouch, DribbleTouchSolver.PresentationKind(AnimActionKind.FirstTouch, true));
        }

        [Test]
        public void PresentationKind_ReceivingFirstTouch_StaysFirstTouch()
        {
            Assert.AreEqual(AnimActionKind.FirstTouch, DribbleTouchSolver.PresentationKind(AnimActionKind.FirstTouch, false));
            Assert.AreEqual(AnimActionKind.Pass, DribbleTouchSolver.PresentationKind(AnimActionKind.Pass, true));
        }

        [Test]
        public void Profile_DribbleTouch_IsLayeredWithoutWindupOrRunUp()
        {
            ActionClipProfile p = Touch;
            Assert.IsTrue(p.LayeredOnLocomotion);
            Assert.IsFalse(p.UsesRunUp);
            Assert.AreEqual(0f, p.MinWindup, 1e-6f);
            Assert.AreEqual(0f, p.PlantFootOffset.SqrMagnitude, 1e-9f, "no plant foot: the run cycle keeps its own footfalls");
            // The only body warp while running is a small sideways shift toward the ball (no yaw, no forward/back).
            Assert.IsTrue(p.LateralWarpOnly);
            Assert.AreEqual(DribbleTouchSolver.MaxLateralShift, p.MaxWarpDistance, 1e-6f);
            Assert.LessOrEqual(p.MaxWarpDistance, 0.25f + 1e-6f);
            Assert.AreEqual(0f, p.MaxWarpYaw, 1e-6f);
            Assert.AreEqual(1f, p.MinRate, 1e-6f);
            Assert.AreEqual(1f, p.MaxRate, 1e-6f);
        }

        [Test]
        public void Profile_FullBodyKinds_AreNotLayered()
        {
            Assert.IsFalse(ActionClipLibrary.Default(AnimActionKind.FirstTouch).LayeredOnLocomotion);
            Assert.IsFalse(ActionClipLibrary.Default(AnimActionKind.Pass).LayeredOnLocomotion);
            Assert.IsFalse(ActionClipLibrary.Default(AnimActionKind.Shot).LayeredOnLocomotion);
        }

        [Test]
        public void Profile_DribbleTouch_ContactWindowShorterThanFirstTouch()
        {
            ActionClipProfile first = ActionClipLibrary.Default(AnimActionKind.FirstTouch);
            Assert.Less(Touch.ContactTime, first.ContactTime + 1e-6f);
            Assert.Less(Touch.ClipLength, first.ClipLength);
            Assert.AreEqual(DribbleTouchSolver.BlendIn + DribbleTouchSolver.BlendOut, Touch.ClipLength, 1e-5f);
        }

        [Test]
        public void StateFor_DribbleTouch_IsLocomotion()
        {
            Assert.AreEqual(AnimStateNames.Locomotion, AnimStateNames.StateFor(AnimActionKind.DribbleTouch));
        }

        [Test]
        public void Plan_DribbleTouch_StrikeWeightRampsInAndOutOverPoint12s()
        {
            ActionSyncPlan plan = PlanTouch(0f, 1f, ActionSide.Right);
            ContactIkTargets ik = plan.Ik;
            Assert.AreEqual(1f - DribbleTouchSolver.BlendIn, ik.StrikeRampStart, 1e-5f);
            Assert.AreEqual(1f + DribbleTouchSolver.BlendOut, ik.StrikeReleaseEnd, 1e-5f);
            Assert.AreEqual(0f, ik.StrikeWeightAt(0.87f), 1e-6f);
            Assert.AreEqual(1f, ik.StrikeWeightAt(1f), 1e-5f);
            Assert.AreEqual(0.5f, ik.StrikeWeightAt(0.94f), 1e-3f);
            Assert.AreEqual(0.5f, ik.StrikeWeightAt(1.06f), 1e-3f);
            Assert.AreEqual(0f, ik.StrikeWeightAt(1.13f), 1e-6f);
        }

        [Test]
        public void Plan_DribbleTouch_WeightMatchesSolverCurve()
        {
            ActionSyncPlan plan = PlanTouch(0f, 1f, ActionSide.Left);
            for (float t = 0.8f; t <= 1.2f; t += 0.01f)
                Assert.AreEqual(DribbleTouchSolver.WeightAt(t, 1f), plan.Ik.StrikeWeightAt(t), 1e-5f, $"t={t}");
        }

        [Test]
        public void Plan_DribbleTouch_NoPlantNoStrideNoForwardWarp()
        {
            // Ball straight ahead (0.35 m, right foot offset 0.09 m): no plant, no stride plan, no yaw, and the body is
            // never pulled forward or back; only the 0.09 m foot offset is taken up sideways.
            ActionSyncPlan plan = PlanTouch(0f, 1f, ActionSide.Right);
            Assert.AreEqual(ContactEffector.None, plan.Ik.PlantEffector);
            Assert.IsFalse(plan.HasStridePlan);
            Assert.AreEqual(0f, plan.Warp.TranslationDelta.Z, 1e-6f, "forward/back warp while running");
            Assert.AreEqual(-0.09f, plan.Warp.TranslationDelta.X, 1e-4f);
            Assert.AreEqual(0f, plan.Warp.YawDelta, 1e-6f);
        }

        private static ActionSyncPlan PlanTouchAt(SimVec3 ballLocal, float yawDeg, ActionSide side)
        {
            SimVec3 root = new SimVec3(3f, 0f, 6f);
            SimVec3 ball = root + AnimMath.LocalToWorld(ballLocal, yawDeg);
            var r = new ActionSyncRequest(AnimActionKind.DribbleTouch, side, 0f, 1f, root, root, yawDeg, ball,
                AnimMath.Forward(yawDeg), 6f, ActionSide.Right);
            return new AnimationSyncPlanner().Plan(r);
        }

        [Test]
        public void Plan_DribbleTouch_BallToTheSide_BodyShiftsSidewaysOnly()
        {
            // Running toward +X (yaw 90), ball 0.4 m to the right and 0.6 m ahead: shift = 0.4 - 0.09 = 0.31, capped at 0.25,
            // along the body's right axis (-Z in the world), nothing along the run.
            var plan = PlanTouchAt(new SimVec3(0.4f, 0.11f, 0.6f), 90f, ActionSide.Right);
            SimVec3 local = AnimMath.WorldToLocal(plan.Warp.TranslationDelta, 90f);
            Assert.AreEqual(DribbleTouchSolver.MaxLateralShift, local.X, 1e-4f);
            Assert.AreEqual(0f, local.Z, 1e-4f);
            Assert.AreEqual(0f, plan.Warp.YawDelta, 1e-6f);
        }

        [Test]
        public void Plan_DribbleTouch_BallLeft_LeftFootShiftsLeft()
        {
            var plan = PlanTouchAt(new SimVec3(-0.25f, 0.11f, 0.35f), 0f, ActionSide.Left);
            Assert.AreEqual(-(0.25f - 0.09f), plan.Warp.TranslationDelta.X, 1e-4f);
        }

        [Test]
        public void Plan_DribbleTouch_ShiftEasesInAndOutInsideTheTouchWindow()
        {
            var plan = PlanTouchAt(new SimVec3(0.5f, 0.11f, 0.35f), 0f, ActionSide.Right);
            var w = plan.Warp;
            Assert.AreEqual(0f, w.WeightAt(1f - DribbleTouchSolver.BlendIn - 1e-3f), 1e-6f);
            Assert.AreEqual(1f, w.WeightAt(1f), 1e-6f);
            Assert.AreEqual(0f, w.WeightAt(1f + DribbleTouchSolver.BlendOut + 1e-3f), 1e-6f);
            // Eased (smoothstep): slow at the edges, no step anywhere.
            Assert.Less(w.WeightAt(1f - 0.9f * DribbleTouchSolver.BlendIn), 0.05f);
            float prev = 0f, maxStep = 0f;
            for (float t = 0.85f; t <= 1.15f; t += 1f / 120f)
            {
                float v = w.WeightAt(t);
                maxStep = System.Math.Max(maxStep, System.Math.Abs(v - prev));
                prev = v;
            }
            Assert.Less(maxStep, 0.11f, "warp pops");
        }

        [Test]
        public void Plan_DribbleTouch_StrikeEffectorFollowsSide()
        {
            Assert.AreEqual(ContactEffector.RightFoot, PlanTouch(0f, 1f, ActionSide.Right).Ik.StrikeEffector);
            Assert.AreEqual(ContactEffector.LeftFoot, PlanTouch(0f, 1f, ActionSide.Left).Ik.StrikeEffector);
        }

        [Test]
        public void Plan_DribbleTouch_StrikeTargetBehindBallAlongRun()
        {
            ActionSyncPlan plan = PlanTouch(0f, 1f, ActionSide.Right);
            SimVec3 ball = plan.Request.BallAtContact;
            SimVec3 t = plan.Ik.StrikeTarget;
            Assert.AreEqual(ball.Z - ContactIkSolver.DefaultBallRadius, t.Z, 1e-5f);
            Assert.AreEqual(ball.X, t.X, 1e-5f);
        }

        [Test]
        public void Plan_DribbleTouch_EndsShortlyAfterContact()
        {
            ActionSyncPlan plan = PlanTouch(0f, 1f, ActionSide.Right);
            Assert.AreEqual(1f + DribbleTouchSolver.BlendOut, plan.EndTime, 1e-4f);
        }

        [Test]
        public void Plan_DribbleTouch_ShortLeadStillLandsOnContact()
        {
            // Touch announced only 0.05 s ahead: no wind-up to protect, the IK envelope just starts late.
            ActionSyncPlan plan = PlanTouch(0.95f, 1f, ActionSide.Right);
            Assert.IsTrue(plan.Timing.Feasible);
            Assert.AreEqual(1f, plan.Timing.PredictedContactTime, 1e-4f);
            Assert.AreEqual(1f, plan.Ik.StrikeWeightAt(1f), 1e-5f);
        }

        [Test]
        public void WeightAt_PeakScalesCurve()
        {
            Assert.AreEqual(0.85f, DribbleTouchSolver.WeightAt(1f, 1f, 0.85f), 1e-6f);
            Assert.AreEqual(0f, DribbleTouchSolver.WeightAt(0.88f, 1f, 0.85f), 1e-6f);
            Assert.AreEqual(0f, DribbleTouchSolver.WeightAt(1.12f, 1f, 0.85f), 1e-6f);
        }

        [Test]
        public void StepPeriod_Standing_IsZero()
        {
            Assert.AreEqual(0f, DribbleTouchSolver.StepPeriod(0.3f), 1e-6f);
        }

        [Test]
        public void StepPeriod_Running_MatchesStrideModel()
        {
            var s = new StrideAdjuster();
            Assert.AreEqual(s.NominalStepLength(6f) / 6f, DribbleTouchSolver.StepPeriod(6f, s), 1e-6f);
        }

        [Test]
        public void FootfallsBefore_RoundsHalfStepPhase()
        {
            Assert.AreEqual(0, DribbleTouchSolver.FootfallsBefore(0.10f, 0.28f));
            Assert.AreEqual(1, DribbleTouchSolver.FootfallsBefore(0.20f, 0.28f));
            Assert.AreEqual(2, DribbleTouchSolver.FootfallsBefore(0.50f, 0.28f));
            Assert.AreEqual(0, DribbleTouchSolver.FootfallsBefore(-0.1f, 0.28f));
        }

        [Test]
        public void ChooseFoot_ContactBeforeNextFootfall_UsesSwingingFoot()
        {
            ActionSide f = DribbleTouchSolver.ChooseFoot(true, ActionSide.Left, 0.05f, 6f, true, ActionSide.Left, ActionSide.Right);
            Assert.AreEqual(ActionSide.Left, f, "stride parity wins over alternation");
        }

        [Test]
        public void ChooseFoot_OneFootfallBeforeContact_UsesOtherFoot()
        {
            float period = DribbleTouchSolver.StepPeriod(6f);
            ActionSide f = DribbleTouchSolver.ChooseFoot(true, ActionSide.Left, period, 6f, false, ActionSide.Right, ActionSide.Left);
            Assert.AreEqual(ActionSide.Right, f);
        }

        [Test]
        public void ChooseFoot_TwoFootfallsBeforeContact_UsesSwingingFootAgain()
        {
            float period = DribbleTouchSolver.StepPeriod(6f);
            ActionSide f = DribbleTouchSolver.ChooseFoot(true, ActionSide.Right, 2f * period, 6f, false, ActionSide.Right, ActionSide.Left);
            Assert.AreEqual(ActionSide.Right, f);
        }

        [Test]
        public void ChooseFoot_NoGait_AlternatesWithLastTouch()
        {
            Assert.AreEqual(ActionSide.Left,
                DribbleTouchSolver.ChooseFoot(false, ActionSide.Right, 0.3f, 6f, true, ActionSide.Right, ActionSide.Right));
            Assert.AreEqual(ActionSide.Right,
                DribbleTouchSolver.ChooseFoot(true, ActionSide.Left, 0.3f, 0.2f, true, ActionSide.Left, ActionSide.Left),
                "too slow to read a stride: alternate");
        }

        [Test]
        public void ChooseFoot_NoGaitNoHistory_UsesPreferredFoot()
        {
            Assert.AreEqual(ActionSide.Left,
                DribbleTouchSolver.ChooseFoot(false, ActionSide.Right, 0.3f, 6f, false, ActionSide.Right, ActionSide.Left));
        }

        [Test]
        public void ChooseFoot_TouchesEveryOtherStep_Alternate()
        {
            // Touches two steps apart while the run keeps the same swing phase at each announcement land on the same
            // foot; touches an odd number of steps apart alternate. Check a sequence of touches 3 steps apart.
            float period = DribbleTouchSolver.StepPeriod(6f);
            ActionSide swing = ActionSide.Right;
            ActionSide prev = DribbleTouchSolver.ChooseFoot(true, swing, 0f, 6f, false, swing, swing);
            for (int i = 0; i < 4; i++)
            {
                // Three footfalls later the swing foot has flipped (odd count).
                swing = DribbleTouchSolver.Other(swing);
                ActionSide next = DribbleTouchSolver.ChooseFoot(true, swing, 0f, 6f, true, prev, ActionSide.Right);
                Assert.AreNotEqual(prev, next, $"touch {i}");
                prev = next;
            }
            Assert.Greater(period, 0f);
        }
    }
}
