using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("Offside")]
    public class MatchStoppageTests
    {
        [Test]
        public void BeginStoppage_DuringFirstHalf_EntersStoppage()
        {
            var fsm = new MatchStateMachine();
            fsm.TransitionTo(MatchState.FirstHalf);
            Assert.IsTrue(fsm.BeginStoppage());
            Assert.AreEqual(MatchState.Stoppage, fsm.CurrentState);
        }

        [Test]
        public void EndStoppage_AfterFirstHalfStoppage_ReturnsToFirstHalf()
        {
            var fsm = new MatchStateMachine();
            fsm.TransitionTo(MatchState.FirstHalf);
            fsm.BeginStoppage();
            fsm.EndStoppage();
            Assert.AreEqual(MatchState.FirstHalf, fsm.CurrentState);
        }

        [Test]
        public void EndStoppage_AfterSecondHalfStoppage_ReturnsToSecondHalf()
        {
            var fsm = new MatchStateMachine();
            fsm.TransitionTo(MatchState.FirstHalf);
            fsm.TransitionTo(MatchState.HalfTime);
            fsm.TransitionTo(MatchState.SecondHalf);
            fsm.BeginStoppage();
            fsm.EndStoppage();
            Assert.AreEqual(MatchState.SecondHalf, fsm.CurrentState);
        }

        [Test]
        public void BeginStoppage_BeforeKickoff_ReturnsFalse()
        {
            var fsm = new MatchStateMachine();
            Assert.IsFalse(fsm.BeginStoppage());
            Assert.AreEqual(MatchState.PreKickoff, fsm.CurrentState);
        }

        [Test]
        public void PauseResume_DuringStoppage_ReturnsToStoppage()
        {
            var fsm = new MatchStateMachine();
            fsm.TransitionTo(MatchState.FirstHalf);
            fsm.BeginStoppage();
            fsm.Pause();
            fsm.Resume();
            Assert.AreEqual(MatchState.Stoppage, fsm.CurrentState);
            fsm.EndStoppage();
            Assert.AreEqual(MatchState.FirstHalf, fsm.CurrentState);
        }

        [Test]
        public void EndStoppage_WhenNotInStoppage_DoesNothing()
        {
            var fsm = new MatchStateMachine();
            fsm.TransitionTo(MatchState.FirstHalf);
            fsm.EndStoppage();
            Assert.AreEqual(MatchState.FirstHalf, fsm.CurrentState);
        }

        [Test]
        public void MatchStateDisplay_Stoppage_ShowsFreeKick()
        {
            Assert.AreEqual("FREE KICK", new MatchStateDisplay().GetStateText(MatchState.Stoppage));
        }
    }
}
