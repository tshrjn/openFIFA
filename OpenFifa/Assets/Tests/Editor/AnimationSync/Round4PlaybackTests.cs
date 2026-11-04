using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    /// <summary>
    /// Fourth-round review: when the driver re-plans, defers or abandons an action (same-id refreshes, snapped contacts,
    /// withdrawn predictions, touch hand-over) and which foot takes a dribble touch (measured gait phase, planted feet).
    /// </summary>
    [TestFixture]
    [Category("FID_ANIM")]
    public class Round4PlaybackTests
    {
        private const float Tick = 1f / 60f;
        private static readonly AnimationSyncPlanner Planner = new AnimationSyncPlanner();

        private static ActionSyncPlan Plan(AnimActionKind kind, float now, float contact, SimVec3 ball, ActionSide side = ActionSide.Right)
        {
            var r = new ActionSyncRequest(kind, side, now, contact, SimVec3.Zero, SimVec3.Zero, 0f, ball,
                new SimVec3(0f, 0f, 1f), 6f, ActionSide.Right);
            return Planner.Plan(r);
        }

        private static readonly SimVec3 Ball = new SimVec3(0f, 0.11f, 0.6f);

        // ------------------------------------------------------------------ items 1 and 4: same id, moved contact

        [Test]
        public void Decide_SameIdContactSnappedByOneTick_Replans()
        {
            var plan = Plan(AnimActionKind.DribbleTouch, 1f, 1.2f, Ball);
            var d = ActionPlaybackPolicy.Decide(plan, 7, false, false, 7, 1.2f + Tick, Ball, true, 1.05f, Tick);
            Assert.AreEqual(ActionPlaybackDecision.Replan, d);
        }

        [Test]
        public void Decide_SameIdContactMovedByManyTicks_Replans()
        {
            // The first dribble prediction used NextTouchTick; the touch really came 28 ticks later.
            var plan = Plan(AnimActionKind.DribbleTouch, 1f, 1.2f, Ball);
            Assert.AreEqual(ActionPlaybackDecision.Replan,
                ActionPlaybackPolicy.Decide(plan, 7, true, false, 7, 1.2f + 28 * Tick, Ball, true, 1.1f, Tick));
        }

        [Test]
        public void Decide_SameIdUnchanged_Keeps()
        {
            var plan = Plan(AnimActionKind.GoalkeeperDive, 1f, 1.5f, Ball);
            Assert.AreEqual(ActionPlaybackDecision.Keep,
                ActionPlaybackPolicy.Decide(plan, 3, true, false, 3, 1.5f, Ball + new SimVec3(0.05f, 0f, 0f), false, 1.2f, Tick));
        }

        [Test]
        public void Decide_SameIdBallMovedBeyondSlack_Replans()
        {
            // Dive drift: the keeper (and so the reach target) moved while the AI kept steering him.
            var plan = Plan(AnimActionKind.GoalkeeperDive, 1f, 1.5f, Ball);
            Assert.AreEqual(ActionPlaybackDecision.Replan,
                ActionPlaybackPolicy.Decide(plan, 3, true, false, 3, 1.5f, Ball + new SimVec3(0.5f, 0f, 0f), false, 1.2f, Tick));
        }

        [Test]
        public void Decide_UnknownTickLength_UsesDefaultTick()
        {
            var plan = Plan(AnimActionKind.Shot, 1f, 1.5f, Ball);
            Assert.AreEqual(ActionPlaybackDecision.Replan,
                ActionPlaybackPolicy.Decide(plan, 3, false, false, 3, 1.5f + Tick, Ball, false, 1.1f, 0f));
            Assert.AreEqual(ActionPlaybackDecision.Keep,
                ActionPlaybackPolicy.Decide(plan, 3, false, false, 3, 1.5f + 0.2f * Tick, Ball, false, 1.1f, 0f));
        }

        [Test]
        public void Decide_WithdrawnBeforeContact_Aborts()
        {
            var plan = Plan(AnimActionKind.GoalkeeperDive, 1f, 1.5f, Ball);
            Assert.AreEqual(ActionPlaybackDecision.Abort,
                ActionPlaybackPolicy.Decide(plan, 3, true, true, 0, 0f, SimVec3.Zero, false, 1.3f, Tick));
        }

        [Test]
        public void Decide_NoneAfterContact_KeepsPlayingOut()
        {
            var plan = Plan(AnimActionKind.GoalkeeperDive, 1f, 1.5f, Ball);
            Assert.AreEqual(ActionPlaybackDecision.Keep,
                ActionPlaybackPolicy.Decide(plan, 3, true, true, 0, 0f, SimVec3.Zero, false, 1.6f, Tick));
        }

        [Test]
        public void Decide_PlanFinishedButSameIdStillReported_DoesNotRestart()
        {
            Assert.AreEqual(ActionPlaybackDecision.Keep,
                ActionPlaybackPolicy.Decide(null, 5, false, false, 5, 1.5f, Ball, false, 2.5f, Tick));
            Assert.AreEqual(ActionPlaybackDecision.Start,
                ActionPlaybackPolicy.Decide(null, 5, false, false, 6, 3f, Ball, false, 2.5f, Tick));
        }

        // ------------------------------------------------------------------ item 2: touch hand-over

        [Test]
        public void Decide_NextTouchWhilePreviousBlendsOut_Defers()
        {
            var plan = Plan(AnimActionKind.DribbleTouch, 0.9f, 1f, Ball);
            float now = 1f + 0.5f * DribbleTouchSolver.BlendOut;
            Assert.Greater(plan.Ik.StrikeWeightAt(now), 0.2f, "scenario: the old touch is still clearly weighted");
            Assert.AreEqual(ActionPlaybackDecision.Defer,
                ActionPlaybackPolicy.Decide(plan, 1, true, false, 2, 1.5f, Ball, true, now, Tick));
        }

        [Test]
        public void Decide_NextTouchRampAlreadyStarted_Switches()
        {
            var plan = Plan(AnimActionKind.DribbleTouch, 0.9f, 1f, Ball);
            float now = 1.05f;
            Assert.AreEqual(ActionPlaybackDecision.Start,
                ActionPlaybackPolicy.Decide(plan, 1, true, false, 2, now + 0.5f * DribbleTouchSolver.BlendIn, Ball, true, now, Tick));
        }

        [Test]
        public void Decide_NextTouchAfterBlendOut_Starts()
        {
            var plan = Plan(AnimActionKind.DribbleTouch, 0.9f, 1f, Ball);
            Assert.AreEqual(ActionPlaybackDecision.Start,
                ActionPlaybackPolicy.Decide(plan, 1, true, false, 2, 1.5f, Ball, true, plan.Ik.StrikeReleaseEnd, Tick));
        }

        [Test]
        public void Decide_TouchDuringFullBodyClip_Keeps()
        {
            var plan = Plan(AnimActionKind.Pass, 0.5f, 1f, Ball);
            Assert.AreEqual(ActionPlaybackDecision.Keep,
                ActionPlaybackPolicy.Decide(plan, 1, true, false, 2, 1.5f, Ball, true, 1.1f, Tick));
        }

        /// <summary>
        /// Item 2 end to end on the weight curve: with the tracker's hold, the next touch's id reaches the driver (which lags
        /// up to one tick) only after the previous touch's IK weight is zero, so switching plans never drops weight.
        /// </summary>
        [Test]
        public void TouchHold_NextIdArrivesAfterPreviousWeightIsZero()
        {
            var plan = Plan(AnimActionKind.DribbleTouch, 0.9f, 1f, Ball);
            float presentation = 1f + SimAnimActionTracker.TouchHold - Tick; // worst-case render lag
            Assert.AreEqual(0f, plan.Ik.StrikeWeightAt(presentation), 1e-6f);
        }

        // ------------------------------------------------------------------ re-timing a clip that is playing

        [Test]
        public void Retime_ContactLater_ContinuesFromShownFrameAndLandsOnNewContact()
        {
            var profile = ActionClipLibrary.Default(AnimActionKind.GoalkeeperDive);
            var timing = ActionTimingSolver.Solve(profile, 1f, 1.5f);
            float now = 1.3f, newContact = 1.55f;
            var re = ActionPlaybackPolicy.Retime(timing, profile, now, newContact);
            Assert.AreEqual(timing.ClipTimeAt(now), re.ClipTimeAt(now), 1e-5f, "no jump in the shown frame");
            Assert.AreEqual(newContact, re.PredictedContactTime, 1e-4f);
            Assert.IsTrue(re.Feasible);
            Assert.Less(re.PlaybackRate, timing.PlaybackRate);
        }

        [Test]
        public void Retime_ContactMuchEarlier_ClampsToMaxRate()
        {
            var profile = ActionClipLibrary.Default(AnimActionKind.GoalkeeperDive);
            var timing = ActionTimingSolver.Solve(profile, 1f, 1.5f);
            var re = ActionPlaybackPolicy.Retime(timing, profile, 1.2f, 1.21f);
            Assert.AreEqual(profile.MaxRate, re.PlaybackRate, 1e-5f);
            Assert.IsFalse(re.Feasible);
        }

        [Test]
        public void Retime_PastContactFrame_KeepsRate()
        {
            var profile = ActionClipLibrary.Default(AnimActionKind.Shot);
            var timing = ActionTimingSolver.Solve(profile, 1f, 1.5f);
            var re = ActionPlaybackPolicy.Retime(timing, profile, 1.7f, 1.8f);
            Assert.AreEqual(timing.PlaybackRate, re.PlaybackRate, 1e-6f);
            Assert.AreEqual(timing.ClipTimeAt(1.7f), re.ClipTimeAt(1.7f), 1e-5f);
        }

        // ------------------------------------------------------------------ item 3: foot from the measured gait

        [Test]
        public void SwingingFootAt_IsNeverPlanted()
        {
            for (int i = 0; i < 200; i++)
            {
                float phase = i / 200f;
                foreach (float rs in new[] { 0f, 0.13f, 0.5f, 0.9f })
                {
                    ActionSide f = DribbleTouchSolver.SwingingFootAt(phase, rs);
                    Assert.IsFalse(DribbleTouchSolver.IsPlanted(f, phase, rs), $"phase {phase} strike {rs}: {f} is planted");
                }
            }
        }

        [Test]
        public void ChooseFootFromPhase_AdvancesCycleToContact()
        {
            // Right strikes at 0, left at 0.5. At phase 0.1 the right foot is planted and the left swings toward 0.5.
            Assert.AreEqual(ActionSide.Left, DribbleTouchSolver.ChooseFootFromPhase(0.1f, 1.5f, 0f));
            // A quarter cycle later (phase 0.6) the left is down and the right swings toward 1.0.
            Assert.AreEqual(ActionSide.Right, DribbleTouchSolver.ChooseFootFromPhase(0.1f, 1.5f, 0.5f / 1.5f));
            // A whole cycle later it is the same foot as now.
            Assert.AreEqual(ActionSide.Left, DribbleTouchSolver.ChooseFootFromPhase(0.1f, 1.5f, 1f / 1.5f));
        }

        /// <summary>
        /// The nominal-stride rule assumes a cadence; when the Animator plays the cycle at a different rate (LocoRate
        /// clamping, a different clip) it picks a foot that is planted at the contact, and the touch IK drags it. The
        /// measured-phase rule never does.
        /// </summary>
        [Test]
        public void ChooseFoot_CadenceDiffersFromNominal_PhaseRuleNeverPicksPlantedFoot()
        {
            const float speed = 6f;
            float period = DribbleTouchSolver.StepPeriod(speed);
            float rate = 1f / (2f * period * 1.35f); // the clip cycles 35 % slower than the stride model assumes
            int nominalPlanted = 0, samples = 0;
            for (int i = 0; i < 40; i++)
            {
                float phase = i / 40f;
                ActionSide swingNow = DribbleTouchSolver.SwingingFootAt(phase);
                for (int j = 1; j <= 30; j++)
                {
                    float ttc = j * 0.02f;
                    float phaseAtContact = DribbleTouchSolver.Wrap01(phase + rate * ttc);
                    ActionSide nominal = DribbleTouchSolver.ChooseFoot(true, swingNow, ttc, speed, false, ActionSide.Right, ActionSide.Right);
                    ActionSide measured = DribbleTouchSolver.ChooseFootFromPhase(phase, rate, ttc);
                    samples++;
                    if (DribbleTouchSolver.IsPlanted(nominal, phaseAtContact)) nominalPlanted++;
                    Assert.IsFalse(DribbleTouchSolver.IsPlanted(measured, phaseAtContact), $"phase {phase} ttc {ttc}");
                }
            }
            Assert.Greater(nominalPlanted, samples / 10, "scenario: the nominal rule should pick planted feet here");
        }

        [Test]
        public void SwingWeight_ZeroMidStance_OneInSwing_ContinuousAtEdges()
        {
            float stance = DribbleTouchSolver.DefaultStanceFraction;
            Assert.AreEqual(0f, DribbleTouchSolver.SwingWeight(ActionSide.Right, stance * 0.5f), 1e-6f);
            Assert.AreEqual(1f, DribbleTouchSolver.SwingWeight(ActionSide.Right, 0.75f), 1e-6f);
            Assert.AreEqual(0f, DribbleTouchSolver.SwingWeight(ActionSide.Left, 0.5f + stance * 0.5f), 1e-6f);
            float prev = DribbleTouchSolver.SwingWeight(ActionSide.Right, 0f);
            for (int i = 1; i <= 1000; i++)
            {
                float w = DribbleTouchSolver.SwingWeight(ActionSide.Right, i / 1000f);
                Assert.Less(System.Math.Abs(w - prev), 0.1f, $"jump at phase {i / 1000f}");
                prev = w;
            }
        }

        [Test]
        public void GaitPhaseEstimator_120_60_30HzFrames_MeasuresCycleRateAndPhase()
        {
            foreach (float fps in new[] { 120f, 60f, 30f })
            {
                var g = new GaitPhaseEstimator();
                float rate = 1.4f, dt = 1f / fps, t = 0f;
                Assert.IsFalse(g.Valid);
                for (int i = 0; i < (int)fps; i++)
                {
                    g.Push(0.3f + rate * t, dt);
                    t += dt;
                }
                Assert.IsTrue(g.Valid, $"{fps} Hz");
                Assert.AreEqual(rate, g.CyclesPerSecond, 1e-3f, $"{fps} Hz");
                Assert.AreEqual(DribbleTouchSolver.Wrap01(0.3f + rate * (t - dt)), g.Phase, 1e-4f, $"{fps} Hz");
                Assert.AreEqual(DribbleTouchSolver.Wrap01(g.Phase + rate * 0.2f), g.PhaseAfter(0.2f), 1e-5f, $"{fps} Hz");
            }
        }

        [Test]
        public void GaitPhaseEstimator_StateRestart_ResetsRate()
        {
            var g = new GaitPhaseEstimator();
            for (int i = 0; i < 10; i++) g.Push(2f + i * 0.02f, 1f / 60f);
            Assert.IsTrue(g.Valid);
            g.Push(0.05f, 1f / 60f);
            Assert.IsFalse(g.Valid);
            g.Push(0.07f, 1f / 60f);
            Assert.IsTrue(g.Valid);
        }
    }
}
