using NUnit.Framework;
using OpenFifa.Gameplay;
using UnityEngine;

namespace OpenFifa.Tests.Editor
{
    /// <summary>Procedural ears (EarBuilder).</summary>
    [TestFixture]
    [Category("Kits")]
    public class EarBuilderTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void EarMesh_HasExpectedSizeAndSitsAroundTheHead()
        {
            var mesh = EarBuilder.GetMesh();
            Assert.AreEqual(EarBuilder.VertexCount, mesh.vertexCount);
            Assert.AreEqual(EarBuilder.TriangleCount * 3, mesh.triangles.Length);
            var v = mesh.vertices;
            var n = mesh.normals;
            var centre = HairCapBuilder.KitToBone(new Vector3(0f, 0.212f, -0.022f));
            float midX = HairCapBuilder.KitToBone(Vector3.zero).x;
            int left = 0, right = 0;
            for (int i = 0; i < v.Length; i++)
            {
                Assert.Less((v[i] - centre).magnitude, 0.12f, "vertex " + i + " near the head");
                // Clear of the skull (half width ~0.086 m), or the head's painted hair shows through the ear.
                Assert.GreaterOrEqual(Mathf.Abs(v[i].x - midX), 0.087f - 1e-4f, "vertex " + i + " outside the skull");
                Assert.AreEqual(1f, n[i].magnitude, 1e-3f, "unit normal " + i);
                // Normals face away from the head: same side as the vertex.
                if (Mathf.Abs(v[i].x) > 0.05f) Assert.AreEqual(Mathf.Sign(v[i].x), Mathf.Sign(n[i].x), "normal side " + i);
                if (v[i].x > 0f) right++; else left++;
            }
            Assert.AreEqual(left, right, "one ear per side");
        }

        [Test]
        public void EarAttach_AddsOneEarsChildAndReplacesItOnReattach()
        {
            _go = new GameObject("Model");
            var head = new GameObject("Head").transform;
            head.SetParent(_go.transform, false);
            var first = EarBuilder.Attach(_go, new Color(0.6f, 0.45f, 0.35f));
            Assert.IsNotNull(first, "URP Lit missing");
            Assert.AreEqual(head, first.transform.parent);
            var firstMat = first.sharedMaterial;
            var second = EarBuilder.Attach(_go, new Color(0.3f, 0.2f, 0.15f));
            Assert.IsNotNull(second);
            Assert.AreNotSame(firstMat, second.sharedMaterial, "material per skin tone");
            int ears = 0;
            foreach (Transform c in head) if (c.name == "Ears") ears++;
            Assert.AreEqual(1, ears, "old ears replaced");
            Assert.AreSame(head, second.transform.parent);
        }
    }
}
