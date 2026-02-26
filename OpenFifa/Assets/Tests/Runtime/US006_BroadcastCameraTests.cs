using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using OpenFifa.Gameplay;
using OpenFifa.Core;

namespace OpenFifa.Tests.Runtime
{
    [TestFixture]
    [Category("US-006")]
    public class US006_BroadcastCameraTests
    {
        private GameObject _pitchRoot;
        private GameObject _ball;
        private GameObject _cameraRig;
        private BroadcastCameraController _cameraController;
        private Camera _mainCamera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var pitchConfig = new PitchConfigData();
            _pitchRoot = new GameObject("PitchTestRoot");
            var builder = _pitchRoot.AddComponent<PitchBuilder>();
            builder.BuildPitch(pitchConfig);

            _ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _ball.name = "Ball";
            _ball.transform.position = new Vector3(0, 0.5f, 0);

            // Create camera rig
            _cameraRig = new GameObject("MainCamera");
            _cameraRig.tag = "MainCamera";
            _mainCamera = _cameraRig.AddComponent<Camera>();
            _cameraController = _cameraRig.AddComponent<BroadcastCameraController>();
            _cameraController.Initialize(new CameraConfigData(), _ball.transform, null);

            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_ball != null) Object.Destroy(_ball);
            if (_cameraRig != null) Object.Destroy(_cameraRig);
            if (_pitchRoot != null) Object.Destroy(_pitchRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Camera_FollowsBall_AsTrackingTarget()
        {
            yield return null;

            // Move ball to a new position
            _ball.transform.position = new Vector3(10f, 0.5f, 5f);

            // Wait for camera to follow
            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Camera should be looking toward the ball
            Vector3 camToBall = _ball.transform.position - _cameraRig.transform.position;
            float angle = Vector3.Angle(_cameraRig.transform.forward, camToBall);

            Assert.Less(angle, 30f,
                $"Camera should be pointing toward ball. Angle between forward and ball direction: {angle:F1} degrees. " +
                $"Camera pos: {_cameraRig.transform.position}, Ball pos: {_ball.transform.position}");
        }

        [UnityTest]
        public IEnumerator Camera_ElevationAngle_IsApproximately35Degrees()
        {
            yield return null;
            yield return null;
            yield return null;

            // Camera should be above the pitch at roughly 35 degree angle
            Vector3 camPos = _cameraRig.transform.position;
            Vector3 ballPos = _ball.transform.position;
            Vector3 horizontal = new Vector3(ballPos.x - camPos.x, 0, ballPos.z - camPos.z);
            float elevation = Mathf.Atan2(camPos.y - ballPos.y, horizontal.magnitude) * Mathf.Rad2Deg;

            Assert.That(elevation, Is.InRange(20f, 50f),
                $"Camera elevation angle should be ~35 degrees but was {elevation:F1} degrees. " +
                $"Camera pos: {camPos}, Ball pos: {ballPos}");
        }

        [UnityTest]
        public IEnumerator Camera_YPosition_NeverGoesBelow0()
        {
            yield return null;

            // Move ball around and check camera never goes below pitch
            Vector3[] positions = new[]
            {
                new Vector3(-20, 0.5f, 0),
                new Vector3(20, 0.5f, 0),
                new Vector3(0, 0.5f, 12),
                new Vector3(0, 0.5f, -12)
            };

            foreach (var pos in positions)
            {
                _ball.transform.position = pos;
                for (int i = 0; i < 30; i++) yield return null;

                Assert.Greater(_cameraRig.transform.position.y, 0f,
                    $"Camera Y should always be > 0. Camera pos: {_cameraRig.transform.position} " +
                    $"when ball at {pos}");
            }
        }

        [UnityTest]
        public IEnumerator Camera_SmoothlyFollows_NoBigJumps()
        {
            yield return null;

            Vector3 previousPos = _cameraRig.transform.position;

            // Move ball rapidly
            _ball.transform.position = new Vector3(20f, 0.5f, 10f);

            yield return null;

            Vector3 newPos = _cameraRig.transform.position;
            float delta = Vector3.Distance(previousPos, newPos);

            // Camera should not teleport (smooth damping)
            Assert.Less(delta, 15f,
                $"Camera should smoothly follow, not teleport. " +
                $"Moved {delta:F2}m in one frame. " +
                $"Previous: {previousPos}, Current: {newPos}");
        }

        [UnityTest]
        public IEnumerator Camera_FieldOfView_ShowsReasonablePortionOfPitch()
        {
            yield return null;

            // Camera field of view should show a good portion of the pitch
            Assert.That(_mainCamera.fieldOfView, Is.InRange(30f, 90f),
                $"Camera FOV should be reasonable but was {_mainCamera.fieldOfView}");
        }
    }
}
