using System.Collections.Generic;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Procedural ears for the mannequin head (the Quaternius head has none). Each ear is a flat elliptical plate
    /// whose back and top rim stand slightly off the skull, in the head bone's space and parented to the head bone
    /// like <see cref="HairCapBuilder"/> caps, with one shared URP Lit material per skin tone.
    /// <see cref="VertexCount"/> vertices and <see cref="TriangleCount"/> triangles per pair.
    /// </summary>
    public static class EarBuilder
    {
        private const int Around = 18, Rings = 5;

        /// <summary>Vertices in the ear-pair mesh.</summary>
        public const int VertexCount = 2 * (Rings + 1) * (Around + 1);

        /// <summary>Triangles in the ear-pair mesh.</summary>
        public const int TriangleCount = 2 * Rings * Around * 2;

        private static Mesh _mesh;
        private static readonly Dictionary<Color32, Material> Materials = new Dictionary<Color32, Material>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int CullId = Shader.PropertyToID("_Cull");

        /// <summary>Adds (or replaces) the ears under the model's head bone. Returns the renderer or null.</summary>
        public static MeshRenderer Attach(GameObject model, Color skin)
        {
            var head = HairCapBuilder.FindHeadBone(model);
            if (head == null) return null;
            var old = head.Find("Ears");
            if (old != null)
            {
                if (Application.isPlaying) Object.Destroy(old.gameObject);
                else Object.DestroyImmediate(old.gameObject);
            }
            var material = GetMaterial(skin);
            if (material == null) return null;
            var go = new GameObject("Ears");
            go.transform.SetParent(head, false);
            go.AddComponent<MeshFilter>().sharedMesh = GetMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return mr;
        }

        private static Material GetMaterial(Color skin)
        {
            Color32 key = skin;
            if (Materials.TryGetValue(key, out var m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;
            m = new Material(shader) { name = "Ear_" + ColorUtility.ToHtmlStringRGB(skin), hideFlags = HideFlags.DontSave };
            // Warmer and redder than the face, and a little brighter: URP Lit lacks the kit shader's skin fill.
            var c = new Color(Mathf.Clamp01(skin.r * 1.17f), Mathf.Clamp01(skin.g * 1.07f), Mathf.Clamp01(skin.b * 1.03f), 1f);
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, 0.32f);
            if (m.HasProperty(CullId)) m.SetFloat(CullId, 0f);
            Materials[key] = m;
            return m;
        }

        /// <summary>Shared ear-pair mesh (kit space mapped into head-bone space).</summary>
        public static Mesh GetMesh()
        {
            if (_mesh != null) return _mesh;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (int side = -1; side <= 1; side += 2) AddEar(verts, normals, tris, side);
            _mesh = new Mesh { name = "Ears", hideFlags = HideFlags.DontSave };
            _mesh.SetVertices(verts);
            _mesh.SetNormals(normals);
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateBounds();
            return _mesh;
        }

        // Ear: a flat elliptical plate, 6 cm tall and 3.4 cm deep, clear of the skull (|x| 0.087 m), its back and top
        // rim standing up to ~1.3 cm off, centred behind the jaw line at eye-to-nose height.
        private static void AddEar(List<Vector3> verts, List<Vector3> normals, List<int> tris, int side)
        {
            var centre = new Vector3(side * 0.087f, 0.212f, -0.022f);
            int start = verts.Count;
            for (int r = 0; r <= Rings; r++)
            {
                float u = r / (float)Rings;                         // 0 = bowl centre .. 1 = helix rim
                for (int a = 0; a <= Around; a++)
                {
                    float t = a / (float)Around * Mathf.PI * 2f;
                    float y = Mathf.Sin(t) * 0.030f * u;
                    float z = Mathf.Cos(t) * 0.017f * u;
                    // Rim stands off the head, more at the back/top; the lobe and front stay close.
                    float back = Mathf.Clamp01(0.5f - 0.5f * Mathf.Cos(t) + 0.3f * Mathf.Sin(t));
                    float off = 0.005f * u + 0.008f * back * u * u;
                    var k = centre + new Vector3(side * (off + 0.003f * Mathf.Sin(u * Mathf.PI)), y, z - 0.004f * u);
                    verts.Add(HairCapBuilder.KitToBone(k));
                    var n = new Vector3(side * 1f, -0.25f * y / 0.03f, -0.4f * z / 0.017f * u).normalized;
                    normals.Add(HairCapBuilder.KitDirToBone(n));
                }
            }
            for (int r = 0; r < Rings; r++)
            {
                for (int a = 0; a < Around; a++)
                {
                    int i0 = start + r * (Around + 1) + a, i1 = i0 + 1, i2 = i0 + Around + 1, i3 = i2 + 1;
                    if (side > 0) { tris.Add(i0); tris.Add(i2); tris.Add(i1); tris.Add(i1); tris.Add(i2); tris.Add(i3); }
                    else { tris.Add(i0); tris.Add(i1); tris.Add(i2); tris.Add(i1); tris.Add(i3); tris.Add(i2); }
                }
            }
        }
    }
}
