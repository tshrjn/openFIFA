using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using OpenFifa.Gameplay;
using OpenFifa.Core;
using System.IO;

namespace OpenFifa.Editor
{
    public static class ScreenshotCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private static string OutputDir => Path.Combine(
            Application.dataPath, "..", "..", "screenshots");

        public static void CaptureAll()
        {
            Directory.CreateDirectory(OutputDir);

            // Create a fresh scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Build the pitch
            var pitchRoot = new GameObject("PitchRoot");
            var pitchBuilder = pitchRoot.AddComponent<PitchBuilder>();
            var pitchConfig = new PitchConfigData();
            pitchBuilder.BuildPitch(pitchConfig);

            // Add directional light
            var lightGo = new GameObject("DirectionalLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.9f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Create ball
            var ball = CreateBall(new Vector3(0f, 0.2f, 0f));

            // Create a player
            var player = CreatePlayer(new Vector3(-3f, 0.5f, 2f));

            // Create a second player
            var player2 = CreatePlayer(new Vector3(5f, 0.5f, -1f));

            // === Screenshot 1: Broadcast / TV angle (full pitch overview) ===
            var cam1 = CreateCamera("BroadcastCam",
                new Vector3(0f, 25f, -22f),
                Quaternion.Euler(50f, 0f, 0f),
                45f);
            CaptureScreenshot(cam1, "01_broadcast_view.png");
            Object.DestroyImmediate(cam1.gameObject);

            // === Screenshot 2: Sideline view ===
            var cam2 = CreateCamera("SidelineCam",
                new Vector3(-30f, 8f, 0f),
                Quaternion.Euler(20f, 90f, 0f),
                50f);
            CaptureScreenshot(cam2, "02_sideline_view.png");
            Object.DestroyImmediate(cam2.gameObject);

            // === Screenshot 3: Close-up on center (ball + players) ===
            var cam3 = CreateCamera("CenterCloseup",
                new Vector3(-5f, 4f, -5f),
                Quaternion.Euler(30f, 40f, 0f),
                40f);
            CaptureScreenshot(cam3, "03_center_closeup.png");
            Object.DestroyImmediate(cam3.gameObject);

            // === Screenshot 4: Goal area view ===
            float halfLength = pitchConfig.PitchLength / 2f;
            var cam4 = CreateCamera("GoalAreaCam",
                new Vector3(halfLength + 5f, 5f, 8f),
                Quaternion.Euler(20f, -150f, 0f),
                50f);
            CaptureScreenshot(cam4, "04_goal_area.png");
            Object.DestroyImmediate(cam4.gameObject);

            // === Screenshot 5: Bird's eye / top-down ===
            var cam5 = CreateCamera("BirdsEyeCam",
                new Vector3(0f, 40f, 0f),
                Quaternion.Euler(90f, 0f, 0f),
                50f);
            CaptureScreenshot(cam5, "05_birds_eye.png");
            Object.DestroyImmediate(cam5.gameObject);

            Debug.Log($"[ScreenshotCapture] All screenshots saved to: {OutputDir}");
            EditorApplication.Exit(0);
        }

        private static Camera CreateCamera(string name, Vector3 position, Quaternion rotation, float fov)
        {
            var go = new GameObject(name);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.4f, 0.6f, 0.9f); // Sky blue
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            go.transform.position = position;
            go.transform.rotation = rotation;
            return cam;
        }

        private static void CaptureScreenshot(Camera camera, string filename)
        {
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            string path = Path.Combine(OutputDir, filename);
            File.WriteAllBytes(path, bytes);
            Debug.Log($"[ScreenshotCapture] Saved: {path} ({bytes.Length / 1024}KB)");

            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        private static Material CreateColorMaterial(Color color)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");

            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }

        private static GameObject CreateBall(Vector3 position)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            ball.transform.position = position;
            ball.transform.localScale = Vector3.one * 0.22f; // radius 0.11
            var renderer = ball.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateColorMaterial(Color.white);
            return ball;
        }

        private static GameObject CreatePlayer(Vector3 position)
        {
            var player = new GameObject("Player");
            player.transform.position = position;

            // Body (capsule)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(player.transform);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            var bodyRenderer = body.GetComponent<MeshRenderer>();
            bodyRenderer.sharedMaterial = CreateColorMaterial(new Color(0.2f, 0.4f, 0.9f));

            // Head (sphere)
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(player.transform);
            head.transform.localPosition = new Vector3(0f, 2f, 0f);
            head.transform.localScale = Vector3.one * 0.4f;
            var headRenderer = head.GetComponent<MeshRenderer>();
            headRenderer.sharedMaterial = CreateColorMaterial(new Color(0.9f, 0.75f, 0.6f));

            return player;
        }
    }
}
