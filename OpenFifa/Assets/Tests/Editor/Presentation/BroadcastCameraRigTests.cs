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

        /// <summary>
        /// Raw (unclamped) tilt range over the whole pitch for a rig: <paramref name="lo"/>..<paramref name="hi"/>, and
        /// the most the 22-30 deg clamp has to correct it.
        /// </summary>
        private static float RawTiltRange(float near, float far, float height, out float lo, out float hi)
        {
            lo = float.MaxValue;
            hi = float.MinValue;
            foreach (float x in Xs())
            foreach (float z in Zs())
            {
                float fx = Math.Max(-HalfWidth, Math.Min(HalfWidth, x));
                float raw = BroadcastRig.RawPosePitchDegrees(fx, z, HalfWidth, HalfLength, height, near, far, Fov, FromTop, MaxYaw);
                lo = Math.Min(lo, raw);
                hi = Math.Max(hi, raw);
            }
            return Math.Max(Math.Max(0f, BroadcastRig.MinPitchDegrees - lo), Math.Max(0f, hi - BroadcastRig.MaxPitchDegrees));
        }

        /// <summary>
        /// The 22-30 deg range of the clamped tilt is guaranteed by the clamp, so on its own it proves nothing about the
        /// rig. What the geometry must do: the riding depth keeps the RAW tilt (before the clamp) within
        /// <see cref="BroadcastRig.MaxClampCorrectionDegrees"/> of the range everywhere, so the clamp is a small trim
        /// (the ball sits a little off its screen height, <see cref="Pose_BallAnywhereOnPitch_BallStaysWellInsideTheFrame"/>)
        /// and not what holds the tilt steady. Measured: raw 14.4-35.2 deg at the default height and setbacks.
        /// </summary>
        [Test]
        public void Pose_BallAnywhereOnPitch_RawTiltNeedsOnlyASmallClampTrim()
        {
            foreach (float x in Xs())
            foreach (float z in Zs())
            {
                float fx = Math.Max(-HalfWidth, Math.Min(HalfWidth, x));
                BroadcastRig.Pose(fx, z, HalfWidth, HalfLength, H, Near, Far, Fov, FromTop, MaxYaw,
                    out _, out float cy, out _, out _, out float pitch);
                float raw = BroadcastRig.RawPosePitchDegrees(fx, z, HalfWidth, HalfLength, H, Near, Far, Fov, FromTop, MaxYaw);
                Assert.AreEqual(Math.Max(BroadcastRig.MinPitchDegrees, Math.Min(BroadcastRig.MaxPitchDegrees, raw)), pitch, 1e-4f,
                    $"Pose's tilt is the clamped raw tilt, ball at ({x}, {z})");
                Assert.That(raw, Is.InRange(BroadcastRig.MinPitchDegrees - BroadcastRig.MaxClampCorrectionDegrees,
                    BroadcastRig.MaxPitchDegrees + BroadcastRig.MaxClampCorrectionDegrees), $"raw tilt, ball at ({x}, {z})");
                Assert.AreEqual(H, cy, 1e-5f, "camera height must not move with the play");
            }
            float trim = RawTiltRange(Near, Far, H, out float lo, out float hi);
            Console.WriteLine($"raw tilt {lo:F2}..{hi:F2} deg, largest clamp trim {trim:F2} deg");
            // Mid-pitch play (the middle third across, the centre half along) is framed by the geometry, not the clamp.
            float mid = BroadcastRig.RawPosePitchDegrees(HalfWidth / 3f, 0f, HalfWidth, HalfLength, H, Near, Far, Fov, FromTop, MaxYaw);
            Assert.That(mid, Is.InRange(BroadcastRig.MinPitchDegrees, BroadcastRig.MaxPitchDegrees), "near-centre play needs no clamp");
        }

        /// <summary>
        /// Regression guard: the check above must fail for a rig whose steady tilt would come only from the clamp. A
        /// fixed-depth gantry (the old rig's single 28 m line; also a rig that stops riding, near = far) swings the raw
        /// tilt far outside the range, more than the allowed trim.
        /// </summary>
        [Test]
        public void RawTiltGuard_FixedDepthRig_WouldFail()
        {
            float riding = RawTiltRange(Near, Far, H, out float rLo, out float rHi);
            float fixedOld = RawTiltRange(28f, 28f, H, out float fLo, out float fHi);
            float fixedNear = RawTiltRange(Near, Near, H, out _, out _);
            float fixedFar = RawTiltRange(Far, Far, H, out _, out _);
            Console.WriteLine($"riding {rLo:F1}..{rHi:F1} (trim {riding:F1}); fixed 28 m {fLo:F1}..{fHi:F1} (trim {fixedOld:F1}); " +
                              $"fixed {Near} m trim {fixedNear:F1}; fixed {Far} m trim {fixedFar:F1}");
            Assert.LessOrEqual(riding, BroadcastRig.MaxClampCorrectionDegrees);
            Assert.Greater(fixedOld, BroadcastRig.MaxClampCorrectionDegrees, "a fixed 28 m line relies on the clamp");
            Assert.Greater(fixedNear, BroadcastRig.MaxClampCorrectionDegrees, "always at the near setback relies on the clamp");
            Assert.Greater(fixedFar, BroadcastRig.MaxClampCorrectionDegrees, "always at the far setback relies on the clamp");
            Assert.Less(rHi - rLo, fHi - fLo - 5f, "riding the depth narrows the raw tilt swing");
        }

        [Test]
        public void Pose_NearTouchlinePlay_NoSwingTowardsOverhead()
        {
            // The old fixed 28 m / 19 m rig tilted ~38 deg at the near touchline and ~9 deg at the far one. Raw tilts,
            // so this is the geometry, not the clamp (the clamped values differ by at most 8 deg by construction).
            float near = BroadcastRig.RawPosePitchDegrees(HalfWidth, 0f, HalfWidth, HalfLength, H, Near, Far, Fov, FromTop, MaxYaw);
            float far = BroadcastRig.RawPosePitchDegrees(-HalfWidth, 0f, HalfWidth, HalfLength, H, Near, Far, Fov, FromTop, MaxYaw);
            Assert.That(near - far, Is.LessThanOrEqualTo(21.5f), $"raw near {near:F1} v far {far:F1}");
            Assert.That(near, Is.LessThanOrEqualTo(BroadcastRig.MaxPitchDegrees + BroadcastRig.MaxClampCorrectionDegrees));
            float oldNear = BroadcastRig.RawPosePitchDegrees(HalfWidth, 0f, HalfWidth, HalfLength, 19f, 28f, 28f, Fov, FromTop, MaxYaw);
            float oldFar = BroadcastRig.RawPosePitchDegrees(-HalfWidth, 0f, HalfWidth, HalfLength, 19f, 28f, 28f, Fov, FromTop, MaxYaw);
            Assert.Greater(oldNear - oldFar, 21.5f, $"the old rig ({oldNear:F1} v {oldFar:F1}) fails the same check");
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
                Assert.That(py, Is.InRange(3f, 6f), "low pitchside camera, above the players' heads");
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

        private static BroadcastView GoalView(int side, float ballX, out float px, out float py, out float pz)
        {
            GoalCamera.Pose(side, ballX, HalfWidth, HalfLength, out px, out py, out pz, out float lx, out float ly, out float lz);
            float dx = lx - px, dy = ly - py, dz = lz - pz;
            float yaw = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
            float pitch = (float)(Math.Atan2(-dy, Math.Sqrt(dx * dx + dz * dz)) * 180.0 / Math.PI);
            return new BroadcastView(px, py, pz, yaw, pitch, GoalCamera.GoalCamFov, Aspect);
        }

        [Test]
        public void GoalCamPose_StadiumSkylineAboveFrame_NoEmptyCornerInShot()
        {
            // Above the corner fill rims and the roof fronts there is no geometry (clear colour): the first goal camera
            // showed the open corner behind the goal as a dark slab across the top-left of the frame.
            var layout = StadiumLayout.Create(PitchSpec.Standard);
            var skyline = StadiumSkyline.Points(layout);
            foreach (int side in new[] { 1, -1 })
            foreach (float ballX in new[] { -6f, -2f, 0f, 2f, 6f })
            {
                var view = GoalView(side, ballX, out _, out _, out _);
                Assert.That(StadiumSkyline.LowestInView(view, skyline), Is.LessThan(-0.02f), $"goal {side}, ball x {ballX}");
            }
        }

        [Test]
        public void StadiumSkyline_OldLevelGoalCam_WasDetected()
        {
            // Regression guard for the check itself: the first goal camera (2 m up, level, 40 deg) fails it.
            var layout = StadiumLayout.Create(PitchSpec.Standard);
            float px = 14f, py = 2f, pz = HalfLength + 1.5f, lx = 0f, ly = 1f, lz = HalfLength - 2.5f;
            float dx = lx - px, dy = ly - py, dz = lz - pz;
            var view = new BroadcastView(px, py, pz, (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI),
                (float)(Math.Atan2(-dy, Math.Sqrt(dx * dx + dz * dz)) * 180.0 / Math.PI), 40f, Aspect);
            Assert.Greater(StadiumSkyline.LowestInView(view, StadiumSkyline.Points(layout)), 0.1f);
        }

        [Test]
        public void GoalCamPose_NearFrustumClearOfStadiumAndGoalFrame()
        {
            var layout = StadiumLayout.Create(PitchSpec.Standard);
            const float postHalf = 7.32f * 0.5f, crossbar = 2.44f, netDepth = 2.5f;
            foreach (int side in new[] { 1, -1 })
            {
                var view = GoalView(side, 0f, out float px, out float py, out float pz);
                Assert.IsFalse(StadiumSkyline.InsideSolid(layout, px, py, pz), "camera inside stadium geometry");
                // Points of the frustum from 0.3 m to 8 m out, over a 5 x 5 grid of the frame.
                double yaw = view.YawDegrees * Math.PI / 180.0, pitch = view.PitchDegrees * Math.PI / 180.0;
                double th = Math.Tan(GoalCamera.GoalCamFov * 0.5 * Math.PI / 180.0);
                double fx = Math.Sin(yaw) * Math.Cos(pitch), fy = -Math.Sin(pitch), fz = Math.Cos(yaw) * Math.Cos(pitch);
                double rx = Math.Cos(yaw), rz = -Math.Sin(yaw);
                double ux = Math.Sin(yaw) * Math.Sin(pitch), uy = Math.Cos(pitch), uz = Math.Cos(yaw) * Math.Sin(pitch);
                for (float depth = 0.3f; depth <= 8f; depth += 0.5f)
                for (int i = 0; i <= 4; i++)
                for (int j = 0; j <= 4; j++)
                {
                    double sx = (i / 2.0 - 1.0) * th * Aspect * depth, sy = (j / 2.0 - 1.0) * th * depth;
                    float x = (float)(px + fx * depth + rx * sx + ux * sy);
                    float y = (float)(py + fy * depth + uy * sy);
                    float z = (float)(pz + fz * depth + rz * sx + uz * sy);
                    string at = $"goal {side}: frustum point ({x:F1}, {y:F1}, {z:F1}) at {depth} m";
                    Assert.IsFalse(StadiumSkyline.InsideSolid(layout, x, y, z), at + " inside stadium geometry");
                    bool inGoal = Math.Abs(x) <= postHalf + 0.2f && y <= crossbar + 0.2f
                                  && side * z >= HalfLength - 0.2f && side * z <= HalfLength + netDepth;
                    Assert.IsFalse(inGoal, at + " inside the goal frame / net");
                }
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

        [Test]
        public void Director_PausedLongerThanMaxHold_GoalCameraHoldsAndResumesItsTimer()
        {
            var d = new GoalCamera.Director();
            Assert.IsTrue(d.Update(true, 1, 0.016f));
            d.Update(true, 1, 1f);
            // A 10 s pause (presentation time keeps running): the goal camera holds and its timer stops.
            for (int i = 0; i < 600; i++) Assert.IsFalse(d.Update(true, 1, 1f / 60f, paused: true));
            Assert.IsTrue(d.Active, "a pause longer than the max hold must not end the goal camera");
            Assert.AreEqual(1f, d.Elapsed, 1e-4f, "the timer does not advance while paused");
            // After the pause the remaining ~3 s play out.
            int frames = 0;
            while (d.Active && frames < 1000) { d.Update(true, 1, 1f / 60f); frames++; }
            Assert.That(frames / 60f, Is.InRange(GoalCamera.GoalCamMaxSeconds - 1f - 0.05f, GoalCamera.GoalCamMaxSeconds - 1f + 0.05f));
        }

        [Test]
        public void SkipInput_PauseAndResumePresses_DoNotSkipTheGoalCamera()
        {
            const ulong esc = 1UL, a = 2UL;
            const float dt = 1f / 60f;
            var k = new GoalCamera.SkipInput();
            Assert.IsFalse(k.Update(true, 0UL, false, dt), "arming frame");
            for (int i = 0; i < 30; i++) Assert.IsFalse(k.Update(true, 0UL, false, dt)); // past the grace time
            // Esc goes down: the controller toggles pause before the skip check, so this frame is already paused.
            Assert.IsFalse(k.Update(true, esc, true, dt), "the pause press is not a skip");
            Assert.IsFalse(k.Update(true, esc, true, dt));
            Assert.IsFalse(k.Update(true, 0UL, true, dt));
            Assert.IsFalse(k.Update(true, a, true, dt), "menu input while paused is not a skip");
            Assert.IsFalse(k.Update(true, 0UL, true, dt));
            // Esc again resumes: that frame is unpaused but the resume press is not a skip either.
            Assert.IsFalse(k.Update(true, esc, false, dt), "the resume press is not a skip");
            Assert.IsFalse(k.Update(true, esc, false, dt), "still held");
            Assert.IsFalse(k.Update(true, 0UL, false, dt));
            // A new press after the pause still skips.
            Assert.IsTrue(k.Update(true, a, false, dt), "a fresh press after the pause skips");
        }

        [Test]
        public void SkipInput_GraceTimerDoesNotRunWhilePaused()
        {
            const ulong a = 2UL;
            const float dt = 1f / 60f;
            var k = new GoalCamera.SkipInput();
            k.Update(true, 0UL, false, dt);
            k.Update(true, 0UL, false, dt);
            for (int i = 0; i < 120; i++) k.Update(true, 0UL, true, dt);
            k.Update(true, 0UL, false, dt);
            Assert.That(k.Elapsed, Is.LessThan(GoalCamera.GoalCamSkipGraceSeconds), "two seconds of pause count for nothing");
            Assert.IsFalse(k.Update(true, a, false, dt), "still inside the grace time");
            // Re-arms for the next goal camera.
            Assert.IsFalse(k.Update(false, 0UL, false, dt));
            Assert.IsFalse(k.Update(true, a, false, dt), "held from before the cut");
        }
    }
}
