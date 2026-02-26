# OpenFifa: Agent Operational Runbook

> How to write code, run tests, and handle failures. Follow this exactly.

---

## Session Start Checklist

1. **Read `docs/DOCUMENTATION.md`** — What was completed last session? Any blockers? Known issues?
2. **Read `prd.json`** — Find the next story: `passes: false` with all `dependsOn` stories having `passes: true`
3. **Read the story's acceptance criteria** — These are your definition of done
4. **Read relevant existing code** — Understand what you're building on before writing new code
5. **Identify which tests to write** — EditMode for pure logic, PlayMode for physics/engine

---

## Code Writing Patterns

### Separation of Concerns

**Pure C# classes** go in `Assets/Scripts/Core/`. These have NO Unity dependencies (no MonoBehaviour, no GameObject, no Transform). They are testable in EditMode (milliseconds).

```csharp
// Assets/Scripts/Core/MatchState.cs
namespace OpenFifa.Core
{
    public class MatchState
    {
        public int ScoreA { get; private set; }
        public int ScoreB { get; private set; }

        public void RecordGoal(Team team, int scorerId, float minute)
        {
            if (team == Team.A) ScoreA++;
            else ScoreB++;
            Goals.Add(new GoalRecord(team, scorerId, minute));
        }
    }
}
```

**MonoBehaviour wrappers** go in `Assets/Scripts/Gameplay/`. They bridge Unity engine features to Core logic.

```csharp
// Assets/Scripts/Gameplay/MatchController.cs
namespace OpenFifa.Gameplay
{
    public class MatchController : MonoBehaviour
    {
        private MatchState _state; // Core logic, no Unity dependency

        private void Awake()
        {
            _state = new MatchState(/* config from ScriptableObject */);
        }
    }
}
```

### ScriptableObjects for All Tuning

Never hardcode gameplay values. Every tunable parameter lives in a ScriptableObject:

```csharp
[CreateAssetMenu(menuName = "OpenFifa/Config/Ball Physics")]
public class BallPhysicsConfig : ScriptableObject
{
    [SerializeField] private float _mass = 0.43f;
    [SerializeField] private float _bounciness = 0.6f;
    [SerializeField] private float _friction = 0.5f;
    [SerializeField] private float _drag = 0.1f;
    [SerializeField] private float _angularDrag = 0.5f;

    public float Mass => _mass;
    public float Bounciness => _bounciness;
    // ...
}
```

### Event Pattern

Use C# events, not UnityEvent:

```csharp
public event Action<Team, int> OnGoalScored; // (team, scorerId)
public event Action<MatchPeriod> OnPeriodChanged;
public event Action<PlayerIdentity> OnBallOwnerChanged;
```

### No Public Fields

```csharp
// BAD
public float speed = 10f;

// GOOD
[SerializeField] private float _speed = 10f;
public float Speed => _speed;
```

---

## Test Writing Protocol

### Write Tests FIRST (TDD)

1. Read the acceptance criteria
2. Write a test for each criterion
3. Run tests — they should FAIL (red)
4. Write the minimum code to make them pass (green)
5. Refactor if needed
6. Run again — all green

### EditMode Tests (Pure Logic)

```csharp
// Assets/Tests/Editor/US007_MatchTimerTests.cs
using NUnit.Framework;
using OpenFifa.Core;

[TestFixture]
[Category("US-007")]
public class MatchTimerTests
{
    [Test]
    public void MatchTimer_Advance_CountsDown()
    {
        var timer = new MatchTimer(halfDurationSeconds: 180f);
        timer.Advance(10f);
        Assert.AreEqual(170f, timer.RemainingSeconds, 0.01f);
    }
}
```

### PlayMode Tests (Physics/Engine)

```csharp
// Assets/Tests/Runtime/US003_BallPhysicsTests.cs
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
[Category("US-003")]
public class BallPhysicsTests
{
    [UnityTest]
    public IEnumerator Ball_WhenDropped_BouncesWithinRestitutionRange()
    {
        var ball = CreateTestBall(height: 3f);
        yield return WaitUntilBounced(ball, timeout: 3f);

        float bounceHeight = GetMaxHeightAfterBounce(ball);
        float restitution = bounceHeight / 3f;

        Assert.That(restitution, Is.InRange(0.45f, 0.75f),
            $"Bounce restitution {restitution:F3} outside [0.45, 0.75]");
    }
}
```

### Test Assertion Rules

1. **Never exact equality for physics** — use `Is.InRange()` or tolerance
2. **Never time-based waits for sync** — use condition-based polling with timeout
3. **Always include debugging context** in assertion messages
4. **Category tag must match story ID** — `[Category("US-003")]`
5. **One assertion per test when possible** — easier to debug failures

---

## Quality Gates

Run these in order after completing a story:

### Gate 1: Story Tests (< 30 seconds)
```bash
unity -runTests -batchmode -nographics -testCategory "US-XXX" -testResults ./test-results/gate1.xml
```

### Gate 2: Full EditMode Suite (< 2 minutes)
```bash
unity -runTests -batchmode -nographics -testPlatform EditMode -testResults ./test-results/gate2.xml
```

### Gate 3: Full PlayMode Suite (< 10 minutes)
```bash
unity -runTests -batchmode -testPlatform PlayMode -testResults ./test-results/gate3.xml
```

### Gate 4: Build Verification
```bash
# macOS build
unity -batchmode -nographics -quit -buildTarget StandaloneOSX

# iPad build
unity -batchmode -nographics -quit -buildTarget iOS
```

If ANY gate fails, do NOT proceed. Debug and fix first.

---

## Failure Handling

### Test Failure
1. Read the full assertion message (it contains debugging context)
2. Identify root cause — is it your code or a pre-existing issue?
3. Fix the code, not the test (unless the test is wrong)
4. Re-run the failing test
5. Then re-run the full suite

### Build Failure
1. Read the compiler error message
2. Common causes: missing `using`, wrong namespace, missing reference
3. Fix and re-build

### Flaky Test (passes sometimes, fails sometimes)
1. Tag with `[Category("Quarantine")]`
2. Add to known issues in `docs/DOCUMENTATION.md`
3. Create a note: what it tests, how it fails, suspected cause
4. Do NOT delete flaky tests — quarantine and investigate later

### Scene/Prefab Conflicts
1. Scene files (.unity) and prefabs (.prefab) merge poorly
2. If modifying a scene: make minimal changes, commit separately
3. If creating new scene content: use prefabs (instantiate at runtime) over scene hierarchy
4. Prefer programmatic scene setup in tests over loading scene files

---

## Commit Protocol

```bash
# Commit message format
git commit -m "feat(US-003): implement ball physics with PlayMode tests

- Add BallPhysicsConfig ScriptableObject
- Rigidbody mass=0.43kg, bounce=0.6, friction=0.5
- PlayMode tests for bounce, roll, drift
- All existing tests still pass"
```

### Commit Checklist
- [ ] All quality gates pass
- [ ] `prd.json` updated (`passes: true` for completed story)
- [ ] `docs/DOCUMENTATION.md` updated with session summary
- [ ] Commit message includes story ID
- [ ] No unintended file changes (check `git diff --stat`)

---

## Unity-Specific Tips

### Package Dependencies (install via Package Manager)
- `com.unity.cinemachine` — Camera system
- `com.unity.inputsystem` — New Input System
- `com.unity.textmeshpro` — UI text
- `com.unity.test-framework` — Testing (usually pre-installed)
- `com.unity.test-framework.performance` — Performance benchmarks

### Test Assembly Setup
- `Assets/Tests/Editor/` needs `OpenFifa.Tests.Editor.asmdef` referencing EditMode
- `Assets/Tests/Runtime/` needs `OpenFifa.Tests.Runtime.asmdef` referencing PlayMode
- Both must reference the main `OpenFifa.asmdef`

### Build Scripts
```csharp
// Assets/Editor/BuildScript.cs
public static class BuildScript
{
    public static void BuildMacOS()
    {
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray(),
            locationPathName = "build/macOS/OpenFifa.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        };
        BuildPipeline.BuildPlayer(options);
    }

    public static void BuildIPad()
    {
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray(),
            locationPathName = "build/iOS",
            target = BuildTarget.iOS,
            options = BuildOptions.None
        };
        BuildPipeline.BuildPlayer(options);
    }
}
```
