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
            Assert.AreEqual(attackerZ, attacker.GetPosition(1).z, Eps);
            Assert.AreEqual(defenderZ, defender.GetPosition(0).z, Eps);
            Assert.AreEqual(34f, attacker.GetPosition(0).x, Eps);
            Assert.AreEqual(-34f, attacker.GetPosition(1).x, Eps);
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
