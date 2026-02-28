using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using OpenFifa.AI;
using OpenFifa.Core;
using OpenFifa.Gameplay;

namespace OpenFifa.Tests.Runtime
{
    [TestFixture]
    [Category("US010")]
    public class US010_AIControllerTests
    {
        private GameObject _pitchRoot;
        private GameObject _ball;
        private GameObject _aiPlayer;
        private AIController _aiController;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var pitchConfig = new PitchConfigData();
            _pitchRoot = new GameObject("PitchTestRoot");
            var builder = _pitchRoot.AddComponent<PitchBuilder>();
            builder.BuildPitch(pitchConfig);

            _ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _ball.name = "Ball";
            _ball.tag = "Ball";
            _ball.transform.position = new Vector3(0, 0.5f, 0);

            _aiPlayer = CreateAIPlayer(new Vector3(-5, 1, -6));

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_aiPlayer != null) Object.Destroy(_aiPlayer);
            if (_ball != null) Object.Destroy(_ball);
            if (_pitchRoot != null) Object.Destroy(_pitchRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AIController_HasCurrentState_Property()
        {
            yield return null;
            Assert.IsNotNull(_aiController,
                "AIController should be attached to AI player");
            // Just verifying the property exists and returns a valid state
            var state = _aiController.CurrentState;
            Assert.IsTrue(System.Enum.IsDefined(typeof(AIState), state),
                $"CurrentState should be a valid AIState value, was {state}");
        }

        [UnityTest]
        public IEnumerator AIController_BallNearby_EntersChaseBallState()
        {
            // Place ball near the AI player
            _ball.transform.position = new Vector3(-3, 0.5f, -6);
            _aiController.SetAsNearestToBall(true);

            float timeout = 2f;
            float elapsed = 0f;
            while (elapsed < timeout && _aiController.CurrentState != AIState.ChaseBall)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(AIState.ChaseBall, _aiController.CurrentState,
                $"AI should enter ChaseBall when ball is nearby. " +
                $"State: {_aiController.CurrentState}, Ball dist: " +
                $"{Vector3.Distance(_aiPlayer.transform.position, _ball.transform.position):F2}");
        }

        [UnityTest]
        public IEnumerator AIController_BallFarAway_EntersReturnToPositionState()
        {
            // Place ball far away
            _ball.transform.position = new Vector3(20, 0.5f, 10);
            _aiController.SetAsNearestToBall(false);

            // Move AI away from formation position
            _aiPlayer.transform.position = new Vector3(10, 1, 10);

            float timeout = 2f;
            float elapsed = 0f;
            while (elapsed < timeout && _aiController.CurrentState != AIState.ReturnToPosition)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(AIState.ReturnToPosition, _aiController.CurrentState,
                $"AI should enter ReturnToPosition when ball is far. " +
                $"State: {_aiController.CurrentState}");
        }

        [UnityTest]
        public IEnumerator AIController_MovesTowardBall_InChaseBallState()
        {
            _ball.transform.position = new Vector3(0, 0.5f, 0);
            _aiController.SetAsNearestToBall(true);

            float initialDist = Vector3.Distance(
                _aiPlayer.transform.position, _ball.transform.position);

            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float finalDist = Vector3.Distance(
                _aiPlayer.transform.position, _ball.transform.position);

            Assert.Less(finalDist, initialDist,
                $"AI should move closer to ball. Initial dist: {initialDist:F2}, Final: {finalDist:F2}");
        }

        [UnityTest]
        public IEnumerator AIController_MovesTowardFormation_InReturnToPositionState()
        {
            // Ball far away, AI far from formation
            _ball.transform.position = new Vector3(20, 0.5f, 20);
            _aiController.SetAsNearestToBall(false);
            _aiPlayer.transform.position = new Vector3(10, 1, 10);

            Vector3 formationPos = _aiController.FormationPosition;
            float initialDist = Vector3.Distance(
                new Vector3(_aiPlayer.transform.position.x, 0, _aiPlayer.transform.position.z),
                new Vector3(formationPos.x, 0, formationPos.z));

            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float finalDist = Vector3.Distance(
                new Vector3(_aiPlayer.transform.position.x, 0, _aiPlayer.transform.position.z),
                new Vector3(formationPos.x, 0, formationPos.z));

            Assert.Less(finalDist, initialDist,
                $"AI should move toward formation. Initial dist: {initialDist:F2}, Final: {finalDist:F2}. " +
                $"Formation pos: {formationPos}");
        }

        private GameObject CreateAIPlayer(Vector3 position)
        {
            var player = new GameObject("AIPlayer");
            player.transform.position = position;

            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
            capsule.center = new Vector3(0, 0.9f, 0);

            var rb = player.AddComponent<Rigidbody>();
            rb.mass = 75f;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationY
                           | RigidbodyConstraints.FreezeRotationZ;

            _aiController = player.AddComponent<AIController>();
            _aiController.Initialize(
                new AIConfigData(),
                _ball.transform,
                new Vector3(-5, 0, -6) // Formation position
            );

            return player;
        }
    }
}
