using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-012")]
    public class US012_AIShootingTests
    {
        [Test]
        public void ShotEvaluator_InRange_ReturnsTrue()
        {
            var evaluator = new ShotEvaluator(shootRange: 15f);
            bool inRange = evaluator.IsInShootingRange(
                shooterX: 15f, shooterZ: 0f,
                goalX: 25f, goalZ: 0f);
            Assert.IsTrue(inRange,
                "Should be in range at distance 10 (< 15)");
        }

        [Test]
        public void ShotEvaluator_OutOfRange_ReturnsFalse()
        {
            var evaluator = new ShotEvaluator(shootRange: 15f);
            bool inRange = evaluator.IsInShootingRange(
                shooterX: 0f, shooterZ: 0f,
                goalX: 25f, goalZ: 0f);
            Assert.IsFalse(inRange,
                "Should not be in range at distance 25 (> 15)");
        }

        [Test]
        public void ShotEvaluator_CalculateShotForce_IsReasonable()
        {
            var evaluator = new ShotEvaluator(shootRange: 15f);
            float force = evaluator.CalculateShotForce(15f);
            Assert.That(force, Is.InRange(10f, 25f),
                $"Shot force at 15m should be 10-25 but was {force:F2}");
        }

        [Test]
        public void ShotEvaluator_ShotTarget_IsWithinGoalWidth()
        {
            var evaluator = new ShotEvaluator(shootRange: 15f);
            float goalCenterZ = 0f;
            float goalHalfWidth = 2.5f;

            // Run multiple times to test randomness
            for (int i = 0; i < 20; i++)
            {
                float targetZ = evaluator.CalculateShotTargetZ(goalCenterZ, goalHalfWidth);
                Assert.That(targetZ, Is.InRange(-goalHalfWidth, goalHalfWidth),
                    $"Shot target Z ({targetZ:F2}) should be within goal width [-{goalHalfWidth}, {goalHalfWidth}]");
            }
        }

        [Test]
        public void ShotEvaluator_ShotTarget_Varies()
        {
            var evaluator = new ShotEvaluator(shootRange: 15f);
            float goalCenterZ = 0f;
            float goalHalfWidth = 2.5f;

            bool allSame = true;
            float firstTarget = evaluator.CalculateShotTargetZ(goalCenterZ, goalHalfWidth);
            for (int i = 0; i < 10; i++)
            {
                float target = evaluator.CalculateShotTargetZ(goalCenterZ, goalHalfWidth);
                if (System.Math.Abs(target - firstTarget) > 0.01f)
                {
                    allSame = false;
                    break;
                }
            }
            Assert.IsFalse(allSame,
                "Shot targets should vary (not always the same)");
        }

        [Test]
        public void ShotEvaluator_PrefersShooting_WhenInRangeAndClear()
        {
            var evaluator = new ShotEvaluator(shootRange: 15f);
            bool shouldShoot = evaluator.ShouldShoot(
                distanceToGoal: 10f,
                hasClearLine: true,
                hasBallPossession: true);
            Assert.IsTrue(shouldShoot,
                "Should prefer shooting when in range with clear line and possession");
        }

        [Test]
        public void ShotEvaluator_DoesNotShoot_WithoutPossession()
        {
            var evaluator = new ShotEvaluator(shootRange: 15f);
            bool shouldShoot = evaluator.ShouldShoot(
                distanceToGoal: 10f,
                hasClearLine: true,
                hasBallPossession: false);
            Assert.IsFalse(shouldShoot,
                "Should not shoot without ball possession");
        }

        [Test]
        public void ShotEvaluator_DoesNotShoot_WithoutClearLine()
        {
            var evaluator = new ShotEvaluator(shootRange: 15f);
            bool shouldShoot = evaluator.ShouldShoot(
                distanceToGoal: 10f,
                hasClearLine: false,
                hasBallPossession: true);
            Assert.IsFalse(shouldShoot,
                "Should not shoot without clear line");
        }
    }
}
