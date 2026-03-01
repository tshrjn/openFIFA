using UnityEngine;
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

        private static readonly Color HomeColor = new Color(0.2f, 0.4f, 0.9f);
        private static readonly Color AwayColor = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color SkinColor = new Color(0.9f, 0.75f, 0.6f);

        public static void CaptureAll()
        {
            Directory.CreateDirectory(OutputDir);

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

            // Ambient light
            RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.65f);

            // Create goal posts at both ends
            float halfLength = pitchConfig.PitchLength / 2f;
            CreateGoalFrame(new Vector3(halfLength, 0f, 0f));
            CreateGoalFrame(new Vector3(-halfLength, 0f, 0f));

            // Create ball at center
            CreateBall(new Vector3(0f, 0.2f, 0f));

            // Spawn Rush 4v4 formation — home team (blue)
            var formation = FormationLayoutData.CreateRush4v4();
            var homePositions = formation.GetWorldPositions(isHomeTeam: true, pitchLength: pitchConfig.PitchLength);
            foreach (var pos in homePositions)
                CreatePlayer(new Vector3(pos.x, 0f, pos.z), HomeColor);

            // Spawn Rush 4v4 formation — away team (red)
            var awayPositions = formation.GetWorldPositions(isHomeTeam: false, pitchLength: pitchConfig.PitchLength);
            foreach (var pos in awayPositions)
                CreatePlayer(new Vector3(pos.x, 0f, pos.z), AwayColor);

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
            cam.backgroundColor = new Color(0.15f, 0.18f, 0.25f); // Dark arena background
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
            ball.transform.localScale = Vector3.one * 0.22f;
            ball.GetComponent<MeshRenderer>().sharedMaterial = CreateColorMaterial(Color.white);
            return ball;
        }

        private static GameObject CreatePlayer(Vector3 position, Color teamColor)
        {
            var player = new GameObject("Player");
            player.transform.position = position;

            // Body (capsule)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(player.transform);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            body.GetComponent<MeshRenderer>().sharedMaterial = CreateColorMaterial(teamColor);

            // Head (sphere)
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(player.transform);
            head.transform.localPosition = new Vector3(0f, 2f, 0f);
            head.transform.localScale = Vector3.one * 0.4f;
            head.GetComponent<MeshRenderer>().sharedMaterial = CreateColorMaterial(SkinColor);

            return player;
        }

        private static void CreateGoalFrame(Vector3 position)
        {
            float goalWidth = 5f; // PitchConfigData default
            float goalHeight = 2.44f;
            float postRadius = 0.06f;
            float halfWidth = goalWidth / 2f;

            var root = new GameObject("GoalFrame");
            root.transform.position = position;

            // Left post
            var leftPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftPost.name = "LeftPost";
            leftPost.transform.SetParent(root.transform);
            leftPost.transform.localPosition = new Vector3(0f, goalHeight / 2f, -halfWidth);
            leftPost.transform.localScale = new Vector3(postRadius * 2f, goalHeight / 2f, postRadius * 2f);
            leftPost.GetComponent<MeshRenderer>().sharedMaterial = CreateColorMaterial(Color.white);

            // Right post
            var rightPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightPost.name = "RightPost";
            rightPost.transform.SetParent(root.transform);
            rightPost.transform.localPosition = new Vector3(0f, goalHeight / 2f, halfWidth);
            rightPost.transform.localScale = new Vector3(postRadius * 2f, goalHeight / 2f, postRadius * 2f);
            rightPost.GetComponent<MeshRenderer>().sharedMaterial = CreateColorMaterial(Color.white);

            // Crossbar
            var crossbar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crossbar.name = "Crossbar";
            crossbar.transform.SetParent(root.transform);
            crossbar.transform.localPosition = new Vector3(0f, goalHeight, 0f);
            crossbar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            crossbar.transform.localScale = new Vector3(postRadius * 2f, halfWidth, postRadius * 2f);
            crossbar.GetComponent<MeshRenderer>().sharedMaterial = CreateColorMaterial(Color.white);

            // Net (semi-transparent quad)
            var net = GameObject.CreatePrimitive(PrimitiveType.Quad);
            net.name = "Net";
            net.transform.SetParent(root.transform);
            float netOffsetX = position.x > 0 ? 1f : -1f;
            net.transform.localPosition = new Vector3(netOffsetX, goalHeight / 2f, 0f);
            net.transform.localScale = new Vector3(goalWidth, goalHeight, 1f);
            net.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            net.GetComponent<MeshRenderer>().sharedMaterial = CreateColorMaterial(new Color(1f, 1f, 1f, 0.3f));
            var col = net.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }
    }
}
