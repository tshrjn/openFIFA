using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay.SinglePlayer
{
    /// <summary>
    /// Review capture for a player build run under Xvfb. Enabled by <c>-spCapture &lt;dir&gt;</c>; optional
    /// <c>-spCaptureSeconds</c> (default 30) and <c>-spCaptureFps</c> (default 30). It:
    /// 1. saves the difficulty picker (still_picker.png);
    /// 2. replays the same seeded match headless to full time (the session is deterministic): finds the first goal,
    ///    collects ball moments (<see cref="CaptureHighlights"/>) and writes tactics.txt (<see cref="MatchTacticsStats"/>);
    ///    then starts the real match and fast-forwards so the goal lands about two thirds into the clip;
    /// 3. writes frames/frame_00001.png... at a fixed capture frame rate (Time.captureFramerate), plus stills from the
    ///    broadcast, sideline, close-up, goal-area and top-down cameras, one with the HUD and touch overlay, and two
    ///    chest-up close-ups of the controlled player (still_closeup_face.png, still_closeup_back.png);
    /// 4. copies 11 frames at ~10 fps around up to three probe moments in the window (a long pass, a shot, a bounce or
    ///    roll) to sequences/seqA-01.png... and describes them in sequences.txt;
    /// 5. logs every commentary line spoken in the window to commentary.txt ("mm:ss.s | event | speaker | text"; name
    ///    calls are tagged "[call]", and a line cut off by a bigger moment gets a second "[cut]" entry);
    /// 6. writes capture.txt (run parameters, the clubs and every league's club ids for -spHome/-spAway) and quits.
    /// Stitch the frames with ffmpeg (capture.sh does).
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

        /// <summary>Frames per sequence and their spacing (s): 11 frames at 10 fps, the moment at frame 6.</summary>
        public const int SequenceFrames = 11;
        public const float SequenceStep = 0.1f;
        public const int MaxSequences = 3;
        /// <summary>Chest-up close-up: vertical field of view (deg), distance (m) and how far below the head top it aims.</summary>
        public const float CloseFov = 24f, CloseDistance = 2.2f, CloseAimBelowHead = 0.42f;

        private SinglePlayerMatchController _c;
        private int _frame;
        private readonly StringBuilder _commentary = new StringBuilder(4096);
        private int _commentaryLines;
        private readonly CommentaryUtterance[] _recent = new CommentaryUtterance[8];
        private int _recentNext;
        private readonly CommentaryUtterance[] _recentCuts = new CommentaryUtterance[8];
        private int _recentCutNext;
        private readonly List<CaptureHighlight> _sequences = new List<CaptureHighlight>(MaxSequences);
        private readonly int[] _sequenceWritten = new int[MaxSequences];
        private int _seqIndex, _seqFrame, _seqStepTicks, _seqBefore;

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

            // Replay the whole match headless with the same options and inputs: first goal, ball moments, tactics.
            var probeOptions = _c.BuildOptions(_c.Level, out _, out _);
            var probe = new SinglePlayerMatchSession(probeOptions);
            // Same seat driver as the recorded match: a fresh bot (forced in Awake), or null = team AI in AI vs AI.
            probe.HumanSlots[0].Input = _c.CreateHumanInput();
            var highlights = new CaptureHighlights();
            var tactics = new MatchTacticsStats(Math.Max(1, probe.Sim.Config.TickRate / 2));
            int goalTick = -1;
            while (!probe.IsFinished)
            {
                var ev = probe.StepOnce(null);
                highlights.Observe(probe.Sim, ev);
                tactics.Observe(probe.Sim, ev);
                for (int i = 0; i < ev.Count; i++)
                    if (goalTick < 0 && ev[i].Type == MatchEventType.Goal && probe.Sim.Tick > 10 * probe.Sim.Config.TickRate) goalTick = probe.Sim.Tick;
            }
            Debug.Log("[capture] first goal at tick " + goalTick + ", " + highlights.All.Count + " ball moments");
            WriteTactics(tactics, probe);

            _c.StartMatch(_c.Level);
            // The probe stepped with the yaw it was built with; keep the recorded match on exactly that value.
            _c.Session.CameraYaw = probeOptions.CameraYaw;
            _c.PinCameraYaw = true;
            int tickRate = _c.Session.Sim.Config.TickRate;
            int startTick = goalTick > 0 ? Math.Max(0, goalTick - (int)(Seconds * 0.66f * tickRate)) : 0;
            if (startTick > 0) _c.FastForwardTo(startTick);
            yield return null;

            int total = Mathf.RoundToInt(Seconds * Fps);
            // Sequences: moments whose 11 frames fit inside the recorded window (half a second of margin each end).
            _seqStepTicks = Math.Max(1, Mathf.RoundToInt(SequenceStep * tickRate));
            _seqBefore = (SequenceFrames / 2) * _seqStepTicks;
            int seqAfter = (SequenceFrames - 1) * _seqStepTicks - _seqBefore;
            int windowStart = _c.Session.Sim.Tick + tickRate / 2;
            int windowEnd = startTick + (int)((long)total * tickRate / Math.Max(1, Fps)) - tickRate / 2;
            CaptureHighlights.Pick(highlights.All, windowStart, windowEnd, _seqBefore, seqAfter, MaxSequences, _sequences);
            if (_sequences.Count > 0) Directory.CreateDirectory(Path.Combine(OutputDir, "sequences"));
            int closeAt = Math.Max(1, total / 5);
            int[] stillAt = { total / 10, total * 3 / 10, total * 4 / 10, total / 2, total * 9 / 10 };
            string[] stillName = { "still_broadcast.png", "still_sideline.png", "still_closeup.png", "still_topdown.png", "still_hud_touch.png" };
            var modes = new[] { SinglePlayerCamera.Mode.Broadcast, SinglePlayerCamera.Mode.Sideline, SinglePlayerCamera.Mode.CloseUp, SinglePlayerCamera.Mode.TopDown, SinglePlayerCamera.Mode.Broadcast };
            bool goalStillDone = false;
            var hud = GetComponent<SinglePlayerHud>();

            for (_frame = 1; _frame <= total; _frame++)
            {
                yield return new WaitForEndOfFrame();
                string framePath = Path.Combine(OutputDir, "frames", $"frame_{_frame:00000}.png");
                Save(framePath);
                LogCommentary();
                CopySequenceFrame(framePath);

                if (_frame == closeAt)
                {
                    yield return CloseUpStill(true, "still_closeup_face.png");
                    yield return CloseUpStill(false, "still_closeup_back.png");
                }

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
            File.WriteAllText(Path.Combine(OutputDir, "commentary.txt"), _commentary.ToString());
            WriteSequences(probeOptions.Config.HalfDurationSeconds);
            File.WriteAllText(Path.Combine(OutputDir, "capture.txt"),
                $"frames={total} fps={Fps} level={_c.Level} seed={_c.Seed} goalTick={goalTick} startTick={startTick} " +
                $"endTick={_c.Session.Sim.Tick} score={r.HomeScore}-{r.AwayScore}\n" +
                $"home={ClubLine(_c.HomeClub)} away={ClubLine(_c.AwayClub)} league={(_c.Leagues.Count > 0 ? _c.Leagues[Mathf.Clamp(_c.LeagueIndex, 0, _c.Leagues.Count - 1)].Id : "none")} " +
                $"seat={(_c.AiVsAi ? "team AI" : "scripted bot")} controlled={_c.Session.ControlledId}\n" +
                $"commentaryLines={_commentaryLines} sequences={_sequences.Count} window={windowStart}..{windowEnd}\n" +
                "Clubs for -spHome/-spAway (optionally -spLeague <id>; both clubs from one league):\n" +
                RosterSelection.Describe(_c.Leagues));
            Debug.Log("[capture] done");
            Application.Quit(0);
        }

        private static string ClubLine(RosterClub c) => c != null ? c.Id + " (" + c.Name + ")" : "default";

        private void WriteTactics(MatchTacticsStats stats, SinglePlayerMatchSession probe)
        {
            var sb = new StringBuilder(3072);
            var res = probe.Result;
            sb.Append("Headless tactics summary: ").Append(_c.HomeClub != null ? _c.HomeClub.Name : "Home").Append(" v ")
              .Append(_c.AwayClub != null ? _c.AwayClub.Name : "Away").Append(", seed ").Append(_c.Seed).Append(", level ").Append(_c.Level)
              .Append(", ").Append(probe.Options.Config.HalfDurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture))
              .Append(" s halves, final ").Append(res.HomeScore).Append('-').Append(res.AwayScore).Append('\n');
            sb.Append("Source: the capture's full-match MatchSim replay (same options and seat driver: ")
              .Append(_c.AiVsAi ? "team AI on both sides" : "scripted bot on the user's seat, " + _c.HumanTeam)
              .Append("). Shape: outfield players, sampled every 0.5 s of live play; width across the pitch, depth along it;\n")
              .Append("defensive line = mean distance of the four deepest outfield players from their own goal line.\n")
              .Append("PPDA = opposition passes in their own 60% of the pitch / this team's tackles, interceptions and fouls there.\n\n");
            sb.Append(stats.Format(_c.HomeShort, _c.AwayShort));
            File.WriteAllText(Path.Combine(OutputDir, "tactics.txt"), sb.ToString());
        }

        /// <summary>Appends the commentary lines that started this frame (each utterance once, even across held frames).</summary>
        private void LogCommentary()
        {
            var session = _c.Session;
            var cues = session.FrameCues;
            for (int i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                if (cue.Utterance == null) continue;
                var st = session.Sim.State;
                string clock = CaptureText.Clock(st.Clock, st.Half, session.Sim.Config.HalfDurationSeconds);
                if (cue.Kind == CommentaryCueKind.Stop)
                {
                    // The line was cut off by a bigger moment: log it so the transcript does not read as fully spoken.
                    // Deduped like starts (a frozen sim re-reads the same frame's cues).
                    if (Seen(_recentCuts, cue.Utterance)) continue;
                    _recentCuts[_recentCutNext] = cue.Utterance;
                    _recentCutNext = (_recentCutNext + 1) % _recentCuts.Length;
                    _commentary.Append(CaptureText.CommentaryLine(clock, cue.Utterance.Category.Event, cue.Utterance.Voice,
                        "[cut] " + cue.Utterance.Transcript)).Append('\n');
                    continue;
                }
                if (Seen(_recent, cue.Utterance)) continue;
                _recent[_recentNext] = cue.Utterance;
                _recentNext = (_recentNext + 1) % _recent.Length;
                string tag = cue.Utterance.Category.Event == CommentaryEventType.OnTheBall ? "[call] " : "";
                _commentary.Append(CaptureText.CommentaryLine(clock, cue.Utterance.Category.Event, cue.Utterance.Voice,
                    tag + cue.Utterance.Transcript)).Append('\n');
                _commentaryLines++;
            }
        }

        private static bool Seen(CommentaryUtterance[] recent, CommentaryUtterance u)
        {
            for (int i = 0; i < recent.Length; i++)
                if (ReferenceEquals(recent[i], u)) return true;
            return false;
        }

        /// <summary>Copies this frame into the current sequence when the sim has reached its next sample tick (one per frame).</summary>
        private void CopySequenceFrame(string framePath)
        {
            if (_seqIndex >= _sequences.Count) return;
            int tick = _c.Session.Sim.Tick;
            int sampleTick = _sequences[_seqIndex].Tick - _seqBefore + _seqFrame * _seqStepTicks;
            if (tick < sampleTick) return;
            char label = (char)('A' + _seqIndex);
            File.Copy(framePath, Path.Combine(OutputDir, "sequences", $"seq{label}-{_seqFrame + 1:00}.png"), true);
            _seqFrame++;
            _sequenceWritten[_seqIndex] = _seqFrame;
            if (_seqFrame >= SequenceFrames)
            {
                _seqIndex++;
                _seqFrame = 0;
            }
        }

        private void WriteSequences(float halfSeconds)
        {
            var sb = new StringBuilder(1024);
            sb.Append("Sequences: ").Append(SequenceFrames).Append(" consecutive gameplay frames every ").Append(_seqStepTicks)
              .Append(" sim ticks (~10 fps of match time), the moment at frame ").Append(SequenceFrames / 2 + 1)
              .Append(". Chosen from the headless replay's ball moments inside the recorded window.\n");
            if (_sequences.Count == 0) sb.Append("(no long pass, shot, bounce or roll fitted the window)\n");
            for (int i = 0; i < _sequences.Count; i++)
            {
                string label = "seq" + (char)('A' + i);
                sb.Append(CaptureHighlights.Describe(label, _sequences[i], halfSeconds, _c.Names, _c.HomeShort, _c.AwayShort))
                  .Append(" | frames ").Append(label).Append("-01..").Append(label).Append('-').Append(_sequenceWritten[i].ToString("00"))
                  .Append('\n');
            }
            File.WriteAllText(Path.Combine(OutputDir, "sequences.txt"), sb.ToString());
        }

        /// <summary>
        /// Chest-up still of the controlled player from the front (face, shirt front) or the back (name and number).
        /// Starts from the close-up mode, then holds the camera component for one frame and frames the player itself:
        /// the camera's own framing logic is unchanged.
        /// </summary>
        private IEnumerator CloseUpStill(bool front, string file)
        {
            var cam = _c.BroadcastCamera;
            var unityCam = cam.GetComponent<Camera>();
            var old = cam.CameraMode;
            float oldFov = unityCam.fieldOfView;
            _c.Frozen = true;
            cam.CameraMode = SinglePlayerCamera.Mode.CloseUp;
            cam.Snap();
            cam.enabled = false;

            int id = _c.Session.ControlledId;
            var t = _c.PlayerTransform(id);
            float headTop = 1.8f;
            var renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                headTop = Mathf.Clamp(b.max.y, 1.5f, 2.1f); // skinned bounds can be loose
            }
            float facing = _c.Session.Sim.State.Players[id].Facing; // sim yaw: 0 = +Z, +pi/2 = +X (Unity's Euler y)
            var fwd = new Vector3(Mathf.Sin(facing), 0f, Mathf.Cos(facing));
            if (!front) fwd = -fwd;
            var side = Vector3.Cross(Vector3.up, fwd);
            var aim = new Vector3(t.position.x, headTop - CloseAimBelowHead, t.position.z);
            unityCam.fieldOfView = CloseFov;
            cam.transform.position = aim + fwd * CloseDistance + side * (0.25f * CloseDistance) + Vector3.up * 0.1f;
            cam.transform.LookAt(aim);

            yield return new WaitForEndOfFrame();
            Save(Path.Combine(OutputDir, file));
            cam.enabled = true;
            unityCam.fieldOfView = oldFov;
            cam.CameraMode = old;
            cam.Snap();
            _c.Frozen = false;
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
