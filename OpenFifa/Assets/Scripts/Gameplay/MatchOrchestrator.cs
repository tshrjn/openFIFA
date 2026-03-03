using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using OpenFifa.Core;
using OpenFifa.AI;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Central match bootstrapper. Wires all existing systems into a playable
    /// Rush mode (4v4, no goalkeeper) match at runtime using primitive geometry.
    /// Attach to a single GameObject in the Match scene — everything else is
    /// created programmatically.
    /// </summary>
    public class MatchOrchestrator : MonoBehaviour
    {
        [Header("Match Settings")]
        [SerializeField] private float _halfDuration = 180f;
        [SerializeField] private bool _rushMode = true;

        [Header("Team Colors")]
        [SerializeField] private Color _homeColor = new Color(0.2f, 0.4f, 0.9f);
        [SerializeField] private Color _awayColor = new Color(0.9f, 0.2f, 0.2f);

        // Runtime state
        private MatchStateMachine _stateMachine;
        private MatchTimer _matchTimer;
        private MatchScore _matchScore;
        private KickoffLogic _kickoffLogic;

        // Scene objects
        private Transform _ball;
        private BallController _ballController;
        private BallOwnership _ballOwnership;
        private readonly List<GameObject> _homePlayers = new List<GameObject>();
        private readonly List<GameObject> _awayPlayers = new List<GameObject>();
        private GameObject _activePlayer;
        private BroadcastCameraController _cameraController;

        // HUD
        private TextMeshProUGUI _scoreText;
        private TextMeshProUGUI _timerText;
        private TextMeshProUGUI _periodText;

        // Config
        private PitchConfigData _pitchConfig;
        private FormationLayoutData _formation;

        private void Awake()
        {
            _pitchConfig = new PitchConfigData();
            _formation = _rushMode
                ? FormationLayoutData.CreateRush4v4()
                : FormationLayoutData.CreateDefault212();

            CreatePitch();
            CreateGoalPosts();
            CreateBall();
            CreateGoalDetectors();
            SpawnTeam(isHome: true);
            SpawnTeam(isHome: false);
            SetupCamera();
            CreateHUD();
            SetupStadiumLighting();
            CreateStadiumEnvironment();
            SetupPostProcessing();
            FixEventSystem();
        }

        private void Start()
        {
            // Initialize match systems
            _stateMachine = new MatchStateMachine();
            _matchTimer = new MatchTimer(_halfDuration);
            _matchScore = new MatchScore();
            _kickoffLogic = new KickoffLogic();

            // Wire goal detection
            GoalDetector.OnGoalScored += HandleGoalScored;

            // Wire timer events
            _matchTimer.OnPeriodChanged += HandlePeriodChanged;
            _matchTimer.OnTimeUpdated += HandleTimeUpdated;

            // Set player layer mask on BallOwnership so it can detect nearby players
            if (_ballOwnership != null)
            {
                var field = typeof(BallOwnership).GetField("_playerLayerMask",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    LayerMask mask = LayerMask.GetMask("Default");
                    field.SetValue(_ballOwnership, mask);
                }
            }

            // Start the match
            ResetForKickoff();
            _matchTimer.StartMatch();
            _stateMachine.TransitionTo(MatchState.FirstHalf);
            UpdateHUD();

            // Minimap radar (needs teams + ball ready)
            CreateMinimap();
        }

        private void Update()
        {
            if (_stateMachine == null) return;

            var state = _stateMachine.CurrentState;

            // Tick timer during active play
            if (state == MatchState.FirstHalf || state == MatchState.SecondHalf)
            {
                _matchTimer.Tick(Time.deltaTime);
                UpdateAICoordination();
            }

            // Handle keyboard input for the active player
            HandleInput();
        }

        private void OnDestroy()
        {
            GoalDetector.OnGoalScored -= HandleGoalScored;
        }

        // === PITCH ===

        private void CreatePitch()
        {
            var pitchRoot = new GameObject("PitchRoot");
            var pitchBuilder = pitchRoot.AddComponent<PitchBuilder>();
            pitchBuilder.BuildPitch(_pitchConfig);
        }

        // === GOAL POSTS ===

        private void CreateGoalPosts()
        {
            float halfLength = _pitchConfig.HalfLength;

            CreateGoalFrame(new Vector3(halfLength, 0f, 0f), "GoalFrame_East");
            CreateGoalFrame(new Vector3(-halfLength, 0f, 0f), "GoalFrame_West");
        }

        private void CreateGoalFrame(Vector3 position, string name)
        {
            float goalWidth = _pitchConfig.GoalWidth;
            float goalHeight = 2.44f;
            float postRadius = 0.06f;
            float halfWidth = goalWidth / 2f;

            var root = new GameObject(name);
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

            // Goal net — procedural grid mesh (back + two sides + roof)
            float netDepth = _pitchConfig.GoalAreaDepth;
            float sign = position.x > 0 ? 1f : -1f;
            var netMat = CreateNetMaterial();

            // Back net (behind goal line)
            CreateNetPanel(root.transform, "NetBack",
                new Vector3(sign * netDepth, goalHeight / 2f, 0f),
                Quaternion.Euler(0f, 90f, 0f),
                goalWidth, goalHeight, 12, 6);

            // Side nets (left and right)
            CreateNetPanel(root.transform, "NetSideLeft",
                new Vector3(sign * netDepth / 2f, goalHeight / 2f, -halfWidth),
                Quaternion.identity,
                netDepth, goalHeight, 4, 6);

            CreateNetPanel(root.transform, "NetSideRight",
                new Vector3(sign * netDepth / 2f, goalHeight / 2f, halfWidth),
                Quaternion.identity,
                netDepth, goalHeight, 4, 6);

            // Roof net
            CreateNetPanel(root.transform, "NetRoof",
                new Vector3(sign * netDepth / 2f, goalHeight, 0f),
                Quaternion.Euler(90f, 0f, 0f),
                netDepth, goalWidth, 4, 12);
        }

        // === BALL ===

        private void CreateBall()
        {
            var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "Ball";
            ballGo.tag = "Ball";
            ballGo.transform.position = new Vector3(0f, 0.5f, 0f);
            ballGo.transform.localScale = Vector3.one * 0.22f;

            ballGo.GetComponent<MeshRenderer>().sharedMaterial = CreateSoccerBallMaterial();

            _ballController = ballGo.AddComponent<BallController>();
            _ballOwnership = ballGo.AddComponent<BallOwnership>();
            _ball = ballGo.transform;

            // BallController sets SphereCollider.radius=0.11 (designed for scale 1.0).
            // Since we scale the ball to 0.22, reset radius to 0.5 so the collider
            // fills the visual mesh — world radius becomes 0.5 * 0.22 = 0.11m (correct).
            var sphereCollider = ballGo.GetComponent<SphereCollider>();
            if (sphereCollider != null) sphereCollider.radius = 0.5f;

            // Drop shadow for depth perception
            ballGo.AddComponent<BallShadow>();
        }

        // === GOAL DETECTORS ===

        private void CreateGoalDetectors()
        {
            float halfLength = _pitchConfig.HalfLength;
            float goalWidth = _pitchConfig.GoalWidth;

            // East goal detector (ball entering = TeamA scores, since TeamB defends east)
            CreateGoalDetector(
                new Vector3(halfLength + 1f, 1.5f, 0f),
                new Vector3(2f, 3f, goalWidth),
                TeamIdentifier.TeamA,
                "GoalDetector_East");

            // West goal detector (ball entering = TeamB scores, since TeamA defends west)
            CreateGoalDetector(
                new Vector3(-(halfLength + 1f), 1.5f, 0f),
                new Vector3(2f, 3f, goalWidth),
                TeamIdentifier.TeamB,
                "GoalDetector_West");
        }

        private void CreateGoalDetector(Vector3 position, Vector3 size,
            TeamIdentifier scoringTeam, string name)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            var boxCollider = go.AddComponent<BoxCollider>();
            boxCollider.size = size;
            boxCollider.isTrigger = true;

            var detector = go.AddComponent<GoalDetector>();
            detector.Initialize(scoringTeam);
            detector.SetBallReference(_ball);
        }

        // === PLAYERS ===

        private void SpawnTeam(bool isHome)
        {
            var team = isHome ? TeamIdentifier.TeamA : TeamIdentifier.TeamB;
            var color = isHome ? _homeColor : _awayColor;
            var positions = _formation.GetWorldPositions(isHome, _pitchConfig.PitchLength);
            var playerList = isHome ? _homePlayers : _awayPlayers;

            for (int i = 0; i < positions.Length; i++)
            {
                bool isHumanControlled = isHome && i == 2; // Midfielder is human-controlled
                var worldPos = new Vector3(positions[i].x, 1f, positions[i].z);
                var player = CreatePlayer(worldPos, team, i, color, isHumanControlled, positions[i]);
                playerList.Add(player);

                if (isHumanControlled)
                {
                    _activePlayer = player;
                }
            }
        }

        private GameObject CreatePlayer(Vector3 position, TeamIdentifier team, int index,
            Color teamColor, bool isHumanControlled, FormationPosition formPos)
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = $"{team}_Player_{index}";
            player.transform.position = position;
            player.transform.localScale = new Vector3(0.6f, 1f, 0.6f);

            // Team color
            player.GetComponent<MeshRenderer>().sharedMaterial = CreateColorMaterial(teamColor);

            // Physics — add explicitly before PlayerController (which also requires it)
            var rb = player.AddComponent<Rigidbody>();
            rb.mass = 70f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationY
                           | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.useGravity = true;

            // Zero-friction so velocity-based movement works
            var capsule = player.GetComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.3f;
            capsule.center = new Vector3(0f, 0f, 0f);
            var physicsMat = new PhysicsMaterial("PlayerMat");
            physicsMat.dynamicFriction = 0f;
            physicsMat.staticFriction = 0f;
            physicsMat.frictionCombine = PhysicsMaterialCombine.Minimum;
            capsule.sharedMaterial = physicsMat;

            // Identity
            var identity = player.AddComponent<PlayerIdentity>();
            identity.Configure(index, team, $"Player {index + 1}");

            // Movement controller (human)
            var controller = player.AddComponent<PlayerController>();
            controller.Initialize(new PlayerStatsData());
            controller.enabled = isHumanControlled;

            // AI controller
            var aiController = player.AddComponent<AIController>();
            var formationWorldPos = new Vector3(formPos.x, 0f, formPos.z);
            aiController.Initialize(new AIConfigData(), _ball, formationWorldPos);
            aiController.enabled = !isHumanControlled;

            // Kicking and tackling
            player.AddComponent<PlayerKicker>();
            player.AddComponent<TackleSystem>();

            // Floating name label above head
            CreatePlayerLabel(player, index + 1, teamColor);

            // Active player indicator — flat ring at feet
            if (isHumanControlled)
            {
                CreatePlayerIndicatorRing(player);
            }

            return player;
        }

        // === CAMERA ===

        private void SetupCamera()
        {
            var mainCam = Camera.main;
            if (mainCam == null) return;

            _cameraController = mainCam.GetComponent<BroadcastCameraController>();
            if (_cameraController == null)
            {
                _cameraController = mainCam.gameObject.AddComponent<BroadcastCameraController>();
            }

            var camConfig = new CameraConfigData(
                elevationAngle: 40f,
                followDamping: 0.8f,
                distance: 30f,
                fieldOfView: 55f,
                ballTrackingWeight: 1f,
                playerTrackingWeight: 0.3f,
                minHeight: 8f
            );

            Transform playerTarget = _activePlayer != null ? _activePlayer.transform : null;
            _cameraController.Initialize(camConfig, _ball, playerTarget);
        }

        // === HUD (Broadcast-style scoreboard) ===

        private void CreateHUD()
        {
            var canvas = new GameObject("HUDCanvas");
            var canvasComp = canvas.AddComponent<Canvas>();
            canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasComp.sortingOrder = 10;

            var scaler = canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // === Scoreboard bar — horizontal strip across top ===
            var scoreBar = CreateUIPanel(canvas.transform, "ScoreBar",
                new Vector2(0.30f, 0.93f), new Vector2(0.70f, 0.99f),
                new Color(0.08f, 0.08f, 0.12f, 0.92f));

            // Home team badge (color block + name)
            var homeBadge = CreateUIPanel(scoreBar.transform, "HomeBadge",
                new Vector2(0f, 0f), new Vector2(0.30f, 1f),
                new Color(_homeColor.r, _homeColor.g, _homeColor.b, 0.85f));
            CreateHUDText(homeBadge.transform, "HomeLabel",
                new Vector2(0.05f, 0f), new Vector2(0.95f, 1f),
                "HOME", 28, TextAlignmentOptions.Center, Color.white);

            // Score display (center)
            var scorePanel = CreateUIPanel(scoreBar.transform, "ScorePanel",
                new Vector2(0.30f, 0f), new Vector2(0.70f, 1f),
                new Color(0.05f, 0.05f, 0.08f, 0.95f));
            _scoreText = CreateHUDText(scorePanel.transform, "ScoreText",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                "0 - 0", 48, TextAlignmentOptions.Center, Color.white);

            // Away team badge (color block + name)
            var awayBadge = CreateUIPanel(scoreBar.transform, "AwayBadge",
                new Vector2(0.70f, 0f), new Vector2(1f, 1f),
                new Color(_awayColor.r, _awayColor.g, _awayColor.b, 0.85f));
            CreateHUDText(awayBadge.transform, "AwayLabel",
                new Vector2(0.05f, 0f), new Vector2(0.95f, 1f),
                "AWAY", 28, TextAlignmentOptions.Center, Color.white);

            // === Timer pill — below scorebar, centered ===
            var timerPill = CreateUIPanel(canvas.transform, "TimerPill",
                new Vector2(0.43f, 0.885f), new Vector2(0.57f, 0.93f),
                new Color(0.12f, 0.12f, 0.18f, 0.90f));
            _timerText = CreateHUDText(timerPill.transform, "TimerText",
                new Vector2(0f, 0f), new Vector2(0.55f, 1f),
                "03:00", 26, TextAlignmentOptions.Center, Color.white);
            _periodText = CreateHUDText(timerPill.transform, "PeriodText",
                new Vector2(0.55f, 0f), new Vector2(1f, 1f),
                "1ST", 20, TextAlignmentOptions.Center, new Color(0.7f, 0.8f, 1f));

            // === Controls hint — bottom center, subtle ===
            var hintBar = CreateUIPanel(canvas.transform, "HintBar",
                new Vector2(0.15f, 0f), new Vector2(0.85f, 0.04f),
                new Color(0f, 0f, 0f, 0.5f));
            CreateHUDText(hintBar.transform, "ControlsHint",
                new Vector2(0.02f, 0f), new Vector2(0.98f, 1f),
                "WASD: Move  |  Shift: Sprint  |  Space: Pass  |  F/Click: Shoot  |  E: Tackle  |  Q: Switch",
                16, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.7f));
        }

        private RectTransform CreateUIPanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = color;

            return rect;
        }

        private TextMeshProUGUI CreateHUDText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string text, float fontSize, TextAlignmentOptions alignment,
            Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color ?? Color.white;
            tmp.fontStyle = FontStyles.Bold;

            return tmp;
        }

        // === MINIMAP RADAR (B5) ===

        private void CreateMinimap()
        {
            var hudCanvas = GameObject.Find("HUDCanvas");
            if (hudCanvas == null) return;

            var minimapGo = new GameObject("MinimapRadar");
            minimapGo.transform.SetParent(hudCanvas.transform, false);

            var rect = minimapGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.82f, 0.06f);
            rect.anchorMax = new Vector2(0.98f, 0.30f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var radar = minimapGo.AddComponent<MinimapRadar>();
            radar.Initialize(
                _pitchConfig.PitchLength,
                _pitchConfig.PitchWidth,
                _ball, _homePlayers, _awayPlayers);
        }

        // === STADIUM LIGHTING (B1.2) ===

        private void SetupStadiumLighting()
        {
            var cfg = StadiumLightingConfig.CreateNightStadium();

            // Dark night sky — no skybox, just solid dark color
            RenderSettings.skybox = null;
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.04f, 0.05f, 0.08f);

            // Dark ambient for night stadium atmosphere
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(cfg.AmbientR, cfg.AmbientG, cfg.AmbientB);

            // Remove scene directional light — we replace it with floodlights + fill
            var existingLight = FindAnyObjectByType<Light>();
            if (existingLight != null && existingLight.type == LightType.Directional)
            {
                Destroy(existingLight.gameObject);
            }

            // Fill light (soft directional from above)
            var fillGo = new GameObject("FillLight");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(cfg.FillColorR, cfg.FillColorG, cfg.FillColorB);
            fill.intensity = cfg.FillIntensity;
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 4 corner floodlights
            float halfLength = _pitchConfig.HalfLength;
            float halfWidth = _pitchConfig.HalfWidth;
            float offset = 5f; // slightly outside pitch
            var floodColor = new Color(cfg.FloodlightColorR, cfg.FloodlightColorG, cfg.FloodlightColorB);

            Vector3[] corners = new[]
            {
                new Vector3(halfLength + offset, cfg.FloodlightHeight, halfWidth + offset),
                new Vector3(halfLength + offset, cfg.FloodlightHeight, -(halfWidth + offset)),
                new Vector3(-(halfLength + offset), cfg.FloodlightHeight, halfWidth + offset),
                new Vector3(-(halfLength + offset), cfg.FloodlightHeight, -(halfWidth + offset))
            };

            for (int i = 0; i < corners.Length; i++)
            {
                var floodGo = new GameObject($"Floodlight_{i}");
                floodGo.transform.position = corners[i];
                floodGo.transform.LookAt(Vector3.zero);

                var floodLight = floodGo.AddComponent<Light>();
                floodLight.type = LightType.Spot;
                floodLight.color = floodColor;
                floodLight.intensity = cfg.FloodlightIntensity;
                floodLight.range = cfg.FloodlightRange;
                floodLight.spotAngle = cfg.FloodlightSpotAngle;
                floodLight.shadows = LightShadows.Soft;
                floodLight.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
            }
        }

        // === STADIUM ENVIRONMENT (B2) ===

        private void CreateStadiumEnvironment()
        {
            var envRoot = new GameObject("StadiumEnvironment");
            float hl = _pitchConfig.HalfLength;
            float hw = _pitchConfig.HalfWidth;

            // Floodlight towers at 4 corners
            float towerOffset = 5f;
            CreateFloodlightTower(envRoot.transform, new Vector3(hl + towerOffset, 0f, hw + towerOffset));
            CreateFloodlightTower(envRoot.transform, new Vector3(hl + towerOffset, 0f, -(hw + towerOffset)));
            CreateFloodlightTower(envRoot.transform, new Vector3(-(hl + towerOffset), 0f, hw + towerOffset));
            CreateFloodlightTower(envRoot.transform, new Vector3(-(hl + towerOffset), 0f, -(hw + towerOffset)));

            // Advertising boards along touchlines (north and south)
            CreateAdBoards(envRoot.transform, hw + 1.5f, true);  // North side
            CreateAdBoards(envRoot.transform, -(hw + 1.5f), true); // South side

            // Ground surround — dark area around pitch (beyond touchlines)
            CreateGroundSurround(envRoot.transform);
        }

        private void CreateFloodlightTower(Transform parent, Vector3 basePos)
        {
            float towerHeight = 25f;
            float poleRadius = 0.3f;

            var tower = new GameObject("FloodlightTower");
            tower.transform.SetParent(parent);
            tower.transform.position = basePos;

            // Main pole
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(tower.transform);
            pole.transform.localPosition = new Vector3(0f, towerHeight / 2f, 0f);
            pole.transform.localScale = new Vector3(poleRadius * 2f, towerHeight / 2f, poleRadius * 2f);
            pole.GetComponent<MeshRenderer>().sharedMaterial = CreateColorMaterial(new Color(0.5f, 0.5f, 0.55f));
            var poleCol = pole.GetComponent<Collider>();
            if (poleCol != null) Destroy(poleCol);

            // Light housing (box at the top)
            var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            housing.name = "LightHousing";
            housing.transform.SetParent(tower.transform);
            housing.transform.localPosition = new Vector3(0f, towerHeight, 0f);
            housing.transform.localScale = new Vector3(2f, 0.5f, 1.5f);

            // Bright emissive white to simulate lit panels
            var housingMat = CreateColorMaterial(new Color(1f, 0.98f, 0.9f));
            housingMat.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.85f) * 3f);
            housingMat.EnableKeyword("_EMISSION");
            housing.GetComponent<MeshRenderer>().sharedMaterial = housingMat;
            var housingCol = housing.GetComponent<Collider>();
            if (housingCol != null) Destroy(housingCol);

            // Look the housing toward pitch center
            housing.transform.LookAt(new Vector3(0f, towerHeight, 0f));
        }

        private void CreateAdBoards(Transform parent, float zPos, bool alongX)
        {
            float hl = _pitchConfig.HalfLength;
            float boardHeight = 1f;
            float boardWidth = 8f;
            int boardCount = 5;
            float totalSpan = boardWidth * boardCount;
            float startX = -totalSpan / 2f + boardWidth / 2f;

            var boardMat = CreateAdBoardMaterial();

            for (int i = 0; i < boardCount; i++)
            {
                var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = $"AdBoard_{(zPos > 0 ? "N" : "S")}_{i}";
                board.transform.SetParent(parent);

                float x = startX + i * boardWidth;
                board.transform.position = new Vector3(x, boardHeight / 2f, zPos);
                board.transform.localScale = new Vector3(boardWidth - 0.2f, boardHeight, 0.15f);
                board.GetComponent<MeshRenderer>().sharedMaterial = boardMat;

                var col = board.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
        }

        private static Material CreateAdBoardMaterial()
        {
            int w = 512, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var pixels = new Color[w * h];

            // Dark blue background with subtle branding-style stripe
            var bgColor = new Color(0.05f, 0.08f, 0.2f);
            var stripeColor = new Color(0.1f, 0.15f, 0.35f);
            var accentColor = new Color(0.9f, 0.7f, 0.1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = bgColor;
                    // Top and bottom accent stripes
                    if (y < 4 || y >= h - 4)
                        c = accentColor;
                    // Middle decorative stripe
                    else if (y >= h / 2 - 2 && y <= h / 2 + 2)
                        c = stripeColor;

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false);
            tex.wrapMode = TextureWrapMode.Repeat;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            mat.mainTexture = tex;
            mat.SetFloat("_Smoothness", 0.3f);
            // Slight emission so boards are visible in dark stadium
            mat.SetColor("_EmissionColor", new Color(0.03f, 0.05f, 0.12f));
            mat.EnableKeyword("_EMISSION");
            return mat;
        }

        private void CreateGroundSurround(Transform parent)
        {
            float hl = _pitchConfig.HalfLength;
            float hw = _pitchConfig.HalfWidth;
            float surroundSize = 20f; // Extra ground beyond pitch
            float totalLength = _pitchConfig.PitchLength + surroundSize * 2;
            float totalWidth = _pitchConfig.PitchWidth + surroundSize * 2;

            var surround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surround.name = "GroundSurround";
            surround.transform.SetParent(parent);
            surround.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            surround.transform.localScale = new Vector3(totalLength, 0.01f, totalWidth);

            // Dark gray concrete/track surface
            var mat = CreateColorMaterial(new Color(0.12f, 0.13f, 0.14f));
            surround.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var col = surround.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        // === POST-PROCESSING (B1.1) ===

        private void SetupPostProcessing()
        {
            var cfg = PostProcessingConfig.CreateNightStadium();

            // Enable post-processing on camera
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                var camData = mainCam.GetComponent<UniversalAdditionalCameraData>();
                if (camData == null)
                    camData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camData.renderPostProcessing = true;
            }

            // Create global Volume
            var volumeGo = new GameObject("PostProcessVolume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

            // Bloom — warm glow on highlights
            var bloom = volume.profile.Add<Bloom>();
            bloom.threshold.Override(cfg.BloomThreshold);
            bloom.intensity.Override(cfg.BloomIntensity);
            bloom.scatter.Override(cfg.BloomScatter);

            // Color Adjustments — warm night feel
            var colorAdj = volume.profile.Add<ColorAdjustments>();
            colorAdj.postExposure.Override(cfg.PostExposure);
            colorAdj.contrast.Override(cfg.Contrast);
            colorAdj.saturation.Override(cfg.Saturation);
            colorAdj.colorFilter.Override(new Color(cfg.ColorFilterR, cfg.ColorFilterG, cfg.ColorFilterB));

            // Vignette — dark edges for cinematic look
            var vignette = volume.profile.Add<Vignette>();
            vignette.intensity.Override(cfg.VignetteIntensity);
            vignette.smoothness.Override(cfg.VignetteSmoothness);
        }

        /// <summary>
        /// Replace legacy StandaloneInputModule with InputSystemUIInputModule.
        /// The Match scene's EventSystem may have StandaloneInputModule (wrong GUID
        /// in hand-crafted YAML), which calls UnityEngine.Input every frame and
        /// throws InvalidOperationException when Active Input Handling is set to
        /// "Input System Package (New)".
        /// </summary>
        private void FixEventSystem()
        {
            var eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null) return;

            var standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneModule != null)
            {
                Destroy(standaloneModule);
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        // === INPUT HANDLING ===

        private void HandleInput()
        {
            if (_activePlayer == null) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            var controller = _activePlayer.GetComponent<PlayerController>();
            if (controller == null || !controller.enabled) return;

            // Movement
            float h = 0f, v = 0f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) v += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) v -= 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) h -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) h += 1f;

            controller.SetMoveInput(new Vector2(h, v));
            controller.SetSprinting(keyboard.leftShiftKey.isPressed);

            // Pass
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                var kicker = _activePlayer.GetComponent<PlayerKicker>();
                if (kicker != null)
                {
                    kicker.Pass();
                    kicker.OnKickContact();
                }
            }

            // Shoot (F key or left mouse click)
            var mouse = Mouse.current;
            if (keyboard.fKey.wasPressedThisFrame ||
                (mouse != null && mouse.leftButton.wasPressedThisFrame))
            {
                var kicker = _activePlayer.GetComponent<PlayerKicker>();
                if (kicker != null)
                {
                    kicker.Shoot();
                    kicker.OnKickContact();
                }
            }

            // Tackle
            if (keyboard.eKey.wasPressedThisFrame)
            {
                var tackle = _activePlayer.GetComponent<TackleSystem>();
                if (tackle != null) tackle.AttemptTackle();
            }

            // Switch player
            if (keyboard.qKey.wasPressedThisFrame)
            {
                SwitchToNearestTeammate();
            }
        }

        private void SwitchToNearestTeammate()
        {
            if (_activePlayer == null || _ball == null) return;

            float nearestDist = float.MaxValue;
            GameObject nearest = null;

            foreach (var player in _homePlayers)
            {
                if (player == _activePlayer) continue;
                float dist = Vector3.Distance(player.transform.position, _ball.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = player;
                }
            }

            if (nearest != null)
            {
                // Deactivate old player
                var oldController = _activePlayer.GetComponent<PlayerController>();
                var oldAI = _activePlayer.GetComponent<AIController>();
                if (oldController != null) oldController.enabled = false;
                if (oldAI != null)
                {
                    oldAI.enabled = true;
                    oldAI.SetBallReference(_ball);
                }

                // Remove old indicator
                var oldIndicator = _activePlayer.transform.Find("ActiveIndicator");
                if (oldIndicator != null) Destroy(oldIndicator.gameObject);

                // Activate new player
                _activePlayer = nearest;
                var newController = nearest.GetComponent<PlayerController>();
                var newAI = nearest.GetComponent<AIController>();
                if (newController != null) newController.enabled = true;
                if (newAI != null) newAI.enabled = false;

                // Add indicator ring at feet
                CreatePlayerIndicatorRing(nearest);

                // Update camera target
                if (_cameraController != null)
                {
                    _cameraController.SetPlayerTarget(nearest.transform);
                }
            }
        }

        // === AI COORDINATION ===

        private void UpdateAICoordination()
        {
            CoordinateTeamAI(_homePlayers);
            CoordinateTeamAI(_awayPlayers);
        }

        private void CoordinateTeamAI(List<GameObject> team)
        {
            float nearestDist = float.MaxValue;
            AIController nearestAI = null;

            foreach (var player in team)
            {
                var ai = player.GetComponent<AIController>();
                if (ai == null || !ai.enabled) continue;

                float dist = Vector3.Distance(player.transform.position, _ball.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestAI = ai;
                }
            }

            foreach (var player in team)
            {
                var ai = player.GetComponent<AIController>();
                if (ai == null || !ai.enabled) continue;
                ai.SetAsNearestToBall(ai == nearestAI);
            }
        }

        // === MATCH FLOW ===

        private void HandleGoalScored(TeamIdentifier scoringTeam)
        {
            _matchScore.AddGoal(scoringTeam);
            _kickoffLogic.OnGoalScored(scoringTeam);
            UpdateHUD();

            Debug.Log($"[MatchOrchestrator] GOAL! {scoringTeam} scores! " +
                      $"Score: {_matchScore.ScoreA} - {_matchScore.ScoreB}");

            // Reset for next kickoff after delay
            Invoke(nameof(ResetForKickoff), 2f);
        }

        private void HandlePeriodChanged(MatchPeriod newPeriod)
        {
            switch (newPeriod)
            {
                case MatchPeriod.HalfTime:
                    _stateMachine.TransitionTo(MatchState.HalfTime);
                    if (_periodText != null) _periodText.text = "HT";
                    Debug.Log("[MatchOrchestrator] Half Time!");
                    // Auto-start second half after 3 seconds
                    Invoke(nameof(StartSecondHalf), 3f);
                    break;

                case MatchPeriod.FullTime:
                    _stateMachine.TransitionTo(MatchState.FullTime);
                    if (_periodText != null) _periodText.text = "FT";
                    Debug.Log($"[MatchOrchestrator] Full Time! Final score: " +
                              $"{_matchScore.ScoreA} - {_matchScore.ScoreB}");
                    break;
            }
        }

        private void StartSecondHalf()
        {
            _matchTimer.StartSecondHalf();
            _stateMachine.TransitionTo(MatchState.SecondHalf);
            if (_periodText != null) _periodText.text = "2ND";
            ResetForKickoff();
        }

        private void HandleTimeUpdated(float remainingSeconds)
        {
            if (_timerText == null) return;
            int minutes = Mathf.FloorToInt(remainingSeconds / 60f);
            int seconds = Mathf.FloorToInt(remainingSeconds % 60f);
            _timerText.text = $"{minutes:00}:{seconds:00}";
        }

        private void ResetForKickoff()
        {
            // Reset ball to center
            if (_ballController != null)
            {
                _ballController.ResetToPosition(new Vector3(0f, 0.5f, 0f));
            }

            // Reset players to formation positions
            ResetTeamPositions(_homePlayers, isHome: true);
            ResetTeamPositions(_awayPlayers, isHome: false);
        }

        private void ResetTeamPositions(List<GameObject> team, bool isHome)
        {
            var positions = _formation.GetWorldPositions(isHome, _pitchConfig.PitchLength);
            for (int i = 0; i < team.Count && i < positions.Length; i++)
            {
                var rb = team[i].GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                team[i].transform.position = new Vector3(positions[i].x, 1f, positions[i].z);
            }
        }

        private void UpdateHUD()
        {
            if (_scoreText != null)
            {
                _scoreText.text = $"{_matchScore.ScoreA}  -  {_matchScore.ScoreB}";
            }
        }

        // === PLAYER NAME LABEL (B4) ===

        private static void CreatePlayerLabel(GameObject player, int number, Color teamColor)
        {
            var labelRoot = new GameObject("NameLabel");
            labelRoot.transform.SetParent(player.transform);
            labelRoot.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            // World-space canvas for the label
            var canvas = labelRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rect = labelRoot.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120f, 40f);
            rect.localScale = Vector3.one * 0.005f; // 120*0.005 = 0.6m wide

            // Background panel — team color with rounded feel
            var bgGo = new GameObject("LabelBG");
            bgGo.transform.SetParent(labelRoot.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var bgImg = bgGo.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(teamColor.r * 0.7f, teamColor.g * 0.7f, teamColor.b * 0.7f, 0.85f);

            // Number text
            var textGo = new GameObject("NumberText");
            textGo.transform.SetParent(labelRoot.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = number.ToString();
            tmp.fontSize = 30;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;

            // Billboard component to always face camera
            labelRoot.AddComponent<BillboardLabel>();
        }

        // === GOAL NET MESH (B1.5) ===

        /// <summary>
        /// Creates a procedural grid mesh that looks like real goal netting.
        /// </summary>
        private void CreateNetPanel(Transform parent, string name,
            Vector3 localPos, Quaternion localRot, float width, float height,
            int cols, int rows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = CreateNetMaterial();

            mf.mesh = GenerateNetMesh(width, height, cols, rows);
        }

        /// <summary>
        /// Generates a grid-line mesh (thin quads forming a net pattern).
        /// </summary>
        private static Mesh GenerateNetMesh(float width, float height, int cols, int rows)
        {
            float lineThickness = 0.02f;
            float halfW = width / 2f;
            float halfH = height / 2f;

            var verts = new List<Vector3>();
            var tris = new List<int>();

            // Vertical lines
            for (int c = 0; c <= cols; c++)
            {
                float x = -halfW + (width / cols) * c;
                AddQuad(verts, tris,
                    new Vector3(x - lineThickness, -halfH, 0),
                    new Vector3(x + lineThickness, -halfH, 0),
                    new Vector3(x + lineThickness, halfH, 0),
                    new Vector3(x - lineThickness, halfH, 0));
            }

            // Horizontal lines
            for (int r = 0; r <= rows; r++)
            {
                float y = -halfH + (height / rows) * r;
                AddQuad(verts, tris,
                    new Vector3(-halfW, y - lineThickness, 0),
                    new Vector3(halfW, y - lineThickness, 0),
                    new Vector3(halfW, y + lineThickness, 0),
                    new Vector3(-halfW, y + lineThickness, 0));
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(List<Vector3> verts, List<int> tris,
            Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl)
        {
            int i = verts.Count;
            verts.Add(bl); verts.Add(br); verts.Add(tr); verts.Add(tl);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
            tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
        }

        private static Material CreateNetMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            mat.color = new Color(0.95f, 0.95f, 0.95f);
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Metallic", 0f);
            // Double-sided rendering
            mat.SetFloat("_Cull", 0f); // 0 = Off (both sides)
            return mat;
        }

        // === SOCCER BALL TEXTURE (B1.6) ===

        private static Material CreateSoccerBallMaterial()
        {
            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
            var pixels = new Color[size * size];
            var white = new Color(0.95f, 0.95f, 0.95f);
            var black = new Color(0.1f, 0.1f, 0.1f);

            // Create a pentagon pattern approximation
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    // Create hexagonal-ish pattern with pentagons
                    float cellSize = 0.2f;
                    float cx = (u % cellSize) / cellSize - 0.5f;
                    float cy = (v % cellSize) / cellSize - 0.5f;
                    float dist = Mathf.Sqrt(cx * cx + cy * cy);

                    int cellX = Mathf.FloorToInt(u / cellSize);
                    int cellY = Mathf.FloorToInt(v / cellSize);
                    bool isDark = ((cellX + cellY) % 3 == 0) && dist < 0.35f;

                    // Seam lines between cells
                    bool isSeam = Mathf.Abs(cx) > 0.42f || Mathf.Abs(cy) > 0.42f;

                    if (isDark)
                        pixels[y * size + x] = black;
                    else if (isSeam)
                        pixels[y * size + x] = new Color(0.7f, 0.7f, 0.7f);
                    else
                        pixels[y * size + x] = white;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Repeat;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            mat.mainTexture = tex;
            mat.SetFloat("_Smoothness", 0.6f); // Ball is somewhat glossy
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        // === PLAYER INDICATOR RING (B1.7) ===

        private static void CreatePlayerIndicatorRing(GameObject player)
        {
            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.name = "ActiveIndicator";
            indicator.transform.SetParent(player.transform);
            indicator.transform.localPosition = new Vector3(0f, -0.95f, 0f); // At feet
            indicator.transform.localScale = new Vector3(2.5f, 0.02f, 2.5f); // Flat ring

            // Yellow emissive ring
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            mat.color = new Color(1f, 0.9f, 0f);
            mat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0f) * 2f);
            mat.EnableKeyword("_EMISSION");
            indicator.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var col = indicator.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        // === MATERIAL HELPER ===

        private static Material CreateColorMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");

            var mat = new Material(shader);
            mat.color = color;
            if (color.a < 1f && shader.name.Contains("Lit"))
            {
                // Enable transparency for semi-transparent materials (e.g., goal nets)
                mat.SetFloat("_Surface", 1f); // 1 = Transparent
                mat.SetFloat("_Blend", 0f);   // 0 = Alpha
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = 3000;
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            return mat;
        }
    }
}
