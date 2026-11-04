using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Stadium
{
    /// <summary>
    /// Crowd atlas bleed: the cards bob by shifting their V down, so a card's bottom edge samples the top rows of the
    /// band below. Those rows must be background for the whole bob plus the filter footprint.
    /// </summary>
    [TestFixture]
    [Category("FID_STADIUM")]
    public class CrowdCardPaddingTests
    {
        [Test]
        public void CardLayout_TallestFigure_LeavesTheTopPadClear()
        {
            int clear = CrowdVariation.CardBandPx - CrowdVariation.ContentTopPx;
            Assert.GreaterOrEqual(clear, CrowdVariation.CardTopPadPx, "the tallest head reaches into the top pad");
            Assert.GreaterOrEqual(CrowdVariation.CardTopPadPx, 6, "about 6 px of padding or more");
        }

        [Test]
        public void CardOffset_FullExcitementForAMinute_NeverLeavesThePad()
        {
            const int texH = 1024;
            float maxPx = 0f;
            for (float t = 0f; t < 60f; t += 0.004f)
                for (int g = 0; g < CrowdVariation.PhaseGroups; g++)
                    foreach (float e in new[] { 0f, 0.5f, 0.8f, 1f, 1.5f })
                    {
                        CrowdVariation.CardOffset(e, t, g, texH, out _, out float dv);
                        maxPx = Math.Max(maxPx, -dv * texH);
                    }
            Assert.LessOrEqual(maxPx, CrowdVariation.MaxBobPx + 1e-3f);
            // One pixel of bilinear footprint and one for the first mip must still land on background.
            Assert.LessOrEqual(maxPx + 2f, CrowdVariation.CardTopPadPx + 1e-3f);
            Assert.Greater(maxPx, CrowdVariation.MaxBobPx - 0.5f, "the excited bounce should still be visible");
        }
    }
}
