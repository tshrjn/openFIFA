<p align="center">
  <img src="https://img.shields.io/badge/Unity-6%20LTS-000000?style=for-the-badge&logo=unity&logoColor=white" alt="Unity 6 LTS"/>
  <img src="https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white" alt="C#"/>
  <img src="https://img.shields.io/badge/macOS-000000?style=for-the-badge&logo=apple&logoColor=white" alt="macOS"/>
  <img src="https://img.shields.io/badge/iPad-000000?style=for-the-badge&logo=apple&logoColor=white" alt="iPad"/>
  <img src="https://img.shields.io/badge/License-MIT-blue?style=for-the-badge" alt="MIT License"/>
  <img src="https://img.shields.io/github/stars/tshrjn/openfifa?style=for-the-badge&color=yellow" alt="Stars"/>
</p>

<h1 align="center">OpenFifa</h1>

<p align="center">
  <strong>The open-source AAA arcade soccer game for macOS & iPad</strong><br/>
  5v5 fast-paced matches. Premium gameplay. Built with Unity. Powered by AI agents.
</p>

<p align="center">
  <a href="#getting-started">Getting Started</a> &bull;
  <a href="#gameplay">Gameplay</a> &bull;
  <a href="#contributing">Contributing</a> &bull;
  <a href="#request-for-art">Art Wanted</a> &bull;
  <a href="#roadmap">Roadmap</a>
</p>

---

## What is OpenFifa?

OpenFifa is an **open-source, AAA arcade-style 5v5 soccer game** for macOS and iPad. Think FIFA meets Sensible Soccer — fast matches, premium-quality gameplay, intuitive controls, and pure fun.

Built from scratch using **Unity 6 LTS** with a unique twist: the entire codebase is being developed through **AI-assisted long-horizon task automation** — every feature starts as a test, every component is independently verifiable, and the entire development process is documented in real-time.

> **This is not just a game. It's an experiment in building complex software with AI coding agents.**

<!-- TODO: Replace with actual gameplay GIF -->
<p align="center">
  <em>Gameplay preview coming soon — development is underway!</em>
</p>

---

## News & Updates

| Date | Update |
|------|--------|
| **2026-02-27** | Project scaffolding complete. 50 user stories defined. Development begins. |

<!-- Add new entries at the top of this table -->

---

## Features

| Feature | Status |
|---------|--------|
| 5v5 arcade matches | Planned |
| AI opponents with FSM behavior | Planned |
| Keyboard/mouse/trackpad controls (macOS) + touch controls (iPad) | Planned |
| Broadcast-style camera | Planned |
| Match flow (kickoff, halftime, fulltime) | Planned |
| Goal celebrations + replays | Planned |
| Team selection + formations | Planned |
| Sound effects + crowd audio | Planned |
| AAA gameplay optimized for macOS & iPad (60fps, <200MB) | Planned |
| Local multiplayer (same device) | Planned |

---

## Getting Started

### Prerequisites

- **Unity 6 LTS** (2022.3+) with macOS Build Support + iOS Build Support (for iPad)
- **Xcode 15+** (for macOS and iPad builds)
- **macOS 14+ Sonoma** (primary development and target platform)
- Git LFS (for large binary assets)

### Installation

```bash
# Clone the repository
git clone https://github.com/tshrjn/openfifa.git
cd openfifa

# Open in Unity Hub
# 1. Open Unity Hub → "Add project from disk"
# 2. Select the openfifa/ directory
# 3. Make sure Unity 6 LTS is selected as the editor version
# 4. Click "Open"
```

### Running Tests

```bash
# EditMode tests (fast — pure C# logic)
unity -runTests -batchmode -nographics -projectPath . -testPlatform EditMode -testResults ./test-results/editmode.xml

# PlayMode tests (physics, gameplay, integration)
unity -runTests -batchmode -projectPath . -testPlatform PlayMode -testResults ./test-results/playmode.xml
```

### Building for macOS & iPad

```bash
# Build macOS app
unity -batchmode -nographics -quit -projectPath . -buildTarget StandaloneOSX -executeMethod BuildScript.BuildMacOS

# Build iPad (via Xcode project)
unity -batchmode -nographics -quit -projectPath . -buildTarget iOS -executeMethod BuildScript.BuildIPad

# Run macOS app directly
open build/macOS/OpenFifa.app

# Compile iPad build for Simulator
xcodebuild -project build/iOS/Unity-iPhone.xcodeproj \
  -scheme Unity-iPhone \
  -destination 'platform=iOS Simulator,name=iPad Pro 13-inch (M4)' \
  build
```

---

## Gameplay

OpenFifa is designed for quick, fun matches:

- **5v5 format** — faster pace than 11v11, easier to follow
- **3-minute halves** — complete matches in ~7 minutes
- **Simplified rules** — no offsides, no VAR, just play
- **Keyboard/mouse (macOS)** + **virtual joystick (iPad)** — full control on both platforms
- **AI opponents** — difficulty scales from casual to challenging

### Controls

| Action | Keyboard/Mouse (macOS) | Touch (iPad) |
|--------|------------------------|--------------|
| Move | WASD / Arrow keys | Left joystick |
| Pass | Z / Left Click | Pass button |
| Shoot | X / Right Click | Shoot button |
| Tackle | C | Tackle button |
| Sprint | Left Shift | Hold Sprint button |
| Switch player | Tab | Tap switch button |
| Pause | Escape | Pause button |

---

## Contributing

We welcome contributions from everyone! OpenFifa is built by the community, for the community.

### Ways to Contribute

- **Code** — Pick a user story from [`prd.json`](prd.json), write tests, implement the feature
- **Art & Assets** — See [Request for Art](#request-for-art) below
- **Testing** — Run the game, report bugs, write test cases
- **Documentation** — Improve docs, tutorials, code comments
- **Game Design** — Suggest mechanics, balance tweaks, game feel improvements

### Development Workflow

1. Check [`prd.json`](prd.json) for available user stories
2. Read [`docs/IMPLEMENT.md`](docs/IMPLEMENT.md) for coding conventions
3. Write tests first (TDD) — see [`docs/TESTING.md`](docs/TESTING.md)
4. Implement the feature
5. Run the full test suite
6. Submit a PR with the story ID in the title (e.g., `US-003: Ball physics`)

### Code Standards

- **Namespace**: `OpenFifa.*`
- **Testing**: NUnit 3, `[Category("US-XXX")]` tags, tolerance-based assertions
- **Architecture**: Pure C# logic separated from MonoBehaviour, ScriptableObjects for tuning
- **Style**: `[SerializeField] private` fields, no public fields on MonoBehaviours

See [`CLAUDE.md`](CLAUDE.md) for the complete coding guide.

---

## Request for Art

**We need artists!** OpenFifa currently uses placeholder assets (Unity primitives with solid colors). We're looking for contributors to help with:

### Characters
- Low-poly humanoid soccer player models (male/female)
- Team jersey textures (10+ teams with distinct colors)
- Goalkeeper-specific model variant

### Animations
- Soccer-specific: dribbling, ball control, headers, bicycle kicks
- Celebrations: 5+ unique goal celebrations
- Goalkeeper: punching, distributing, catching

### Environment
- Stadium models (stands, floodlights, tunnels)
- Pitch details (grass texture with mowed bands, corner flags, nets)
- Scoreboard and advertising boards

### Audio
- Crowd chants and reactions
- Commentary clips (optional, stretch goal)
- Menu music

### Art Style Guide

We're targeting a **stylized low-poly aesthetic** — clean, colorful, and readable at a distance. Think of the visual clarity of Sensible Soccer combined with modern low-poly 3D art.

**Accepted formats**: FBX, glTF/GLB (models), PNG (textures), WAV (audio), TTF/OTF (fonts)

**License requirement**: All contributed assets must be **CC0, CC-BY, or MIT** licensed.

See [`docs/ASSETS.md`](docs/ASSETS.md) for the full asset pipeline guide and list of free sources we're already using.

---

## Roadmap

Development is organized into 6 phases with ~50 user stories:

```
Phase 1: Foundation        [US-001 — US-008]  ░░░░░░░░░░  0%
Phase 2: Core Gameplay     [US-009 — US-018]  ░░░░░░░░░░  0%
Phase 3: Game Feel         [US-019 — US-027]  ░░░░░░░░░░  0%
Phase 4: UI & Menus        [US-028 — US-035]  ░░░░░░░░░░  0%
Phase 5: Polish & Platform  [US-036 — US-043]  ░░░░░░░░░░  0%
Phase 6: Asset Integration [US-044 — US-050]  ░░░░░░░░░░  0%
```

See [`docs/PLAN.md`](docs/PLAN.md) for detailed milestones and [`prd.json`](prd.json) for all user stories.

---

## Architecture

OpenFifa follows a **test-driven, agent-friendly architecture**:

```
OpenFifa/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/          # Pure C# game logic (EditMode testable)
│   │   ├── Gameplay/      # MonoBehaviour controllers
│   │   ├── AI/            # Player AI, formations, goalkeeper
│   │   ├── UI/            # HUD, menus, transitions
│   │   └── Audio/         # Sound management
│   ├── ScriptableObjects/ # All tunable parameters
│   ├── Scenes/            # MainMenu, TeamSelect, Match, Results
│   ├── Prefabs/           # Player, Ball, Pitch, Goal, UI elements
│   └── Tests/
│       ├── Editor/        # EditMode tests (fast, pure logic)
│       └── Runtime/       # PlayMode tests (physics, integration)
├── docs/                  # Long-horizon task docs
└── prd.json               # User stories & task graph
```

Key principle: **Pure C# logic is separated from MonoBehaviour** so core game logic (scoring, match state, formations, AI decisions) can be tested in EditMode (milliseconds) without starting the Unity engine.

---

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Engine | Unity 6 LTS (2022.3+) |
| Render Pipeline | Universal Render Pipeline (URP) |
| Language | C# |
| Testing | NUnit 3 (Unity Test Framework) |
| CI/CD | GameCI (GitHub Actions) |
| Camera | Cinemachine |
| Input | Unity Input System |
| Physics | PhysX |
| UI | Unity UI + TextMeshPro |
| Target | macOS 14+ Sonoma (primary), iPadOS 17+ (secondary) |

---

## AI-Driven Development

OpenFifa is an experiment in **agent-driven game development**. Every feature is:

1. **Specified** as a user story with testable acceptance criteria ([`prd.json`](prd.json))
2. **Test-driven** — tests written before implementation ([`docs/TESTING.md`](docs/TESTING.md))
3. **Built by AI agents** — Claude Code sessions following the long-horizon task pattern
4. **Verified automatically** — 10-layer test suite with zero human visual verification required
5. **Documented in real-time** — every session logs decisions and progress ([`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md))

Inspired by:
- [OpenAI Codex Long Horizon Tasks](https://developers.openai.com/cookbook/examples/codex/long_horizon_tasks/)
- [Anthropic's AI-Built C Compiler](https://www.anthropic.com/engineering/claude-compiler)
- [Cursor's Planner-Worker-Judge Architecture](https://www.cursor.com/blog/scaling-with-agents)

---

## Star History

<a href="https://star-history.com/#tshrjn/openfifa&Date">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=tshrjn/openfifa&type=Date&theme=dark" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=tshrjn/openfifa&type=Date" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=tshrjn/openfifa&type=Date" />
 </picture>
</a>

---

## License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

## Community

- [GitHub Issues](https://github.com/tshrjn/openfifa/issues) — Bug reports and feature requests
- [GitHub Discussions](https://github.com/tshrjn/openfifa/discussions) — Questions, ideas, and general chat
<!-- - [Discord](https://discord.gg/openfifa) — Real-time community chat -->

---

<p align="center">
  Built with determination and AI agents.<br/>
  <strong>Star this repo</strong> if you believe open-source gaming deserves better.
</p>
