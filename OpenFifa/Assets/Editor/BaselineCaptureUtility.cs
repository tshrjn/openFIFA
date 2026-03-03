using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using OpenFifa.Core;
using System.IO;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Batch screenshot capture utility for visual regression baselines.
    /// Captures all ScreenshotSpecs from a VisualRegressionConfig with
    /// consistent rendering settings (fixed time, fixed seed, no particles).
    /// Stores baselines in Assets/Tests/Baselines/{version}/.
    /// </summary>
    public static class BaselineCaptureUtility
    {
        /// <summary>
        /// Captures baseline screenshots for all camera checkpoints in the config.
        /// Stores PNG files in the specified output directory.
        /// </summary>
        public static void CaptureAllBaselines(VisualRegressionConfig config, string outputDir)
        {
            if (config == null)
            {
                Debug.LogError("[BaselineCaptureUtility] Config is null.");
                return;
            }

            Directory.CreateDirectory(outputDir);

            // Set consistent rendering state
            SetConsistentRenderingState();

            // Open or create a clean scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Build the pitch for screenshots
            var pitchRoot = new GameObject("PitchRoot");
            var pitchBuilder = pitchRoot.AddComponent<Gameplay.PitchBuilder>();
            var pitchConfig = new PitchConfigData();
            pitchBuilder.BuildPitch(pitchConfig);

            // Add directional light
            var lightGo = new GameObject("DirectionalLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.9f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Ambient light
            RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.65f);

            // Create ball at center
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            ball.transform.position = new Vector3(0f, 0.2f, 0f);
            ball.transform.localScale = Vector3.one * 0.22f;

            // Capture each checkpoint
            foreach (var checkpoint in config.CameraCheckpoints)
            {
                CaptureCheckpoint(checkpoint, config.CaptureWidth, config.CaptureHeight, outputDir);
            }

            // Clean up
            RestoreRenderingState();

            Debug.Log($"[BaselineCaptureUtility] Captured {config.CameraCheckpoints.Count} baselines to: {outputDir}");
        }

        /// <summary>
        /// Captures baselines from a BaselineSet (with full ScreenshotSpec metadata).
        /// </summary>
        public static void CaptureFromBaselineSet(BaselineSet set, string outputDir)
        {
            if (set == null || set.Specs == null)
            {
                Debug.LogError("[BaselineCaptureUtility] BaselineSet or its specs are null.");
                return;
            }

            Directory.CreateDirectory(outputDir);
            SetConsistentRenderingState();

            foreach (var spec in set.Specs)
            {
                if (!VisualRegressionValidator.IsSpecValid(spec))
                {
                    Debug.LogWarning($"[BaselineCaptureUtility] Skipping invalid spec: {spec?.Name ?? "null"}");
                    continue;
                }

                CaptureCheckpoint(
                    spec.CameraAngle,
                    spec.ResolutionWidth,
                    spec.ResolutionHeight,
                    outputDir);
            }

            RestoreRenderingState();

            Debug.Log($"[BaselineCaptureUtility] Captured {set.Specs.Count} baselines for version {set.Version}");
        }

        /// <summary>
        /// Captures a single screenshot from a specific camera checkpoint.
        /// </summary>
        private static void CaptureCheckpoint(
            CameraCheckpoint checkpoint, int width, int height, string outputDir)
        {
            var camGo = new GameObject($"BaselineCam_{checkpoint.Name}");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.18f, 0.25f);
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;

            camGo.transform.position = new Vector3(checkpoint.PosX, checkpoint.PosY, checkpoint.PosZ);
            camGo.transform.rotation = Quaternion.Euler(checkpoint.RotX, checkpoint.RotY, checkpoint.RotZ);

            // Render to texture
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            GL.Flush();

            Texture2D tex = null;
            try
            {
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                byte[] pngBytes = tex.EncodeToPNG();
                string filePath = Path.Combine(outputDir, checkpoint.Name + ".png");
                File.WriteAllBytes(filePath, pngBytes);
                Debug.Log($"[BaselineCaptureUtility] Saved: {filePath} ({pngBytes.Length / 1024}KB)");
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = null;
                if (rt != null) Object.DestroyImmediate(rt);
                if (tex != null) Object.DestroyImmediate(tex);
                Object.DestroyImmediate(camGo);
            }
        }

        /// <summary>
        /// Sets up consistent rendering state for deterministic screenshots.
        /// - Fixed random seed
        /// - Disable particles
        /// - Fixed time step
        /// </summary>
        private static void SetConsistentRenderingState()
        {
            // Fixed random seed for deterministic rendering
            Random.InitState(42);

            // Disable all particle systems in scene
            var particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            foreach (var ps in particles)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            // Fixed time step for physics consistency
            Time.fixedDeltaTime = 0.02f;
        }

        /// <summary>
        /// Restores default rendering state after capture.
        /// </summary>
        private static void RestoreRenderingState()
        {
            Time.fixedDeltaTime = 0.02f;
        }
    }
}
