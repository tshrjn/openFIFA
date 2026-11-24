using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Presentation
{
    /// <summary>
    /// Engine-free checks of the broadcast camera rig (steady tilt and height over the whole pitch, player size, stand
    /// clearance), near-side occlusion culling, the radar auto-fade and the goal camera.
    /// </summary>
    [TestFixture]
    [Category("FID_PRESENTATION")]
    public class BroadcastCameraRigTests
    {
        private const float HalfWidth = 34f, HalfLength = 52.5f, MaxYaw = 25f, Aspect = 16f / 9f;
        private const float H = BroadcastRig.DefaultHeight, Near = BroadcastRig.DefaultNearSetback, Far = BroadcastRig.DefaultFarSetback;
        private const float Fov = BroadcastRig.DefaultFov;
        private static float FromTop => BroadcastFraming.BallScreenFromTop;

        private static BroadcastView ViewFor(float fx, float fz, float height = H, float fov = Fov)
        {
            BroadcastRig.Pose(fx, fz, HalfWidth, HalfLength, height, Near, Far, fov, FromTop, MaxYaw,
                out float cx, out float cy, out float cz, out float yaw, out float pitch);
            return new BroadcastView(cx, cy, cz, yaw, pitch, fov, Aspect);
        }

        private static float[] Xs()
        {
            var xs = new float[37];
            for (int i = 0; i < xs.Length; i++) xs[i] = -36f + 2f * i;
            return xs;
        }

        private static float[] Zs()
        {
            var zs = new float[23];
            for (int i = 0; i < zs.Length; i++) zs[i] = -55f + 5f * i;
            return zs;
        }

        // ------------------------------------------------------------------ tilt, height, framing

        [Test]
        public void Pose_BallAnywhereOnPitch_PitchBetween22And30Degrees()
        {
            foreach (float x in Xs())
            foreach (float z in Zs())
            {
                float fx = Math.Max(-HalfWidth, Math.Min(HalfWidth, x));
                BroadcastRig.Pose(fx, z, HalfWidth, HalfLength, H, Near, Far, Fov, FromTop, MaxYaw,
                    out _, out float cy, out _, out _, out float pitch);
                Assert.That(pitch, Is.InRange(22f, 30f), $"ball at ({x}, {z})");
                Assert.AreEqual(H, cy, 1e-5f, "camera height must not move with the play");
            }
        }

        [Test]
        public void Pose_NearTouchlinePlay_NoSwingTowardsOverhead()
        {
            // The old fixed 28 m / 19 m rig tilted ~38 deg at the near touchline and ~9 deg at the far one.
            BroadcastRig.Pose(HalfWidth, 0f, HalfWidth, HalfLength, H, Near, Far, Fov, FromTop, MaxYaw,
                out _, out _, out _, out _, out float nearPitch);
            BroadcastRig.Pose(-HalfWidth, 0f, HalfWidth, HalfLength, H, Near, Far, Fov, FromTop, MaxYaw,
                out _, out _, out _, out _, out float farPitch);
            Assert.That(nearPitch - farPitch, Is.LessThanOrEqualTo(8f + 1e-4f));
            Assert.That(nearPitch, Is.LessThanOrEqualTo(30f + 1e-4f));
        }

        [Test]
        public void Pose_BallAnywhereOnPitch_BallStaysWellInsideTheFrame()
        {
            foreach (float x in Xs())
            foreach (float z in Zs())
            {
                float fx = Math.Max(-HalfWidth, Math.Min(HalfWidth, x));
                var view = ViewFor(fx, z);
                Assert.IsTrue(view.Project(x, 0.11f, z, out float u, out float v), $"ball at ({x}, {z}) behind camera");
                Assert.That(v, Is.InRange(0.2f, 0.85f), $"ball at ({x}, {z}) vertical");
                Assert.That(u, Is.InRange(0.05f, 0.95f), $"ball at ({x}, {z}) horizontal");
            }
        }

        [Test]
        public void Pose_FarTouchlinePlay_PlayersHeadsInFrame()
        {
            foreach (float z in Zs())
            {
                var view = ViewFor(-HalfWidth, z);
                Assert.IsTrue(view.Project(-HalfWidth - 1f, 1.9f, z, out _, out float v));
                Assert.That(v, Is.GreaterThan(0.1f), "player head at the far touchline, z = " + z);
            }
        }

        [Test]
        public void Pose_MidPitchPlay_Players35To50PixelsAt720p()
        {
            foreach (float x in new[] { -10f, -5f, 0f, 5f, 10f })
            {
                float camX = HalfWidth + BroadcastRig.Setback(x, HalfWidth, Near, Far);
                float d = (float)Math.Sqrt((camX - x) * (camX - x) + H * H);
                Assert.That(BroadcastFraming.Pixels(1.8f, d, Fov, 720f), Is.InRange(35f, 50f), "play at x = " + x);
            }
            foreach (float x in Xs())
            {
                float fx = Math.Max(-HalfWidth, Math.Min(HalfWidth, x));
                float camX = HalfWidth + BroadcastRig.Setback(fx, HalfWidth, Near, Far);
                float d = (float)Math.Sqrt((camX - x) * (camX - x) + H * H);
                Assert.That(BroadcastFraming.Pixels(1.8f, d, Fov, 720f), Is.InRange(28f, 75f), "play at x = " + x);
            }
        }

        [Test]
        public void DefaultFov_Within26To28Degrees()
        {
            Assert.That(BroadcastRig.DefaultFov, Is.InRange(26f, 28f));
        }

        [Test]
        public void Setback_FollowsPlayAcross_ClampedToRange()
        {
            Assert.AreEqual(Near, BroadcastRig.Setback(HalfWidth, HalfWidth, Near, Far), 1e-4f);
            Assert.AreEqual(Far, BroadcastRig.Setback(-HalfWidth, HalfWidth, Near, Far), 1e-4f);
            Assert.AreEqual(Near, BroadcastRig.Setback(HalfWidth + 5f, HalfWidth, Near, Far), 1e-4f);
            Assert.AreEqual(Far, BroadcastRig.Setback(-HalfWidth - 5f, HalfWidth, Near, Far), 1e-4f);
            Assert.AreEqual((Near + Far) * 0.5f, BroadcastRig.Setback(0f, HalfWidth, Near, Far), 1e-4f);
        }

        [Test]
        public void Pose_StraightAcross_ProjectsFocusAtBallScreenHeight()
        {
            // Mid-pitch the tilt is clamped (the ball sits a little higher); 10 m towards the camera it is not.
            var view = ViewFor(10f, 0f);
            Assert.AreEqual(-90f, view.YawDegrees, 1e-3f);
            Assert.IsTrue(view.Project(10f, 0f, 0f, out float u, out float v));
            Assert.AreEqual(0.5f, u, 1e-4f);
            Assert.AreEqual(FromTop, v, 0.01f);
            // +Z is screen right from the +X side.
            Assert.IsTrue(view.Project(10f, 0f, 5f, out float uRight, out _));
            Assert.Greater(uRight, 0.5f);
        }

        [Test]
        public void Pose_NearGoal_YawClampedAndGantryStopsShortOfTheEnd()
        {
            BroadcastRig.Pose(0f, HalfLength, HalfWidth, HalfLength, H, Near, Far, Fov, FromTop, MaxYaw,
                out _, out _, out float cz, out float yaw, out _);
            Assert.AreEqual(HalfLength - BroadcastRig.GantryEndInset, cz, 1e-4f);
            Assert.That(yaw, Is.InRange(-90f, -90f + MaxYaw + 1e-3f));
            Assert.Greater(yaw, -90f, "turns towards the +Z goal");
        }

        // ------------------------------------------------------------------ stand clearance

        [Test]
        public void Rig_OverWholeDollyRange_CameraClearOfMainStandAndSeesNearTouchline()
        {
            var layout = StadiumLayout.Create(PitchSpec.Standard);
            var stand = layout.MainStand;
            var solids = layout.SightlineSolids(StandSide.Near);
            for (float d = Far; d <= Near + 1e-3f; d += 0.25f)
            {
                Assert.IsFalse(stand.Contains(d, H, true), "camera inside the main stand at depth " + d);
                Assert.That(H, Is.LessThanOrEqualTo(stand.RoofUndersideAt(d) - 1f), "roof too low at depth " + d);
                Assert.IsTrue(StadiumLayout.SegmentClear(solids, d, H, 0f, 0f), "near touchline hidden from depth " + d);
                Assert.IsTrue(StadiumLayout.SegmentClear(solids, d, H, 0f, 1.8f), "player head at the touchline hidden from " + d);
            }
        }

        // ------------------------------------------------------------------ near-side occlusion

        [Test]
        public void Occluders_DugoutsAndTunnel_OnNearSideBetweenTouchlineAndStand()
        {
            var layout = StadiumLayout.Create(PitchSpec.Standard);
            var boxes = NearSideOcclusion.Occluders(layout);
            Assert.AreEqual(3, boxes.Count);
            foreach (var b in boxes)
            {
                Assert.Greater(b.MinX, HalfWidth + layout.Spec.BoardSetback);
                Assert.LessOrEqual(b.MaxX, HalfWidth + layout.MainStand.MinDepth + 1e-3f);
                Assert.Greater(b.MaxY, 2f);
            }
        }

        [Test]
        public void Coverage_NearTouchlinePlayInFrontOfDugout_HidesIt_MidPitchPlayShowsAll()
        {
            var layout = StadiumLayout.Create(PitchSpec.Standard);
            var boxes = NearSideOcclusion.Occluders(layout);
            float dugoutZ = layout.DugoutCentres[1];
            var nearView = ViewFor(HalfWidth, dugoutZ);
            float worst = 0f;
            foreach (var b in boxes) worst = Math.Max(worst, NearSideOcclusion.Coverage(nearView, b));
            Assert.Greater(worst, NearSideOcclusion.HideCoverage, "a dugout roof fills the bottom of the frame: hide it");

            foreach (float x in new[] { -HalfWidth, -15f, 0f, 10f })
            foreach (float z in Zs())
            {
                var view = ViewFor(x, z);
                foreach (var b in boxes)
                    Assert.Less(NearSideOcclusion.Coverage(view, b), NearSideOcclusion.ShowCoverage, $"play at ({x}, {z})");
            }
        }

        [Test]
        public void Coverage_FarAwayAlongPitch_OutOfShotIsZero()
        {
            var layout = StadiumLayout.Create(PitchSpec.Standard);
            var view = ViewFor(HalfWidth, 45f);
            var minusSide = NearSideOcclusion.Occluders(layout)[0]; // dugout at -10 m
            Assert.AreEqual(0f, NearSideOcclusion.Coverage(view, minusSide), 1e-5f);
        }

        [Test]
        public void Hidden_Hysteresis_NoFlickerBetweenThresholds()
        {
            float mid = (NearSideOcclusion.HideCoverage + NearSideOcclusion.ShowCoverage) * 0.5f;
            Assert.IsFalse(NearSideOcclusion.Hidden(false, mid));
            Assert.IsTrue(NearSideOcclusion.Hidden(true, mid));
            Assert.IsTrue(NearSideOcclusion.Hidden(false, NearSideOcclusion.HideCoverage + 0.01f));
            Assert.IsFalse(NearSideOcclusion.Hidden(true, NearSideOcclusion.ShowCoverage - 0.01f));
        }

        // ------------------------------------------------------------------ radar fade

        // SinglePlayerHudCanvas radar at 1280x720: 190 x 105, bottom-centre, 20 up.
        private const float RMinX = 640f - 95f, RMaxX = 640f + 95f, RMinY = 20f, RMaxY = 125f;

        [Test]
        public void RadarCovers_NearTouchlineBallMidPitch_ButNotCentreCircle()
        {
            var nearView = ViewFor(HalfWidth - 1f, 0f);
            Assert.IsTrue(nearView.Project(HalfWidth - 1f + 2.5f, 0.11f, 0f, out float u, out float v));
            Assert.IsTrue(HudRadarFade.Covers(RMinX, RMinY, RMaxX, RMaxY, u * 1280f, (1f - v) * 720f, HudRadarFade.MarginPx720),
                "ball near the near touchline, below the framed point, is under the radar");

            var centre = ViewFor(0f, 0f);
            Assert.IsTrue(centre.Project(0f, 0.11f, 0f, out u, out v));
            Assert.IsFalse(HudRadarFade.Covers(RMinX, RMinY, RMaxX, RMaxY, u * 1280f, (1f - v) * 720f, HudRadarFade.MarginPx720));
        }

        [Test]
        public void CoversSpan_PlayerStandingAboveRadar_HeadAndFeetChecked()
        {
            Assert.IsTrue(HudRadarFade.CoversSpan(RMinX, RMinY, RMaxX, RMaxY, 640f, 110f, 160f, 0f));
            Assert.IsFalse(HudRadarFade.CoversSpan(RMinX, RMinY, RMaxX, RMaxY, 640f, 140f, 190f, 0f));
            Assert.IsFalse(HudRadarFade.CoversSpan(RMinX, RMinY, RMaxX, RMaxY, 300f, 60f, 100f, 0f));
        }

        [Test]
        public void Step_Covered_FadesToFloorInFadeTime_ThenBack()
        {
            float a = 1f;
            int frames = 0;
            while (a > HudRadarFade.CoveredAlpha && frames < 1000) { a = HudRadarFade.Step(a, true, 1f / 60f); frames++; }
            Assert.AreEqual(HudRadarFade.CoveredAlpha, a, 1e-5f);
            Assert.That(frames / 60f, Is.InRange(HudRadarFade.FadeSeconds - 0.03f, HudRadarFade.FadeSeconds + 0.03f));
            for (int i = 0; i < 60; i++) a = HudRadarFade.Step(a, false, 1f / 60f);
            Assert.AreEqual(1f, a, 1e-5f);
            Assert.AreEqual(0.5f, HudRadarFade.Step(0.5f, false, 0f), 1e-6f);
        }

        // ------------------------------------------------------------------ goal camera

        [Test]
        public void ShotZoom_OnlyWhileShotLiveNearGoal_SmoothAndMonotonic()
        {
            Assert.AreEqual(0f, GoalCamera.ShotZoomAmount(false, 50f, HalfLength));
            Assert.AreEqual(0f, GoalCamera.ShotZoomAmount(true, 0f, HalfLength));
            Assert.AreEqual(1f, GoalCamera.ShotZoomAmount(true, -45f, HalfLength));
            float last = -1f;
            for (float z = 0f; z <= HalfLength; z += 0.5f)
            {
                float a = GoalCamera.ShotZoomAmount(true, z, HalfLength);
                Assert.GreaterOrEqual(a, last - 1e-6f);
                Assert.Less(a - Math.Max(0f, last), 0.08f, "no jump at z = " + z);
                last = a;
            }
            Assert.AreEqual(Fov, GoalCamera.ZoomedFov(Fov, 0f), 1e-5f);
            Assert.That(GoalCamera.ZoomedFov(Fov, 1f), Is.InRange(20f, 22f));
        }

        [Test]
        public void ShotZoom_FullZoomNearGoal_BallStillInFrameWithinDeadZone()
        {
            // The framed point never trails the ball by more than 9 m (along) / 6.3 m (across).
            float zoomFov = GoalCamera.ZoomedFov(Fov, 1f);
            foreach (float x in new[] { -30f, 0f, 30f })
            {
                float fz = HalfLength - 12f, fx = x;
                var view = ViewFor(fx, fz, H, zoomFov);
                foreach (var (ox, oz) in new[] { (6.3f, 0f), (-6.3f, 0f), (0f, 9f), (0f, -9f) })
                {
                    Assert.IsTrue(view.Project(fx + ox, 0.11f, fz + oz, out float u, out float v));
                    Assert.That(u, Is.InRange(0f, 1f), $"focus ({fx}, {fz}) offset ({ox}, {oz})");
                    Assert.That(v, Is.InRange(0f, 1f), $"focus ({fx}, {fz}) offset ({ox}, {oz})");
                }
            }
        }

        [Test]
        public void GoalCamPose_PitchsideBesideGoal_GoalmouthInFrame()
        {
            var layout = StadiumLayout.Create(PitchSpec.Standard);
            foreach (int side in new[] { 1, -1 })
            {
                GoalCamera.Pose(side, 0f, HalfWidth, HalfLength, out float px, out float py, out float pz,
                    out float lx, out float ly, out float lz);
                // On the run-off: behind the goal line but in front of the end ad boards, outside the goal frame and net.
                float behind = side * pz - HalfLength;
                Assert.That(behind, Is.InRange(0.5f, layout.Spec.BoardSetback - 1f));
                Assert.That(Math.Abs(px), Is.InRange(7.32f * 0.5f + 3f, HalfWidth));
                Assert.That(py, Is.InRange(1f, 3f));
                float dx = lx - px, dy = ly - py, dz = lz - pz;
                float yaw = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
                float pitch = (float)(Math.Atan2(-dy, Math.Sqrt(dx * dx + dz * dz)) * 180.0 / Math.PI);
                var view = new BroadcastView(px, py, pz, yaw, pitch, GoalCamera.GoalCamFov, Aspect);
                float goalLine = side * HalfLength, postHalf = 7.32f * 0.5f;
                foreach (float gx in new[] { -postHalf, postHalf })
                foreach (float gy in new[] { 0f, 2.44f })
                {
                    Assert.IsTrue(view.Project(gx, gy, goalLine, out float u, out float v));
                    Assert.That(u, Is.InRange(0.02f, 0.98f), $"goal {side} post ({gx}, {gy})");
                    Assert.That(v, Is.InRange(0.02f, 0.98f), $"goal {side} post ({gx}, {gy})");
                }
                // A tighter look than the broadcast camera: the goal is several times bigger on screen.
                Assert.IsTrue(view.Project(-postHalf, 0f, goalLine, out float u0, out _));
                Assert.IsTrue(view.Project(postHalf, 0f, goalLine, out float u1, out _));
                var broadcast = ViewFor(0f, goalLine);
                Assert.IsTrue(broadcast.Project(-postHalf, 2.44f, goalLine, out _, out float bv0));
                Assert.IsTrue(broadcast.Project(-postHalf, 0f, goalLine, out _, out float bv1));
                Assert.IsTrue(view.Project(-postHalf, 2.44f, goalLine, out _, out float gv0));
                Assert.IsTrue(view.Project(-postHalf, 0f, goalLine, out _, out float gv1));
                Assert.Greater(Math.Abs(gv1 - gv0), 2f * Math.Abs(bv1 - bv0), "goal height on screen vs broadcast");
            }
        }

        [Test]
        public void Director_GoalStoppage_CutsOnceHoldsThenReleases()
        {
            var d = new GoalCamera.Director();
            Assert.IsFalse(d.Update(false, 0, 0.016f));
            Assert.IsTrue(d.Update(true, 1, 0.016f), "cut on the first goal-stoppage frame");
            Assert.IsTrue(d.Active);
            Assert.AreEqual(1, d.GoalSign);
            Assert.IsFalse(d.Update(true, 1, 0.5f), "no second cut while held");
            Assert.IsTrue(d.Active);
            Assert.IsFalse(d.Update(false, 0, 0.016f));
            Assert.IsFalse(d.Active, "released when the restart is set up");

            Assert.IsTrue(d.Update(true, -1, 0.016f));
            Assert.AreEqual(-1, d.GoalSign);
            d.Skip();
            Assert.IsFalse(d.Active);
            Assert.IsFalse(d.Update(true, -1, 0.016f), "a skipped goal camera does not come back for the same goal");

            var t = new GoalCamera.Director();
            t.Update(true, 1, 0f);
            for (int i = 0; i < 300 && t.Active; i++) t.Update(true, 1, 1f / 60f);
            Assert.IsFalse(t.Active, "max hold");
        }
    }
}
