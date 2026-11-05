using System.Collections.Generic;
using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Import-time head sculpt for the Quaternius mannequin (its head is ~176 vertices: flat planes, no nose or brow).
    /// Triangles skinned to the head bone are subdivided (Phong-style midpoints, so the cranium and jaw round off),
    /// then front-of-face vertices are pushed forward or back by smooth volumes in kit space: nose bridge and tip,
    /// brow ridge, eye sockets, cheekbones, lips and chin. Painted features (KitPaintHead) are positioned in the same
    /// kit space, so they stay on the volumes. Neighbouring (neck) triangles are split at the same midpoints, so the
    /// mesh stays conforming (no T-junctions); midpoints on head/neck edges stay on the straight edge and boundary
    /// vertices are never sculpted. <see cref="Apply"/> runs before the kit body-map bake (which maps the new vertices
    /// like any other); <see cref="RecomputeHeadNormals"/> runs after it, because the bake's wide seam weld would
    /// otherwise blur the sculpted features' shading.
    /// Calibrated on UAL2_Standard (<see cref="MannequinHeadSpace"/>); KitBodyMapPostprocessor applies it to the UAL
    /// animation models only. Changing anything here needs <see cref="Version"/> bumped (it feeds the importer version).
    /// </summary>
    public static class HeadSculpt
    {
        /// <summary>Bump on any change that alters the sculpted mesh (part of KitBodyMapPostprocessor.GetVersion).</summary>
        public const uint Version = 2;

        /// <summary>Subdivision passes over the head triangles (each pass splits a triangle into four).</summary>
        public const int Levels = 2;

        /// <summary>Minimum head-bone weight for a vertex to count as head.</summary>
        public const float HeadWeightMin = 0.5f;

        /// <summary>Overall strength of the face relief.</summary>
        public const float ReliefScale = 1.5f;

        private static readonly Vector3 HeadCentreBone = new Vector3(MannequinHeadSpace.CentreBoneX, MannequinHeadSpace.CentreBoneY, MannequinHeadSpace.CentreBoneZ);
        private static readonly Vector3 KitUp = new Vector3(0f, MannequinHeadSpace.UpBoneY, MannequinHeadSpace.UpBoneZ);
        private static readonly Vector3 KitFront = new Vector3(0f, MannequinHeadSpace.FrontBoneY, MannequinHeadSpace.FrontBoneZ);

        /// <summary>Head-bone-space point to kit space.</summary>
        public static Vector3 BoneToKit(Vector3 b)
        {
            var d = b - HeadCentreBone;
            return new Vector3(d.x, Vector3.Dot(d, KitUp) + MannequinHeadSpace.CentreKitY, Vector3.Dot(d, KitFront) + MannequinHeadSpace.CentreKitZ);
        }

        /// <summary>
        /// Forward displacement (metres, along the kit front axis) of the face at a kit-space point.
        /// Zero away from the face.
        /// </summary>
        public static float FaceRelief(Vector3 k)
        {
            float nz = MannequinHeadSpace.Frontness(k.z);
            float front = Smooth(0.25f, 0.65f, nz);
            if (front <= 0f) return 0f;
            float x = k.x, ax = Mathf.Abs(x), y = k.y;
            float d = 0f;
            // Nose: a ridge from the brow line to the tip (deepest at the tip, y ~0.193), with soft alae.
            float noseAlong = Smooth(0.240f, 0.212f, y) * Smooth(0.181f, 0.192f, y);
            d += 0.011f * noseAlong * Mathf.Lerp(0.55f, 1f, Smooth(0.225f, 0.196f, y)) * G(x, 0.0065f + 0.004f * Smooth(0.215f, 0.19f, y));
            d += 0.0025f * G2(ax - 0.0095f, y - 0.190f, 0.006f, 0.004f);
            // Brow ridge and eye sockets.
            d += 0.0045f * G2(ax - 0.030f, y - 0.241f, 0.024f, 0.006f);
            d -= 0.0040f * G2(ax - 0.031f, y - 0.226f, 0.013f, 0.008f);
            // Cheekbones, lips, chin.
            d += 0.0040f * G2(ax - 0.048f, y - 0.206f, 0.013f, 0.011f);
            d += 0.0025f * G2(x, y - 0.162f, 0.017f, 0.006f);
            d += 0.0045f * G2(x, y - 0.124f, 0.015f, 0.011f);
            // Under the lower lip a slight recess.
            d -= 0.0015f * G2(x, y - 0.148f, 0.016f, 0.004f);
            return d * front * ReliefScale;
        }

        /// <summary>Subdivides and sculpts the head of one skinned mesh in place. Returns a summary.</summary>
        public static string Apply(SkinnedMeshRenderer smr)
        {
            var mesh = smr.sharedMesh;
            if (mesh == null || mesh.vertexCount == 0) return "head sculpt skipped (no mesh)";
            if (mesh.blendShapeCount > 0) return "head sculpt skipped (blend shapes)";
            int hi = HeadBoneIndex(smr);
            var bindposes = mesh.bindposes;
            if (hi < 0 || hi >= bindposes.Length) return "head sculpt skipped (no head bone)";
            for (int m = 0; m < mesh.subMeshCount; m++)
                if (mesh.GetTopology(m) != MeshTopology.Triangles) return "head sculpt skipped (non-triangle submesh)";

            var verts = new List<Vector3>(mesh.vertices);
            var normals = new List<Vector3>(mesh.normals);
            var uv0 = new List<Vector2>();
            mesh.GetUVs(0, uv0);
            bool hasUv = uv0.Count == verts.Count;
            var weights = new List<BoneWeight>(mesh.boneWeights);
            if (normals.Count != verts.Count || weights.Count != verts.Count) return "head sculpt skipped (missing normals or weights)";
            var subTris = new List<int[]>();
            for (int m = 0; m < mesh.subMeshCount; m++) subTris.Add(mesh.GetTriangles(m));
            int startVerts = verts.Count;

            float HeadW(BoneWeight w) =>
                (w.boneIndex0 == hi ? w.weight0 : 0f) + (w.boneIndex1 == hi ? w.weight1 : 0f) +
                (w.boneIndex2 == hi ? w.weight2 : 0f) + (w.boneIndex3 == hi ? w.weight3 : 0f);
            var isHead = new List<bool>(verts.Count);
            for (int i = 0; i < verts.Count; i++) isHead.Add(HeadW(weights[i]) >= HeadWeightMin);
            // Vertices on any non-head triangle are boundary: never moved by the subdivision or the sculpt.
            var pinned = new List<bool>(new bool[verts.Count]);
            foreach (var tris in subTris)
                for (int t = 0; t + 2 < tris.Length; t += 3)
                    if (!(isHead[tris[t]] && isHead[tris[t + 1]] && isHead[tris[t + 2]]))
                        pinned[tris[t]] = pinned[tris[t + 1]] = pinned[tris[t + 2]] = true;

            for (int level = 0; level < Levels; level++)
            {
                var smoothN = PositionNormals(verts, normals);
                // Edges of non-head triangles (rebuilt each pass: neck triangles are split conformingly below, so
                // their sub-edges are in this set on the next pass) keep their midpoints on the straight edge.
                var straight = new HashSet<long>();
                foreach (var tris in subTris)
                    for (int t = 0; t + 2 < tris.Length; t += 3)
                        if (!(isHead[tris[t]] && isHead[tris[t + 1]] && isHead[tris[t + 2]]))
                        {
                            straight.Add(Key(tris[t], tris[t + 1])); straight.Add(Key(tris[t + 1], tris[t + 2])); straight.Add(Key(tris[t + 2], tris[t]));
                        }
                var mid = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = Key(a, b);
                    if (mid.TryGetValue(key, out int idx)) return idx;
                    var p = 0.5f * (verts[a] + verts[b]);
                    bool flat = straight.Contains(key);
                    if (!flat)
                    {
                        Vector3 na = smoothN[a], nb = smoothN[b];
                        var pa = p - Vector3.Dot(p - verts[a], na) * na;
                        var pb = p - Vector3.Dot(p - verts[b], nb) * nb;
                        p = Vector3.Lerp(p, 0.5f * (pa + pb), 0.75f);
                    }
                    idx = verts.Count;
                    verts.Add(p);
                    normals.Add((normals[a] + normals[b]).normalized);
                    if (hasUv) uv0.Add(0.5f * (uv0[a] + uv0[b]));
                    var w = Blend(weights[a], weights[b]);
                    weights.Add(w);
                    isHead.Add(HeadW(w) >= HeadWeightMin);
                    pinned.Add(flat);
                    mid[key] = idx;
                    return idx;
                }
                for (int m = 0; m < subTris.Count; m++)
                {
                    var tris = subTris[m];
                    var outTris = new List<int>(tris.Length * 2);
                    // Head triangles first, so every head edge has its midpoint before the neighbours are split.
                    for (int t = 0; t + 2 < tris.Length; t += 3)
                    {
                        int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                        if (!(isHead[a] && isHead[b] && isHead[c])) continue;
                        int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                        outTris.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
                    }
                    // Other triangles: split exactly the edges that got a midpoint (conforming, no T-junctions).
                    for (int t = 0; t + 2 < tris.Length; t += 3)
                    {
                        int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                        if (isHead[a] && isHead[b] && isHead[c]) continue;
                        SplitConforming(a, b, c, mid, outTris);
                    }
                    subTris[m] = outTris.ToArray();
                }
            }

            // Sculpt: kit-space relief along the kit front axis, mapped back to mesh space.
            var toBone = bindposes[hi];
            var toMesh = toBone.inverse;
            var frontMesh = toMesh.MultiplyVector(KitFront).normalized;
            int sculpted = 0;
            for (int i = 0; i < verts.Count; i++)
            {
                if (!isHead[i] || pinned[i]) continue;
                // Boundary vertices are pinned; the weight fade keeps the sculpt soft towards the neck.
                float w = Mathf.Clamp01((HeadW(weights[i]) - HeadWeightMin) / (1f - HeadWeightMin) * 2f);
                float d = FaceRelief(BoneToKit(toBone.MultiplyPoint3x4(verts[i]))) * w;
                if (Mathf.Abs(d) < 1e-6f) continue;
                verts[i] += frontMesh * d;
                sculpted++;
            }

            // No mesh.Clear(): on an import-time mesh with Read/Write off it leaves the mesh unwritable for the rest
            // of the import. Growing the vertex arrays in place keeps it writable.
            if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            if (hasUv) mesh.SetUVs(0, uv0);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bindposes;
            mesh.subMeshCount = subTris.Count;
            for (int m = 0; m < subTris.Count; m++) mesh.SetTriangles(subTris[m], m);
            if (hasUv) mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return $"head sculpt: {startVerts} -> {verts.Count} vertices, {sculpted} sculpted";
        }

        private static void SplitConforming(int a, int b, int c, Dictionary<long, int> mid, List<int> outTris)
        {
            int[] v = { a, b, c };
            int[] m = new int[3];
            int count = 0;
            for (int e = 0; e < 3; e++)
            {
                m[e] = mid.TryGetValue(Key(v[e], v[(e + 1) % 3]), out int idx) ? idx : -1;
                if (m[e] >= 0) count++;
            }
            if (count == 0) { outTris.Add(a); outTris.Add(b); outTris.Add(c); return; }
            if (count == 3)
            {
                outTris.AddRange(new[] { a, m[0], m[2], m[0], b, m[1], m[2], m[1], c, m[0], m[1], m[2] });
                return;
            }
            // Rotate so edge 0 (v0-v1) is split; for two splits, also edge 1 (v1-v2).
            int r = 0;
            if (count == 1) { while (m[r] < 0) r++; }
            else { while (!(m[r] >= 0 && m[(r + 1) % 3] >= 0)) r++; }
            int p0 = v[r], p1 = v[(r + 1) % 3], p2 = v[(r + 2) % 3];
            int m01 = m[r], m12 = m[(r + 1) % 3];
            if (count == 1)
            {
                outTris.AddRange(new[] { p0, m01, p2, m01, p1, p2 });
            }
            else
            {
                outTris.AddRange(new[] { m01, p1, m12, p0, m01, m12, p0, m12, p2 });
            }
        }

        /// <summary>
        /// Smooth, area-weighted normals for the sculpted head vertices (welded by position), from the final positions.
        /// Run after the kit bake: its 18 mm seam weld would otherwise blur the sculpted features' shading. Boundary
        /// vertices (shared with neck triangles) keep the weld's normals so the neck seam stays continuous.
        /// Returns the number of normals written.
        /// </summary>
        public static int RecomputeHeadNormals(SkinnedMeshRenderer smr)
        {
            var mesh = smr.sharedMesh;
            int hi = HeadBoneIndex(smr);
            if (mesh == null || hi < 0) return 0;
            var verts = mesh.vertices;
            var normals = mesh.normals;
            var weights = mesh.boneWeights;
            if (normals.Length != verts.Length || weights.Length != verts.Length) return 0;
            var isHead = new bool[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var w = weights[i];
                float h = (w.boneIndex0 == hi ? w.weight0 : 0f) + (w.boneIndex1 == hi ? w.weight1 : 0f) +
                          (w.boneIndex2 == hi ? w.weight2 : 0f) + (w.boneIndex3 == hi ? w.weight3 : 0f);
                isHead[i] = h >= HeadWeightMin;
            }
            var boundary = new bool[verts.Length];
            var faceSum = new Vector3[verts.Length];
            for (int m = 0; m < mesh.subMeshCount; m++)
            {
                var tris = mesh.GetTriangles(m);
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    if (!(isHead[a] && isHead[b] && isHead[c])) { boundary[a] = boundary[b] = boundary[c] = true; continue; }
                    var fn = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                    faceSum[a] += fn; faceSum[b] += fn; faceSum[c] += fn;
                }
            }
            var groups = new Dictionary<Vector3Int, Vector3>();
            for (int i = 0; i < verts.Length; i++)
            {
                if (!isHead[i]) continue;
                var key = Vector3Int.RoundToInt(verts[i] * 4000f);
                groups[key] = (groups.TryGetValue(key, out var s) ? s : Vector3.zero) + faceSum[i];
            }
            int written = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                if (!isHead[i] || boundary[i]) continue;
                var s = groups[Vector3Int.RoundToInt(verts[i] * 4000f)];
                if (s.sqrMagnitude > 1e-14f && Vector3.Dot(s, normals[i]) > 0f) { normals[i] = s.normalized; written++; }
            }
            mesh.normals = normals;
            if (mesh.uv != null && mesh.uv.Length == verts.Length) mesh.RecalculateTangents();
            return written;
        }

        private static int HeadBoneIndex(SkinnedMeshRenderer smr)
        {
            var bones = smr.bones;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && (bones[i].name == "Head" || bones[i].name == "head" || bones[i].name == "DEF-head")) return i;
            return -1;
        }

        private static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        private static List<Vector3> PositionNormals(List<Vector3> verts, List<Vector3> normals)
        {
            var sum = new Dictionary<Vector3Int, Vector3>();
            for (int i = 0; i < verts.Count; i++)
            {
                var key = Vector3Int.RoundToInt(verts[i] * 4000f);
                sum[key] = (sum.TryGetValue(key, out var s) ? s : Vector3.zero) + normals[i];
            }
            var outN = new List<Vector3>(verts.Count);
            for (int i = 0; i < verts.Count; i++)
            {
                var s = sum[Vector3Int.RoundToInt(verts[i] * 4000f)];
                outN.Add(s.sqrMagnitude > 1e-12f ? s.normalized : normals[i]);
            }
            return outN;
        }

        /// <summary>Average of two skin weights, keeping the four largest influences, renormalised.</summary>
        private static BoneWeight Blend(BoneWeight a, BoneWeight b)
        {
            var acc = new Dictionary<int, float>();
            void Add(int i, float w) { if (w > 0f) acc[i] = (acc.TryGetValue(i, out var v) ? v : 0f) + 0.5f * w; }
            Add(a.boneIndex0, a.weight0); Add(a.boneIndex1, a.weight1); Add(a.boneIndex2, a.weight2); Add(a.boneIndex3, a.weight3);
            Add(b.boneIndex0, b.weight0); Add(b.boneIndex1, b.weight1); Add(b.boneIndex2, b.weight2); Add(b.boneIndex3, b.weight3);
            var list = new List<KeyValuePair<int, float>>(acc);
            list.Sort((x, y) => y.Value.CompareTo(x.Value));
            float total = 0f;
            for (int i = 0; i < list.Count && i < 4; i++) total += list[i].Value;
            var r = new BoneWeight();
            if (total <= 0f) return a;
            if (list.Count > 0) { r.boneIndex0 = list[0].Key; r.weight0 = list[0].Value / total; }
            if (list.Count > 1) { r.boneIndex1 = list[1].Key; r.weight1 = list[1].Value / total; }
            if (list.Count > 2) { r.boneIndex2 = list[2].Key; r.weight2 = list[2].Value / total; }
            if (list.Count > 3) { r.boneIndex3 = list[3].Key; r.weight3 = list[3].Value / total; }
            return r;
        }

        private static float G(float x, float s) => Mathf.Exp(-0.5f * x * x / (s * s));
        private static float G2(float x, float y, float sx, float sy) => Mathf.Exp(-0.5f * (x * x / (sx * sx) + y * y / (sy * sy)));

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
