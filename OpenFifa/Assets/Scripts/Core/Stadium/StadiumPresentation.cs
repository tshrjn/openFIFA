using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// De-tiling plan for the crowd cards (engine-free so it can be tested). Each row of seats is cut into short
    /// segments of a few seats; every segment picks, from a hash of (salt, row, segment), which texture variant it
    /// shows, where in the 32-seat strip it starts, a height scale, a brightness tweak and an idle-bob phase group.
    /// Neighbouring rows and segments therefore never repeat the same 16 m strip, which is what made the old crowd
    /// tile visibly every 30-40 px at broadcast distance.
    /// </summary>
    public static class CrowdVariation
    {
        /// <summary>Texture variants per crowd mix band (<see cref="StadiumCrowd.BandCount"/> mixes).</summary>
        public const int VariantsPerBand = 4;
        public static int TotalRows => StadiumCrowd.BandCount * VariantsPerBand;

        /// <summary>Seats in one texture row (the strip a segment picks its start from).</summary>
        public const int SeatsPerStrip = 32;
        public const int MinSeats = 3, MaxSeats = 7;

        /// <summary>Crowd materials, each bobbing with its own phase.</summary>
        public const int PhaseGroups = 4;

        public struct Segment
        {
            public int Seats;
            public int Variant;
            public int StartSeat;
            public int Group;
            /// <summary>Card height multiplier (0.92..1.08).</summary>
            public float HeightScale;
            /// <summary>-1..1: tilts the card normal a little so lighting varies segment to segment.</summary>
            public float Shade;
        }

        public static uint RowKey(uint salt, int stand, int tier, int row) =>
            StadiumCrowd.Hash(salt ^ (uint)(stand * 7919 + tier * 104729 + row * 613 + 17));

        public static Segment SegmentAt(uint rowKey, int index)
        {
            uint h = StadiumCrowd.Hash(rowKey + (uint)index * 0x9E3779B9U);
            uint h2 = StadiumCrowd.Hash(h ^ 0x68E31DA4U);
            return new Segment
            {
                Seats = MinSeats + (int)(h % (uint)(MaxSeats - MinSeats + 1)),
                Variant = (int)((h >> 8) % VariantsPerBand),
                StartSeat = (int)((h >> 12) % SeatsPerStrip),
                Group = (int)((h2 >> 4) % PhaseGroups),
                HeightScale = 0.92f + 0.16f * StadiumCrowd.Unit(h2),
                Shade = StadiumCrowd.Unit(StadiumCrowd.Hash(h2 + 7u)) * 2f - 1f,
            };
        }

        /// <summary>V range (0..1 over the whole crowd texture) of a band's variant row.</summary>
        public static void RowV(int band, int variant, out float v0, out float v1)
        {
            int row = band * VariantsPerBand + variant;
            v0 = row / (float)TotalRows;
            v1 = (row + 1) / (float)TotalRows;
        }

        /// <summary>Idle sway amplitude and the extra bob at full excitement, in texture pixels of a 64 px band.</summary>
        public const float IdleBobPx = 1.2f, ExcitedBobPx = 5f;

        // Crowd card layout (px within one 64 px band, y up), shared with StadiumTextures so the padding is checked here.
        /// <summary>Band height (px) of one crowd card row in the texture.</summary>
        public const int CardBandPx = 64;
        /// <summary>Shoulder line: base height plus up to <see cref="ShoulderJitterPx"/>.</summary>
        public const int ShoulderBasePx = 34, ShoulderJitterPx = 3;
        public const int HeadRadiusMaxPx = 8;
        /// <summary>
        /// Background rows kept clear at the top of every card. The bob shifts the cards' V down, so a card's bottom edge
        /// samples the top rows of the band below: they must be background for the whole bob plus the bilinear footprint.
        /// </summary>
        public const int CardTopPadPx = 8;
        /// <summary>Largest bob (px): the top pad minus a pixel for bilinear filtering and one for the first mip.</summary>
        public const float MaxBobPx = CardTopPadPx - 2;

        /// <summary>First row (exclusive top) any figure reaches: tallest shoulder + neck gap + the largest head.</summary>
        public static int ContentTopPx => ShoulderBasePx + ShoulderJitterPx + 1 + 2 * HeadRadiusMaxPx + 1;

        public static float GroupPhase(int group) => group * 1.7f + 0.4f;

        /// <summary>
        /// Card texture offset for a phase group: a slow idle sway at any excitement plus the bounce that grows with
        /// excitement (~1.1 Hz calm to ~2.3 Hz on a goal). <paramref name="texHeightPx"/> converts pixels to V.
        /// Never more than <see cref="IdleBobPx"/> + <see cref="ExcitedBobPx"/> pixels, so a card never shows the
        /// neighbouring band (the cards keep 6 px of background at the top and bottom).
        /// </summary>
        public static void CardOffset(float excitement, float t, int group, int texHeightPx, out float du, out float dv)
        {
            float e = excitement < 0f ? 0f : excitement > 1f ? 1f : excitement;
            float ph = GroupPhase(group);
            float idle = IdleBobPx * (0.5f + 0.5f * (float)Math.Sin(0.7 * t + ph * 1.3));
            float hz = 1.1f + 1.2f * e;
            float bounce = ExcitedBobPx * e * (float)Math.Sqrt(e) * Math.Abs((float)Math.Sin(Math.PI * hz * t + ph));
            // Clamped inside the cards' top padding: the bob never shows the neighbouring band.
            float px = Math.Min(idle + bounce, MaxBobPx);
            dv = -px / Math.Max(1, texHeightPx);
            du = 0.0012f * (0.3f + e) * (float)Math.Sin(0.9 * t + ph);
        }
    }

    /// <summary>
    /// LED advertising ring schedule (engine-free). The board texture has two rows: ads (top half) and the goal
    /// celebration graphic (bottom half). Boards are cut into segments that each start on a different ad (hash), and
    /// segments are split between two materials whose step-scroll is half a dwell apart, so neighbouring segments
    /// neither show the same ad nor change at the same moment. On a goal every board switches to the goal row and
    /// scrolls continuously for <see cref="GoalSeconds"/>.
    /// </summary>
    public static class LedBoardSchedule
    {
        public const int AdCount = 8;
        /// <summary>Length of one ad on a 0.9 m board (matches the 256 x 40 px ad cell's aspect).</summary>
        public const float AdMetres = 5.8f;
        public const int MinAdsPerSegment = 2, MaxAdsPerSegment = 4;
        public const int PhaseGroups = 2;
        public const float DwellSeconds = 9f, SlideSeconds = 0.7f;
        public const float GoalSeconds = 8f, GoalAdsPerSecond = 1.6f;

        public struct Segment
        {
            public int Ads;
            public int StartAd;
            public int Group;
        }

        public static Segment SegmentAt(uint salt, int board, int index)
        {
            uint h = StadiumCrowd.Hash(salt ^ (uint)(board * 92821 + index * 7477 + 3));
            return new Segment
            {
                Ads = MinAdsPerSegment + (int)(h % (uint)(MaxAdsPerSegment - MinAdsPerSegment + 1)),
                StartAd = (int)((h >> 8) % AdCount),
                Group = (int)((h >> 16) % PhaseGroups),
            };
        }

        /// <summary>Scroll in ads at time t for a phase group: hold for a dwell, then slide one ad (smoothstep).</summary>
        public static float AdScroll(float t, int group)
        {
            float tt = t + group * DwellSeconds * 0.5f;
            if (tt < 0f) tt = 0f;
            float step = (float)Math.Floor(tt / DwellSeconds);
            float inDwell = tt - step * DwellSeconds;
            float k = (inDwell - (DwellSeconds - SlideSeconds)) / SlideSeconds;
            k = k < 0f ? 0f : k > 1f ? 1f : k;
            k = k * k * (3f - 2f * k);
            return step + k;
        }

        /// <summary>Goal mode: active for <see cref="GoalSeconds"/> after the goal; scroll grows linearly.</summary>
        public static bool GoalActive(float sinceGoal) => sinceGoal >= 0f && sinceGoal < GoalSeconds;

        public static float GoalScroll(float sinceGoal) => sinceGoal * GoalAdsPerSecond;

        /// <summary>
        /// Material texture offset (u in texture widths, v in texture heights) for a group. The quads map the ad row
        /// (v 0.5..1); the goal row is reached with v offset -0.5. Scrolling moves the content right-to-left.
        /// </summary>
        public static void Offset(float t, float sinceGoal, int group, out float u, out float v)
        {
            if (GoalActive(sinceGoal))
            {
                u = Frac(GoalScroll(sinceGoal) / AdCount + group * 0.5f / AdCount);
                v = -0.5f;
                return;
            }
            u = Frac(AdScroll(t, group) / AdCount);
            v = 0f;
        }

        private static float Frac(float x) => x - (float)Math.Floor(x);
    }
}
