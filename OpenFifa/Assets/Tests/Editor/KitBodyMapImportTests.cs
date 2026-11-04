using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenFifa.Tests.Editor
{
    /// <summary>The single-player mannequin is imported with the kit body map (KitBodyMapPostprocessor).</summary>
    [TestFixture]
    [Category("Kits")]
    public class KitBodyMapImportTests
    {
        private const string ModelPath = "Assets/Animations/Quaternius/UAL2/UAL2_Standard.fbx";

        [Test]
        public void MannequinMesh_Imported_HasBodyMapWithPlausibleRegions()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) Assert.Ignore("Mannequin model not in project.");
            var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotEmpty(smrs);
            foreach (var smr in smrs)
            {
                var mesh = smr.sharedMesh;
                Assert.IsTrue(mesh.HasVertexAttribute(VertexAttribute.TexCoord2), smr.name + " UV2");
                Assert.IsTrue(mesh.HasVertexAttribute(VertexAttribute.TexCoord3), smr.name + " UV3");
                Assert.IsTrue(OpenFifa.Gameplay.JerseyRenderer.HasBodyMap(smr));
            }
        }
    }
}
