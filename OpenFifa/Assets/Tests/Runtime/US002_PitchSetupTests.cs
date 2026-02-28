using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using OpenFifa.Gameplay;
using OpenFifa.Core;

namespace OpenFifa.Tests.Runtime
{
    [TestFixture]
    [Category("US002")]
    public class US002_PitchSetupTests
    {
        private GameObject _pitchRoot;
        private PitchBuilder _builder;
        private PitchConfigData _config;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _config = new PitchConfigData();
            _pitchRoot = new GameObject("PitchTestRoot");
            _builder = _pitchRoot.AddComponent<PitchBuilder>();
            _builder.BuildPitch(_config);
            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_pitchRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pitch_HasMeshRenderer_WithGreenMaterial()
        {
            yield return null;

            var pitch = GameObject.Find("Pitch");
            Assert.IsNotNull(pitch, "A GameObject named 'Pitch' should exist");

            var renderer = pitch.GetComponent<MeshRenderer>();
            Assert.IsNotNull(renderer, "Pitch should have a MeshRenderer component");
            Assert.IsNotNull(renderer.sharedMaterial, "Pitch MeshRenderer should have a material assigned");

            // Green material check - the material color should be greenish
            Color color = renderer.sharedMaterial.color;
            Assert.Greater(color.g, color.r,
                $"Pitch material should be green-ish. Color: R={color.r}, G={color.g}, B={color.b}");
        }

        [UnityTest]
        public IEnumerator Pitch_Dimensions_AreApproximately50x30()
        {
            yield return null;

            var pitch = GameObject.Find("Pitch");
            Assert.IsNotNull(pitch, "Pitch object should exist");

            var renderer = pitch.GetComponent<MeshRenderer>();
            Assert.IsNotNull(renderer, "Pitch should have a MeshRenderer");

            var bounds = renderer.bounds.size;
            Assert.AreEqual(50f, bounds.x, 2f,
                $"Pitch X dimension should be ~50m but was {bounds.x}m");
            Assert.AreEqual(30f, bounds.z, 2f,
                $"Pitch Z dimension should be ~30m but was {bounds.z}m");
        }

        [UnityTest]
        public IEnumerator BoundaryColliders_PreventBallFromExiting_NorthWall()
        {
            yield return null;

            var ball = CreateTestBall(Vector3.zero);
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(0, 0, 30f); // North

            // Wait for ball to hit boundary
            float timeout = 3f, elapsed = 0f;
            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float halfWidth = _config.PitchWidth / 2f;
            Assert.Less(ball.transform.position.z, halfWidth + 1f,
                $"Ball should be stopped by north boundary. Position.z = {ball.transform.position.z}, " +
                $"boundary at z={halfWidth}");

            Object.Destroy(ball);
        }

        [UnityTest]
        public IEnumerator BoundaryColliders_PreventBallFromExiting_SouthWall()
        {
            yield return null;

            var ball = CreateTestBall(Vector3.zero);
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(0, 0, -30f); // South

            float timeout = 3f, elapsed = 0f;
            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float halfWidth = _config.PitchWidth / 2f;
            Assert.Greater(ball.transform.position.z, -(halfWidth + 1f),
                $"Ball should be stopped by south boundary. Position.z = {ball.transform.position.z}, " +
                $"boundary at z=-{halfWidth}");

            Object.Destroy(ball);
        }

        [UnityTest]
        public IEnumerator BoundaryColliders_PreventBallFromExiting_EastWall()
        {
            // East wall has goal opening, so shoot toward the corner area
            yield return null;

            var ball = CreateTestBall(new Vector3(0, 0.5f, 10f)); // offset from center to hit wall, not goal
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(30f, 0, 0); // East

            float timeout = 3f, elapsed = 0f;
            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float halfLength = _config.PitchLength / 2f;
            Assert.Less(ball.transform.position.x, halfLength + 2f,
                $"Ball should be stopped by east boundary. Position.x = {ball.transform.position.x}, " +
                $"boundary at x={halfLength}");

            Object.Destroy(ball);
        }

        [UnityTest]
        public IEnumerator BoundaryColliders_PreventBallFromExiting_WestWall()
        {
            yield return null;

            var ball = CreateTestBall(new Vector3(0, 0.5f, 10f)); // offset from center to hit wall, not goal
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(-30f, 0, 0); // West

            float timeout = 3f, elapsed = 0f;
            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float halfLength = _config.PitchLength / 2f;
            Assert.Greater(ball.transform.position.x, -(halfLength + 2f),
                $"Ball should be stopped by west boundary. Position.x = {ball.transform.position.x}, " +
                $"boundary at x=-{halfLength}");

            Object.Destroy(ball);
        }

        [UnityTest]
        public IEnumerator CenterCircle_AfterBuild_ExistsAtPitchMidpoint()
        {
            yield return null;

            var centerCircle = GameObject.Find("CenterCircle");
            Assert.IsNotNull(centerCircle, "A GameObject named 'CenterCircle' should exist");

            Assert.AreEqual(0f, centerCircle.transform.position.x, 0.5f,
                $"Center circle X should be at midpoint (0), was {centerCircle.transform.position.x}");
            Assert.AreEqual(0f, centerCircle.transform.position.z, 0.5f,
                $"Center circle Z should be at midpoint (0), was {centerCircle.transform.position.z}");
        }

        [UnityTest]
        public IEnumerator GoalAreaMarkings_AfterBuild_ExistOnBothEnds()
        {
            yield return null;

            var goalAreaA = GameObject.Find("GoalAreaA");
            var goalAreaB = GameObject.Find("GoalAreaB");

            Assert.IsNotNull(goalAreaA, "GoalAreaA marking should exist");
            Assert.IsNotNull(goalAreaB, "GoalAreaB marking should exist");

            float halfLength = _config.PitchLength / 2f;
            Assert.AreEqual(halfLength, Mathf.Abs(goalAreaA.transform.position.x), 3f,
                $"GoalAreaA should be near pitch end. Position.x = {goalAreaA.transform.position.x}");
            Assert.AreEqual(halfLength, Mathf.Abs(goalAreaB.transform.position.x), 3f,
                $"GoalAreaB should be near pitch end. Position.x = {goalAreaB.transform.position.x}");
        }

        [UnityTest]
        public IEnumerator GoalOpenings_BallShotAtGoal_PassesThroughBoundary()
        {
            // A ball shot directly at the center of the goal opening should pass through
            yield return null;

            var ball = CreateTestBall(new Vector3(20f, 0.5f, 0f));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(20f, 0, 0); // Toward east goal

            float timeout = 3f, elapsed = 0f;
            float halfLength = _config.PitchLength / 2f;
            bool passedGoalLine = false;
            while (elapsed < timeout && !passedGoalLine)
            {
                if (ball.transform.position.x > halfLength)
                {
                    passedGoalLine = true;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(passedGoalLine,
                $"Ball should pass through goal opening. Final position.x = {ball.transform.position.x}, " +
                $"goal line at x={halfLength}");

            Object.Destroy(ball);
        }

        [UnityTest]
        public IEnumerator FourBoundaryColliders_AfterBuild_Exist()
        {
            yield return null;

            var boundaryNorth = GameObject.Find("BoundaryNorth");
            var boundarySouth = GameObject.Find("BoundarySouth");
            var boundaryEastUpper = GameObject.Find("BoundaryEastUpper");
            var boundaryWestUpper = GameObject.Find("BoundaryWestUpper");

            Assert.IsNotNull(boundaryNorth, "North boundary collider should exist");
            Assert.IsNotNull(boundarySouth, "South boundary collider should exist");
            Assert.IsNotNull(boundaryEastUpper, "East upper boundary collider should exist");
            Assert.IsNotNull(boundaryWestUpper, "West upper boundary collider should exist");
        }

        private GameObject CreateTestBall(Vector3 position)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "TestBall";
            ball.transform.position = position + Vector3.up * 0.5f;
            ball.transform.localScale = Vector3.one * 0.22f;

            var rb = ball.AddComponent<Rigidbody>();
            rb.mass = 0.43f;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.5f;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            return ball;
        }
    }
}
