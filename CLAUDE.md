# OpenFifa — Agent Instructions

## Project Overview

OpenFifa is a 5v5 AAA arcade-style soccer game for macOS, built with Unity 6 LTS + C# + URP. This is a premium-quality game with AAA-quality art targeting EA FC / FIFA / PES visual standards, designed for macOS desktops with FIFA-style keyboard/mouse and Xbox-like gamepad controls. Development follows the long-horizon task pattern: each Claude Code session picks up where the last left off, guided by persistent documentation and a machine-readable PRD.

## Tech Stack

- **Engine**: Unity 6 LTS (2022.3+) with Universal Render Pipeline (URP)
- **Language**: C#
- **Target**: macOS 14+ Sonoma
- **Testing**: NUnit 3 (Unity Test Framework) — EditMode + PlayMode
- **Camera**: Cinemachine
- **Input**: Unity Input System
- **Physics**: PhysX
- **UI**: Unity UI + TextMeshPro
- **CI/CD**: GameCI (GitHub Actions)

## Key Documents

Read these before starting work:

| Document | Purpose |
|----------|---------|
| [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md) | **Read first** — latest status, completed work, known issues |
| [`prd.json`](prd.json) | User stories — find your next task (first `passes: false` with all deps met) |
| [`docs/PROMPT.md`](docs/PROMPT.md) | Frozen game spec — goals, non-goals, constraints |
| [`docs/PLAN.md`](docs/PLAN.md) | Milestones and phase checkpoints |
| [`docs/IMPLEMENT.md`](docs/IMPLEMENT.md) | Coding patterns, test protocol, failure handling |
| [`docs/TESTING.md`](docs/TESTING.md) | 10-layer testing strategy reference |
| [`docs/ASSETS.md`](docs/ASSETS.md) | Asset sources and import workflows |

## Directory Structure

```
OpenFifa/                              # Unity project root
├── Assets/
│   ├── Scripts/
│   │   ├── Core/                      # Pure C# — NO MonoBehaviour (EditMode testable)
│   │   │   ├── MatchState.cs          # Score, timer, match flow
│   │   │   ├── MatchTimer.cs          # Countdown logic
│   │   │   ├── Formation.cs           # Position definitions
│   │   │   └── AIDecisionEngine.cs    # AI logic (pure)
│   │   ├── Gameplay/                  # MonoBehaviour controllers
│   │   │   ├── PlayerController.cs
│   │   │   ├── BallController.cs
│   │   │   ├── GoalDetector.cs
│   │   │   └── MatchController.cs
│   │   ├── AI/                        # AI behaviors
│   │   │   ├── AIController.cs
│   │   │   ├── GoalkeeperAI.cs
│   │   │   └── States/               # FSM states
│   │   ├── UI/                        # UI controllers
│   │   └── Audio/                     # Audio management
│   ├── ScriptableObjects/             # All tunable parameters
│   │   ├── BallPhysicsConfig.asset
│   │   ├── PlayerStatsConfig.asset
│   │   ├── FormationData/
│   │   └── MatchConfig.asset
│   ├── Scenes/
│   │   ├── MainMenu.unity
│   │   ├── TeamSelect.unity
│   │   ├── Match.unity
│   │   └── Results.unity
│   ├── Prefabs/
│   ├── Materials/
│   ├── Animations/
│   └── Tests/
│       ├── Editor/                    # EditMode tests (fast, pure logic)
│       │   ├── MatchScoreTests.cs
│       │   ├── MatchTimerTests.cs
│       │   └── FormationTests.cs
│       └── Runtime/                   # PlayMode tests (physics, integration)
│           ├── BallPhysicsTests.cs
│           ├── PlayerMovementTests.cs
│           ├── AIBehaviorTests.cs
│           └── E2ETests.cs
├── docs/
├── prd.json
└── CLAUDE.md                          # This file
```

## Coding Conventions

### Architecture

- **Separate pure C# from MonoBehaviour.** Game logic (scoring, timer, formations, AI decisions) goes in `Scripts/Core/` as plain C# classes. MonoBehaviour wrappers in `Scripts/Gameplay/` call into Core logic. This enables EditMode testing (milliseconds) for all game logic.
- **ScriptableObjects for all tunable values.** Ball mass, player speed, formation positions, match duration — everything goes in a ScriptableObject. Never hardcode gameplay parameters.
- **No public fields on MonoBehaviours.** Use `[SerializeField] private` for Inspector-exposed fields.

### Naming

- **Namespace**: `OpenFifa.Core`, `OpenFifa.Gameplay`, `OpenFifa.AI`, `OpenFifa.UI`, `OpenFifa.Audio`
- **Tests**: `MethodUnderTest_Scenario_ExpectedResult` (e.g., `Ball_WhenKicked_ReachesGoalInExpectedTime`)
- **Test files**: `{Feature}Tests.cs` (e.g., `BallPhysicsTests.cs`)
- **Test categories**: `[Category("US-XXX")]` matching the story ID

### Code Style

- One class per file
- `readonly` where possible
- `var` for obvious types, explicit types for ambiguous
- No `#region` blocks
- Events use C# `event Action<T>` pattern, not UnityEvent (faster, testable)

## Testing Commands

```bash
# EditMode tests — run after EVERY code change (< 30 seconds)
unity -runTests -batchmode -nographics -projectPath ./OpenFifa -testPlatform EditMode -testResults ./test-results/editmode.xml

# PlayMode tests — run per story completion (< 5 minutes)
unity -runTests -batchmode -projectPath ./OpenFifa -testPlatform PlayMode -testResults ./test-results/playmode.xml

# Specific story tests
unity -runTests -batchmode -nographics -projectPath ./OpenFifa -testPlatform EditMode -testCategory "US-007" -testResults ./test-results/us007.xml

# Full suite (EditMode + PlayMode)
unity -runTests -batchmode -projectPath ./OpenFifa -testPlatform EditMode -testResults ./test-results/editmode.xml && \
unity -runTests -batchmode -projectPath ./OpenFifa -testPlatform PlayMode -testResults ./test-results/playmode.xml

# Build verification (macOS)
unity -batchmode -nographics -quit -projectPath ./OpenFifa -buildTarget StandaloneOSX

# Build verification (macOS only — iPad deferred)
# unity -batchmode -nographics -quit -projectPath ./OpenFifa -buildTarget iOS
```

## Quality Gates

Every completed story MUST pass ALL of these:

1. **Build**: `unity -batchmode -nographics -quit -buildTarget StandaloneOSX` and `-buildTarget iOS` both exit with code 0
2. **Story tests**: `unity -runTests -testCategory "US-XXX"` — all pass
3. **Full EditMode suite**: zero failures
4. **Full PlayMode suite**: zero failures (excluding `[Category("Quarantine")]`)
5. **No new warnings**: Unity console has no new warnings from your changes

## Session Protocol

### Starting a Session

1. Read `docs/DOCUMENTATION.md` — understand what was completed, what's in progress, known issues
2. Check `prd.json` — find the next story where `passes: false` and all `dependsOn` stories have `passes: true`
3. Read the story's acceptance criteria carefully
4. Read relevant existing code if modifying existing systems

### During Work

1. Write tests FIRST (TDD) when possible
2. Run EditMode tests after every code change
3. Run PlayMode tests after completing a feature
4. If a test fails: read the assertion message, debug, fix, re-run
5. If a test is flaky: tag with `[Category("Quarantine")]`, note in DOCUMENTATION.md

### Ending a Session

1. Run the FULL test suite — all must pass
2. Update `prd.json` — set `passes: true` for completed stories
3. Update `docs/DOCUMENTATION.md` with:
   - What was completed
   - Key decisions made
   - Known issues discovered
   - What the next session should pick up
4. Commit with story ID: `feat(US-XXX): description of change`

## Common Patterns

### Physics Testing (tolerance-based)

```csharp
// NEVER exact equality for physics
Assert.That(ball.transform.position.z, Is.InRange(14f, 23f),
    $"Ball landed at {ball.transform.position.z}m — outside range");
```

### Condition-Based Waits (not time-based)

```csharp
// NEVER: yield return new WaitForSeconds(2f); Assert.IsTrue(done);
// ALWAYS:
float timeout = 5f, elapsed = 0f;
while (!condition && elapsed < timeout)
{
    elapsed += Time.deltaTime;
    yield return null;
}
Assert.IsTrue(condition, $"Timed out after {timeout}s");
```

### Informative Assert Messages

```csharp
Assert.That(player.transform.position.x, Is.InRange(-25f, 25f),
    $"Player at ({player.transform.position}) outside pitch. " +
    $"Velocity: {rb.velocity}, LastCollision: {lastCollisionTag}");
```
