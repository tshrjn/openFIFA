# OpenFifa: Automated Testing Strategy

> **Core principle**: Every user story must have acceptance criteria expressible as automated tests. If a criterion can't be tested programmatically, rewrite it until it can — or defer to a human-in-the-loop polish phase.

---

## Testing Layers Overview

| Layer | Type | Speed | When to Run |
|-------|------|-------|-------------|
| 1. EditMode Unit Tests | NUnit, pure C# | Milliseconds | After every code change |
| 2. PlayMode Integration | NUnit + coroutines | Seconds-minutes | Per story completion |
| 3. Physics Simulation | PlayMode + PhysX | Seconds | When physics code changes |
| 4. Visual Regression | Screenshot diff | Minutes | After visual changes |
| 5. Performance Budgets | Profiler asserts | Minutes | Per phase checkpoint |
| 6. Platform Build Verification | macOS .app + xcodebuild (iPad) | 15-30 min | Per phase checkpoint |
| 7. State Machine Verification | EditMode | Milliseconds | When FSM code changes |
| 8. E2E Game Loop | PlayMode + scene loads | Minutes | Phase 4+ checkpoints |
| 9. AI Behavior Validation | PlayMode + simulation | Minutes | When AI code changes |
| 10. CI Pipeline (GameCI) | All of the above | 30-60 min | Every push to main |

---

## 1. EditMode vs PlayMode Decision Matrix

| What You're Testing | Mode | Why |
|---|---|---|
| Score calculation, timer, formations | EditMode | Pure C#, no engine, runs in ms |
| ScriptableObject validation | EditMode | Data-only, no MonoBehaviour |
| Ball physics (force, velocity, trajectory) | PlayMode | Requires PhysX simulation |
| Player controller (movement, input) | PlayMode | Requires MonoBehaviour.Update |
| Animation state transitions | PlayMode | Requires Animator + time |
| Scene loading / UI navigation | PlayMode | Requires full engine lifecycle |
| AI decision logic (pure) | EditMode | If separated from MonoBehaviour |
| AI behavior in-game (positioning) | PlayMode | Requires physics + multiple objects |

---

## 2. CLI Commands

```bash
# EditMode (fast — run after EVERY change)
unity -runTests -batchmode -nographics \
  -projectPath ./OpenFifa \
  -testPlatform EditMode \
  -testResults ./test-results/editmode.xml

# PlayMode (slower — run per story)
unity -runTests -batchmode \
  -projectPath ./OpenFifa \
  -testPlatform PlayMode \
  -testResults ./test-results/playmode.xml

# Specific story
unity -runTests -batchmode -nographics \
  -projectPath ./OpenFifa \
  -testCategory "US-007" \
  -testResults ./test-results/us007.xml

# Exit code: 0 = all pass, non-zero = failures
# XML contains detailed failure messages
```

---

## 3. Physics Testing Patterns

### Tolerance-Based Assertions (MANDATORY)

Unity's PhysX is **not deterministic across platforms**. Never use exact equality.

```csharp
private const float POSITION_TOLERANCE = 0.05f; // 5cm
private const float VELOCITY_TOLERANCE = 0.1f;  // 0.1 m/s
private const float ANGLE_TOLERANCE = 2f;        // 2 degrees

// NEVER: Assert.AreEqual(expected, ball.transform.position);
// ALWAYS: Assert.That(value, Is.InRange(min, max), "context...");
```

### Condition-Based Waits (MANDATORY)

```csharp
// BAD — flaky, arbitrary timing
yield return new WaitForSeconds(2f);
Assert.IsTrue(ball.IsInGoal);

// GOOD — deterministic, with timeout
float timeout = 5f, elapsed = 0f;
while (!ball.IsInGoal && elapsed < timeout)
{
    elapsed += Time.deltaTime;
    yield return null;
}
Assert.IsTrue(ball.IsInGoal,
    $"Ball did not reach goal after {timeout}s. " +
    $"Position: {ball.transform.position}, Velocity: {rb.velocity}");
```

### Ball Physics Test Suite

Required tests for US-003:
- Bounce restitution within 0.45-0.75 range
- Rolling ball stops within 6-22m from 10m/s
- Ball cannot escape pitch boundaries
- Stationary ball does not drift (< 1cm over 2 seconds)
- Magnus effect produces lateral drift with spin
- Goal post collision bounces correctly

---

## 4. Visual Regression Testing

### Approach: Capture-Compare-Threshold

```csharp
[Category("VisualRegression")]
public class VisualRegressionTests
{
    private const float MAX_PIXEL_DIFF_PERCENT = 2.0f;
    private const string GOLDEN_DIR = "Assets/Tests/GoldenImages/";

    [UnityTest]
    public IEnumerator Scene_MatchesGoldenImage()
    {
        // Load scene, wait for settle, capture screenshot
        // Compare against golden image with tolerance
        // First run: save as golden, pass the test
    }
}
```

### Camera Checkpoints
Define 3 fixed camera positions for consistent visual testing:
1. **KickoffBroadcast** — Default broadcast view at kickoff
2. **GoalCloseup** — Close-up of goal area
3. **CornerFlag** — Corner view for pitch rendering check

### Headless Rendering
- Visual regression requires GPU (use macOS CI runners)
- GameCI Docker images include virtual GPU for headless rendering
- Skip visual tests on Linux runners

---

## 5. Performance Budget Assertions

```csharp
[Category("Performance")]
public class PerformanceTests
{
    [UnityTest]
    public IEnumerator Gameplay_MaintainsFrameRate()
    {
        // Measure 5 seconds of gameplay
        // Assert: avg FPS > 28, worst frame < 100ms, < 5% spikes over 33ms
    }

    [UnityTest]
    public IEnumerator DrawCalls_UnderBudget()
    {
        // Assert: batches < 100, setPassCalls < 15, triangles < 200K
    }

    [UnityTest]
    public IEnumerator GameplayLoop_ZeroGCAllocations()
    {
        // Warm up, then measure GC.Alloc recorder for 2 seconds
        // Assert: < 5 allocations total
    }
}
```

---

## 6. Platform Build Verification

Script: `scripts/verify-build.sh`

### macOS Checks:
1. macOS .app bundle exists and is valid
2. App bundle < 200MB
3. Required frameworks present (Metal, AppKit, AVFoundation)
4. .app launches without crash (smoke test)

### iPad Checks (deferred):
iPad build verification is deferred. Touch controls are not in v1 scope.

---

## 7. State Machine Testing

```csharp
[Category("StateMachine")]
public class MatchFlowTests
{
    [Test]
    public void MatchState_FollowsCorrectSequence()
    {
        // PreKickoff → FirstHalf → HalfTime → SecondHalf → FullTime
    }

    [Test]
    public void MatchState_RejectsInvalidTransitions()
    {
        // Can't score during PreKickoff, can't restart during play, etc.
    }

    [Test]
    public void MatchState_PausePreservesState()
    {
        // Pause and resume returns to correct state
    }
}
```

---

## 8. E2E Game Loop Testing

```csharp
[Category("E2E")]
[Timeout(300000)] // 5 minutes
public class EndToEndTests
{
    [UnityTest]
    public IEnumerator FullGameLoop_MainMenuToResults()
    {
        // MainMenu → Play button → TeamSelect → Confirm →
        // Match (100x speed) → FullTime → Results → MainMenu
        // No errors, no crashes
    }
}
```

---

## 9. AI Behavior Validation

```csharp
[Category("AI")]
public class AIBehaviorTests
{
    [UnityTest]
    public IEnumerator AI_WithBall_PassesToOpenTeammate()
    {
        // Set up scenario, let AI think 2s, verify pass occurred
    }

    [UnityTest]
    public IEnumerator AI_Goalkeeper_ShiftsTowardBall()
    {
        // Ball on right side, GK should shift right within 1s
    }

    [UnityTest]
    public IEnumerator AI_Defenders_ReturnToFormation()
    {
        // Ball far away, defenders should return to home position
    }
}
```

---

## 10. CI/CD Pipeline (GameCI)

See `.github/workflows/openfifa-ci.yml` for full configuration.

### Pipeline Stages

```
Push to main/ralph/*
  → EditMode tests (< 2 min, ubuntu)
  → PlayMode tests (< 15 min, ubuntu, needs editmode)
  → Platform builds: macOS + iPad (< 30 min, macos, needs playmode)

Nightly schedule
  → Performance + Visual Regression (macos with GPU)
```

---

## Agent Testing Loop

```
┌─────────────────────────────────────────────┐
│  1. Pick story from prd.json                │
│  2. Write tests for acceptance criteria     │
│  3. Run tests — should FAIL (red)           │
│  4. Write implementation code               │
│  5. Run story tests — should PASS (green)   │
│  6. Run full EditMode suite (< 30s)         │
│  7. Run full PlayMode suite (< 5min)        │
│  8. If any fail: debug, fix, go to step 5   │
│  9. Build verification                      │
│  10. Commit + update docs                   │
│  11. Next story                             │
└─────────────────────────────────────────────┘
```

---

## Anti-Patterns

- **Do NOT test rendering pixel-by-pixel without tolerance** — GPU differences cause 1-2% variation
- **Do NOT use `WaitForSeconds(N)` for synchronization** — use condition-based polling
- **Do NOT test Unity Editor GUI operations** — they break in batch mode
- **Do NOT delete flaky tests** — quarantine with `[Category("Quarantine")]`
- **Do NOT skip tests to make CI green** — fix the root cause

---

## Informative Assert Messages

```csharp
// BAD — agent gets no context
Assert.IsTrue(result);

// GOOD — agent gets full debugging context
Assert.That(player.transform.position.x, Is.InRange(-25f, 25f),
    $"Player at ({player.transform.position.x:F2}, {player.transform.position.z:F2}) " +
    $"outside pitch [-25, 25]. Velocity: ({rb.velocity.x:F2}, {rb.velocity.z:F2}). " +
    $"Last collision: {controller.LastCollisionTag}. Boundary enabled: {collider.enabled}");
```
