using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-011")]
    public class US011_AIPassingTests
    {
        [Test]
        public void PassEvaluator_SelectsMostOpenTeammate()
        {
            var evaluator = new PassEvaluator();

            // 3 teammates, 1 opponent
            var teammates = new PositionData[]
            {
                new PositionData(0, 10f, 0f),   // teammate 0
                new PositionData(1, 5f, 5f),    // teammate 1
                new PositionData(2, -10f, 0f),  // teammate 2
            };

            var opponents = new PositionData[]
            {
                new PositionData(0, 6f, 5f),    // near teammate 1
            };

            int bestTarget = evaluator.FindMostOpenTeammate(teammates, opponents);

            // Teammate 0 at (10,0) - nearest opponent at (6,5) = ~5.1m
            // Teammate 1 at (5,5) - nearest opponent at (6,5) = ~1m
            // Teammate 2 at (-10,0) - nearest opponent at (6,5) = ~16.8m
            Assert.AreEqual(2, bestTarget,
                $"Should select teammate 2 (most open) but selected {bestTarget}");
        }

        [Test]
        public void PassEvaluator_ReturnsNegativeOne_WhenNoTeammates()
        {
            var evaluator = new PassEvaluator();
            int target = evaluator.FindMostOpenTeammate(
                new PositionData[0], new PositionData[0]);
            Assert.AreEqual(-1, target,
                "Should return -1 when no teammates available");
        }

        [Test]
        public void PassEvaluator_CalculatesPassForce_ScalesWithDistance()
        {
            var evaluator = new PassEvaluator();

            float shortForce = evaluator.CalculatePassForce(5f);
            float longForce = evaluator.CalculatePassForce(20f);

            Assert.Greater(longForce, shortForce,
                $"Long pass force ({longForce:F2}) should be > short ({shortForce:F2})");
        }

        [Test]
        public void PassEvaluator_CalculatePassForce_HasMinimumForce()
        {
            var evaluator = new PassEvaluator();
            float force = evaluator.CalculatePassForce(0.1f);
            Assert.Greater(force, 0f,
                $"Pass force should be > 0 even at short distance, was {force:F2}");
        }

        [Test]
        public void PassEvaluator_CalculatePassForce_HasMaximumForce()
        {
            var evaluator = new PassEvaluator();
            float force = evaluator.CalculatePassForce(100f);
            Assert.LessOrEqual(force, 20f,
                $"Pass force should be capped at 20, was {force:F2}");
        }

        [Test]
        public void PassEvaluator_OpennessScore_IsDistanceToNearestOpponent()
        {
            var evaluator = new PassEvaluator();

            var teammates = new PositionData[]
            {
                new PositionData(0, 0f, 0f),
            };
            var opponents = new PositionData[]
            {
                new PositionData(0, 3f, 4f), // distance = 5
            };

            float score = evaluator.CalculateOpenness(teammates[0], opponents);
            Assert.AreEqual(5f, score, 0.1f,
                $"Openness score should be 5 (distance to nearest opponent) but was {score:F2}");
        }

        [Test]
        public void PassEvaluator_OpennessScore_WithNoOpponents_IsMaxValue()
        {
            var evaluator = new PassEvaluator();
            var teammate = new PositionData(0, 0f, 0f);
            float score = evaluator.CalculateOpenness(teammate, new PositionData[0]);
            Assert.AreEqual(float.MaxValue, score,
                "Openness with no opponents should be MaxValue");
        }
    }
}
