using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using OpenFifa.Gameplay;
using OpenFifa.Core;
using TMPro;

namespace OpenFifa.Tests.Runtime
{
    [TestFixture]
    [Category("Integration")]
    public class MatchOrchestratorIntegrationTests
    {
        private MatchOrchestrator _orchestrator;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Create a fresh MatchOrchestrator — it builds everything in Awake/Start
            var go = new GameObject("MatchOrchestrator");
            _orchestrator = go.AddComponent<MatchOrchestrator>();

            // Wait for Awake + Start + first physics step
            yield return new WaitForFixedUpdate();
            yield return null; // extra frame for Start()
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Clean up all created objects
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var obj in allObjects)
            {
                if (obj != null && obj.scene.IsValid())
                    Object.Destroy(obj);
            }
            yield return null;
        }

        // === SCENE BOOTSTRAP TESTS ===

        [UnityTest]
        public IEnumerator Bootstrap_PitchRoot_Exists()
        {
            yield return null;
            var pitch = GameObject.Find("PitchRoot");
            Assert.IsNotNull(pitch, "PitchRoot should be created by MatchOrchestrator");
        }

        [UnityTest]
        public IEnumerator Bootstrap_Ball_Exists()
        {
            yield return null;
            var ball = GameObject.Find("Ball");
            Assert.IsNotNull(ball, "Ball should be created by MatchOrchestrator");
        }

        [UnityTest]
        public IEnumerator Bootstrap_GoalFrames_Exist()
        {
            yield return null;
            var goalEast = GameObject.Find("GoalFrame_East");
            var goalWest = GameObject.Find("GoalFrame_West");
            Assert.IsNotNull(goalEast, "GoalFrame_East should exist");
            Assert.IsNotNull(goalWest, "GoalFrame_West should exist");
        }

        [UnityTest]
        public IEnumerator Bootstrap_GoalDetectors_Exist()
        {
            yield return null;
            var detEast = GameObject.Find("GoalDetector_East");
            var detWest = GameObject.Find("GoalDetector_West");
            Assert.IsNotNull(detEast, "GoalDetector_East should exist");
            Assert.IsNotNull(detWest, "GoalDetector_West should exist");
        }

        [UnityTest]
        public IEnumerator Bootstrap_HUDCanvas_Exists()
        {
            yield return null;
            var hud = GameObject.Find("HUDCanvas");
            Assert.IsNotNull(hud, "HUDCanvas should be created by MatchOrchestrator");
        }

        // === TEAM SPAWNING TESTS ===

        [UnityTest]
        public IEnumerator Teams_FourHomePlayers_Spawned()
        {
            yield return null;
            var players = new List<GameObject>();
            for (int i = 0; i < 4; i++)
            {
                var p = GameObject.Find($"TeamA_Player_{i}");
                if (p != null) players.Add(p);
            }
            Assert.AreEqual(4, players.Count,
                $"Expected 4 home (TeamA) players, found {players.Count}");
        }

        [UnityTest]
        public IEnumerator Teams_FourAwayPlayers_Spawned()
        {
            yield return null;
            var players = new List<GameObject>();
            for (int i = 0; i < 4; i++)
            {
                var p = GameObject.Find($"TeamB_Player_{i}");
                if (p != null) players.Add(p);
            }
            Assert.AreEqual(4, players.Count,
                $"Expected 4 away (TeamB) players, found {players.Count}");
        }

        [UnityTest]
        public IEnumerator Teams_EightTotalPlayers_Spawned()
        {
            yield return null;
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                if (GameObject.Find($"TeamA_Player_{i}") != null) count++;
                if (GameObject.Find($"TeamB_Player_{i}") != null) count++;
            }
            Assert.AreEqual(8, count, $"Expected 8 total players (4v4), found {count}");
        }

        // === COMPONENT INTEGRITY TESTS ===

        [UnityTest]
        public IEnumerator Ball_HasBallController()
        {
            yield return null;
            var ball = GameObject.Find("Ball");
            Assert.IsNotNull(ball, "Ball not found");
            Assert.IsNotNull(ball.GetComponent<BallController>(),
                "Ball should have BallController component");
        }

        [UnityTest]
        public IEnumerator Ball_HasBallOwnership()
        {
            yield return null;
            var ball = GameObject.Find("Ball");
            Assert.IsNotNull(ball, "Ball not found");
            Assert.IsNotNull(ball.GetComponent<BallOwnership>(),
                "Ball should have BallOwnership component");
        }

        [UnityTest]
        public IEnumerator Ball_HasRigidbody()
        {
            yield return null;
            var ball = GameObject.Find("Ball");
            Assert.IsNotNull(ball, "Ball not found");
            var rb = ball.GetComponent<Rigidbody>();
            Assert.IsNotNull(rb, "Ball should have Rigidbody component");
        }

        [UnityTest]
        public IEnumerator Players_HavePlayerController()
        {
            yield return null;
            for (int i = 0; i < 4; i++)
            {
                var p = GameObject.Find($"TeamA_Player_{i}");
                Assert.IsNotNull(p, $"TeamA_Player_{i} not found");
                Assert.IsNotNull(p.GetComponent<PlayerController>(),
                    $"TeamA_Player_{i} should have PlayerController");
            }
        }

        [UnityTest]
        public IEnumerator Players_HaveRigidbody()
        {
            yield return null;
            for (int i = 0; i < 4; i++)
            {
                var p = GameObject.Find($"TeamA_Player_{i}");
                Assert.IsNotNull(p, $"TeamA_Player_{i} not found");
                var rb = p.GetComponent<Rigidbody>();
                Assert.IsNotNull(rb, $"TeamA_Player_{i} should have Rigidbody");
                Assert.AreEqual(70f, rb.mass, 0.1f,
                    $"TeamA_Player_{i} Rigidbody mass should be 70kg");
            }
        }

        [UnityTest]
        public IEnumerator Players_HavePlayerKicker()
        {
            yield return null;
            for (int i = 0; i < 4; i++)
            {
                var p = GameObject.Find($"TeamA_Player_{i}");
                Assert.IsNotNull(p, $"TeamA_Player_{i} not found");
                Assert.IsNotNull(p.GetComponent<PlayerKicker>(),
                    $"TeamA_Player_{i} should have PlayerKicker");
            }
        }

        // === HUD TESTS ===

        [UnityTest]
        public IEnumerator HUD_ScoreText_Exists()
        {
            yield return null;
            var scoreGo = GameObject.Find("ScoreText");
            Assert.IsNotNull(scoreGo, "ScoreText should exist under HUDCanvas");
            var tmp = scoreGo.GetComponent<TextMeshProUGUI>();
            Assert.IsNotNull(tmp, "ScoreText should have TextMeshProUGUI component");
            Assert.IsTrue(tmp.text.Contains("0 - 0"),
                $"Score should start at 0-0, got: '{tmp.text}'");
        }

        [UnityTest]
        public IEnumerator HUD_TimerText_Exists()
        {
            yield return null;
            var timerGo = GameObject.Find("TimerText");
            Assert.IsNotNull(timerGo, "TimerText should exist under HUDCanvas");
            var tmp = timerGo.GetComponent<TextMeshProUGUI>();
            Assert.IsNotNull(tmp, "TimerText should have TextMeshProUGUI component");
        }

        [UnityTest]
        public IEnumerator HUD_PeriodText_ShowsFirstHalf()
        {
            yield return null;
            var periodGo = GameObject.Find("PeriodText");
            Assert.IsNotNull(periodGo, "PeriodText should exist under HUDCanvas");
            var tmp = periodGo.GetComponent<TextMeshProUGUI>();
            Assert.IsNotNull(tmp, "PeriodText should have TextMeshProUGUI component");
            Assert.AreEqual("1ST HALF", tmp.text,
                $"Period should start as '1ST HALF', got: '{tmp.text}'");
        }

        // === ACTIVE PLAYER TESTS ===

        [UnityTest]
        public IEnumerator ActivePlayer_HasYellowIndicator()
        {
            yield return null;
            // Find any player with an ActiveIndicator child
            bool found = false;
            for (int i = 0; i < 4; i++)
            {
                var p = GameObject.Find($"TeamA_Player_{i}");
                if (p != null)
                {
                    var indicator = p.transform.Find("ActiveIndicator");
                    if (indicator != null)
                    {
                        found = true;
                        break;
                    }
                }
            }
            Assert.IsTrue(found, "One home player should have an ActiveIndicator");
        }

        [UnityTest]
        public IEnumerator ActivePlayer_OnlyOnePlayerControllerEnabled()
        {
            yield return null;
            int enabledCount = 0;
            for (int i = 0; i < 4; i++)
            {
                var p = GameObject.Find($"TeamA_Player_{i}");
                if (p != null)
                {
                    var pc = p.GetComponent<PlayerController>();
                    if (pc != null && pc.enabled) enabledCount++;
                }
            }
            Assert.AreEqual(1, enabledCount,
                $"Exactly 1 home PlayerController should be enabled, found {enabledCount}");
        }

        // === MATCH STATE TESTS ===

        [UnityTest]
        public IEnumerator MatchState_StartsInFirstHalf()
        {
            // Wait a couple frames for Start() to complete
            yield return null;
            yield return null;

            var periodGo = GameObject.Find("PeriodText");
            Assert.IsNotNull(periodGo, "PeriodText not found");
            var tmp = periodGo.GetComponent<TextMeshProUGUI>();
            Assert.AreEqual("1ST HALF", tmp.text,
                "Match should start in 1ST HALF state");
        }

        [UnityTest]
        public IEnumerator MatchTimer_CountsDown()
        {
            // Read initial timer
            var timerGo = GameObject.Find("TimerText");
            Assert.IsNotNull(timerGo, "TimerText not found");
            var tmp = timerGo.GetComponent<TextMeshProUGUI>();
            string initialTime = tmp.text;

            // Wait 2 seconds
            yield return new WaitForSeconds(2f);

            string laterTime = tmp.text;
            Assert.AreNotEqual(initialTime, laterTime,
                $"Timer should have changed after 2 seconds. Initial: {initialTime}, Later: {laterTime}");
        }

        // === BALL POSITION TESTS ===

        [UnityTest]
        public IEnumerator Ball_StartsNearCenter()
        {
            yield return null;
            var ball = GameObject.Find("Ball");
            Assert.IsNotNull(ball, "Ball not found");
            var pos = ball.transform.position;
            Assert.That(Mathf.Abs(pos.x), Is.LessThan(2f),
                $"Ball should start near center X, was at {pos}");
            Assert.That(Mathf.Abs(pos.z), Is.LessThan(2f),
                $"Ball should start near center Z, was at {pos}");
        }

        // === NO CONSOLE ERROR TEST ===

        [UnityTest]
        public IEnumerator NoConsoleErrors_OnBootstrap()
        {
            // LogAssert will fail the test if any unexpected errors were logged
            // during setup. This catches Input System errors, null refs, etc.
            yield return new WaitForSeconds(0.5f);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
