using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US002")]
    public class US002_PitchConfigTests
    {
        [Test]
        public void PitchConfig_DefaultPitchLength_Is50()
        {
            var config = new PitchConfigData();
            Assert.AreEqual(50f, config.PitchLength, 0.01f,
                $"Default pitch length should be 50m but was {config.PitchLength}m");
        }

        [Test]
        public void PitchConfig_DefaultPitchWidth_Is30()
        {
            var config = new PitchConfigData();
            Assert.AreEqual(30f, config.PitchWidth, 0.01f,
                $"Default pitch width should be 30m but was {config.PitchWidth}m");
        }

        [Test]
        public void PitchConfig_DefaultGoalWidth_Is5()
        {
            var config = new PitchConfigData();
            Assert.AreEqual(5f, config.GoalWidth, 0.01f,
                $"Default goal width should be 5m but was {config.GoalWidth}m");
        }

        [Test]
        public void PitchConfig_DefaultCenterCircleRadius_Is3()
        {
            var config = new PitchConfigData();
            Assert.AreEqual(3f, config.CenterCircleRadius, 0.01f,
                $"Default center circle radius should be 3m but was {config.CenterCircleRadius}m");
        }

        [Test]
        public void PitchConfig_DefaultGoalAreaDepth_Is4()
        {
            var config = new PitchConfigData();
            Assert.AreEqual(4f, config.GoalAreaDepth, 0.01f,
                $"Default goal area depth should be 4m but was {config.GoalAreaDepth}m");
        }

        [Test]
        public void PitchConfig_DefaultGoalHeight_Is2Point4()
        {
            var config = new PitchConfigData();
            Assert.AreEqual(2.4f, config.GoalHeight, 0.01f,
                $"Default goal height should be 2.4m but was {config.GoalHeight}m");
        }

        [Test]
        public void PitchConfig_DefaultBoundaryWallHeight_Is3()
        {
            var config = new PitchConfigData();
            Assert.AreEqual(3f, config.BoundaryWallHeight, 0.01f,
                $"Default boundary wall height should be 3m but was {config.BoundaryWallHeight}m");
        }

        [Test]
        public void PitchConfig_DefaultBoundaryWallThickness_Is1()
        {
            var config = new PitchConfigData();
            Assert.AreEqual(1f, config.BoundaryWallThickness, 0.01f,
                $"Default boundary wall thickness should be 1m but was {config.BoundaryWallThickness}m");
        }

        [Test]
        public void PitchConfig_CustomValues_AreRetained()
        {
            var config = new PitchConfigData(
                pitchLength: 60f,
                pitchWidth: 40f,
                goalWidth: 6f,
                centerCircleRadius: 4f,
                goalAreaDepth: 5f,
                goalHeight: 3f,
                boundaryWallHeight: 4f,
                boundaryWallThickness: 1.5f
            );

            Assert.AreEqual(60f, config.PitchLength, 0.01f, "Custom pitch length not retained");
            Assert.AreEqual(40f, config.PitchWidth, 0.01f, "Custom pitch width not retained");
            Assert.AreEqual(6f, config.GoalWidth, 0.01f, "Custom goal width not retained");
            Assert.AreEqual(4f, config.CenterCircleRadius, 0.01f, "Custom center circle radius not retained");
            Assert.AreEqual(5f, config.GoalAreaDepth, 0.01f, "Custom goal area depth not retained");
            Assert.AreEqual(3f, config.GoalHeight, 0.01f, "Custom goal height not retained");
        }

        [Test]
        public void PitchConfig_HalfLength_IsHalfOfPitchLength()
        {
            var config = new PitchConfigData();
            Assert.AreEqual(25f, config.HalfLength, 0.01f,
                $"Half length should be 25m but was {config.HalfLength}m");
        }

        [Test]
        public void PitchConfig_HalfWidth_IsHalfOfPitchWidth()
        {
            var config = new PitchConfigData();
            Assert.AreEqual(15f, config.HalfWidth, 0.01f,
                $"Half width should be 15m but was {config.HalfWidth}m");
        }

        [Test]
        public void PitchConfig_GoalOpeningHalfWidth_IsHalfOfGoalWidth()
        {
            var config = new PitchConfigData();
            Assert.AreEqual(2.5f, config.GoalOpeningHalfWidth, 0.01f,
                $"Goal opening half width should be 2.5m but was {config.GoalOpeningHalfWidth}m");
        }
    }
}
