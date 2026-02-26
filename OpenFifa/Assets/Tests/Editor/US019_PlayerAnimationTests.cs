using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-019")]
    public class US019_PlayerAnimationTests
    {
        [Test]
        public void AnimationStateLogic_InitialState_IsIdle()
        {
            var logic = new AnimationStateLogic();
            Assert.AreEqual(AnimationStateId.Idle, logic.CurrentState);
        }

        [Test]
        public void AnimationStateLogic_SpeedZero_StaysIdle()
        {
            var logic = new AnimationStateLogic();
            logic.UpdateLocomotion(0f, false);
            Assert.AreEqual(AnimationStateId.Idle, logic.CurrentState);
        }

        [Test]
        public void AnimationStateLogic_SpeedAboveWalk_TransitionsToRun()
        {
            var logic = new AnimationStateLogic();
            logic.UpdateLocomotion(4f, false);
            Assert.AreEqual(AnimationStateId.Run, logic.CurrentState);
        }

        [Test]
        public void AnimationStateLogic_Sprinting_TransitionsToSprint()
        {
            var logic = new AnimationStateLogic();
            logic.UpdateLocomotion(8f, true);
            Assert.AreEqual(AnimationStateId.Sprint, logic.CurrentState);
        }

        [Test]
        public void AnimationStateLogic_TriggerKick_TransitionsToKick()
        {
            var logic = new AnimationStateLogic();
            logic.TriggerAction(AnimationActionTrigger.Kick);
            Assert.AreEqual(AnimationStateId.Kick, logic.CurrentState);
        }

        [Test]
        public void AnimationStateLogic_TriggerTackle_TransitionsToTackle()
        {
            var logic = new AnimationStateLogic();
            logic.TriggerAction(AnimationActionTrigger.Tackle);
            Assert.AreEqual(AnimationStateId.Tackle, logic.CurrentState);
        }

        [Test]
        public void AnimationStateLogic_TriggerCelebrate_TransitionsToCelebrate()
        {
            var logic = new AnimationStateLogic();
            logic.TriggerAction(AnimationActionTrigger.Celebrate);
            Assert.AreEqual(AnimationStateId.Celebrate, logic.CurrentState);
        }

        [Test]
        public void AnimationStateLogic_ActionComplete_ReturnsToLocomotion()
        {
            var logic = new AnimationStateLogic();
            logic.UpdateLocomotion(4f, false);
            logic.TriggerAction(AnimationActionTrigger.Kick);
            Assert.AreEqual(AnimationStateId.Kick, logic.CurrentState);

            logic.CompleteAction();
            // Should return to the locomotion state based on last speed
            Assert.AreEqual(AnimationStateId.Run, logic.CurrentState);
        }

        [Test]
        public void AnimationStateLogic_SpeedParameter_ScalesWithSpeed()
        {
            var logic = new AnimationStateLogic();
            logic.UpdateLocomotion(3.5f, false);
            // Speed param: 3.5 / maxSpeed(10.5) = ~0.333
            Assert.Greater(logic.SpeedParameter, 0.3f);
            Assert.Less(logic.SpeedParameter, 0.4f);
        }

        [Test]
        public void AnimationStateLogic_SpeedParameter_ClampedToZeroOne()
        {
            var logic = new AnimationStateLogic();
            logic.UpdateLocomotion(0f, false);
            Assert.AreEqual(0f, logic.SpeedParameter);

            logic.UpdateLocomotion(20f, true);
            Assert.AreEqual(1f, logic.SpeedParameter);
        }

        [Test]
        public void AnimationStateLogic_IsInActionState_TrueForActions()
        {
            var logic = new AnimationStateLogic();
            Assert.IsFalse(logic.IsInActionState);

            logic.TriggerAction(AnimationActionTrigger.Kick);
            Assert.IsTrue(logic.IsInActionState);

            logic.CompleteAction();
            Assert.IsFalse(logic.IsInActionState);
        }

        [Test]
        public void AnimationStateLogic_ActionOverridesLocomotion()
        {
            var logic = new AnimationStateLogic();
            logic.UpdateLocomotion(5f, false);
            logic.TriggerAction(AnimationActionTrigger.Tackle);

            // Updating locomotion during action should not change state
            logic.UpdateLocomotion(8f, true);
            Assert.AreEqual(AnimationStateId.Tackle, logic.CurrentState,
                "Action state should not be overridden by locomotion");
        }
    }
}
