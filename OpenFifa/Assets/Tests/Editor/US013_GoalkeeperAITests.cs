using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US013")]
    public class US013_GoalkeeperAITests
    {
        [Test]
        public void GoalkeeperState_EnumValues_IncludesPositioning()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(GoalkeeperState), GoalkeeperState.Positioning));
        }

        [Test]
        public void GoalkeeperState_EnumValues_IncludesDiving()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(GoalkeeperState), GoalkeeperState.Diving));
        }

        [Test]
        public void GoalkeeperState_EnumValues_IncludesRecovering()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(GoalkeeperState), GoalkeeperState.Recovering));
        }

        [Test]
        public void GoalkeeperLogic_LateralPosition_IsBetweenBallAndGoal()
        {
            var logic = new GoalkeeperLogic(goalAreaWidth: 5f, goalCenterX: 25f, goalCenterZ: 0f);

            float targetZ = logic.CalculateLateralPosition(ballZ: 5f);

            // Should be between goal center (0) and ball Z (5), clamped to area
            Assert.That(targetZ, Is.InRange(0f, 5f),
                $"Lateral position should be between 0 and 5 but was {targetZ:F2}");
        }

        [Test]
        public void GoalkeeperLogic_LateralPosition_ClampedToGoalArea()
        {
            var logic = new GoalkeeperLogic(goalAreaWidth: 5f, goalCenterX: 25f, goalCenterZ: 0f);

            float targetZ = logic.CalculateLateralPosition(ballZ: 20f);
            float halfArea = 5f / 2f;

            Assert.LessOrEqual(targetZ, halfArea,
                $"Lateral position should be clamped to goal area half width ({halfArea}) but was {targetZ:F2}");
        }

        [Test]
        public void GoalkeeperLogic_DetectsShot_WhenBallMovingTowardGoal()
        {
            var logic = new GoalkeeperLogic(goalAreaWidth: 5f, goalCenterX: 25f, goalCenterZ: 0f);

            bool isShot = logic.IsShotDetected(
                ballX: 15f, ballZ: 0f,
                ballVelocityX: 10f, ballVelocityZ: 0f,
                ballSpeed: 10f,
                speedThreshold: 5f
            );

            Assert.IsTrue(isShot,
                "Should detect shot when ball moves toward goal at high speed");
        }

        [Test]
        public void GoalkeeperLogic_DoesNotDetectShot_WhenBallMovingAway()
        {
            var logic = new GoalkeeperLogic(goalAreaWidth: 5f, goalCenterX: 25f, goalCenterZ: 0f);

            bool isShot = logic.IsShotDetected(
                ballX: 15f, ballZ: 0f,
                ballVelocityX: -10f, ballVelocityZ: 0f, // Moving away
                ballSpeed: 10f,
                speedThreshold: 5f
            );

            Assert.IsFalse(isShot,
                "Should not detect shot when ball moves away from goal");
        }

        [Test]
        public void GoalkeeperLogic_DoesNotDetectShot_WhenBallSlow()
        {
            var logic = new GoalkeeperLogic(goalAreaWidth: 5f, goalCenterX: 25f, goalCenterZ: 0f);

            bool isShot = logic.IsShotDetected(
                ballX: 15f, ballZ: 0f,
                ballVelocityX: 2f, ballVelocityZ: 0f,
                ballSpeed: 2f,
                speedThreshold: 5f
            );

            Assert.IsFalse(isShot,
                "Should not detect shot when ball is slow");
        }
    }
}
