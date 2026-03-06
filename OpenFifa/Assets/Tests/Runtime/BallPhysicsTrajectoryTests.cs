using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using OpenFifa.Gameplay;
using OpenFifa.Core;

namespace OpenFifa.Tests.Runtime
{
    [TestFixture]
    [Category("BallPhysicsTrajectory")]
    public class BallPhysicsTrajectoryTests
    {
        private GameObject _pitchRoot;
        private PitchBuilder _pitchBuilder;
        private PitchConfigData _pitchConfig;
        private List<GameObject> _spawnedObjects;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _pitchConfig = new PitchConfigData();
            _pitchRoot = new GameObject("PitchTestRoot");
            _pitchBuilder = _pitchRoot.AddComponent<PitchBuilder>();
            _pitchBuilder.BuildPitch(_pitchConfig);

            _spawnedObjects = new List<GameObject>();

            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var obj in _spawnedObjects)
            {
                if (obj != null) Object.Destroy(obj);
            }
            if (_pitchRoot != null) Object.Destroy(_pitchRoot);
            yield return null;
        }

        #region Helpers

        private GameObject CreateTestBall(Vector3 position, bool useGravity = true)
        {
            var ball = new GameObject("Ball");
            ball.tag = "Ball";
            int ballLayer = LayerMask.NameToLayer("Ball");
            ball.layer = ballLayer != -1 ? ballLayer : 0;

            var sphereCollider = ball.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.11f;

            var physicsMaterial = new PhysicsMaterial("TestBallMaterial");
            physicsMaterial.bounciness = 0.8f;
            physicsMaterial.dynamicFriction = 0.5f;
            physicsMaterial.staticFriction = 0.5f;
            physicsMaterial.bounceCombine = PhysicsMaterialCombine.Maximum;
            physicsMaterial.frictionCombine = PhysicsMaterialCombine.Average;
            sphereCollider.sharedMaterial = physicsMaterial;

            var rb = ball.AddComponent<Rigidbody>();
            rb.mass = 0.43f;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.5f;
            rb.useGravity = useGravity;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            ball.transform.position = position;

            _spawnedObjects.Add(ball);
            return ball;
        }

        private GameObject CreateTestPlayer(Vector3 position, TeamIdentifier team, int id)
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = $"{team}_Player_{id}";
            player.transform.position = position;

            var rb = player.AddComponent<Rigidbody>();
            rb.mass = 75f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.useGravity = true;
            rb.isKinematic = true; // Keep players stationary for tests

            _spawnedObjects.Add(player);
            return player;
        }

        #endregion

        // =========================================================================
        // Ground Pass Mechanics (4 tests)
        // =========================================================================

        [UnityTest]
        public IEnumerator GroundPass_BallStaysLow_RollsWithFriction()
        {
            // Ball on ground with horizontal-only force, gravity on
            var ball = CreateTestBall(new Vector3(0, 0.15f, 5f), useGravity: true);
            var rb = ball.GetComponent<Rigidbody>();

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            // Apply ground pass force (horizontal only)
            rb.linearVelocity = new Vector3(8f, 0, 0);

            float maxHeight = 0f;
            float initialSpeed = rb.linearVelocity.magnitude;
            float timeout = 5f;
            float elapsed = 0f;

            while (elapsed < timeout && rb.linearVelocity.magnitude > 0.1f)
            {
                elapsed += Time.deltaTime;
                if (ball.transform.position.y > maxHeight)
                    maxHeight = ball.transform.position.y;
                yield return null;
            }

            Assert.Less(maxHeight, 0.5f,
                $"Ground pass should stay low (max height < 0.5m) but reached {maxHeight:F3}m");

            float finalSpeed = rb.linearVelocity.magnitude;
            Assert.Less(finalSpeed, initialSpeed,
                $"Ball should decelerate due to friction. Initial: {initialSpeed:F2}, Final: {finalSpeed:F2}");
        }

        [UnityTest]
        public IEnumerator Pass_BallReachesTeammateArea()
        {
            // Pass from origin toward x=10
            var ball = CreateTestBall(new Vector3(0, 0.15f, 5f), useGravity: true);
            var rb = ball.GetComponent<Rigidbody>();

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            rb.linearVelocity = new Vector3(8f, 0, 0);
            float targetX = 10f;
            float arrivalRadius = 3f;

            float timeout = 5f;
            float elapsed = 0f;
            bool arrived = false;

            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                if (Mathf.Abs(ball.transform.position.x - targetX) < arrivalRadius)
                {
                    arrived = true;
                    break;
                }
                yield return null;
            }

            Assert.IsTrue(arrived,
                $"Pass should reach teammate area (x={targetX}±{arrivalRadius}). " +
                $"Ball ended at {ball.transform.position}, Velocity: {rb.linearVelocity}");
        }

        [UnityTest]
        public IEnumerator PassForce_ScalesWithDistance()
        {
            yield return null;

            var evaluator = new PassEvaluator(minPassForce: 4f, maxPassForce: 20f, passForceMultiplier: 0.5f);
            float shortForce = evaluator.CalculatePassForce(5f);   // 4 + 5*0.5 = 6.5
            float longForce = evaluator.CalculatePassForce(32f);   // 4 + 32*0.5 = 20 (clamped)

            Assert.That(shortForce, Is.InRange(5f, 8f),
                $"Short pass (5m) force should be ~6.5 but was {shortForce:F2}");
            Assert.That(longForce, Is.InRange(18f, 20f),
                $"Long pass (32m) force should be ~20 (clamped) but was {longForce:F2}");
            Assert.Greater(longForce, shortForce,
                $"Long pass force ({longForce:F2}) should exceed short pass force ({shortForce:F2})");
        }

        [UnityTest]
        public IEnumerator PassDirection_IsAccurate()
        {
            // Apply force at 45 degrees (1,0,1 normalized)
            var ball = CreateTestBall(new Vector3(0, 0.5f, 0), useGravity: false);
            var rb = ball.GetComponent<Rigidbody>();

            yield return new WaitForFixedUpdate();

            float force = 8f;
            var direction = new Vector3(1f, 0, 1f).normalized;
            rb.AddForce(direction * force, ForceMode.Impulse);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            // Measure actual direction
            var vel = rb.linearVelocity;
            var velFlat = new Vector3(vel.x, 0, vel.z).normalized;
            float angleDeg = Vector3.Angle(direction, velFlat);

            Assert.Less(angleDeg, 15f,
                $"Pass direction should be within 15° of target. " +
                $"Angle: {angleDeg:F1}°, Velocity: {vel}, Expected direction: {direction}");
        }

        // =========================================================================
        // Shot Mechanics (4 tests)
        // =========================================================================

        [UnityTest]
        public IEnumerator Shot_WithUpwardArc_BouncesThenRolls()
        {
            var ball = CreateTestBall(new Vector3(0, 0.5f, 5f), useGravity: true);
            var rb = ball.GetComponent<Rigidbody>();

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            // Impulse with upward component (lob shot)
            rb.AddForce(new Vector3(12f, 3f, 0), ForceMode.Impulse);

            float maxY = 0f;
            float timeout = 5f;
            float elapsed = 0f;
            bool hasLanded = false;

            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                if (ball.transform.position.y > maxY)
                    maxY = ball.transform.position.y;

                // Consider landed when ball is low and has been high before
                if (maxY > 1f && ball.transform.position.y < 0.3f)
                {
                    hasLanded = true;
                    break;
                }
                yield return null;
            }

            Assert.Greater(maxY, 1f,
                $"Lob shot should arc above 1m but max height was {maxY:F3}m");
            Assert.IsTrue(hasLanded,
                $"Ball should eventually land. Max height: {maxY:F3}m, " +
                $"Final position: {ball.transform.position}");
        }

        [UnityTest]
        public IEnumerator Shot_FromWithinRange_ReachesGoalArea()
        {
            // Place ball within shooting range, shoot toward east goal
            var ball = CreateTestBall(new Vector3(15f, 0.15f, 0), useGravity: true);
            var rb = ball.GetComponent<Rigidbody>();

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            rb.linearVelocity = new Vector3(15f, 0, 0);

            float timeout = 3f;
            float elapsed = 0f;
            float maxX = ball.transform.position.x;

            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                if (ball.transform.position.x > maxX)
                    maxX = ball.transform.position.x;
                yield return null;
            }

            Assert.Greater(maxX, 24f,
                $"Shot from x=15 should reach goal area (x>24). Max X reached: {maxX:F2}");
        }

        [UnityTest]
        public IEnumerator ShotForce_ScalesWithDistance()
        {
            yield return null;

            var evaluator = new ShotEvaluator(shootRange: 15f, baseShotForce: 12f, shotForceMultiplier: 0.5f);
            float closeForce = evaluator.CalculateShotForce(5f);   // 12 + 5*0.5 = 14.5
            float farForce = evaluator.CalculateShotForce(15f);    // 12 + 15*0.5 = 19.5

            Assert.That(closeForce, Is.InRange(13f, 16f),
                $"Close shot (5m) force should be ~14.5 but was {closeForce:F2}");
            Assert.That(farForce, Is.InRange(18f, 21f),
                $"Far shot (15m) force should be ~19.5 but was {farForce:F2}");
            Assert.Greater(farForce, closeForce,
                $"Far shot force ({farForce:F2}) should exceed close shot force ({closeForce:F2})");
        }

        [UnityTest]
        public IEnumerator ShotAim_RandomizationWithinGoalWidth()
        {
            yield return null;

            var evaluator = new ShotEvaluator();
            float goalCenterZ = 0f;
            float goalHalfWidth = 2.5f;
            float maxRange = goalHalfWidth * 0.8f; // 2.0

            int samples = 100;
            int outOfRange = 0;

            for (int i = 0; i < samples; i++)
            {
                float targetZ = evaluator.CalculateShotTargetZ(goalCenterZ, goalHalfWidth);
                if (Mathf.Abs(targetZ - goalCenterZ) > maxRange + 0.01f)
                {
                    outOfRange++;
                }
            }

            Assert.AreEqual(0, outOfRange,
                $"{outOfRange}/{samples} shot targets were outside ±{maxRange:F2}m of goal center");
        }

        // =========================================================================
        // Ball Bounce & Drag (3 tests)
        // =========================================================================

        [UnityTest]
        public IEnumerator BallDrop_FromHeight_BouncesWithRestitution()
        {
            float dropHeight = 3f;
            var ball = CreateTestBall(new Vector3(0, dropHeight, 5f), useGravity: true);
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = Vector3.zero;

            float timeout = 5f;
            float elapsed = 0f;
            bool hasHitGround = false;
            bool hasBouncedUp = false;
            float maxBounceHeight = 0f;

            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;

                if (!hasHitGround && ball.transform.position.y < 0.2f)
                    hasHitGround = true;

                if (hasHitGround && rb.linearVelocity.y > 0.1f)
                    hasBouncedUp = true;

                if (hasBouncedUp && rb.linearVelocity.y <= 0f)
                {
                    maxBounceHeight = ball.transform.position.y;
                    break;
                }

                yield return null;
            }

            float restitution = maxBounceHeight / dropHeight;
            Assert.That(restitution, Is.InRange(0.4f, 0.75f),
                $"Bounce restitution {restitution:F3} outside [0.4, 0.75]. " +
                $"Drop: {dropHeight}m, Bounce: {maxBounceHeight:F3}m");
        }

        [UnityTest]
        public IEnumerator BallRolling_DeceleratesMonotonically()
        {
            var ball = CreateTestBall(new Vector3(0, 0.15f, 5f), useGravity: true);
            var rb = ball.GetComponent<Rigidbody>();

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            rb.linearVelocity = new Vector3(15f, 0, 0);

            // Sample speed at 0.5s, 1.5s, 2.5s
            float[] sampleTimes = { 0.5f, 1.5f, 2.5f };
            float[] speeds = new float[sampleTimes.Length];
            int sampleIndex = 0;
            float elapsed = 0f;

            while (sampleIndex < sampleTimes.Length && elapsed < 4f)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= sampleTimes[sampleIndex])
                {
                    speeds[sampleIndex] = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
                    sampleIndex++;
                }
                yield return null;
            }

            Assert.AreEqual(sampleTimes.Length, sampleIndex,
                "Should have collected all speed samples");

            for (int i = 1; i < speeds.Length; i++)
            {
                Assert.LessOrEqual(speeds[i], speeds[i - 1] + 0.1f,
                    $"Speed at t={sampleTimes[i]:F1}s ({speeds[i]:F2}) should be <= " +
                    $"speed at t={sampleTimes[i - 1]:F1}s ({speeds[i - 1]:F2}). " +
                    $"All speeds: [{string.Join(", ", System.Array.ConvertAll(speeds, s => s.ToString("F2")))}]");
            }
        }

        [UnityTest]
        public IEnumerator HighVelocityShot_DoesNotTunnelThroughColliders()
        {
            // 50 m/s toward north wall — ContinuousDynamic should prevent tunneling
            var ball = CreateTestBall(new Vector3(0, 0.5f, 0), useGravity: false);
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(0, 0, 50f);

            float timeout = 3f;
            float elapsed = 0f;
            float halfWidth = _pitchConfig.HalfWidth;

            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.Less(Mathf.Abs(ball.transform.position.z), halfWidth + 3f,
                $"Ball at 50m/s should NOT tunnel through wall. " +
                $"Position: {ball.transform.position}, HalfWidth: {halfWidth}");
        }

        // =========================================================================
        // Blocking & Interception (3 tests)
        // =========================================================================

        [UnityTest]
        public IEnumerator LooseBall_NearestPlayerWithin1m_Claims()
        {
            yield return null;

            var ownership = new BallOwnershipLogic();
            float distanceToBall = 0.8f;
            float claimRadius = 1f;

            Assert.IsFalse(ownership.IsOwned,
                "Ball should start unowned");
            Assert.IsTrue(ownership.CanClaim(distanceToBall, claimRadius),
                $"Player at {distanceToBall}m should be able to claim (radius={claimRadius}m)");

            ownership.SetOwner(1);
            Assert.IsTrue(ownership.IsOwned,
                "Ball should be owned after SetOwner");
            Assert.AreEqual(1, ownership.CurrentOwnerId,
                $"Owner should be player 1 but was {ownership.CurrentOwnerId}");
        }

        [UnityTest]
        public IEnumerator TwoPlayersRaceToBall_NearestClaimsFirst()
        {
            yield return null;

            var ownership = new BallOwnershipLogic();
            float playerADistance = 0.5f;
            float playerBDistance = 2.0f;
            float claimRadius = 1f;

            bool canClaimA = ownership.CanClaim(playerADistance, claimRadius);
            bool canClaimB = ownership.CanClaim(playerBDistance, claimRadius);

            Assert.IsTrue(canClaimA,
                $"Player A at {playerADistance}m should be able to claim (radius={claimRadius}m)");
            Assert.IsFalse(canClaimB,
                $"Player B at {playerBDistance}m should NOT be able to claim (radius={claimRadius}m)");

            // Player A claims first
            ownership.SetOwner(1);
            Assert.AreEqual(1, ownership.CurrentOwnerId,
                "Player A (id=1) should own the ball");

            // Player B cannot claim because ball is already owned
            Assert.IsFalse(ownership.CanClaim(playerBDistance, claimRadius),
                "Player B should not be able to claim an already-owned ball");
        }

        [UnityTest]
        public IEnumerator Defender_BlocksLineOfSight_ShouldShootReturnsFalse()
        {
            yield return null;

            var evaluator = new ShotEvaluator(shootRange: 15f);

            // With clear line: should shoot
            bool shouldShootClear = evaluator.ShouldShoot(10f, hasClearLine: true, hasBallPossession: true);
            Assert.IsTrue(shouldShootClear,
                "Should shoot when in range, clear line, and has possession");

            // Without clear line (defender blocking): should not shoot
            bool shouldShootBlocked = evaluator.ShouldShoot(10f, hasClearLine: false, hasBallPossession: true);
            Assert.IsFalse(shouldShootBlocked,
                "Should NOT shoot when line is blocked by defender");

            // Without possession: should not shoot
            bool shouldShootNoPossession = evaluator.ShouldShoot(10f, hasClearLine: true, hasBallPossession: false);
            Assert.IsFalse(shouldShootNoPossession,
                "Should NOT shoot without ball possession");

            // Out of range: should not shoot
            bool shouldShootFar = evaluator.ShouldShoot(20f, hasClearLine: true, hasBallPossession: true);
            Assert.IsFalse(shouldShootFar,
                "Should NOT shoot when out of range (20m > 15m)");
        }

        // =========================================================================
        // Kick Mechanics (3 tests)
        // =========================================================================

        [UnityTest]
        public IEnumerator Kick_PassForce_Is8_ShootForce_Is15()
        {
            yield return null;

            var config = new KickConfigData();
            var logic = new KickLogic(config);

            // Prepare pass
            logic.PrepareKick(KickType.Pass, 0f, 0f, 1f, 0f);
            Assert.IsTrue(logic.HasPendingKick, "Should have pending kick after PrepareKick");
            Assert.AreEqual(8f, logic.PendingForce, 0.001f,
                $"Pass force should be 8 but was {logic.PendingForce}");

            var passResult = logic.ExecuteKick();
            Assert.IsTrue(passResult.Applied, "Pass kick should be applied");
            Assert.AreEqual(8f, passResult.Force, 0.001f,
                $"Pass result force should be 8 but was {passResult.Force}");
            Assert.AreEqual(KickType.Pass, passResult.Type,
                $"Kick type should be Pass but was {passResult.Type}");

            // Prepare shoot
            logic.PrepareKick(KickType.Shoot, 0f, 0f, 1f, 0f);
            Assert.AreEqual(15f, logic.PendingForce, 0.001f,
                $"Shoot force should be 15 but was {logic.PendingForce}");

            var shootResult = logic.ExecuteKick();
            Assert.IsTrue(shootResult.Applied, "Shoot kick should be applied");
            Assert.AreEqual(15f, shootResult.Force, 0.001f,
                $"Shoot result force should be 15 but was {shootResult.Force}");
            Assert.AreEqual(KickType.Shoot, shootResult.Type,
                $"Kick type should be Shoot but was {shootResult.Type}");
        }

        [UnityTest]
        public IEnumerator Kick_DirectionMatchesPlayerFacing()
        {
            yield return null;

            var config = new KickConfigData();
            var logic = new KickLogic(config);

            // Facing +X
            logic.PrepareKick(KickType.Pass, 0f, 0f, 1f, 0f);
            var result = logic.ExecuteKick();
            Assert.AreEqual(1f, result.DirectionX, 0.01f,
                $"Facing +X: DirectionX should be 1 but was {result.DirectionX}");
            Assert.AreEqual(0f, result.DirectionZ, 0.01f,
                $"Facing +X: DirectionZ should be 0 but was {result.DirectionZ}");

            // Facing 45 degrees (+X,+Z normalized)
            float diag = 1f / Mathf.Sqrt(2f);
            logic.PrepareKick(KickType.Pass, 0f, 0f, 1f, 1f);
            result = logic.ExecuteKick();
            Assert.AreEqual(diag, result.DirectionX, 0.02f,
                $"Facing 45°: DirectionX should be {diag:F3} but was {result.DirectionX}");
            Assert.AreEqual(diag, result.DirectionZ, 0.02f,
                $"Facing 45°: DirectionZ should be {diag:F3} but was {result.DirectionZ}");

            // Facing -X
            logic.PrepareKick(KickType.Shoot, 0f, 0f, -1f, 0f);
            result = logic.ExecuteKick();
            Assert.AreEqual(-1f, result.DirectionX, 0.01f,
                $"Facing -X: DirectionX should be -1 but was {result.DirectionX}");
            Assert.AreEqual(0f, result.DirectionZ, 0.01f,
                $"Facing -X: DirectionZ should be 0 but was {result.DirectionZ}");
        }

        [UnityTest]
        public IEnumerator PlayerKicks_OwnershipReleased()
        {
            yield return null;

            var ownership = new BallOwnershipLogic();

            // Set owner
            ownership.SetOwner(5);
            Assert.AreEqual(5, ownership.CurrentOwnerId,
                $"Owner should be 5 but was {ownership.CurrentOwnerId}");
            Assert.IsTrue(ownership.IsOwned, "Ball should be owned");

            // Release (simulating kick)
            ownership.Release();
            Assert.AreEqual(-1, ownership.CurrentOwnerId,
                $"After release, owner should be -1 but was {ownership.CurrentOwnerId}");
            Assert.IsFalse(ownership.IsOwned, "Ball should be unowned after release");

            // Transfer
            ownership.SetOwner(3);
            ownership.Transfer(7);
            Assert.AreEqual(7, ownership.CurrentOwnerId,
                $"After transfer, owner should be 7 but was {ownership.CurrentOwnerId}");
        }
    }
}
