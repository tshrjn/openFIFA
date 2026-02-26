using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-030")]
    public class US030_FullHUDTests
    {
        [Test]
        public void MinimapLogic_WorldToMinimap_CenterMapsToCenter()
        {
            var logic = new MinimapLogic(50f, 30f, 200f, 120f);
            float mx, my;
            logic.WorldToMinimap(0f, 0f, out mx, out my);
            Assert.AreEqual(100f, mx, 1f, "Center world should map to center minimap X");
            Assert.AreEqual(60f, my, 1f, "Center world should map to center minimap Y");
        }

        [Test]
        public void MinimapLogic_WorldToMinimap_CornerMapsCorrectly()
        {
            var logic = new MinimapLogic(50f, 30f, 200f, 120f);
            float mx, my;
            logic.WorldToMinimap(25f, 15f, out mx, out my);
            Assert.AreEqual(200f, mx, 1f, "Positive corner should map to right edge");
            Assert.AreEqual(120f, my, 1f, "Positive corner should map to top edge");
        }

        [Test]
        public void MinimapLogic_WorldToMinimap_NegativeCorner()
        {
            var logic = new MinimapLogic(50f, 30f, 200f, 120f);
            float mx, my;
            logic.WorldToMinimap(-25f, -15f, out mx, out my);
            Assert.AreEqual(0f, mx, 1f);
            Assert.AreEqual(0f, my, 1f);
        }

        [Test]
        public void MatchStateDisplay_FirstHalf_ReturnsCorrectText()
        {
            var display = new MatchStateDisplay();
            string text = display.GetStateText(MatchState.FirstHalf);
            Assert.AreEqual("FIRST HALF", text);
        }

        [Test]
        public void MatchStateDisplay_HalfTime_ReturnsCorrectText()
        {
            var display = new MatchStateDisplay();
            string text = display.GetStateText(MatchState.HalfTime);
            Assert.AreEqual("HALF TIME", text);
        }

        [Test]
        public void MatchStateDisplay_SecondHalf_ReturnsCorrectText()
        {
            var display = new MatchStateDisplay();
            string text = display.GetStateText(MatchState.SecondHalf);
            Assert.AreEqual("SECOND HALF", text);
        }

        [Test]
        public void MatchStateDisplay_FullTime_ReturnsCorrectText()
        {
            var display = new MatchStateDisplay();
            string text = display.GetStateText(MatchState.FullTime);
            Assert.AreEqual("FULL TIME", text);
        }

        [Test]
        public void MatchStateDisplay_GoalCelebration_ReturnsGoal()
        {
            var display = new MatchStateDisplay();
            string text = display.GetStateText(MatchState.GoalCelebration);
            Assert.AreEqual("GOAL!", text);
        }

        [Test]
        public void MatchStateDisplay_Paused_ReturnsPaused()
        {
            var display = new MatchStateDisplay();
            string text = display.GetStateText(MatchState.Paused);
            Assert.AreEqual("PAUSED", text);
        }

        [Test]
        public void MinimapLogic_ClampsToBounds()
        {
            var logic = new MinimapLogic(50f, 30f, 200f, 120f);
            float mx, my;
            logic.WorldToMinimap(100f, 100f, out mx, out my);
            Assert.LessOrEqual(mx, 200f);
            Assert.LessOrEqual(my, 120f);
        }
    }
}
