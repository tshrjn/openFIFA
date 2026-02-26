# OpenFifa Development Log

> Real-time record of decisions, progress, and issues. **Read this first** at the start of every session.

---

## How to Use This Log

- **Starting a session?** Read the most recent entry to understand current state.
- **Finishing a session?** Add a new entry at the TOP of the log (newest first).
- **Format**: Use the template below for each entry.

### Entry Template

```markdown
## [YYYY-MM-DD] Session — US-XXX: Story Title

**Status**: Completed / In Progress / Blocked
**Changes**: List of files created or modified
**Decisions**: Key decisions made and rationale
**Known Issues**: Any bugs, flaky tests, or concerns discovered
**Next**: What the next session should pick up
```

---

## Log

## 2026-02-27 Session — US-009: Team formation system with 2-1-2 layout

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/PositionRole.cs — Position role enum
- OpenFifa/Assets/Scripts/Core/FormationSlotData.cs — Slot data (role + offset)
- OpenFifa/Assets/Scripts/Core/FormationLayoutData.cs — Formation layout with mirroring
- OpenFifa/Assets/Scripts/Gameplay/FormationData.cs — ScriptableObject wrapper
- OpenFifa/Assets/Scripts/Gameplay/FormationManager.cs — Runtime formation manager
- OpenFifa/Assets/Tests/Editor/US009_FormationTests.cs — 12 EditMode tests

**Decisions**:
- Default 2-1-2: GK(0,0,-12), LB(-6,0,-6), RB(6,0,-6), CM(0,0,0), LW(-6,0,8), RW(6,0,8)
- Home team defends negative X, away positive X
- Mirroring negates both X and Z offsets for away team
- FormationPosition struct in Core for lightweight position data
- FormationManager supports runtime formation swapping

**Known Issues**: None

**Next**: US-010 — AI player finite state machine

## 2026-02-27 Session — US-008: Basic HUD displaying score and timer

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/HUDFormatter.cs — Pure C# score/timer formatting
- OpenFifa/Assets/Scripts/UI/HUDController.cs — HUD MonoBehaviour with TMP_Text
- OpenFifa/Assets/Tests/Editor/US008_HUDFormatTests.cs — 9 EditMode tests
- OpenFifa/Assets/Tests/Runtime/US008_HUDDisplayTests.cs — 6 PlayMode tests
- Updated OpenFifa.Tests.Runtime.asmdef to reference Unity.TextMeshPro and Unity.InputSystem

**Decisions**:
- HUDFormatter is pure C# for EditMode testing of format logic
- HUDController subscribes to events AND polls in Update for smooth display
- CanvasScaler: 1920x1080 reference, matchWidthOrHeight=0.5
- Timer format: MM:SS using TimeSpan
- Score format: "TeamA X - Y TeamB" with configurable team names

**Known Issues**: None

**Next**: US-009 — Match flow controller

## 2026-02-27 Session — US-007: Match timer + score tracking as pure C# logic

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/MatchPeriod.cs — Match period enum
- OpenFifa/Assets/Scripts/Core/MatchTimer.cs — Pure C# match timer
- OpenFifa/Assets/Scripts/Core/MatchScore.cs — Pure C# score tracker
- OpenFifa/Assets/Tests/Editor/US007_MatchTimerTests.cs — 17 EditMode tests
- OpenFifa/Assets/Tests/Editor/US007_MatchScoreTests.cs — 8 EditMode tests

**Decisions**:
- All pure C# in Core — zero MonoBehaviour dependency
- MatchPeriod enum: PreKickoff, FirstHalf, HalfTime, SecondHalf, FullTime
- Timer only ticks during active play (FirstHalf, SecondHalf)
- Events: OnPeriodChanged (period transitions), OnTimeUpdated (every tick), OnScoreChanged
- MatchScore uses simple int fields, not Dictionary (lighter weight)
- GetScoreDisplay() returns "X - Y" format string
- Remaining time clamped to 0 (never negative)

**Known Issues**: None

**Next**: US-008 — Basic HUD displaying score and timer

## 2026-02-27 Session — US-006: Broadcast camera with Cinemachine follow

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/CameraConfigData.cs — Pure C# camera config data
- OpenFifa/Assets/Scripts/Gameplay/BroadcastCameraConfig.cs — ScriptableObject wrapper
- OpenFifa/Assets/Scripts/Gameplay/BroadcastCameraController.cs — Camera follow controller
- OpenFifa/Assets/Tests/Editor/US006_CameraConfigTests.cs — 7 EditMode tests
- OpenFifa/Assets/Tests/Runtime/US006_BroadcastCameraTests.cs — 5 PlayMode tests

**Decisions**:
- BroadcastCameraController is standalone, works with or without Cinemachine
- Weighted tracking: ball (1.0) + active player (0.5) for focus balance
- SmoothDamp for smooth following, configurable damping time
- MinHeight enforced to prevent camera clipping through pitch
- Elevation angle calculated from distance and angle (35 degrees default)
- Camera looks at weighted midpoint of tracked objects

**Known Issues**: None

**Next**: US-007 — Match timer + score tracking as pure C# logic

## 2026-02-27 Session — US-005: Goal detection system with event broadcasting

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/TeamIdentifier.cs — TeamA/TeamB enum
- OpenFifa/Assets/Scripts/Core/GoalEventData.cs — Goal event payload data
- OpenFifa/Assets/Scripts/Gameplay/GoalDetector.cs — Goal detection MonoBehaviour
- OpenFifa/Assets/Tests/Editor/US005_GoalDetectionTests.cs — 6 EditMode tests
- OpenFifa/Assets/Tests/Runtime/US005_GoalDetectionPlayModeTests.cs — 7 PlayMode tests

**Decisions**:
- GoalDetector uses static event Action<TeamIdentifier> for global subscription
- Also has instance event for per-detector subscription
- Trigger volumes placed behind goal line (not on it) to require full crossing
- Ball reset after goal with configurable delay (default 2s)
- _goalDetected flag prevents duplicate goal events from same ball entry
- GoalEventData contains both ScoringTeam and DefendingTeam for convenience

**Known Issues**: None

**Next**: US-006 — Broadcast camera with Cinemachine follow

## 2026-02-27 Session — US-004: Single player controller with keyboard movement and sprint

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/PlayerStatsData.cs — Pure C# player stats data
- OpenFifa/Assets/Scripts/Gameplay/PlayerStatsConfig.cs — ScriptableObject wrapper
- OpenFifa/Assets/Scripts/Gameplay/PlayerController.cs — Player MonoBehaviour with input handling
- OpenFifa/Assets/Scripts/Gameplay/OpenFifa.Gameplay.asmdef — Added Unity.InputSystem reference
- OpenFifa/Assets/Tests/Editor/US004_PlayerConfigTests.cs — 7 EditMode tests
- OpenFifa/Assets/Tests/Runtime/US004_PlayerMovementTests.cs — 10 PlayMode tests

**Decisions**:
- PlayerController uses velocity-based movement (not MovePosition) for physics interaction
- Movement input normalized to prevent diagonal speed boost
- SetMoveInput/SetSprinting methods allow programmatic control (tests + AI)
- Input System callbacks (OnMove/OnSprint) for PlayerInput component integration
- Rigidbody constraints freeze all rotation axes to prevent tipping
- Acceleration/deceleration model for smooth starts and stops
- Base speed 7 m/s, sprint 10.5 m/s (1.5x multiplier), acceleration 5 m/s^2

**Known Issues**: None

**Next**: US-005 — Goal detection system with event broadcasting

## 2026-02-27 Session — US-003: Ball physics with realistic mass, bounce, friction, and rolling

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/BallPhysicsData.cs — Pure C# ball physics config data
- OpenFifa/Assets/Scripts/Core/BallState.cs — BallState enum (Free, Possessed, InFlight)
- OpenFifa/Assets/Scripts/Gameplay/BallPhysicsConfig.cs — ScriptableObject wrapper
- OpenFifa/Assets/Scripts/Gameplay/BallController.cs — Ball MonoBehaviour with state management
- OpenFifa/Assets/Tests/Editor/US003_BallConfigTests.cs — 9 EditMode tests
- OpenFifa/Assets/Tests/Runtime/US003_BallPhysicsTests.cs — 10 PlayMode tests

**Decisions**:
- BallController uses [RequireComponent] for Rigidbody and SphereCollider
- PhysicMaterial created at runtime with bounceCombine=Average, frictionCombine=Average
- Ball radius 0.11m (standard soccer ball)
- BallState transitions: Free -> Possessed, Free -> InFlight, InFlight -> Free (on ground contact)
- Events use C# event Action<BallState, BallState> pattern for state changes
- Rigidbody interpolation=Interpolate, collisionDetection=ContinuousDynamic

**Known Issues**: None

**Next**: US-004 — Single player controller with keyboard movement and sprint

## 2026-02-27 Session — US-002: Soccer pitch with correct 5v5 proportions + boundary colliders

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/PitchConfigData.cs — Pure C# pitch config data class
- OpenFifa/Assets/Scripts/Gameplay/PitchConfig.cs — ScriptableObject wrapper
- OpenFifa/Assets/Scripts/Gameplay/PitchBuilder.cs — Runtime pitch builder MonoBehaviour
- OpenFifa/Assets/Tests/Editor/US002_PitchConfigTests.cs — 12 EditMode tests
- OpenFifa/Assets/Tests/Runtime/US002_PitchSetupTests.cs — 9 PlayMode tests

**Decisions**:
- PitchConfigData is pure C# (no Unity deps) for EditMode testability
- PitchConfig ScriptableObject has ToData() method to bridge to pure C#
- Boundary walls split at goal ends to create goal openings
- Goal nets represented by back wall + side walls behind each goal
- Center circle uses LineRenderer, goal areas use LineRenderer markings
- Pitch surface is a scaled Cube (not Plane) for consistent bounds calculation
- Ball boundary tests use 30 m/s velocity to stress-test colliders
- Custom layers: Pitch (8), Boundary (9) pre-configured in TagManager

**Known Issues**: None

**Next**: US-003 — Ball physics with realistic mass, bounce, friction, and rolling

## 2026-02-27 Session — US-001: Unity project scaffold + macOS/iPad build pipeline + test framework

**Status**: Completed
**Changes**:
- Created full Unity project structure under OpenFifa/
- OpenFifa/Assets/Scripts/Core/OpenFifa.Core.asmdef (noEngineReferences: true)
- OpenFifa/Assets/Scripts/Gameplay/OpenFifa.Gameplay.asmdef
- OpenFifa/Assets/Scripts/AI/OpenFifa.AI.asmdef
- OpenFifa/Assets/Scripts/UI/OpenFifa.UI.asmdef
- OpenFifa/Assets/Scripts/Audio/OpenFifa.Audio.asmdef
- OpenFifa/Assets/Editor/OpenFifa.Editor.asmdef
- OpenFifa/Assets/Editor/BuildScript.cs (macOS + iPad build methods)
- OpenFifa/Assets/Tests/Editor/OpenFifa.Tests.Editor.asmdef (EditMode test runner)
- OpenFifa/Assets/Tests/Runtime/OpenFifa.Tests.Runtime.asmdef (PlayMode test runner)
- OpenFifa/Assets/Tests/Editor/US001_ProjectScaffoldTests.cs (15 EditMode tests)
- OpenFifa/Assets/Tests/Runtime/US001_ProjectScaffoldPlayModeTests.cs (3 PlayMode tests)
- OpenFifa/Packages/manifest.json (URP, Cinemachine, InputSystem, TMP, TestFramework)
- OpenFifa/ProjectSettings/ (ProjectSettings, QualitySettings, GraphicsSettings, InputManager, TagManager, PhysicsManager, EditorBuildSettings, TimeManager, AudioManager)
- OpenFifa/.vsconfig

**Decisions**:
- Core asmdef has noEngineReferences: true to enforce pure C# separation
- Gameplay, AI, UI, Audio asmdefs reference Core for dependency chain
- Test asmdefs use overrideReferences with nunit.framework.dll
- TagManager pre-configured with Ball, TeamA, TeamB, GoalTrigger tags and custom layers
- PhysicsManager uses default gravity -9.81, autoSyncTransforms disabled for determinism
- New Input System set as active input handler (m_ActiveInputHandler: 2)
- URP assigned in GraphicsSettings via customRenderPipeline reference

**Known Issues**: None

**Next**: US-002 — Soccer pitch with correct 5v5 proportions + boundary colliders

## 2026-02-27 Session — Project Initialization

**Status**: Completed
**Changes**:
- Created project scaffolding: CLAUDE.md, README.md, .gitignore
- Created docs/: PROMPT.md, PLAN.md, IMPLEMENT.md, TESTING.md, ASSETS.md, DOCUMENTATION.md
- Created prd.json with 50 user stories across 6 phases
- Created scripts/verify-build.sh
- Created .github/workflows/openfifa-ci.yml

**Decisions**:
- Engine: Unity 6 LTS + C# + URP (proven macOS/iPad pipeline, large ecosystem)
- Orchestration: Claude Code direct with Codex long-horizon task pattern (not ralph-tui)
- Art style: Stylized low-poly placeholders first, real assets via Phase 6 stories
- Testing: 10-layer automated strategy, TDD workflow, NUnit + GameCI
- Architecture: Pure C# Core logic separated from MonoBehaviour for EditMode testability
- All tunable values in ScriptableObjects, no hardcoded gameplay parameters

**Known Issues**: None (empty project)

**Next**: US-001 — Unity project scaffold with macOS/iPad build pipeline and test framework
