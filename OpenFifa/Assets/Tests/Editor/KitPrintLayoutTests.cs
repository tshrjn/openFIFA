using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("Kits")]
    public class KitPrintLayoutTests
    {
        private const int W = 1024;
        private const int H = 512;

        private static KitPrintLayout Build(string name, int number, KitNameLayout layout = KitNameLayout.Straight,
            KitFontStyle style = KitFontStyle.Block, float outline = 0f, bool front = true)
        {
            return KitPrintLayoutLogic.Build(name, number, layout, style, outline, front, W, H);
        }

        [Test]
        public void Build_NameAndNumber_FormatsUppercaseAndKeepsNumber()
        {
            var layout = Build("  reyes ", 9);
            Assert.AreEqual("REYES", layout.PrintedName);
            Assert.AreEqual(9, layout.PrintedNumber);
            Assert.AreEqual(5, layout.GlyphsOf(KitPrintElement.BackName).Count);
            Assert.AreEqual(1, layout.GlyphsOf(KitPrintElement.BackNumber).Count);
            Assert.AreEqual(1, layout.GlyphsOf(KitPrintElement.FrontNumber).Count);
        }

        [Test]
        public void Build_TwoDigitNumber_IsCentredOnBackPanel()
        {
            var layout = Build("VARGA", 10);
            var digits = layout.GlyphsOf(KitPrintElement.BackNumber);
            Assert.AreEqual(2, digits.Count);
            Assert.Less(digits[0].CenterX, digits[1].CenterX, "Digits must read left to right.");

            float left = digits[0].CenterX - digits[0].InkWidth * 0.5f;
            float right = digits[1].CenterX + digits[1].InkWidth * 0.5f;
            Assert.AreEqual(layout.BackPanel.CenterX, (left + right) * 0.5f, 1f);
        }

        [Test]
        public void Build_BackNumber_IsLargerThanNameAndFrontNumber()
        {
            var layout = Build("LINDQVIST", 7);
            float numberH = layout.GlyphsOf(KitPrintElement.BackNumber)[0].InkHeight;
            float nameH = layout.GlyphsOf(KitPrintElement.BackName)[0].InkHeight;
            float frontH = layout.GlyphsOf(KitPrintElement.FrontNumber)[0].InkHeight;
            Assert.Greater(numberH, nameH * 3f);
            Assert.Greater(numberH, frontH * 2.5f);
        }

        [Test]
        public void Build_NameAboveNumber_OnBackPanel()
        {
            var layout = Build("HALE", 1);
            var name = layout.GlyphsOf(KitPrintElement.BackName)[0];
            var number = layout.GlyphsOf(KitPrintElement.BackNumber)[0];
            Assert.Greater(name.CenterY - name.InkHeight * 0.5f, number.CenterY + number.InkHeight * 0.5f,
                "Name must sit fully above the number.");
        }

        [TestCase(KitNameLayout.Straight)]
        [TestCase(KitNameLayout.Arched)]
        public void Build_AllGlyphs_StayInsideTheirPanels(KitNameLayout nameLayout)
        {
            var layout = Build("FERREIRA-LUND", 88, nameLayout, KitFontStyle.Block, 0.4f);
            foreach (var g in layout.Glyphs)
            {
                var panel = g.Element == KitPrintElement.FrontNumber ? layout.FrontPanel : layout.BackPanel;
                float halfDiag = 0.5f * (float)Math.Sqrt(g.InkWidth * g.InkWidth + g.InkHeight * g.InkHeight);
                Assert.That(panel.Contains(g.CenterX - halfDiag, g.CenterY - halfDiag)
                            && panel.Contains(g.CenterX + halfDiag, g.CenterY + halfDiag),
                    $"Glyph '{g.Character}' ({g.Element}) at ({g.CenterX:F0},{g.CenterY:F0}) leaves its panel.");
            }
        }

        [Test]
        public void Build_LongName_FitsMaxWidth()
        {
            var layout = Build("ABERNATHYWILLOWS", 5);
            var glyphs = layout.GlyphsOf(KitPrintElement.BackName);
            float left = glyphs[0].CenterX - glyphs[0].InkWidth * 0.5f;
            var last = glyphs[glyphs.Count - 1];
            float right = last.CenterX + last.InkWidth * 0.5f;
            Assert.LessOrEqual(right - left, layout.BackPanel.Width * KitPrintLayoutLogic.NameMaxWidth + 1f);
        }

        [Test]
        public void Build_LongName_IsSmallerThanShortName()
        {
            float shortH = Build("SATO", 5).GlyphsOf(KitPrintElement.BackName)[0].InkHeight;
            float longH = Build("ABERNATHYWILLOWS", 5).GlyphsOf(KitPrintElement.BackName)[0].InkHeight;
            Assert.Less(longH, shortH);
        }

        [Test]
        public void Build_ArchedName_IsSymmetricAndCurvesDown()
        {
            var glyphs = Build("OKONJO", 4, KitNameLayout.Arched).GlyphsOf(KitPrintElement.BackName);
            var first = glyphs[0];
            var last = glyphs[glyphs.Count - 1];
            var middle = glyphs[2];

            Assert.AreEqual(-first.Rotation, last.Rotation, 1e-4f, "Outer letters tilt by equal, opposite angles.");
            Assert.Greater(first.Rotation, 0f, "Left letters lean left (counter-clockwise).");
            Assert.AreEqual(first.CenterY, last.CenterY, 0.01f);
            Assert.Greater(middle.CenterY, first.CenterY, "Arch peaks in the middle.");
        }

        [Test]
        public void Build_StraightName_HasNoRotationAndSharedBaseline()
        {
            var glyphs = Build("OKONJO", 4).GlyphsOf(KitPrintElement.BackName);
            foreach (var g in glyphs)
            {
                Assert.AreEqual(0f, g.Rotation);
                Assert.AreEqual(glyphs[0].CenterY, g.CenterY, 1e-4f);
            }
        }

        [Test]
        public void Build_EmptyName_OnlyNumbers()
        {
            var layout = Build("", 23);
            Assert.AreEqual(0, layout.GlyphsOf(KitPrintElement.BackName).Count);
            Assert.AreEqual(2, layout.GlyphsOf(KitPrintElement.BackNumber).Count);
        }

        [Test]
        public void Build_NoFrontNumber_OmitsFrontGlyphs()
        {
            Assert.AreEqual(0, Build("SATO", 11, front: false).GlyphsOf(KitPrintElement.FrontNumber).Count);
        }

        [TestCase(0, 1)]
        [TestCase(150, 99)]
        public void Build_NumberOutOfRange_IsClamped(int input, int expected)
        {
            Assert.AreEqual(expected, Build("X", input).PrintedNumber);
        }

        [Test]
        public void Build_FrontNumber_OnFrontPanelUpperChest()
        {
            var layout = Build("SATO", 11);
            foreach (var g in layout.GlyphsOf(KitPrintElement.FrontNumber))
            {
                Assert.Greater(g.CenterX, layout.FrontPanel.X);
                Assert.Greater(g.CenterY, layout.FrontPanel.Height * 0.6f);
            }
        }

        [Test]
        public void Build_CondensedStyle_IsNarrowerThanBlock()
        {
            float block = Build("", 88, style: KitFontStyle.Block).GlyphsOf(KitPrintElement.BackNumber)[0].InkWidth;
            float condensed = Build("", 88, style: KitFontStyle.Condensed).GlyphsOf(KitPrintElement.BackNumber)[0].InkWidth;
            Assert.Less(condensed, block);
        }

        [Test]
        public void GetGlyph_AccentedAndLowercase_MapToBaseCapitals()
        {
            Assert.AreEqual('E', KitGlyphFont.GetGlyph('é').Character);
            Assert.AreEqual('A', KitGlyphFont.GetGlyph('a').Character);
            Assert.AreEqual(' ', KitGlyphFont.GetGlyph('#').Character);
            for (char c = 'A'; c <= 'Z'; c++) Assert.IsTrue(KitGlyphFont.HasGlyph(c), c.ToString());
            for (char c = '0'; c <= '9'; c++) Assert.IsTrue(KitGlyphFont.HasGlyph(c), c.ToString());
        }

        [Test]
        public void Rasterize_Number_InksCentreAndLeavesCornersClear()
        {
            var layout = Build("", 8, outline: 0.3f);
            var pixels = KitPrintRasterizer.Rasterize(layout);
            Assert.AreEqual(W * H * 4, pixels.Length);

            // '8' has a horizontal bar through its centre.
            var g = layout.GlyphsOf(KitPrintElement.BackNumber)[0];
            int idx = ((int)g.CenterY * W + (int)g.CenterX) * 4;
            Assert.Greater(pixels[idx], 200, "Fill (R) expected at the centre bar of '8'.");
            Assert.GreaterOrEqual(pixels[idx + 1], pixels[idx], "Outline coverage (G) includes the fill.");

            Assert.AreEqual(0, pixels[0], "Bottom-left corner must stay empty.");
            Assert.AreEqual(0, pixels[(W * H - 1) * 4 + 3], "Top-right corner must stay empty.");
        }

        [Test]
        public void Rasterize_Outline_ExtendsBeyondFill()
        {
            var layout = Build("", 1, outline: 0.4f);
            var pixels = KitPrintRasterizer.Rasterize(layout);
            int fillOnly = 0, outlineOnly = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i] > 128) fillOnly++;
                else if (pixels[i + 1] > 128) outlineOnly++;
            }
            Assert.Greater(fillOnly, 0);
            Assert.Greater(outlineOnly, 0, "Outline ring expected around the glyph.");
        }

        [Test]
        public void DistanceToSegment_SquareVsRoundCaps_DifferAtCorner()
        {
            var seg = new KitGlyphSegment(0f, 0f, 4f, 0f);
            float square = KitPrintRasterizer.DistanceToSegment(seg, 5f, 1f, true);
            float round = KitPrintRasterizer.DistanceToSegment(seg, 5f, 1f, false);
            Assert.AreEqual(1f, square, 1e-4f);
            Assert.AreEqual((float)Math.Sqrt(2f), round, 1e-4f);
        }

        [Test]
        public void CrestRasterize_Shield_HasOpaqueCentreAndClearCorners()
        {
            const int size = 64;
            var pixels = KitCrestRasterizer.Rasterize(size, new SimpleColor(0.1f, 0.3f, 0.8f),
                SimpleColor.White, new SimpleColor(0.9f, 0.7f, 0.2f), "HC", KitFontStyle.Block);
            Assert.AreEqual(255, pixels[((size / 2) * size + size / 2) * 4 + 3]);
            Assert.AreEqual(0, pixels[3], "Bottom-left corner outside the shield.");
            Assert.AreEqual(0, pixels[((size - 1) * size) * 4 + 3], "Top-left corner outside the shield.");
        }
    }
}
