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
    [Category("GameplayTrajectory")]
    public class GameplayTrajectoryTests
    {
        private GameObject _pitchRoot;
        private PitchBuilder _pitchBuilder;
        private PitchConfigData _pitchConfig;
        private GameObject _goalDetectorEast;
        private GameObject _goalDetectorWest;
        private GoalDetector _detectorEast;
        private GoalDetector _detectorWest;
        private List<TeamIdentifier> _goalEvents;
        private List<GameObject> _spawnedObjects;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _pitchConfig = new PitchConfigData();
            _pitchRoot = new GameObject("PitchTestRoot");
            _pitchBuilder = _pitchRoot.AddComponent<PitchBuilder>();
            _pitchBuilder.BuildPitch(_pitchConfig);

            float halfLength = _pitchConfig.HalfLength;
            float goalHalfWidth = _pitchConfig.GoalOpeningHalfWidth;
            float goalHeight = _pitchConfig.GoalHeight;
            float triggerDepth = 1f;

            // East goal — TeamA scores here (ball moving +X)
            _goalDetectorEast = CreateGoalTrigger("GoalTriggerEast",
                new Vector3(halfLength + triggerDepth / 2f, goalHeight / 2f, 0),
                new Vector3(triggerDepth, goalHeight, _pitchConfig.GoalWidth),
                TeamIdentifier.TeamA);
            _detectorEast = _goalDetectorEast.GetComponent<GoalDetector>();

            // West goal — TeamB scores here (ball moving -X)
            _goalDetectorWest = CreateGoalTrigger("GoalTriggerWest",
                new Vector3(-(halfLength + triggerDepth / 2f), goalHeight / 2f, 0),
                new Vector3(triggerDepth, goalHeight, _pitchConfig.GoalWidth),
                TeamIdentifier.TeamB);
            _detectorWest = _goalDetectorWest.GetComponent<GoalDetector>();

            _goalEvents = new List<TeamIdentifier>();
            _spawnedObjects = new List<GameObject>();
            GoalDetector.OnGoalScored += HandleGoalScored;

            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GoalDetector.OnGoalScored -= HandleGoalScored;
            foreach (var obj in _spawnedObjects)
            {
                if (obj != null) Object.Destroy(obj);
            }
            if (_goalDetectorEast != null) Object.Destroy(_goalDetectorEast);
            if (_goalDetectorWest != null) Object.Destroy(_goalDetectorWest);
            if (_pitchRoot != null) Object.Destroy(_pitchRoot);
            yield return null;
        }

        private void HandleGoalScored(TeamIdentifier team)
        {
            _goalEvents.Add(team);
        }

        #region Helpers

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

        private GameObject CreateTestBall(Vector3 position, bool useGravity = false)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            ball.tag = "Ball";
            ball.transform.position = position;
            ball.transform.localScale = Vector3.one * 0.22f;

            int ballLayer = LayerMask.NameToLayer("Ball");
            ball.layer = ballLayer != -1 ? ballLayer : 0;

            // Replace the default collider with a properly sized one
            var existingCollider = ball.GetComponent<SphereCollider>();
            if (existingCollider != null) Object.DestroyImmediate(existingCollider);

            var sphereCollider = ball.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.5f; // local space (0.5 * 0.22 scale = 0.11 world)

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
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

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

            var identity = player.AddComponent<PlayerIdentity>();
            identity.Configure(id, team, $"Player{id}");

            _spawnedObjects.Add(player);
            return player;
        }

        private IEnumerator WaitForCondition(System.Func<bool> condition, float timeout, string description)
        {
            float elapsed = 0f;
            while (elapsed < timeout && !condition())
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        #endregion

        // =========================================================================
        // Goal Scoring Tests (8)
        // =========================================================================

        [UnityTest]
        public IEnumerator DirectShot_FromCenter_ScoresInEastGoal()
        {
            var ball = CreateTestBall(new Vector3(0, 0.5f, 0));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(25f, 0, 0);

            float timeout = 5f;
            float elapsed = 0f;
            while (elapsed < timeout && _goalEvents.Count == 0)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(1, _goalEvents.Count,
                $"Direct shot from center toward east goal should trigger 1 goal. Events: {_goalEvents.Count}, " +
                $"Ball position: {ball.transform.position}, Velocity: {rb.linearVelocity}");
            Assert.AreEqual(TeamIdentifier.TeamA, _goalEvents[0],
                $"East goal should credit TeamA but got {_goalEvents[0]}");
        }

        [UnityTest]
        public IEnumerator DirectShot_FromCenter_ScoresInWestGoal()
        {
            var ball = CreateTestBall(new Vector3(0, 0.5f, 0));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(-25f, 0, 0);

            float timeout = 5f;
            float elapsed = 0f;
            while (elapsed < timeout && _goalEvents.Count == 0)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(1, _goalEvents.Count,
                $"Direct shot from center toward west goal should trigger 1 goal. Events: {_goalEvents.Count}, " +
                $"Ball position: {ball.transform.position}, Velocity: {rb.linearVelocity}");
            Assert.AreEqual(TeamIdentifier.TeamB, _goalEvents[0],
                $"West goal should credit TeamB but got {_goalEvents[0]}");
        }

        [UnityTest]
        public IEnumerator AngledShot_FromWing_ScoresInEastGoal()
        {
            // From right wing position, aim toward east goal center
            float startX = 15f;
            float startZ = 8f;
            float targetX = _pitchConfig.HalfLength;
            float targetZ = 0f;

            float dx = targetX - startX;
            float dz = targetZ - startZ;
            float mag = Mathf.Sqrt(dx * dx + dz * dz);
            var direction = new Vector3(dx / mag, 0, dz / mag);

            var ball = CreateTestBall(new Vector3(startX, 0.5f, startZ));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = direction * 20f;

            float timeout = 5f;
            float elapsed = 0f;
            while (elapsed < timeout && _goalEvents.Count == 0)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(1, _goalEvents.Count,
                $"Angled shot from wing ({startX},{startZ}) should score. Events: {_goalEvents.Count}, " +
                $"Ball position: {ball.transform.position}");
            Assert.AreEqual(TeamIdentifier.TeamA, _goalEvents[0],
                $"East goal should credit TeamA but got {_goalEvents[0]}");
        }

        [UnityTest]
        public IEnumerator Shot_HitsOutsideGoal_NoGoalScored()
        {
            // Aim ball at z offset well outside goal opening (goalWidth=5, halfWidth=2.5)
            float targetZ = 6f; // Far outside goal width
            var ball = CreateTestBall(new Vector3(15f, 0.5f, targetZ));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(20f, 0, 0);

            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(0, _goalEvents.Count,
                $"Ball aimed outside goal width (z={targetZ}) should not score. Events: {_goalEvents.Count}, " +
                $"Ball position: {ball.transform.position}");
        }

        [UnityTest]
        public IEnumerator Ball_OnGoalLine_Stationary_DoesNotTriggerGoal()
        {
            // Place ball just inside pitch at goal line — should NOT overlap trigger
            float halfLength = _pitchConfig.HalfLength;
            var ball = CreateTestBall(new Vector3(halfLength - 1f, 0.5f, 0));

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(0, _goalEvents.Count,
                $"Stationary ball on goal line should NOT trigger goal. Events: {_goalEvents.Count}, " +
                $"Ball position: {ball.transform.position}");
        }

        [UnityTest]
        public IEnumerator Ball_JustPastGoalLine_TriggersGoal()
        {
            // Launch ball from near the goal line so it crosses quickly
            var ball = CreateTestBall(new Vector3(22f, 0.5f, 0));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(15f, 0, 0);

            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout && _goalEvents.Count == 0)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(1, _goalEvents.Count,
                $"Ball crossing goal line should trigger exactly one goal. Events: {_goalEvents.Count}, " +
                $"Ball position: {ball.transform.position}");
        }

        [UnityTest]
        public IEnumerator GoalScored_BallResetsToCenter_After2Seconds()
        {
            var ball = CreateTestBall(new Vector3(20f, 0.5f, 0));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(20f, 0, 0);

            _detectorEast.SetBallReference(ball.transform);
            _detectorWest.SetBallReference(ball.transform);

            // Wait for goal
            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout && _goalEvents.Count == 0)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.Greater(_goalEvents.Count, 0, "Goal should have been scored first");

            // Wait for ball reset (up to 4s after goal)
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
                $"Ball should reset to center X after goal. Position: {ball.transform.position}");
            Assert.AreEqual(0f, ball.transform.position.z, 2f,
                $"Ball should reset to center Z after goal. Position: {ball.transform.position}");
        }

        [UnityTest]
        public IEnumerator TwoGoals_BothCounted()
        {
            // First goal: east
            var ball1 = CreateTestBall(new Vector3(20f, 0.5f, 0));
            var rb1 = ball1.GetComponent<Rigidbody>();
            rb1.linearVelocity = new Vector3(20f, 0, 0);

            _detectorEast.SetBallReference(ball1.transform);

            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout && _goalEvents.Count == 0)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(1, _goalEvents.Count, "First goal should register");

            // Wait for reset
            yield return new WaitForSeconds(3f);
            _detectorEast.ResetDetection();
            _detectorWest.ResetDetection();

            // Destroy first ball, create second for west goal
            Object.Destroy(ball1);
            _spawnedObjects.Remove(ball1);
            yield return new WaitForFixedUpdate();

            var ball2 = CreateTestBall(new Vector3(-20f, 0.5f, 0));
            var rb2 = ball2.GetComponent<Rigidbody>();
            rb2.linearVelocity = new Vector3(-20f, 0, 0);

            _detectorWest.SetBallReference(ball2.transform);

            elapsed = 0f;
            while (elapsed < timeout && _goalEvents.Count < 2)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(2, _goalEvents.Count,
                $"Should have 2 goal events but got {_goalEvents.Count}");
            Assert.AreEqual(TeamIdentifier.TeamA, _goalEvents[0],
                $"First goal should be TeamA but was {_goalEvents[0]}");
            Assert.AreEqual(TeamIdentifier.TeamB, _goalEvents[1],
                $"Second goal should be TeamB but was {_goalEvents[1]}");
        }

        // =========================================================================
        // Boundary Behavior Tests (5)
        // =========================================================================

        [UnityTest]
        public IEnumerator Ball_KickedTowardNorthWall_BouncesBack()
        {
            // Place ball away from goals (center X, low Z offset)
            var ball = CreateTestBall(new Vector3(0, 0.5f, 5f));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(0, 0, 20f); // Toward north wall (+Z)

            float timeout = 3f;
            float elapsed = 0f;
            bool bounced = false;
            while (elapsed < timeout && !bounced)
            {
                elapsed += Time.deltaTime;
                if (rb.linearVelocity.z < -0.5f) // Velocity flipped
                {
                    bounced = true;
                }
                yield return null;
            }

            Assert.IsTrue(bounced,
                $"Ball should bounce off north wall. Final velocity: {rb.linearVelocity}, " +
                $"Position: {ball.transform.position}");
        }

        [UnityTest]
        public IEnumerator Ball_KickedTowardSouthWall_BouncesBack()
        {
            var ball = CreateTestBall(new Vector3(0, 0.5f, -5f));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(0, 0, -20f); // Toward south wall (-Z)

            float timeout = 3f;
            float elapsed = 0f;
            bool bounced = false;
            while (elapsed < timeout && !bounced)
            {
                elapsed += Time.deltaTime;
                if (rb.linearVelocity.z > 0.5f) // Velocity flipped
                {
                    bounced = true;
                }
                yield return null;
            }

            Assert.IsTrue(bounced,
                $"Ball should bounce off south wall. Final velocity: {rb.linearVelocity}, " +
                $"Position: {ball.transform.position}");
        }

        [UnityTest]
        public IEnumerator Ball_KickedHardAtCorner_StaysInPlay()
        {
            // Diagonal velocity toward NE corner
            var ball = CreateTestBall(new Vector3(10f, 0.5f, 5f));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(20f, 0, 20f);

            float timeout = 3f;
            float elapsed = 0f;
            float halfLength = _pitchConfig.HalfLength;
            float halfWidth = _pitchConfig.HalfWidth;
            float margin = 3f; // Allow some margin for wall thickness

            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.Less(Mathf.Abs(ball.transform.position.x), halfLength + margin,
                $"Ball should stay within X bounds. Position: {ball.transform.position}");
            Assert.Less(Mathf.Abs(ball.transform.position.z), halfWidth + margin,
                $"Ball should stay within Z bounds. Position: {ball.transform.position}");
        }

        [UnityTest]
        public IEnumerator Ball_MissesGoal_HitsBackWall()
        {
            // Aim at z=6 which is well outside goal half-width (2.5m)
            var ball = CreateTestBall(new Vector3(15f, 0.5f, 6f));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(20f, 0, 0);

            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(0, _goalEvents.Count,
                $"Ball missing goal (z=6) should not score. Events: {_goalEvents.Count}");

            float halfLength = _pitchConfig.HalfLength;
            Assert.Less(ball.transform.position.x, halfLength + 3f,
                $"Ball should be stopped by boundary. Position: {ball.transform.position}");
        }

        [UnityTest]
        public IEnumerator Ball_InsideGoalArea_StoppedByBackWall()
        {
            // Ball enters goal area at center (within goal opening) but should be stopped by back wall
            var ball = CreateTestBall(new Vector3(20f, 0.5f, 0));
            var rb = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = new Vector3(30f, 0, 0);

            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float halfLength = _pitchConfig.HalfLength;
            float backWallLimit = halfLength + _pitchConfig.GoalAreaDepth + 3f;
            Assert.Less(ball.transform.position.x, backWallLimit,
                $"Ball in goal area should be stopped by back wall. " +
                $"Position: {ball.transform.position}, Limit: {backWallLimit}");
        }

        // =========================================================================
        // Match Flow Tests (5)
        // =========================================================================

        [UnityTest]
        public IEnumerator GoalScored_CorrectTeamCredited_InMatchScore()
        {
            var score = new MatchScore();

            // Wire up MatchScore to GoalDetector events via local handler
            System.Action<TeamIdentifier> scoreHandler = (team) => score.AddGoal(team);
            GoalDetector.OnGoalScored += scoreHandler;

            // Score a goal for TeamA (east)
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

            GoalDetector.OnGoalScored -= scoreHandler;

            Assert.AreEqual(1, score.ScoreA,
                $"TeamA score should be 1 but was {score.ScoreA}");
            Assert.AreEqual(0, score.ScoreB,
                $"TeamB score should be 0 but was {score.ScoreB}");
        }

        [UnityTest]
        public IEnumerator HalfTime_TransitionOccurs()
        {
            yield return null;

            var timer = new MatchTimer(0.5f); // Very short half
            timer.StartMatch();

            Assert.AreEqual(MatchPeriod.FirstHalf, timer.CurrentPeriod,
                $"Timer should start at FirstHalf but was {timer.CurrentPeriod}");

            // Tick past the half duration
            timer.Tick(0.6f);

            Assert.AreEqual(MatchPeriod.HalfTime, timer.CurrentPeriod,
                $"Timer should transition to HalfTime but was {timer.CurrentPeriod}");
        }

        [UnityTest]
        public IEnumerator FullTime_MatchEnds()
        {
            yield return null;

            var timer = new MatchTimer(0.5f);
            timer.StartMatch();

            // Tick through first half
            timer.Tick(0.6f);
            Assert.AreEqual(MatchPeriod.HalfTime, timer.CurrentPeriod);

            // Start second half
            timer.StartSecondHalf();
            Assert.AreEqual(MatchPeriod.SecondHalf, timer.CurrentPeriod);

            // Tick through second half
            timer.Tick(0.6f);
            Assert.AreEqual(MatchPeriod.FullTime, timer.CurrentPeriod,
                $"Timer should be FullTime after both halves but was {timer.CurrentPeriod}");
            Assert.IsTrue(timer.IsMatchOver,
                "IsMatchOver should be true after full time");
        }

        [UnityTest]
        public IEnumerator KickoffLogic_AlternatesAfterGoal()
        {
            yield return null;

            var kickoff = new KickoffLogic(TeamIdentifier.TeamA);
            Assert.AreEqual(TeamIdentifier.TeamA, kickoff.KickingTeam,
                "Initial kicking team should be TeamA");

            // TeamA scores — TeamB gets next kickoff
            kickoff.OnGoalScored(TeamIdentifier.TeamA);
            Assert.AreEqual(TeamIdentifier.TeamB, kickoff.KickingTeam,
                $"After TeamA scores, kicking team should be TeamB but was {kickoff.KickingTeam}");

            // TeamB scores — TeamA gets next kickoff
            kickoff.OnGoalScored(TeamIdentifier.TeamB);
            Assert.AreEqual(TeamIdentifier.TeamA, kickoff.KickingTeam,
                $"After TeamB scores, kicking team should be TeamA but was {kickoff.KickingTeam}");
        }

        [UnityTest]
        public IEnumerator ScoreDisplay_UpdatesAfterGoal()
        {
            yield return null;

            var score = new MatchScore();
            Assert.AreEqual("0 - 0", score.GetScoreDisplay(),
                $"Initial score display should be '0 - 0' but was '{score.GetScoreDisplay()}'");

            score.AddGoal(TeamIdentifier.TeamA);
            Assert.AreEqual("1 - 0", score.GetScoreDisplay(),
                $"After TeamA goal, display should be '1 - 0' but was '{score.GetScoreDisplay()}'");

            score.AddGoal(TeamIdentifier.TeamB);
            Assert.AreEqual("1 - 1", score.GetScoreDisplay(),
                $"After TeamB goal, display should be '1 - 1' but was '{score.GetScoreDisplay()}'");

            score.AddGoal(TeamIdentifier.TeamA);
            Assert.AreEqual("2 - 1", score.GetScoreDisplay(),
                $"After second TeamA goal, display should be '2 - 1' but was '{score.GetScoreDisplay()}'");
        }
    }
}
