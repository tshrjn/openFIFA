using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID_ANIM")]
    public class IdleVarietyTests
    {
        [Test]
        public void Ctor_SameSeed_SameIdle_DifferentSeeds_Desynchronised()
        {
            var a = new IdleVariety(42);
            var b = new IdleVariety(42);
            Assert.AreEqual(a.PhaseOffset, b.PhaseOffset);
            Assert.AreEqual(a.RateMultiplier, b.RateMultiplier);
            // 22 players: phases spread over the cycle, rates over 0.85-1.15.
            float minP = 1f, maxP = 0f, minR = 2f, maxR = 0f;
            for (int i = 0; i < 22; i++)
            {
                var v = new IdleVariety(i * 31 + 5);
                minP = System.Math.Min(minP, v.PhaseOffset); maxP = System.Math.Max(maxP, v.PhaseOffset);
                minR = System.Math.Min(minR, v.RateMultiplier); maxR = System.Math.Max(maxR, v.RateMultiplier);
                Assert.That(v.RateMultiplier, Is.InRange(0.85f, 1.15f));
                Assert.That(v.PhaseOffset, Is.InRange(0f, 1f));
            }
            Assert.Greater(maxP - minP, 0.5f, "idle phases bunched together");
            Assert.Greater(maxR - minR, 0.1f, "idle rates nearly identical");
        }

        [Test]
        public void Step_TenSeconds_WeightShiftSwitchesAndStaysInRange()
        {
            var v = new IdleVariety(9);
            float start = v.IdleVariant;
            bool changed = false;
            for (int k = 0; k < 600; k++)
            {
                v.Step(1f / 60f, 0f, 0f, 0f, 30f);
                Assert.That(v.IdleVariant, Is.InRange(0f, 1f));
                if (System.Math.Abs(v.IdleVariant - start) > 0.9f) changed = true;
            }
            Assert.IsTrue(changed, "no weight shift in 10 s");
        }

        [Test]
        public void Step_BallClose_ReadyStance_BallFarOrRunning_None()
        {
            var near = new IdleVariety(1);
            var far = new IdleVariety(1);
            var running = new IdleVariety(1);
            for (int k = 0; k < 120; k++)
            {
                near.Step(1f / 60f, 0.2f, 0f, 0f, 6f);
                far.Step(1f / 60f, 0.2f, 0f, 0f, 35f);
                running.Step(1f / 60f, 6f, 0f, 0f, 6f);
            }
            Assert.Greater(near.Ready, 0.9f);
            Assert.Less(far.Ready, 0.02f);
            Assert.Less(running.Ready, 0.02f);
        }

        [Test]
        public void Step_HeadFollowsBall_ClampedAndFadesWhenBehindOrSprinting()
        {
            var side = new IdleVariety(3);
            var behind = new IdleVariety(3);
            var sprint = new IdleVariety(3);
            float maxAbs = 0f;
            for (int k = 0; k < 300; k++)
            {
                side.Step(1f / 60f, 0f, 0f, 60f, 15f);      // ball 60 deg to the right
                behind.Step(1f / 60f, 0f, 0f, 175f, 15f);   // ball behind
                sprint.Step(1f / 60f, 7f, 0f, 60f, 15f);
                maxAbs = System.Math.Max(maxAbs, System.Math.Abs(behind.LookYaw));
            }
            Assert.Greater(side.LookYaw, 15f, "head does not turn toward the ball");
            Assert.LessOrEqual(maxAbs, IdleVariety.MaxLookYaw + 1e-3f);
            Assert.Greater(side.LookWeight, behind.LookWeight, "ball behind should weaken the look");
            Assert.Less(sprint.LookWeight, 0.05f);
        }

        [Test]
        public void Step_LookTarget_ChangesInSaccadesNotEveryFrame()
        {
            // With the ball fixed, the head target only moves at saccades (every 0.4-1.4 s): within any 0.3 s the
            // look direction settles instead of wandering every frame.
            var v = new IdleVariety(17);
            int scans = 0;
            float prev = 0f;
            for (int k = 0; k < 60 * 20; k++)
            {
                v.Step(1f / 60f, 0f, 0f, 0f, 15f);
                if (k > 60 && System.Math.Abs(v.LookYaw - prev) > 10f) scans++;
                if (k % 30 == 0) prev = v.LookYaw;
            }
            Assert.Greater(scans, 0, "never checks a shoulder");
        }

        // -------------------------------------------------------------- dribble touch foot vs ball side

        [Test]
        public void ChooseTouchFoot_BallWellToTheRight_RightFootEvenIfLeftSwings()
        {
            var foot = DribbleTouchSolver.ChooseTouchFoot(ActionSide.Left, 0.35f, out bool overridden);
            Assert.AreEqual(ActionSide.Right, foot);
            Assert.IsTrue(overridden);
        }

        [Test]
        public void ChooseTouchFoot_BallWellToTheLeft_LeftFoot()
        {
            var foot = DribbleTouchSolver.ChooseTouchFoot(ActionSide.Left, -0.4f, out bool overridden);
            Assert.AreEqual(ActionSide.Left, foot);
            Assert.IsFalse(overridden, "the stride already chose the ball-side foot");
        }

        [Test]
        public void ChooseTouchFoot_BallInFront_StrideDecides()
        {
            Assert.AreEqual(ActionSide.Left, DribbleTouchSolver.ChooseTouchFoot(ActionSide.Left, 0.05f, out _));
            Assert.AreEqual(ActionSide.Right, DribbleTouchSolver.ChooseTouchFoot(ActionSide.Right, -0.05f, out _));
        }

        [Test]
        public void BallLocal_RunningAlongX_BallOnTheRightHasPositiveX()
        {
            // Facing +X (yaw 90): the body's right is -Z. Ball 0.35 m ahead and 0.4 m to the right.
            var root = new SimVec3(10f, 0f, 5f);
            var ball = root + new SimVec3(0.35f, 0.11f, -0.4f);
            var local = DribbleTouchSolver.BallLocal(ball, root, 90f);
            Assert.AreEqual(0.4f, local.X, 1e-4f);
            Assert.AreEqual(0.35f, local.Z, 1e-4f);
        }

        [Test]
        public void Plan_DribbleTouch_StrikeTargetIncludesTheLateralOffset()
        {
            // The strike foot's IK target is the ball's predicted position at contact, sideways offset included.
            SimVec3 root = new SimVec3(0f, 0f, 6f);
            SimVec3 ball = root + new SimVec3(0.4f, 0.11f, 0.3f);
            var r = new ActionSyncRequest(AnimActionKind.DribbleTouch, ActionSide.Right, 0f, 1f, SimVec3.Zero, root, 0f, ball,
                new SimVec3(0f, 0f, 1f), 6f, ActionSide.Right);
            var plan = new AnimationSyncPlanner().Plan(r);
            Assert.AreEqual(ContactEffector.RightFoot, plan.Ik.StrikeEffector);
            Assert.AreEqual(ball.X, plan.Ik.StrikeTarget.X, 0.02f, "IK target lost the sideways offset");
            Assert.Less(SimMath.FlatDistance(plan.Ik.StrikeTarget, ball), DribbleTouchSolver.SideThreshold + 0.05f);
        }
    }
}
