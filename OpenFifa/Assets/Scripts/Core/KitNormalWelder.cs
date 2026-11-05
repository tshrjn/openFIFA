using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Smooths shading across mesh seams: every vertex gets the average normal of all vertices within
    /// <c>radius</c> (rest pose) that face the same way (dot &gt; <c>minDot</c>). Used on the segmented mannequin,
    /// whose separate joint rings and body pieces otherwise show hard lines at elbows, shoulders and knees.
    /// Pure C#; O(n) with a spatial hash.
    /// </summary>
    public static class KitNormalWelder
    {
        /// <summary>
        /// Returns the welded normals (xyz per vertex, normalized). <paramref name="normals"/> is not modified.
        /// </summary>
        public static float[] Weld(IReadOnlyList<SimVec3> positions, float[] normals, float radius, float minDot = 0.2f)
        {
            if (positions == null) throw new ArgumentNullException(nameof(positions));
            if (normals == null || normals.Length < positions.Count * 3) throw new ArgumentException("normals too small", nameof(normals));
            int n = positions.Count;
            var result = new float[n * 3];
            if (radius <= 0f)
            {
                Array.Copy(normals, result, n * 3);
                return result;
            }

            float cell = radius;
            var grid = new Dictionary<long, List<int>>();
            for (int i = 0; i < n; i++)
            {
                long key = Key(Cell(positions[i].X, cell), Cell(positions[i].Y, cell), Cell(positions[i].Z, cell));
                if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>(4);
                list.Add(i);
            }

            float r2 = radius * radius;
            for (int i = 0; i < n; i++)
            {
                var p = positions[i];
                float nx = normals[i * 3], ny = normals[i * 3 + 1], nz = normals[i * 3 + 2];
                float sx = 0f, sy = 0f, sz = 0f;
                int cx = Cell(p.X, cell), cy = Cell(p.Y, cell), cz = Cell(p.Z, cell);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!grid.TryGetValue(Key(cx + dx, cy + dy, cz + dz), out var list)) continue;
                    foreach (int j in list)
                    {
                        if ((positions[j] - p).SqrMagnitude > r2) continue;
                        float jx = normals[j * 3], jy = normals[j * 3 + 1], jz = normals[j * 3 + 2];
                        if (jx * nx + jy * ny + jz * nz < minDot) continue;
                        sx += jx; sy += jy; sz += jz;
                    }
                }
                float len = (float)Math.Sqrt(sx * sx + sy * sy + sz * sz);
                if (len < 1e-6f) { sx = nx; sy = ny; sz = nz; len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz); if (len < 1e-6f) len = 1f; }
                result[i * 3] = sx / len;
                result[i * 3 + 1] = sy / len;
                result[i * 3 + 2] = sz / len;
            }
            return result;
        }

        private static int Cell(float v, float size) => (int)Math.Floor(v / size);

        private static long Key(int x, int y, int z) =>
            ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (long)(z & 0x1FFFFF);
    }
}
