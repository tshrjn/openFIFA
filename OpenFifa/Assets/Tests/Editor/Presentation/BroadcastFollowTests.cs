using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Presentation
{
    /// <summary>
    /// Broadcast camera follow (<see cref="BroadcastFraming.FollowAxis"/>): the hard dead-zone edge keeps the ball in
    /// shot, and when it lets go after a long ball the framing keeps the ball's speed instead of stopping dead.
    /// </summary>
    [TestFixture]
    [Category("FID_PRESENTATION")]
    public class BroadcastFollowTests
    {
        private const float Dt = 1f / 60f, MaxOffset = 9f, Smooth = 0.3f;

        private struct Run
        {
            public float ReleaseJump;   // largest one-frame change of camera speed when the edge lets go (m/s)
            public int Releases;
            public float MaxOffset;     // farthest the framing got from the ball (m)
        }

        /// <summary>
        /// A long ball cleared by the defending side (the attack lead points back the other way, so the framing lags into
        /// the edge), then the ball lands and slows, possession changes and the lead swings round: the edge releases.
        /// legacy = the old behaviour (spring velocity zeroed whenever the edge holds).
        /// </summary>
        private static Run Simulate(float kickSpeed, bool legacy)
        {
            float ball = 0f, bv = 0f, f = 0f, v = 0f, prevCam = 0f;
            float bvs = 0f, lead = 0f, leadV = 0f, attackLead = -5f, attackLeadV = 0f;
            bool kicked = false, wasPinned = false;
            var r = new Run();
            for (int i = 0; i < 60 * 8; i++)
            {
                float t = i * Dt;
                if (!kicked && t >= 0.5f) { bv = kickSpeed; kicked = true; }
                if (t < 2f) bv -= 0.012f * bv * bv * Dt; // air drag
                else bv = Math.Max(0f, bv - 6f * Dt);     // rolling to a stop
                ball += bv * Dt;
                // Same lead model as SinglePlayerCamera: smoothed ball speed x 0.5 s (max 6 m), damped 0.5 s.
                bvs += (bv - bvs) * (1f - (float)Math.Exp(-Dt * 3f));
                lead = BroadcastFraming.SmoothDamp(lead, Math.Max(-6f, Math.Min(6f, bvs * 0.5f)), ref leadV, 0.5f, Dt);
                if (t > 2.2f) attackLead = BroadcastFraming.SmoothDamp(attackLead, 5f, ref attackLeadV, 1.2f, Dt);
                float target = 0.85f * ball + lead + attackLead; // controlled player left at the kick spot (15 % blend)

                float prev = f;
                if (legacy)
                {
                    f = BroadcastFraming.SmoothDamp(f, target, ref v, Smooth, Dt);
                    float held = BroadcastFraming.ClampFollow(f, ball, MaxOffset);
                    if (held != f) v = 0f;
                    f = held;
                }
                else
                {
                    f = BroadcastFraming.FollowAxis(f, ref v, target, ball, bv, MaxOffset, Smooth, Dt);
                }
                bool pinned = Math.Abs(f - ball) > MaxOffset - 1e-3f;
                float cam = (f - prev) / Dt;
                if (i > 1 && wasPinned && !pinned)
                {
                    r.Releases++;
                    r.ReleaseJump = Math.Max(r.ReleaseJump, Math.Abs(cam - prevCam));
                }
                r.MaxOffset = Math.Max(r.MaxOffset, Math.Abs(f - ball));
                prevCam = cam;
                wasPinned = pinned;
            }
            return r;
        }

        [Test]
        public void FollowAxis_EdgeReleasesAfterLongBall_CameraSpeedIsContinuous()
        {
            foreach (float kick in new[] { 20f, 28f, 34f })
            {
                var fixedRun = Simulate(kick, false);
                Assert.GreaterOrEqual(fixedRun.Releases, 1, $"{kick} m/s: the scenario must pin and release the edge");
                Assert.Less(fixedRun.ReleaseJump, 0.5f, $"{kick} m/s: camera speed jumped {fixedRun.ReleaseJump:F2} m/s in one frame at the release");
                Assert.LessOrEqual(fixedRun.MaxOffset, MaxOffset + 1e-3f, "the ball must never leave the dead zone");
            }
        }

        [Test]
        public void FollowAxis_LegacyZeroedVelocity_StoppedDeadAtTheRelease()
        {
            // Documents the bug the carry fixes: zeroing the spring velocity at the edge dropped 3-6 m/s in one frame.
            var legacy = Simulate(28f, true);
            Assert.GreaterOrEqual(legacy.Releases, 1);
            Assert.Greater(legacy.ReleaseJump, 2f);
            Assert.Less(Simulate(28f, false).ReleaseJump, legacy.ReleaseJump * 0.2f);
        }

        [Test]
        public void FollowAxis_WhilePinned_MovesWithTheBallAndCarriesItsSpeed()
        {
            float f = 0f, v = 0f;
            // Ball 20 m ahead and running away at 15 m/s: the edge holds the framing 9 m behind it.
            f = BroadcastFraming.FollowAxis(f, ref v, 20f, 20f, 15f, MaxOffset, Smooth, Dt);
            Assert.AreEqual(11f, f, 1e-4f);
            Assert.AreEqual(15f, v, 1e-4f, "pinned framing carries the ball's speed");
            // A teleport-sized velocity is capped.
            BroadcastFraming.FollowAxis(0f, ref v, 50f, 50f, 900f, MaxOffset, Smooth, Dt);
            Assert.AreEqual(BroadcastFraming.MaxFollowSpeed, v, 1e-4f);
        }

        [Test]
        public void SmoothDamp_ConvergesWithoutOvershoot()
        {
            float x = 0f, v = 0f;
            for (int i = 0; i < 600; i++)
            {
                x = BroadcastFraming.SmoothDamp(x, 10f, ref v, Smooth, Dt);
                Assert.LessOrEqual(x, 10f + 1e-4f);
            }
            Assert.AreEqual(10f, x, 1e-3f);
            Assert.AreEqual(3f, BroadcastFraming.SmoothDamp(3f, 10f, ref v, Smooth, 0f), "dt 0 holds");
        }
    
        [Test]
        public void ResumeSmoothTime_EasesFromSlowStartBackToNormal()
        {
            const float Base = 0.3f;
            float start = BroadcastFraming.ResumeSmoothTime(Base, 0f);
            Assert.AreEqual(Base / BroadcastFraming.ResumeMinResponse, start, 1e-4f, "gentle start right after the hand-back");
            float prev = start;
            for (float t = 0.05f; t < BroadcastFraming.ResumeEaseSeconds; t += 0.05f)
            {
                float s = BroadcastFraming.ResumeSmoothTime(Base, t);
                Assert.LessOrEqual(s, prev + 1e-5f, $"smoothing must only tighten as the ease runs (t={t})");
                prev = s;
            }
            Assert.AreEqual(Base, BroadcastFraming.ResumeSmoothTime(Base, BroadcastFraming.ResumeEaseSeconds), 1e-6f);
            Assert.AreEqual(Base, BroadcastFraming.ResumeSmoothTime(Base, float.MaxValue), 1e-6f, "no hand-back: normal follow");
        }

        [Test]
        public void FollowAxis_AfterResumeFromRest_FirstStepIsGentlerThanNormal()
        {
            const float Base = 0.3f, Dt = 1f / 60f;
            float vEased = 0f, vNormal = 0f;
            float eased = BroadcastFraming.FollowAxis(0f, ref vEased, 4f, 4f, 0f, 9f, BroadcastFraming.ResumeSmoothTime(Base, 0f), Dt);
            float normal = BroadcastFraming.FollowAxis(0f, ref vNormal, 4f, 4f, 0f, 9f, Base, Dt);
            Assert.Greater(eased, 0f);
            Assert.Less(eased, normal, "eased hand-back moves less on its first frame than the normal follow");
        }
    }
}
