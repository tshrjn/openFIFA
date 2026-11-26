using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Accumulates flat-shaded quads for one (sub)mesh. Each quad takes an explicit normal: the winding is flipped to
    /// face it, and the normal is written as given (so a roof underside can be tilted to catch the light).
    /// </summary>
    public sealed class StadiumMeshBuffer
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<int> Triangles = new List<int>();

        public int TriangleCount => Triangles.Count / 3;

        public void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, Vector2 uv)
            => Quad(p0, p1, p2, p3, normal, uv, uv, uv, uv);

        /// <summary>Quad p0-p1-p2-p3 (in order around its edge) facing <paramref name="normal"/>.</summary>
        public void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            int b = Vertices.Count;
            Vertices.Add(p0); Vertices.Add(p1); Vertices.Add(p2); Vertices.Add(p3);
            var n = normal.normalized;
            Normals.Add(n); Normals.Add(n); Normals.Add(n); Normals.Add(n);
            Uvs.Add(uv0); Uvs.Add(uv1); Uvs.Add(uv2); Uvs.Add(uv3);
            // Unity: the front face is the one Cross(b - a, c - a) points at.
            var face = Vector3.Cross(p1 - p0, p2 - p0);
            if (face.sqrMagnitude < 1e-12f) face = Vector3.Cross(p2 - p0, p3 - p0);
            if (Vector3.Dot(face, normal) >= 0f)
            {
                Triangles.Add(b); Triangles.Add(b + 1); Triangles.Add(b + 2);
                Triangles.Add(b); Triangles.Add(b + 2); Triangles.Add(b + 3);
            }
            else
            {
                Triangles.Add(b); Triangles.Add(b + 2); Triangles.Add(b + 1);
                Triangles.Add(b); Triangles.Add(b + 3); Triangles.Add(b + 2);
            }
        }

        /// <summary>Builds one mesh whose submeshes are the given buffers (in material order).</summary>
        public static Mesh Combine(string name, params StadiumMeshBuffer[] parts)
        {
            var mesh = new Mesh { name = name };
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            foreach (var p in parts)
            {
                v.AddRange(p.Vertices);
                n.AddRange(p.Normals);
                uv.AddRange(p.Uvs);
            }
            mesh.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = parts.Length;
            int offset = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                var tris = new int[parts[i].Triangles.Count];
                for (int k = 0; k < tris.Length; k++) tris[k] = parts[i].Triangles[k] + offset;
                mesh.SetTriangles(tris, i, false);
                offset += parts[i].Vertices.Count;
            }
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
