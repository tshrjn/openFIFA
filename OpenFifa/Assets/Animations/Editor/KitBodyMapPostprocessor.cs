using System;
using System.Collections.Generic;
using OpenFifa.Core;
using UnityEditor;
using UnityEngine;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Bakes the kit body map (<see cref="KitBodyMapBaker"/>) into the skinned meshes of the Quaternius
    /// characters on import: UV2 = (armT, legT, armWeight, legWeight), UV3 = rest-pose kit-space position + 1.
    /// The kit shader's SkinnedBody mapping reads these, so shirt / shorts / socks / boots / skin regions,
    /// patterns and name/number prints follow the skin instead of swimming in object space.
    /// Limbs come from the importer's humanoid mapping (falling back to common bone names).
    /// Bump <see cref="Version"/> when the bake changes so Unity reimports the models.
    /// </summary>
    public sealed class KitBodyMapPostprocessor : AssetPostprocessor
    {
        public const uint Version = 4;

        /// <summary>Mesh UV channel holding (armT, legT, armWeight, legWeight).</summary>
        public const int LimbChannel = 2;

        /// <summary>Mesh UV channel holding the rest-pose kit-space position (w = 1 when baked).</summary>
        public const int BodyChannel = 3;

        /// <summary>
        /// Neutral kit-shader material put on every slot of the baked renderers. Besides giving the undressed
        /// mannequin a plain training kit, it makes player builds keep the UV2/UV3 channels: Unity strips mesh
        /// channels that no shader on the mesh's materials reads (Player Settings > Optimize Mesh Data).
        /// </summary>
        public const string BodyMaterialPath = "Assets/Resources/Kits/OpenFifaKitBody.mat";

        public override uint GetVersion() => Version;

        // After QuaterniusImportPostprocessor (default order 0).
        public override int GetPostprocessOrder() => 50;

        private bool IsOurs =>
            assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
            (assetPath.StartsWith(QuaterniusImportPostprocessor.AnimationsRoot, StringComparison.Ordinal) ||
             assetPath.StartsWith(QuaterniusImportPostprocessor.CharactersRoot, StringComparison.Ordinal));

        private void OnPostprocessModel(GameObject root)
        {
            if (!IsOurs) return;
            var importer = assetImporter as ModelImporter;
            var human = importer != null ? importer.humanDescription.human : null;
            context.DependsOnSourceAsset(BodyMaterialPath);
            var bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string result = Bake(root.transform, smr, human);
                if (result == null) continue;
                Debug.Log($"[KitBodyMap] {assetPath} / {smr.name}: {result}");
                if (bodyMaterial != null && smr.sharedMesh != null
                    && smr.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord3))
                {
                    var mats = new Material[Mathf.Max(1, smr.sharedMesh.subMeshCount)];
                    for (int i = 0; i < mats.Length; i++) mats[i] = bodyMaterial;
                    smr.sharedMaterials = mats;
                }
            }
        }

        /// <summary>Bakes one renderer's mesh in place. Returns a summary, or null when skipped.</summary>
        public static string Bake(Transform root, SkinnedMeshRenderer smr, HumanBone[] human)
        {
            var mesh = smr.sharedMesh;
            if (mesh == null || mesh.vertexCount == 0) return null;
            var bones = smr.bones;
            var bindposes = mesh.bindposes;
            if (bones == null || bones.Length == 0) return "skipped (no bones)";

            var names = HumanBoneNames(human);
            Transform Find(string humanName) => FindBone(root, names, humanName);

            var hips = Find("Hips");
            var neck = Find("Neck") ?? Find("UpperChest") ?? Find("Chest");
            var armL = new[] { Find("LeftUpperArm"), Find("LeftLowerArm"), Find("LeftHand") };
            var armR = new[] { Find("RightUpperArm"), Find("RightLowerArm"), Find("RightHand") };
            var legL = new[] { Find("LeftUpperLeg"), Find("LeftLowerLeg"), Find("LeftFoot"), Find("LeftToes") };
            var legR = new[] { Find("RightUpperLeg"), Find("RightLowerLeg"), Find("RightFoot"), Find("RightToes") };
            if (hips == null || neck == null || Array.IndexOf(armL, null) >= 0 || Array.IndexOf(armR, null) >= 0
                || legL[2] == null || legR[2] == null || legL[0] == null || legR[0] == null || legL[1] == null || legR[1] == null)
            {
                return "skipped (humanoid joints not found)";
            }

            // Rest pose: bones in smr.bones come from the bind poses, others from the import hierarchy.
            var meshToRoot = root.worldToLocalMatrix * smr.transform.localToWorldMatrix;
            var boneIndex = new Dictionary<Transform, int>();
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && !boneIndex.ContainsKey(bones[i])) boneIndex[bones[i]] = i;

            SimVec3 Rest(Transform t)
            {
                Vector3 p;
                if (boneIndex.TryGetValue(t, out int bi) && bi < bindposes.Length)
                    p = meshToRoot.MultiplyPoint3x4(bindposes[bi].inverse.MultiplyPoint3x4(Vector3.zero));
                else
                    p = root.InverseTransformPoint(t.position);
                return new SimVec3(p.x, p.y, p.z);
            }

            SimVec3[] ArmChain(Transform[] a)
            {
                var s = Rest(a[0]); var e = Rest(a[1]); var w = Rest(a[2]);
                return new[] { s, e, w, w + (w - e).Normalized * 0.17f };
            }

            SimVec3[] LegChain(Transform[] l)
            {
                var h = Rest(l[0]); var k = Rest(l[1]); var a = Rest(l[2]);
                var toe = l[3] != null ? Rest(l[3]) : a + (a - k).Normalized * 0.08f;
                var dir = (toe - a);
                dir = new SimVec3(dir.X, 0f, dir.Z).Normalized;
                return new[] { h, k, a, toe + dir * 0.07f };
            }

            var skeleton = new KitBodySkeleton
            {
                Hips = Rest(hips),
                Neck = Rest(neck),
                ArmLeft = ArmChain(armL),
                ArmRight = ArmChain(armR),
                LegLeft = LegChain(legL),
                LegRight = LegChain(legR),
            };

            var limbs = new KitBodyLimb[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                limbs[i] = Classify(bones[i], armL[0], armR[0], legL[0], legR[0]);

            var verts = mesh.vertices;
            var positions = new SimVec3[verts.Length];
            for (int v = 0; v < verts.Length; v++)
            {
                var p = meshToRoot.MultiplyPoint3x4(verts[v]);
                positions[v] = new SimVec3(p.x, p.y, p.z);
            }

            var weights = mesh.boneWeights;
            int[] idx = null;
            float[] w4 = null;
            if (weights != null && weights.Length == verts.Length)
            {
                idx = new int[verts.Length * 4];
                w4 = new float[verts.Length * 4];
                for (int v = 0; v < weights.Length; v++)
                {
                    var bw = weights[v];
                    idx[v * 4] = bw.boneIndex0; w4[v * 4] = bw.weight0;
                    idx[v * 4 + 1] = bw.boneIndex1; w4[v * 4 + 1] = bw.weight1;
                    idx[v * 4 + 2] = bw.boneIndex2; w4[v * 4 + 2] = bw.weight2;
                    idx[v * 4 + 3] = bw.boneIndex3; w4[v * 4 + 3] = bw.weight3;
                }
            }

            var body0 = new float[verts.Length * KitBodyMapBaker.Stride];
            var body1 = new float[verts.Length * KitBodyMapBaker.Stride];
            var frame = KitBodyMapBaker.Bake(skeleton, positions, idx, w4, 4, limbs, body0, body1);

            var uv2 = new List<Vector4>(verts.Length);
            var uv3 = new List<Vector4>(verts.Length);
            int arm = 0, leg = 0;
            for (int v = 0; v < verts.Length; v++)
            {
                int o = v * KitBodyMapBaker.Stride;
                uv2.Add(new Vector4(body0[o], body0[o + 1], body0[o + 2], body0[o + 3]));
                uv3.Add(new Vector4(body1[o], body1[o + 1], body1[o + 2], body1[o + 3]));
                if (body0[o + 2] > 0.5f) arm++;
                else if (body0[o + 3] > 0.5f) leg++;
            }
            mesh.SetUVs(LimbChannel, uv2);
            mesh.SetUVs(BodyChannel, uv3);

            // Optional dump for offline inspection: OPENFIFA_KITBODY_DUMP=<dir> writes one CSV per mesh.
            string dumpDir = Environment.GetEnvironmentVariable("OPENFIFA_KITBODY_DUMP");
            if (!string.IsNullOrEmpty(dumpDir)) Dump(dumpDir, smr, mesh, verts, body0, body1, bones, limbs);

            string headInfo = HeadProfile(root, smr, mesh, verts, body0, body1, frame, Find("Head"), boneIndex, bindposes, meshToRoot);

            var limbCounts = new int[5];
            foreach (var l in limbs) limbCounts[(int)l]++;

            float height = skeleton.Neck.Y - Math.Min(skeleton.LegLeft[2].Y, skeleton.LegRight[2].Y);
            return $"{verts.Length} verts ({arm} arm, {leg} leg), bones torso/armL/armR/legL/legR " +
                   $"{limbCounts[0]}/{limbCounts[1]}/{limbCounts[2]}/{limbCounts[3]}/{limbCounts[4]}, neck-to-ankle {height:F2} m, " +
                   $"torso half width {frame.TorsoHalfWidth:F3} m, half depth {frame.TorsoHalfDepth:F3} m, " +
                   $"up {frame.Up}, right {frame.Right}, front {frame.Forward}, torso centre {frame.TorsoCentre}, floor {frame.FloorY:F3}; {headInfo}";
        }

        /// <summary>
        /// Head ellipsoid (from the vertices above the neck) in the head bone's local space, plus the kit axes in
        /// that space. Logged for MannequinHeadProfile (hair caps are built in head-bone space).
        /// </summary>
        private static string HeadProfile(Transform root, SkinnedMeshRenderer smr, Mesh mesh, Vector3[] verts,
            float[] body0, float[] body1, KitBodyFrame frame, Transform head, Dictionary<Transform, int> boneIndex,
            Matrix4x4[] bindposes, Matrix4x4 meshToRoot)
        {
            if (head == null || !boneIndex.TryGetValue(head, out int hi) || hi >= bindposes.Length) return "head: n/a";
            var toHead = bindposes[hi];
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = -min;
            var kmin = min; var kmax = max;
            int n = 0;
            for (int v = 0; v < verts.Length; v++)
            {
                int o = v * KitBodyMapBaker.Stride;
                if (body0[o + 2] > 0.5f || body1[o + 1] < 0.06f) continue;
                var k = new Vector3(body1[o], body1[o + 1], body1[o + 2]);
                kmin = Vector3.Min(kmin, k); kmax = Vector3.Max(kmax, k);
                n++;
            }
            if (n == 0) return "head: no vertices";
            // Kit-space centre -> root space -> mesh space -> head bone space.
            var kc = 0.5f * (kmin + kmax);
            var c = frame.TorsoCentre;
            Vector3 KitToRoot(Vector3 k)
            {
                var kk = new SimVec3(k.x + c.X, k.y, k.z + c.Z);
                var p = frame.Origin + frame.Right * kk.X + frame.Up * kk.Y + frame.Forward * kk.Z;
                return new Vector3(p.X, p.Y, p.Z);
            }
            var rootToMesh = meshToRoot.inverse;
            Vector3 KitPointToHead(Vector3 k) => toHead.MultiplyPoint3x4(rootToMesh.MultiplyPoint3x4(KitToRoot(k)));
            Vector3 KitDirToHead(SimVec3 d) => toHead.MultiplyVector(rootToMesh.MultiplyVector(new Vector3(d.X, d.Y, d.Z))).normalized;
            var hc = KitPointToHead(kc);
            float scale = toHead.MultiplyVector(rootToMesh.MultiplyVector(Vector3.up)).magnitude;
            return $"head kit centre {kc.x:F3},{kc.y:F3},{kc.z:F3} half {0.5f * (kmax.x - kmin.x):F3},{0.5f * (kmax.y - kmin.y):F3},{0.5f * (kmax.z - kmin.z):F3}; " +
                   $"head-bone space: centre {hc.x:F4},{hc.y:F4},{hc.z:F4} right {V(KitDirToHead(frame.Right))} up {V(KitDirToHead(frame.Up))} front {V(KitDirToHead(frame.Forward))} metre {scale:F4}";
        }

        private static string V(Vector3 v) => $"{v.x:F3},{v.y:F3},{v.z:F3}";

        private static void Dump(string dir, SkinnedMeshRenderer smr, Mesh mesh, Vector3[] verts, float[] body0,
            float[] body1, Transform[] bones, KitBodyLimb[] limbs)
        {
            try
            {
                System.IO.Directory.CreateDirectory(dir);
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("x,y,z,armT,legT,armW,legW,sub");
                var sub = new int[verts.Length];
                for (int m = 0; m < mesh.subMeshCount; m++)
                    foreach (int i in mesh.GetIndices(m)) sub[i] = m;
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                for (int v = 0; v < verts.Length; v++)
                {
                    int o = v * KitBodyMapBaker.Stride;
                    sb.Append(string.Format(inv, "{0:F4},{1:F4},{2:F4},{3:F3},{4:F3},{5:F3},{6:F3},{7}\n",
                        body1[o], body1[o + 1], body1[o + 2], body0[o], body0[o + 1], body0[o + 2], body0[o + 3], sub[v]));
                }
                string name = smr.name + "_" + mesh.vertexCount;
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, name + ".csv"), sb.ToString());
                var tri = new System.Text.StringBuilder();
                for (int m = 0; m < mesh.subMeshCount; m++)
                {
                    var idx = mesh.GetIndices(m);
                    for (int i = 0; i + 2 < idx.Length; i += 3) tri.AppendLine(idx[i] + "," + idx[i + 1] + "," + idx[i + 2] + "," + m);
                }
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, name + "_tris.csv"), tri.ToString());
                var b = new System.Text.StringBuilder();
                for (int i = 0; i < bones.Length; i++)
                    b.AppendLine((bones[i] != null ? bones[i].name : "null") + "," + limbs[i] + "," + (bones[i] != null && bones[i].parent != null ? bones[i].parent.name : ""));
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, name + "_bones.csv"), b.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KitBodyMap] dump failed: " + e.Message);
            }
        }

        private static KitBodyLimb Classify(Transform bone, Transform armL, Transform armR, Transform legL, Transform legR)
        {
            for (var t = bone; t != null; t = t.parent)
            {
                if (t == armL) return KitBodyLimb.ArmLeft;
                if (t == armR) return KitBodyLimb.ArmRight;
                if (t == legL) return KitBodyLimb.LegLeft;
                if (t == legR) return KitBodyLimb.LegRight;
            }
            return KitBodyLimb.Torso;
        }

        /// <summary>Human bone name -> rig bone name, from the importer, plus Unreal-style fallbacks.</summary>
        private static Dictionary<string, string> HumanBoneNames(HumanBone[] human)
        {
            var map = new Dictionary<string, string>
            {
                { "Hips", "pelvis" }, { "Chest", "spine_02" }, { "UpperChest", "spine_03" }, { "Neck", "neck_01" },
                { "LeftUpperArm", "upperarm_l" }, { "LeftLowerArm", "lowerarm_l" }, { "LeftHand", "hand_l" },
                { "RightUpperArm", "upperarm_r" }, { "RightLowerArm", "lowerarm_r" }, { "RightHand", "hand_r" },
                { "LeftUpperLeg", "thigh_l" }, { "LeftLowerLeg", "calf_l" }, { "LeftFoot", "foot_l" }, { "LeftToes", "ball_l" },
                { "RightUpperLeg", "thigh_r" }, { "RightLowerLeg", "calf_r" }, { "RightFoot", "foot_r" }, { "RightToes", "ball_r" },
            };
            if (human != null)
                foreach (var hb in human)
                    if (!string.IsNullOrEmpty(hb.humanName) && !string.IsNullOrEmpty(hb.boneName))
                        map[hb.humanName] = hb.boneName;
            return map;
        }

        private static Transform FindBone(Transform root, Dictionary<string, string> names, string humanName)
        {
            return names.TryGetValue(humanName, out string bone) ? FindDeep(root, bone) : null;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = FindDeep(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
