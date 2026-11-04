using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Look of the procedural broadcast turf. Colours are sRGB (0..1, what a colour picker shows).
    /// Defaults follow the judging notes: a muted olive green inside #3E7A2A..#4C8A34, mow stripes about 5.5 m wide
    /// running goal line to goal line (perpendicular to the touchlines) with about 10% light/dark contrast.
    /// </summary>
    public sealed class TurfSettings
    {
        /// <summary>Base albedo, sRGB. #457B2E.</summary>
        public float BaseR = 69f / 255f, BaseG = 123f / 255f, BaseB = 46f / 255f;

        /// <summary>Worn turf (dry, thinned grass with soil showing), sRGB. #7A7448.</summary>
        public float WornR = 122f / 255f, WornG = 116f / 255f, WornB = 72f / 255f;

        /// <summary>Nominal mow stripe width (m) along the pitch length. The real width is length / round(length / width).</summary>
        public float StripeWidth = 5.5f;

        /// <summary>Light-stripe / dark-stripe sRGB luma ratio minus one (0.10 = 10%; about +-11% in linear luminance).</summary>
        public float StripeContrast = 0.10f;

        /// <summary>Distance (m) over which a stripe fades to its neighbour at the boundary (soft mower edge, no aliasing).</summary>
        public float StripeEdge = 0.35f;

        /// <summary>0 = plain stripes; up to 1 = full checkerboard from a cross mow along the width. 0.3 reads as a slight checker.</summary>
        public float CrossMow = 0f;

        /// <summary>Large-scale (8-30 m) brightness variation, +- fraction of sRGB value. Breaks up tiling at distance.</summary>
        public float MacroAmount = 0.045f;

        /// <summary>Scales goalmouth / penalty spot / centre spot wear (0 = pristine).</summary>
        public float WearAmount = 1f;

        /// <summary>Tangent-space tilt of the base normal per stripe (blades laid towards / away from the camera), view-dependent sheen.</summary>
        public float SheenTilt = 0.05f;

        /// <summary>Smoothness (stored in base-map alpha): base, +- per stripe, and how much wear removes.</summary>
        public float Smoothness = 0.24f, StripeSmoothness = 0.05f, WearSmoothnessLoss = 0.12f;

        /// <summary>Detail (blade) texture world tile size (m).</summary>
        public float DetailTileMetres = 3f;

        /// <summary>Penalty mark distance from the goal line (m).</summary>
        public float PenaltySpotDistance = 11f;

        public uint Seed = 1729u;

        /// <summary>Stable text key of every field, for caching generated textures.</summary>
        public string CacheKey()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            float[] f =
            {
                BaseR, BaseG, BaseB, WornR, WornG, WornB, StripeWidth, StripeContrast, StripeEdge, CrossMow, MacroAmount,
                WearAmount, SheenTilt, Smoothness, StripeSmoothness, WearSmoothnessLoss, DetailTileMetres, PenaltySpotDistance
            };
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < f.Length; i++) sb.Append(f[i].ToString("R", inv)).Append('|');
            sb.Append(Seed.ToString(inv));
            return sb.ToString();
        }
    }

    /// <summary>
    /// Engine-free maths for the procedural turf: mow stripe mask, colour targets, macro variation, wear, and the
    /// RGBA32 texel data of the base map, base normal map and the tiling blade detail maps (deterministic, seeded).
    /// Coordinates: x along the pitch length, z along the width, both centred on the centre spot (metres).
    /// Texel rows go bottom-up (row 0 = v 0 = -z), as Texture2D.SetPixelData expects.
    /// </summary>
    public static class TurfMath
    {
        // ------------------------------------------------------------------ colour

        public static float SrgbToLinear(float c)
        {
            return c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4);
        }

        public static float LinearToSrgb(float c)
        {
            if (c <= 0f) return 0f;
            return c <= 0.0031308f ? c * 12.92f : 1.055f * (float)Math.Pow(c, 1.0 / 2.4) - 0.055f;
        }

        /// <summary>Rec.709 relative luminance of an sRGB colour (decoded to linear first).</summary>
        public static float LinearLuminance(float r, float g, float b)
        {
            return 0.2126f * SrgbToLinear(r) + 0.7152f * SrgbToLinear(g) + 0.0722f * SrgbToLinear(b);
        }

        /// <summary>Rec.709 luma on the sRGB-encoded values (perceptual, what a picker or screenshot shows).</summary>
        public static float Luma(float r, float g, float b)
        {
            return 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }

        /// <summary>HSV saturation (max - min) / max.</summary>
        public static float Saturation(float r, float g, float b)
        {
            float mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            return mx <= 0f ? 0f : (mx - mn) / mx;
        }

        /// <summary>Multiplier k so that (1 + k) / (1 - k) = 1 + contrast; the light stripe is base * (1 + k), the dark base * (1 - k).</summary>
        public static float StripeHalfAmplitude(float contrast)
        {
            return contrast <= 0f ? 0f : contrast / (2f + contrast);
        }

        // ------------------------------------------------------------------ stripes

        /// <summary>Number of stripes across an extent: round(extent / nominalWidth), at least 1.</summary>
        public static int StripeCount(float extent, float nominalWidth)
        {
            if (nominalWidth <= 0f) return 1;
            return Math.Max(1, (int)Math.Round(extent / nominalWidth));
        }

        /// <summary>
        /// +1 on even (light) bands, -1 on odd (dark) bands of <paramref name="count"/> equal bands spanning
        /// [-extent/2, extent/2], ramping linearly to 0 within <paramref name="edge"/> metres of each boundary.
        /// </summary>
        public static float BandSign(float pos, float extent, int count, float edge)
        {
            float w = extent / count;
            float t = (pos + extent * 0.5f) / w;
            int idx = (int)Math.Floor(t);
            float frac = t - idx;
            float d = Math.Min(frac, 1f - frac) * w;
            float s = (((idx % 2) + 2) % 2) == 0 ? 1f : -1f;
            float ramp = edge <= 0f ? 1f : Math.Min(1f, d / edge);
            return s * ramp;
        }

        /// <summary>
        /// Mow mask in [-1, 1]: +1 light stripe, -1 dark stripe, 0 outside the field (the run-off is unstriped).
        /// Stripes run goal line to goal line, alternating along the length; CrossMow blends in a checker.
        /// </summary>
        public static float StripeMask(float x, float z, float fieldLength, float fieldWidth, TurfSettings s)
        {
            if (Math.Abs(x) > fieldLength * 0.5f || Math.Abs(z) > fieldWidth * 0.5f) return 0f;
            float along = BandSign(x, fieldLength, StripeCount(fieldLength, s.StripeWidth), s.StripeEdge);
            if (s.CrossMow <= 0f) return along;
            float across = BandSign(z, fieldWidth, StripeCount(fieldWidth, s.StripeWidth), s.StripeEdge);
            float c = Math.Min(1f, s.CrossMow);
            return along * (1f - c) + along * across * c;
        }

        // ------------------------------------------------------------------ noise

        private static uint Hash(int x, int y, uint seed)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ seed * 0xcb1ab31fu;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                h *= 0x27d4eb2du;
                h ^= h >> 16;
                return h;
            }
        }

        private static float Lattice(int x, int y, uint seed)
        {
            return (Hash(x, y, seed) & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
        }

        private static int Wrap(int i, int p)
        {
            return p > 0 ? ((i % p) + p) % p : i;
        }

        /// <summary>Smooth value noise in [-1, 1]; periodic with (periodX, periodY) lattice cells when those are &gt; 0.</summary>
        public static float ValueNoise(float x, float y, uint seed, int periodX = 0, int periodY = 0)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float fx = x - ix, fy = y - iy;
            float ux = fx * fx * (3f - 2f * fx), uy = fy * fy * (3f - 2f * fy);
            int x0 = Wrap(ix, periodX), x1 = Wrap(ix + 1, periodX), y0 = Wrap(iy, periodY), y1 = Wrap(iy + 1, periodY);
            float a = Lattice(x0, y0, seed), b = Lattice(x1, y0, seed), c = Lattice(x0, y1, seed), d = Lattice(x1, y1, seed);
            float ab = a + (b - a) * ux, cd = c + (d - c) * ux;
            return ab + (cd - ab) * uy;
        }

        /// <summary>Fractal sum of value noise, normalised to about [-1, 1].</summary>
        public static float Fbm(float x, float y, uint seed, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f, f = 1f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueNoise(x * f, y * f, seed + (uint)o * 101u);
                norm += amp;
                amp *= 0.5f;
                f *= 2.03f;
            }
            return sum / norm;
        }

        // ------------------------------------------------------------------ wear

        private static float Blob(float dx, float dz, float rx, float rz)
        {
            float q = (dx / rx) * (dx / rx) + (dz / rz) * (dz / rz);
            return q > 9f ? 0f : (float)Math.Exp(-q);
        }

        /// <summary>
        /// Wear in [0, 1]: goalmouths (keeper's line and the six-yard area), penalty marks and the centre spot,
        /// broken up by noise so it reads as thinned patches, not discs. Symmetric ends.
        /// </summary>
        public static float Wear(float x, float z, float fieldLength, TurfSettings s)
        {
            if (s.WearAmount <= 0f) return 0f;
            float hl = fieldLength * 0.5f;
            float ax = Math.Abs(x);
            float w = 0f;
            w = Math.Max(w, 0.95f * Blob(ax - (hl - 1.2f), z, 1.6f, 3.4f));   // keeper's line, between the posts
            w = Math.Max(w, 0.55f * Blob(ax - (hl - 4.0f), z, 3.6f, 5.5f));   // six-yard box traffic
            w = Math.Max(w, 0.60f * Blob(ax - (hl - s.PenaltySpotDistance), z, 1.1f, 1.1f));
            w = Math.Max(w, 0.65f * Blob(x, z, 1.5f, 1.5f));                  // centre spot (kick-offs)
            if (w < 0.004f) return 0f;
            float n = Fbm(x / 1.3f + 17.3f, z / 1.3f - 5.1f, s.Seed + 7u, 3) * 0.5f + 0.5f;
            float v = w * (0.35f + 1.1f * n);
            v = Math.Max(0f, Math.Min(1f, v)) * s.WearAmount;
            return Math.Min(1f, v);
        }

        // ------------------------------------------------------------------ surface colour

        /// <summary>Macro variation factor around 1 (8 m and 30 m scales).</summary>
        public static float MacroFactor(float x, float z, TurfSettings s)
        {
            float n = 0.65f * ValueNoise(x / 30f + 3.1f, z / 30f + 7.7f, s.Seed + 11u)
                    + 0.35f * ValueNoise(x / 8f - 1.9f, z / 8f + 2.3f, s.Seed + 23u);
            return 1f + s.MacroAmount * n;
        }

        /// <summary>
        /// Albedo (sRGB) and smoothness at a surface point. Field geometry is the painted pitch; anything outside it is
        /// the run-off (same turf, no stripes).
        /// </summary>
        public static void Surface(float x, float z, float fieldLength, float fieldWidth, TurfSettings s,
            out float r, out float g, out float b, out float smoothness)
        {
            float wear = Wear(x, z, fieldLength, s);
            float mask = StripeMask(x, z, fieldLength, fieldWidth, s) * (1f - 0.7f * wear);
            float k = StripeHalfAmplitude(s.StripeContrast);
            float f = (1f + k * mask) * MacroFactor(x, z, s);
            // slight hue drift: a touch yellower / bluer in patches
            float hue = 0.02f * ValueNoise(x / 14f + 9.2f, z / 14f - 4.4f, s.Seed + 31u);
            r = s.BaseR * f * (1f + hue);
            g = s.BaseG * f;
            b = s.BaseB * f * (1f - hue);
            float ww = 0.6f * wear;
            r += (s.WornR - r) * ww;
            g += (s.WornG - g) * ww;
            b += (s.WornB - b) * ww;
            float sn = ValueNoise(x / 4f, z / 4f, s.Seed + 43u);
            smoothness = s.Smoothness + s.StripeSmoothness * mask - s.WearSmoothnessLoss * wear + 0.02f * sn;
            r = Clamp01(r); g = Clamp01(g); b = Clamp01(b);
            smoothness = Clamp01(smoothness);
        }

        // ------------------------------------------------------------------ texel data

        private static byte ToByte(float v)
        {
            int i = (int)Math.Round(Clamp01(v) * 255f);
            return (byte)i;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        /// <summary>World position of texel centre (i, j) on a surface of surfaceLength x surfaceWidth centred at 0.</summary>
        public static void TexelToWorld(int i, int j, int w, int h, float surfaceLength, float surfaceWidth, out float x, out float z)
        {
            x = ((i + 0.5f) / w - 0.5f) * surfaceLength;
            z = ((j + 0.5f) / h - 0.5f) * surfaceWidth;
        }

        /// <summary>Base map: RGB = sRGB albedo, A = smoothness. Covers the whole surface (pitch + run-off) once.</summary>
        public static byte[] BaseMap(int w, int h, float surfaceLength, float surfaceWidth, float fieldLength, float fieldWidth, TurfSettings s)
        {
            var data = new byte[w * h * 4];
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    TexelToWorld(i, j, w, h, surfaceLength, surfaceWidth, out float x, out float z);
                    Surface(x, z, fieldLength, fieldWidth, s, out float r, out float g, out float b, out float sm);
                    int o = (j * w + i) * 4;
                    data[o] = ToByte(r); data[o + 1] = ToByte(g); data[o + 2] = ToByte(b); data[o + 3] = ToByte(sm);
                }
            return data;
        }

        /// <summary>
        /// Encodes a tangent-space normal. RGB = xyz * 0.5 + 0.5. A = 1 for desktop (URP decodes RG or AG*R there),
        /// or A = x when <paramref name="alphaHoldsX"/> (mobile: works for both the XYZ and the DXT5nm-style/ASTC decoders).
        /// </summary>
        public static void EncodeNormal(float nx, float ny, float nz, bool alphaHoldsX, byte[] data, int o)
        {
            float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            nx /= len; ny /= len; nz /= len;
            data[o] = ToByte(nx * 0.5f + 0.5f);
            data[o + 1] = ToByte(ny * 0.5f + 0.5f);
            data[o + 2] = ToByte(nz * 0.5f + 0.5f);
            data[o + 3] = alphaHoldsX ? data[o] : (byte)255;
        }

        /// <summary>Gentle ground undulation (m).</summary>
        public static float GroundHeight(float x, float z, TurfSettings s)
        {
            return 0.05f * Fbm(x / 7f + 1.7f, z / 7f - 3.3f, s.Seed + 53u, 3);
        }

        /// <summary>
        /// Base normal map (tangent x = +length, y = +width): ground undulation, per-stripe blade lean along the width
        /// (blades laid towards / away from a touchline camera: view-dependent sheen), rougher in worn areas.
        /// </summary>
        public static byte[] BaseNormalMap(int w, int h, float surfaceLength, float surfaceWidth, float fieldLength, float fieldWidth,
            TurfSettings s, bool alphaHoldsX)
        {
            var data = new byte[w * h * 4];
            // height once per texel, then central differences (texel spacing), clamped at the border
            var hgt = new float[w * h];
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    TexelToWorld(i, j, w, h, surfaceLength, surfaceWidth, out float hx, out float hz);
                    hgt[j * w + i] = GroundHeight(hx, hz, s);
                }
            float sx = surfaceLength / w, sz = surfaceWidth / h;
            var lean = new TurfSettings { StripeWidth = s.StripeWidth, StripeEdge = s.StripeEdge, CrossMow = 0f };
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    TexelToWorld(i, j, w, h, surfaceLength, surfaceWidth, out float x, out float z);
                    int il = Math.Max(0, i - 1), ir = Math.Min(w - 1, i + 1), jd = Math.Max(0, j - 1), ju = Math.Min(h - 1, j + 1);
                    float dhx = (hgt[j * w + ir] - hgt[j * w + il]) / ((ir - il) * sx);
                    float dhz = (hgt[ju * w + i] - hgt[jd * w + i]) / ((ju - jd) * sz);
                    float wear = Wear(x, z, fieldLength, s);
                    float m = StripeMask(x, z, fieldLength, fieldWidth, lean) * (1f - wear);
                    float rough = 0.12f * wear;
                    float nx = -dhx + (rough > 0f ? rough * ValueNoise(x * 3f, z * 3f, s.Seed + 61u) : 0f);
                    float ny = -dhz + s.SheenTilt * m + (rough > 0f ? rough * ValueNoise(x * 3f + 5f, z * 3f, s.Seed + 67u) : 0f);
                    EncodeNormal(nx, ny, 1f, alphaHoldsX, data, (j * w + i) * 4);
                }
            return data;
        }

        /// <summary>Tileable blade height field in about [-1, 1] at texel (i, j) of a size x size tile.</summary>
        public static float BladeHeight(int i, int j, int size, uint seed)
        {
            // blades lie along the mower's travel (+-width = v), so cells are stretched along v
            float u = (float)i / size, v = (float)j / size;
            float fine = ValueNoise(u * 128f, v * 48f, seed + 71u, 128, 48);
            float mid = ValueNoise(u * 32f, v * 16f, seed + 73u, 32, 16);
            float low = ValueNoise(u * 8f, v * 8f, seed + 79u, 8, 8);
            return 0.55f * fine + 0.3f * mid + 0.15f * low;
        }

        /// <summary>
        /// Detail albedo for URP's x2 detail mode (linear texture, 0.5 = no change): fine blade speckle with slight
        /// yellow / blue-green variation. Mean stays near 0.5, so it adds texture without shifting the base colour,
        /// and fades to neutral in the mips (distance blend).
        /// </summary>
        public static byte[] DetailAlbedo(int size, TurfSettings s)
        {
            var data = new byte[size * size * 4];
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                {
                    float hgt = BladeHeight(i, j, size, s.Seed);
                    float u = (float)i / size, v = (float)j / size;
                    float tint = ValueNoise(u * 24f, v * 24f, s.Seed + 83u, 24, 24);
                    float l = 0.5f + 0.13f * hgt;
                    int o = (j * size + i) * 4;
                    data[o] = ToByte(l + 0.025f * tint);
                    data[o + 1] = ToByte(l);
                    data[o + 2] = ToByte(l - 0.03f * tint);
                    data[o + 3] = 255;
                }
            return data;
        }

        /// <summary>Tileable detail normal map from the blade height field (wrap-around finite differences).</summary>
        public static byte[] DetailNormal(int size, TurfSettings s, bool alphaHoldsX, float strength = 2.2f)
        {
            var hgt = new float[size * size];
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                    hgt[j * size + i] = BladeHeight(i, j, size, s.Seed);
            var data = new byte[size * size * 4];
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                {
                    int il = (i - 1 + size) % size, ir = (i + 1) % size, jd = (j - 1 + size) % size, ju = (j + 1) % size;
                    float dx = (hgt[j * size + ir] - hgt[j * size + il]) * 0.5f;
                    float dy = (hgt[ju * size + i] - hgt[jd * size + i]) * 0.5f;
                    EncodeNormal(-dx * strength, -dy * strength, 1f, alphaHoldsX, data, (j * size + i) * 4);
                }
            return data;
        }

        /// <summary>Detail texture tiling (repeats across the surface) for a world tile size.</summary>
        public static void DetailTiling(float surfaceLength, float surfaceWidth, float tileMetres, out float tx, out float ty)
        {
            float t = tileMetres > 0.01f ? tileMetres : 3f;
            tx = surfaceLength / t;
            ty = surfaceWidth / t;
        }
    }
}
