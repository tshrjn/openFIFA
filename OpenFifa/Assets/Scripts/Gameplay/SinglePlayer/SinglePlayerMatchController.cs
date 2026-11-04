using System;
using System.Collections.Generic;
using System.IO;
using OpenFifa.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenFifa.Gameplay.SinglePlayer
{
    /// <summary>
    /// Runtime entry point for OpenFootball single player. Builds the 105 x 68 m pitch (PitchBuilder), goals, ball,
    /// 22 player visuals (the Quaternius mannequin with <see cref="PlayerAnimationDriver"/> when a model is assigned,
    /// capsules otherwise), the broadcast camera and the HUD, then runs a <see cref="SinglePlayerMatchSession"/> every
    /// frame. Rosters come from StreamingAssets (plus user files in persistentDataPath); commentary is shown as subtitles.
    ///
    /// Command line (player or batch editor): -spLevel 0..6, -spSeed N, -spHalf seconds, -spAutoStart, -spAiVsAi,
    /// -spCapture dir (see <see cref="SinglePlayerCapture"/>).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class SinglePlayerMatchController : MonoBehaviour
    {
        public const string ProductName = "OpenFootball";

        [Header("Visuals")]
        [Tooltip("Player model (e.g. Assets/Animations/Quaternius/UAL2/UAL2_Standard.fbx). Empty = Resources/OpenFootball/PlayerModel, else capsules.")]
        public GameObject PlayerModelPrefab;
        [Tooltip("Footballer.controller built by OpenFootball > Animation > Build Footballer Animator.")]
        public RuntimeAnimatorController AnimatorController;

        [Header("Match")]
        public TeamAIDifficultyLevel Level = TeamAIDifficultyLevel.Professional;
        public ulong Seed = 1;
        [Tooltip("Sim seconds per half (360 = a 12-minute match).")]
        public float HalfSeconds = 360f;
        [Tooltip("Skip the difficulty picker.")]
        public bool AutoStart;
        [Tooltip("Attract mode: the team AI also controls the user's player.")]
        public bool AiVsAi;
        public SimTeam HumanTeam = SimTeam.Home;
        [Tooltip("Drive the user's player with the deterministic ScriptedGamepadBot (soak runs, captures).")]
        public bool ScriptedHuman;

        public SinglePlayerMatchSession Session { get; private set; }
        public SinglePlayerInput Input { get; private set; }
        public SinglePlayerCamera BroadcastCamera { get; private set; }
        public List<RosterLeague> Leagues { get; } = new List<RosterLeague>();
        public List<string> RosterProblems { get; } = new List<string>();
        public int HomeClubIndex { get; set; }
        public int AwayClubIndex { get; set; } = 1;
        public RosterClub HomeClub { get; private set; }
        public RosterClub AwayClub { get; private set; }
        public string[] Names { get; private set; } = new string[MatchSimState.PlayerCount];
        public bool Started => Session != null;
        /// <summary>Hold the match without showing the pause menu (review captures).</summary>
        public bool Frozen { get; set; }

        /// <summary>Latest commentary line (play-by-play or analyst) for subtitles.</summary>
        public string Subtitle { get; private set; } = string.Empty;
        public float SubtitleUntil { get; private set; }

        private CommentaryLineBank _commentaryBank;
        private CommentaryEngine _commentary;
        private Transform _playersRoot;
        private float _ballRadius = 0.11f;
        private TeamAIDifficultyLevel _defaultLevel;
        /// <summary>Capture: keep Session.CameraYaw at the value the session was built with (matches the headless probe).</summary>
        public bool PinCameraYaw { get; set; }
        private readonly Transform[] _players = new Transform[MatchSimState.PlayerCount];
        private readonly PlayerAnimationDriver[] _drivers = new PlayerAnimationDriver[MatchSimState.PlayerCount];
        private Transform _ball;
        private Transform _marker;
        private Transform _pitchRoot;
        private double _realClock;

        private void Awake()
        {
            _defaultLevel = Level;
            ParseCommandLine();
            BuildEnvironment();
            LoadRosters();
            LoadCommentary();
        }

        private void Start()
        {
            if (AutoStart) StartMatch(Level);
        }

        private void ParseCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string next = i + 1 < args.Length ? args[i + 1] : null;
                if (a == "-spLevel" && int.TryParse(next, out int lv)) Level = (TeamAIDifficultyLevel)Mathf.Clamp(lv, 0, 6);
                else if (a == "-spSeed" && ulong.TryParse(next, out ulong sd)) Seed = sd;
                else if (a == "-spHalf" && float.TryParse(next, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float h)) HalfSeconds = h;
                else if (a == "-spAutoStart") AutoStart = true;
                else if (a == "-spAiVsAi") AiVsAi = true;
                else if (a == "-spBot") ScriptedHuman = true;
            }
        }

        // ------------------------------------------------------------------ setup

        private void BuildEnvironment()
        {
            var pitch = PitchSpec.Standard;
            // PitchBuilder lays the length along X; rotate its root so the length runs along Z like the sim.
            _pitchRoot = new GameObject("Pitch (PitchBuilder)").transform;
            _pitchRoot.SetParent(transform, false);
            var builder = _pitchRoot.gameObject.AddComponent<PitchBuilder>();
            builder.BuildPitch(new PitchConfigData(pitch.Length, pitch.Width, pitch.GoalWidth, 9.15f, 5.5f, pitch.GoalHeight, 1f, 0.5f));
            // Rotate after building: PitchBuilder parents with worldPositionStays, so a pre-rotated root would be undone.
            _pitchRoot.localRotation = Quaternion.Euler(0f, 90f, 0f);

            BuildGoal(+1, pitch);
            BuildGoal(-1, pitch);
            BuildStands(pitch);

            if (FindFirstObjectByType<Light>() == null)
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.shadows = LightShadows.Soft;
            }
            // Light from the camera side and high, so the far stand's shadow falls behind it, not on the pitch.
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.transform.rotation = Quaternion.Euler(62f, -90f, 0f);

            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Broadcast Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.11f, 0.16f);
            cam.farClipPlane = 600f;
            BroadcastCamera = cam.GetComponent<SinglePlayerCamera>() ?? cam.gameObject.AddComponent<SinglePlayerCamera>();
            BroadcastCamera.HalfLength = pitch.HalfLength;
            BroadcastCamera.HalfWidth = pitch.HalfWidth;

            _ball = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            _ball.name = "Ball";
            SetBallRadius(new BallSimConfig().Radius);
            Destroy(_ball.GetComponent<Collider>());
            Tint(_ball.gameObject, Color.white);
            ApplyBallPattern(_ball.gameObject);
            BroadcastCamera.Ball = _ball;

            _marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            _marker.name = "ControlledMarker";
            _marker.localScale = new Vector3(1.6f, 0.02f, 1.6f);
            Destroy(_marker.GetComponent<Collider>());
            Tint(_marker.gameObject, new Color(1f, 0.85f, 0.1f));
            _marker.gameObject.SetActive(false);
            BroadcastCamera.Snap();
        }

        /// <summary>Visual ball = sim ball (Unity's sphere primitive is 1 m across), so it sits on the grass at y = r.</summary>
        private void SetBallRadius(float r)
        {
            _ballRadius = r;
            _ball.localScale = Vector3.one * (2f * r);
        }

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        /// <summary>
        /// Procedural panel pattern (dark patches at the 12 icosahedron vertices) so the ball's spin is visible.
        /// Generated at runtime, no asset.
        /// </summary>
        private static void ApplyBallPattern(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null) return;
            const int W = 128, H = 64;
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var verts = new Vector3[12];
            int n = 0;
            for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
            {
                verts[n++] = new Vector3(0f, a, b * phi).normalized;
                verts[n++] = new Vector3(a, b * phi, 0f).normalized;
                verts[n++] = new Vector3(b * phi, 0f, a).normalized;
            }
            float cosR = Mathf.Cos(0.33f);
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float lat = ((y + 0.5f) / H - 0.5f) * Mathf.PI;
                for (int x = 0; x < W; x++)
                {
                    float lon = (x + 0.5f) / W * 2f * Mathf.PI;
                    var d = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                    bool dark = false;
                    for (int k = 0; k < 12 && !dark; k++) dark = Vector3.Dot(d, verts[k]) > cosR;
                    px[y * W + x] = dark ? new Color32(30, 32, 40, 255) : new Color32(245, 245, 245, 255);
                }
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "BallPanels" };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            var m = r.sharedMaterial;
            if (m.HasProperty(BaseMapId)) m.SetTexture(BaseMapId, tex);
            if (m.HasProperty(MainTexId)) m.SetTexture(MainTexId, tex);
        }

        private void BuildGoal(int side, PitchSpec pitch)
        {
            var root = new GameObject(side > 0 ? "Goal +Z" : "Goal -Z").transform;
            root.SetParent(transform, false);
            float z = side * pitch.HalfLength;
            float hw = pitch.GoalWidth * 0.5f, h = pitch.GoalHeight, r = 0.06f;
            Post(root, new Vector3(-hw, h * 0.5f, z), new Vector3(r * 2f, h * 0.5f, r * 2f), Quaternion.identity);
            Post(root, new Vector3(hw, h * 0.5f, z), new Vector3(r * 2f, h * 0.5f, r * 2f), Quaternion.identity);
            Post(root, new Vector3(0f, h, z), new Vector3(r * 2f, hw, r * 2f), Quaternion.Euler(0f, 0f, 90f));
            // Simple net: a translucent back and roof.
            var back = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            back.SetParent(root, false);
            back.localPosition = new Vector3(0f, h * 0.5f, z + side * 2f);
            back.localScale = new Vector3(pitch.GoalWidth, h, 0.02f);
            Destroy(back.GetComponent<Collider>());
            Tint(back.gameObject, new Color(0.9f, 0.9f, 0.9f, 0.35f));
        }

        private static void Post(Transform parent, Vector3 pos, Vector3 scale, Quaternion rot)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            p.SetParent(parent, false);
            p.localPosition = pos;
            p.localRotation = rot;
            p.localScale = scale;
            Destroy(p.GetComponent<Collider>());
            Tint(p.gameObject, Color.white);
        }

        private void BuildStands(PitchSpec pitch)
        {
            var root = new GameObject("Stands").transform;
            root.SetParent(transform, false);
            var col = new Color(0.22f, 0.24f, 0.3f);
            // Far touchline and both ends; the broadcast camera side (+X) stays open.
            Box(root, new Vector3(-(pitch.HalfWidth + 16f), 7f, 0f), new Vector3(18f, 14f, pitch.Length + 30f), col, -25f, 0f);
            for (int s = -1; s <= 1; s += 2)
                Box(root, new Vector3(0f, 5f, s * (pitch.HalfLength + 16f)), new Vector3(pitch.Width + 30f, 10f, 14f), col, 0f, s * -25f);
            // Advertising boards along the near touchline give scale in the broadcast view.
            for (int i = -5; i <= 5; i++)
                Box(root, new Vector3(pitch.HalfWidth + 4f, 0.5f, i * 9.5f), new Vector3(0.2f, 1f, 9f),
                    (i & 1) == 0 ? new Color(0.1f, 0.3f, 0.8f) : new Color(0.85f, 0.85f, 0.9f), 0f, 0f);
        }

        private static void Box(Transform parent, Vector3 pos, Vector3 size, Color c, float tiltZ, float tiltX)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            b.SetParent(parent, false);
            b.localPosition = pos;
            b.localScale = size;
            b.localRotation = Quaternion.Euler(tiltX, 0f, tiltZ);
            Destroy(b.GetComponent<Collider>());
            Tint(b.gameObject, c);
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Colours a primitive with a fresh URP/Lit (or fallback) material.</summary>
        public static void Tint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            var m = new Material(shader);
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
            r.sharedMaterial = m;
        }

        private void LoadRosters()
        {
            Leagues.Clear();
            RosterProblems.Clear();
            try
            {
                Leagues.AddRange(RosterMatchBuilder.LoadLeagues(Application.streamingAssetsPath, Application.persistentDataPath, RosterProblems));
            }
            catch (Exception e)
            {
                RosterProblems.Add("Roster load failed: " + e.Message);
            }
            foreach (var p in RosterProblems) Debug.LogWarning("[" + ProductName + "] " + p);
        }

        private void LoadCommentary()
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "Commentary", "lines.en.json");
                if (!File.Exists(path)) return;
                var bank = CommentaryLineBank.Load(File.ReadAllText(path), out var errors);
                if (bank == null)
                {
                    Debug.LogWarning("[" + ProductName + "] commentary bank rejected: " + string.Join("; ", errors));
                    return;
                }
                _commentaryBank = bank; // a fresh engine is made per match (StartMatch), seeded from the match seed
            }
            catch (Exception e)
            {
                Debug.LogWarning("[" + ProductName + "] commentary unavailable: " + e.Message);
            }
        }

        public IReadOnlyList<RosterClub> Clubs => Leagues.Count > 0 ? Leagues[0].Clubs : null;

        // ------------------------------------------------------------------ match

        /// <summary>Session options for a level with the currently selected clubs (deterministic for a given seed).</summary>
        public SinglePlayerMatchOptions BuildOptions(TeamAIDifficultyLevel level, out Color homeColor, out Color awayColor)
        {
            var options = new SinglePlayerMatchOptions
            {
                Level = level,
                Seed = Seed,
                HumanTeam = HumanTeam,
                Config = new MatchSimConfig { HalfDurationSeconds = HalfSeconds },
                CameraYaw = BroadcastCamera != null ? BroadcastCamera.YawRadians : -Mathf.PI * 0.5f,
            };
            homeColor = new Color(0.75f, 0.1f, 0.15f);
            awayColor = new Color(0.95f, 0.95f, 0.97f);
            var clubs = Clubs;
            if (clubs != null && clubs.Count >= 2)
            {
                HomeClub = clubs[Mathf.Clamp(HomeClubIndex, 0, clubs.Count - 1)];
                AwayClub = clubs[Mathf.Clamp(AwayClubIndex, 0, clubs.Count - 1)];
                if (AwayClub == HomeClub) AwayClub = clubs[(HomeClubIndex + 1) % clubs.Count];
                options.Setup = RosterMatchBuilder.BuildSetup(HomeClub, AwayClub, Seed, out var hs, out var aws, out var names, out var ids);
                options.HomeTactics = hs.Tactics;
                options.AwayTactics = aws.Tactics;
                options.PlayerNames = names;
                options.PlayerIds = ids;
                Names = names;
                homeColor = ParseColour(HomeClub.PrimaryColour, homeColor);
                awayColor = ParseColour(AwayClub.PrimaryColour, awayColor);
                if (ColourDistance(homeColor, awayColor) < 0.35f) awayColor = ParseColour(AwayClub.AwayColour, awayColor);
            }
            else
            {
                Names = new string[MatchSimState.PlayerCount];
                for (int i = 0; i < Names.Length; i++) Names[i] = (i < 11 ? "H" : "A") + (i % 11 + 1);
            }
            return options;
        }

        /// <summary>Input for the user's seat: null in AI vs AI, the scripted bot, or the device reader.</summary>
        public IHumanInputSource CreateHumanInput() =>
            AiVsAi ? null : ScriptedHuman ? (IHumanInputSource)new ScriptedGamepadBot() : Input;

        public void StartMatch(TeamAIDifficultyLevel level)
        {
            Level = level;
            var options = BuildOptions(level, out var homeColor, out var awayColor);
            Session = new SinglePlayerMatchSession(options);
            Input = new SinglePlayerInput(Session.Profile.PassAssist);
            Session.HumanSlots[0].Input = CreateHumanInput();
            _commentary = _commentaryBank != null ? new CommentaryEngine(_commentaryBank, null, (uint)(Seed & 0xffffffff) | 1u) : null;
            if (_commentary != null) Session.AttachCommentaryEngine(_commentary);
            SetBallRadius(Session.Sim.Config.Ball.Radius);
            _ball.rotation = Quaternion.identity;

            SpawnPlayers(homeColor, awayColor);
            BroadcastCamera.Snap();
            Debug.Log($"[{ProductName}] kick-off: {TeamAIDifficultyLadder.DisplayName(level)}, seed {Seed}, " +
                      $"{(HomeClub != null ? HomeClub.Name + " v " + AwayClub.Name : "default squads")}, " +
                      $"{(AiVsAi ? "AI vs AI" : ScriptedHuman ? "scripted human" : "user = " + HumanTeam)}");
        }

        /// <summary>Tears the match down and returns to the picker (selections kept). No-op without a match.</summary>
        public void EndMatch()
        {
            if (Session == null) return;
            if (_playersRoot != null) Destroy(_playersRoot.gameObject);
            _playersRoot = null;
            Array.Clear(_players, 0, _players.Length);
            Array.Clear(_drivers, 0, _drivers.Length);
            Session = null;
            Input = null;
            _commentary = null;
            Frozen = false;
            PinCameraYaw = false;
            Subtitle = string.Empty;
            SubtitleUntil = 0f;
            _marker.gameObject.SetActive(false);
            BroadcastCamera.Player = null;
        }

        /// <summary>Same clubs and level, next seed (deterministic: an LCG step of the current seed).</summary>
        public void Rematch()
        {
            var level = Level;
            EndMatch();
            Seed = NextSeed(Seed);
            StartMatch(level);
        }

        /// <summary>Back to the picker with the launch defaults (level, first two clubs).</summary>
        public void QuitToMenu()
        {
            EndMatch();
            Level = _defaultLevel;
            HomeClubIndex = 0;
            AwayClubIndex = 1;
        }

        public static ulong NextSeed(ulong seed) => seed * 6364136223846793005UL + 1442695040888963407UL;

        /// <summary>Runs the match without rendering up to <paramref name="tick"/> (captures, skipping ahead).</summary>
        public void FastForwardTo(int tick)
        {
            if (Session == null) return;
            while (Session.Sim.Tick < tick && !Session.IsFinished) Session.StepOnce(null);
            SyncVisuals();
            UpdateControlledMarker();
            BroadcastCamera.Snap();
        }

        private static float ColourDistance(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        private static Color ParseColour(string hex, Color fallback) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;

        private void SpawnPlayers(Color home, Color away)
        {
            var prefab = PlayerModelPrefab != null ? PlayerModelPrefab : Resources.Load<GameObject>("OpenFootball/PlayerModel");
            var root = new GameObject("Players").transform;
            root.SetParent(transform, false);
            _playersRoot = root;
            bool kits = prefab != null && SinglePlayerKitDresser.Prepare(HomeClub, AwayClub);
            for (int id = 0; id < MatchSimState.PlayerCount; id++)
            {
                bool homeTeam = id < MatchSetup.PlayersPerTeam;
                bool keeper = id == 0 || id == MatchSetup.PlayersPerTeam;
                Color c = keeper ? (homeTeam ? new Color(0.1f, 0.75f, 0.3f) : new Color(0.95f, 0.75f, 0.1f)) : (homeTeam ? home : away);
                var p = new GameObject($"P{id:00} {Names[id]}").transform;
                p.SetParent(root, false);
                _players[id] = p;

                if (prefab != null)
                {
                    var visual = new GameObject("Visual").transform;
                    visual.SetParent(p, false);
                    var model = Instantiate(prefab, visual);
                    model.name = "Model";
                    var animator = model.GetComponentInChildren<Animator>();
                    if (animator == null) animator = model.AddComponent<Animator>();
                    if (AnimatorController != null) animator.runtimeAnimatorController = AnimatorController;
                    animator.applyRootMotion = false;
                    if (!kits || !SinglePlayerKitDresser.Dress(model, Session, HomeClub, AwayClub, id))
                    foreach (var r in model.GetComponentsInChildren<Renderer>())
                    {
                        var block = new MaterialPropertyBlock();
                        r.GetPropertyBlock(block);
                        block.SetColor(BaseColorId, c);
                        block.SetColor(ColorId, c);
                        r.SetPropertyBlock(block);
                    }
                    var driver = p.gameObject.AddComponent<PlayerAnimationDriver>();
                    driver.Source = new SimPlayerAnimSource(Session, id);
                    _drivers[id] = driver;
                }
                else
                {
                    var body = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
                    body.SetParent(p, false);
                    body.localPosition = new Vector3(0f, 0.9f, 0f);
                    body.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                    Destroy(body.GetComponent<Collider>());
                    Tint(body.gameObject, c);
                    var nose = GameObject.CreatePrimitive(PrimitiveType.Cube).transform; // facing indicator
                    nose.SetParent(p, false);
                    nose.localPosition = new Vector3(0f, 1.45f, 0.32f);
                    nose.localScale = new Vector3(0.18f, 0.12f, 0.2f);
                    Destroy(nose.GetComponent<Collider>());
                    Tint(nose.gameObject, Color.black);
                }
            }
            SyncVisuals();
            UpdateControlledMarker();
        }

        /// <summary>
        /// False while the match clock is held: paused, frozen for a capture still, half time or full time.
        /// Drivers get zero speed then, and touches are cancelled.
        /// </summary>
        public bool SimAdvancing =>
            Session != null && !Frozen && !Session.Paused
            && Session.Sim.Phase != SimMatchPhase.HalfTime && Session.Sim.Phase != SimMatchPhase.FullTime;

        private void Update()
        {
            if (Session == null) return;
            _realClock += Time.unscaledDeltaTime;
            if (PausePressed() && !Session.IsFinished) Session.TogglePause();
            // Touch edges are queued every frame and drained per sim tick (SinglePlayerInput.Read).
            Input?.PollFrame(SimAdvancing);
            if (!Frozen)
            {
                if (!PinCameraYaw) Session.CameraYaw = BroadcastCamera.YawRadians;
                Session.Advance(Time.deltaTime, null, _realClock);

                var cues = Session.FrameCues;
                for (int i = 0; i < cues.Count; i++)
                {
                    var cue = cues[i];
                    if (cue.Kind != CommentaryCueKind.Start || cue.Utterance == null) continue;
                    Subtitle = cue.Utterance.Transcript.Replace(" | ", " ");
                    SubtitleUntil = Time.unscaledTime + 4.5f;
                }
            }
            // After Advance, so a whistle this frame already stops the legs; drivers run later (order 100).
            bool advancing = SimAdvancing;
            bool freezePose = Frozen || Session.Paused;
            for (int id = 0; id < _drivers.Length; id++)
            {
                if (_drivers[id] == null) continue;
                _drivers[id].Halted = !advancing;
                _drivers[id].FreezePose = freezePose;
            }
            SyncVisuals();
            RotateBall(advancing ? Time.deltaTime : 0f);
        }

        /// <summary>
        /// Spins the ball mesh: rolling (v / r about Up x v, plus the sim's side spin) on the ground, the sim's
        /// angular velocity in flight. The sim's spin vector maps one-to-one onto Unity's AngleAxis convention.
        /// </summary>
        private void RotateBall(float dt)
        {
            if (dt <= 0f) return;
            var b = Session.Sim.State.Ball;
            SimVec3 w = b.Grounded ? BallIntegrator.RollingSpin(b.Velocity, _ballRadius, b.Spin.Y) : b.Spin;
            float mag = w.Magnitude;
            if (mag < 1e-3f) return;
            var axis = new Vector3(w.X / mag, w.Y / mag, w.Z / mag);
            _ball.rotation = Quaternion.AngleAxis(mag * dt * Mathf.Rad2Deg, axis) * _ball.rotation;
        }

        /// <summary>The controlled-player ring follows the player after the animation drivers (order 100) moved it.</summary>
        private void LateUpdate()
        {
            if (Session == null) return;
            UpdateControlledMarker();
        }

        private void UpdateControlledMarker()
        {
            int controlled = AiVsAi ? -1 : Session.ControlledId;
            bool show = controlled >= 0 && _players[controlled] != null && _players[controlled].gameObject.activeSelf;
            _marker.gameObject.SetActive(show);
            if (show)
            {
                _marker.position = _players[controlled].position + new Vector3(0f, 0.03f, 0f);
                BroadcastCamera.Player = _players[controlled];
            }
            else
            {
                BroadcastCamera.Player = null;
            }
        }

        private static bool PausePressed()
        {
            var pad = Gamepad.current;
            if (pad != null && pad.startButton.wasPressedThisFrame) return true;
            var kb = Keyboard.current;
            return kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame);
        }

        /// <summary>
        /// Copies sim state to the scene, rendering between the previous and the current fixed-step snapshot (alpha =
        /// accumulator remainder), the same scheme <see cref="PlayerAnimationDriver"/> uses for animated players, so the
        /// ball and the feet line up at contact. Players with a driver are moved by it.
        /// </summary>
        public void SyncVisuals()
        {
            if (Session == null) return;
            var s = Session.Sim.State;
            float alpha = Mathf.Clamp01(Session.InterpolationAlpha);
            for (int id = 0; id < MatchSimState.PlayerCount; id++)
            {
                _players[id].gameObject.SetActive(!s.Players[id].SentOff);
                if (_drivers[id] != null && _drivers[id].Animator != null) continue;
                var prev = Session.PreviousPositions[id];
                var cur = s.Players[id].Position;
                float yaw = Mathf.LerpAngle(Session.PreviousFacing[id] * Mathf.Rad2Deg, s.Players[id].Facing * Mathf.Rad2Deg, alpha);
                _players[id].SetPositionAndRotation(
                    new Vector3(Mathf.Lerp(prev.X, cur.X, alpha), 0f, Mathf.Lerp(prev.Z, cur.Z, alpha)),
                    Quaternion.Euler(0f, yaw, 0f));
            }
            var pb = Session.PreviousBall;
            var b = s.Ball.Position;
            _ball.position = new Vector3(Mathf.Lerp(pb.X, b.X, alpha), Mathf.Max(_ballRadius, Mathf.Lerp(pb.Y, b.Y, alpha)), Mathf.Lerp(pb.Z, b.Z, alpha));
        }

        public Transform PlayerTransform(int id) => _players[id];
        public Transform BallTransform => _ball;

        // ------------------------------------------------------------------ HUD helpers

        public string ClockText()
        {
            int t = ClockSeconds();
            return $"{t / 60:00}:{t % 60:00}";
        }

        /// <summary>Displayed match clock in whole seconds (90-minute scale). Allocation-free; the HUD caches the string.</summary>
        public int ClockSeconds()
        {
            if (Session == null) return 0;
            var s = Session.Sim.State;
            float minutes = s.Clock / Session.Sim.Config.HalfDurationSeconds * 45f + (s.Half >= 2 ? 45f : 0f);
            int m = Mathf.FloorToInt(minutes);
            int sec = Mathf.FloorToInt((minutes - m) * 60f);
            return m * 60 + sec;
        }

        public string HomeShort => HomeClub != null ? HomeClub.ShortName : "HOM";
        public string AwayShort => AwayClub != null ? AwayClub.ShortName : "AWY";
    }
}
