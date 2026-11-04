using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Stadium
{
    /// <summary>Engine-free checks of the procedural stadium layout: rake, rows, clearances and camera sightlines.</summary>
    [TestFixture]
    [Category("FID_STADIUM")]
    public class StadiumLayoutTests
    {
        private static StadiumLayout Layout() => StadiumLayout.Create(PitchSpec.Standard);

        // SinglePlayerCamera broadcast defaults: height 24, x = 0.3 * focusX + HalfWidth + Distance * 0.55 (Distance 40).
        private const float CamHeight = 24f;
        private static float CamDepthMin => -0.3f * 34f + 40f * 0.55f;
        private static float CamDepthMax => 0.3f * 34f + 40f * 0.55f;

        [Test]
        public void Tiers_RakeWithinLimits_LowerShallowerThanUpper()
        {
            var l = Layout();
            foreach (var p in new[] { l.MainStand, l.SideStand, l.EndStand })
            {
                Assert.That(p.Lower.SteepestRakeDegrees, Is.InRange(18f, 30f));
                Assert.That(p.Upper.SteepestRakeDegrees, Is.InRange(25f, 35.01f));
                Assert.That(p.Upper.SteepestRakeDegrees, Is.GreaterThan(p.Lower.SteepestRakeDegrees));
            }
        }

        [Test]
        public void Tiers_EveryRowSeesOverRowInFront_CValueAtLeast60mm()
        {
            var l = Layout();
            foreach (var p in new[] { l.MainStand, l.SideStand, l.EndStand })
            {
                for (int i = 1; i < p.Lower.Rows; i++) Assert.That(p.Lower.CValue(i), Is.GreaterThanOrEqualTo(0.06f - 1e-4f), "lower row " + i);
                for (int i = 1; i < p.Upper.Rows; i++) Assert.That(p.Upper.CValue(i), Is.GreaterThanOrEqualTo(0.06f - 1e-4f), "upper row " + i);
            }
        }

        [Test]
        public void Tiers_RowsStepUpwardsAndOutwards_Monotonic()
        {
            var t = Layout().SideStand.Lower;
            Assert.AreEqual(14, t.Rows);
            for (int i = 1; i < t.Rows; i++)
            {
                Assert.That(t.TreadHeight(i), Is.GreaterThan(t.TreadHeight(i - 1)));
                Assert.That(t.RowFront(i), Is.EqualTo(t.RowBack(i - 1)).Within(1e-4f));
            }
            Assert.AreEqual(-1, t.RowAt(t.FrontDepth - 0.01f));
            Assert.AreEqual(0, t.RowAt(t.FrontDepth + 0.01f));
            Assert.AreEqual(t.Rows - 1, t.RowAt(t.BackDepth - 0.01f));
        }

        [Test]
        public void UpperTier_OverhangsLowerTier_WithHeadroomBelow()
        {
            var l = Layout();
            foreach (var p in new[] { l.MainStand, l.SideStand, l.EndStand })
            {
                Assert.That(p.Upper.FrontDepth, Is.LessThan(p.Lower.BackDepth));
                int under = p.Lower.RowAt(p.Upper.FrontDepth);
                Assert.That(p.SoffitHeight(p.Upper.FrontDepth) - p.Lower.TreadHeight(under), Is.GreaterThanOrEqualTo(2.5f));
            }
        }

        [Test]
        public void Stands_ClearOfRunOffAndBoards_StartBeyondBoardRing()
        {
            var l = Layout();
            var s = l.Spec;
            Assert.That(s.BoardSetback, Is.InRange(4f, 5f));
            Assert.That(s.BoardHeight, Is.LessThanOrEqualTo(1.0f));
            Assert.That(s.BoardSetback, Is.LessThanOrEqualTo(s.RunOff), "boards stand on the grass run-off");
            foreach (var p in new[] { l.MainStand, l.SideStand, l.EndStand })
            {
                Assert.That(p.MinDepth, Is.GreaterThanOrEqualTo(s.RunOff + 2f));
                foreach (var b in p.Solids(true)) Assert.That(b.D0, Is.GreaterThanOrEqualTo(s.RunOff + 2f));
            }
            Assert.That(l.DugoutBox.D0, Is.GreaterThan(s.BoardSetback + s.BoardThickness));
            Assert.That(l.DugoutBox.D1, Is.LessThan(l.MainStand.MinDepth));
        }

        [Test]
        public void BroadcastCamera_SeesNearTouchline_OverBoardsDugoutsAndMainStand()
        {
            var l = Layout();
            var solids = l.SightlineSolids(StandSide.Near);
            for (float d = CamDepthMin; d <= CamDepthMax; d += 0.25f)
            {
                foreach (var b in solids) Assert.IsFalse(b.Contains(d, CamHeight), "camera inside the main stand at d=" + d);
                Assert.IsTrue(StadiumLayout.SegmentClear(solids, d, CamHeight, 0f, 0f), "touchline hidden from d=" + d);
                Assert.IsTrue(StadiumLayout.SegmentClear(solids, d, CamHeight, 0f, 1.8f), "player head at touchline hidden from d=" + d);
            }
        }

        [Test]
        public void BroadcastCamera_MainStandRoofAboveCamera_AndGantryBehindIt()
        {
            var p = Layout().MainStand;
            for (float d = CamDepthMin; d <= CamDepthMax; d += 0.25f)
                Assert.That(p.RoofUndersideAt(d), Is.GreaterThan(CamHeight + 1f), "roof too low over camera at d=" + d);
            Assert.That(p.BackDepth, Is.GreaterThan(CamDepthMax + 1f));
            Assert.That(p.SurfaceHeightAt(CamDepthMax), Is.LessThan(CamHeight - 3f));
        }

        [Test]
        public void GoalAreaCamera_BehindGoal_ClearOfEndStand()
        {
            // SinglePlayerCamera GoalArea: 14 m behind the goal line at 7 m, looking at the goalmouth.
            var l = Layout();
            var solids = l.SightlineSolids(StandSide.AwayEnd);
            foreach (var b in solids) Assert.IsFalse(b.Contains(14f, 7f));
            Assert.IsTrue(StadiumLayout.SegmentClear(solids, 14f, 7f, 0f, 0.5f));
        }

        [Test]
        public void Layout_StandsMeetAtCorners_PylonsInOpenCorners()
        {
            var l = Layout();
            Assert.That(l.HalfExtent(StandSide.Near), Is.EqualTo(l.Pitch.HalfLength + l.Spec.EndSetback).Within(1e-4f));
            Assert.That(l.HalfExtent(StandSide.HomeEnd), Is.EqualTo(l.Pitch.HalfWidth + l.Spec.SideSetback).Within(1e-4f));
            var c = l.CornerCentre(1, 1);
            var pyl = l.PylonBase(1, 1);
            float dx = pyl.X - c.X, dz = pyl.Z - c.Z;
            Assert.That(System.Math.Sqrt(dx * dx + dz * dz), Is.GreaterThan(l.CornerFillRadius + 0.5f));
            Assert.That(pyl.X, Is.GreaterThan(c.X));
            Assert.That(pyl.Z, Is.GreaterThan(c.Z));
        }

        [Test]
        public void ToWorld_StandsFaceThePitch_OutwardIsAwayFromCentre()
        {
            var l = Layout();
            for (int s = 0; s < 4; s++)
            {
                var side = (StandSide)s;
                var a = l.ToWorld(side, 0f, 0f, 0f);
                var b = l.ToWorld(side, 10f, 0f, 0f);
                Assert.That(b.X * b.X + b.Z * b.Z, Is.GreaterThan(a.X * a.X + a.Z * a.Z));
            }
            var near = l.ToWorld(StandSide.Near, 8f, 0f, 1f);
            Assert.That(near.X, Is.EqualTo(34f + 8f).Within(1e-4f));
        }

        [Test]
        public void Seats_TotalCapacity_IsBelievable()
        {
            var l = Layout();
            int seats = 0;
            for (int s = 0; s < 4; s++) seats += l.SeatsInStand((StandSide)s);
            Assert.That(seats, Is.InRange(15000, 60000));
        }

        [Test]
        public void Crowd_EndBias_HomeEndMostlyHomeShirts_AwayEndMostlyAway()
        {
            int[] home = new int[4], away = new int[4];
            for (int seat = 0; seat < 4000; seat++)
            {
                int h = StadiumCrowd.SeatKind(0, seat, 11u);
                int a = StadiumCrowd.SeatKind(1, seat, 11u);
                home[h + 1]++;
                away[a + 1]++;
            }
            Assert.That(home[1], Is.GreaterThan(5 * home[2]));
            Assert.That(away[2], Is.GreaterThan(5 * away[1]));
            Assert.That(home[0], Is.InRange(200, 700), "about 10% empty seats");
            Assert.AreEqual(StadiumCrowd.SeatKind(2, 17, 5u), StadiumCrowd.SeatKind(2, 17, 5u));
        }

        [Test]
        public void CrowdMotion_StillWhenCalm_BouncesMoreWhenExcited()
        {
            StadiumCrowd.CardOffset(0f, 3.3f, 0.5f, out float du, out float dv);
            Assert.AreEqual(0f, du);
            Assert.AreEqual(0f, dv);
            float maxLow = 0f, maxHigh = 0f;
            for (int i = 0; i < 400; i++)
            {
                float t = i * 0.01f;
                StadiumCrowd.CardOffset(0.3f, t, 0f, out _, out float v1);
                StadiumCrowd.CardOffset(1f, t, 0f, out _, out float v2);
                maxLow = System.Math.Max(maxLow, -v1);
                maxHigh = System.Math.Max(maxHigh, -v2);
                Assert.That(v2, Is.LessThanOrEqualTo(0f));
                Assert.That(-v2, Is.LessThanOrEqualTo(StadiumCrowd.MaxBobV + 1e-6f));
            }
            Assert.That(maxHigh, Is.GreaterThan(2f * maxLow));
            StadiumCrowd.CardOffset(5f, 1f, 0f, out _, out float clamped);
            StadiumCrowd.CardOffset(1f, 1f, 0f, out _, out float one);
            Assert.AreEqual(one, clamped);
        }
    }
}
