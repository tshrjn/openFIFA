using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID_PITCH")]
    public class TurfMathTests
    {
        private const float L = 105f, W = 68f, RunOff = 5f;
        private static TurfSettings S => new TurfSettings();

        private static float Hex(int v) => v / 255f;

        [Test]
        public void BaseColour_Default_InsideJudgesOliveRange()
        {
            var s = S;
            // #3E7A2A .. #4C8A34
            Assert.That(s.BaseR, Is.InRange(Hex(0x3E), Hex(0x4C)));
            Assert.That(s.BaseG, Is.InRange(Hex(0x7A), Hex(0x8A)));
            Assert.That(s.BaseB, Is.InRange(Hex(0x2A), Hex(0x34)));
        }

        [Test]
        public void BaseColour_Default_LessSaturatedThanOldLime()
        {
            var s = S;
            float oldSat = TurfMath.Saturation(0.22f, 0.55f, 0.18f);
            Assert.Less(TurfMath.Saturation(s.BaseR, s.BaseG, s.BaseB), oldSat);
            Assert.Less(TurfMath.Luma(s.BaseR, s.BaseG, s.BaseB), TurfMath.Luma(0.22f, 0.55f, 0.18f));
        }

        [Test]
        public void StripeCount_StandardPitch_NineteenOddAndAbout5p5m()
        {
            int n = TurfMath.StripeCount(L, 5.5f);
            Assert.AreEqual(19, n);
            Assert.AreEqual(5.5f, L / n, 0.1f);
        }

        [Test]
        public void StripeMask_AlongLength_AlternatesEveryStripeWidth()
        {
            var s = S;
            float w = L / TurfMath.StripeCount(L, s.StripeWidth);
            float first = TurfMath.StripeMask(-L / 2 + w * 0.5f, 0f, L, W, s);
            float second = TurfMath.StripeMask(-L / 2 + w * 1.5f, 0f, L, W, s);
            Assert.AreEqual(1f, first, 1e-5f);
            Assert.AreEqual(-1f, second, 1e-5f);
        }

        [Test]
        public void StripeMask_AcrossWidth_ConstantWithoutCrossMow()
        {
            var s = S;
            float a = TurfMath.StripeMask(10f, -30f, L, W, s), b = TurfMath.StripeMask(10f, 30f, L, W, s);
            Assert.AreEqual(a, b, 1e-6f);
        }

        [Test]
        public void StripeMask_EndsAndHalves_AreSymmetric()
        {
            var s = S;
            for (float x = 0.3f; x < L / 2; x += 1.7f)
                Assert.AreEqual(TurfMath.StripeMask(x, 3f, L, W, s), TurfMath.StripeMask(-x, 3f, L, W, s), 1e-4f, "x=" + x);
        }

        [Test]
        public void StripeMask_RunOff_IsZero()
        {
            var s = S;
            Assert.AreEqual(0f, TurfMath.StripeMask(L / 2 + 2f, 0f, L, W, s));
            Assert.AreEqual(0f, TurfMath.StripeMask(0f, W / 2 + 2f, L, W, s));
        }

        [Test]
        public void StripeMask_Boundary_SoftEdgeIsContinuous()
        {
            var s = S;
            float w = L / TurfMath.StripeCount(L, s.StripeWidth);
            float b = -L / 2 + w;
            Assert.AreEqual(0f, TurfMath.StripeMask(b, 0f, L, W, s), 0.02f);
            float step = 0.01f, max = 0f, prev = TurfMath.StripeMask(b - 1f, 0f, L, W, s);
            for (float x = b - 1f + step; x < b + 1f; x += step)
            {
                float v = TurfMath.StripeMask(x, 0f, L, W, s);
                max = Math.Max(max, Math.Abs(v - prev));
                prev = v;
            }
            Assert.Less(max, 0.05f, "no hard step at the stripe boundary");
        }

        [Test]
        public void StripeMask_CrossMow_MakesMildChecker()
        {
            var s = S;
            s.CrossMow = 0.3f;
            float wl = L / TurfMath.StripeCount(L, s.StripeWidth), ww = W / TurfMath.StripeCount(W, s.StripeWidth);
            float x0 = -L / 2 + wl * 0.5f, z0 = -W / 2 + ww * 0.5f;
            float a = TurfMath.StripeMask(x0, z0, L, W, s), b = TurfMath.StripeMask(x0, z0 + ww, L, W, s);
            Assert.AreEqual(1f, a, 1e-4f);
            Assert.AreEqual(0.4f, b, 1e-4f);
        }

        [Test]
        public void StripeContrast_Default_LumaRatioEightToTwelvePercent()
        {
            var s = S;
            s.MacroAmount = 0f; s.WearAmount = 0f;
            float w = L / TurfMath.StripeCount(L, s.StripeWidth);
            TurfMath.Surface(-L / 2 + w * 2.5f, 20f, L, W, s, out float r1, out float g1, out float b1, out _);
            TurfMath.Surface(-L / 2 + w * 3.5f, 20f, L, W, s, out float r2, out float g2, out float b2, out _);
            float ratio = TurfMath.Luma(r1, g1, b1) / TurfMath.Luma(r2, g2, b2);
            Assert.That(ratio - 1f, Is.InRange(0.08f, 0.12f));
            float linear = TurfMath.LinearLuminance(r1, g1, b1) / TurfMath.LinearLuminance(r2, g2, b2);
            float halfSwing = (linear - 1f) / (linear + 1f);
            Assert.That(halfSwing, Is.InRange(0.06f, 0.15f), "+-6..15% linear luminance");
        }

        [Test]
        public void Surface_RunOff_SameTurfWithoutStripes()
        {
            var s = S;
            s.MacroAmount = 0f; s.WearAmount = 0f;
            TurfMath.Surface(L / 2 + 3f, 10f, L, W, s, out float r, out float g, out float b, out _);
            Assert.AreEqual(s.BaseR, r, 0.01f);
            Assert.AreEqual(s.BaseG, g, 0.01f);
            Assert.AreEqual(s.BaseB, b, 0.01f);
        }

        [Test]
        public void Wear_Goalmouth_HigherThanMidfieldAndSymmetric()
        {
            var s = S;
            float gm = 0f, gmOther = 0f;
            for (float z = -2f; z <= 2f; z += 0.5f)
            {
                gm += TurfMath.Wear(L / 2 - 1.2f, z, L, s);
                gmOther += TurfMath.Wear(-(L / 2 - 1.2f), z, L, s);
            }
            Assert.Greater(gm / 9f, 0.4f);
            Assert.Greater(gmOther / 9f, 0.4f);
            Assert.AreEqual(0f, TurfMath.Wear(25f, 20f, L, s));
            Assert.Greater(TurfMath.Wear(0f, 0f, L, s), 0.1f, "centre spot");
        }

        [Test]
        public void Surface_WornGoalmouth_IsBrownerAndMatte()
        {
            var s = S;
            TurfMath.Surface(L / 2 - 1.2f, 0f, L, W, s, out float r, out float g, out float b, out float sm);
            Assert.Greater(r / g, s.BaseR / s.BaseG);
            Assert.Less(sm, s.Smoothness);
        }

        [Test]
        public void Macro_Variation_SmallButPresent()
        {
            var s = S;
            float mn = 9f, mx = -9f;
            for (float x = -50f; x <= 50f; x += 2f)
                for (float z = -30f; z <= 30f; z += 2f)
                {
                    float f = TurfMath.MacroFactor(x, z, s);
                    mn = Math.Min(mn, f); mx = Math.Max(mx, f);
                }
            Assert.LessOrEqual(mx, 1f + s.MacroAmount + 1e-4f);
            Assert.GreaterOrEqual(mn, 1f - s.MacroAmount - 1e-4f);
            Assert.Greater(mx - mn, s.MacroAmount * 0.6f);
        }

        [Test]
        public void BaseMap_SameSeed_Deterministic()
        {
            var a = TurfMath.BaseMap(64, 32, L + 2 * RunOff, W + 2 * RunOff, L, W, S);
            var b = TurfMath.BaseMap(64, 32, L + 2 * RunOff, W + 2 * RunOff, L, W, S);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void DetailAlbedo_Mean_NearHalfSoX2ModeKeepsBaseColour()
        {
            var d = TurfMath.DetailAlbedo(128, S);
            double sum = 0;
            for (int i = 0; i < d.Length; i += 4) sum += d[i + 1];
            double mean = sum / (d.Length / 4) / 255.0;
            Assert.AreEqual(0.5, mean, 0.02);
        }

        [Test]
        public void ValueNoise_Periodic_TilesSeamlessly()
        {
            for (float y = 0.1f; y < 8f; y += 0.37f)
                Assert.AreEqual(TurfMath.ValueNoise(0f, y, 5u, 8, 8), TurfMath.ValueNoise(8f, y, 5u, 8, 8), 1e-5f);
        }

        [Test]
        public void EncodeNormal_Flat_IsHalfHalfOneAndAlphaRule()
        {
            var buf = new byte[8];
            TurfMath.EncodeNormal(0f, 0f, 1f, false, buf, 0);
            TurfMath.EncodeNormal(0.3f, 0f, 1f, true, buf, 4);
            Assert.AreEqual(128, buf[0]); Assert.AreEqual(128, buf[1]); Assert.AreEqual(255, buf[2]); Assert.AreEqual(255, buf[3]);
            Assert.AreEqual(buf[4], buf[7], "mobile: alpha carries x");
        }

        [Test]
        public void BaseNormal_Stripes_LeanOppositeWays()
        {
            var s = S;
            s.WearAmount = 0f;
            const int w = 230, h = 4;
            var n = TurfMath.BaseNormalMap(w, h, L, W, L, W, s, false);
            float sw = L / TurfMath.StripeCount(L, s.StripeWidth);
            int i1 = (int)((sw * 2.5f) / L * w), i2 = (int)((sw * 3.5f) / L * w);
            int g1 = n[(1 * w + i1) * 4 + 1], g2 = n[(1 * w + i2) * 4 + 1];
            Assert.Greater(g1, 128);
            Assert.Less(g2, 128);
        }
    }
}
