using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US007")]
    public class US007_MatchTimerTests
    {
        [Test]
        public void MatchTimer_InitialPeriod_IsPreKickoff()
        {
            var timer = new MatchTimer(180f);
            Assert.AreEqual(MatchPeriod.PreKickoff, timer.CurrentPeriod,
                $"Initial period should be PreKickoff but was {timer.CurrentPeriod}");
        }

        [Test]
        public void MatchTimer_InitialRemainingTime_EqualsHalfDuration()
        {
            var timer = new MatchTimer(180f);
            Assert.AreEqual(180f, timer.RemainingSeconds, 0.01f,
                $"Initial remaining time should be 180s but was {timer.RemainingSeconds}");
        }

        [Test]
        public void MatchTimer_StartMatch_TransitionsToFirstHalf()
        {
            var timer = new MatchTimer(180f);
            timer.StartMatch();
            Assert.AreEqual(MatchPeriod.FirstHalf, timer.CurrentPeriod,
                $"After StartMatch, period should be FirstHalf but was {timer.CurrentPeriod}");
        }

        [Test]
        public void MatchTimer_Tick_DecrementsRemainingTime()
        {
            var timer = new MatchTimer(180f);
            timer.StartMatch();
            timer.Tick(10f);
            Assert.AreEqual(170f, timer.RemainingSeconds, 0.01f,
                $"After 10s tick, remaining should be 170s but was {timer.RemainingSeconds}");
        }

        [Test]
        public void MatchTimer_FirstHalfExpired_TransitionsToHalfTime()
        {
            var timer = new MatchTimer(180f);
            timer.StartMatch();
            timer.Tick(181f); // Exceed first half duration
            Assert.AreEqual(MatchPeriod.HalfTime, timer.CurrentPeriod,
                $"After first half expires, period should be HalfTime but was {timer.CurrentPeriod}");
        }

        [Test]
        public void MatchTimer_StartSecondHalf_TransitionsToSecondHalf()
        {
            var timer = new MatchTimer(180f);
            timer.StartMatch();
            timer.Tick(181f); // First half expires -> HalfTime
            timer.StartSecondHalf();
            Assert.AreEqual(MatchPeriod.SecondHalf, timer.CurrentPeriod,
                $"After StartSecondHalf, period should be SecondHalf but was {timer.CurrentPeriod}");
        }

        [Test]
        public void MatchTimer_SecondHalfExpired_TransitionsToFullTime()
        {
            var timer = new MatchTimer(180f);
            timer.StartMatch();
            timer.Tick(181f); // -> HalfTime
            timer.StartSecondHalf();
            timer.Tick(181f); // -> FullTime
            Assert.AreEqual(MatchPeriod.FullTime, timer.CurrentPeriod,
                $"After second half expires, period should be FullTime but was {timer.CurrentPeriod}");
        }

        [Test]
        public void MatchTimer_RemainingSeconds_ResetsForSecondHalf()
        {
            var timer = new MatchTimer(180f);
            timer.StartMatch();
            timer.Tick(181f); // -> HalfTime
            timer.StartSecondHalf();
            Assert.AreEqual(180f, timer.RemainingSeconds, 0.01f,
                $"Remaining time should reset to 180s for second half but was {timer.RemainingSeconds}");
        }

        [Test]
        public void MatchTimer_RemainingSeconds_NeverGoesBelowZero()
        {
            var timer = new MatchTimer(180f);
            timer.StartMatch();
            timer.Tick(500f);
            Assert.GreaterOrEqual(timer.RemainingSeconds, 0f,
                $"Remaining seconds should never go below 0 but was {timer.RemainingSeconds}");
        }

        [Test]
        public void MatchTimer_IsExpired_TrueWhenFullTime()
        {
            var timer = new MatchTimer(180f);
            timer.StartMatch();
            timer.Tick(181f);
            timer.StartSecondHalf();
            timer.Tick(181f);
            Assert.IsTrue(timer.IsMatchOver,
                "IsMatchOver should be true at FullTime");
        }

        [Test]
        public void MatchTimer_IsExpired_FalseBeforeFullTime()
        {
            var timer = new MatchTimer(180f);
            timer.StartMatch();
            timer.Tick(90f);
            Assert.IsFalse(timer.IsMatchOver,
                "IsMatchOver should be false during first half");
        }

        [Test]
        public void MatchTimer_OnPeriodChanged_FiresOnTransition()
        {
            var timer = new MatchTimer(180f);
            MatchPeriod? receivedPeriod = null;
            timer.OnPeriodChanged += (period) => receivedPeriod = period;

            timer.StartMatch();
            Assert.AreEqual(MatchPeriod.FirstHalf, receivedPeriod,
                "OnPeriodChanged should fire with FirstHalf on StartMatch");
        }

        [Test]
        public void MatchTimer_OnPeriodChanged_FiresForHalfTime()
        {
            var timer = new MatchTimer(180f);
            MatchPeriod? receivedPeriod = null;

            timer.StartMatch();
            timer.OnPeriodChanged += (period) => receivedPeriod = period;
            timer.Tick(181f);

            Assert.AreEqual(MatchPeriod.HalfTime, receivedPeriod,
                "OnPeriodChanged should fire with HalfTime when first half expires");
        }

        [Test]
        public void MatchTimer_OnPeriodChanged_FiresForFullTime()
        {
            var timer = new MatchTimer(180f);
            MatchPeriod? receivedPeriod = null;

            timer.StartMatch();
            timer.Tick(181f);
            timer.StartSecondHalf();
            timer.OnPeriodChanged += (period) => receivedPeriod = period;
            timer.Tick(181f);

            Assert.AreEqual(MatchPeriod.FullTime, receivedPeriod,
                "OnPeriodChanged should fire with FullTime when second half expires");
        }

        [Test]
        public void MatchTimer_TickDuringPreKickoff_DoesNotDecrementTime()
        {
            var timer = new MatchTimer(180f);
            timer.Tick(10f); // Tick before match starts
            Assert.AreEqual(180f, timer.RemainingSeconds, 0.01f,
                $"Tick during PreKickoff should not decrement time. Remaining: {timer.RemainingSeconds}");
        }

        [Test]
        public void MatchTimer_TickDuringHalfTime_DoesNotDecrementTime()
        {
            var timer = new MatchTimer(180f);
            timer.StartMatch();
            timer.Tick(181f); // -> HalfTime
            float remaining = timer.RemainingSeconds;
            timer.Tick(10f); // Should not change anything
            Assert.AreEqual(remaining, timer.RemainingSeconds, 0.01f,
                "Tick during HalfTime should not decrement time");
        }

        [Test]
        public void MatchTimer_TickDuringFullTime_DoesNotDecrementTime()
        {
            var timer = new MatchTimer(180f);
            timer.StartMatch();
            timer.Tick(181f);
            timer.StartSecondHalf();
            timer.Tick(181f); // -> FullTime
            float remaining = timer.RemainingSeconds;
            timer.Tick(10f);
            Assert.AreEqual(remaining, timer.RemainingSeconds, 0.01f,
                "Tick during FullTime should not decrement time");
        }
    }
}
