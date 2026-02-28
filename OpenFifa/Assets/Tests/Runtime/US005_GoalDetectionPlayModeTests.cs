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
    [Category("US005")]
    public class US005_GoalDetectionPlayModeTests
    {
        private GameObject _pitchRoot;
        private PitchBuilder _pitchBuilder;
        private PitchConfigData _pitchConfig;
        private GameObject _goalDetectorA;
        private GameObject _goalDetectorB;
        private GoalDetector _detectorA;
        private GoalDetector _detectorB;
        private List<TeamIdentifier> _goalEvents;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _pitchConfig = new PitchConfigData();
            _pitchRoot = new GameObject("PitchTestRoot");
            _pitchBuilder = _pitchRoot.AddComponent<PitchBuilder>();
            _pitchBuilder.BuildPitch(_pitchConfig);

            // Create goal detectors
            float halfLength = _pitchConfig.HalfLength;
            float goalHalfWidth = _pitchConfig.GoalOpeningHalfWidth;
            float goalHeight = _pitchConfig.GoalHeight;
            float triggerDepth = 1f;

            // Goal A (east end - positive X) - TeamA scores here
            _goalDetectorA = CreateGoalTrigger("GoalTriggerA",
                new Vector3(halfLength + triggerDepth / 2f, goalHeight / 2f, 0),
                new Vector3(triggerDepth, goalHeight, _pitchConfig.GoalWidth),
                TeamIdentifier.TeamA);
            _detectorA = _goalDetectorA.GetComponent<GoalDetector>();

            // Goal B (west end - negative X) - TeamB scores here
            _goalDetectorB = CreateGoalTrigger("GoalTriggerB",
                new Vector3(-(halfLength + triggerDepth / 2f), goalHeight / 2f, 0),
                new Vector3(triggerDepth, goalHeight, _pitchConfig.GoalWidth),
                TeamIdentifier.TeamB);
            _detectorB = _goalDetectorB.GetComponent<GoalDetector>();

            _goalEvents = new List<TeamIdentifier>();
            GoalDetector.OnGoalScored += HandleGoalScored;

            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GoalDetector.OnGoalScored -= HandleGoalScored;
            if (_goalDetectorA != null) Object.Destroy(_goalDetectorA);
            if (_goalDetectorB != null) Object.Destroy(_goalDetectorB);
            if (_pitchRoot != null) Object.Destroy(_pitchRoot);
            yield return null;
        }

        private void HandleGoalScored(TeamIdentifier team)
        {
            _goalEvents.Add(team);
        }

        [UnityTest]
        public IEnumerator GoalTrigger_IsTrigger_NotPhysicalBlocker()
        {
            yield return null;

            var colliderA = _goalDetectorA.GetComponent<BoxCollider>();
            Assert.IsTrue(colliderA.isTrigger,
                "Goal trigger A should have isTrigger = true");

            var colliderB = _goalDetectorB.GetComponent<BoxCollider>();
            Assert.IsTrue(colliderB.isTrigger,
                "Goal trigger B should have isTrigger = true");
        }

        [UnityTest]
        public IEnumerator GoalTrigger_ColliderSize_SpansFullGoalWidthAndHeight()
        {
            yield return null;

            var colliderA = _goalDetectorA.GetComponent<BoxCollider>();
            Assert.AreEqual(_pitchConfig.GoalWidth, colliderA.size.z, 0.5f,
                $"Goal trigger A width should be ~{_pitchConfig.GoalWidth}m but was {colliderA.size.z}m");
            Assert.AreEqual(_pitchConfig.GoalHeight, colliderA.size.y, 0.5f,
                $"Goal trigger A height should be ~{_pitchConfig.GoalHeight}m but was {colliderA.size.y}m");
        }

        [UnityTest]
        public IEnumerator Ball_OnGoalLine_DoesNotTriggerGoal()
        {
            // Place ball exactly on the goal line (not past it)
            float halfLength = _pitchConfig.HalfLength;
            var ball = CreateTestBall(new Vector3(halfLength, 0.5f, 0));

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(0, _goalEvents.Count,
                $"Ball on goal line should NOT trigger a goal. Events: {_goalEvents.Count}");

            Object.Destroy(ball);
        }

        [UnityTest]
        public IEnumerator Ball_MovedPastGoalLine_TriggersExactlyOneGoal()
        {
            // Launch ball toward east goal
            var ball = CreateTestBall(new Vector3(20f, 0.5f, 0));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(20f, 0, 0);

            // Wait for goal detection
            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout && _goalEvents.Count == 0)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(1, _goalEvents.Count,
                $"Ball past goal line should trigger exactly one goal event. Events: {_goalEvents.Count}");

            Object.Destroy(ball);
        }

        [UnityTest]
        public IEnumerator GoalEvent_ContainsCorrectTeam_WhenScoringOnEastGoal()
        {
            var ball = CreateTestBall(new Vector3(20f, 0.5f, 0));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(20f, 0, 0);

            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout && _goalEvents.Count == 0)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.Greater(_goalEvents.Count, 0,
                "Should have received at least one goal event");
            Assert.AreEqual(TeamIdentifier.TeamA, _goalEvents[0],
                $"Scoring on east goal should be TeamA goal but got {_goalEvents[0]}");

            Object.Destroy(ball);
        }

        [UnityTest]
        public IEnumerator GoalEvent_ContainsCorrectTeam_WhenScoringOnWestGoal()
        {
            var ball = CreateTestBall(new Vector3(-20f, 0.5f, 0));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(-20f, 0, 0);

            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout && _goalEvents.Count == 0)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.Greater(_goalEvents.Count, 0,
                "Should have received at least one goal event");
            Assert.AreEqual(TeamIdentifier.TeamB, _goalEvents[0],
                $"Scoring on west goal should be TeamB goal but got {_goalEvents[0]}");

            Object.Destroy(ball);
        }

        [UnityTest]
        public IEnumerator GoalDetector_OnGoalScored_ExposesPublicEvent()
        {
            yield return null;

            // Verify the static event exists and can be subscribed to
            bool eventExists = true;
            try
            {
                System.Action<TeamIdentifier> handler = (team) => { };
                GoalDetector.OnGoalScored += handler;
                GoalDetector.OnGoalScored -= handler;
            }
            catch
            {
                eventExists = false;
            }

            Assert.IsTrue(eventExists,
                "GoalDetector should expose a public static OnGoalScored event");
        }

        [UnityTest]
        public IEnumerator Ball_AfterGoal_IsRepositionedToCenter()
        {
            var ball = CreateTestBall(new Vector3(20f, 0.5f, 0));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(20f, 0, 0);

            // Assign ball reference to detectors
            _detectorA.SetBallReference(ball.transform);
            _detectorB.SetBallReference(ball.transform);

            // Wait for goal detection
            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout && _goalEvents.Count == 0)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Wait for ball reset (up to 3 seconds after goal)
            float resetTimeout = 4f;
            elapsed = 0f;
            while (elapsed < resetTimeout)
            {
                elapsed += Time.deltaTime;
                if (Mathf.Abs(ball.transform.position.x) < 1f &&
                    Mathf.Abs(ball.transform.position.z) < 1f)
                {
                    break;
                }
                yield return null;
            }

            Assert.AreEqual(0f, ball.transform.position.x, 2f,
                $"Ball should be repositioned to center after goal. Position: {ball.transform.position}");
            Assert.AreEqual(0f, ball.transform.position.z, 2f,
                $"Ball should be repositioned to center after goal. Position: {ball.transform.position}");

            Object.Destroy(ball);
        }

        private GameObject CreateGoalTrigger(string name, Vector3 position, Vector3 size, TeamIdentifier scoringTeam)
        {
            var trigger = new GameObject(name);
            trigger.transform.SetParent(_pitchRoot.transform);
            trigger.transform.localPosition = position;
            trigger.tag = "GoalTrigger";

            int goalLayer = LayerMask.NameToLayer("GoalTrigger");
            trigger.layer = goalLayer != -1 ? goalLayer : 0;

            var collider = trigger.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = true;

            var detector = trigger.AddComponent<GoalDetector>();
            detector.Initialize(scoringTeam);

            return trigger;
        }

        private GameObject CreateTestBall(Vector3 position)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            ball.tag = "Ball";
            ball.transform.position = position;
            ball.transform.localScale = Vector3.one * 0.22f;

            int ballLayer = LayerMask.NameToLayer("Ball");
            ball.layer = ballLayer != -1 ? ballLayer : 0;

            var rb = ball.AddComponent<Rigidbody>();
            rb.mass = 0.43f;
            rb.linearDamping = 0.1f;
            rb.useGravity = false; // Disable gravity for deterministic tests
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            return ball;
        }
    }
}
