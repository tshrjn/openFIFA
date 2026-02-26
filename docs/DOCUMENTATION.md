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

## 2026-02-27 Session — US-027: Replay system for last 5 seconds on goal

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/ReplayBuffer.cs — Pre-allocated ring buffer + ReplayLogic
- OpenFifa/Assets/Scripts/Gameplay/ReplaySystem.cs — Replay MonoBehaviour with coroutine playback
- OpenFifa/Assets/Tests/Editor/US027_ReplayTests.cs — 11 EditMode tests

**Decisions**:
- ReplayBuffer: pre-allocated ring buffer with flat float arrays (no GC during recording)
- 150 frames capacity (5s at 30fps)
- ReplayLogic: state machine for recording/playback with 0.5x speed
- Position/rotation stored as flat float arrays (3 floats pos, 4 floats rot per object)
- Original transforms saved before replay and restored after
- Subscribes to GoalDetector.OnGoalScored for automatic replay trigger

**Known Issues**: None

**Next**: US-028 — Main menu, US-030 — Full match HUD

## 2026-02-27 Session — US-026: Player run dust particle effects

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/RunDustLogic.cs — Pure C# dust emission logic
- OpenFifa/Assets/Scripts/Gameplay/RunDustEffect.cs — Dust particle MonoBehaviour
- OpenFifa/Assets/Tests/Editor/US026_RunDustTests.cs — 8 EditMode tests

**Decisions**:
- Walk threshold 2 m/s, max 20 particles per player
- Emission rate: 2-15 particles/sec scaling with speed
- Hemisphere shape rotated -90 for ground-aligned emission
- Brownish-tan color with alpha fade-out over lifetime
- Positioned at player feet (child GameObject, Y ~0.05)

**Known Issues**: None

**Next**: US-027 — Replay system, US-028 — Main menu

## 2026-02-27 Session — US-025: Dynamic crowd reaction audio

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/CrowdReactionLogic.cs — Pure C# crowd intensity and near-miss detection
- OpenFifa/Assets/Scripts/Audio/CrowdReactionSystem.cs — Dynamic crowd MonoBehaviour with AudioMixer
- OpenFifa/Assets/Tests/Editor/US025_CrowdReactionTests.cs — 10 EditMode tests

**Decisions**:
- Intensity scales linearly from 0.3 (center) to 1.0 (goal line) based on ball X proximity
- Volume maps from -20dB (base) to 0dB (max) via AudioMixer exposed parameter
- Smooth transitions via MoveTowards at lerpSpeed 3
- Near-miss detection: ball near goal line (>23m X), outside goal width, at speed >10 m/s
- CrowdReactionSystem updates AudioMixer "CrowdVolume" parameter each frame

**Known Issues**: None

**Next**: US-026 — Player dust particles, US-027 — Replay system

## 2026-02-27 Session — US-024: Camera shake on goal via Cinemachine Impulse

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/CameraShakeLogic.cs — Pure C# shake with decay and pseudo-random offsets
- OpenFifa/Assets/Scripts/Gameplay/GoalCameraShake.cs — Camera shake MonoBehaviour
- OpenFifa/Assets/Tests/Editor/US024_CameraShakeTests.cs — 10 EditMode tests

**Decisions**:
- CameraShakeLogic pure C# with linear decay, configurable duration (0.7s default)
- Pseudo-random offset via LCG hash for deterministic shake pattern
- GoalCameraShake subscribes to GoalDetector.OnGoalScored
- Offsets applied in LateUpdate to camera localPosition
- Intensity, duration, decay all configurable via serialized fields

**Known Issues**: None

**Next**: US-025 — Dynamic crowd reaction, US-026 — Player dust

## 2026-02-27 Session — US-023: Sound effects for whistle, kick, crowd, and goal

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/SoundEventMapper.cs — Pure C# match state to sound event mapping
- OpenFifa/Assets/Scripts/Audio/SoundManager.cs — Singleton sound manager MonoBehaviour
- OpenFifa/Assets/Tests/Editor/US023_SoundTests.cs — 8 EditMode tests

**Decisions**:
- SoundEventMapper pure C# maps state transitions to SoundEventType enum
- Whistle on: kickoff, halftime, fulltime, second half start
- GoalCheer on: goal celebration state
- SoundManager singleton with DontDestroyOnLoad
- Separate AudioSources for SFX (PlayOneShot) and ambient (loop)
- Placeholder clips generated via AudioClip.Create with sine waves and noise
- Subscribes to PlayerKicker.OnKickContactEvent for kick sounds

**Known Issues**: None

**Next**: US-024 — Camera shake, US-025 — Dynamic crowd

## 2026-02-27 Session — US-022: Ball trail particle effect at high velocity

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/BallTrailLogic.cs — Pure C# trail emission logic
- OpenFifa/Assets/Scripts/Gameplay/BallTrailEffect.cs — Ball trail particle MonoBehaviour
- OpenFifa/Assets/Tests/Editor/US022_BallTrailTests.cs — 10 EditMode tests

**Decisions**:
- Velocity threshold 10 m/s, max 50 particles
- Alpha and emission rate scale linearly with speed above threshold
- Emission rate: 5-30 particles/sec based on speed ratio
- ParticleSystem configured in code: world space, sphere shape, fade-out over lifetime
- Color lerp between transparent and warm gold based on speed

**Known Issues**: None

**Next**: US-023 — Sound effects, US-024 — Camera shake

## 2026-02-27 Session — US-021: Goal celebration sequence with slow-motion and camera zoom

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/CelebrationLogic.cs — Pure C# celebration state management
- OpenFifa/Assets/Scripts/Gameplay/CelebrationSequence.cs — Celebration MonoBehaviour with coroutine
- OpenFifa/Assets/Tests/Editor/US021_CelebrationTests.cs — 10 EditMode tests

**Decisions**:
- CelebrationLogic pure C# with IsPlaying, TargetTimeScale, and kickoff trigger
- TryStartCelebration prevents stacking (returns false if already playing)
- ShouldTriggerKickoff is consumed on read (one-shot flag)
- Slow motion at 0.3x for 2 real-time seconds using WaitForSecondsRealtime
- CelebrationSequence subscribes to GoalDetector.OnGoalScored static event
- Time.timeScale restored to 1.0 after celebration completes

**Known Issues**: None

**Next**: US-022 — Ball trail, US-023 — Sound effects

## 2026-02-27 Session — US-020: Ball kick animation synchronized with force application

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/KickConfigData.cs — Pure C# kick config, KickType enum, KickLogic, KickResult
- OpenFifa/Assets/Scripts/Gameplay/PlayerKicker.cs — Kick execution MonoBehaviour with animation sync
- OpenFifa/Assets/Tests/Editor/US020_KickAnimationTests.cs — 10 EditMode tests

**Decisions**:
- KickLogic stores pending kick data, executed at contact frame via ExecuteKick()
- Contact frame time 80ms (under 100ms requirement for responsive feel)
- Pass force 8, Shoot force 15 (configurable via serialized fields)
- OnKickContact() callable by AnimationEvent at contact frame
- Ball ownership released before force applied
- Direction normalized from player's transform.forward
- Pure C# sqrt implementation for direction normalization (no Mathf dependency in Core)

**Known Issues**: None

**Next**: US-021 — Goal celebration, US-022 — Ball trail

## 2026-02-27 Session — US-019: Player animation state machine with placeholder animations

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/AnimationStateLogic.cs — Pure C# animation state machine logic with enums
- OpenFifa/Assets/Scripts/Gameplay/PlayerAnimator.cs — Animator bridge MonoBehaviour
- OpenFifa/Assets/Tests/Editor/US019_PlayerAnimationTests.cs — 12 EditMode tests

**Decisions**:
- AnimationStateId enum: Idle, Run, Sprint, Kick, Tackle, Celebrate
- AnimationActionTrigger enum for action overrides
- Speed parameter normalized 0-1 for Blend Tree (maxSpeed = 10.5)
- Action states override locomotion until CompleteAction() called
- Walk threshold 0.5 m/s, sprint requires speed > 5 m/s + isSprinting flag
- PlayerAnimator uses Animator.StringToHash for performance
- OnActionComplete() called by animation event or timer

**Known Issues**: None

**Next**: US-020 — Ball kick animation, US-022 — Ball trail

## 2026-02-27 Session — US-017: Tackle mechanic with cooldown and dispossession

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/TackleLogic.cs — Pure C# tackle logic with range, cooldown, stun
- OpenFifa/Assets/Scripts/Gameplay/TackleSystem.cs — Tackle MonoBehaviour with physics lunge
- OpenFifa/Assets/Tests/Editor/US017_TackleTests.cs — 12 EditMode tests

**Decisions**:
- TackleLogic pure C# for EditMode testing: range check, cooldown tracking, stun duration
- Default radius 1.5m, cooldown 1.0s, stun 0.5s
- Lunge via Rigidbody.AddForce impulse toward ball carrier
- Dispossession calls BallOwnership.Release() to make ball loose
- Stunned player has PlayerController disabled for stun duration via coroutine
- TackleResult struct captures lunge/dispossess/stun data

**Known Issues**: None

**Next**: US-019 — Player animations, US-022 — Ball trail

## 2026-02-27 Session — US-018: Ball ownership tracking system

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/BallOwnershipLogic.cs — Pure C# ownership tracking logic
- OpenFifa/Assets/Scripts/Gameplay/BallOwnership.cs — Ball ownership MonoBehaviour
- OpenFifa/Assets/Scripts/Gameplay/PlayerIdentity.cs — Player identity component
- OpenFifa/Assets/Tests/Editor/US018_BallOwnershipTests.cs — 12 EditMode tests

**Decisions**:
- BallOwnershipLogic uses int ID (-1 = no owner) for pure C# testability
- CanClaim checks both ownership state and distance
- SetOwner suppresses event if owner unchanged (prevents duplicate events)
- Transfer method for direct ownership handoff without releasing first
- BallOwnership MonoBehaviour follows owner in FixedUpdate with configurable offset
- Auto-claim via Physics.OverlapSphere on player layer when ball is loose
- PlayerIdentity component provides ID and team affiliation per player

**Known Issues**: None

**Next**: US-017 — Tackle mechanic (now unblocked), US-019 — Player animations

## 2026-02-27 Session — US-016: Player switching to nearest teammate

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/PlayerSwitchLogic.cs — Pure C# nearest player finding and switch logic
- OpenFifa/Assets/Scripts/Gameplay/PlayerSwitcher.cs — Player switching MonoBehaviour with Input System
- OpenFifa/Assets/Tests/Editor/US016_PlayerSwitchingTests.cs — 9 EditMode tests

**Decisions**:
- PlayerSwitchLogic pure C# for EditMode testability
- Tie-breaking: lowest index wins (deterministic)
- PerformSwitch excludes current player (always switches to a different one)
- Visual indicator via child "ActiveIndicator" GameObject toggling
- PlayerController enabled on active, AIController enabled on non-active
- SwitchResult struct captures previous/new index and whether switch occurred

**Known Issues**: None

**Next**: US-018 — Ball ownership tracking, US-019 — Player animations

## 2026-02-27 Session — US-014: Match state machine with full flow and pause support

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/MatchState.cs — Match state enum (PreKickoff, FirstHalf, HalfTime, SecondHalf, FullTime, GoalCelebration, Paused)
- OpenFifa/Assets/Scripts/Core/MatchStateMachine.cs — Pure C# match state machine with enforced valid transitions
- OpenFifa/Assets/Tests/Editor/US014_MatchStateMachineTests.cs — 12 EditMode tests

**Decisions**:
- MatchStateMachine uses Dictionary<MatchState, HashSet<MatchState>> for valid transitions
- Pause preserves _previousState, Resume restores it
- GoalCelebration transitions back to PreKickoff for kickoff reset
- TransitionTo(Paused) delegates to Pause() internally
- InvalidOperationException thrown on invalid transitions
- Event: OnStateChanged fires (oldState, newState) on every transition

**Known Issues**: None

**Next**: US-016 — Player switching, US-018 — Ball ownership

## 2026-02-27 Session — US-015: Kickoff sequence with ball placement and player reset

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/KickoffState.cs — Kickoff state enum
- OpenFifa/Assets/Scripts/Core/KickoffLogic.cs — Pure C# kickoff logic
- OpenFifa/Assets/Scripts/Gameplay/KickoffSequence.cs — Kickoff MonoBehaviour
- OpenFifa/Assets/Tests/Editor/US015_KickoffTests.cs — 8 EditMode tests

**Decisions**:
- KickoffLogic tracks which team kicks off (alternates on each goal)
- Ball set to kinematic during setup, dynamic when play begins
- Configurable setup delay (default 1s)
- KickoffSequence uses coroutine for sequenced setup

**Known Issues**: None

**Next**: US-014 — Match state machine (now unblocked), US-016 — Player switching

## 2026-02-27 Session — US-013: Goalkeeper AI with positioning and diving

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/GoalkeeperState.cs — Goalkeeper state enum
- OpenFifa/Assets/Scripts/Core/GoalkeeperLogic.cs — Pure C# GK logic
- OpenFifa/Assets/Scripts/AI/GoalkeeperAI.cs — Goalkeeper MonoBehaviour
- OpenFifa/Assets/Tests/Editor/US013_GoalkeeperAITests.cs — 8 EditMode tests

**Decisions**:
- GoalkeeperLogic pure C#: lateral positioning, shot detection, ball arrival prediction
- Lateral position: lerp 60% toward ball, clamped to goal area width
- Shot detection: dot product > 0.5 with speed above threshold
- Dive: rapid movement to predicted ball arrival point
- Recovery: slow return to center over configurable time

**Known Issues**: None

**Next**: US-014 — Match state machine, or US-015 — Kickoff sequence

## 2026-02-27 Session — US-012: AI shooting toward goal

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/ShotEvaluator.cs — Pure C# shot evaluation
- OpenFifa/Assets/Scripts/AI/AIShootingSystem.cs — AI shooting MonoBehaviour
- OpenFifa/Assets/Tests/Editor/US012_AIShootingTests.cs — 8 EditMode tests

**Decisions**:
- ShotEvaluator pure C#: range check, force calculation, randomized target
- Shot target varies within 80% of goal width for unpredictability
- Physics.Linecast checks for clear line to goal (defender layer mask)
- Shooting priority over passing when in range with clear line

**Known Issues**: None

**Next**: US-013 — Goalkeeper AI

## 2026-02-27 Session — US-011: AI passing to detect open teammate and pass

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/PositionData.cs — Lightweight position struct
- OpenFifa/Assets/Scripts/Core/PassEvaluator.cs — Pure C# pass evaluation logic
- OpenFifa/Assets/Scripts/AI/AIPassingSystem.cs — AI passing MonoBehaviour
- OpenFifa/Assets/Tests/Editor/US011_AIPassingTests.cs — 7 EditMode tests

**Decisions**:
- PassEvaluator is pure C# for openness calculation and force scaling
- Openness = distance to nearest opponent (higher = more open)
- Pass force scales with distance, clamped to [4, 20] range
- AIPassingSystem wraps PassEvaluator with Unity-specific ball force application
- LastSelectedTargetIndex exposed for test verification

**Known Issues**: None

**Next**: US-012 — AI shooting toward goal

## 2026-02-27 Session — US-010: AI player finite state machine

**Status**: Completed
**Changes**:
- OpenFifa/Assets/Scripts/Core/AIState.cs — AI state enum
- OpenFifa/Assets/Scripts/Core/AIConfigData.cs — AI config data class
- OpenFifa/Assets/Scripts/Core/AIDecisionEngine.cs — Pure C# decision logic
- OpenFifa/Assets/Scripts/AI/AIController.cs — AI MonoBehaviour with FSM
- OpenFifa/Assets/Tests/Editor/US010_AIStateTests.cs — 11 EditMode tests
- OpenFifa/Assets/Tests/Runtime/US010_AIControllerTests.cs — 5 PlayMode tests

**Decisions**:
- AIDecisionEngine is pure C# for EditMode testing
- Decision logic: nearest+inRange->Chase, atFormation+ballFar->Idle, else->ReturnToPosition
- Movement via direct Rigidbody velocity setting
- State transitions logged in editor via Debug.Log
- AI uses SetAsNearestToBall() externally set by team coordinator

**Known Issues**: None

**Next**: US-011 — AI passing

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
