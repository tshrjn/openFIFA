using System.Collections.Generic;
using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Procedural hair caps for the mannequin head (styles with volume: <see cref="HairStyle.Curly"/>,
    /// <see cref="HairStyle.Afro"/> and <see cref="HairStyle.Tied"/>; shorter styles are painted by the kit shader). The cap is an ellipsoid shell
    /// around the cranium, cut at the same hairline the shader paints, built once per style (shared mesh) in the
    /// head bone's space and parented to the head bone, so it follows head animation without skinning.
    /// One shared URP Lit material per hair colour (SRP Batcher friendly). Under 1k triangles per cap.
    ///
    /// Head constants are the Quaternius mannequin's, as logged by KitBodyMapPostprocessor ("head-bone space").
    /// </summary>
    public static class HairCapBuilder
    {
        /// <summary>Head-bone-space position of the kit-space point (0, 0.201, -0.012) (head centre incl. neck).</summary>
        private static readonly Vector3 HeadCentreBone = new Vector3(0f, 0.1194f, -0.0068f);
        private const float HeadCentreKitY = 0.201f;
        private const float HeadCentreKitZ = -0.012f;
        private static readonly Vector3 KitUpInBone = new Vector3(0f, 0.999f, 0.037f);
        private static readonly Vector3 KitFrontInBone = new Vector3(0f, -0.037f, 0.999f);

        /// <summary>Cranium ellipsoid in kit space (centre y/z, radii x/y/z), metres.</summary>
        private static readonly Vector3 CraniumCentre = new Vector3(0f, 0.240f, -0.012f);
        private static readonly Vector3 CraniumRadii = new Vector3(0.090f, 0.106f, 0.121f);

        private static readonly Dictionary<HairStyle, Mesh> Meshes = new Dictionary<HairStyle, Mesh>();
        private static readonly Dictionary<Color32, Material> Materials = new Dictionary<Color32, Material>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int CullId = Shader.PropertyToID("_Cull");

        /// <summary>True for styles that need a hair mesh.</summary>
        public static bool NeedsMesh(HairStyle style) =>
            style == HairStyle.Curly || style == HairStyle.Tied || style == HairStyle.Afro;

        /// <summary>
        /// Adds (or replaces) the hair cap under the model's head bone. Returns the renderer, or null when the style
        /// is painted only or the model has no humanoid head bone.
        /// </summary>
        public static MeshRenderer Attach(GameObject model, HairStyle style, Color colour)
        {
            if (model == null) return null;
            var head = FindHead(model);
            if (head == null) return null;
            var old = head.Find("HairCap");
            if (old != null)
            {
                if (Application.isPlaying) Object.Destroy(old.gameObject);
                else Object.DestroyImmediate(old.gameObject);
            }
            if (!NeedsMesh(style)) return null;
            var material = GetMaterial(colour);
            if (material == null) return null;

            var go = new GameObject("HairCap");
            go.transform.SetParent(head, false);
            go.AddComponent<MeshFilter>().sharedMesh = GetMesh(style);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return mr;
        }

        private static Transform FindHead(GameObject model)
        {
            var animator = model.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                var t = animator.GetBoneTransform(HumanBodyBones.Head);
                if (t != null) return t;
            }
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == "DEF-head" || t.name == "head" || t.name == "Head") return t;
            return null;
        }

        private static Material GetMaterial(Color colour)
        {
            Color32 key = colour;
            if (Materials.TryGetValue(key, out var m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;
            m = new Material(shader) { name = "Hair_" + ColorUtility.ToHtmlStringRGB(colour), hideFlags = HideFlags.DontSave };
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, colour);
            if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, 0.28f);
            if (m.HasProperty(CullId)) m.SetFloat(CullId, 0f); // both faces: the cap is a thin open shell
            Materials[key] = m;
            return m;
        }

        /// <summary>Shared mesh for a style (built on first use).</summary>
        public static Mesh GetMesh(HairStyle style)
        {
            if (Meshes.TryGetValue(style, out var mesh) && mesh != null) return mesh;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            if (style == HairStyle.Curly)
            {
                AddShell(verts, normals, tris, 0.012f, 0.012f);
            }
            else if (style == HairStyle.Afro)
            {
                AddShell(verts, normals, tris, 0.028f, 0.016f);
            }
            else
            {
                AddShell(verts, normals, tris, 0.005f, 0f);
                AddEllipsoid(verts, normals, tris, new Vector3(0f, 0.300f, -0.125f), new Vector3(0.034f, 0.030f, 0.032f), 12, 8);
            }
            mesh = new Mesh { name = "HairCap_" + style, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            Meshes[style] = mesh;
            return mesh;
        }

        /// <summary>Hairline height (kit space) at a point, matching KitPaintHead in OpenFifaKitSurface.hlsl.</summary>
        public static float Hairline(float x, float z)
        {
            float nz = (z - HeadCentreKitZ) / 0.118f;
            float hl = Mathf.Lerp(0.150f, 0.288f, Smooth(-0.55f, 0.75f, nz));
            float ax = Mathf.Abs(x);
            return Mathf.Min(hl, Mathf.Lerp(0.288f, 0.252f, Smooth(0.055f, 0.08f, ax) * Smooth(0.8f, 0.2f, nz)));
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static void AddShell(List<Vector3> verts, List<Vector3> normals, List<int> tris, float offset, float curl)
        {
            const int Lon = 28, Lat = 16;
            int baseIndex = verts.Count;
            var keep = new bool[(Lon + 1) * (Lat + 1)];
            for (int j = 0; j <= Lat; j++)
            {
                float v = j / (float)Lat;                    // 0 top .. 1 bottom
                float phi = v * Mathf.PI * 0.82f;            // stop above the jaw
                for (int i = 0; i <= Lon; i++)
                {
                    float theta = i / (float)Lon * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Sin(phi) * Mathf.Sin(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Cos(theta));
                    var surface = Vector3.Scale(dir, CraniumRadii) + CraniumCentre;
                    var n = new Vector3(dir.x / CraniumRadii.x, dir.y / CraniumRadii.y, dir.z / CraniumRadii.z).normalized;
                    float bump = curl * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(theta * 7f + phi * 3f) * Mathf.Sin(phi * 9f)))
                                 * Mathf.Clamp01(1.2f - v);
                    var p = surface + n * (offset + bump);
                    keep[j * (Lon + 1) + i] = surface.y > Hairline(surface.x, surface.z) - 0.004f;
                    verts.Add(KitToBone(p));
                    normals.Add(KitDirToBone(n));
                }
            }
            for (int j = 0; j < Lat; j++)
            {
                for (int i = 0; i < Lon; i++)
                {
                    int a = j * (Lon + 1) + i, b = a + 1, c = a + Lon + 1, d = c + 1;
                    if (!(keep[a] && keep[b] && keep[c] && keep[d])) continue;
                    tris.Add(baseIndex + a); tris.Add(baseIndex + c); tris.Add(baseIndex + b);
                    tris.Add(baseIndex + b); tris.Add(baseIndex + c); tris.Add(baseIndex + d);
                }
            }
        }

        private static void AddEllipsoid(List<Vector3> verts, List<Vector3> normals, List<int> tris, Vector3 centre, Vector3 radii, int lon, int lat)
        {
            int baseIndex = verts.Count;
            for (int j = 0; j <= lat; j++)
            {
                float phi = j / (float)lat * Mathf.PI;
                for (int i = 0; i <= lon; i++)
                {
                    float theta = i / (float)lon * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Sin(phi) * Mathf.Sin(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Cos(theta));
                    verts.Add(KitToBone(Vector3.Scale(dir, radii) + centre));
                    normals.Add(KitDirToBone(new Vector3(dir.x / radii.x, dir.y / radii.y, dir.z / radii.z).normalized));
                }
            }
            for (int j = 0; j < lat; j++)
            {
                for (int i = 0; i < lon; i++)
                {
                    int a = j * (lon + 1) + i, b = a + 1, c = a + lon + 1, d = c + 1;
                    tris.Add(baseIndex + a); tris.Add(baseIndex + c); tris.Add(baseIndex + b);
                    tris.Add(baseIndex + b); tris.Add(baseIndex + c); tris.Add(baseIndex + d);
                }
            }
        }

        private static Vector3 KitToBone(Vector3 k)
        {
            return HeadCentreBone + Vector3.right * k.x + KitUpInBone * (k.y - HeadCentreKitY) + KitFrontInBone * (k.z - HeadCentreKitZ);
        }

        private static Vector3 KitDirToBone(Vector3 d) => (Vector3.right * d.x + KitUpInBone * d.y + KitFrontInBone * d.z).normalized;
    }
}
