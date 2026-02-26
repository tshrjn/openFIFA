using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-010")]
    public class US010_AIStateTests
    {
        [Test]
        public void AIState_HasIdle()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(AIState), AIState.Idle));
        }

        [Test]
        public void AIState_HasChaseBall()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(AIState), AIState.ChaseBall));
        }

        [Test]
        public void AIState_HasReturnToPosition()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(AIState), AIState.ReturnToPosition));
        }

        [Test]
        public void AIConfigData_DefaultChaseRange_Is10()
        {
            var config = new AIConfigData();
            Assert.AreEqual(10f, config.ChaseRange, 0.01f,
                $"Default chase range should be 10 but was {config.ChaseRange}");
        }

        [Test]
        public void AIConfigData_DefaultMoveSpeed_Is6()
        {
            var config = new AIConfigData();
            Assert.AreEqual(6f, config.MoveSpeed, 0.01f,
                $"Default move speed should be 6 but was {config.MoveSpeed}");
        }

        [Test]
        public void AIConfigData_DefaultPositionThreshold_Is1()
        {
            var config = new AIConfigData();
            Assert.AreEqual(1f, config.PositionThreshold, 0.01f,
                $"Default position threshold should be 1 but was {config.PositionThreshold}");
        }

        [Test]
        public void AIDecisionEngine_BallInRange_NearestPlayer_ReturnsChaseBall()
        {
            var config = new AIConfigData();
            var engine = new AIDecisionEngine(config);

            // AI is at origin, ball at (5,0,0) = distance 5 < chaseRange 10
            var decision = engine.Evaluate(
                aiPositionX: 0f, aiPositionZ: 0f,
                ballPositionX: 5f, ballPositionZ: 0f,
                formationPositionX: 0f, formationPositionZ: 0f,
                isNearestToBall: true
            );

            Assert.AreEqual(AIState.ChaseBall, decision,
                $"Should be ChaseBall when nearest to ball in range, got {decision}");
        }

        [Test]
        public void AIDecisionEngine_BallInRange_NotNearestPlayer_ReturnsReturnToPosition()
        {
            var config = new AIConfigData();
            var engine = new AIDecisionEngine(config);

            var decision = engine.Evaluate(
                aiPositionX: 0f, aiPositionZ: 0f,
                ballPositionX: 5f, ballPositionZ: 0f,
                formationPositionX: 0f, formationPositionZ: -6f,
                isNearestToBall: false
            );

            Assert.AreEqual(AIState.ReturnToPosition, decision,
                $"Should be ReturnToPosition when not nearest to ball, got {decision}");
        }

        [Test]
        public void AIDecisionEngine_BallFarAway_AtFormation_ReturnsIdle()
        {
            var config = new AIConfigData();
            var engine = new AIDecisionEngine(config);

            // Ball far away (distance 20 > chaseRange 10), AI at formation position
            var decision = engine.Evaluate(
                aiPositionX: 0f, aiPositionZ: 0f,
                ballPositionX: 20f, ballPositionZ: 0f,
                formationPositionX: 0f, formationPositionZ: 0f,
                isNearestToBall: false
            );

            Assert.AreEqual(AIState.Idle, decision,
                $"Should be Idle when ball far and at formation, got {decision}");
        }

        [Test]
        public void AIDecisionEngine_BallFarAway_NotAtFormation_ReturnsReturnToPosition()
        {
            var config = new AIConfigData();
            var engine = new AIDecisionEngine(config);

            // Ball far, AI far from formation
            var decision = engine.Evaluate(
                aiPositionX: 10f, aiPositionZ: 10f,
                ballPositionX: -20f, ballPositionZ: -20f,
                formationPositionX: 0f, formationPositionZ: 0f,
                isNearestToBall: false
            );

            Assert.AreEqual(AIState.ReturnToPosition, decision,
                $"Should be ReturnToPosition when far from formation, got {decision}");
        }
    }
}
