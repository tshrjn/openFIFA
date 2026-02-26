# OpenFifa: Development Milestones

> Source of truth for progress tracking. Updated as stories complete.

---

## Phase Overview

| Phase | Stories | Description | Checkpoint |
|-------|---------|-------------|------------|
| 1. Foundation | US-001 — US-008 | Project scaffold, pitch, ball, player, goals, camera, HUD | Ball bounces on pitch, macOS app builds |
| 2. Core Gameplay | US-009 — US-018 | AI, goalkeeper, match FSM, scoring, switching, tackling | Full 5v5 match at 100x speed without crash |
| 3. Game Feel | US-019 — US-027 | Animations, particles, SFX, celebrations, replays | All animations trigger correctly, audio plays |
| 4. UI & Menus | US-028 — US-035 | Full menu flow, HUD, settings, transitions | E2E: menu → select → match → results → menu |
| 5. Polish & Platform | US-036 — US-043 | Controls (keyboard/mouse + touch), haptics, performance, multi-res | 60fps on macOS, 30fps on iPad, <200MB, zero GC |
| 6. Asset Integration | US-044 — US-050 | Real models, Mixamo anims, stadium, audio, local MP | Visual baselines set, all asset swaps verified |

---

## Phase 1: Foundation

**Goal**: A green pitch with a bouncing ball, one controllable player, goal detection, camera, and a basic score/timer HUD. Builds as macOS .app and iPad Simulator target.

| Story | Title | Status |
|-------|-------|--------|
| US-001 | Unity project scaffold + macOS/iPad build + test framework | Pending |
| US-002 | Soccer pitch with boundaries | Pending |
| US-003 | Ball physics | Pending |
| US-004 | Single player controller | Pending |
| US-005 | Goal detection system | Pending |
| US-006 | Broadcast camera (Cinemachine) | Pending |
| US-007 | Match timer + score tracking | Pending |
| US-008 | Basic HUD (score + timer) | Pending |

**Checkpoint validation**:
```bash
# macOS build compiles
unity -batchmode -nographics -quit -projectPath ./OpenFifa -buildTarget StandaloneOSX

# iPad build compiles
unity -batchmode -nographics -quit -projectPath ./OpenFifa -buildTarget iOS

# All tests pass
unity -runTests -batchmode -nographics -testPlatform EditMode -testResults ./test-results/p1-editmode.xml
unity -runTests -batchmode -testPlatform PlayMode -testResults ./test-results/p1-playmode.xml

# Platform build verification
./scripts/verify-build.sh
```

---

## Phase 2: Core Gameplay

**Goal**: Two full AI teams play a complete match with formations, passing, shooting, goalkeeping, tackling, and proper match flow (kickoff through fulltime).

| Story | Title | Status |
|-------|-------|--------|
| US-009 | Team formation system (2-1-2) | Pending |
| US-010 | AI player FSM (idle, chase, return) | Pending |
| US-011 | AI passing | Pending |
| US-012 | AI shooting | Pending |
| US-013 | Goalkeeper AI | Pending |
| US-014 | Match state machine (full flow) | Pending |
| US-015 | Kickoff sequence | Pending |
| US-016 | Player switching | Pending |
| US-017 | Tackle mechanic | Pending |
| US-018 | Ball ownership system | Pending |

**Checkpoint validation**:
```bash
# Fast-forward match simulation (10 matches, no crashes)
unity -runTests -batchmode -testCategory "MatchSimulation" -testResults ./test-results/p2-simulation.xml

# Full test suite
unity -runTests -batchmode -nographics -testPlatform EditMode
unity -runTests -batchmode -testPlatform PlayMode
```

---

## Phase 3: Game Feel

**Goal**: The game looks and sounds like a soccer game. Players animate, the ball has trails, goals trigger celebrations with slowmo and camera effects, crowd reacts.

| Story | Title | Status |
|-------|-------|--------|
| US-019 | Player animation state machine | Pending |
| US-020 | Kick animation + force sync | Pending |
| US-021 | Goal celebration sequence | Pending |
| US-022 | Ball trail particles | Pending |
| US-023 | Sound effects (whistle, kick, crowd, goal) | Pending |
| US-024 | Camera shake on goal | Pending |
| US-025 | Crowd reaction audio | Pending |
| US-026 | Player run dust particles | Pending |
| US-027 | Replay system (5-second goal replay) | Pending |

**Checkpoint validation**:
```bash
# All animation states verified
unity -runTests -batchmode -testCategory "Animation" -testResults ./test-results/p3-animation.xml

# Audio triggers verified
unity -runTests -batchmode -testCategory "Audio" -testResults ./test-results/p3-audio.xml
```

---

## Phase 4: UI & Menus

**Goal**: Complete user journey — main menu through post-match results. Settings persist. Scenes transition smoothly.

| Story | Title | Status |
|-------|-------|--------|
| US-028 | Main menu scene | Pending |
| US-029 | Team selection screen | Pending |
| US-030 | Match HUD (full) | Pending |
| US-031 | Pause menu | Pending |
| US-032 | Post-match results screen | Pending |
| US-033 | Settings screen | Pending |
| US-034 | Scene transition system | Pending |
| US-035 | E2E user journey test | Pending |

**Checkpoint validation**:
```bash
# End-to-end navigation test
unity -runTests -batchmode -testCategory "E2E" -testResults ./test-results/p4-e2e.xml
```

---

## Phase 5: Polish & Platform

**Goal**: Keyboard/mouse controls work on macOS, touch controls work on iPad, performance hits budgets, app builds for both platforms without issues.

| Story | Title | Status |
|-------|-------|--------|
| US-036 | Keyboard/mouse controls (macOS) + virtual joystick (iPad) | Pending |
| US-037 | Keyboard shortcuts (macOS) + touch buttons (iPad) | Pending |
| US-038 | Haptic feedback (iPad) + screen shake/audio feedback (macOS) | Pending |
| US-039 | Draw call optimization | Pending |
| US-040 | GC-free gameplay loop | Pending |
| US-041 | Multi-resolution UI (macOS windows + iPad sizes) | Pending |
| US-042 | macOS + iPad build hardening | Pending |
| US-043 | Performance budget test suite | Pending |

**Checkpoint validation**:
```bash
# Performance tests
unity -runTests -batchmode -testCategory "Performance" -testResults ./test-results/p5-perf.xml

# Platform build verification
./scripts/verify-build.sh
```

---

## Phase 6: Asset Integration & Advanced

**Goal**: Replace placeholders with real assets. Establish visual baselines. Add local multiplayer.

| Story | Title | Status |
|-------|-------|--------|
| US-044 | Import Quaternius characters + team colors | Pending |
| US-045 | Mixamo soccer animations | Pending |
| US-046 | Stadium environment | Pending |
| US-047 | Soccer ball model + PBR | Pending |
| US-048 | Audio integration (real sounds) | Pending |
| US-049 | Visual regression baselines | Pending |
| US-050 | Local multiplayer (same device) | Pending |

**Checkpoint validation**:
```bash
# Visual regression
unity -runTests -batchmode -testCategory "VisualRegression" -testResults ./test-results/p6-visual.xml

# Asset integrity (no missing scripts, broken shaders, missing clips)
unity -runTests -batchmode -nographics -testCategory "AssetIntegrity" -testResults ./test-results/p6-assets.xml

# Full suite (everything still works)
unity -runTests -batchmode -nographics -testPlatform EditMode
unity -runTests -batchmode -testPlatform PlayMode
```
