using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using TMPro;
using OpenFifa.Core;
using OpenFifa.UI;

namespace OpenFifa.Tests.Runtime
{
    [TestFixture]
    [Category("US008")]
    public class US008_HUDDisplayTests
    {
        private GameObject _canvasRoot;
        private Canvas _canvas;
        private CanvasScaler _scaler;
        private HUDController _hud;
        private MatchTimer _timer;
        private MatchScore _score;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _canvasRoot = new GameObject("HUDCanvas");
            _canvas = _canvasRoot.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _scaler = _canvasRoot.AddComponent<CanvasScaler>();
            _canvasRoot.AddComponent<GraphicRaycaster>();

            // Score text
            var scoreObj = new GameObject("ScoreText");
            scoreObj.transform.SetParent(_canvasRoot.transform);
            var scoreText = scoreObj.AddComponent<TextMeshProUGUI>();
            var scoreRect = scoreObj.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0.5f, 1f);
            scoreRect.anchorMax = new Vector2(0.5f, 1f);
            scoreRect.anchoredPosition = new Vector2(0, -40);

            // Timer text
            var timerObj = new GameObject("TimerText");
            timerObj.transform.SetParent(_canvasRoot.transform);
            var timerText = timerObj.AddComponent<TextMeshProUGUI>();
            var timerRect = timerObj.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 1f);
            timerRect.anchorMax = new Vector2(0.5f, 1f);
            timerRect.anchoredPosition = new Vector2(0, -80);

            _timer = new MatchTimer(180f);
            _score = new MatchScore();

            _hud = _canvasRoot.AddComponent<HUDController>();
            _hud.Initialize(_timer, _score, scoreText, timerText);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_canvasRoot != null) Object.Destroy(_canvasRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Canvas_RenderMode_IsScreenSpaceOverlay()
        {
            yield return null;
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, _canvas.renderMode,
                "Canvas render mode should be ScreenSpaceOverlay");
        }

        [UnityTest]
        public IEnumerator CanvasScaler_IsConfigured_WithReferenceResolution()
        {
            yield return null;

            _hud.ConfigureCanvasScaler(_scaler);

            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, _scaler.uiScaleMode,
                "Canvas Scaler should use Scale With Screen Size mode");
            Assert.AreEqual(1920f, _scaler.referenceResolution.x, 1f,
                $"Reference resolution X should be 1920 but was {_scaler.referenceResolution.x}");
            Assert.AreEqual(1080f, _scaler.referenceResolution.y, 1f,
                $"Reference resolution Y should be 1080 but was {_scaler.referenceResolution.y}");
        }

        [UnityTest]
        public IEnumerator ScoreText_DefaultState_DisplaysCorrectFormat()
        {
            yield return null;

            var scoreText = _hud.ScoreText;
            Assert.IsTrue(scoreText.text.Contains("0 - 0"),
                $"Score text should contain '0 - 0' but was '{scoreText.text}'");
        }

        [UnityTest]
        public IEnumerator ScoreText_AfterGoalScored_UpdatesOnGoal()
        {
            yield return null;

            _score.AddGoal(TeamIdentifier.TeamA);
            _hud.RefreshScore();

            yield return null;

            var scoreText = _hud.ScoreText;
            Assert.IsTrue(scoreText.text.Contains("1 - 0"),
                $"Score text should contain '1 - 0' after TeamA goal but was '{scoreText.text}'");
        }

        [UnityTest]
        public IEnumerator TimerText_MatchStarted_DisplaysMMSSFormat()
        {
            _timer.StartMatch();
            yield return null;

            _hud.RefreshTimer();

            var timerText = _hud.TimerText;
            Assert.IsTrue(timerText.text.Contains(":"),
                $"Timer text should be in MM:SS format but was '{timerText.text}'");
            Assert.AreEqual(5, timerText.text.Length,
                $"Timer text should be 5 chars (MM:SS) but was {timerText.text.Length}: '{timerText.text}'");
        }

        [UnityTest]
        public IEnumerator TimerText_AfterTick_UpdatesOnTick()
        {
            _timer.StartMatch();
            yield return null;

            _hud.RefreshTimer();
            string initialText = _hud.TimerText.text;

            _timer.Tick(10f);
            _hud.RefreshTimer();

            yield return null;

            string updatedText = _hud.TimerText.text;
            Assert.AreNotEqual(initialText, updatedText,
                $"Timer text should change after tick. Initial: '{initialText}', After: '{updatedText}'");
        }
    }
}
