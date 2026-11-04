using System.Collections;
using NUnit.Framework;
using OpenFifa.Core;
using OpenFifa.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFifa.Tests.Runtime
{
    [TestFixture]
    [Category("ExternalDisplay")]
    public class ExternalDisplayRoutingTests
    {
        private int _displayCount;
        private GameObject _root;
        private ExternalDisplayManager _manager;
        private Camera _gameCamera;
        private Camera _minimapCamera;
        private Canvas _hud;
        private Canvas _deviceCanvas;
        private Canvas _menu;

        [SetUp]
        public void SetUp()
        {
            _displayCount = 1;
            ExternalDisplayManager.DisplayCountOverride = () => _displayCount;
            ExternalDisplayManager.FakeNativeSize = new PixelSize(3840, 2160);
            _root = new GameObject("ExternalDisplayRoutingTests");

            _gameCamera = NewCamera("Game", new Rect(0f, 0f, 1f, 1f));
            _minimapCamera = NewCamera("Minimap", new Rect(0.75f, 0.75f, 0.25f, 0.25f));
            _hud = NewCanvas("HUD");
            _deviceCanvas = NewCanvas("TouchControls");
            _deviceCanvas.gameObject.AddComponent<DeviceScreenCanvas>();
            _menu = NewCanvas("PauseMenu");
            var button = new GameObject("Resume", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(_menu.transform, false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null) Object.DestroyImmediate(_manager.gameObject);
            Object.DestroyImmediate(_root);
            ExternalDisplayManager.DisplayCountOverride = null;
            FramePacer.ClearOverride();
        }

        [UnityTest]
        public IEnumerator Monitor_PluggedIn_RoutesGameAndHudOnly_ThenRestoresOnUnplug()
        {
            _manager = new GameObject("[ExternalDisplayManager]").AddComponent<ExternalDisplayManager>();
            yield return null;
            Assert.IsFalse(_manager.IsExternalActive);
            Assert.AreEqual(0, _gameCamera.targetDisplay);

            _displayCount = 2;
            yield return null;
            yield return null;

            Assert.IsTrue(_manager.IsExternalActive, "manager should pick up the second display");
            Assert.AreEqual(1, _gameCamera.targetDisplay, "full-screen game camera goes to the monitor");
            Assert.AreEqual(0, _minimapCamera.targetDisplay, "partial-viewport cameras stay put");
            Assert.AreEqual(new Rect(0.75f, 0.75f, 0.25f, 0.25f), _minimapCamera.rect, "minimap rect untouched");
            Assert.AreEqual(1, _hud.targetDisplay, "display-only HUD goes to the monitor");
            Assert.AreEqual(0, _deviceCanvas.targetDisplay, "DeviceScreenCanvas stays on the iPad");
            Assert.AreEqual(0, _menu.targetDisplay, "touchable canvases stay on the iPad");
            Assert.AreEqual(new PixelSize(2560, 1440), _manager.RenderSize, "4K monitor starts at the 1440p tier");

            _displayCount = 1;
            yield return null;

            Assert.IsFalse(_manager.IsExternalActive);
            Assert.AreEqual(0, _gameCamera.targetDisplay);
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), _gameCamera.rect);
            Assert.AreEqual(0, _hud.targetDisplay);
        }

        [UnityTest]
        public IEnumerator Monitor_SuperUltrawide_PillarboxesGameCamera()
        {
            ExternalDisplayManager.FakeNativeSize = new PixelSize(5120, 1440);
            _displayCount = 2;
            _manager = new GameObject("[ExternalDisplayManager]").AddComponent<ExternalDisplayManager>();
            yield return null;

            Assert.IsTrue(_manager.IsExternalActive);
            Assert.Less(_gameCamera.rect.width, 1f, "32:9 should be pillarboxed");
            Assert.AreEqual(1f, _gameCamera.rect.height, 0.0001f);
            Assert.AreEqual((1f - _gameCamera.rect.width) / 2f, _gameCamera.rect.x, 0.0001f, "bars are centred");
        }

        private Camera NewCamera(string name, Rect rect)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var cam = go.AddComponent<Camera>();
            cam.rect = rect;
            return cam;
        }

        private Canvas NewCanvas(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }
    }
}
