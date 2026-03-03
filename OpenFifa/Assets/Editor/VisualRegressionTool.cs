using UnityEngine;
using UnityEditor;
using OpenFifa.Core;
using System.Collections.Generic;
using System.IO;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Editor window for visual regression testing.
    /// Provides UI for capturing baselines, running comparisons,
    /// viewing diff visualizations, and exporting reports.
    /// </summary>
    public class VisualRegressionTool : EditorWindow
    {
        private VisualRegressionConfig _config;
        private ComparisonAlgorithm _selectedAlgorithm = ComparisonAlgorithm.PixelDiff;
        private float _passThreshold = 0.98f;
        private float _maxDiffPct = 2f;
        private string _baselineVersion = "v1";
        private Vector2 _scrollPosition;
        private RegressionReport _lastReport;
        private bool _showResults;
        private bool _showDiffVisualization;
        private int _selectedResultIndex = -1;
        private Texture2D _diffTexture;

        [MenuItem("OpenFifa/Visual Regression")]
        public static void ShowWindow()
        {
            var window = GetWindow<VisualRegressionTool>("Visual Regression");
            window.minSize = new Vector2(500f, 600f);
        }

        private void OnEnable()
        {
            _config = new VisualRegressionConfig();
        }

        private void OnDisable()
        {
            if (_diffTexture != null)
            {
                DestroyImmediate(_diffTexture);
                _diffTexture = null;
            }
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.LabelField("Visual Regression Testing", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawConfigSection();
            EditorGUILayout.Space(10);
            DrawCaptureSection();
            EditorGUILayout.Space(10);
            DrawComparisonSection();
            EditorGUILayout.Space(10);

            if (_showResults && _lastReport != null)
            {
                DrawResultsPanel();
                EditorGUILayout.Space(10);
            }

            if (_showDiffVisualization && _diffTexture != null)
            {
                DrawDiffVisualization();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawConfigSection()
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                _baselineVersion = EditorGUILayout.TextField("Baseline Version", _baselineVersion);
                _selectedAlgorithm = (ComparisonAlgorithm)EditorGUILayout.EnumPopup("Algorithm", _selectedAlgorithm);
                _passThreshold = EditorGUILayout.Slider("Pass Threshold", _passThreshold, 0f, 1f);
                _maxDiffPct = EditorGUILayout.Slider("Max Diff %", _maxDiffPct, 0f, 100f);

                EditorGUILayout.LabelField($"Resolution: {_config.CaptureWidth}x{_config.CaptureHeight}");
                EditorGUILayout.LabelField($"Checkpoints: {_config.CameraCheckpoints.Count}");
                EditorGUILayout.LabelField($"Baseline Path: {_config.BaselinePath}");
            }
        }

        private void DrawCaptureSection()
        {
            EditorGUILayout.LabelField("Capture", EditorStyles.boldLabel);

            if (GUILayout.Button("Capture Baselines", GUILayout.Height(30)))
            {
                CaptureBaselines();
            }

            string baselineDir = GetBaselineDirectory();
            if (Directory.Exists(baselineDir))
            {
                var files = Directory.GetFiles(baselineDir, "*.png");
                EditorGUILayout.HelpBox(
                    $"Baseline directory exists with {files.Length} file(s).\n{baselineDir}",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No baselines captured yet. Click 'Capture Baselines' to create initial baselines.",
                    MessageType.Warning);
            }
        }

        private void DrawComparisonSection()
        {
            EditorGUILayout.LabelField("Comparison", EditorStyles.boldLabel);

            if (GUILayout.Button("Run Comparison", GUILayout.Height(30)))
            {
                RunComparison();
            }

            if (_lastReport != null && GUILayout.Button("Export Report to JSON"))
            {
                ExportReport();
            }
        }

        private void DrawResultsPanel()
        {
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);

            float passRate = _lastReport.GetPassRate();
            string statusMsg = $"Pass Rate: {passRate:P1} — " +
                              $"{_lastReport.PassedCount} passed, {_lastReport.FailedCount} failed " +
                              $"out of {_lastReport.TotalComparisons}";

            var msgType = _lastReport.FailedCount == 0 ? MessageType.Info : MessageType.Error;
            EditorGUILayout.HelpBox(statusMsg, msgType);

            for (int i = 0; i < _lastReport.Results.Count; i++)
            {
                var result = _lastReport.Results[i];
                string icon = result.Result.Passed ? "[PASS]" : "[FAIL]";
                string label = $"{icon} {result.ScreenshotName} — " +
                              $"Similarity: {result.Result.Similarity:F3}, " +
                              $"Diff: {result.Result.DiffPercentage:F2}%";

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(label);
                if (GUILayout.Button("View Diff", GUILayout.Width(80)))
                {
                    _selectedResultIndex = i;
                    GenerateDiffTexture(result.ScreenshotName);
                    _showDiffVisualization = true;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawDiffVisualization()
        {
            EditorGUILayout.LabelField("Diff Visualization", EditorStyles.boldLabel);

            if (_diffTexture != null)
            {
                float aspect = (float)_diffTexture.width / _diffTexture.height;
                float displayWidth = EditorGUIUtility.currentViewWidth - 40f;
                float displayHeight = displayWidth / aspect;
                var rect = GUILayoutUtility.GetRect(displayWidth, displayHeight);
                GUI.DrawTexture(rect, _diffTexture, ScaleMode.ScaleToFit);
            }

            if (GUILayout.Button("Close Diff View"))
            {
                _showDiffVisualization = false;
                if (_diffTexture != null)
                {
                    DestroyImmediate(_diffTexture);
                    _diffTexture = null;
                }
            }
        }

        private void CaptureBaselines()
        {
            string baselineDir = GetBaselineDirectory();
            Directory.CreateDirectory(baselineDir);

            BaselineCaptureUtility.CaptureAllBaselines(_config, baselineDir);

            AssetDatabase.Refresh();
            Debug.Log($"[VisualRegressionTool] Baselines captured to: {baselineDir}");
        }

        private void RunComparison()
        {
            string baselineDir = GetBaselineDirectory();
            if (!Directory.Exists(baselineDir))
            {
                Debug.LogError("[VisualRegressionTool] No baseline directory found. Capture baselines first.");
                return;
            }

            string currentDir = Path.Combine(Application.dataPath, "..", "..", "screenshots");
            if (!Directory.Exists(currentDir))
            {
                Debug.LogError("[VisualRegressionTool] No current screenshots found. Run ScreenshotCapture first.");
                return;
            }

            var thresholds = new ComparisonThresholds(_passThreshold, _maxDiffPct);
            var runner = new RegressionTestRunner(
                _selectedAlgorithm, thresholds, _config.PerChannelDiffThreshold);

            var baselines = LoadScreenshots(baselineDir);
            var currentShots = LoadScreenshots(currentDir);

            _lastReport = runner.RunBatch(
                baselines, currentShots,
                _config.CaptureWidth, _config.CaptureHeight);

            _showResults = true;

            string status = _lastReport.FailedCount == 0 ? "ALL PASSED" : $"{_lastReport.FailedCount} FAILED";
            Debug.Log($"[VisualRegressionTool] Comparison complete: {status}");
        }

        private void ExportReport()
        {
            if (_lastReport == null) return;

            string path = EditorUtility.SaveFilePanel(
                "Export Regression Report", "", "regression_report.json", "json");

            if (string.IsNullOrEmpty(path)) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"timestamp\": {_lastReport.Timestamp},");
            sb.AppendLine($"  \"totalComparisons\": {_lastReport.TotalComparisons},");
            sb.AppendLine($"  \"passedCount\": {_lastReport.PassedCount},");
            sb.AppendLine($"  \"failedCount\": {_lastReport.FailedCount},");
            sb.AppendLine($"  \"passRate\": {_lastReport.GetPassRate():F4},");
            sb.AppendLine("  \"results\": [");

            for (int i = 0; i < _lastReport.Results.Count; i++)
            {
                var r = _lastReport.Results[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"name\": \"{r.ScreenshotName}\",");
                sb.AppendLine($"      \"passed\": {r.Result.Passed.ToString().ToLower()},");
                sb.AppendLine($"      \"similarity\": {r.Result.Similarity:F6},");
                sb.AppendLine($"      \"diffPercentage\": {r.Result.DiffPercentage:F4},");
                sb.AppendLine($"      \"diffPixelCount\": {r.Result.DiffPixelCount}");
                sb.Append("    }");
                if (i < _lastReport.Results.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[VisualRegressionTool] Report exported to: {path}");
        }

        private Dictionary<string, byte[]> LoadScreenshots(string directory)
        {
            var result = new Dictionary<string, byte[]>();
            if (!Directory.Exists(directory)) return result;

            foreach (var filePath in Directory.GetFiles(directory, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(filePath);
                var pngBytes = File.ReadAllBytes(filePath);
                // Decode PNG to raw RGBA using Texture2D
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(pngBytes))
                {
                    result[name] = tex.GetRawTextureData();
                }
                DestroyImmediate(tex);
            }

            return result;
        }

        private void GenerateDiffTexture(string screenshotName)
        {
            if (_diffTexture != null)
            {
                DestroyImmediate(_diffTexture);
                _diffTexture = null;
            }

            string baselineDir = GetBaselineDirectory();
            string currentDir = Path.Combine(Application.dataPath, "..", "..", "screenshots");

            string baselinePath = Path.Combine(baselineDir, screenshotName + ".png");
            string currentPath = Path.Combine(currentDir, screenshotName + ".png");

            if (!File.Exists(baselinePath) || !File.Exists(currentPath))
            {
                Debug.LogWarning("[VisualRegressionTool] Could not find both images for diff visualization.");
                return;
            }

            var texA = new Texture2D(2, 2);
            var texB = new Texture2D(2, 2);
            texA.LoadImage(File.ReadAllBytes(baselinePath));
            texB.LoadImage(File.ReadAllBytes(currentPath));

            int w = texA.width;
            int h = texA.height;

            if (w != texB.width || h != texB.height)
            {
                Debug.LogWarning("[VisualRegressionTool] Image dimensions do not match.");
                DestroyImmediate(texA);
                DestroyImmediate(texB);
                return;
            }

            var pixelsA = texA.GetPixels32();
            var pixelsB = texB.GetPixels32();

            _diffTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var diffPixels = new Color32[pixelsA.Length];

            int threshold = _config.PerChannelDiffThreshold;
            for (int i = 0; i < pixelsA.Length; i++)
            {
                int dr = Mathf.Abs(pixelsA[i].r - pixelsB[i].r);
                int dg = Mathf.Abs(pixelsA[i].g - pixelsB[i].g);
                int db = Mathf.Abs(pixelsA[i].b - pixelsB[i].b);

                if (dr > threshold || dg > threshold || db > threshold)
                {
                    // Highlight diff in red with intensity based on magnitude
                    byte intensity = (byte)Mathf.Min(255, (dr + dg + db) * 2);
                    diffPixels[i] = new Color32(intensity, 0, 0, 255);
                }
                else
                {
                    // Show baseline at reduced opacity
                    diffPixels[i] = new Color32(
                        (byte)(pixelsA[i].r / 3),
                        (byte)(pixelsA[i].g / 3),
                        (byte)(pixelsA[i].b / 3),
                        255);
                }
            }

            _diffTexture.SetPixels32(diffPixels);
            _diffTexture.Apply();

            DestroyImmediate(texA);
            DestroyImmediate(texB);
        }

        private string GetBaselineDirectory()
        {
            return Path.Combine(
                Application.dataPath,
                _config.BaselinePath,
                _baselineVersion);
        }
    }
}
