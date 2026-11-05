using System;
using System.IO;
using OpenFifa.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenFifa.Gameplay.Editor
{
    /// <summary>
    /// Batch-mode preview of <see cref="ProceduralStadium"/>: builds it with a flat green pitch in a new empty scene and
    /// writes PNG stills (broadcast, behind goal, wide aerial, main stand). Run with
    /// -executeMethod OpenFifa.Gameplay.Editor.StadiumPreviewRender.Render -stadiumOut &lt;dir&gt; (needs a GPU context, so no -nographics).
    /// </summary>
    public static class StadiumPreviewRender
    {
        public static void Render()
        {
            int code = 0;
            try { RenderAll(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        [MenuItem("OpenFifa/Stadium/Render Preview Stills")]
        public static void RenderAll()
        {
            string outDir = Arg("-stadiumOut") ?? Path.Combine(Path.GetTempPath(), "stadium-preview");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var pitch = PitchSpec.Standard;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material Mat(Color c) { var m = new Material(lit); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.1f); return m; }

            // Flat pitch + 5 m run-off, with white lines so touchline visibility can be judged.
            var grass = GameObject.CreatePrimitive(PrimitiveType.Plane);
            grass.name = "Pitch";
            grass.transform.localScale = new Vector3((pitch.Width + 10f) / 10f, 1f, (pitch.Length + 10f) / 10f);
            grass.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.22f, 0.5f, 0.2f));
            var white = Mat(Color.white);
            void Line(float x, float z, float sx, float sz)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Cube);
                q.transform.position = new Vector3(x, 0.01f, z);
                q.transform.localScale = new Vector3(sx, 0.01f, sz);
                q.GetComponent<Renderer>().sharedMaterial = white;
            }
            Line(pitch.HalfWidth, 0f, 0.12f, pitch.Length);
            Line(-pitch.HalfWidth, 0f, 0.12f, pitch.Length);
            Line(0f, pitch.HalfLength, pitch.Width, 0.12f);
            Line(0f, -pitch.HalfLength, pitch.Width, 0.12f);
            Line(0f, 0f, pitch.Width, 0.12f);
            var red = Mat(new Color(0.8f, 0.1f, 0.15f));
            var blue = Mat(new Color(0.1f, 0.3f, 0.85f));
            for (int i = 0; i < 6; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                p.transform.position = new Vector3(i * 6f - 10f, 0.9f, i * 7f - 18f);
                p.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
                p.GetComponent<Renderer>().sharedMaterial = (i & 1) == 0 ? red : blue;
            }
            var edge = GameObject.CreatePrimitive(PrimitiveType.Capsule); // player on the near touchline
            edge.transform.position = new Vector3(pitch.HalfWidth - 0.3f, 0.9f, 5f);
            edge.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            edge.GetComponent<Renderer>().sharedMaterial = red;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(62f, -90f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.5f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.35f, 0.37f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.2f, 0.16f);

            var stadium = ProceduralStadium.Build(null, pitch, new Color(0.75f, 0.1f, 0.15f), new Color(0.1f, 0.3f, 0.8f));
            Debug.Log($"[StadiumPreview] triangles {stadium.TriangleCount}, draw calls {stadium.DrawCalls}");
            File.WriteAllText(Path.Combine(outDir, "stats.txt"),
                $"triangles {stadium.TriangleCount}\ndraw calls (stadium) {stadium.DrawCalls}\n");

            var volGo = new GameObject("Bloom Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = vol.profile.Add<Bloom>(true);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.65f);

            var cam = new GameObject("Preview Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.11f, 0.16f);
            cam.farClipPlane = 800f;
            cam.allowHDR = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            // SinglePlayerCamera broadcast default (focus at the centre spot), GoalArea mode, then wide shots.
            Shot(cam, outDir, "broadcast", new Vector3(pitch.HalfWidth + 22f, 24f, 0f), new Vector3(0f, 0f, 0f), 34f);
            Shot(cam, outDir, "broadcast_wing", new Vector3(pitch.HalfWidth + 22f + 10.2f, 24f, 30f), new Vector3(20f, 0f, 30f), 34f);
            Shot(cam, outDir, "behind_goal", new Vector3(0f, 7f, pitch.HalfLength + 14f), new Vector3(0f, 0.5f, pitch.HalfLength - 14f), 55f);
            Shot(cam, outDir, "aerial_wide", new Vector3(120f, 85f, -125f), new Vector3(0f, 0f, 0f), 45f);
            Shot(cam, outDir, "main_stand", new Vector3(-20f, 12f, -30f), new Vector3(45f, 10f, 5f), 55f);
        }

        private static void Shot(Camera cam, string dir, string name, Vector3 pos, Vector3 look, float fov)
        {
            const int W = 1920, H = 1080;
            cam.transform.position = pos;
            cam.transform.LookAt(look);
            cam.fieldOfView = fov;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("[StadiumPreview] wrote " + name);
        }

        private static string Arg(string key)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
            return null;
        }
    }
}
