using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// How well two outfield kits tell apart from the broadcast camera, where a player is a few dozen pixels tall
    /// under stadium light. Plain RGB distance treats sky blue and white as different colours, but from that distance
    /// both read as "light", and two dark kits (navy and maroon) both read as "dark". This model:
    /// 1. Mixes each shirt's primary with its secondary by how much of the shirt the pattern covers (stripes, hoops
    ///    and halves read as a blend of both from afar). The blend is in sRGB, not linear light: on a player a few dozen
    ///    pixels tall the stripes are still resolved, so a light secondary does not wash the shirt out.
    /// 2. Converts to CIELAB (sRGB, D65) and applies a broadcast look: chroma is scaled down (haze, small figures,
    ///    bright sun) and more so for dark colours, whose hue is the first thing to vanish on screen.
    /// 3. Scores CIEDE2000 between the two looks, shirts weighted <see cref="ShirtWeight"/> and shorts the rest.
    /// A score under <see cref="MinContrast"/> is a clash. Pure C#.
    /// </summary>
    public static class KitBroadcastContrast
    {
        /// <summary>Minimum broadcast contrast for two outfield kits to read as different teams.</summary>
        public const float MinContrast = 22f;

        /// <summary>Share of the score from the shirts (the rest is shorts).</summary>
        public const float ShirtWeight = 0.75f;

        /// <summary>Chroma kept at broadcast distance for a mid-light colour (L* 60 and above).</summary>
        public const float ChromaScale = 0.6f;

        /// <summary>Broadcast contrast between two kits (CIEDE2000 units, about 0..100). Null kits score 100.</summary>
        public static float Contrast(KitDefinitionData a, KitDefinitionData b)
        {
            if (a == null || b == null) return 100f;
            float shirt = DeltaE2000(BroadcastLab(ShirtColour(a)), BroadcastLab(ShirtColour(b)));
            float shorts = DeltaE2000(BroadcastLab(Parse(a.ShortsColor, ShirtColour(a))), BroadcastLab(Parse(b.ShortsColor, ShirtColour(b))));
            return ShirtWeight * shirt + (1f - ShirtWeight) * shorts;
        }

        /// <summary>True when the two kits are too close to tell apart from the broadcast camera.</summary>
        public static bool Clashes(KitDefinitionData a, KitDefinitionData b) => Contrast(a, b) < MinContrast;

        /// <summary>The shirt's colour from afar: primary mixed with secondary by pattern coverage (sRGB blend).</summary>
        public static SimpleColor ShirtColour(KitDefinitionData kit)
        {
            var primary = Parse(kit.PrimaryColor, SimpleColor.White);
            float w = PatternCoverage(KitDefinitionConverter.GetPattern(kit));
            if (w <= 0f || string.IsNullOrEmpty(kit.SecondaryColor)) return primary;
            var secondary = Parse(kit.SecondaryColor, primary);
            return new SimpleColor(
                Mix(primary.R, secondary.R, w), Mix(primary.G, secondary.G, w), Mix(primary.B, secondary.B, w));
        }

        /// <summary>Share of the shirt in the secondary colour for each pattern.</summary>
        public static float PatternCoverage(JerseyPattern pattern)
        {
            switch (pattern)
            {
                case JerseyPattern.Stripes:
                case JerseyPattern.Hoops:
                case JerseyPattern.HalfAndHalf:
                    return 0.4f;
                case JerseyPattern.Sash:
                case JerseyPattern.Chevron:
                case JerseyPattern.Gradient:
                    return 0.2f;
                case JerseyPattern.Pinstripes:
                    return 0.1f;
                default:
                    return 0f;
            }
        }

        /// <summary>CIELAB of a colour as seen from the broadcast camera (chroma reduced, more for dark colours).</summary>
        public static Lab BroadcastLab(SimpleColor c)
        {
            var lab = ToLab(c);
            // Dark colours lose their hue first: below L* 60 chroma fades further, down to 40% of the scale at L* 15.
            float t = Clamp01((lab.L - 15f) / 45f);
            float scale = ChromaScale * (0.4f + 0.6f * t);
            return new Lab(lab.L, lab.A * scale, lab.B * scale);
        }

        /// <summary>sRGB (0..1, D65) to CIELAB.</summary>
        public static Lab ToLab(SimpleColor c)
        {
            float r = ToLinear(c.R), g = ToLinear(c.G), b = ToLinear(c.B);
            float x = (0.4124564f * r + 0.3575761f * g + 0.1804375f * b) / 0.95047f;
            float y = 0.2126729f * r + 0.7151522f * g + 0.0721750f * b;
            float z = (0.0193339f * r + 0.1191920f * g + 0.9503041f * b) / 1.08883f;
            float fx = F(x), fy = F(y), fz = F(z);
            return new Lab(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
        }

        /// <summary>CIEDE2000 colour difference (kL = kC = kH = 1).</summary>
        public static float DeltaE2000(Lab p, Lab q)
        {
            double l1 = p.L, a1 = p.A, b1 = p.B, l2 = q.L, a2 = q.A, b2 = q.B;
            double c1 = Math.Sqrt(a1 * a1 + b1 * b1), c2 = Math.Sqrt(a2 * a2 + b2 * b2);
            double cBar = (c1 + c2) / 2.0;
            double cBar7 = Math.Pow(cBar, 7);
            double g = 0.5 * (1 - Math.Sqrt(cBar7 / (cBar7 + Math.Pow(25, 7))));
            double a1p = (1 + g) * a1, a2p = (1 + g) * a2;
            double c1p = Math.Sqrt(a1p * a1p + b1 * b1), c2p = Math.Sqrt(a2p * a2p + b2 * b2);
            double h1p = Hue(b1, a1p), h2p = Hue(b2, a2p);
            double dLp = l2 - l1, dCp = c2p - c1p;
            double dhp = 0;
            if (c1p * c2p != 0)
            {
                dhp = h2p - h1p;
                if (dhp > 180) dhp -= 360;
                else if (dhp < -180) dhp += 360;
            }
            double dHp = 2 * Math.Sqrt(c1p * c2p) * Math.Sin(Rad(dhp / 2));
            double lBarP = (l1 + l2) / 2, cBarP = (c1p + c2p) / 2;
            double hBarP = h1p + h2p;
            if (c1p * c2p != 0)
            {
                if (Math.Abs(h1p - h2p) <= 180) hBarP = (h1p + h2p) / 2;
                else hBarP = h1p + h2p < 360 ? (h1p + h2p + 360) / 2 : (h1p + h2p - 360) / 2;
            }
            double t = 1 - 0.17 * Math.Cos(Rad(hBarP - 30)) + 0.24 * Math.Cos(Rad(2 * hBarP))
                       + 0.32 * Math.Cos(Rad(3 * hBarP + 6)) - 0.20 * Math.Cos(Rad(4 * hBarP - 63));
            double dTheta = 30 * Math.Exp(-((hBarP - 275) / 25) * ((hBarP - 275) / 25));
            double cBarP7 = Math.Pow(cBarP, 7);
            double rc = 2 * Math.Sqrt(cBarP7 / (cBarP7 + Math.Pow(25, 7)));
            double sl = 1 + 0.015 * (lBarP - 50) * (lBarP - 50) / Math.Sqrt(20 + (lBarP - 50) * (lBarP - 50));
            double sc = 1 + 0.045 * cBarP;
            double sh = 1 + 0.015 * cBarP * t;
            double rt = -Math.Sin(Rad(2 * dTheta)) * rc;
            double dl = dLp / sl, dc = dCp / sc, dh = dHp / sh;
            return (float)Math.Sqrt(dl * dl + dc * dc + dh * dh + rt * dc * dh);
        }

        /// <summary>A CIELAB colour.</summary>
        public readonly struct Lab
        {
            public readonly float L, A, B;
            public Lab(float l, float a, float b) { L = l; A = a; B = b; }
            public override string ToString() => $"L{L:F0} a{A:F0} b{B:F0}";
        }

        private static SimpleColor Parse(string hex, SimpleColor fallback) => KitColorUtility.ParseOrDefault(hex, fallback);

        private static float Mix(float a, float b, float w) => a * (1f - w) + b * w;

        private static float ToLinear(float c) =>
            c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4f);

        private static float F(float t) => t > 0.008856f ? (float)Math.Pow(t, 1.0 / 3.0) : 7.787f * t + 16f / 116f;

        private static double Hue(double b, double a)
        {
            if (a == 0 && b == 0) return 0;
            double h = Math.Atan2(b, a) * 180 / Math.PI;
            return h < 0 ? h + 360 : h;
        }

        private static double Rad(double deg) => deg * Math.PI / 180;

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
