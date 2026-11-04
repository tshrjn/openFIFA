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
    /// -spHome clubId, -spAway clubId, -spLeague leagueId (ids from the roster JSON; both clubs from one league),
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
        [Tooltip("Match weather; sets the pitch condition for ball roll and bounce (rain = wet, snow = heavy).")]
        public WeatherType Weather = WeatherType.Clear;
        [Tooltip("Force a pitch condition instead of the weather's (Normal = use the weather's).")]
        public PitchSurface SurfaceOverride = PitchSurface.Normal;
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
        /// <summary>League the clubs are picked from (index into <see cref="Leagues"/>; -spLeague/-spHome/-spAway set it).</summary>
        public int LeagueIndex { get; set; }
        /// <summary>Club ids asked for on the command line (null = default), resolved after the rosters load.</summary>
        public string ArgLeague, ArgHome, ArgAway;
        /// <summary>
        /// -spHomeStyle / -spAwayStyle: force a team style (e.g. park-the-bus, high-press) over the club's own. The preset
        /// is used as written (TeamAITactics.FromStyle): the club's reputation offset (RosterMatchBuilder.TacticsFor) is
        /// dropped, so two clubs forced to one style press and hold their line identically. Meant for captures and tests.
        /// </summary>
        public string ArgHomeStyle, ArgAwayStyle;
        private int _defaultLeague, _defaultHome, _defaultAway = 1;
        public RosterClub HomeClub { get; private set; }
        public RosterClub AwayClub { get; private set; }
        public string[] Names { get; private set; } = new string[MatchSimState.PlayerCount];
        /// <summary>Squad numbers per sim id (roster shirt numbers, else 1-11).</summary>
        public int[] ShirtNumbers { get; private set; } = new int[MatchSimState.PlayerCount];
        /// <summary>Team colours of the current match (HUD accents).</summary>
        public Color HomeColour { get; private set; } = new Color(0.75f, 0.1f, 0.15f);
        public Color AwayColour { get; private set; } = new Color(0.95f, 0.95f, 0.97f);
        public bool Started => Session != null;
        /// <summary>Hold the match without showing the pause menu (review captures).</summary>
        public bool Frozen { get; set; }

        /// <summary>Latest commentary line (play-by-play or analyst) for subtitles.</summary>
        public string Subtitle { get; private set; } = string.Empty;
        public float SubtitleUntil { get; private set; }

        private CommentaryLineBank _commentaryBank;
        private CommentaryEngine _commentary;
        private CommentaryAudioPlayer _commentaryAudio;
        private bool _wasPaused;
        /// <summary>Voiced commentary (clips in StreamingAssets/Commentary/audio); subtitles show either way.</summary>
        public CommentaryAudioPlayer CommentaryAudio => _commentaryAudio;
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
            ApplyClubSelection();
            LoadCommentary();
            // The index is read in every mode: it is the engine's catalog, so a seed's commentary is the same with or
            // without sound. Loading and playback only happen with a real audio output outside captures. Clip timing
            // comes from the index, never from playback (no NotifyFinished), for the same reason.
            _commentaryAudio = gameObject.AddComponent<CommentaryAudioPlayer>();
            bool indexed = _commentaryAudio.LoadIndex("en");
            _commentaryAudio.Enabled = !_muteCommentary && !_captureRun && CommentaryAudioPlayer.AudioOutputAvailable();
            if (indexed)
                Debug.Log("[" + ProductName + "] commentary audio: " + _commentaryAudio.Index.Count + " clips" +
                          (_commentaryAudio.Enabled ? "" : " (catalog only, not played)"));
        }

        private void Start()
        {
            if (AutoStart) StartMatch(Level);
        }

        private bool _muteCommentary;
        private bool _captureRun;
        private CommentaryUtterance _subtitleUtterance;

        private void ClearSubtitle()
        {
            Subtitle = string.Empty;
            SubtitleUntil = 0f;
            _subtitleUtterance = null;
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
                else if (a == "-spWeather")
                {
                    if (MatchWeatherOption.TryParse(next, out var w, out var surface))
                    {
                        Weather = w;
                        SurfaceOverride = surface ?? PitchSurface.Normal;
                    }
                    else Debug.LogWarning("[" + ProductName + "] -spWeather expects " + MatchWeatherOption.Values + ", got '" + next + "'");
                }
                else if (a == "-spAutoStart") AutoStart = true;
                else if (a == "-spAiVsAi") AiVsAi = true;
                else if (a == "-spBot") ScriptedHuman = true;
                else if (a == "-spHome" && next != null) ArgHome = next;
                else if (a == "-spAway" && next != null) ArgAway = next;
                else if (a == "-spLeague" && next != null) ArgLeague = next;
                else if (a == "-spHomeStyle" && next != null) ArgHomeStyle = next;
                else if (a == "-spAwayStyle" && next != null) ArgAwayStyle = next;
                else if (a == "-spNoCommentary") _muteCommentary = true;
                else if (a == "-spCapture") _captureRun = true;
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
            builder.RunOffMetres = RunOffMetres; // grass beyond the lines instead of a dark void at the touchline
            builder.BuildPitch(new PitchConfigData(pitch.Length, pitch.Width, pitch.GoalWidth, 9.15f, 5.5f, pitch.GoalHeight, 1f, 0.5f));
            // Rotate after building: PitchBuilder parents with worldPositionStays, so a pre-rotated root would be undone.
            _pitchRoot.localRotation = Quaternion.Euler(0f, 90f, 0f);

            BuildGoal(+1, pitch);
            BuildGoal(-1, pitch);
            // Procedural stadium (stands, crowd, roofs, floodlights, LED ring 4.5 m back on the run-off).
            _stadium = ProceduralStadium.Build(transform, pitch, new Color(0.75f, 0.1f, 0.15f), new Color(0.1f, 0.3f, 0.8f));

            if (FindFirstObjectByType<Light>() == null)
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.shadows = LightShadows.Soft;
            }
            // Light from the camera side and high, so the far stand's shadow falls behind it, not on the pitch.
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional)
                {
                    // ~40 deg sun from the camera side, angled down the pitch: long readable player shadows that fall
                    // away from the broadcast camera.
                    l.transform.rotation = Quaternion.Euler(40f, -75f, 0f);
                    l.shadows = LightShadows.Soft; // resolution, cascades and bias come from the URP asset
                    BroadcastLighting.ConfigureSun(l); // warm tint, shadow strength 0.8, high soft-shadow filter
                }
            BroadcastLighting.ApplyAmbient(); // trilight fill so shaded sides are not black
            BroadcastLighting.ApplyQualityTier(); // iPad/mobile (or -spQuality low): lighter AA, shadows, no SSAO

            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Broadcast Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.11f, 0.16f);
            cam.farClipPlane = 600f;
            cam.nearClipPlane = 0.5f; // better depth precision for the lines and ring at broadcast distance
            var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()
                          ?? cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true; // ACES tonemapping / grade from the scene's global volume
            BroadcastLighting.ConfigureAntialiasing(camData); // SMAA on top of MSAA 4x
            BroadcastCamera = cam.GetComponent<SinglePlayerCamera>() ?? cam.gameObject.AddComponent<SinglePlayerCamera>();
            BroadcastCamera.HalfLength = pitch.HalfLength;
            BroadcastCamera.HalfWidth = pitch.HalfWidth;
            FitBroadcastCameraToStand();

            _ball = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            _ball.name = "Ball";
            SetBallRadius(new BallSimConfig().Radius);
            Destroy(_ball.GetComponent<Collider>());
            Tint(_ball.gameObject, Color.white);
            ApplyBallPattern(_ball.gameObject);
            BroadcastCamera.Ball = _ball;
            _ballShadow = BuildBallShadow();

            _marker = BuildMarkerRing();
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
            var netMat = BuildNet(root, side, z, hw, h);
            _netMaterials.Add(netMat);
            _netMouths.Add(new Vector3(0f, h * 0.5f, z));
        }

        /// <summary>Net depth at the roof (from the goal line) and at the ground (m).</summary>
        public const float NetTopDepth = 1.0f, NetBottomDepth = 2.0f;
        /// <summary>Net mesh cell size (m).</summary>
        public const float NetCell = 0.14f;
        /// <summary>Grass beyond the touch and goal lines (m).</summary>
        public const float RunOffMetres = 5f;

        private static Material _netMaterial;

        /// <summary>
        /// Net box behind the goal: roof, sloping back and two sides, drawn with a procedural cutout grid so the
        /// keeper and the far pitch stay visible through it. Sprites/Default is in the always-included shader list, so
        /// it blends in the player build without depending on a URP keyword variant being kept; it renders both faces.
        /// </summary>
        private static Material BuildNet(Transform root, int side, float z, float hw, float h)
        {
            float zt = z + side * NetTopDepth, zb = z + side * NetBottomDepth;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            // Roof: crossbar to the top back bar.
            NetQuad(verts, uvs, tris, new Vector3(-hw, h, z), new Vector3(hw, h, z), new Vector3(hw, h, zt), new Vector3(-hw, h, zt), 0);
            // Back: top back bar down to the ground bar.
            NetQuad(verts, uvs, tris, new Vector3(-hw, h, zt), new Vector3(hw, h, zt), new Vector3(hw, 0f, zb), new Vector3(-hw, 0f, zb), 1);
            // Sides: post, top, slope, ground.
            for (int sx = -1; sx <= 1; sx += 2)
                NetQuad(verts, uvs, tris, new Vector3(sx * hw, 0f, z), new Vector3(sx * hw, h, z), new Vector3(sx * hw, h, zt), new Vector3(sx * hw, 0f, zb), 2);

            var mesh = new Mesh { name = "GoalNet" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            var cols = new Color[verts.Count];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
            mesh.colors = cols;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var net = new GameObject("Net").transform;
            net.SetParent(root, false);
            net.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = net.gameObject.AddComponent<MeshRenderer>();
            var mat = new Material(NetMaterial()) { name = "GoalNet " + (side > 0 ? "+Z" : "-Z") };
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Back frame: the top back bar and the ground bars that hold the net (thin, grey).
            var frame = new Color(0.78f, 0.78f, 0.8f);
            float r = 0.03f;
            Bar(root, new Vector3(-hw, h, zt), new Vector3(hw, h, zt), r, frame);
            Bar(root, new Vector3(-hw, 0.02f, zb), new Vector3(hw, 0.02f, zb), r, frame);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Bar(root, new Vector3(sx * hw, h, zt), new Vector3(sx * hw, 0.02f, zb), r, frame);
                Bar(root, new Vector3(sx * hw, 0.02f, z), new Vector3(sx * hw, 0.02f, zb), r, frame);
            }
            return mat;
        }

        /// <summary>Quad a-b-c-d with UVs in net cells (axis 0: x/z, 1: x/slope, 2: z/y).</summary>
        private static void NetQuad(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, int plane)
        {
            int i = v.Count;
            foreach (var p in new[] { a, b, c, d })
            {
                v.Add(p);
                Vector2 q;
                if (plane == 0) q = new Vector2(p.x, p.z);
                else if (plane == 1) q = new Vector2(p.x, p.y * 1.08f); // slope is ~8% longer than its height
                else q = new Vector2(p.z, p.y);
                uv.Add(q / NetCell);
            }
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        private static void Bar(Transform parent, Vector3 from, Vector3 to, float radius, Color c)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            b.SetParent(parent, false);
            Vector3 d = to - from;
            b.localPosition = (from + to) * 0.5f;
            b.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            b.localScale = new Vector3(radius * 2f, d.magnitude * 0.5f, radius * 2f);
            Destroy(b.GetComponent<Collider>());
            Tint(b.gameObject, c);
        }

        /// <summary>Procedural net: white 2-texel strands on a 16-texel cell, transparent between; mipmaps fade it to a light veil at distance.</summary>
        private static Material NetMaterial()
        {
            if (_netMaterial != null) return _netMaterial;
            const int N = 16;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    bool strand = x < 2 || y < 2;
                    px[y * N + x] = strand ? new Color32(240, 240, 240, 190) : new Color32(255, 255, 255, 0);
                }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true)
            {
                name = "NetGrid", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4,
            };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            _netMaterial = new Material(shader) { name = "GoalNet", mainTexture = tex };
            return _netMaterial;
        }

        /// <summary>
        /// Marker ring radii (m) and its lift above the grass surface (the pitch cube's top is at 0.05 m). About 35%
        /// smaller than the first version (0.62 / 0.8), which dominated the player silhouette at broadcast distance.
        /// </summary>
        public const float RingInner = 0.4f, RingOuter = 0.52f, RingY = 0.09f;

        /// <summary>
        /// Controlled-player ring: a flat annulus mesh, unlit (Sprites/Default, transparent queue, no z-write, no shadows),
        /// 4 cm above the grass. The old marker was a 1.6 m cylinder whose top sat exactly on the pitch surface (y 0.05),
        /// so it z-fought with the grass and showed as flickering yellow shards.
        /// </summary>
        private static Transform BuildMarkerRing()
        {
            const int seg = 64;
            var verts = new Vector3[seg * 2];
            var cols = new Color[seg * 2];
            var tris = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = d * RingOuter;
                verts[i * 2 + 1] = d * RingInner;
                cols[i * 2] = cols[i * 2 + 1] = Color.white;
                int j = (i + 1) % seg;
                // Clockwise seen from above (front face up); the shader is double-sided anyway.
                tris[i * 6] = i * 2; tris[i * 6 + 1] = i * 2 + 1; tris[i * 6 + 2] = j * 2;
                tris[i * 6 + 3] = j * 2; tris[i * 6 + 4] = i * 2 + 1; tris[i * 6 + 5] = j * 2 + 1;
            }
            var mesh = new Mesh { name = "MarkerRing", vertices = verts, colors = cols, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("ControlledMarker");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader) { name = "MarkerRing", color = new Color(1f, 0.85f, 0.1f, 0.95f) };
            m.renderQueue = 3100; // after the grass, lines and net
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
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

        /// <summary>Applies -spLeague/-spHome/-spAway; these become the defaults the menu returns to.</summary>
        private void ApplyClubSelection()
        {
            int l = LeagueIndex, h = HomeClubIndex, w = AwayClubIndex;
            if (RosterSelection.TryResolve(Leagues, ArgLeague, ArgHome, ArgAway, ref l, ref h, ref w, RosterProblems))
            {
                LeagueIndex = l;
                HomeClubIndex = h;
                AwayClubIndex = w;
            }
            else
            {
                Debug.LogWarning("[" + ProductName + "] club selection ignored: " + RosterProblems[RosterProblems.Count - 1] +
                                 "\n" + RosterSelection.Describe(Leagues));
            }
            _defaultLeague = LeagueIndex;
            _defaultHome = HomeClubIndex;
            _defaultAway = AwayClubIndex;
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

        public IReadOnlyList<RosterClub> Clubs => Leagues.Count > 0 ? Leagues[Mathf.Clamp(LeagueIndex, 0, Leagues.Count - 1)].Clubs : null;

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
                Weather = Weather,
                Surface = SurfaceOverride != PitchSurface.Normal ? SurfaceOverride : (PitchSurface?)null,
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
                options.TeamNames = new[] { HomeClub.Name, AwayClub.Name };
                options.PlayerIds = ids;
                Names = names;
                // Per sim id from each side's own sheet (two clubs can both field a No. 11: that is roster data, not a bug).
                ShirtNumbers = MatchSheetNumbers.Build(hs, aws);
                homeColor = ParseColour(HomeClub.PrimaryColour, homeColor);
                awayColor = ParseColour(AwayClub.PrimaryColour, awayColor);
                if (ColourDistance(homeColor, awayColor) < 0.35f) awayColor = ParseColour(AwayClub.AwayColour, awayColor);
            }
            else
            {
                Names = new string[MatchSimState.PlayerCount];
                for (int i = 0; i < Names.Length; i++) Names[i] = (i < 11 ? "H" : "A") + (i % 11 + 1);
                ShirtNumbers = MatchSheetNumbers.Default();
            }
            options.HomeTactics = StyleOverride(ArgHomeStyle, options.HomeTactics, options.Setup, true);
            options.AwayTactics = StyleOverride(ArgAwayStyle, options.AwayTactics, options.Setup, false);
            HomeColour = homeColor;
            AwayColour = awayColor;
            return options;
        }

        /// <summary>
        /// Tactics for a forced style (-spHomeStyle / -spAwayStyle), keeping the side's formation (the club's, or the
        /// default setup's best fit); the given tactics when no style was asked for or the name is unknown.
        /// </summary>
        private TeamAITactics StyleOverride(string styleName, TeamAITactics current, MatchSetup setup, bool home)
        {
            if (string.IsNullOrEmpty(styleName)) return current;
            if (!TeamAITactics.TryParseStyle(styleName, out var style))
            {
                Debug.LogWarning("[" + ProductName + "] unknown team style '" + styleName + "'");
                return current;
            }
            var spots = setup != null ? (home ? setup.HomeFormation : setup.AwayFormation)
                : (home ? MatchSetup.CreateDefault(Seed).HomeFormation : MatchSetup.CreateDefault(Seed).AwayFormation);
            var formation = current != null ? current.Formation : SimFormationMapper.BestFit(spots);
            Debug.Log("[" + ProductName + "] " + (home ? "home" : "away") + " style forced to " + style);
            return TeamAITactics.FromStyle(style, formation);
        }

        /// <summary>Input for the user's seat: null in AI vs AI, the scripted bot, or the device reader.</summary>
        public IHumanInputSource CreateHumanInput() =>
            AiVsAi ? null : ScriptedHuman ? (IHumanInputSource)new ScriptedGamepadBot() : Input;

        public void StartMatch(TeamAIDifficultyLevel level)
        {
            Level = level;
            var options = BuildOptions(level, out var homeColor, out var awayColor);
            if (_stadium != null) _stadium.SetTeamColours(homeColor, awayColor);
            Session = new SinglePlayerMatchSession(options);
            Input = new SinglePlayerInput(Session.Profile.PassAssist);
            Session.HumanSlots[0].Input = CreateHumanInput();
            _commentary = _commentaryBank != null ? new CommentaryEngine(_commentaryBank, null, (uint)(Seed & 0xffffffff) | 1u) : null;
            if (_commentary != null)
            {
                _commentary.Catalog = _commentaryAudio != null ? _commentaryAudio.Index : null;
                Session.AttachCommentaryEngine(_commentary);
                _commentaryAudio?.Preload(Names, options.TeamNames);
            }
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
            if (_commentaryAudio != null)
            {
                _commentaryAudio.StopAll();
                _commentaryAudio.UnloadMatchClips();
            }
            _subtitleUtterance = null;
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

        /// <summary>Back to the picker with the launch defaults (level, the command-line clubs or the first two).</summary>
        public void QuitToMenu()
        {
            EndMatch();
            Level = _defaultLevel;
            LeagueIndex = _defaultLeague;
            HomeClubIndex = _defaultHome;
            AwayClubIndex = _defaultAway;
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
                    driver.Ball = _ball; // explicit: no scene lookup by name or tag
                    driver.VisualBallRadius = VisualBallRadius;
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
                BroadcastCamera.AttackDirection = BroadcastFraming.AttackDirection(Session.Sim);
                _commentary?.SetCrowdIntensity(_excitement);
                Session.Advance(Time.deltaTime, null, _realClock);

                var cues = Session.FrameCues;
                if (_commentaryAudio != null)
                {
                    if (Session.Paused != _wasPaused)
                    {
                        _wasPaused = Session.Paused;
                        if (_wasPaused)
                        {
                            _commentaryAudio.StopAll();
                            ClearSubtitle();
                        }
                    }
                    if (!Session.Paused) _commentaryAudio.Handle(cues);
                }
                for (int i = 0; i < cues.Count; i++)
                {
                    var cue = cues[i];
                    var u = cue.Utterance;
                    if (u == null) continue;
                    if (cue.Kind == CommentaryCueKind.Stop)
                    {
                        if (ReferenceEquals(u, _subtitleUtterance)) ClearSubtitle();
                        continue;
                    }
                    if (Session.Paused) continue;
                    _subtitleUtterance = u;
                    Subtitle = u.Transcript.Replace(" | ", " ");
                    SubtitleUntil = Time.unscaledTime + Mathf.Max(2.5f, (float)(u.EndTime - u.StartTime) + 1.2f);
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
            UpdateBallScale(Time.unscaledDeltaTime);
            RotateBall(advancing ? Time.deltaTime : 0f);
            if (!Frozen) UpdateCrowd(advancing ? Time.deltaTime : 0f);
        }

        /// <summary>
        /// Spins the ball mesh: rolling (v / r about Up x v, plus the sim's side spin) on the ground, the sim's
        /// angular velocity in flight. The sim's spin vector maps one-to-one onto Unity's AngleAxis convention.
        /// </summary>
        private void RotateBall(float dt)
        {
            if (dt <= 0f) return;
            var b = Session.Sim.State.Ball;
            SimVec3 w = b.Grounded ? BallIntegrator.RollingSpin(b.Velocity, VisualBallRadius, b.Spin.Y) : b.Spin; // rolls without slip as drawn
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
            UpdateBallVisual();
            UpdateNetFade();
        }

        private ProceduralStadium _stadium;

        /// <summary>
        /// The broadcast camera rides above the main (near) stand. Lift it until, over its whole dolly range, it is
        /// outside the stand and roof and has a clear line to the near touchline (the stand's rows rise to ~17 m at
        /// 30 m back, so a 15 m camera at that depth sat inside the seating and showed its underside).
        /// </summary>
        private void FitBroadcastCameraToStand()
        {
            if (_stadium == null || _stadium.Layout == null || BroadcastCamera == null) return;
            var stand = _stadium.Layout.MainStand;
            float far = BroadcastCamera.BroadcastSetback;
            float near = far - SinglePlayerCamera.DollyIn * BroadcastCamera.HalfWidth;
            float h = BroadcastCamera.BroadcastHeight;
            for (int k = 0; k < 40; k++)
            {
                bool ok = true;
                for (float d = near; d <= far + 0.01f && ok; d += 0.5f)
                    ok = !stand.Contains(d, h, true) && stand.SegmentClear(d, h, 0f, 0f, true, 0.25f)
                         && h <= stand.RoofUndersideAt(d) - 1f;
                if (ok) break;
                h += 0.5f;
            }
            BroadcastCamera.BroadcastHeight = h;
        }
        private float _excitement = CrowdBaseline;
        /// <summary>Crowd excitement at rest and how fast it settles back (per second).</summary>
        public const float CrowdBaseline = 0.25f, CrowdDecay = 0.12f;

        /// <summary>
        /// Crowd reaction from this frame's sim events: a goal peaks it, shots/saves/woodwork lift it, fouls stir it,
        /// then it settles back to the baseline. Allocation-free.
        /// </summary>
        private void UpdateCrowd(float dt)
        {
            if (_stadium == null || Session == null) return;
            var ev = Session.FrameEvents;
            for (int i = 0; i < ev.Count; i++)
            {
                switch (ev[i].Type)
                {
                    case MatchEventType.Goal: _excitement = 1f; break;
                    case MatchEventType.PostHit:
                    case MatchEventType.CrossbarHit:
                    case MatchEventType.Save: _excitement = Mathf.Max(_excitement, 0.75f); break;
                    case MatchEventType.Shot: _excitement = Mathf.Max(_excitement, 0.55f); break;
                    case MatchEventType.Foul:
                    case MatchEventType.YellowCard:
                    case MatchEventType.RedCard: _excitement = Mathf.Max(_excitement, 0.45f); break;
                }
            }
            _excitement = Mathf.MoveTowards(_excitement, CrowdBaseline, CrowdDecay * dt);
            _stadium.SetExcitement(_excitement);
        }

        private Transform _ballShadow;
        private Material _ballShadowMat;
        private readonly List<Material> _netMaterials = new List<Material>(2);
        private readonly List<Vector3> _netMouths = new List<Vector3>(2);

        /// <summary>
        /// Smallest on-screen ball diameter (px at the current resolution) before the visual is scaled up. Set above the
        /// 6 px target because the shaded underside and MSAA eat about a pixel (6 px requested measured 3-5 px).
        /// </summary>
        /// <remarks>
        /// The cap was 3x: a 0.33 m ball next to feet whose IK aimed at the 0.11 m sim ball. 1.8x keeps it readable on
        /// the broadcast camera (the camera is 30-50 m away) without dwarfing the boots.
        /// </remarks>
        public const float MinBallPixels = 7.5f, MaxBallScale = 1.8f;
        /// <summary>Time constant (s) of the visual ball scale: the camera dollying and zooming no longer makes it pulse.</summary>
        public const float BallScaleSmoothing = 0.6f;

        private float _ballScale = 1f;
        private bool _ballScaleInit;

        /// <summary>Radius (m) the ball is drawn at: the sim radius times the readability scale. Foot IK aims at this surface.</summary>
        public float VisualBallRadius => _ballRadius * _ballScale;

        /// <summary>
        /// Visual ball scale for readability (never below true size, at most <see cref="MaxBallScale"/>), smoothed over
        /// time. Runs in Update, before the animation drivers' IK, so the feet aim at the ball as it is drawn this frame.
        /// </summary>
        private void UpdateBallScale(float dt)
        {
            var cam = BroadcastCamera != null ? BroadcastCamera.GetComponent<Camera>() : null;
            if (cam == null || _ball == null) return;
            float d = Vector3.Distance(cam.transform.position, _ball.position);
            float px = 2f * _ballRadius * Screen.height / (2f * Mathf.Max(0.1f, d) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float k = BroadcastFraming.BallScale(px, MinBallPixels, MaxBallScale);
            if (!_ballScaleInit) { _ballScale = k; _ballScaleInit = true; }
            else if (dt > 0f) _ballScale += (k - _ballScale) * (1f - Mathf.Exp(-dt / BallScaleSmoothing));
            float r = VisualBallRadius;
            for (int id = 0; id < _drivers.Length; id++)
                if (_drivers[id] != null) _drivers[id].VisualBallRadius = r;
        }

        /// <summary>
        /// Ball readability at broadcast distance: draws the ball at the smoothed scale (<see cref="UpdateBallScale"/>),
        /// keeps it resting on the grass, and places the contact shadow blob under it (larger and fainter with height).
        /// </summary>
        private void UpdateBallVisual()
        {
            if (_ball == null) return;
            Vector3 p = _ball.position;
            float r = VisualBallRadius;
            _ball.localScale = Vector3.one * (2f * r);
            if (p.y < r) _ball.position = new Vector3(p.x, r, p.z);

            if (_ballShadow != null)
            {
                float h = Mathf.Max(0f, p.y - _ballRadius);
                // Contact shadow: about 0.5 m and dark on the ground, spreading and fading as the ball rises.
                float size = Mathf.Max(0.5f, 2.4f * r) * (1f + h * 0.12f);
                _ballShadow.position = new Vector3(p.x, RingY - 0.01f, p.z);
                _ballShadow.localScale = new Vector3(size, 1f, size);
                _ballShadowMat.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.8f, 0.15f, Mathf.Clamp01(h / 6f)));
            }
        }

        private Transform BuildBallShadow()
        {
            const int N = 64;
            var px = new Color32[N * N];
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    // Solid core to ~45% of the radius, then a soft falloff: reads as a contact shadow, not a smudge.
                    float a = Mathf.Clamp01((1f - d) / 0.55f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * a * (3f - 2f * a) * 255f));
                }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "BallShadow" };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            var mesh = new Mesh
            {
                name = "BallShadowQuad",
                vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateBounds();
            var go = new GameObject("BallShadow");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            _ballShadowMat = new Material(shader) { name = "BallShadow", mainTexture = tex, color = new Color(0f, 0f, 0f, 0.5f) };
            _ballShadowMat.renderQueue = 3050;
            mr.sharedMaterial = _ballShadowMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        /// <summary>Fades a goal's net when the camera is close behind it (goal-area views), so it does not wall off play.</summary>
        private void UpdateNetFade()
        {
            var cam = BroadcastCamera != null ? BroadcastCamera.transform : null;
            if (cam == null) return;
            for (int i = 0; i < _netMaterials.Count; i++)
            {
                Vector3 mouth = _netMouths[i];
                float d = Vector3.Distance(cam.position, mouth);
                bool behind = Mathf.Sign(cam.position.z) == Mathf.Sign(mouth.z) && Mathf.Abs(cam.position.z) > Mathf.Abs(mouth.z);
                float a = behind ? Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(10f, 30f, d)) : 1f;
                _netMaterials[i].color = new Color(1f, 1f, 1f, a);
            }
        }

        private void UpdateControlledMarker()
        {
            int controlled = AiVsAi ? -1 : Session.ControlledId;
            bool show = controlled >= 0 && _players[controlled] != null && _players[controlled].gameObject.activeSelf;
            _marker.gameObject.SetActive(show);
            if (show)
            {
                var pp = _players[controlled].position;
                _marker.position = new Vector3(pp.x, RingY, pp.z);
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
