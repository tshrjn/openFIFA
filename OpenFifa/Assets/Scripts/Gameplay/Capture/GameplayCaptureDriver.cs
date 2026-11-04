using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Headless gameplay recorder for visual review. Activated only by command-line flags,
    /// so it does nothing in normal play:
    ///   -ofCapture &lt;dir&gt;      write PNG frames to dir (required to activate)
    ///   -ofCaptureSeconds N    seconds of gameplay to record (default 30)
    ///   -ofCaptureFps N        fixed capture framerate (default 30)
    ///   -ofCaptureWarmup N     seconds to skip before recording (default 2)
    ///   -ofAutoplay            AI controls every player, including the human one
    /// Time.captureFramerate makes game time advance exactly 1/fps per frame, so the
    /// recording plays back at real speed no matter how slowly the box renders.
    /// </summary>
    public class GameplayCaptureDriver : MonoBehaviour
    {
        private string _dir;
        private float _seconds = 30f;
        private int _fps = 30;
        private float _warmup = 2f;
        private bool _autoplay;
        private int _frame;
        private int _warmupFrames;
        private int _totalFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var args = Environment.GetCommandLineArgs();
            string dir = ArgValue(args, "-ofCapture");
            if (string.IsNullOrEmpty(dir)) return;

            var go = new GameObject("GameplayCaptureDriver");
            DontDestroyOnLoad(go);
            var driver = go.AddComponent<GameplayCaptureDriver>();
            driver._dir = dir;
            driver._seconds = ParseFloat(ArgValue(args, "-ofCaptureSeconds"), 30f);
            driver._fps = Mathf.Max(1, (int)ParseFloat(ArgValue(args, "-ofCaptureFps"), 30f));
            driver._warmup = ParseFloat(ArgValue(args, "-ofCaptureWarmup"), 2f);
            driver._autoplay = Array.IndexOf(args, "-ofAutoplay") >= 0;
        }

        private void Start()
        {
            Directory.CreateDirectory(_dir);
            Time.captureFramerate = _fps;
            _warmupFrames = Mathf.RoundToInt(_warmup * _fps);
            _totalFrames = Mathf.RoundToInt(_seconds * _fps);
            Debug.Log($"[Capture] {_totalFrames} frames at {_fps} fps to {_dir} (warmup {_warmupFrames}, autoplay {_autoplay})");
        }

        private void Update()
        {
            if (_autoplay) HandAllPlayersToAI();

            _frame++;
            if (_frame <= _warmupFrames) return;

            int index = _frame - _warmupFrames;
            if (index > _totalFrames)
            {
                Debug.Log($"[Capture] done, {_totalFrames} frames");
                Application.Quit(0);
                return;
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, $"frame_{index:D5}.png"));
        }

        // The orchestrator may hand control back to a human player on switches, so re-apply each frame.
        private static void HandAllPlayersToAI()
        {
            var controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var pc in controllers)
            {
                if (!pc.enabled) continue;
                var ai = pc.GetComponent<OpenFifa.AI.AIController>();
                if (ai == null) continue;
                pc.enabled = false;
                ai.enabled = true;
            }
        }

        private static string ArgValue(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static float ParseFloat(string s, float fallback)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }
    }
}
