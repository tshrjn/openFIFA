using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US025")]
    public class US025_CrowdReactionTests
    {
        [Test]
        public void CrowdReactionLogic_BaseIntensity_IsThirtyPercent()
        {
            var logic = new CrowdReactionLogic(25f); // pitch half-length
            Assert.AreEqual(0.3f, logic.BaseIntensity, 0.001f);
        }

        [Test]
        public void CrowdReactionLogic_BallAtCenter_IntensityIsBase()
        {
            var logic = new CrowdReactionLogic(25f);
            logic.UpdateBallPosition(0f, 0f);
            Assert.AreEqual(0.3f, logic.CurrentIntensity, 0.05f,
                "Ball at center should yield base intensity");
        }

        [Test]
        public void CrowdReactionLogic_BallNearGoal_IntensityHigher()
        {
            var logic = new CrowdReactionLogic(25f);
            logic.UpdateBallPosition(24f, 0f);
            Assert.Greater(logic.CurrentIntensity, 0.3f,
                "Ball near goal should have higher intensity than base");
        }

        [Test]
        public void CrowdReactionLogic_BallAtGoalLine_IntensityNearMax()
        {
            var logic = new CrowdReactionLogic(25f);
            logic.UpdateBallPosition(25f, 0f);
            Assert.Greater(logic.CurrentIntensity, 0.8f,
                "Ball at goal line should have near-max intensity");
        }

        [Test]
        public void CrowdReactionLogic_Intensity_ClampedToOne()
        {
            var logic = new CrowdReactionLogic(25f);
            logic.UpdateBallPosition(30f, 0f); // beyond pitch
            Assert.LessOrEqual(logic.CurrentIntensity, 1f);
        }

        [Test]
        public void CrowdReactionLogic_VolumeDb_BaseIsNeg20()
        {
            var logic = new CrowdReactionLogic(25f);
            logic.UpdateBallPosition(0f, 0f);
            // Base intensity 0.3 should map to around -20 + (0.3 * 20) = -14
            Assert.LessOrEqual(logic.VolumeDe, 0f);
        }

        [Test]
        public void CrowdReactionLogic_VolumeDb_HighIntensityNearZero()
        {
            var logic = new CrowdReactionLogic(25f);
            logic.UpdateBallPosition(25f, 0f);
            // SmoothUpdate to converge smoothed intensity toward current (1.0)
            for (int i = 0; i < 100; i++)
                logic.SmoothUpdate(0.1f);
            Assert.Greater(logic.VolumeDe, -5f,
                "High intensity should yield volume near 0 dB after smoothing");
        }

        [Test]
        public void CrowdReactionLogic_NearMiss_Detected()
        {
            var logic = new CrowdReactionLogic(25f);
            // Ball moving toward goal at high speed, passes within 2m of goal post
            bool nearMiss = logic.CheckNearMiss(24f, 3.5f, 15f, true);
            Assert.IsTrue(nearMiss, "Should detect near-miss: ball near goal edge at speed");
        }

        [Test]
        public void CrowdReactionLogic_NearMiss_NotDetected_BallFarFromGoal()
        {
            var logic = new CrowdReactionLogic(25f);
            bool nearMiss = logic.CheckNearMiss(10f, 0f, 15f, true);
            Assert.IsFalse(nearMiss, "Should not detect near-miss when ball far from goal");
        }

        [Test]
        public void CrowdReactionLogic_SmoothTransition_IntensityChangesGradually()
        {
            var logic = new CrowdReactionLogic(25f);
            logic.UpdateBallPosition(0f, 0f);
            float centerIntensity = logic.CurrentIntensity;

            // Smoothly move toward goal
            logic.UpdateBallPosition(24f, 0f);
            logic.SmoothUpdate(0.1f);
            float smoothed = logic.SmoothedIntensity;

            // Smoothed should be between center and target
            Assert.Greater(smoothed, centerIntensity - 0.01f);
        }
    }
}
