using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// The four stands around the sim pitch (length along Z, width along X).
    /// Near = +X, the broadcast camera side (main stand with dugouts and tunnel); Far = -X;
    /// HomeEnd = -Z (the goal Home defends in the first half); AwayEnd = +Z.
    /// </summary>
    public enum StandSide
    {
        Near = 0,
        Far = 1,
        HomeEnd = 2,
        AwayEnd = 3,
    }

    /// <summary>
    /// One raked tier of seat rows of equal depth. Tread heights follow the C-value method: each row's eye line to the
    /// focal line (the touchline or goal line, at d = 0, h = 0) clears the eyes of the row in front by TargetC,
    /// with the rake capped at MaxRakeDegrees (the cap wins if both cannot hold).
    /// d is the horizontal distance outward from the pitch line, h the height above the grass, both in metres.
    /// </summary>
    public sealed class StadiumTier
    {
        private readonly float[] _tread;

        public float FrontDepth { get; }
        public float RowDepth { get; }
        public float EyeHeight { get; }
        public float EyeSetback { get; }
        public float TargetC { get; }
        public float MaxRakeDegrees { get; }

        public int Rows => _tread.Length;
        public float BackDepth => FrontDepth + Rows * RowDepth;
        public float FirstTreadHeight => _tread[0];
        public float TopHeight => _tread[_tread.Length - 1];

        public StadiumTier(float frontDepth, float firstTreadHeight, int rows, float rowDepth, float targetC,
            float maxRakeDegrees, float eyeHeight = 1.2f, float eyeSetback = 0.4f, float minRise = 0.2f)
        {
            if (rows < 1) throw new ArgumentOutOfRangeException(nameof(rows));
            if (rowDepth <= 0f) throw new ArgumentOutOfRangeException(nameof(rowDepth));
            FrontDepth = frontDepth;
            RowDepth = rowDepth;
            EyeHeight = eyeHeight;
            EyeSetback = eyeSetback;
            TargetC = targetC;
            MaxRakeDegrees = maxRakeDegrees;
            _tread = new float[rows];
            _tread[0] = firstTreadHeight;
            float maxRise = rowDepth * (float)Math.Tan(maxRakeDegrees * Math.PI / 180.0);
            for (int i = 1; i < rows; i++)
            {
                float dPrev = EyeDepth(i - 1);
                float d = dPrev + rowDepth;
                float ePrev = _tread[i - 1] + eyeHeight;
                float eNeed = (ePrev + targetC) * d / dPrev;
                float rise = eNeed - ePrev;
                if (rise > maxRise) rise = maxRise;
                if (rise < minRise) rise = minRise;
                _tread[i] = _tread[i - 1] + rise;
            }
        }

        public float RowFront(int row) => FrontDepth + row * RowDepth;
        public float RowBack(int row) => FrontDepth + (row + 1) * RowDepth;
        public float TreadHeight(int row) => _tread[row];
        public float Rise(int row) => row <= 0 ? 0f : _tread[row] - _tread[row - 1];
        public float RakeDegrees(int row) => (float)(Math.Atan2(Rise(row), RowDepth) * 180.0 / Math.PI);
        public float EyeDepth(int row) => RowFront(row) + EyeSetback;
        public float EyeHeightAbove(int row) => _tread[row] + EyeHeight;

        /// <summary>Steepest rake of any row, degrees.</summary>
        public float SteepestRakeDegrees
        {
            get
            {
                float m = 0f;
                for (int i = 1; i < Rows; i++) m = Math.Max(m, RakeDegrees(i));
                return m;
            }
        }

        /// <summary>
        /// C-value of a row (metres): how far the eye line from this row to the focal point passes above the eyes of
        /// the row in front. Row 0 has nobody in front and returns +infinity.
        /// </summary>
        public float CValue(int row)
        {
            if (row <= 0) return float.PositiveInfinity;
            float d = EyeDepth(row), e = EyeHeightAbove(row);
            float dPrev = EyeDepth(row - 1);
            return e * dPrev / d - EyeHeightAbove(row - 1);
        }

        /// <summary>Row index whose tread covers depth d, or -1.</summary>
        public int RowAt(float d)
        {
            if (d < FrontDepth || d >= BackDepth) return -1;
            int r = (int)((d - FrontDepth) / RowDepth);
            return r >= Rows ? Rows - 1 : r;
        }
    }

    /// <summary>Axis-aligned box in a stand's (d, h) cross-section.</summary>
    public readonly struct ProfileBox
    {
        public readonly float D0, D1, H0, H1;

        public ProfileBox(float d0, float d1, float h0, float h1)
        {
            D0 = d0; D1 = d1; H0 = h0; H1 = h1;
        }

        /// <summary>Strictly inside (points on the boundary count as outside).</summary>
        public bool Contains(float d, float h) => d > D0 && d < D1 && h > H0 && h < H1;
    }

    /// <summary>
    /// Cross-section of one stand: lower tier, an upper tier that overhangs the back of the lower one, an optional flat
    /// gantry behind the upper tier, a back wall and a cantilever roof. All in (d, h) metres from the pitch line.
    /// </summary>
    public sealed class StandProfile
    {
        public StadiumTier Lower { get; }
        public StadiumTier Upper { get; }
        public float FrontWallThickness { get; } = 0.3f;
        /// <summary>Upper-tier front parapet top, above the first upper tread.</summary>
        public float UpperParapetHeight { get; } = 1.0f;
        /// <summary>Soffit (underside of the upper tier) below its first tread, at the front.</summary>
        public float UpperFrontDrop { get; }
        public float PlatformDepth { get; }
        public float BackWallThickness { get; } = 0.4f;
        public float RoofFrontDepth { get; }
        public float RoofFrontHeight { get; }
        public float RoofBackHeight { get; }
        public float RoofThickness { get; } = 0.5f;
        public float RoofFasciaDepth { get; } = 1.6f;

        public float BackDepth => Upper.BackDepth + PlatformDepth;
        public float OuterDepth => BackDepth + BackWallThickness;
        public float MinDepth => Lower.FrontDepth - FrontWallThickness;
        public float UpperParapetTop => Upper.FirstTreadHeight + UpperParapetHeight;

        public StandProfile(StadiumTier lower, StadiumTier upper, float upperFrontDrop, float platformDepth,
            float roofFrontDepth, float roofFrontHeight, float roofBackHeight)
        {
            Lower = lower ?? throw new ArgumentNullException(nameof(lower));
            Upper = upper ?? throw new ArgumentNullException(nameof(upper));
            UpperFrontDrop = upperFrontDrop;
            PlatformDepth = platformDepth;
            RoofFrontDepth = roofFrontDepth;
            RoofFrontHeight = roofFrontHeight;
            RoofBackHeight = roofBackHeight;
        }

        /// <summary>Underside of the upper tier: parallel-ish to its rake, then flat under the gantry.</summary>
        public float SoffitHeight(float d)
        {
            float d0 = Upper.FrontDepth, d1 = Upper.BackDepth;
            float h0 = Upper.FirstTreadHeight - UpperFrontDrop, h1 = Upper.TopHeight - UpperFrontDrop;
            if (d <= d0) return h0;
            if (d >= d1) return h1;
            return h0 + (h1 - h0) * (d - d0) / (d1 - d0);
        }

        /// <summary>Top surface of the roof at depth d (linear front to back).</summary>
        public float RoofHeight(float d)
        {
            float d0 = RoofFrontDepth, d1 = OuterDepth;
            float t = (d - d0) / (d1 - d0);
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return RoofFrontHeight + (RoofBackHeight - RoofFrontHeight) * t;
        }

        /// <summary>Lowest point of the roof structure (fascia bottom) over depth d, or +inf where there is no roof.</summary>
        public float RoofUndersideAt(float d)
        {
            if (d < RoofFrontDepth || d > OuterDepth) return float.PositiveInfinity;
            float under = RoofHeight(d) - RoofThickness;
            if (d <= RoofFrontDepth + 0.3f) under = RoofFrontHeight - RoofFasciaDepth;
            return under;
        }

        /// <summary>Solid volume of the stand as boxes (conservative: the concourse under the upper tier counts as solid).</summary>
        public List<ProfileBox> Solids(bool includeRoof)
        {
            var list = new List<ProfileBox>(Lower.Rows + Upper.Rows + 8);
            // Lower tier: front wall and stepped rows down to the ground.
            list.Add(new ProfileBox(MinDepth, Lower.FrontDepth, 0f, Lower.FirstTreadHeight));
            for (int i = 0; i < Lower.Rows; i++)
                list.Add(new ProfileBox(Lower.RowFront(i), Lower.RowBack(i), 0f, Lower.TreadHeight(i)));
            // Upper tier: parapet and rows hanging above the soffit.
            list.Add(new ProfileBox(Upper.FrontDepth - FrontWallThickness, Upper.FrontDepth, SoffitHeight(Upper.FrontDepth), UpperParapetTop));
            for (int i = 0; i < Upper.Rows; i++)
                list.Add(new ProfileBox(Upper.RowFront(i), Upper.RowBack(i), SoffitHeight(Upper.RowFront(i)), Upper.TreadHeight(i)));
            if (PlatformDepth > 0f)
                list.Add(new ProfileBox(Upper.BackDepth, BackDepth, SoffitHeight(BackDepth), Upper.TopHeight));
            // Concourse block behind the lower tier, under the upper tier.
            list.Add(new ProfileBox(Lower.BackDepth, OuterDepth, 0f, SoffitHeight(Lower.BackDepth)));
            // Back wall up to the roof.
            list.Add(new ProfileBox(BackDepth, OuterDepth, 0f, RoofBackHeight));
            if (includeRoof)
            {
                float lo = Math.Min(RoofFrontHeight, RoofBackHeight);
                float hi = Math.Max(RoofFrontHeight, RoofBackHeight);
                // Two boxes bracket the sloped slab; the fascia hangs below the front edge.
                list.Add(new ProfileBox(RoofFrontDepth, OuterDepth, lo - RoofThickness, hi));
                list.Add(new ProfileBox(RoofFrontDepth, RoofFrontDepth + 0.3f, RoofFrontHeight - RoofFasciaDepth, RoofFrontHeight));
            }
            return list;
        }

        public bool Contains(float d, float h, bool includeRoof)
        {
            var s = Solids(includeRoof);
            for (int i = 0; i < s.Count; i++)
                if (s[i].Contains(d, h)) return true;
            return false;
        }

        /// <summary>Highest solid point of the stand (excluding the roof) at depth d; 0 outside the stand.</summary>
        public float SurfaceHeightAt(float d)
        {
            float top = 0f;
            var s = Solids(false);
            for (int i = 0; i < s.Count; i++)
                if (d >= s[i].D0 && d <= s[i].D1 && s[i].H1 > top) top = s[i].H1;
            return top;
        }

        /// <summary>True when the straight segment (d0,h0)-(d1,h1) does not pass through any solid (sampled every step metres).</summary>
        public bool SegmentClear(float d0, float h0, float d1, float h1, bool includeRoof, float step = 0.05f)
            => StadiumLayout.SegmentClear(Solids(includeRoof), d0, h0, d1, h1, step);
    }

    /// <summary>Tunable stadium dimensions (metres, degrees). Defaults give a ~30k-seat two-tier ground.</summary>
    public sealed class StadiumLayoutSpec
    {
        /// <summary>Grass beyond the lines (PitchBuilder RunOffMetres).</summary>
        public float RunOff = 5f;
        public float BoardSetback = 4.5f;
        public float BoardHeight = 0.9f;
        public float BoardThickness = 0.25f;
        /// <summary>Lower-tier front from the touchline (side stands) and goal line (end stands).</summary>
        public float SideSetback = 8f;
        public float EndSetback = 8f;
        public float FrontWallHeight = 1.2f;
        public int LowerRows = 14;
        public float LowerRowDepth = 0.8f;
        public float LowerTargetC = 0.09f;
        public int UpperRows = 12;
        public int MainUpperRows = 14;
        public float UpperRowDepth = 0.85f;
        public float UpperTargetC = 0.06f;
        public float MaxRakeDegrees = 35f;
        /// <summary>How far the upper tier reaches forward over the back of the lower tier.</summary>
        public float UpperOverhang = 2f;
        /// <summary>Clear height under the upper-tier soffit above the lower-tier tread below its front edge.</summary>
        public float UnderTierHeadroom = 2.6f;
        public float UpperFrontDrop = 1.4f;
        /// <summary>Roof clearance above the top row / gantry.</summary>
        public float RoofHeadroom = 3.5f;
        /// <summary>Roof front edge sits this much higher than its back edge.</summary>
        public float RoofFrontLift = 1.5f;
        /// <summary>Roof front edge this far in front of the upper-tier front.</summary>
        public float RoofReach = 3f;
        /// <summary>SinglePlayerCamera broadcast defaults: height, and its depth beyond the near touchline (max).</summary>
        public float BroadcastCameraHeight = 24f;
        public float BroadcastCameraMaxDepth = 32.5f;
        /// <summary>Clearance kept between the broadcast camera and the main stand (gantry behind it, roof above it).</summary>
        public float CameraClearance = 3f;
        public float DugoutCentre = 10f;
        public float DugoutWidth = 7f;
        public float DugoutDepth = 1.9f;
        public float DugoutHeight = 2.2f;
        public float DugoutFront = 5.5f;
        public float TunnelWidth = 5f;
        public float TunnelHeight = 2.5f;
        public float AisleSpacing = 14f;
        public float AisleWidth = 1.2f;
        public int CornerSegments = 8;
        public float PylonCornerOffset = 9f;
        public float PylonHeight = 44f;
        public float RoofLampSpacing = 8f;
    }

    /// <summary>
    /// Engine-free stadium layout around a <see cref="PitchSpec"/>: stand profiles, extents, corner fills, boards,
    /// dugouts, tunnel and floodlight pylons. The Gameplay StadiumBuilder turns it into meshes.
    /// </summary>
    public sealed class StadiumLayout
    {
        public const int TriangleBudget = 60000;

        public PitchSpec Pitch { get; }
        public StadiumLayoutSpec Spec { get; }
        public StandProfile MainStand { get; }
        public StandProfile SideStand { get; }
        public StandProfile EndStand { get; }

        private StadiumLayout(PitchSpec pitch, StadiumLayoutSpec spec)
        {
            Pitch = pitch;
            Spec = spec;
            SideStand = BuildProfile(spec, spec.SideSetback, spec.UpperRows, false);
            EndStand = BuildProfile(spec, spec.EndSetback, spec.UpperRows, false);
            MainStand = BuildProfile(spec, spec.SideSetback, spec.MainUpperRows, true);
        }

        public static StadiumLayout Create(PitchSpec pitch, StadiumLayoutSpec spec = null)
        {
            if (pitch == null) throw new ArgumentNullException(nameof(pitch));
            return new StadiumLayout(pitch, spec ?? new StadiumLayoutSpec());
        }

        private static StandProfile BuildProfile(StadiumLayoutSpec s, float setback, int upperRows, bool main)
        {
            var lower = new StadiumTier(setback, s.FrontWallHeight, s.LowerRows, s.LowerRowDepth, s.LowerTargetC, s.MaxRakeDegrees);
            float upperFront = lower.BackDepth - s.UpperOverhang;
            // Soffit must clear the heads of the lower-tier row under the upper front edge.
            int rowUnder = lower.RowAt(upperFront);
            float treadUnder = rowUnder >= 0 ? lower.TreadHeight(rowUnder) : lower.TopHeight;
            float firstUpper = Math.Max(treadUnder + s.UnderTierHeadroom + s.UpperFrontDrop, lower.TopHeight + 1.0f);
            var upper = new StadiumTier(upperFront, firstUpper, upperRows, s.UpperRowDepth, s.UpperTargetC, s.MaxRakeDegrees);

            float platform = 0f;
            float roofBack = upper.TopHeight + s.RoofHeadroom;
            if (main)
            {
                // The broadcast camera rides over the main stand: give it a gantry to sit above, and a roof above it.
                platform = Math.Max(0f, s.BroadcastCameraMaxDepth + s.CameraClearance - upper.BackDepth);
                roofBack = Math.Max(roofBack, s.BroadcastCameraHeight + s.CameraClearance + 0.5f);
            }
            float roofFront = roofBack + s.RoofFrontLift;
            float roofFrontDepth = Math.Max(lower.FrontDepth + 2f, upperFront - s.RoofReach);
            return new StandProfile(lower, upper, s.UpperFrontDrop, platform, roofFrontDepth, roofFront, roofBack);
        }

        public static bool IsEnd(StandSide side) => side == StandSide.HomeEnd || side == StandSide.AwayEnd;

        public StandProfile Profile(StandSide side) =>
            side == StandSide.Near ? MainStand : IsEnd(side) ? EndStand : SideStand;

        /// <summary>Distance from the pitch centre to the line this stand faces (touchline or goal line).</summary>
        public float LineDistance(StandSide side) => IsEnd(side) ? Pitch.HalfLength : Pitch.HalfWidth;

        /// <summary>Half the stand's length along its line: side stands run to the end-stand fronts and vice versa.</summary>
        public float HalfExtent(StandSide side) =>
            IsEnd(side) ? Pitch.HalfWidth + Spec.SideSetback : Pitch.HalfLength + Spec.EndSetback;

        /// <summary>Unit outward direction (x, z) of a stand.</summary>
        public static void Outward(StandSide side, out float x, out float z)
        {
            switch (side)
            {
                case StandSide.Near: x = 1f; z = 0f; return;
                case StandSide.Far: x = -1f; z = 0f; return;
                case StandSide.HomeEnd: x = 0f; z = -1f; return;
                default: x = 0f; z = 1f; return;
            }
        }

        /// <summary>Unit along-the-stand direction (x, z): outward rotated so (outward, up, along) is a fixed handedness.</summary>
        public static void Along(StandSide side, out float x, out float z)
        {
            Outward(side, out float ox, out float oz);
            x = -oz;
            z = ox;
        }

        /// <summary>World position of stand-local (d outward from the line, a along the stand, h up).</summary>
        public SimVec3 ToWorld(StandSide side, float d, float a, float h)
        {
            Outward(side, out float ox, out float oz);
            Along(side, out float ax, out float az);
            float r = LineDistance(side) + d;
            return new SimVec3(ox * r + ax * a, h, oz * r + az * a);
        }

        /// <summary>Corner fill centre for corner (sx, sz) in {-1, +1}: where a side stand front meets an end stand front.</summary>
        public SimVec3 CornerCentre(int sx, int sz) =>
            new SimVec3(sx * (Pitch.HalfWidth + Spec.SideSetback), 0f, sz * (Pitch.HalfLength + Spec.EndSetback));

        /// <summary>Floodlight pylon foot for corner (sx, sz), in the open corner behind the lower-tier corner fill.</summary>
        public SimVec3 PylonBase(int sx, int sz)
        {
            var c = CornerCentre(sx, sz);
            return new SimVec3(c.X + sx * Spec.PylonCornerOffset, 0f, c.Z + sz * Spec.PylonCornerOffset);
        }

        /// <summary>Outer radius of the lower-tier corner fill (from the corner centre).</summary>
        public float CornerFillRadius => SideStand.Lower.BackDepth - Spec.SideSetback;

        /// <summary>Along-stand centres of the two dugouts on the near side.</summary>
        public float[] DugoutCentres => new[] { -Spec.DugoutCentre, Spec.DugoutCentre };

        /// <summary>Tunnel gap in the near lower tier, along-stand interval.</summary>
        public void TunnelSpan(out float a0, out float a1)
        {
            a0 = -Spec.TunnelWidth * 0.5f;
            a1 = Spec.TunnelWidth * 0.5f;
        }

        /// <summary>Aisle centres along a stand (symmetric about its middle), none within half an aisle-spacing of the ends.</summary>
        public List<float> AisleCentres(StandSide side)
        {
            var list = new List<float>();
            float e = HalfExtent(side) - Spec.AisleSpacing * 0.5f;
            for (float a = Spec.AisleSpacing * 0.5f; a <= e; a += Spec.AisleSpacing)
            {
                list.Add(-a);
                list.Add(a);
            }
            list.Sort();
            return list;
        }

        /// <summary>The ad board as a (d, h) box in front of a stand.</summary>
        public ProfileBox BoardBox => new ProfileBox(Spec.BoardSetback, Spec.BoardSetback + Spec.BoardThickness, 0f, Spec.BoardHeight);

        /// <summary>Dugout shell as a (d, h) box (near side only).</summary>
        public ProfileBox DugoutBox => new ProfileBox(Spec.DugoutFront, Spec.DugoutFront + Spec.DugoutDepth, 0f, Spec.DugoutHeight + 0.15f);

        /// <summary>All solids a camera on this side could look through: the stand (with roof), its board, and dugouts on the near side.</summary>
        public List<ProfileBox> SightlineSolids(StandSide side)
        {
            var list = Profile(side).Solids(true);
            list.Add(BoardBox);
            if (side == StandSide.Near) list.Add(DugoutBox);
            return list;
        }

        /// <summary>Sampled segment-vs-boxes test in a (d, h) cross-section.</summary>
        public static bool SegmentClear(List<ProfileBox> solids, float d0, float h0, float d1, float h1, float step = 0.05f)
        {
            float dd = d1 - d0, dh = h1 - h0;
            float len = (float)Math.Sqrt(dd * dd + dh * dh);
            int n = Math.Max(2, (int)Math.Ceiling(len / Math.Max(0.005f, step)));
            for (int k = 0; k <= n; k++)
            {
                float t = (float)k / n;
                float d = d0 + dd * t, h = h0 + dh * t;
                for (int i = 0; i < solids.Count; i++)
                    if (solids[i].Contains(d, h)) return false;
            }
            return true;
        }

        /// <summary>Seats in one stand (rows x seat width), ignoring aisles and the tunnel.</summary>
        public int SeatsInStand(StandSide side, float seatWidth = 0.5f)
        {
            var p = Profile(side);
            int perRow = (int)(2f * HalfExtent(side) / seatWidth);
            return perRow * (p.Lower.Rows + p.Upper.Rows);
        }
    }

    /// <summary>Crowd colour mix and card motion for the stadium crowd cards (engine-free so it can be tested).</summary>
    public static class StadiumCrowd
    {
        /// <summary>Texture bands: 0 home end, 1 away end, 2 and 3 mixed (side stands, alternating rows).</summary>
        public const int BandCount = 4;

        public static int BandFor(StandSide side, int row)
        {
            switch (side)
            {
                case StandSide.HomeEnd: return 0;
                case StandSide.AwayEnd: return 1;
                default: return (row & 1) == 0 ? 2 : 3;
            }
        }

        /// <summary>Share of seats (0..1) in home shirts and away shirts for a band; the rest wear neutral colours.</summary>
        public static void Shares(int band, out float home, out float away)
        {
            switch (band)
            {
                case 0: home = 0.78f; away = 0.03f; return;
                case 1: home = 0.08f; away = 0.72f; return;
                case 2: home = 0.5f; away = 0.18f; return;
                default: home = 0.42f; away = 0.24f; return;
            }
        }

        /// <summary>Occupancy: probability a seat is filled.</summary>
        public const float Occupancy = 0.9f;

        /// <summary>Deterministic 32-bit hash (xorshift-multiply) for per-seat choices.</summary>
        public static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352dU;
            x ^= x >> 15;
            x *= 0x846ca68bU;
            x ^= x >> 16;
            return x;
        }

        /// <summary>0..1 from a hash.</summary>
        public static float Unit(uint h) => (h & 0xFFFFFF) / 16777216f;

        /// <summary>Seat contents for (band, seat index, salt): -1 empty, 0 home shirt, 1 away shirt, 2 neutral.</summary>
        public static int SeatKind(int band, int seat, uint salt)
        {
            uint h = Hash((uint)seat * 0x9E3779B1U ^ (uint)band * 0x85EBCA77U ^ salt);
            if (Unit(h) >= Occupancy) return -1;
            Shares(band, out float home, out float away);
            float u = Unit(Hash(h ^ 0xA511E9B3U));
            if (u < home) return 0;
            if (u < home + away) return 1;
            return 2;
        }

        /// <summary>Largest vertical card offset (texture V units; a band is 1/BandCount of V).</summary>
        public const float MaxBobV = 0.02f;

        /// <summary>
        /// Card texture offset for an excitement level (0..1) at time t (seconds), for a crowd group with a phase
        /// (radians). Zero at excitement 0; bob height grows with excitement, bounce rate from ~1.1 to ~2.3 Hz.
        /// </summary>
        public static void CardOffset(float excitement, float t, float phase, out float du, out float dv)
        {
            float e = excitement < 0f ? 0f : excitement > 1f ? 1f : excitement;
            if (e <= 0f)
            {
                du = 0f;
                dv = 0f;
                return;
            }
            float amp = MaxBobV * e * (float)Math.Sqrt(e);
            float hz = 1.1f + 1.2f * e;
            float s = (float)Math.Sin(Math.PI * hz * t + phase);
            dv = -amp * Math.Abs(s);
            du = 0.0015f * e * (float)Math.Sin(0.9 * t + phase);
        }
    }
}
