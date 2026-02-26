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
