using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenFifa.Tests.Runtime
{
    [TestFixture]
    [Category("US001")]
    public class US001_ProjectScaffoldPlayModeTests
    {
        [UnityTest]
        public IEnumerator SamplePlayModeTest_WhenRun_YieldsAndPasses()
        {
            // This is the sample PlayMode test that yields and passes
            // Verifies the PlayMode test runner is operational
            yield return null;

            Assert.IsTrue(true, "Sample PlayMode test should yield and pass");
        }

        [UnityTest]
        public IEnumerator PlayModeTestRunner_WhenCreatingGameObject_Succeeds()
        {
            var go = new GameObject("TestObject");
            yield return null;

            Assert.IsNotNull(go, "Should be able to create GameObjects in PlayMode tests");
            Assert.AreEqual("TestObject", go.name,
                $"GameObject name should be 'TestObject' but was '{go.name}'");

            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator PlayModeTestRunner_GravityEnabled_PhysicsSimulationWorks()
        {
            var go = new GameObject("PhysicsTestObject");
            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = true;
            go.transform.position = new Vector3(0, 10, 0);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.Less(go.transform.position.y, 10f,
                $"Rigidbody should fall under gravity. Position.y = {go.transform.position.y}");

            Object.Destroy(go);
        }
    }
}
