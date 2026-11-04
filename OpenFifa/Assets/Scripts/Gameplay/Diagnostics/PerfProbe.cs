using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Per-frame performance recorder for any player build. Enabled by <c>-perfProbe &lt;file.csv&gt;</c>, with optional
    /// <c>-perfProbeWarmup</c> (frames to skip, default 120) and <c>-perfProbeFrames</c> (frames to record, default 1800).
    /// Writes one CSV row per frame (CPU frame time, main/render thread time, draw calls, batches, set-pass calls,
    /// triangles, vertices, shadow casters, visible skinned meshes, GC bytes) and a summary file next to it, then quits.
    /// Counters a build does not expose read as -1. Never active unless the flag is passed.
    /// </summary>
    public sealed class PerfProbe : MonoBehaviour
    {
        private static readonly (ProfilerCategory cat, string name, string col)[] Counters =
        {
            (ProfilerCategory.Internal, "Main Thread", "main_ms"),
            (ProfilerCategory.Render, "Render Thread", "render_ms"),
            (ProfilerCategory.Render, "Draw Calls Count", "draws"),
            (ProfilerCategory.Render, "Batches Count", "batches"),
            (ProfilerCategory.Render, "SetPass Calls Count", "setpass"),
            (ProfilerCategory.Render, "Triangles Count", "tris"),
            (ProfilerCategory.Render, "Vertices Count", "verts"),
            (ProfilerCategory.Render, "Shadow Casters Count", "shadow_casters"),
            (ProfilerCategory.Render, "Visible Skinned Meshes Count", "skinned"),
            (ProfilerCategory.Memory, "GC Allocated In Frame", "gc_bytes"),
        };

        private string _path;
        private int _warmup = 120;
        private int _frames = 1800;
        private int _frame;
        private ProfilerRecorder[] _rec;
        private double[][] _data;
        private float[] _dt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var args = Environment.GetCommandLineArgs();
            string path = null;
            int warmup = 120, frames = 1800;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-perfProbe") path = args[i + 1];
                else if (args[i] == "-perfProbeWarmup") int.TryParse(args[i + 1], out warmup);
                else if (args[i] == "-perfProbeFrames") int.TryParse(args[i + 1], out frames);
            }
            if (string.IsNullOrEmpty(path)) return;
            var go = new GameObject("[PerfProbe]");
            DontDestroyOnLoad(go);
            var p = go.AddComponent<PerfProbe>();
            p._path = path;
            p._warmup = Mathf.Max(0, warmup);
            p._frames = Mathf.Max(1, frames);
        }

        private void Start()
        {
            _rec = new ProfilerRecorder[Counters.Length];
            for (int i = 0; i < Counters.Length; i++)
                _rec[i] = ProfilerRecorder.StartNew(Counters[i].cat, Counters[i].name, 1);
            _data = new double[Counters.Length][];
            for (int i = 0; i < Counters.Length; i++) _data[i] = new double[_frames];
            _dt = new float[_frames];
            Debug.Log($"[PerfProbe] recording {_frames} frames after {_warmup} warm-up frames -> {_path}");
        }

        private void LateUpdate()
        {
            _frame++;
            int k = _frame - _warmup - 1;
            if (k < 0) return;
            if (k >= _frames) { Finish(); return; }
            _dt[k] = Time.unscaledDeltaTime * 1000f;
            for (int i = 0; i < _rec.Length; i++)
            {
                if (!_rec[i].Valid) { _data[i][k] = -1; continue; }
                double v = _rec[i].LastValue;
                if (_rec[i].UnitType == ProfilerMarkerDataUnit.TimeNanoseconds) v /= 1e6;
                _data[i][k] = v;
            }
        }

        private void Finish()
        {
            enabled = false;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("frame,frame_ms");
            foreach (var c in Counters) sb.Append(',').Append(c.col);
            sb.Append('\n');
            for (int k = 0; k < _frames; k++)
            {
                sb.Append(k).Append(',').Append(_dt[k].ToString("F3", inv));
                for (int i = 0; i < Counters.Length; i++) sb.Append(',').Append(_data[i][k].ToString("0.###", inv));
                sb.Append('\n');
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path)) ?? ".");
            File.WriteAllText(_path, sb.ToString());

            var sum = new StringBuilder();
            sum.Append($"device={SystemInfo.deviceModel} gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType} ");
            sum.Append($"screen={Screen.width}x{Screen.height} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}\n");
            sum.Append(Line("frame_ms", _dt));
            for (int i = 0; i < Counters.Length; i++) sum.Append(Line(Counters[i].col, _data[i]));
            File.WriteAllText(Path.ChangeExtension(_path, ".summary.txt"), sum.ToString());
            File.WriteAllText(Path.ChangeExtension(_path, ".renderers.txt"), RendererInventory());
            foreach (var r in _rec) r.Dispose();
            Debug.Log("[PerfProbe] done\n" + sum);
            Application.Quit(0);
        }

        /// <summary>Every active renderer grouped by mesh: count, triangles, shadow mode, materials, skinning quality.</summary>
        private static string RendererInventory()
        {
            var groups = new System.Collections.Generic.Dictionary<string, (int n, long tris, string info)>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled) continue;
                Mesh mesh = null;
                string extra = "";
                if (r is SkinnedMeshRenderer smr) { mesh = smr.sharedMesh; extra = $" skinned quality={smr.quality} bones={smr.bones.Length} offscreen={smr.updateWhenOffscreen}"; }
                else if (r.TryGetComponent<MeshFilter>(out var mf)) mesh = mf.sharedMesh;
                long tris = 0;
                if (mesh != null) for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetIndexCount(s) / 3;
                string key = (mesh != null ? mesh.name : r.GetType().Name) + " | " + r.shadowCastingMode + (r.receiveShadows ? " recv" : "") +
                             $" mats={r.sharedMaterials.Length}{extra} shader={(r.sharedMaterial != null ? r.sharedMaterial.shader.name : "-")}";
                groups.TryGetValue(key, out var g);
                groups[key] = (g.n + 1, g.tris + tris, key);
            }
            var list = new System.Collections.Generic.List<(int n, long tris, string info)>(groups.Values);
            list.Sort((a, b) => b.tris.CompareTo(a.tris));
            var sb = new StringBuilder();
            long total = 0;
            foreach (var g in list) total += g.tris;
            sb.Append($"total_tris={total} groups={list.Count}\n");
            foreach (var g in list) sb.Append($"{g.tris,9} tris  x{g.n,-4} {g.info}\n");
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights) sb.Append($"light {l.name} type={l.type} shadows={l.shadows} res={l.shadowResolution} range={l.range} enabled={l.isActiveAndEnabled}\n");
            return sb.ToString();
        }

        private static string Line(string name, float[] v)
        {
            var d = new double[v.Length];
            for (int i = 0; i < v.Length; i++) d[i] = v[i];
            return Line(name, d);
        }

        private static string Line(string name, double[] v)
        {
            var s = (double[])v.Clone();
            Array.Sort(s);
            double mean = 0;
            foreach (var x in s) mean += x;
            mean /= s.Length;
            double P(double q) => s[Math.Min(s.Length - 1, (int)(q * s.Length))];
            return string.Format(CultureInfo.InvariantCulture, "{0,-15} mean={1,10:0.###} p50={2,10:0.###} p95={3,10:0.###} p99={4,10:0.###} max={5,10:0.###}\n",
                name, mean, P(0.5), P(0.95), P(0.99), s[s.Length - 1]);
        }
    }
}
