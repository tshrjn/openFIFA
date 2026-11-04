using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    /// <summary>
    /// Fourth-round review: gamepad presses that start and end between two sim ticks were lost because the input read
    /// isPressed once per tick. The edge queue collects per-frame pressed/released edges and drains them per tick.
    /// </summary>
    [TestFixture]
    [Category("FID_CONTROLS")]
    public class GamepadEdgeQueueTests
    {
        private const float TickDt = 1f / 60f;

        private struct Tap
        {
            public float Start, End;
            public GamepadButton Button;
            public Tap(float start, float duration, GamepadButton b = GamepadButton.South) { Start = start; End = start + duration; Button = b; }
        }

        private static GamepadButton HeldAt(List<Tap> taps, float t)
        {
            GamepadButton b = GamepadButton.None;
            foreach (var tap in taps) if (tap.Start <= t && t < tap.End) b |= tap.Button;
            return b;
        }

        private static void Edges(List<Tap> taps, float from, float to, out GamepadButton pressed, out GamepadButton released)
        {
            pressed = released = GamepadButton.None;
            foreach (var tap in taps)
            {
                if (tap.Start > from && tap.Start <= to) pressed |= tap.Button;
                if (tap.End > from && tap.End <= to) released |= tap.Button;
            }
        }

        /// <summary>
        /// Plays <paramref name="taps"/> through frames at <paramref name="fps"/> and a 60 Hz sim, and counts the kicks of
        /// <paramref name="kind"/> the resolver fires. useQueue false is the old behaviour: the held state read per tick.
        /// </summary>
        private static int Run(List<Tap> taps, float fps, float seconds, bool useQueue, ControlAction kind = ControlAction.Pass,
            System.Func<float, bool> paused = null)
        {
            var resolver = new GamepadControlResolver(ControlPreset.Classic());
            var q = new GamepadEdgeQueue();
            float frameDt = 1f / fps, acc = 0f, simTime = 0f, prev = 0f;
            int fired = 0;
            for (float t = frameDt; t <= seconds; t += frameDt)
            {
                GamepadButton held = HeldAt(taps, t);
                Edges(taps, prev, t, out var pressed, out var released);
                prev = t;
                bool advancing = paused == null || !paused(t);
                q.PushFrame(new GamepadFrame(held), pressed, released);
                if (!advancing)
                {
                    q.DiscardEdges();
                    continue;
                }
                acc += frameDt;
                while (acc >= TickDt - 1e-6f)
                {
                    acc -= TickDt;
                    simTime += TickDt;
                    GamepadFrame f = useQueue ? q.Drain() : new GamepadFrame(held);
                    if (resolver.Tick(f, ControlContext.Attack(simTime)).Action == kind) fired++;
                }
            }
            return fired;
        }

        private static List<Tap> ShortTaps(int n, float duration)
        {
            var taps = new List<Tap>();
            for (int i = 0; i < n; i++) taps.Add(new Tap(0.5f + i * 0.31f + i * 0.0037f, duration));
            return taps;
        }

        [Test]
        public void Drain_120_60_30HzFrames_EveryShortTapPassesOnce()
        {
            foreach (float fps in new[] { 120f, 60f, 30f })
            {
                var taps = ShortTaps(12, 0.006f);
                Assert.AreEqual(12, Run(taps, fps, 5f, true), $"{fps} Hz frames");
            }
        }

        [Test]
        public void PerTickIsPressed_ShortTaps_AreLost_RegressionBaseline()
        {
            // Documents the bug the queue fixes: sampling the held state per tick misses taps between ticks.
            Assert.Less(Run(ShortTaps(12, 0.006f), 120f, 5f, false), 12);
        }

        [Test]
        public void Drain_120_60_30HzFrames_NormalPressesStillPassOnce()
        {
            foreach (float fps in new[] { 120f, 60f, 30f })
                Assert.AreEqual(8, Run(ShortTaps(8, 0.12f), fps, 4f, true), $"{fps} Hz frames");
        }

        [Test]
        public void Drain_ReleaseAndRepressWithinOneTick_GivesTwoKicks()
        {
            // Held for 0.2 s, released for 5 ms (inside one 120 Hz frame pair), pressed again for 0.2 s.
            var taps = new List<Tap> { new Tap(0.5f, 0.2f), new Tap(0.705f, 0.2f) };
            foreach (float fps in new[] { 120f, 60f, 30f })
                Assert.AreEqual(2, Run(taps, fps, 2f, true), $"{fps} Hz frames");
        }

        [Test]
        public void Drain_PressAndReleaseInsideOneFrame_StillOneKick()
        {
            var q = new GamepadEdgeQueue();
            var r = new GamepadControlResolver(ControlPreset.Classic());
            int fired = 0;
            float t = 1f;
            // The device shows the button up at the sample, but reports both edges for the frame.
            q.PushFrame(new GamepadFrame(GamepadButton.None), GamepadButton.South, GamepadButton.South);
            for (int i = 0; i < 4; i++, t += TickDt)
                if (r.Tick(q.Drain(), ControlContext.Attack(t)).Action == ControlAction.Pass) fired++;
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Drain_HeldCharge_ReleasedOnce()
        {
            var taps = new List<Tap> { new Tap(0.5f, 0.6f) };
            foreach (float fps in new[] { 120f, 60f, 30f })
                Assert.AreEqual(1, Run(taps, fps, 2f, true), $"{fps} Hz frames");
        }

        [Test]
        public void DiscardEdges_TapWhilePaused_NotDeliveredOnResume()
        {
            var taps = new List<Tap> { new Tap(1.0f, 0.006f) };
            Assert.AreEqual(0, Run(taps, 120f, 2f, true, ControlAction.Pass, t => t > 0.9f && t < 1.2f));
        }

        [Test]
        public void Drain_TriggerTapBetweenTicks_ReportedAsHeldForOneTick()
        {
            var q = new GamepadEdgeQueue();
            q.PushFrame(new GamepadFrame(GamepadButton.None) { RightTrigger = 0.1f }, GamepadButton.RightTrigger, GamepadButton.RightTrigger);
            Assert.Greater(q.Drain().RightTrigger, 0.5f);
            Assert.LessOrEqual(q.Drain().RightTrigger, 0.5f);
        }

        [Test]
        public void Drain_NoNewFrames_KeepsLatestSticksAndHeldButtons()
        {
            var q = new GamepadEdgeQueue();
            q.PushFrame(new GamepadFrame(GamepadButton.LeftShoulder, new ControlVec2(0.3f, 0.7f)) { LeftTrigger = 0.8f },
                GamepadButton.LeftShoulder | GamepadButton.LeftTrigger, GamepadButton.None);
            q.Drain();
            var f = q.Drain(); // a 30 Hz frame runs two ticks
            Assert.AreEqual(GamepadButton.LeftShoulder, f.Buttons);
            Assert.AreEqual(0.7f, f.LeftStick.Y, 1e-6f);
            Assert.AreEqual(0.8f, f.LeftTrigger, 1e-6f);
        }
    
        [Test]
        public void PushFrame_AnalogTrigger_UsesTheResolverHysteresis()
        {
            var q = new GamepadEdgeQueue();
            var r = new GamepadControlResolver();
            float t = 0f;
            bool Shield(float lt)
            {
                q.PushFrame(new GamepadFrame(GamepadButton.None) { LeftTrigger = lt }, GamepadButton.None, GamepadButton.None);
                var f = q.Drain();
                bool queueHeld = (q.Reported & GamepadButton.LeftTrigger) != 0;
                bool shield = (r.Tick(f, ControlContext.Attack(t += 1f / 60f)).Held & ControlHeld.Shield) != 0;
                Assert.AreEqual(queueHeld, shield, "queue and resolver agree at LT " + lt);
                return shield;
            }
            Assert.IsFalse(Shield(0.5f));
            Assert.IsTrue(Shield(0.6f));
            Assert.IsTrue(Shield(0.4f));
            Assert.IsTrue(Shield(0.52f));
            Assert.IsFalse(Shield(0.3f));
            Assert.IsFalse(Shield(0.5f));
        }
}
}
