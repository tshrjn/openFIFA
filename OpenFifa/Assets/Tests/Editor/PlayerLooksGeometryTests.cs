using NUnit.Framework;
using OpenFifa.Editor;
using OpenFifa.Gameplay;
using UnityEngine;

namespace OpenFifa.Tests.Editor
{
    /// <summary>Import-time normal smoothing (KitBodyMapPostprocessor.SmoothNormals) and the procedural ears.</summary>
    [TestFixture]
    [Category("Kits")]
    public class PlayerLooksGeometryTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void SmoothNormals_SplitCube_KeepsRightAngleEdgesHard()
        {
            var mesh = SplitCube();
            KitBodyMapPostprocessor.SmoothNormals(mesh, KitBodyMapPostprocessor.SmoothAngle);
            var n = mesh.normals;
            // Every vertex keeps its own face's axis-aligned normal: 90 degrees is above the smoothing angle.
            for (int i = 0; i < n.Length; i++)
            {
                float maxAxis = Mathf.Max(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
                Assert.AreEqual(1f, maxAxis, 1e-4f, "vertex " + i);
            }
        }

        [Test]
        public void SmoothNormals_ShallowCreaseWithSplitVertices_SharesNormals()
        {
            // Two quads meeting at a 30 degree crease along x = 0, with the crease vertices split (hard edge).
            float t = Mathf.Tan(30f * Mathf.Deg2Rad);
            var verts = new[]
            {
                new Vector3(-1, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(-1, 0, 1),     // flat quad
                new Vector3(0, 0, 0), new Vector3(1, t, 0), new Vector3(1, t, 1), new Vector3(0, 0, 1),       // tilted quad
            };
            var mesh = new Mesh { vertices = verts, triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6 } };
            mesh.RecalculateNormals(); // per-shell normals: the crease is hard
            Assert.Greater(Vector3.Angle(mesh.normals[1], mesh.normals[4]), 25f);

            KitBodyMapPostprocessor.SmoothNormals(mesh, KitBodyMapPostprocessor.SmoothAngle);
            var n = mesh.normals;
            Assert.Less(Vector3.Angle(n[1], n[4]), 0.01f, "crease vertex (0,0,0)");
            Assert.Less(Vector3.Angle(n[2], n[7]), 0.01f, "crease vertex (0,0,1)");
            Assert.AreEqual(1f, n[1].magnitude, 1e-4f);
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

        private static Mesh SplitCube()
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            var normals = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var nrm in normals)
            {
                var u = Vector3.Cross(nrm, Mathf.Abs(nrm.y) > 0.5f ? Vector3.forward : Vector3.up);
                var w = Vector3.Cross(nrm, u);
                int b = verts.Count;
                verts.Add(0.5f * (nrm - u - w)); verts.Add(0.5f * (nrm + u - w));
                verts.Add(0.5f * (nrm + u + w)); verts.Add(0.5f * (nrm - u + w));
                tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
