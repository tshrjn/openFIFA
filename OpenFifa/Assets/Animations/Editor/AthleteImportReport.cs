using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Batch report on the Blender athlete body import (tools/blender/athlete_body.py): Humanoid avatar validity and
    /// bone mapping, LOD group, renderers, kit body-map channels and bounds.
    /// -executeMethod OpenFifa.Editor.AthleteImportReport.Run [-athletePath Assets/...fbx]
    /// </summary>
    public static class AthleteImportReport
    {
        public const string DefaultPath = QuaterniusImportPostprocessor.AthleteRoot + "Athlete.fbx";

        public static void Run()
        {
            string path = DefaultPath;
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-athletePath");
            if (i >= 0 && i + 1 < args.Length) path = args[i + 1];
            Debug.Log(Report(path));
            Debug.Log("[AthleteImport] humanScale athlete " + HumanScale(path).ToString("F4") + ", mannequin "
                      + HumanScale(FootballAnimationBuilder.Ual2Path).ToString("F4"));
            EditorApplication.Exit(0);
        }

        /// <summary>Animator.humanScale of a model instance (hips height of the avatar in metres-ish units).</summary>
        public static float HumanScale(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return -1f;
            var go = Object.Instantiate(prefab);
            try
            {
                var animator = go.GetComponentInChildren<Animator>();
                return animator != null && animator.isHuman ? animator.humanScale : -1f;
            }
            finally { Object.DestroyImmediate(go); }
        }

        public static string Report(string path)
        {
            var sb = new StringBuilder("[AthleteImport] " + path + "\n");
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) return sb.Append("  not imported").ToString();
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            sb.AppendLine($"  animationType {importer.animationType}, avatarSetup {importer.avatarSetup}, globalScale {importer.globalScale}, useFileScale {importer.useFileScale}, fileScale {importer.fileScale}");
            var animator = go.GetComponentInChildren<Animator>();
            var avatar = animator != null ? animator.avatar : null;
            sb.AppendLine($"  avatar {(avatar != null ? avatar.name : "none")} valid {avatar != null && avatar.isValid} human {avatar != null && avatar.isHuman}");
            foreach (var hb in importer.humanDescription.human.OrderBy(h => h.humanName))
                sb.Append($"{hb.humanName}={hb.boneName} ");
            sb.AppendLine();
            var lod = go.GetComponentInChildren<LODGroup>();
            if (lod != null)
                foreach (var l in lod.GetLODs())
                    sb.AppendLine($"  LOD h={l.screenRelativeTransitionHeight:F3}: {string.Join(",", l.renderers.Select(r => r.name))}");
            else sb.AppendLine("  no LODGroup");
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = smr.sharedMesh;
                sb.AppendLine($"  SMR {smr.name}: {m.vertexCount} verts, {m.triangles.Length / 3} tris, submeshes {m.subMeshCount}, bones {smr.bones.Length}, " +
                              $"uv2 {m.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord2)}, uv3 {m.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord3)}, " +
                              $"bounds {m.bounds.center} {m.bounds.size}, mats {string.Join(",", smr.sharedMaterials.Select(x => x != null ? x.name : "null"))}, quality {smr.quality}");
            }
            var t = go.transform;
            sb.AppendLine($"  root rot {t.localRotation.eulerAngles} scale {t.localScale}; first child {(t.childCount > 0 ? t.GetChild(0).name + " " + t.GetChild(0).localRotation.eulerAngles : "-")}");
            return sb.ToString();
        }
    }
}
