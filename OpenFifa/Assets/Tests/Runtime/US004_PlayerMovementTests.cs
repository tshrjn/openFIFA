using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using OpenFifa.Gameplay;
using OpenFifa.Core;

namespace OpenFifa.Tests.Runtime
{
    [TestFixture]
    [Category("US-004")]
    public class US004_PlayerMovementTests
    {
        private GameObject _pitchRoot;
        private PitchBuilder _pitchBuilder;
        private PitchConfigData _pitchConfig;
        private GameObject _player;
        private PlayerController _playerController;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _pitchConfig = new PitchConfigData();
            _pitchRoot = new GameObject("PitchTestRoot");
            _pitchBuilder = _pitchRoot.AddComponent<PitchBuilder>();
            _pitchBuilder.BuildPitch(_pitchConfig);

            _player = CreateTestPlayer(Vector3.up * 1f);
            _playerController = _player.GetComponent<PlayerController>();

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_pitchRoot != null) Object.Destroy(_pitchRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_HasCapsuleCollider_AndRigidbody()
        {
            yield return null;

            Assert.IsNotNull(_player.GetComponent<CapsuleCollider>(),
                "Player should have a CapsuleCollider component");
            Assert.IsNotNull(_player.GetComponent<Rigidbody>(),
                "Player should have a Rigidbody component");
        }

        [UnityTest]
        public IEnumerator Player_MovesForward_WhenInputApplied()
        {
            yield return new WaitForFixedUpdate();

            Vector3 startPos = _player.transform.position;
            _playerController.SetMoveInput(new Vector2(0, 1)); // Forward (Z+)

            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float deltaZ = _player.transform.position.z - startPos.z;
            Assert.Greater(deltaZ, 0.1f,
                $"Player should move forward (Z+) when input (0,1) applied. " +
                $"Delta Z = {deltaZ}, Start: {startPos}, Current: {_player.transform.position}");
        }

        [UnityTest]
        public IEnumerator Player_MovesRight_WhenInputApplied()
        {
            yield return new WaitForFixedUpdate();

            Vector3 startPos = _player.transform.position;
            _playerController.SetMoveInput(new Vector2(1, 0)); // Right (X+)

            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float deltaX = _player.transform.position.x - startPos.x;
            Assert.Greater(deltaX, 0.1f,
                $"Player should move right (X+) when input (1,0) applied. " +
                $"Delta X = {deltaX}");
        }

        [UnityTest]
        public IEnumerator Player_SprintMode_IncreasesSpeed()
        {
            yield return new WaitForFixedUpdate();

            // First measure normal speed
            _playerController.SetMoveInput(new Vector2(0, 1));
            _playerController.SetSprinting(false);

            float elapsed = 0f;
            while (elapsed < 1f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float normalSpeed = _playerController.CurrentSpeed;

            // Reset
            _playerController.SetMoveInput(Vector2.zero);
            var rb = _player.GetComponent<Rigidbody>();
            rb.linearVelocity = Vector3.zero;
            yield return new WaitForFixedUpdate();

            // Now measure sprint speed
            _playerController.SetMoveInput(new Vector2(0, 1));
            _playerController.SetSprinting(true);

            elapsed = 0f;
            while (elapsed < 1f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float sprintSpeed = _playerController.CurrentSpeed;

            Assert.Greater(sprintSpeed, normalSpeed * 1.2f,
                $"Sprint speed ({sprintSpeed:F2} m/s) should be significantly faster " +
                $"than normal speed ({normalSpeed:F2} m/s)");
        }

        [UnityTest]
        public IEnumerator Player_IsSprinting_ReturnsTrueWhenSprinting()
        {
            yield return null;

            _playerController.SetSprinting(true);
            _playerController.SetMoveInput(new Vector2(0, 1));

            yield return null;

            Assert.IsTrue(_playerController.IsSprinting,
                "IsSprinting should return true when sprint is active");
        }

        [UnityTest]
        public IEnumerator Player_IsSprinting_ReturnsFalseWhenNotSprinting()
        {
            yield return null;

            _playerController.SetSprinting(false);

            Assert.IsFalse(_playerController.IsSprinting,
                "IsSprinting should return false when sprint is not active");
        }

        [UnityTest]
        public IEnumerator Player_CurrentSpeed_ReturnsVelocityMagnitude()
        {
            yield return new WaitForFixedUpdate();

            _playerController.SetMoveInput(new Vector2(1, 0));

            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            var rb = _player.GetComponent<Rigidbody>();
            Assert.AreEqual(rb.linearVelocity.magnitude, _playerController.CurrentSpeed, 0.1f,
                $"CurrentSpeed ({_playerController.CurrentSpeed:F2}) should match " +
                $"Rigidbody velocity magnitude ({rb.linearVelocity.magnitude:F2})");
        }

        [UnityTest]
        public IEnumerator Player_DiagonalMovement_DoesNotExceedMaxSpeed()
        {
            yield return new WaitForFixedUpdate();

            _playerController.SetMoveInput(new Vector2(1, 1)); // Diagonal
            _playerController.SetSprinting(false);

            // Wait for acceleration
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            var stats = new PlayerStatsData();
            float maxSpeed = stats.BaseSpeed * 1.1f; // Small tolerance

            Assert.LessOrEqual(_playerController.CurrentSpeed, maxSpeed,
                $"Diagonal movement speed ({_playerController.CurrentSpeed:F2} m/s) " +
                $"should not exceed max base speed ({stats.BaseSpeed} m/s). " +
                $"Input should be normalized.");
        }

        [UnityTest]
        public IEnumerator Player_ReachesTopSpeed_Within1To3Seconds()
        {
            yield return new WaitForFixedUpdate();

            _playerController.SetMoveInput(new Vector2(0, 1));
            _playerController.SetSprinting(false);

            var stats = new PlayerStatsData();
            float targetSpeed = stats.BaseSpeed * 0.9f; // 90% of top speed
            float timeout = 5f;
            float elapsed = 0f;

            while (elapsed < timeout && _playerController.CurrentSpeed < targetSpeed)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.That(elapsed, Is.InRange(0.5f, 3.5f),
                $"Player should reach ~90% top speed ({targetSpeed:F1} m/s) in 1-3s " +
                $"but took {elapsed:F2}s. Current speed: {_playerController.CurrentSpeed:F2} m/s");
        }

        [UnityTest]
        public IEnumerator Player_YRotation_IsFrozen()
        {
            yield return null;

            var rb = _player.GetComponent<Rigidbody>();
            bool yRotationFrozen = (rb.constraints & RigidbodyConstraints.FreezeRotationY) != 0;
            Assert.IsTrue(yRotationFrozen,
                $"Player Rigidbody should freeze Y rotation. Constraints: {rb.constraints}");
        }

        [UnityTest]
        public IEnumerator Player_MovingTowardBoundary_CannotExceedBoundaries()
        {
            yield return new WaitForFixedUpdate();

            // Move toward north boundary
            _playerController.SetMoveInput(new Vector2(0, 1));
            _playerController.SetSprinting(true);

            float timeout = 5f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float halfWidth = _pitchConfig.PitchWidth / 2f;
            Assert.Less(_player.transform.position.z, halfWidth + 2f,
                $"Player should be stopped by boundary. Position.z = {_player.transform.position.z}, " +
                $"boundary at z={halfWidth}");
        }

        private GameObject CreateTestPlayer(Vector3 position)
        {
            var player = new GameObject("Player");
            player.transform.position = position;

            int playerLayer = LayerMask.NameToLayer("Player");
            player.layer = playerLayer != -1 ? playerLayer : 0;

            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
            capsule.center = new Vector3(0, 0.9f, 0);

            var rb = player.AddComponent<Rigidbody>();
            rb.mass = 75f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationY
                           | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var controller = player.AddComponent<PlayerController>();
            controller.Initialize(new PlayerStatsData());

            return player;
        }
    }
}
