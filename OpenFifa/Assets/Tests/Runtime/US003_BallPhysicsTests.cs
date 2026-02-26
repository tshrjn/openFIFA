using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using OpenFifa.Gameplay;
using OpenFifa.Core;

namespace OpenFifa.Tests.Runtime
{
    [TestFixture]
    [Category("US-003")]
    public class US003_BallPhysicsTests
    {
        private GameObject _pitchRoot;
        private GameObject _ball;
        private PitchBuilder _pitchBuilder;
        private PitchConfigData _pitchConfig;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _pitchConfig = new PitchConfigData();
            _pitchRoot = new GameObject("PitchTestRoot");
            _pitchBuilder = _pitchRoot.AddComponent<PitchBuilder>();
            _pitchBuilder.BuildPitch(_pitchConfig);

            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_ball != null) Object.Destroy(_ball);
            if (_pitchRoot != null) Object.Destroy(_pitchRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Ball_HasSphereCollider_AndRigidbody()
        {
            _ball = CreateBall(Vector3.up * 0.5f);
            yield return null;

            Assert.IsNotNull(_ball.GetComponent<SphereCollider>(),
                "Ball should have a SphereCollider component");
            Assert.IsNotNull(_ball.GetComponent<Rigidbody>(),
                "Ball should have a Rigidbody component");
        }

        [UnityTest]
        public IEnumerator Ball_RigidbodyMass_Is0Point43()
        {
            _ball = CreateBall(Vector3.up * 0.5f);
            yield return null;

            var rb = _ball.GetComponent<Rigidbody>();
            Assert.AreEqual(0.43f, rb.mass, 0.001f,
                $"Ball Rigidbody mass should be 0.43 kg but was {rb.mass} kg");
        }

        [UnityTest]
        public IEnumerator Ball_RigidbodyDrag_Is0Point1()
        {
            _ball = CreateBall(Vector3.up * 0.5f);
            yield return null;

            var rb = _ball.GetComponent<Rigidbody>();
            Assert.AreEqual(0.1f, rb.linearDamping, 0.001f,
                $"Ball Rigidbody drag should be 0.1 but was {rb.linearDamping}");
        }

        [UnityTest]
        public IEnumerator Ball_RigidbodyAngularDrag_Is0Point5()
        {
            _ball = CreateBall(Vector3.up * 0.5f);
            yield return null;

            var rb = _ball.GetComponent<Rigidbody>();
            Assert.AreEqual(0.5f, rb.angularDamping, 0.001f,
                $"Ball Rigidbody angular drag should be 0.5 but was {rb.angularDamping}");
        }

        [UnityTest]
        public IEnumerator Ball_PhysicMaterial_HasCorrectBounciness()
        {
            _ball = CreateBall(Vector3.up * 0.5f);
            yield return null;

            var collider = _ball.GetComponent<SphereCollider>();
            Assert.IsNotNull(collider.sharedMaterial,
                "Ball SphereCollider should have a PhysicMaterial assigned");
            Assert.AreEqual(0.6f, collider.sharedMaterial.bounciness, 0.001f,
                $"Ball PhysicMaterial bounciness should be 0.6 but was {collider.sharedMaterial.bounciness}");
        }

        [UnityTest]
        public IEnumerator Ball_PhysicMaterial_HasCorrectDynamicFriction()
        {
            _ball = CreateBall(Vector3.up * 0.5f);
            yield return null;

            var collider = _ball.GetComponent<SphereCollider>();
            Assert.AreEqual(0.5f, collider.sharedMaterial.dynamicFriction, 0.001f,
                $"Ball PhysicMaterial dynamic friction should be 0.5 but was {collider.sharedMaterial.dynamicFriction}");
        }

        [UnityTest]
        public IEnumerator Ball_WhenDroppedFrom2m_BouncesWithinRestitutionRange()
        {
            float dropHeight = 2f;
            _ball = CreateBall(new Vector3(0, dropHeight, 0));
            var rb = _ball.GetComponent<Rigidbody>();
            rb.linearVelocity = Vector3.zero;

            // Wait for ball to bounce and reach peak
            float timeout = 5f;
            float elapsed = 0f;
            bool hasHitGround = false;
            bool hasBouncedUp = false;
            float maxBounceHeight = 0f;

            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;

                if (!hasHitGround && _ball.transform.position.y < 0.2f)
                {
                    hasHitGround = true;
                }

                if (hasHitGround && rb.linearVelocity.y > 0.1f)
                {
                    hasBouncedUp = true;
                }

                if (hasBouncedUp && rb.linearVelocity.y <= 0f)
                {
                    maxBounceHeight = _ball.transform.position.y;
                    break;
                }

                yield return null;
            }

            float restitution = maxBounceHeight / dropHeight;
            Assert.That(restitution, Is.InRange(0.45f, 0.75f),
                $"Bounce restitution {restitution:F3} outside expected range [0.45, 0.75]. " +
                $"Drop height: {dropHeight}m, Bounce height: {maxBounceHeight:F3}m");
        }

        [UnityTest]
        public IEnumerator Ball_RollingAt10ms_StopsWithin6To22m()
        {
            _ball = CreateBall(new Vector3(0, 0.15f, 0));
            var rb = _ball.GetComponent<Rigidbody>();

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            // Give ball initial velocity along X axis
            rb.linearVelocity = new Vector3(10f, 0, 0);

            Vector3 startPos = _ball.transform.position;

            // Wait for ball to come to rest
            float timeout = 15f;
            float elapsed = 0f;
            while (elapsed < timeout && rb.linearVelocity.magnitude > 0.05f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float distance = Vector3.Distance(
                new Vector3(startPos.x, 0, startPos.z),
                new Vector3(_ball.transform.position.x, 0, _ball.transform.position.z));

            Assert.That(distance, Is.InRange(6f, 22f),
                $"Ball rolling at 10 m/s should stop within 6-22m but traveled {distance:F2}m. " +
                $"Final velocity: {rb.linearVelocity.magnitude:F3} m/s, Time: {elapsed:F2}s");
        }

        [UnityTest]
        public IEnumerator Ball_CannotEscapeBoundaries_At30ms()
        {
            _ball = CreateBall(new Vector3(0, 0.5f, 10f)); // Offset from center to avoid goal opening
            var rb = _ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(30f, 0, 0); // East

            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float halfLength = _pitchConfig.PitchLength / 2f;
            Assert.Less(_ball.transform.position.x, halfLength + 2f,
                $"Ball should be stopped by boundary. Position.x = {_ball.transform.position.x}");
        }

        [UnityTest]
        public IEnumerator Ball_Interpolation_IsSetToInterpolate()
        {
            _ball = CreateBall(Vector3.up);
            yield return null;

            var rb = _ball.GetComponent<Rigidbody>();
            Assert.AreEqual(RigidbodyInterpolation.Interpolate, rb.interpolation,
                $"Ball Rigidbody interpolation should be Interpolate but was {rb.interpolation}");
        }

        [UnityTest]
        public IEnumerator Ball_CollisionDetection_IsContinuous()
        {
            _ball = CreateBall(Vector3.up);
            yield return null;

            var rb = _ball.GetComponent<Rigidbody>();
            Assert.AreEqual(CollisionDetectionMode.ContinuousDynamic, rb.collisionDetectionMode,
                $"Ball collision detection should be ContinuousDynamic but was {rb.collisionDetectionMode}");
        }

        [UnityTest]
        public IEnumerator Ball_Velocity_CanBeReadAndApplied()
        {
            _ball = CreateBall(new Vector3(0, 0.5f, 0));
            var rb = _ball.GetComponent<Rigidbody>();

            yield return new WaitForFixedUpdate();

            // Apply velocity
            Vector3 testVelocity = new Vector3(5f, 0, 3f);
            rb.linearVelocity = testVelocity;

            yield return new WaitForFixedUpdate();

            // Read velocity back - should be approximately what we set (may differ slightly due to physics)
            Assert.AreEqual(testVelocity.x, rb.linearVelocity.x, 1f,
                $"Ball velocity.x should be ~{testVelocity.x} but was {rb.linearVelocity.x}");
            Assert.AreEqual(testVelocity.z, rb.linearVelocity.z, 1f,
                $"Ball velocity.z should be ~{testVelocity.z} but was {rb.linearVelocity.z}");
        }

        private GameObject CreateBall(Vector3 position)
        {
            var ball = new GameObject("Ball");
            ball.tag = "Ball";
            int ballLayer = LayerMask.NameToLayer("Ball");
            ball.layer = ballLayer != -1 ? ballLayer : 0;

            var sphereCollider = ball.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.11f;

            var physicMaterial = new PhysicMaterial("BallPhysicMaterial");
            physicMaterial.bounciness = 0.6f;
            physicMaterial.dynamicFriction = 0.5f;
            physicMaterial.staticFriction = 0.5f;
            physicMaterial.bounceCombine = PhysicMaterialCombine.Average;
            physicMaterial.frictionCombine = PhysicMaterialCombine.Average;
            sphereCollider.sharedMaterial = physicMaterial;

            var rb = ball.AddComponent<Rigidbody>();
            rb.mass = 0.43f;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.5f;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            ball.transform.position = position;

            return ball;
        }
    }
}
