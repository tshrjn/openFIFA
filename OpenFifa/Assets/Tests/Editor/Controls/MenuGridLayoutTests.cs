using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    /// <summary>The seven-level difficulty picker must fit every iPad width, including 4:3 (961 to 1031 GUI units).</summary>
    [TestFixture]
    [Category("FID_CONTROLS")]
    public class MenuGridLayoutTests
    {
        private const int Levels = 7;

        private static void AssertInside(MenuRect[] r, int count, float w)
        {
            for (int i = 0; i < count; i++)
            {
                Assert.GreaterOrEqual(r[i].X, MenuGridLayout.DefaultMargin - 0.01f, $"button {i} left edge at width {w}");
                Assert.LessOrEqual(r[i].XMax, w - MenuGridLayout.DefaultMargin + 0.01f, $"button {i} right edge at width {w}");
                for (int k = i + 1; k < count; k++)
                {
                    bool overlap = r[i].X < r[k].XMax - 0.01f && r[k].X < r[i].XMax - 0.01f
                        && r[i].Y < r[k].YMax - 0.01f && r[k].Y < r[i].YMax - 0.01f;
                    Assert.IsFalse(overlap, $"buttons {i} and {k} overlap at width {w}");
                }
            }
        }

        [TestCase(961f)]   // iPad Pro 12.9" (2732 x 2048) at 720-unit GUI height
        [TestCase(1031f)]  // iPad Pro 11" (2388 x 1668)
        public void Layout_FourByThreeIPad_OneRowInsideScreen(float w)
        {
            var r = new MenuRect[Levels];
            int rows = MenuGridLayout.Layout(Levels, w, 100f, r);
            Assert.AreEqual(1, rows);
            AssertInside(r, Levels, w);
            Assert.GreaterOrEqual(r[0].Width, MenuGridLayout.DefaultMinWidth);
        }

        [Test]
        public void Layout_WideScreen_UsesFullButtonWidth()
        {
            var r = new MenuRect[Levels];
            Assert.AreEqual(1, MenuGridLayout.Layout(Levels, 1280f, 0f, r));
            Assert.AreEqual(MenuGridLayout.DefaultMaxWidth, r[0].Width, 1e-4f);
            AssertInside(r, Levels, 1280f);
            // Centred.
            Assert.AreEqual(1280f - r[Levels - 1].XMax, r[0].X, 0.01f);
        }

        [Test]
        public void Layout_Narrow_WrapsToTwoRowsOfFourAndThree()
        {
            var r = new MenuRect[Levels];
            int rows = MenuGridLayout.Layout(Levels, 700f, 50f, r);
            Assert.AreEqual(2, rows);
            for (int i = 0; i < 4; i++) Assert.AreEqual(50f, r[i].Y);
            for (int i = 4; i < Levels; i++) Assert.Greater(r[i].Y, 50f);
            AssertInside(r, Levels, 700f);
            Assert.AreEqual(2 * 64f + MenuGridLayout.DefaultGap, MenuGridLayout.BlockHeight(rows), 1e-4f);
        }

        [Test]
        public void Layout_EveryWidthFrom400To2600_StaysOnScreen()
        {
            var r = new MenuRect[Levels];
            for (float w = 400f; w <= 2600f; w += 7f)
            {
                MenuGridLayout.Layout(Levels, w, 0f, r);
                AssertInside(r, Levels, w);
            }
        }
    }
}
