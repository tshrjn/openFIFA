using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US049")]
    [Category("Visual")]
    public class US049_VisualRegressionTests
    {
        // ===== Existing tests (unchanged) =====

        [Test]
        public void VisualRegressionConfig_FixedResolution_1920x1080()
        {
            var config = new VisualRegressionConfig();
            Assert.AreEqual(1920, config.CaptureWidth);
            Assert.AreEqual(1080, config.CaptureHeight);
        }

        [Test]
        public void VisualRegressionConfig_DiffThreshold_Under2Percent()
        {
            var config = new VisualRegressionConfig();
            Assert.LessOrEqual(config.MaxDiffPercentage, 2f);
        }

        [Test]
        public void VisualRegressionConfig_PerChannelThreshold_10()
        {
            var config = new VisualRegressionConfig();
            Assert.AreEqual(10, config.PerChannelDiffThreshold);
        }

        [Test]
        public void VisualRegressionConfig_CameraCheckpoints_3()
        {
            var config = new VisualRegressionConfig();
            Assert.AreEqual(3, config.CameraCheckpoints.Count);
        }

        [Test]
        public void VisualRegressionConfig_Checkpoints_ContainKickoff()
        {
            var config = new VisualRegressionConfig();
            Assert.IsTrue(config.CameraCheckpoints.Exists(c => c.Name == "Kickoff"));
        }

        [Test]
        public void VisualRegressionConfig_Checkpoints_ContainGoalCloseup()
        {
            var config = new VisualRegressionConfig();
            Assert.IsTrue(config.CameraCheckpoints.Exists(c => c.Name == "GoalCloseup"));
        }

        [Test]
        public void VisualRegressionConfig_Checkpoints_ContainCornerFlag()
        {
            var config = new VisualRegressionConfig();
            Assert.IsTrue(config.CameraCheckpoints.Exists(c => c.Name == "CornerFlag"));
        }

        [Test]
        public void VisualRegressionConfig_BaselinePath_Valid()
        {
            var config = new VisualRegressionConfig();
            Assert.IsNotNull(config.BaselinePath);
            Assert.IsTrue(config.BaselinePath.Contains("Baselines"));
        }

        [Test]
        public void PixelComparer_IdenticalImages_ZeroDiff()
        {
            var comparer = new PixelComparer(10);
            byte[] imageA = { 255, 128, 64, 255, 100, 200, 50, 255 };
            byte[] imageB = { 255, 128, 64, 255, 100, 200, 50, 255 };
            float diff = comparer.ComputeDiffPercentage(imageA, imageB);
            Assert.AreEqual(0f, diff);
        }

        [Test]
        public void PixelComparer_CompletelyDifferent_HighDiff()
        {
            var comparer = new PixelComparer(10);
            byte[] imageA = { 0, 0, 0, 255, 0, 0, 0, 255 };
            byte[] imageB = { 255, 255, 255, 255, 255, 255, 255, 255 };
            float diff = comparer.ComputeDiffPercentage(imageA, imageB);
            Assert.AreEqual(100f, diff);
        }

        [Test]
        public void PixelComparer_MinorAntiAliasing_UnderThreshold()
        {
            var comparer = new PixelComparer(10);
            // Slightly different - within per-channel threshold of 10
            byte[] imageA = { 100, 100, 100, 255, 200, 200, 200, 255 };
            byte[] imageB = { 105, 98, 103, 255, 195, 205, 198, 255 };
            float diff = comparer.ComputeDiffPercentage(imageA, imageB);
            Assert.AreEqual(0f, diff);
        }

        [Test]
        public void PixelComparer_DifferentLengths_ReturnsMax()
        {
            var comparer = new PixelComparer(10);
            byte[] imageA = { 0, 0, 0, 255 };
            byte[] imageB = { 0, 0, 0, 255, 255, 255, 255, 255 };
            float diff = comparer.ComputeDiffPercentage(imageA, imageB);
            Assert.AreEqual(100f, diff);
        }

        // ===== New tests for US-049 =====

        // --- ScreenshotSpec validation ---

        [Test]
        public void IsSpecValid_ValidSpec_ReturnsTrue()
        {
            var checkpoint = new CameraCheckpoint("Kickoff", 0f, 20f, 0f, 60f, 0f, 0f);
            var spec = new ScreenshotSpec("KickoffView", checkpoint, 1920, 1080, "Match");
            Assert.IsTrue(VisualRegressionValidator.IsSpecValid(spec),
                "A fully populated ScreenshotSpec should be valid.");
        }

        [Test]
        public void IsSpecValid_NullSpec_ReturnsFalse()
        {
            Assert.IsFalse(VisualRegressionValidator.IsSpecValid(null),
                "Null spec should be invalid.");
        }

        [Test]
        public void IsSpecValid_EmptyName_ReturnsFalse()
        {
            var checkpoint = new CameraCheckpoint("Cam", 0f, 0f, 0f, 0f, 0f, 0f);
            var spec = new ScreenshotSpec("", checkpoint, 1920, 1080, "Match");
            Assert.IsFalse(VisualRegressionValidator.IsSpecValid(spec),
                "Spec with empty name should be invalid.");
        }

        [Test]
        public void IsSpecValid_NullCamera_ReturnsFalse()
        {
            var spec = new ScreenshotSpec("Test", null, 1920, 1080, "Match");
            Assert.IsFalse(VisualRegressionValidator.IsSpecValid(spec),
                "Spec with null camera angle should be invalid.");
        }

        [Test]
        public void IsSpecValid_ZeroResolution_ReturnsFalse()
        {
            var checkpoint = new CameraCheckpoint("Cam", 0f, 0f, 0f, 0f, 0f, 0f);
            var spec = new ScreenshotSpec("Test", checkpoint, 0, 1080, "Match");
            Assert.IsFalse(VisualRegressionValidator.IsSpecValid(spec),
                "Spec with zero width should be invalid.");
        }

        [Test]
        public void IsSpecValid_EmptyScene_ReturnsFalse()
        {
            var checkpoint = new CameraCheckpoint("Cam", 0f, 0f, 0f, 0f, 0f, 0f);
            var spec = new ScreenshotSpec("Test", checkpoint, 1920, 1080, "");
            Assert.IsFalse(VisualRegressionValidator.IsSpecValid(spec),
                "Spec with empty scene name should be invalid.");
        }

        // --- BaselineSet completeness ---

        [Test]
        public void IsBaselineSetComplete_ValidSet_ReturnsTrue()
        {
            var checkpoint = new CameraCheckpoint("Kickoff", 0f, 20f, 0f, 60f, 0f, 0f);
            var spec = new ScreenshotSpec("KickoffView", checkpoint, 1920, 1080, "Match");
            var set = new BaselineSet("v1", 1000L, new List<ScreenshotSpec> { spec }, "macOS", "URP");
            Assert.IsTrue(VisualRegressionValidator.IsBaselineSetComplete(set),
                "A fully populated BaselineSet should be complete.");
        }

        [Test]
        public void IsBaselineSetComplete_NullSet_ReturnsFalse()
        {
            Assert.IsFalse(VisualRegressionValidator.IsBaselineSetComplete(null),
                "Null set should not be complete.");
        }

        [Test]
        public void IsBaselineSetComplete_EmptyVersion_ReturnsFalse()
        {
            var checkpoint = new CameraCheckpoint("Cam", 0f, 0f, 0f, 0f, 0f, 0f);
            var spec = new ScreenshotSpec("Test", checkpoint, 1920, 1080, "Match");
            var set = new BaselineSet("", 1000L, new List<ScreenshotSpec> { spec }, "macOS", "URP");
            Assert.IsFalse(VisualRegressionValidator.IsBaselineSetComplete(set),
                "Set with empty version should not be complete.");
        }

        [Test]
        public void IsBaselineSetComplete_ZeroTimestamp_ReturnsFalse()
        {
            var checkpoint = new CameraCheckpoint("Cam", 0f, 0f, 0f, 0f, 0f, 0f);
            var spec = new ScreenshotSpec("Test", checkpoint, 1920, 1080, "Match");
            var set = new BaselineSet("v1", 0L, new List<ScreenshotSpec> { spec }, "macOS", "URP");
            Assert.IsFalse(VisualRegressionValidator.IsBaselineSetComplete(set),
                "Set with zero timestamp should not be complete.");
        }

        [Test]
        public void IsBaselineSetComplete_EmptySpecs_ReturnsFalse()
        {
            var set = new BaselineSet("v1", 1000L, new List<ScreenshotSpec>(), "macOS", "URP");
            Assert.IsFalse(VisualRegressionValidator.IsBaselineSetComplete(set),
                "Set with no specs should not be complete.");
        }

        [Test]
        public void IsBaselineSetComplete_InvalidSpecInSet_ReturnsFalse()
        {
            // Spec with null camera makes it invalid
            var badSpec = new ScreenshotSpec("Test", null, 1920, 1080, "Match");
            var set = new BaselineSet("v1", 1000L, new List<ScreenshotSpec> { badSpec }, "macOS", "URP");
            Assert.IsFalse(VisualRegressionValidator.IsBaselineSetComplete(set),
                "Set containing an invalid spec should not be complete.");
        }

        // --- PixelComparisonLogic ---

        [Test]
        public void PixelComparisonLogic_IdenticalImages_ZeroDiffCount()
        {
            var logic = new PixelComparisonLogic(10);
            byte[] img = { 128, 64, 32, 255, 200, 100, 50, 255 };
            int count = logic.ComputeDiffCount(img, img);
            Assert.AreEqual(0, count,
                "Identical images should have zero differing pixels.");
        }

        [Test]
        public void PixelComparisonLogic_IdenticalImages_SimilarityOne()
        {
            var logic = new PixelComparisonLogic(10);
            byte[] img = { 128, 64, 32, 255, 200, 100, 50, 255 };
            float sim = logic.ComputeSimilarity(img, img);
            Assert.AreEqual(1f, sim, 0.001f,
                "Identical images should have similarity of 1.0.");
        }

        [Test]
        public void PixelComparisonLogic_TotallyDifferent_LowSimilarity()
        {
            var logic = new PixelComparisonLogic(10);
            byte[] imgA = { 0, 0, 0, 255, 0, 0, 0, 255 };
            byte[] imgB = { 255, 255, 255, 255, 255, 255, 255, 255 };
            float sim = logic.ComputeSimilarity(imgA, imgB);
            Assert.AreEqual(0f, sim, 0.001f,
                "Completely different images should have similarity of 0.0.");
        }

        [Test]
        public void PixelComparisonLogic_DiffMask_IdenticalImages_AllZeros()
        {
            var logic = new PixelComparisonLogic(10);
            byte[] img = { 128, 64, 32, 255, 200, 100, 50, 255 };
            byte[] mask = logic.GenerateDiffMask(img, img);
            Assert.IsNotNull(mask);
            Assert.AreEqual(2, mask.Length, "Mask length should equal pixel count.");
            foreach (byte b in mask)
            {
                Assert.AreEqual(0, b, "All mask bytes should be 0 for identical images.");
            }
        }

        [Test]
        public void PixelComparisonLogic_DiffMask_DifferentImages_NonZero()
        {
            var logic = new PixelComparisonLogic(10);
            byte[] imgA = { 0, 0, 0, 255 };
            byte[] imgB = { 255, 255, 255, 255 };
            byte[] mask = logic.GenerateDiffMask(imgA, imgB);
            Assert.IsNotNull(mask);
            Assert.AreEqual(1, mask.Length);
            Assert.AreEqual(255, mask[0], "Differing pixel should be marked 255.");
        }

        [Test]
        public void PixelComparisonLogic_NullInput_ReturnsNegativeDiffCount()
        {
            var logic = new PixelComparisonLogic(10);
            int count = logic.ComputeDiffCount(null, new byte[] { 0, 0, 0, 255 });
            Assert.AreEqual(-1, count,
                "Null input should return -1 diff count.");
        }

        [Test]
        public void PixelComparisonLogic_SizeMismatch_Returns100Percent()
        {
            var logic = new PixelComparisonLogic(10);
            byte[] imgA = { 0, 0, 0, 255 };
            byte[] imgB = { 0, 0, 0, 255, 128, 128, 128, 255 };
            float pct = logic.ComputeDiffPercentage(imgA, imgB);
            Assert.AreEqual(100f, pct,
                "Size mismatch should return 100% diff.");
        }

        // --- PerceptualHashLogic ---

        [Test]
        public void PerceptualHashLogic_IdenticalImages_ZeroHammingDistance()
        {
            var hasher = new PerceptualHashLogic();
            // 4x4 red image (RGBA)
            var img = CreateSolidImage(4, 4, 255, 0, 0, 255);
            long hashA = hasher.ComputeHash(img, 4, 4);
            long hashB = hasher.ComputeHash(img, 4, 4);
            Assert.AreEqual(0, hasher.HammingDistance(hashA, hashB),
                "Identical images should have zero Hamming distance.");
        }

        [Test]
        public void PerceptualHashLogic_IdenticalImages_SimilarityOne()
        {
            var hasher = new PerceptualHashLogic();
            var img = CreateSolidImage(8, 8, 128, 128, 128, 255);
            long hash = hasher.ComputeHash(img, 8, 8);
            float sim = hasher.ComputeSimilarity(hash, hash);
            Assert.AreEqual(1f, sim, 0.001f,
                "Same hash should yield similarity of 1.0.");
        }

        [Test]
        public void PerceptualHashLogic_SimilarImages_HighSimilarity()
        {
            var hasher = new PerceptualHashLogic();
            // Two images that are very similar (slight shade difference)
            var imgA = CreateSolidImage(8, 8, 128, 128, 128, 255);
            var imgB = CreateSolidImage(8, 8, 130, 126, 129, 255);
            long hashA = hasher.ComputeHash(imgA, 8, 8);
            long hashB = hasher.ComputeHash(imgB, 8, 8);
            float sim = hasher.ComputeSimilarity(hashA, hashB);
            Assert.GreaterOrEqual(sim, 0.9f,
                $"Similar images should have high perceptual similarity, got {sim}.");
        }

        [Test]
        public void PerceptualHashLogic_EmptyImage_ReturnsZeroHash()
        {
            var hasher = new PerceptualHashLogic();
            long hash = hasher.ComputeHash(new byte[0], 0, 0);
            Assert.AreEqual(0L, hash,
                "Empty image should return zero hash.");
        }

        [Test]
        public void PerceptualHashLogic_HammingDistance_MaxIs64()
        {
            var hasher = new PerceptualHashLogic();
            // All bits set vs no bits set
            long hashA = 0L;
            long hashB = -1L; // All 64 bits set
            int distance = hasher.HammingDistance(hashA, hashB);
            Assert.AreEqual(64, distance,
                "Maximum Hamming distance between two 64-bit hashes should be 64.");
        }

        // --- HistogramComparer ---

        [Test]
        public void HistogramComparer_IdenticalImages_SimilarityOne()
        {
            var comparer = new HistogramComparer();
            var img = CreateSolidImage(4, 4, 100, 150, 200, 255);
            float sim = comparer.ComputeSimilarity(img, img);
            Assert.AreEqual(1f, sim, 0.001f,
                "Identical images should have histogram similarity of 1.0.");
        }

        [Test]
        public void HistogramComparer_SameHistogram_ZeroChiSquared()
        {
            var comparer = new HistogramComparer();
            var img = CreateSolidImage(4, 4, 100, 150, 200, 255);
            var histR = comparer.BuildChannelHistogram(img, 0);
            float chiSq = comparer.ChiSquaredDistance(histR, histR);
            Assert.AreEqual(0f, chiSq, 0.001f,
                "Same histogram should have zero chi-squared distance.");
        }

        [Test]
        public void HistogramComparer_DifferentImages_LowerSimilarity()
        {
            var comparer = new HistogramComparer();
            var imgA = CreateSolidImage(4, 4, 0, 0, 0, 255);
            var imgB = CreateSolidImage(4, 4, 255, 255, 255, 255);
            float sim = comparer.ComputeSimilarity(imgA, imgB);
            Assert.Less(sim, 1f,
                $"Different images should have similarity less than 1.0, got {sim}.");
        }

        [Test]
        public void HistogramComparer_EmptyAndNonEmpty_ZeroSimilarity()
        {
            var comparer = new HistogramComparer();
            var imgA = new byte[0];
            var imgB = CreateSolidImage(4, 4, 128, 128, 128, 255);
            float sim = comparer.ComputeSimilarity(imgA, imgB);
            Assert.AreEqual(0f, sim,
                "Empty vs non-empty images should have zero similarity.");
        }

        [Test]
        public void HistogramComparer_BuildChannelHistogram_NormalizedSum()
        {
            var comparer = new HistogramComparer();
            var img = CreateSolidImage(4, 4, 100, 150, 200, 255);
            var hist = comparer.BuildChannelHistogram(img, 0); // R channel
            float sum = 0f;
            for (int i = 0; i < hist.Length; i++) sum += hist[i];
            Assert.AreEqual(1f, sum, 0.001f,
                "Normalized histogram bins should sum to 1.0.");
        }

        // --- SSIMCalculator ---

        [Test]
        public void SSIMCalculator_IdenticalImages_ReturnsOne()
        {
            var ssim = new SSIMCalculator();
            var img = CreateSolidImage(4, 4, 128, 128, 128, 255);
            float result = ssim.ComputeFromRGBA(img, img);
            Assert.AreEqual(1f, result, 0.001f,
                "Identical images should have SSIM of 1.0.");
        }

        [Test]
        public void SSIMCalculator_DifferentImages_LessThanOne()
        {
            var ssim = new SSIMCalculator();
            var imgA = CreateSolidImage(4, 4, 0, 0, 0, 255);
            var imgB = CreateSolidImage(4, 4, 255, 255, 255, 255);
            float result = ssim.ComputeFromRGBA(imgA, imgB);
            Assert.Less(result, 1f,
                $"Different images should have SSIM less than 1.0, got {result}.");
        }

        [Test]
        public void SSIMCalculator_ExtractLuminance_CorrectLength()
        {
            var ssim = new SSIMCalculator();
            // 2 pixels = 8 bytes RGBA
            byte[] img = { 100, 150, 200, 255, 50, 100, 150, 255 };
            float[] lum = ssim.ExtractLuminance(img);
            Assert.AreEqual(2, lum.Length,
                "Luminance array should have one entry per pixel.");
        }

        [Test]
        public void SSIMCalculator_ExtractLuminance_UsesStandardFormula()
        {
            var ssim = new SSIMCalculator();
            // Single pixel: R=100, G=150, B=200
            byte[] img = { 100, 150, 200, 255 };
            float[] lum = ssim.ExtractLuminance(img);
            float expected = 0.299f * 100 + 0.587f * 150 + 0.114f * 200;
            Assert.AreEqual(expected, lum[0], 0.01f,
                $"Luminance should be computed using standard formula. Expected {expected}, got {lum[0]}.");
        }

        [Test]
        public void SSIMCalculator_EmptyImages_ReturnsOne()
        {
            var ssim = new SSIMCalculator();
            float result = ssim.ComputeFromRGBA(new byte[0], new byte[0]);
            Assert.AreEqual(1f, result, 0.001f,
                "Two empty images should yield SSIM of 1.0.");
        }

        [Test]
        public void SSIMCalculator_SizeMismatch_ReturnsZero()
        {
            var ssim = new SSIMCalculator();
            byte[] imgA = { 0, 0, 0, 255 };
            byte[] imgB = { 0, 0, 0, 255, 128, 128, 128, 255 };
            float result = ssim.ComputeFromRGBA(imgA, imgB);
            Assert.AreEqual(0f, result,
                "Size mismatch should return SSIM of 0.");
        }

        // --- ComparisonThresholds ---

        [Test]
        public void IsThresholdReasonable_ValidThresholds_ReturnsTrue()
        {
            var thresholds = new ComparisonThresholds(0.95f, 2f);
            Assert.IsTrue(VisualRegressionValidator.IsThresholdReasonable(thresholds),
                "Threshold with pass=0.95, maxDiff=2% should be reasonable.");
        }

        [Test]
        public void IsThresholdReasonable_NullThresholds_ReturnsFalse()
        {
            Assert.IsFalse(VisualRegressionValidator.IsThresholdReasonable(null),
                "Null thresholds should not be reasonable.");
        }

        [Test]
        public void IsThresholdReasonable_NegativePassThreshold_ReturnsFalse()
        {
            var thresholds = new ComparisonThresholds(-0.1f, 2f);
            Assert.IsFalse(VisualRegressionValidator.IsThresholdReasonable(thresholds),
                "Negative pass threshold should not be reasonable.");
        }

        [Test]
        public void IsThresholdReasonable_PassThresholdAboveOne_ReturnsFalse()
        {
            var thresholds = new ComparisonThresholds(1.5f, 2f);
            Assert.IsFalse(VisualRegressionValidator.IsThresholdReasonable(thresholds),
                "Pass threshold > 1.0 should not be reasonable.");
        }

        [Test]
        public void IsThresholdReasonable_NegativeMaxDiff_ReturnsFalse()
        {
            var thresholds = new ComparisonThresholds(0.95f, -5f);
            Assert.IsFalse(VisualRegressionValidator.IsThresholdReasonable(thresholds),
                "Negative max diff percentage should not be reasonable.");
        }

        [Test]
        public void IsThresholdReasonable_MaxDiffOver100_ReturnsFalse()
        {
            var thresholds = new ComparisonThresholds(0.95f, 150f);
            Assert.IsFalse(VisualRegressionValidator.IsThresholdReasonable(thresholds),
                "Max diff percentage > 100 should not be reasonable.");
        }

        // --- IgnoreRegion overlap ---

        [Test]
        public void IgnoreRegion_OverlappingRegions_ReturnsTrue()
        {
            var regionA = new IgnoreRegion(10, 10, 50, 50);
            var regionB = new IgnoreRegion(30, 30, 50, 50);
            Assert.IsTrue(regionA.Overlaps(regionB),
                "Overlapping regions should return true.");
        }

        [Test]
        public void IgnoreRegion_NonOverlapping_ReturnsFalse()
        {
            var regionA = new IgnoreRegion(0, 0, 10, 10);
            var regionB = new IgnoreRegion(100, 100, 10, 10);
            Assert.IsFalse(regionA.Overlaps(regionB),
                "Non-overlapping regions should return false.");
        }

        [Test]
        public void IgnoreRegion_AdjacentRegions_ReturnsFalse()
        {
            var regionA = new IgnoreRegion(0, 0, 10, 10);
            var regionB = new IgnoreRegion(10, 0, 10, 10);
            Assert.IsFalse(regionA.Overlaps(regionB),
                "Adjacent regions (touching edges) should not overlap.");
        }

        [Test]
        public void IgnoreRegion_NullOther_ReturnsFalse()
        {
            var region = new IgnoreRegion(0, 0, 10, 10);
            Assert.IsFalse(region.Overlaps(null),
                "Overlap check with null should return false.");
        }

        [Test]
        public void IgnoreRegion_ContainedRegion_ReturnsTrue()
        {
            var outer = new IgnoreRegion(0, 0, 100, 100);
            var inner = new IgnoreRegion(20, 20, 10, 10);
            Assert.IsTrue(outer.Overlaps(inner),
                "A region fully containing another should overlap.");
            Assert.IsTrue(inner.Overlaps(outer),
                "A region fully contained by another should also overlap.");
        }

        // --- RegressionReport aggregation ---

        [Test]
        public void RegressionReport_AllPassed_PassRateOne()
        {
            var results = new List<PerScreenshotResult>
            {
                new PerScreenshotResult("A", new ComparisonResult(true, 0.99f, 10, 0.5f)),
                new PerScreenshotResult("B", new ComparisonResult(true, 0.98f, 20, 1.0f)),
                new PerScreenshotResult("C", new ComparisonResult(true, 0.97f, 30, 1.5f))
            };
            var report = new RegressionReport(1000L, results);
            Assert.AreEqual(1f, report.GetPassRate(), 0.001f,
                "All-pass report should have pass rate of 1.0.");
            Assert.AreEqual(3, report.TotalComparisons);
            Assert.AreEqual(3, report.PassedCount);
            Assert.AreEqual(0, report.FailedCount);
        }

        [Test]
        public void RegressionReport_MixedResults_CorrectPassRate()
        {
            var results = new List<PerScreenshotResult>
            {
                new PerScreenshotResult("A", new ComparisonResult(true, 0.99f, 10, 0.5f)),
                new PerScreenshotResult("B", new ComparisonResult(false, 0.50f, 500, 25f)),
                new PerScreenshotResult("C", new ComparisonResult(true, 0.98f, 20, 1.0f)),
                new PerScreenshotResult("D", new ComparisonResult(false, 0.30f, 1000, 50f))
            };
            var report = new RegressionReport(1000L, results);
            Assert.AreEqual(0.5f, report.GetPassRate(), 0.001f,
                "2 out of 4 passed should yield pass rate 0.5.");
            Assert.AreEqual(4, report.TotalComparisons);
            Assert.AreEqual(2, report.PassedCount);
            Assert.AreEqual(2, report.FailedCount);
        }

        [Test]
        public void RegressionReport_EmptyResults_PassRateOne()
        {
            var report = new RegressionReport(1000L, new List<PerScreenshotResult>());
            Assert.AreEqual(1f, report.GetPassRate(), 0.001f,
                "Empty report should return pass rate 1.0 (no failures).");
            Assert.AreEqual(0, report.TotalComparisons);
        }

        [Test]
        public void RegressionReport_AllFailed_PassRateZero()
        {
            var results = new List<PerScreenshotResult>
            {
                new PerScreenshotResult("A", new ComparisonResult(false, 0.1f, 5000, 80f)),
                new PerScreenshotResult("B", new ComparisonResult(false, 0.2f, 4000, 60f))
            };
            var report = new RegressionReport(1000L, results);
            Assert.AreEqual(0f, report.GetPassRate(), 0.001f,
                "All-fail report should have pass rate of 0.0.");
            Assert.AreEqual(0, report.PassedCount);
            Assert.AreEqual(2, report.FailedCount);
        }

        // --- BaselineManager version tracking ---

        [Test]
        public void BaselineManager_RegisterAndRetrieve_Success()
        {
            var manager = new BaselineManager();
            var checkpoint = new CameraCheckpoint("Cam", 0f, 0f, 0f, 0f, 0f, 0f);
            var spec = new ScreenshotSpec("Test", checkpoint, 1920, 1080, "Match");
            var set = new BaselineSet("v1", 1000L, new List<ScreenshotSpec> { spec }, "macOS", "URP");

            manager.RegisterBaseline(set);
            var retrieved = manager.GetBaseline("v1");
            Assert.IsNotNull(retrieved);
            Assert.AreEqual("v1", retrieved.Version);
        }

        [Test]
        public void BaselineManager_GetMissingVersion_ReturnsNull()
        {
            var manager = new BaselineManager();
            Assert.IsNull(manager.GetBaseline("v99"),
                "Retrieving a non-existent version should return null.");
        }

        [Test]
        public void BaselineManager_BaselineCount_TracksRegistrations()
        {
            var manager = new BaselineManager();
            Assert.AreEqual(0, manager.BaselineCount);

            var checkpoint = new CameraCheckpoint("Cam", 0f, 0f, 0f, 0f, 0f, 0f);
            var spec = new ScreenshotSpec("Test", checkpoint, 1920, 1080, "Match");

            manager.RegisterBaseline(
                new BaselineSet("v1", 1000L, new List<ScreenshotSpec> { spec }, "macOS", "URP"));
            Assert.AreEqual(1, manager.BaselineCount);

            manager.RegisterBaseline(
                new BaselineSet("v2", 2000L, new List<ScreenshotSpec> { spec }, "macOS", "URP"));
            Assert.AreEqual(2, manager.BaselineCount);
        }

        [Test]
        public void BaselineManager_RemoveBaseline_DecreasesCount()
        {
            var manager = new BaselineManager();
            var checkpoint = new CameraCheckpoint("Cam", 0f, 0f, 0f, 0f, 0f, 0f);
            var spec = new ScreenshotSpec("Test", checkpoint, 1920, 1080, "Match");
            manager.RegisterBaseline(
                new BaselineSet("v1", 1000L, new List<ScreenshotSpec> { spec }, "macOS", "URP"));

            bool removed = manager.RemoveBaseline("v1");
            Assert.IsTrue(removed);
            Assert.AreEqual(0, manager.BaselineCount);
            Assert.IsNull(manager.GetBaseline("v1"));
        }

        [Test]
        public void BaselineManager_HasVersion_ReturnsTrueForRegistered()
        {
            var manager = new BaselineManager();
            var checkpoint = new CameraCheckpoint("Cam", 0f, 0f, 0f, 0f, 0f, 0f);
            var spec = new ScreenshotSpec("Test", checkpoint, 1920, 1080, "Match");
            manager.RegisterBaseline(
                new BaselineSet("v1", 1000L, new List<ScreenshotSpec> { spec }, "macOS", "URP"));

            Assert.IsTrue(manager.HasVersion("v1"));
            Assert.IsFalse(manager.HasVersion("v2"));
        }

        [Test]
        public void BaselineManager_GetAllVersions_ReturnsRegisteredVersions()
        {
            var manager = new BaselineManager();
            var checkpoint = new CameraCheckpoint("Cam", 0f, 0f, 0f, 0f, 0f, 0f);
            var spec = new ScreenshotSpec("Test", checkpoint, 1920, 1080, "Match");

            manager.RegisterBaseline(
                new BaselineSet("v1", 1000L, new List<ScreenshotSpec> { spec }, "macOS", "URP"));
            manager.RegisterBaseline(
                new BaselineSet("v2", 2000L, new List<ScreenshotSpec> { spec }, "macOS", "URP"));

            var versions = manager.GetAllVersions();
            Assert.AreEqual(2, versions.Count);
            Assert.Contains("v1", versions);
            Assert.Contains("v2", versions);
        }

        [Test]
        public void BaselineManager_IsVersionComplete_ValidSet_ReturnsTrue()
        {
            var manager = new BaselineManager();
            var checkpoint = new CameraCheckpoint("Cam", 0f, 0f, 0f, 0f, 0f, 0f);
            var spec = new ScreenshotSpec("Test", checkpoint, 1920, 1080, "Match");
            manager.RegisterBaseline(
                new BaselineSet("v1", 1000L, new List<ScreenshotSpec> { spec }, "macOS", "URP"));

            Assert.IsTrue(manager.IsVersionComplete("v1"),
                "A valid registered set should be complete.");
        }

        // --- RegressionTestRunner ---

        [Test]
        public void RegressionTestRunner_PixelDiff_IdenticalImages_Passes()
        {
            var thresholds = new ComparisonThresholds(0.95f, 2f);
            var runner = new RegressionTestRunner(ComparisonAlgorithm.PixelDiff, thresholds, 10);
            var img = CreateSolidImage(4, 4, 128, 128, 128, 255);
            var result = runner.Compare(img, img, 4, 4);
            Assert.IsTrue(result.Passed,
                "Identical images should pass pixel diff comparison.");
            Assert.AreEqual(1f, result.Similarity, 0.001f);
            Assert.AreEqual(0f, result.DiffPercentage, 0.001f);
        }

        [Test]
        public void RegressionTestRunner_PixelDiff_DifferentImages_Fails()
        {
            var thresholds = new ComparisonThresholds(0.95f, 2f);
            var runner = new RegressionTestRunner(ComparisonAlgorithm.PixelDiff, thresholds, 10);
            var imgA = CreateSolidImage(4, 4, 0, 0, 0, 255);
            var imgB = CreateSolidImage(4, 4, 255, 255, 255, 255);
            var result = runner.Compare(imgA, imgB, 4, 4);
            Assert.IsFalse(result.Passed,
                "Completely different images should fail pixel diff comparison.");
        }

        [Test]
        public void RegressionTestRunner_NullBaseline_Fails()
        {
            var thresholds = new ComparisonThresholds(0.95f, 2f);
            var runner = new RegressionTestRunner(ComparisonAlgorithm.PixelDiff, thresholds, 10);
            var result = runner.Compare(null, new byte[] { 0, 0, 0, 255 }, 1, 1);
            Assert.IsFalse(result.Passed,
                "Null baseline should fail.");
        }

        [Test]
        public void RegressionTestRunner_RunBatch_MissingCurrent_FailsForMissing()
        {
            var thresholds = new ComparisonThresholds(0.95f, 2f);
            var runner = new RegressionTestRunner(ComparisonAlgorithm.PixelDiff, thresholds, 10);
            var img = CreateSolidImage(4, 4, 128, 128, 128, 255);

            var baselines = new Dictionary<string, byte[]> { { "shot1", img }, { "shot2", img } };
            var current = new Dictionary<string, byte[]> { { "shot1", img } }; // shot2 missing

            var report = runner.RunBatch(baselines, current, 4, 4);
            Assert.AreEqual(2, report.TotalComparisons);
            Assert.AreEqual(1, report.PassedCount, "shot1 should pass.");
            Assert.AreEqual(1, report.FailedCount, "shot2 should fail (missing).");
        }

        // --- ComparisonAlgorithm enum coverage ---

        [Test]
        public void ComparisonAlgorithm_Enum_HasFourValues()
        {
            var values = System.Enum.GetValues(typeof(ComparisonAlgorithm));
            Assert.AreEqual(4, values.Length,
                "ComparisonAlgorithm should have 4 values: PixelDiff, PerceptualHash, SSIM, HistogramDiff.");
        }

        // --- Helper method ---

        /// <summary>
        /// Creates a solid color RGBA image of the specified dimensions.
        /// </summary>
        private static byte[] CreateSolidImage(int w, int h, byte r, byte g, byte b, byte a)
        {
            int totalPixels = w * h;
            var data = new byte[totalPixels * 4];
            for (int i = 0; i < totalPixels; i++)
            {
                data[i * 4 + 0] = r;
                data[i * 4 + 1] = g;
                data[i * 4 + 2] = b;
                data[i * 4 + 3] = a;
            }
            return data;
        }
    }
}
