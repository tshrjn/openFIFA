using System.Linq;
using NUnit.Framework;
using OpenFifa.Core;
using OpenFifa.Editor;
using OpenFifa.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenFifa.Tests.Editor
{
    /// <summary>
    /// The Blender athlete body (tools/blender/athlete_body.py) imports as a Humanoid with three skinned LODs that
    /// carry the kit body map, within the triangle budget, and the game systems recognise it.
    /// </summary>
    [TestFixture]
    [Category("Kits")]
    public class AthleteBodyImportTests
    {
        private const string Path = QuaterniusImportPostprocessor.AthleteRoot + "Athlete.fbx";

        private static GameObject Load()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            Assert.IsNotNull(model, Path + " missing: run tools/blender/athlete_body.py");
            return model;
        }

        [Test]
        public void Avatar_IsValidHumanoid_WithFullLimbMapping()
        {
            var animator = Load().GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator);
            Assert.IsTrue(animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman);
            var human = ((ModelImporter)AssetImporter.GetAtPath(Path)).humanDescription.human;
            foreach (var bone in new[] { "Hips", "Spine", "Chest", "Neck", "Head", "LeftUpperArm", "LeftHand", "RightUpperLeg",
                                         "RightFoot", "LeftToes", "Left Index Proximal" })
                Assert.IsTrue(human.Any(h => h.humanName == bone), bone + " not mapped");
        }

        [Test]
        public void Lods_AreSkinned_CarryBodyMap_AndFitTheBudget()
        {
            var model = Load();
            var lod = model.GetComponentInChildren<LODGroup>();
            Assert.IsNotNull(lod, "no LOD group");
            var lods = lod.GetLODs();
            Assert.AreEqual(3, lods.Length);
            Assert.AreEqual(0f, lods[lods.Length - 1].screenRelativeTransitionHeight, "a player must never be culled");
            int[] maxTris = { 25000, 10000, 4000 };
            for (int i = 0; i < lods.Length; i++)
            {
                var smr = lods[i].renderers.Single() as SkinnedMeshRenderer;
                Assert.IsNotNull(smr);
                var mesh = smr.sharedMesh;
                Assert.IsTrue(mesh.name.StartsWith(AthleteHeadSpace.MeshPrefix), mesh.name);
                Assert.IsTrue(mesh.HasVertexAttribute(VertexAttribute.TexCoord2) && mesh.HasVertexAttribute(VertexAttribute.TexCoord3),
                    mesh.name + " lacks the kit body map");
                Assert.LessOrEqual(mesh.triangles.Length / 3, maxTris[i], mesh.name);
                Assert.AreEqual(KitBodyMapPostprocessor.BodyMaterialPath, AssetDatabase.GetAssetPath(smr.sharedMaterial));
            }
            Assert.GreaterOrEqual(lods[0].renderers[0].GetComponent<SkinnedMeshRenderer>().sharedMesh.triangles.Length / 3, 15000);
        }

        [Test]
        public void Height_IsTheGaitReferenceHeight()
        {
            var model = Load();
            var go = Object.Instantiate(model);
            try
            {
                // Vertex extent in world space (renderer bounds are padded for skinning).
                var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>().First();
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var v in smr.sharedMesh.vertices)
                {
                    float y = smr.transform.TransformPoint(v).y;
                    lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
                }
                Assert.AreEqual(1.83f, hi - lo, 0.01f, "player height (m)");
                Assert.AreEqual(0f, lo, 0.01f, "feet on the ground");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Detection_TellsAthleteFromMannequin()
        {
            Assert.IsTrue(AthleteBody.IsAthlete(Load()));
            var mannequin = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Animations/Quaternius/UAL2/UAL2_Standard.fbx");
            Assert.IsFalse(AthleteBody.IsAthlete(mannequin));
        }

        [Test]
        public void MannequinHeadMapping_IsANearIdentityRotation()
        {
            AthleteBody.MannequinHeadToAthlete(out var p, out var r);
            Assert.Less(Quaternion.Angle(Quaternion.identity, r), 3f);
            // The mannequin head centre lands on the athlete head centre.
            var mCentre = new Vector3(MannequinHeadSpace.CentreBoneX, MannequinHeadSpace.CentreBoneY, MannequinHeadSpace.CentreBoneZ);
            var aCentre = new Vector3(AthleteHeadSpace.CentreBoneX, AthleteHeadSpace.CentreBoneY, AthleteHeadSpace.CentreBoneZ);
            Assert.Less(Vector3.Distance(p + r * mCentre, aCentre), 1e-4f);
        }
    }
}
