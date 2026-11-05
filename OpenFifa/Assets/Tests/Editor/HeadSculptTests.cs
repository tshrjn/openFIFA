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
