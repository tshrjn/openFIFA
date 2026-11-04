using System;
using System.Collections;
using System.IO;
using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay.SinglePlayer
{
    /// <summary>
    /// Review capture for a player build run under Xvfb. Enabled by <c>-spCapture &lt;dir&gt;</c>; optional
    /// <c>-spCaptureSeconds</c> (default 30) and <c>-spCaptureFps</c> (default 30). It:
    /// 1. saves the difficulty picker (still_picker.png);
    /// 2. replays the same seeded match headless to find the first goal (the session is deterministic), then starts the
    ///    real match and fast-forwards so the goal lands about two thirds into the clip;
    /// 3. writes frames/frame_00001.png... at a fixed capture frame rate (Time.captureFramerate), plus stills from the
    ///    broadcast, sideline, close-up, goal-area and top-down cameras and one with the HUD and touch overlay;
    /// 4. quits. Stitch the frames with ffmpeg.
    /// </summary>
    [RequireComponent(typeof(SinglePlayerMatchController))]
    public sealed class SinglePlayerCapture : MonoBehaviour
    {
        public string OutputDir;
        public float Seconds = 30f;
        public int Fps = 30;

        private SinglePlayerMatchController _c;
        private int _frame;

        private void Awake()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-spCapture") OutputDir = args[i + 1];
                else if (args[i] == "-spCaptureSeconds") float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out Seconds);
                else if (args[i] == "-spCaptureFps") int.TryParse(args[i + 1], out Fps);
            }
            _c = GetComponent<SinglePlayerMatchController>();
            if (string.IsNullOrEmpty(OutputDir))
            {
                enabled = false;
                return;
            }
            _c.AutoStart = false; // the capture drives the start
            Directory.CreateDirectory(Path.Combine(OutputDir, "frames"));
            Time.captureFramerate = Mathf.Max(1, Fps);
        }

        private void Start()
        {
            if (enabled) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Debug.Log("[capture] start, writing to " + OutputDir);
            for (int i = 0; i < 8; i++) yield return null;
            yield return Still("still_picker.png");

            // Find the first goal headless with the same options and inputs.
            var probeOptions = _c.BuildOptions(_c.Level, out _, out _);
            var probe = new SinglePlayerMatchSession(probeOptions);
            probe.HumanSlots[0].Input = _c.CreateHumanInput() is ScriptedGamepadBot ? new ScriptedGamepadBot() : null;
            int goalTick = -1;
            while (!probe.IsFinished && goalTick < 0)
            {
                var ev = probe.StepOnce(null);
                for (int i = 0; i < ev.Count; i++)
                    if (ev[i].Type == MatchEventType.Goal && probe.Sim.Tick > 600) goalTick = probe.Sim.Tick;
            }
            Debug.Log("[capture] first goal at tick " + goalTick);

            _c.StartMatch(_c.Level);
            int startTick = goalTick > 0 ? Math.Max(0, goalTick - (int)(Seconds * 0.66f * 60f)) : 0;
            if (startTick > 0) _c.FastForwardTo(startTick);
            yield return null;

            int total = Mathf.RoundToInt(Seconds * Fps);
            int[] stillAt = { total / 10, total * 3 / 10, total * 4 / 10, total / 2, total * 9 / 10 };
            string[] stillName = { "still_broadcast.png", "still_sideline.png", "still_closeup.png", "still_topdown.png", "still_hud_touch.png" };
            var modes = new[] { SinglePlayerCamera.Mode.Broadcast, SinglePlayerCamera.Mode.Sideline, SinglePlayerCamera.Mode.CloseUp, SinglePlayerCamera.Mode.TopDown, SinglePlayerCamera.Mode.Broadcast };
            bool goalStillDone = false;
            var hud = GetComponent<SinglePlayerHud>();

            for (_frame = 1; _frame <= total; _frame++)
            {
                yield return new WaitForEndOfFrame();
                Save(Path.Combine(OutputDir, "frames", $"frame_{_frame:00000}.png"));

                if (!goalStillDone && _c.Session.Sim.Phase == SimMatchPhase.Stoppage && _c.Session.Sim.State.Ball.InNet != 0)
                {
                    goalStillDone = true;
                    _c.BroadcastCamera.GoalSide = _c.Session.Sim.State.Ball.InNet;
                    yield return ModeStill(SinglePlayerCamera.Mode.GoalArea, "still_goal_area.png");
                }
                for (int k = 0; k < stillAt.Length; k++)
                {
                    if (_frame != stillAt[k]) continue;
                    if (k == stillAt.Length - 1 && hud != null) hud.ForceTouchOverlay = true;
                    yield return ModeStill(modes[k], stillName[k]);
                    if (hud != null) hud.ForceTouchOverlay = false;
                }
            }

            if (!goalStillDone)
            {
                _c.BroadcastCamera.GoalSide = 1;
                yield return ModeStill(SinglePlayerCamera.Mode.GoalArea, "still_goal_area.png");
            }
            var r = _c.Session.Result;
            File.WriteAllText(Path.Combine(OutputDir, "capture.txt"),
                $"frames={total} fps={Fps} level={_c.Level} seed={_c.Seed} goalTick={goalTick} startTick={startTick} " +
                $"endTick={_c.Session.Sim.Tick} score={r.HomeScore}-{r.AwayScore}\n");
            Debug.Log("[capture] done");
            Application.Quit(0);
        }

        /// <summary>Holds the sim (without the pause menu) for one frame, renders with another camera mode, saves, restores.</summary>
        private IEnumerator ModeStill(SinglePlayerCamera.Mode mode, string file)
        {
            var cam = _c.BroadcastCamera;
            var old = cam.CameraMode;
            _c.Frozen = true;
            cam.CameraMode = mode;
            cam.Snap();
            yield return new WaitForEndOfFrame();
            Save(Path.Combine(OutputDir, file));
            cam.CameraMode = old;
            cam.Snap();
            _c.Frozen = false;
        }

        private IEnumerator Still(string file)
        {
            yield return new WaitForEndOfFrame();
            Save(Path.Combine(OutputDir, file));
        }

        private static void Save(string path)
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
        }
    }
}
