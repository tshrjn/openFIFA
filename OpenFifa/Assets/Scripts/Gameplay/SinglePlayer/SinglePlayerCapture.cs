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
    ///
    /// The user's seat is always driven by the deterministic <see cref="ScriptedGamepadBot"/> (or the team AI with
    /// -spAiVsAi) in both the probe and the recorded match, so the probe's goal actually happens on camera.
    /// Runs after the controller (execution order -40 vs -50), so its Awake sees the parsed command line and its
    /// AutoStart override cannot be undone by the controller's Awake.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(SinglePlayerMatchController))]
    public sealed class SinglePlayerCapture : MonoBehaviour
    {
        public string OutputDir;
        public float Seconds = 30f;
        public int Fps = 30;
        /// <summary>-spKitStills: after the clip, close-ups of kits (back, front) and faces (kit_*.png).</summary>
        public bool KitStills;

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
                if (args[i] == "-spKitStills" || args[i + 1] == "-spKitStills") KitStills = true;
            }
            _c = GetComponent<SinglePlayerMatchController>();
            if (string.IsNullOrEmpty(OutputDir))
            {
                enabled = false;
                return;
            }
            _c.AutoStart = false; // the capture drives the start
            // Device input would leave the user's player idle and diverge from the headless probe.
            if (!_c.AiVsAi) _c.ScriptedHuman = true;
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
            // Same seat driver as the recorded match: a fresh bot (forced in Awake), or null = team AI in AI vs AI.
            probe.HumanSlots[0].Input = _c.CreateHumanInput();
            int goalTick = -1;
            while (!probe.IsFinished && goalTick < 0)
            {
                var ev = probe.StepOnce(null);
                for (int i = 0; i < ev.Count; i++)
                    if (ev[i].Type == MatchEventType.Goal && probe.Sim.Tick > 10 * probe.Sim.Config.TickRate) goalTick = probe.Sim.Tick;
            }
            Debug.Log("[capture] first goal at tick " + goalTick);

            _c.StartMatch(_c.Level);
            // The probe stepped with the yaw it was built with; keep the recorded match on exactly that value.
            _c.Session.CameraYaw = probeOptions.CameraYaw;
            _c.PinCameraYaw = true;
            int tickRate = _c.Session.Sim.Config.TickRate;
            int startTick = goalTick > 0 ? Math.Max(0, goalTick - (int)(Seconds * 0.66f * tickRate)) : 0;
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
                    Debug.Log("[capture] goal on camera at tick " + _c.Session.Sim.Tick + " (probe " + goalTick + ")");
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
            if (KitStills) yield return KitCloseUps();

            var r = _c.Session.Result;
            File.WriteAllText(Path.Combine(OutputDir, "capture.txt"),
                $"frames={total} fps={Fps} level={_c.Level} seed={_c.Seed} goalTick={goalTick} startTick={startTick} " +
                $"endTick={_c.Session.Sim.Tick} score={r.HomeScore}-{r.AwayScore}\n");
            Debug.Log("[capture] done");
            Application.Quit(0);
        }

        /// <summary>
        /// Kit review stills: for a home outfielder, an away outfielder and a goalkeeper, the camera is placed behind
        /// (name + number), in front (crest, front number) and close to the face, with the match frozen.
        /// </summary>
        private IEnumerator KitCloseUps()
        {
            var cam = _c.BroadcastCamera;
            var camT = cam.transform;
            var hud = GetComponent<SinglePlayerHud>();
            bool hudWas = hud != null && hud.enabled;
            if (hud != null) hud.enabled = false;
            cam.enabled = false;
            _c.Frozen = true;
            var oldPos = camT.position;
            var oldRot = camT.rotation;
            var unityCam = cam.GetComponent<Camera>();
            float oldFov = unityCam != null ? unityCam.fieldOfView : 0f;
            int per = MatchSetup.PlayersPerTeam;
            int[] ids = { 9, per + 7, 0, 4, per + 3, per };
            string[] tags = { "home9", "away7", "homegk", "home4", "away3", "awaygk" };
            for (int n = 0; n < ids.Length; n++)
            {
                var p = _c.PlayerTransform(ids[n]);
                if (p == null || !p.gameObject.activeInHierarchy) continue;
                var fwd = p.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                var chest = p.position + Vector3.up * 1.25f;
                var face = p.position + Vector3.up * 1.62f;
                if (unityCam != null) unityCam.fieldOfView = 30f;
                camT.position = chest - fwd * 2.6f + Vector3.up * 0.15f;
                camT.LookAt(chest);
                yield return new WaitForEndOfFrame();
                Save(Path.Combine(OutputDir, $"kit_back_{tags[n]}.png"));
                camT.position = chest + fwd * 2.6f + Vector3.up * 0.15f;
                camT.LookAt(chest);
                yield return new WaitForEndOfFrame();
                Save(Path.Combine(OutputDir, $"kit_front_{tags[n]}.png"));
                camT.position = face + fwd * 0.75f + p.right * 0.25f;
                camT.LookAt(face);
                yield return new WaitForEndOfFrame();
                Save(Path.Combine(OutputDir, $"kit_face_{tags[n]}.png"));
            }
            if (unityCam != null) unityCam.fieldOfView = oldFov;
            camT.SetPositionAndRotation(oldPos, oldRot);
            cam.enabled = true;
            if (hud != null) hud.enabled = hudWas;
            _c.Frozen = false;
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
