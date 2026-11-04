using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID-ANIM")]
    public class ActionTimingSolverTests
    {
        private static ActionClipProfile Pass => ActionClipLibrary.Default(AnimActionKind.Pass);

        [Test]
        public void Solve_PlentyOfTime_DelaysStartAtRateOne()
        {
            ActionTimingPlan t = ActionTimingSolver.Solve(Pass, 0f, 1f);
            Assert.AreEqual(1f, t.PlaybackRate, 1e-5f);
            Assert.AreEqual(1f - Pass.ContactTime, t.StartTime, 1e-5f);
            Assert.AreEqual(0f, t.StartOffset, 1e-6f);
            Assert.IsTrue(t.Feasible);
        }

        [Test]
        public void Solve_PlentyOfTime_ClipShowsContactFrameAtContactTime()
        {
            ActionTimingPlan t = ActionTimingSolver.Solve(Pass, 0f, 1f);
            Assert.AreEqual(Pass.ContactTime, t.ClipTimeAt(1f), 1e-5f);
            Assert.AreEqual(-1f, t.ClipTimeAt(0.1f), 1e-6f);
        }

        [Test]
        public void Solve_SlightlyShort_SpeedsUpWithinMaxRate()
        {
            ActionTimingPlan t = ActionTimingSolver.Solve(Pass, 0f, 0.32f);
            Assert.AreEqual(Pass.ContactTime / 0.32f, t.PlaybackRate, 1e-4f);
            Assert.AreEqual(0f, t.StartTime, 1e-6f);
            Assert.AreEqual(0.32f, t.PredictedContactTime, 1e-5f);
            Assert.IsTrue(t.Feasible);
        }

        [Test]
        public void Solve_VeryShort_SkipsWindupAndHitsContact()
        {
            ActionTimingPlan t = ActionTimingSolver.Solve(Pass, 0f, 0.1f);
            Assert.Greater(t.StartOffset, 0f);
            Assert.AreEqual(Pass.MaxRate, t.PlaybackRate, 1e-5f);
            Assert.AreEqual(0.1f, t.PredictedContactTime, 1e-4f);
            Assert.IsTrue(t.Feasible);
        }

        [Test]
        public void Solve_Impossible_ReportsLateAndKeepsMinWindup()
        {
            ActionTimingPlan t = ActionTimingSolver.Solve(Pass, 0f, 0.02f);
            Assert.IsFalse(t.Feasible);
            Assert.Greater(t.TimingError, 0f);
            Assert.AreEqual(Pass.MinWindup, Pass.ContactTime - t.StartOffset, 1e-5f);
        }

        [Test]
        public void Solve_ContactInThePast_StartsNowAtMaxSkip()
        {
            ActionTimingPlan t = ActionTimingSolver.Solve(Pass, 1f, 0.9f);
            Assert.AreEqual(1f, t.StartTime, 1e-6f);
            Assert.AreEqual(Pass.ContactTime - Pass.MinWindup, t.StartOffset, 1e-5f);
            Assert.IsFalse(t.Feasible);
        }

        [Test]
        public void Solve_NoContactClip_StartsImmediately()
        {
            ActionTimingPlan t = ActionTimingSolver.Solve(ActionClipLibrary.Default(AnimActionKind.Fall), 2f, 2f);
            Assert.AreEqual(2f, t.StartTime, 1e-6f);
            Assert.AreEqual(1f, t.PlaybackRate, 1e-6f);
        }

        [Test]
        public void TickToSeconds_120Hz_ConvertsExactly()
        {
            Assert.AreEqual(1f, ActionTimingSolver.TickToSeconds(120, 120f), 1e-6f);
            Assert.AreEqual(0.5f, ActionTimingSolver.TickToSeconds(60, 120f), 1e-6f);
        }

        [Test]
        public void Default_EveryContactAction_HasContactInsideClipAndValidRates()
        {
            AnimActionKind[] kinds =
            {
                AnimActionKind.Pass, AnimActionKind.Shot, AnimActionKind.Cross, AnimActionKind.Header,
                AnimActionKind.StandingTackle, AnimActionKind.SlideTackle, AnimActionKind.GoalkeeperDive,
                AnimActionKind.FirstTouch, AnimActionKind.GoalkeeperThrow,
            };
            foreach (AnimActionKind k in kinds)
            {
                ActionClipProfile p = ActionClipLibrary.Default(k);
                Assert.Greater(p.ContactTime, 0f, k.ToString());
                Assert.Less(p.ContactTime, p.ClipLength, k.ToString());
                Assert.LessOrEqual(p.MinWindup, p.ContactTime, k.ToString());
                Assert.GreaterOrEqual(p.MaxRate, 1f, k.ToString());
                Assert.LessOrEqual(p.MinRate, 1f, k.ToString());
            }
        }
    }
}
