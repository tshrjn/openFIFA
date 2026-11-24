using NUnit.Framework;
using OpenFifa.Core;
using OpenFifa.Gameplay;
using UnityEngine;

namespace OpenFifa.Tests.Editor
{
    /// <summary>
    /// The MonoBehaviour pieces of the offside presentation put the lines and the cut camera at the right world
    /// positions for the 4v4 scene (length along X, unchanged) and the 11v11 sim (length along Z). The assistant
    /// referee's placement is covered by OffsidePitchFrameTests (its placeholder build uses Destroy, which edit mode
    /// rejects).
    /// </summary>
    [TestFixture]
    [Category("Offside")]
    public class OffsidePresentationAxisTests
    {
        private const float Eps = 1e-3f;
        private GameObject _root;
        private OffsidePresentationConfig _config;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("OffsideAxisTest");
            _config = OffsidePresentationConfig.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void LineOverlay_LegacyInitialize_LinesRunAlongZAtWorldX()
        {
            var lines = _root.AddComponent<OffsideLineOverlay>();
            lines.Initialize(15f, _config);
            lines.GetLineEndpoints(10f, 1f, out var start, out var end);
            Assert.AreEqual(10f, start.x, Eps);
            Assert.AreEqual(10f, end.x, Eps);
            Assert.AreEqual(-15f, start.z, Eps);
            Assert.AreEqual(15f, end.z, Eps);
            Assert.AreEqual(_config.LineHeight, start.y, Eps);
        }

        [TestCase(31.2f, 29.8f)]
        [TestCase(-31.2f, -29.8f)]
        public void LineOverlay_SimFrame_LinesRunAlongXAtWorldZ(float attackerZ, float defenderZ)
        {
            var lines = _root.AddComponent<OffsideLineOverlay>();
            lines.Initialize(OffsidePitchFrame.ForSimPitch(PitchSpec.Standard), _config);
            lines.Show(attackerZ, defenderZ);
            lines.SetReveal(1f);
            Assert.IsTrue(lines.IsVisible);

            var renderers = _root.GetComponentsInChildren<LineRenderer>(true);
            var attacker = System.Array.Find(renderers, r => r.name == "OffsideAttackerLine");
            var defender = System.Array.Find(renderers, r => r.name == "OffsideDefenderLine");
            Assert.IsNotNull(attacker);
            Assert.IsNotNull(defender);
            Assert.AreEqual(attackerZ, attacker.GetPosition(0).z, Eps);
            Assert.AreEqual(attackerZ, Last(attacker).z, Eps);
            Assert.AreEqual(defenderZ, defender.GetPosition(0).z, Eps);
            Assert.AreEqual(34f, attacker.GetPosition(0).x, Eps);
            Assert.AreEqual(-34f, Last(attacker).x, Eps);
        }

        private static Vector3 Last(LineRenderer r) => r.GetPosition(r.positionCount - 1);

        [Test]
        public void LineOverlay_VertexAlpha_OpaqueAlongTheLine()
        {
            // Regression: with only its two end points, a LineRenderer samples the gradient at the fully transparent
            // touchline ends only, so every line drew at alpha 0 and the VAR lines never showed.
            var lines = _root.AddComponent<OffsideLineOverlay>();
            lines.Initialize(OffsidePitchFrame.ForSimPitch(PitchSpec.Standard), _config);
            lines.Show(-29.71f, -28.69f);
            lines.SetReveal(1f);
            foreach (var r in _root.GetComponentsInChildren<LineRenderer>(true))
            {
                if (!r.enabled) continue;
                Assert.That(r.positionCount, Is.GreaterThanOrEqualTo(3), r.name);
                float total = Vector3.Distance(r.GetPosition(0), Last(r));
                float maxAlpha = 0f;
                for (int i = 0; i < r.positionCount; i++)
                {
                    float t = Vector3.Distance(r.GetPosition(0), r.GetPosition(i)) / total;
                    maxAlpha = Mathf.Max(maxAlpha, r.colorGradient.Evaluate(t).a);
                }
                Assert.That(maxAlpha, Is.GreaterThan(0.1f), r.name + ": no point of the line is visible");
            }
        }

        [Test]
        public void LineOverlay_HasAMaterial_ForAllThreeRenderers()
        {
            var lines = _root.AddComponent<OffsideLineOverlay>();
            lines.Initialize(OffsidePitchFrame.ForSimPitch(PitchSpec.Standard), _config);
            Assert.IsNotNull(lines.LineMaterial, "no line material: the lines would not draw");
            Assert.That(lines.LineMaterial.renderQueue, Is.GreaterThan((int)UnityEngine.Rendering.RenderQueue.Transparent));
            lines.Show(-29.71f, -28.69f);
            lines.SetReveal(1f);
            foreach (var r in _root.GetComponentsInChildren<LineRenderer>(true))
                if (r.enabled) Assert.IsNotNull(r.sharedMaterial, r.name);
        }

        [Test]
        public void LineOverlay_FromTheCutCamera_LinesAtLeastMinimumScreenWidth()
        {
            // The broadcast cut: 14 m up, 6 m beyond the near touchline, at the lines' depth, 50 deg FOV.
            var camGo = new GameObject("CutCam");
            camGo.transform.SetParent(_root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = _config.CameraFieldOfView;
            var frame = OffsidePitchFrame.ForSimPitch(PitchSpec.Standard);
            frame.CameraPose(-29.2f, _config.CameraHeight, _config.CameraSideOffset,
                out float px, out float py, out float pz, out float lx, out float ly, out float lz);
            camGo.transform.SetPositionAndRotation(new Vector3(px, py, pz),
                Quaternion.LookRotation(new Vector3(lx - px, ly - py, lz - pz), Vector3.up));

            var lines = _root.AddComponent<OffsideLineOverlay>();
            lines.Initialize(frame, _config);
            lines.ViewCamera = cam;
            lines.Show(-29.71f, -28.69f);
            lines.SetReveal(1f);

            lines.GetLineEndpoints(-29.71f, 1f, out var start, out var end);
            float nearMin = MinWorldWidth(cam, start), farMin = MinWorldWidth(cam, end);
            Assert.That(lines.AttackerStartWidth, Is.GreaterThanOrEqualTo(Mathf.Max(_config.LineWidth, nearMin) - Eps));
            Assert.That(lines.AttackerEndWidth, Is.GreaterThanOrEqualTo(Mathf.Max(_config.LineWidth, farMin) - Eps));
            Assert.Greater(lines.AttackerEndWidth, lines.AttackerStartWidth, "the far touchline end is wider in world");
            Assert.That(lines.DefenderEndWidth, Is.GreaterThanOrEqualTo(farMin - Eps));
        }

        private float MinWorldWidth(Camera cam, Vector3 p) =>
            2f * Vector3.Distance(cam.transform.position, p) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)
            * _config.LineMinScreenFraction;

        [TestCase(-0.57f, 0.87f)] // c5: the offside at the halfway line (both lines either side of it)
        [TestCase(0f, 0.4f)]      // attacker exactly on the halfway line
        public void LineOverlay_AtHalfwayLine_BothLinesDrawnAtTheirOwnDepth(float attackerZ, float defenderZ)
        {
            var lines = _root.AddComponent<OffsideLineOverlay>();
            lines.Initialize(OffsidePitchFrame.ForSimPitch(PitchSpec.Standard), _config);
            lines.Show(attackerZ, defenderZ);
            lines.SetReveal(1f);

            var renderers = _root.GetComponentsInChildren<LineRenderer>(true);
            var attacker = System.Array.Find(renderers, r => r.name == "OffsideAttackerLine");
            var defender = System.Array.Find(renderers, r => r.name == "OffsideDefenderLine");
            Assert.IsTrue(attacker.enabled && defender.enabled);
            Assert.AreEqual(attackerZ, attacker.GetPosition(0).z, Eps);
            Assert.AreEqual(defenderZ, defender.GetPosition(0).z, Eps);
            // Above the pitch markings (~0.065 m), so the white halfway line does not hide them.
            Assert.That(attacker.GetPosition(0).y, Is.GreaterThan(0.065f));
            Assert.That(Last(defender).y, Is.GreaterThan(0.065f));
            // Parallel: both run the full width at constant depth.
            Assert.AreEqual(attacker.GetPosition(0).z, Last(attacker).z, Eps);
            Assert.AreEqual(defender.GetPosition(0).z, Last(defender).z, Eps);
            Assert.AreEqual(Mathf.Abs(attacker.GetPosition(0).x - Last(attacker).x), 68f, Eps);
            // Line centres are further apart than one line width: two distinct lines, not one.
            Assert.Greater(Mathf.Abs(attackerZ - defenderZ), _config.LineWidth);
        }

        [Test]
        public void CameraCut_SimFrame_SideOnAlignedWithLinesAndRestoresFov()
        {
            var cam = _root.AddComponent<Camera>();
            cam.fieldOfView = 27f;
            var cut = _root.AddComponent<OffsideCameraCut>();
            cut.Initialize(cam, null, OffsidePitchFrame.ForSimPitch(PitchSpec.Standard), _config);

            cut.CutTo(-20f);
            Assert.IsTrue(cut.IsCut);
            var t = cam.transform;
            Assert.AreEqual(34f + _config.CameraSideOffset, t.position.x, Eps);
            Assert.AreEqual(-20f, t.position.z, Eps);
            Assert.AreEqual(0f, t.forward.z, Eps, "looks straight across the pitch, along the lines");
            Assert.Less(t.forward.x, 0f);
            Assert.AreEqual(_config.CameraFieldOfView, cam.fieldOfView, Eps);

            cut.Restore();
            Assert.IsFalse(cut.IsCut);
            Assert.AreEqual(27f, cam.fieldOfView, Eps);
        }

        [Test]
        public void CameraCut_LegacyWithDriver_SuspendsAndRestoresExactPose()
        {
            var cam = _root.AddComponent<Camera>();
            var driverGo = new GameObject("Driver");
            driverGo.transform.SetParent(_root.transform);
            var driver = driverGo.AddComponent<OffsideBanner>(); // any Behaviour stands in for the broadcast controller
            cam.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 20f, 0f));
            var cut = _root.AddComponent<OffsideCameraCut>();
            cut.Initialize(cam, driver, 15f, _config);

            cut.CutTo(5f);
            Assert.IsFalse(driver.enabled);
            Assert.AreEqual(5f, cam.transform.position.x, Eps);
            Assert.AreEqual(-(15f + _config.CameraSideOffset), cam.transform.position.z, Eps);

            cut.Restore();
            Assert.IsTrue(driver.enabled);
            Assert.AreEqual(new Vector3(1f, 2f, 3f).ToString(), cam.transform.position.ToString());
        }

        [Test]
        public void Config_DefaultTiming_MatchesCoreDefaultsWithinTwoAndAHalfSeconds()
        {
            var fromConfig = _config.ToTimingData();
            var core = new OffsidePresentationTimingData();
            Assert.AreEqual(core.TotalDuration, fromConfig.TotalDuration, Eps);
            Assert.AreEqual(core.FreezeStart, fromConfig.FreezeStart, Eps);
            Assert.AreEqual(core.CameraCutIn, fromConfig.CameraCutIn, Eps);
            Assert.AreEqual(core.LinesShow, fromConfig.LinesShow, Eps);
            Assert.AreEqual(core.TimeRestore, fromConfig.TimeRestore, Eps);
            Assert.LessOrEqual(fromConfig.TotalDuration, 2.5f + Eps);
        }
    }
}
