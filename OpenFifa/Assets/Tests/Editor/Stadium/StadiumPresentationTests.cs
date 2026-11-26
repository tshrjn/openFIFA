using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Stadium
{
    /// <summary>Crowd de-tiling plan and LED board schedule (engine-free).</summary>
    [TestFixture]
    [Category("FID_STADIUM")]
    public class StadiumPresentationTests
    {
        [Test]
        public void SegmentAt_SameKey_DeterministicAndInRange()
        {
            uint key = CrowdVariation.RowKey(20261104u, 0, 1, 12);
            for (int i = 0; i < 500; i++)
            {
                var a = CrowdVariation.SegmentAt(key, i);
                var b = CrowdVariation.SegmentAt(key, i);
                Assert.AreEqual(a.Seats, b.Seats);
                Assert.AreEqual(a.StartSeat, b.StartSeat);
                Assert.That(a.Seats, Is.InRange(CrowdVariation.MinSeats, CrowdVariation.MaxSeats));
                Assert.That(a.Variant, Is.InRange(0, CrowdVariation.VariantsPerBand - 1));
                Assert.That(a.StartSeat, Is.InRange(0, CrowdVariation.SeatsPerStrip - 1));
                Assert.That(a.Group, Is.InRange(0, CrowdVariation.PhaseGroups - 1));
                Assert.That(a.HeightScale, Is.InRange(0.92f, 1.08f));
                Assert.That(a.Shade, Is.InRange(-1f, 1f));
            }
        }

        [Test]
        public void SegmentAt_AlongARow_ContentRarelyRepeats()
        {
            uint key = CrowdVariation.RowKey(7u, 2, 0, 3);
            var combos = new HashSet<int>();
            var groups = new HashSet<int>();
            int adjacentSame = 0;
            CrowdVariation.Segment prev = default;
            for (int i = 0; i < 60; i++) // ~150 m of seats
            {
                var s = CrowdVariation.SegmentAt(key, i);
                combos.Add(s.Variant * 100 + s.StartSeat);
                groups.Add(s.Group);
                if (i > 0 && s.Variant == prev.Variant && s.StartSeat == prev.StartSeat) adjacentSame++;
                prev = s;
            }
            Assert.GreaterOrEqual(combos.Count, 40);
            Assert.AreEqual(CrowdVariation.PhaseGroups, groups.Count);
            Assert.LessOrEqual(adjacentSame, 1);
            // Different rows start differently.
            int same = 0;
            for (int r = 0; r < 40; r++)
            {
                var a = CrowdVariation.SegmentAt(CrowdVariation.RowKey(7u, 2, 0, r), 0);
                var b = CrowdVariation.SegmentAt(CrowdVariation.RowKey(7u, 2, 0, r + 1), 0);
                if (a.Variant == b.Variant && a.StartSeat == b.StartSeat) same++;
            }
            Assert.LessOrEqual(same, 3);
        }

        [Test]
        public void RowV_AllBandsAndVariants_TileTheTextureWithoutOverlap()
        {
            float last = 0f;
            for (int band = 0; band < StadiumCrowd.BandCount; band++)
                for (int v = 0; v < CrowdVariation.VariantsPerBand; v++)
                {
                    CrowdVariation.RowV(band, v, out float v0, out float v1);
                    Assert.AreEqual(last, v0, 1e-6f);
                    Assert.Greater(v1, v0);
                    last = v1;
                }
            Assert.AreEqual(1f, last, 1e-6f);
        }

        [Test]
        public void CardOffset_AnyExcitement_StaysInsideCardPadding()
        {
            const int texH = 1024;
            float maxPx = 0f;
            for (float e = 0f; e <= 1.0001f; e += 0.25f)
                for (float t = 0f; t < 10f; t += 0.013f)
                    for (int g = 0; g < CrowdVariation.PhaseGroups; g++)
                    {
                        CrowdVariation.CardOffset(e, t, g, texH, out float du, out float dv);
                        Assert.LessOrEqual(dv, 0f);
                        maxPx = Math.Max(maxPx, -dv * texH);
                        Assert.Less(Math.Abs(du), 0.01f);
                    }
            Assert.LessOrEqual(maxPx, CrowdVariation.IdleBobPx + CrowdVariation.ExcitedBobPx + 1e-3f);
            Assert.LessOrEqual(maxPx, 6.5f); // the cards keep >= 6 px of background top and bottom
        }

        [Test]
        public void CardOffset_Calm_IdleSwayDiffersByGroup()
        {
            CrowdVariation.CardOffset(0f, 2.3f, 0, 1024, out _, out float a);
            CrowdVariation.CardOffset(0f, 2.3f, 1, 1024, out _, out float b);
            Assert.AreNotEqual(a, b);
            float lo = 0f, hi = float.MinValue;
            for (float t = 0f; t < 20f; t += 0.05f)
            {
                CrowdVariation.CardOffset(0f, t, 0, 1024, out _, out float dv);
                lo = Math.Min(lo, dv);
                hi = Math.Max(hi, dv);
            }
            Assert.Greater(hi - lo, 0.5f / 1024f); // it moves
            Assert.LessOrEqual(-lo * 1024f, CrowdVariation.IdleBobPx + 1e-3f); // and only a little
        }

        [Test]
        public void AdScroll_HoldsThenSlidesOneAd()
        {
            Assert.AreEqual(0f, LedBoardSchedule.AdScroll(1f, 0), 1e-6f);
            Assert.AreEqual(0f, LedBoardSchedule.AdScroll(LedBoardSchedule.DwellSeconds - LedBoardSchedule.SlideSeconds - 0.01f, 0), 1e-6f);
            float mid = LedBoardSchedule.AdScroll(LedBoardSchedule.DwellSeconds - LedBoardSchedule.SlideSeconds * 0.5f, 0);
            Assert.That(mid, Is.InRange(0.3f, 0.7f));
            Assert.AreEqual(1f, LedBoardSchedule.AdScroll(LedBoardSchedule.DwellSeconds + 1f, 0), 1e-6f);
            // Group 1 changes half a dwell later than group 0.
            float t = LedBoardSchedule.DwellSeconds * 0.5f - LedBoardSchedule.SlideSeconds * 0.5f;
            Assert.That(LedBoardSchedule.AdScroll(t, 1) % 1f, Is.InRange(0.3f, 0.7f));
            Assert.AreEqual(0f, LedBoardSchedule.AdScroll(t, 0), 1e-6f);
        }

        [Test]
        public void Offset_AfterGoal_ShowsScrollingGoalRowThenReturns()
        {
            LedBoardSchedule.Offset(100f, 1000f, 0, out float u0, out float v0);
            Assert.AreEqual(0f, v0);
            LedBoardSchedule.Offset(100f, 1f, 0, out float u1, out float v1);
            LedBoardSchedule.Offset(100.5f, 1.5f, 0, out float u2, out _);
            Assert.AreEqual(-0.5f, v1);
            Assert.AreNotEqual(u1, u2);
            Assert.That(u1, Is.InRange(0f, 1f));
            LedBoardSchedule.Offset(100f, LedBoardSchedule.GoalSeconds + 0.1f, 0, out _, out float v3);
            Assert.AreEqual(0f, v3);
        }

        [Test]
        public void LedSegments_AlongABoard_StartOnDifferentAdsAndGroups()
        {
            var starts = new HashSet<int>();
            var groups = new HashSet<int>();
            for (int i = 0; i < 12; i++)
            {
                var s = LedBoardSchedule.SegmentAt(20261104u, 0, i);
                Assert.That(s.Ads, Is.InRange(LedBoardSchedule.MinAdsPerSegment, LedBoardSchedule.MaxAdsPerSegment));
                starts.Add(s.StartAd);
                groups.Add(s.Group);
            }
            Assert.GreaterOrEqual(starts.Count, 4);
            Assert.AreEqual(LedBoardSchedule.PhaseGroups, groups.Count);
        }
    }
}
