using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("Offside")]
    public class OffsidePresentationTimelineTests
    {
        private static List<OffsideCue> RunToEnd(OffsidePresentationTimeline timeline, float step = 0.05f)
        {
            var all = new List<OffsideCue>();
            all.AddRange(timeline.Advance(0f));
            int guard = 0;
            while (!timeline.IsComplete && guard++ < 1000)
                all.AddRange(timeline.Advance(step));
            return all;
        }

        [Test]
        public void Advance_Zero_FiresWhistleAndSlowMotionImmediately()
        {
            var timeline = new OffsidePresentationTimeline();
            var cues = timeline.Advance(0f);
            CollectionAssert.Contains(cues, OffsideCue.Whistle);
            CollectionAssert.Contains(cues, OffsideCue.SlowMotionStart);
            CollectionAssert.DoesNotContain(cues, OffsideCue.LinesShow);
        }

        [Test]
        public void Advance_ToEnd_FiresEveryCueExactlyOnce()
        {
            var cues = RunToEnd(new OffsidePresentationTimeline());
            foreach (OffsideCue cue in System.Enum.GetValues(typeof(OffsideCue)))
                Assert.AreEqual(1, cues.Count(c => c == cue), $"{cue} should fire exactly once");
            Assert.AreEqual(OffsideCue.Complete, cues.Last());
        }

        [Test]
        public void Advance_ToEnd_TotalDurationIsAboutTwoAndHalfSeconds()
        {
            var timeline = new OffsidePresentationTimeline();
            RunToEnd(timeline, 0.01f);
            Assert.That(timeline.Elapsed, Is.InRange(2.3f, 2.7f));
        }

        [Test]
        public void Advance_CueTimes_AreInBroadcastOrder()
        {
            var t = new OffsidePresentationTimeline();
            Assert.Less(t.GetCueTime(OffsideCue.FlagRaise), t.GetCueTime(OffsideCue.FreezeStart));
            Assert.LessOrEqual(t.GetCueTime(OffsideCue.CameraCutIn), t.GetCueTime(OffsideCue.LinesShow));
            Assert.Less(t.GetCueTime(OffsideCue.LinesShow), t.GetCueTime(OffsideCue.LinesHide));
            Assert.LessOrEqual(t.GetCueTime(OffsideCue.LinesHide), t.GetCueTime(OffsideCue.CameraCutOut));
            Assert.LessOrEqual(t.GetCueTime(OffsideCue.BannerHide), t.GetCueTime(OffsideCue.Complete));
        }

        [Test]
        public void GetTimeScale_FollowsSlowMotionFreezeRestore()
        {
            var timing = new OffsidePresentationTimingData(slowMotionScale: 0.25f);
            var t = new OffsidePresentationTimeline(timing);
            t.Advance(0.1f);
            Assert.AreEqual(0.25f, t.GetTimeScale(), 1e-5f, "Slow motion");
            t.Advance(0.5f); // 0.6
            Assert.AreEqual(0f, t.GetTimeScale(), 1e-5f, "Freeze");
            t.Advance(1.6f); // 2.2
            Assert.AreEqual(1f, t.GetTimeScale(), 1e-5f, "Restored");
        }

        [Test]
        public void GetLineReveal_SweepsFromZeroToOne()
        {
            var timing = new OffsidePresentationTimingData(linesShow: 0.5f, lineRevealDuration: 0.2f);
            var t = new OffsidePresentationTimeline(timing);
            t.Advance(0.4f);
            Assert.AreEqual(0f, t.GetLineReveal(), 1e-5f);
            t.Advance(0.2f); // 0.6
            Assert.AreEqual(0.5f, t.GetLineReveal(), 1e-3f);
            t.Advance(0.5f); // 1.1
            Assert.AreEqual(1f, t.GetLineReveal(), 1e-5f);
        }

        [Test]
        public void Skip_MidSequence_EmitsTeardownThenComplete()
        {
            var t = new OffsidePresentationTimeline();
            t.Advance(0.8f); // cut-in and lines shown
            var cues = t.Skip().ToList();

            CollectionAssert.IsSubsetOf(
                new[] { OffsideCue.LinesHide, OffsideCue.CameraCutOut, OffsideCue.TimeRestore, OffsideCue.BannerHide },
                cues);
            Assert.AreEqual(OffsideCue.Complete, cues.Last());
            Assert.IsTrue(t.IsComplete);
            Assert.IsTrue(t.WasSkipped);
            Assert.AreEqual(1f, t.GetTimeScale(), 1e-5f);
        }

        [Test]
        public void Skip_DoesNotEmitIntroCuesNotYetFired()
        {
            var t = new OffsidePresentationTimeline();
            t.Advance(0f);
            var cues = t.Skip();
            CollectionAssert.DoesNotContain(cues, OffsideCue.LinesShow);
            CollectionAssert.DoesNotContain(cues, OffsideCue.CameraCutIn);
        }

        [Test]
        public void Skip_AfterComplete_ReturnsNothing()
        {
            var t = new OffsidePresentationTimeline();
            RunToEnd(t);
            Assert.AreEqual(0, t.Skip().Count);
            Assert.AreEqual(0, t.Advance(1f).Count);
        }

        [Test]
        public void Timing_SlowMotionScale_IsClamped()
        {
            Assert.AreEqual(1f, new OffsidePresentationTimingData(slowMotionScale: 3f).SlowMotionScale);
            Assert.AreEqual(0f, new OffsidePresentationTimingData(slowMotionScale: -1f).SlowMotionScale);
        }
    }
}
