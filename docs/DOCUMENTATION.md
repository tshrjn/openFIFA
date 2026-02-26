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

## 2026-02-27 Session — US-001: Unity project scaffold + iOS build pipeline + test framework

**Status**: Completed
**Changes**:
- Created full Unity project structure under OpenFifa/
- OpenFifa/Assets/Scripts/Core/OpenFifa.Core.asmdef (noEngineReferences: true)
- OpenFifa/Assets/Scripts/Gameplay/OpenFifa.Gameplay.asmdef
- OpenFifa/Assets/Scripts/AI/OpenFifa.AI.asmdef
- OpenFifa/Assets/Scripts/UI/OpenFifa.UI.asmdef
- OpenFifa/Assets/Scripts/Audio/OpenFifa.Audio.asmdef
- OpenFifa/Assets/Editor/OpenFifa.Editor.asmdef
- OpenFifa/Assets/Editor/BuildScript.cs (iOS + macOS build methods)
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
- Created scripts/verify-ios-build.sh
- Created .github/workflows/openfifa-ci.yml

**Decisions**:
- Engine: Unity 6 LTS + C# + URP (proven iOS pipeline, large ecosystem)
- Orchestration: Claude Code direct with Codex long-horizon task pattern (not ralph-tui)
- Art style: Stylized low-poly placeholders first, real assets via Phase 6 stories
- Testing: 10-layer automated strategy, TDD workflow, NUnit + GameCI
- Architecture: Pure C# Core logic separated from MonoBehaviour for EditMode testability
- All tunable values in ScriptableObjects, no hardcoded gameplay parameters

**Known Issues**: None (empty project)

**Next**: US-001 — Unity project scaffold with iOS build pipeline and test framework
