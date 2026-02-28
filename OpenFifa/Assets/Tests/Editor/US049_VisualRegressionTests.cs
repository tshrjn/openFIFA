using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US049")]
    [Category("Visual")]
    public class US049_VisualRegressionTests
    {
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
    }
}
