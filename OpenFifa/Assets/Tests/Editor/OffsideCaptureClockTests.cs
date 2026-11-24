using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    /// <summary>
    /// The offside presentation runs on presentation time (PresentationClock): the fixed capture frame time under
    /// Time.captureFramerate, real unscaled time otherwise. On wall-clock time a capture (about 1 rendered frame a
    /// second) ran the whole 2.5 s sequence in two or three frames, so it never showed.
    /// </summary>
    [TestFixture]
    [Category("Offside")]
    public class OffsideCaptureClockTests
    {
        private const float CaptureFrame = 1f / 30f;
        private const float SlowCaptureWallClock = 1.1f; // seconds of real time per rendered capture frame

        private static int FramesToComplete(float captureDeltaTime, float unscaledDeltaTime, out int lineFrames)
        {
            var timeline = new OffsidePresentationTimeline();
            timeline.Advance(0f);
            int frames = 0;
            lineFrames = 0;
            while (!timeline.IsComplete && frames < 10000)
            {
                timeline.Advance((float)PresentationClock.FrameSeconds(captureDeltaTime, unscaledDeltaTime));
                frames++;
                if (timeline.HasFired(OffsideCue.LinesShow) && !timeline.HasFired(OffsideCue.LinesHide)) lineFrames++;
            }
            return frames;
        }

        [Test]
        public void FrameSeconds_Capturing_UsesCaptureFrameTimeNotWallClock()
        {
            Assert.AreEqual(CaptureFrame, PresentationClock.FrameSeconds(CaptureFrame, SlowCaptureWallClock), 1e-6);
        }

        [Test]
        public void FrameSeconds_LivePlay_UsesUnscaledTime()
        {
            Assert.AreEqual(0.016, PresentationClock.FrameSeconds(0f, 0.016f), 1e-6);
            Assert.AreEqual(0.0, PresentationClock.FrameSeconds(0f, -1f), 1e-9);
        }

        [Test]
        public void Timeline_SlowCaptureOnPresentationClock_LastsFullSequenceInCaptureFrames()
        {
            int frames = FramesToComplete(CaptureFrame, SlowCaptureWallClock, out int lineFrames);
            var timing = new OffsidePresentationTimingData();
            int expected = (int)System.Math.Ceiling(timing.TotalDuration / CaptureFrame);
            Assert.That(frames, Is.InRange(expected - 1, expected + 1), "2.5 s sequence at 30 fps capture");
            // Lines are up from LinesShow to LinesHide: ~43 captured frames, enough for the lines still.
            Assert.That(lineFrames, Is.GreaterThanOrEqualTo(40), $"lines visible for {lineFrames} capture frames");
        }

        [Test]
        public void Timeline_SlowCaptureOnWallClock_WouldEndWithinThreeFrames()
        {
            // The old behaviour (unscaled wall-clock time while capturing), kept as the regression's reason.
            int frames = FramesToComplete(0f, SlowCaptureWallClock, out int lineFrames);
            Assert.That(frames, Is.LessThanOrEqualTo(3));
            Assert.That(lineFrames, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void Timeline_LivePlay_SameDurationAsBefore()
        {
            int frames = FramesToComplete(0f, 1f / 60f, out _);
            Assert.That(frames, Is.InRange(149, 151), "2.5 s at 60 fps");
        }
    }
}
