using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    /// <summary>
    /// Render frames (30/60/120 Hz) feeding a 60 Hz sim through <see cref="TouchEventQueue"/>: every press and release
    /// reaches the control scheme exactly once, whatever the frame/tick ratio.
    /// </summary>
    [TestFixture]
    [Category("FID_CONTROLS")]
    public class TouchEventQueueTests
    {
        private const int TickRate = 60;

        /// <summary>One finger press: down at <see cref="Down"/> s, up at <see cref="Up"/> s, at <see cref="Pos"/>.</summary>
        private struct Press
        {
            public int Id;
            public float Down, Up;
            public ControlVec2 Pos;
        }

        private sealed class Result
        {
            public int Shots, Passes, Ticks, MaxBeganPerPress;
            public readonly Dictionary<int, int> BeganCount = new Dictionary<int, int>();
            public readonly Dictionary<int, int> EndCount = new Dictionary<int, int>();
            public float LastCharge;
        }

        /// <summary>
        /// Simulates the device the way Input System EnhancedTouch reports it: Began on the first frame at or after
        /// the press (even if the finger also lifted that frame), Ended on the first later frame at or after release.
        /// Each frame pushes its samples, then runs the sim ticks the accumulator allows, draining once per tick.
        /// </summary>
        private static Result Run(float frameHz, float seconds, IList<Press> presses, TouchControlScheme scheme = null)
        {
            scheme = scheme ?? new TouchControlScheme();
            var q = new TouchEventQueue();
            var frameSamples = new List<TouchPoint>();
            var tickEvents = new List<TouchPoint>();
            var r = new Result();
            double acc = 0, dt = 1.0 / TickRate, fdt = 1.0 / frameHz;
            int frames = (int)(seconds * frameHz);
            int tick = 0;
            for (int f = 0; f <= frames; f++)
            {
                frameSamples.Clear();
                foreach (var p in presses)
                {
                    int began = (int)System.Math.Ceiling(p.Down * frameHz - 1e-6);
                    int ended = System.Math.Max((int)System.Math.Ceiling(p.Up * frameHz - 1e-6), began + 1);
                    if (f < began || f > ended) continue;
                    var phase = f == began ? ControlTouchPhase.Began : f == ended ? ControlTouchPhase.Ended : ControlTouchPhase.Stationary;
                    frameSamples.Add(new TouchPoint(p.Id, p.Pos, phase));
                }
                q.PushFrame(frameSamples);
                if (f > 0) acc += fdt;
                while (acc + 1e-9 >= dt)
                {
                    acc -= dt;
                    q.Drain(tickEvents);
                    foreach (var e in tickEvents)
                    {
                        if (e.Phase == ControlTouchPhase.Began) r.BeganCount[e.FingerId] = (r.BeganCount.TryGetValue(e.FingerId, out var b) ? b : 0) + 1;
                        if (e.Phase == ControlTouchPhase.Ended || e.Phase == ControlTouchPhase.Canceled)
                            r.EndCount[e.FingerId] = (r.EndCount.TryGetValue(e.FingerId, out var c) ? c : 0) + 1;
                    }
                    var intent = scheme.Tick(tickEvents, ControlContext.Attack((float)(tick * dt)));
                    if (intent.Action == ControlAction.Shoot) r.Shots++;
                    if (intent.Action == ControlAction.Pass) r.Passes++;
                    r.LastCharge = intent.Charge;
                    tick++;
                }
            }
            r.Ticks = tick;
            foreach (var kv in r.BeganCount) r.MaxBeganPerPress = System.Math.Max(r.MaxBeganPerPress, kv.Value);
            return r;
        }

        private static List<Press> Taps(TouchSlot slot, int count, float start, float spacing, float hold, float jitter)
        {
            var layout = TouchLayout.IPad4x3();
            var list = new List<Press>();
            for (int i = 0; i < count; i++)
            {
                // Offsets cycle through sub-frame phases so taps land on frames with 0, 1 and 2 ticks.
                float t = start + i * spacing + (i % 7) * jitter;
                list.Add(new Press { Id = 100 + i, Down = t, Up = t + hold, Pos = layout.GetButton(slot).Center });
            }
            return list;
        }

        [Test]
        public void Drain_120HzFrames_60HzTicks_EveryShortTapShootsOnce()
        {
            // 20 ms taps: a finger can go down and up between two ticks, on frames that run no tick.
            var taps = Taps(TouchSlot.Strike, 20, 0.5f, 0.6f, 0.02f, 0.0013f);
            var r = Run(120f, 13f, taps);
            Assert.AreEqual(20, r.Shots, "every tap must fire exactly one shot");
            Assert.AreEqual(1, r.MaxBeganPerPress);
            foreach (var p in taps) Assert.AreEqual(1, r.EndCount[p.Id], "release delivered once for " + p.Id);
            Assert.AreEqual(0f, r.LastCharge, "no stuck charge");
        }

        [Test]
        public void Drain_30HzFrames_TwoTicksPerFrame_BeganSeenOnce()
        {
            var taps = Taps(TouchSlot.Strike, 12, 0.5f, 0.7f, 0.05f, 0.0029f);
            var r = Run(30f, 10f, taps);
            Assert.AreEqual(1, r.MaxBeganPerPress, "Began must not be repeated on the second tick of a frame");
            Assert.AreEqual(12, r.Shots);
            Assert.AreEqual(0f, r.LastCharge);
        }

        [Test]
        public void Drain_60HzFrames_OneTickPerFrame_TapsPass()
        {
            var taps = Taps(TouchSlot.Primary, 10, 0.4f, 0.5f, 0.12f, 0.0021f);
            var r = Run(60f, 6f, taps);
            Assert.AreEqual(10, r.Passes);
            Assert.AreEqual(1, r.MaxBeganPerPress);
        }

        [Test]
        public void Drain_120HzFrames_HeldCharge_ReleasedOnFrameWithoutTick_Fires()
        {
            var layout = TouchLayout.IPad4x3();
            // Release at 1.2042 s: the 120 Hz frame at 1.2083 s carries the Ended and runs no 60 Hz tick (ticks at 1/60 multiples).
            var hold = new List<Press> { new Press { Id = 7, Down = 0.5f, Up = 1.2042f, Pos = layout.GetButton(TouchSlot.Strike).Center } };
            var r = Run(120f, 2f, hold);
            Assert.AreEqual(1, r.Shots);
            Assert.AreEqual(0f, r.LastCharge, "charge released");
        }

        [Test]
        public void PushFrame_ManyMovesBetweenDrains_CoalesceToLatestPosition()
        {
            var q = new TouchEventQueue();
            var frame = new List<TouchPoint> { new TouchPoint(1, new ControlVec2(0.1f, 0.1f), ControlTouchPhase.Began) };
            q.PushFrame(frame);
            for (int i = 1; i <= 5; i++)
            {
                frame[0] = new TouchPoint(1, new ControlVec2(0.1f + i * 0.01f, 0.1f), ControlTouchPhase.Moved);
                q.PushFrame(frame);
            }
            var outList = new List<TouchPoint>();
            q.Drain(outList);
            Assert.AreEqual(2, outList.Count);
            Assert.AreEqual(ControlTouchPhase.Began, outList[0].Phase);
            Assert.AreEqual(ControlTouchPhase.Moved, outList[1].Phase);
            Assert.AreEqual(0.15f, outList[1].Position.X, 1e-5f);
        }

        [Test]
        public void Drain_NoNewEvents_ReportsHeldFingerStationary()
        {
            var q = new TouchEventQueue();
            var frame = new List<TouchPoint> { new TouchPoint(3, new ControlVec2(0.2f, 0.3f), ControlTouchPhase.Began) };
            q.PushFrame(frame);
            var outList = new List<TouchPoint>();
            q.Drain(outList);
            q.Drain(outList);
            Assert.AreEqual(1, outList.Count);
            Assert.AreEqual(ControlTouchPhase.Stationary, outList[0].Phase);
            Assert.AreEqual(3, outList[0].FingerId);
        }

        [Test]
        public void PushFrame_BeganHeldForSeveralFrames_TreatedAsOnePress()
        {
            var q = new TouchEventQueue();
            var frame = new List<TouchPoint> { new TouchPoint(4, new ControlVec2(0.5f, 0.5f), ControlTouchPhase.Began) };
            q.PushFrame(frame);
            q.PushFrame(frame);
            q.PushFrame(frame);
            var outList = new List<TouchPoint>();
            q.Drain(outList);
            Assert.AreEqual(1, outList.Count);
            Assert.AreEqual(ControlTouchPhase.Began, outList[0].Phase);
        }

        [Test]
        public void PushFrame_FingerVanishes_IsCanceled_AndDuplicateEndIgnored()
        {
            var q = new TouchEventQueue();
            var outList = new List<TouchPoint>();
            q.PushFrame(new List<TouchPoint> { new TouchPoint(5, new ControlVec2(0.5f, 0.5f), ControlTouchPhase.Began) });
            q.Drain(outList);
            q.PushFrame(new List<TouchPoint>());
            q.PushFrame(new List<TouchPoint> { new TouchPoint(5, new ControlVec2(0.5f, 0.5f), ControlTouchPhase.Ended) });
            q.Drain(outList);
            Assert.AreEqual(1, outList.Count);
            Assert.AreEqual(ControlTouchPhase.Canceled, outList[0].Phase);
            q.Drain(outList);
            Assert.AreEqual(0, outList.Count, "nothing held any more");
        }

        [Test]
        public void CancelAll_PressBeforePause_ReleaseDuringPause_NoShot_AndPauseTapsIgnored()
        {
            var scheme = new TouchControlScheme();
            var q = new TouchEventQueue();
            var outList = new List<TouchPoint>();
            ControlVec2 strike = scheme.Layout.GetButton(TouchSlot.Strike).Center;
            q.PushFrame(new List<TouchPoint> { new TouchPoint(1, strike, ControlTouchPhase.Began) });
            q.Drain(outList);
            scheme.Tick(outList, ControlContext.Attack(0f));
            Assert.AreEqual(1, q.DownCount);

            // Paused: release finger 1, tap with finger 2.
            q.PushFrame(new List<TouchPoint> { new TouchPoint(1, strike, ControlTouchPhase.Ended), new TouchPoint(2, strike, ControlTouchPhase.Began) });
            q.CancelAll();
            q.PushFrame(new List<TouchPoint> { new TouchPoint(2, strike, ControlTouchPhase.Ended) });
            q.CancelAll();

            // Resumed.
            q.PushFrame(new List<TouchPoint>());
            q.Drain(outList);
            Assert.AreEqual(1, outList.Count);
            Assert.AreEqual(ControlTouchPhase.Canceled, outList[0].Phase);
            Assert.AreEqual(1, outList[0].FingerId);
            var intent = scheme.Tick(outList, ControlContext.Attack(1f));
            Assert.AreEqual(ControlAction.None, intent.Action);
            Assert.AreEqual(0f, intent.Charge);
            q.Drain(outList);
            Assert.AreEqual(0, outList.Count);
        }

        [Test]
        public void CancelAll_FingerHeldThroughPause_IgnoredUntilLifted()
        {
            var q = new TouchEventQueue();
            var outList = new List<TouchPoint>();
            var p = new ControlVec2(0.2f, 0.2f);
            q.PushFrame(new List<TouchPoint> { new TouchPoint(9, p, ControlTouchPhase.Began) });
            q.CancelAll();
            // Still held after resume, then lifted, then a fresh press with a new id.
            q.PushFrame(new List<TouchPoint> { new TouchPoint(9, p, ControlTouchPhase.Moved) });
            q.Drain(outList);
            Assert.AreEqual(0, outList.Count);
            q.PushFrame(new List<TouchPoint> { new TouchPoint(9, p, ControlTouchPhase.Ended) });
            q.PushFrame(new List<TouchPoint> { new TouchPoint(10, p, ControlTouchPhase.Began) });
            q.Drain(outList);
            Assert.AreEqual(1, outList.Count);
            Assert.AreEqual(10, outList[0].FingerId);
            Assert.AreEqual(ControlTouchPhase.Began, outList[0].Phase);
        }
    }
}
