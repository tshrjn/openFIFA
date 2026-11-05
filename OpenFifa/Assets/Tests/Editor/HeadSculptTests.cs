using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Editor;
using UnityEditor;
using UnityEngine;

namespace OpenFifa.Tests.Editor
{
    /// <summary>Import-time head sculpt (HeadSculpt) on the single-player mannequin.</summary>
    [TestFixture]
    [Category("Kits")]
    public class HeadSculptTests
    {
        private const string ModelPath = "Assets/Animations/Quaternius/UAL2/UAL2_Standard.fbx";

        [Test]
        public void FaceRelief_NoseTipStandsOut_BackOfHeadUntouched()
        {
            float tip = HeadSculpt.FaceRelief(new Vector3(0f, 0.195f, 0.105f));
            float cheek = HeadSculpt.FaceRelief(new Vector3(0.03f, 0.195f, 0.095f));
            float back = HeadSculpt.FaceRelief(new Vector3(0f, 0.2f, -0.13f));
            float socket = HeadSculpt.FaceRelief(new Vector3(0.031f, 0.226f, 0.1f));
            Assert.Greater(tip, 0.015f, "nose tip");
            Assert.Greater(tip, cheek * 2f, "nose stands out of the cheek");
            Assert.AreEqual(0f, back, 1e-6f, "back of the head");
            Assert.Less(socket, tip, "eye socket below the nose");
        }

        [Test]
        public void Apply_HeadNeckStrip_StaysConformingAndKeepsTheBoundaryStraight()
        {
            // A 4x4-quad strip: rows y >= 2 skinned to "Head", rows below to the root. Curved normals make the
            // Phong midpoints move off the plane, which would open cracks at the head/neck edge if it were not handled.
            var root = new GameObject("root");
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            try
            {
                var verts = new List<Vector3>(); var normals = new List<Vector3>(); var weights = new List<BoneWeight>();
                for (int y = 0; y <= 4; y++)
                    for (int x = 0; x <= 4; x++)
                    {
                        verts.Add(new Vector3(x * 0.02f, y * 0.02f, 0f));
                        normals.Add(new Vector3((x - 2) * 0.4f, (y - 2) * 0.4f, 1f).normalized);
                        weights.Add(new BoneWeight { boneIndex0 = y >= 2 ? 1 : 0, weight0 = 1f });
                    }
                var tris = new List<int>();
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                    {
                        int i = y * 5 + x;
                        tris.AddRange(new[] { i, i + 5, i + 1, i + 1, i + 5, i + 6 });
                    }
                var mesh = new Mesh();
                mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.boneWeights = weights.ToArray();
                mesh.SetTriangles(tris, 0);
                mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.identity };
                var smr = root.AddComponent<SkinnedMeshRenderer>();
                smr.bones = new[] { root.transform, head };
                smr.sharedMesh = mesh;

                HeadSculpt.Apply(smr);

                var v = mesh.vertices;
                Assert.Greater(v.Length, 25, "head rows subdivided");
                // Every vertex at the boundary height (y = 0.04, the last row skinned to the neck's neighbour) is on
                // the original plane: the boundary edge stayed straight.
                for (int i = 0; i < v.Length; i++)
                    if (Mathf.Abs(v[i].y - 0.04f) < 1e-5f) Assert.AreEqual(0f, v[i].z, 1e-6f, "boundary vertex " + i + " " + v[i]);
                // Conforming: no vertex lies strictly inside any triangle edge (no T-junctions).
                var t = mesh.triangles;
                for (int k = 0; k < t.Length; k += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        Vector3 a = v[t[k + e]], b = v[t[k + (e + 1) % 3]];
                        for (int i = 0; i < v.Length; i++)
                        {
                            if (i == t[k + e] || i == t[k + (e + 1) % 3]) continue;
                            var ab = b - a; float s = Vector3.Dot(v[i] - a, ab) / ab.sqrMagnitude;
                            if (s <= 1e-4f || s >= 1f - 1e-4f) continue;
                            Assert.Greater((a + ab * s - v[i]).magnitude, 1e-5f, $"T-junction: vertex {i} on edge {t[k + e]}-{t[k + (e + 1) % 3]}");
                        }
                    }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MannequinMesh_Imported_NoseStandsOutOfTheCheek()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) Assert.Ignore("Mannequin model not in project.");
            var mesh = model.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh;
            var kit = new List<Vector4>();
            mesh.GetUVs(3, kit); // rest-pose kit-space position (baked after the sculpt)
            Assert.AreEqual(mesh.vertexCount, kit.Count);
            float nose = float.MinValue, cheek = float.MinValue;
            foreach (var k in kit)
            {
                if (k.y < 0.188f || k.y > 0.198f) continue;
                if (Mathf.Abs(k.x) < 0.004f) nose = Mathf.Max(nose, k.z);
                else if (Mathf.Abs(Mathf.Abs(k.x) - 0.03f) < 0.005f) cheek = Mathf.Max(cheek, k.z);
            }
            Assert.Greater(nose, float.MinValue, "vertices near the nose tip");
            Assert.Greater(cheek, float.MinValue, "vertices on the cheek");
            Assert.Greater(nose - cheek, 0.012f, $"nose tip {nose:F4} vs cheek {cheek:F4}");
        }

        [Test]
        public void MannequinMesh_Imported_HeadIsSubdividedAndFinite()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) Assert.Ignore("Mannequin model not in project.");
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var mesh = smr.sharedMesh;
            Assert.Greater(mesh.vertexCount, 8462, "head triangles subdivided (raw mesh has 8462 vertices)");
            var b = mesh.bounds;
            Assert.IsFalse(float.IsNaN(b.size.x) || float.IsInfinity(b.size.x), "finite bounds");
            Assert.Less(b.size.magnitude, 5f, "no runaway vertices");
        }
    }
}
