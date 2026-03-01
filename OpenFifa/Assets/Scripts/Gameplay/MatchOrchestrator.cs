using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
            SetupLighting();
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
                    field.SetValue(_ballOwnership, LayerMask.GetMask("Default"));
                }
            }

            // Start the match
            ResetForKickoff();
            _matchTimer.StartMatch();
            _stateMachine.TransitionTo(MatchState.FirstHalf);
            UpdateHUD();
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

            // Net back plane (semi-transparent)
            var net = GameObject.CreatePrimitive(PrimitiveType.Quad);
            net.name = "Net";
            net.transform.SetParent(root.transform);
            float netDepth = _pitchConfig.GoalAreaDepth;
            float netOffsetX = position.x > 0 ? netDepth / 2f : -netDepth / 2f;
            net.transform.localPosition = new Vector3(netOffsetX, goalHeight / 2f, 0f);
            net.transform.localScale = new Vector3(goalWidth, goalHeight, 1f);
            net.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            var netRenderer = net.GetComponent<MeshRenderer>();
            var netMat = CreateColorMaterial(new Color(1f, 1f, 1f, 0.3f));
            netRenderer.sharedMaterial = netMat;

            // Remove net collider (visual only)
            var netCollider = net.GetComponent<Collider>();
            if (netCollider != null) Destroy(netCollider);
        }

        // === BALL ===

        private void CreateBall()
        {
            var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "Ball";
            ballGo.tag = "Ball";
            ballGo.transform.position = new Vector3(0f, 0.5f, 0f);
            ballGo.transform.localScale = Vector3.one * 0.22f;

            ballGo.GetComponent<MeshRenderer>().sharedMaterial = CreateColorMaterial(Color.white);

            _ballController = ballGo.AddComponent<BallController>();
            _ballOwnership = ballGo.AddComponent<BallOwnership>();
            _ball = ballGo.transform;
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

            // Active player indicator
            if (isHumanControlled)
            {
                var indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                indicator.name = "ActiveIndicator";
                indicator.transform.SetParent(player.transform);
                indicator.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                indicator.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                indicator.GetComponent<MeshRenderer>().sharedMaterial =
                    CreateColorMaterial(Color.yellow);
                var indicatorCollider = indicator.GetComponent<Collider>();
                if (indicatorCollider != null) Destroy(indicatorCollider);
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

        // === HUD ===

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

            // Score text — top center
            _scoreText = CreateHUDText(canvas.transform, "ScoreText",
                new Vector2(0.35f, 0.92f), new Vector2(0.65f, 1f),
                "HOME  0 - 0  AWAY", 42, TextAlignmentOptions.Center);

            // Timer text — below score
            _timerText = CreateHUDText(canvas.transform, "TimerText",
                new Vector2(0.42f, 0.86f), new Vector2(0.58f, 0.92f),
                "45:00", 32, TextAlignmentOptions.Center);

            // Period text — below timer
            _periodText = CreateHUDText(canvas.transform, "PeriodText",
                new Vector2(0.42f, 0.82f), new Vector2(0.58f, 0.86f),
                "1ST HALF", 24, TextAlignmentOptions.Center);

            // Controls hint — bottom center
            CreateHUDText(canvas.transform, "ControlsHint",
                new Vector2(0.2f, 0f), new Vector2(0.8f, 0.05f),
                "WASD: Move | Shift: Sprint | Space: Pass | F/Click: Shoot | E: Tackle | Q: Switch",
                18, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.6f));
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

        // === LIGHTING ===

        private void SetupLighting()
        {
            // Set ambient to a nice outdoor color
            RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.65f);
        }

        // === INPUT HANDLING ===

        private void HandleInput()
        {
            if (_activePlayer == null) return;

            var controller = _activePlayer.GetComponent<PlayerController>();
            if (controller == null || !controller.enabled) return;

            // Movement
            float h = 0f, v = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;

            controller.SetMoveInput(new Vector2(h, v));
            controller.SetSprinting(Input.GetKey(KeyCode.LeftShift));

            // Actions
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var kicker = _activePlayer.GetComponent<PlayerKicker>();
                if (kicker != null)
                {
                    kicker.Pass();
                    kicker.OnKickContact(); // Immediate contact (no animation)
                }
            }

            // Shoot (right mouse or F key — D is used for movement right)
            if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(0))
            {
                var kicker = _activePlayer.GetComponent<PlayerKicker>();
                if (kicker != null)
                {
                    kicker.Shoot();
                    kicker.OnKickContact();
                }
            }

            // Tackle
            if (Input.GetKeyDown(KeyCode.E))
            {
                var tackle = _activePlayer.GetComponent<TackleSystem>();
                if (tackle != null) tackle.AttemptTackle();
            }

            // Switch player
            if (Input.GetKeyDown(KeyCode.Q))
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

                // Add indicator
                var indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                indicator.name = "ActiveIndicator";
                indicator.transform.SetParent(nearest.transform);
                indicator.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                indicator.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                indicator.GetComponent<MeshRenderer>().sharedMaterial =
                    CreateColorMaterial(Color.yellow);
                var col = indicator.GetComponent<Collider>();
                if (col != null) Destroy(col);

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
                    if (_periodText != null) _periodText.text = "HALF TIME";
                    Debug.Log("[MatchOrchestrator] Half Time!");
                    // Auto-start second half after 3 seconds
                    Invoke(nameof(StartSecondHalf), 3f);
                    break;

                case MatchPeriod.FullTime:
                    _stateMachine.TransitionTo(MatchState.FullTime);
                    if (_periodText != null) _periodText.text = "FULL TIME";
                    Debug.Log($"[MatchOrchestrator] Full Time! Final score: " +
                              $"{_matchScore.ScoreA} - {_matchScore.ScoreB}");
                    break;
            }
        }

        private void StartSecondHalf()
        {
            _matchTimer.StartSecondHalf();
            _stateMachine.TransitionTo(MatchState.SecondHalf);
            if (_periodText != null) _periodText.text = "2ND HALF";
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
                _scoreText.text = $"HOME  {_matchScore.ScoreA} - {_matchScore.ScoreB}  AWAY";
            }
        }

        // === MATERIAL HELPER ===

        private static Material CreateColorMaterial(Color color)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");

            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }
    }
}
