using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Per-frame performance recorder for any player build. Inert unless the player is started with
    /// <c>-perfProbe &lt;file.csv&gt;</c> (a relative path goes under Application.persistentDataPath). Options:
    /// <c>-perfProbeWarmup N</c> frames skipped after the start trigger (default 120), <c>-perfProbeFrames N</c> frames
    /// recorded (default 1800, max 36000), <c>-perfProbeScene name</c> to start when that scene is active. Without a
    /// scene name it starts when a <see cref="SinglePlayer.SinglePlayerMatchController"/> has a running session, or on
    /// the first frame if the scene has none.
    ///
    /// Each row is the most recent completed frame: counters are read at the start of the next frame, and
    /// FrameTimingManager timings (CPU main, CPU render, GPU) lag by a few frames. Those need "Frame Timing Stats"
    /// in Player Settings and read -1 without it; so do counters a build does not expose. interval_ms is the wall time
    /// between frames, which equals the frame-rate target whenever the frame finishes early.
    /// Writes the CSV, a percentile summary (with pacing settings) and a renderer inventory, then quits.
    /// </summary>
    public sealed class PerfProbe : MonoBehaviour
    {
        public const int MaxFrames = 36000;

        private static readonly (ProfilerCategory cat, string name, string col)[] Counters =
        {
            (ProfilerCategory.Internal, "Main Thread", "main_marker_ms"),
            (ProfilerCategory.Render, "Draw Calls Count", "draws"),
            (ProfilerCategory.Render, "Batches Count", "batches"),
            (ProfilerCategory.Render, "SetPass Calls Count", "setpass"),
            (ProfilerCategory.Render, "Triangles Count", "tris"),
            (ProfilerCategory.Render, "Vertices Count", "verts"),
            (ProfilerCategory.Render, "Shadow Casters Count", "shadow_casters"),
            (ProfilerCategory.Render, "Visible Skinned Meshes Count", "skinned"),
            (ProfilerCategory.Memory, "GC Allocated In Frame", "gc_bytes"),
        };

        private static readonly string[] TimingCols = { "interval_ms", "cpu_frame_ms", "cpu_main_ms", "cpu_render_ms", "gpu_ms" };

        private string _path;
        private string _scene;
        private int _warmup = 120;
        private int _frames = 1800;
        private bool _started;
        private int _sinceStart;
        private int _count;
        private bool _finished;
        private ProfilerRecorder[] _rec;
        private double[][] _data;
        private readonly FrameTiming[] _timing = new FrameTiming[1];
        private string _warning = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var args = Environment.GetCommandLineArgs();
            string path = null, scene = null;
            int warmup = 120, frames = 1800;
            for (int i = 0; i < args.Length - 1; i++)
            {
                string next = args[i + 1];
                bool value = !string.IsNullOrEmpty(next) && !next.StartsWith("-", StringComparison.Ordinal);
                if (args[i] == "-perfProbe" && value) path = next;
                else if (args[i] == "-perfProbeScene" && value) scene = next;
                else if (args[i] == "-perfProbeWarmup" && int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) && w >= 0) warmup = w;
                else if (args[i] == "-perfProbeFrames" && int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out int f) && f > 0) frames = f;
            }
            if (string.IsNullOrEmpty(path)) return;
            if (!Path.IsPathRooted(path)) path = Path.Combine(Application.persistentDataPath, path);

            var go = new GameObject("[PerfProbe]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            var p = go.AddComponent<PerfProbe>();
            p._path = path;
            p._scene = scene;
            p._warmup = warmup;
            p._frames = Math.Min(frames, MaxFrames);
        }

        private void Start()
        {
            _rec = new ProfilerRecorder[Counters.Length];
            for (int i = 0; i < Counters.Length; i++)
                _rec[i] = ProfilerRecorder.StartNew(Counters[i].cat, Counters[i].name, 1);
            _data = new double[TimingCols.Length + Counters.Length][];
            for (int i = 0; i < _data.Length; i++) _data[i] = new double[_frames];
            if (Time.captureFramerate != 0)
                _warning = $"WARNING: Time.captureFramerate={Time.captureFramerate} (capture mode): frame times are not real-time.";
            if (!FrameTimingManager.IsFeatureEnabled())
                _warning += (_warning.Length > 0 ? " " : "") + "FrameTimingManager disabled (enable Frame Timing Stats): cpu_*/gpu columns read -1.";
            if (_warning.Length > 0) Debug.LogWarning("[PerfProbe] " + _warning);
            Debug.Log($"[PerfProbe] {_frames} frames after {_warmup} warm-up frames -> {_path}");
        }

        private bool ShouldStart()
        {
            if (!string.IsNullOrEmpty(_scene)) return SceneManager.GetActiveScene().name == _scene;
            var c = FindAnyObjectByType<SinglePlayer.SinglePlayerMatchController>();
            return c == null || c.Session != null;
        }

        // Update runs early in the frame: the recorders and timings now hold the previous, completed frame.
        private void Update()
        {
            if (_finished) return;
            FrameTimingManager.CaptureFrameTimings();
            if (!_started)
            {
                _started = ShouldStart();
                return;
            }
            if (++_sinceStart <= _warmup) return;
            if (_count >= _frames)
            {
                Finish();
                return;
            }

            int k = _count++;
            _data[0][k] = Time.unscaledDeltaTime * 1000.0;
            if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
            {
                _data[1][k] = _timing[0].cpuFrameTime;
                _data[2][k] = _timing[0].cpuMainThreadFrameTime;
                _data[3][k] = _timing[0].cpuRenderThreadFrameTime;
                _data[4][k] = _timing[0].gpuFrameTime;
            }
            else
            {
                for (int i = 1; i < TimingCols.Length; i++) _data[i][k] = -1;
            }
            for (int i = 0; i < _rec.Length; i++)
            {
                double v = -1;
                if (_rec[i].Valid && _rec[i].Count > 0)
                {
                    v = _rec[i].LastValue;
                    if (_rec[i].UnitType == ProfilerMarkerDataUnit.TimeNanoseconds) v /= 1e6;
                }
                _data[TimingCols.Length + i][k] = v;
            }
        }

        private void Finish()
        {
            _finished = true;
            int code = 0;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path)) ?? ".");
                File.WriteAllText(_path, Csv());
                string summary = Summary();
                File.WriteAllText(Path.ChangeExtension(_path, ".summary.txt"), summary);
                File.WriteAllText(Path.ChangeExtension(_path, ".renderers.txt"), RendererInventory());
                Debug.Log("[PerfProbe] done\n" + summary);
            }
            catch (Exception e)
            {
                code = 1;
                Debug.LogError("[PerfProbe] could not write results to " + _path + ": " + e);
            }
            finally
            {
                Application.Quit(code);
            }
        }

        private void OnDestroy()
        {
            if (_rec == null) return;
            foreach (var r in _rec) r.Dispose();
            _rec = null;
        }

        private string Name(int i) => i < TimingCols.Length ? TimingCols[i] : Counters[i - TimingCols.Length].col;

        private string Csv()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("frame");
            for (int i = 0; i < _data.Length; i++) sb.Append(',').Append(Name(i));
            sb.Append('\n');
            for (int k = 0; k < _count; k++)
            {
                sb.Append(k);
                for (int i = 0; i < _data.Length; i++) sb.Append(',').Append(_data[i][k].ToString("0.###", inv));
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private string Summary()
        {
            var sb = new StringBuilder();
            sb.Append($"device={SystemInfo.deviceModel} gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType} ");
            sb.Append($"screen={Screen.width}x{Screen.height} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} ");
            sb.Append($"targetFrameRate={Application.targetFrameRate} vSyncCount={QualitySettings.vSyncCount} ");
            sb.Append($"frames={_count} warmup={_warmup} scene={SceneManager.GetActiveScene().name}\n");
            if (_warning.Length > 0) sb.Append(_warning).Append('\n');
            for (int i = 0; i < _data.Length; i++) sb.Append(Line(Name(i), _data[i], _count));
            return sb.ToString();
        }

        private static string Line(string name, double[] v, int n)
        {
            if (n <= 0) return name + " (no samples)\n";
            var s = new double[n];
            Array.Copy(v, s, n);
            Array.Sort(s);
            double mean = 0;
            foreach (var x in s) mean += x;
            mean /= n;
            double P(double q) => s[Math.Min(n - 1, (int)(q * n))];
            return string.Format(CultureInfo.InvariantCulture,
                "{0,-15} mean={1,10:0.###} p50={2,10:0.###} p95={3,10:0.###} p99={4,10:0.###} max={5,10:0.###}\n",
                name, mean, P(0.5), P(0.95), P(0.99), s[n - 1]);
        }

        /// <summary>Every active renderer grouped by mesh: count, triangles, shadow mode, materials, skinning; then lights.</summary>
        private static string RendererInventory()
        {
            var groups = new System.Collections.Generic.Dictionary<string, (int n, long tris)>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled) continue;
                Mesh mesh = null;
                string extra = "";
                if (r is SkinnedMeshRenderer smr)
                {
                    mesh = smr.sharedMesh;
                    extra = $" skinned quality={smr.quality} bones={smr.bones.Length} offscreen={smr.updateWhenOffscreen}";
                }
                else if (r.TryGetComponent<MeshFilter>(out var mf)) mesh = mf.sharedMesh;
                long tris = 0;
                if (mesh != null) for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetIndexCount(s) / 3;
                string key = (mesh != null ? mesh.name : r.GetType().Name) + " | " + r.shadowCastingMode + (r.receiveShadows ? " recv" : "") +
                             $" mats={r.sharedMaterials.Length}{extra} shader={(r.sharedMaterial != null ? r.sharedMaterial.shader.name : "-")}";
                groups.TryGetValue(key, out var g);
                groups[key] = (g.n + 1, g.tris + tris);
            }
            var list = new System.Collections.Generic.List<(string key, int n, long tris)>();
            long total = 0;
            foreach (var kv in groups) { list.Add((kv.Key, kv.Value.n, kv.Value.tris)); total += kv.Value.tris; }
            list.Sort((a, b) => b.tris.CompareTo(a.tris));
            var sb = new StringBuilder($"total_tris={total} groups={list.Count}\n");
            foreach (var g in list) sb.Append($"{g.tris,9} tris  x{g.n,-4} {g.key}\n");
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                sb.Append($"light {l.name} type={l.type} shadows={l.shadows} res={l.shadowResolution} range={l.range} enabled={l.isActiveAndEnabled}\n");
            return sb.ToString();
        }
    }
}
