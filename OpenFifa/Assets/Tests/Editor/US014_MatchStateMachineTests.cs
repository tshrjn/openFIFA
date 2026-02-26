using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-014")]
    public class US014_MatchStateMachineTests
    {
        [Test]
        public void MatchStateMachine_InitialState_IsPreKickoff()
        {
            var fsm = new MatchStateMachine();
            Assert.AreEqual(MatchState.PreKickoff, fsm.CurrentState);
        }

        [Test]
        public void MatchStateMachine_PreKickoff_TransitionsToFirstHalf()
        {
            var fsm = new MatchStateMachine();
            fsm.TransitionTo(MatchState.FirstHalf);
            Assert.AreEqual(MatchState.FirstHalf, fsm.CurrentState);
        }

        [Test]
        public void MatchStateMachine_FirstHalf_TransitionsToHalfTime()
        {
            var fsm = new MatchStateMachine();
            fsm.TransitionTo(MatchState.FirstHalf);
            fsm.TransitionTo(MatchState.HalfTime);
            Assert.AreEqual(MatchState.HalfTime, fsm.CurrentState);
        }

        [Test]
        public void MatchStateMachine_FirstHalf_TransitionsToGoalCelebration()
        {
            var fsm = new MatchStateMachine();
            fsm.TransitionTo(MatchState.FirstHalf);
            fsm.TransitionTo(MatchState.GoalCelebration);
            Assert.AreEqual(MatchState.GoalCelebration, fsm.CurrentState);
        }

        [Test]
        public void MatchStateMachine_GoalCelebration_TransitionsToPreKickoff()
        {
            var fsm = new MatchStateMachine();
            fsm.TransitionTo(MatchState.FirstHalf);
            fsm.TransitionTo(MatchState.GoalCelebration);
            fsm.TransitionTo(MatchState.PreKickoff);
            Assert.AreEqual(MatchState.PreKickoff, fsm.CurrentState);
        }

        [Test]
        public void MatchStateMachine_HalfTime_TransitionsToSecondHalf()
        {
            var fsm = new MatchStateMachine();
            fsm.TransitionTo(MatchState.FirstHalf);
            fsm.TransitionTo(MatchState.HalfTime);
            fsm.TransitionTo(MatchState.SecondHalf);
            Assert.AreEqual(MatchState.SecondHalf, fsm.CurrentState);
        }

        [Test]
        public void MatchStateMachine_SecondHalf_TransitionsToFullTime()
        {
            var fsm = new MatchStateMachine();
            fsm.TransitionTo(MatchState.FirstHalf);
            fsm.TransitionTo(MatchState.HalfTime);
            fsm.TransitionTo(MatchState.SecondHalf);
            fsm.TransitionTo(MatchState.FullTime);
            Assert.AreEqual(MatchState.FullTime, fsm.CurrentState);
        }

        [Test]
        public void MatchStateMachine_InvalidTransition_ThrowsException()
        {
            var fsm = new MatchStateMachine();
            Assert.Throws<InvalidOperationException>(() => fsm.TransitionTo(MatchState.FullTime),
                "Should throw on invalid transition PreKickoff -> FullTime");
        }

        [Test]
        public void MatchStateMachine_Pause_PreservesState()
        {
            var fsm = new MatchStateMachine();
            fsm.TransitionTo(MatchState.FirstHalf);
            fsm.Pause();
            Assert.AreEqual(MatchState.Paused, fsm.CurrentState);
            Assert.AreEqual(MatchState.FirstHalf, fsm.PreviousState);
        }

        [Test]
        public void MatchStateMachine_Resume_RestoresPreviousState()
        {
            var fsm = new MatchStateMachine();
            fsm.TransitionTo(MatchState.FirstHalf);
            fsm.Pause();
            fsm.Resume();
            Assert.AreEqual(MatchState.FirstHalf, fsm.CurrentState);
        }

        [Test]
        public void MatchStateMachine_OnStateChanged_FiresOnTransition()
        {
            var fsm = new MatchStateMachine();
            MatchState? oldState = null, newState = null;
            fsm.OnStateChanged += (o, n) => { oldState = o; newState = n; };

            fsm.TransitionTo(MatchState.FirstHalf);

            Assert.AreEqual(MatchState.PreKickoff, oldState);
            Assert.AreEqual(MatchState.FirstHalf, newState);
        }

        [Test]
        public void MatchStateMachine_SecondHalf_TransitionsToGoalCelebration()
        {
            var fsm = new MatchStateMachine();
            fsm.TransitionTo(MatchState.FirstHalf);
            fsm.TransitionTo(MatchState.HalfTime);
            fsm.TransitionTo(MatchState.SecondHalf);
            fsm.TransitionTo(MatchState.GoalCelebration);
            Assert.AreEqual(MatchState.GoalCelebration, fsm.CurrentState);
        }
    }
}
