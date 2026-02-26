# OpenFifa: Game Specification

> **This document is frozen.** It defines what we're building. Changes require explicit approval.
> Last updated: 2026-02-27

---

## Game Concept

**OpenFifa** is a **5v5 arcade-style soccer game** for iPhone and iPad. It prioritizes fast, fun gameplay over simulation realism. Matches are short (6 minutes total), controls are simple (virtual joystick + action buttons), and the visual style is clean and readable.

The game is single-player vs AI for v1, with local multiplayer (same device) as the final feature.

---

## Target Platforms

| Platform | Priority | Notes |
|----------|----------|-------|
| iOS (iPhone) | Primary | iPhone 14+ at 60fps, iPhone SE at 30fps |
| iOS (iPad) | Primary | All iPads running iOS 16+ |
| macOS | Dev/Testing | Editor + Simulator testing |
| Android | Future | Not in v1 scope |

---

## Goals

1. **Fun arcade gameplay** — A complete match should be enjoyable for a casual player
2. **Smooth performance** — 30-60fps on iPhone 14+, no frame hitches during gameplay
3. **Full match loop** — Main Menu → Team Select → Match → Results → Main Menu
4. **Test-driven quality** — Every feature has automated tests, zero human visual verification required
5. **Agent-buildable** — Every component can be built by an AI coding agent in one session

---

## Non-Goals (v1)

- Online multiplayer / matchmaking
- Commentary / play-by-play audio
- Real team/player licensing
- 11v11 full-size matches
- MFi game controller support
- Career mode / season mode
- In-app purchases
- Leaderboards (Game Center stretch goal)

---

## Hard Constraints

| Constraint | Value |
|-----------|-------|
| Engine | Unity 6 LTS (2022.3+) |
| Render Pipeline | URP |
| Language | C# |
| Min iOS Version | 16.0 |
| Max Bundle Size | 200 MB |
| Target Frame Rate | 30fps minimum, 60fps target |
| Test Framework | NUnit 3 (Unity Test Framework) |
| Max Draw Batches | 100 |
| GC Allocations (gameplay) | 0 bytes/frame |

---

## Art Direction

### Phase 1-5: Placeholders
- Players: Unity Capsule primitives with team-colored materials
- Ball: Unity Sphere with white material
- Pitch: Unity Plane with green material + line markings
- Goals: Unity Cube primitives arranged as posts + crossbar
- Stadium: Poly Haven HDRI skybox only

### Phase 6: Asset Integration
- Players: Quaternius low-poly humanoids with team jerseys
- Ball: 3D soccer ball model with PBR material
- Pitch: Textured with mowed-band grass pattern
- Goals: 3D goal posts with net mesh
- Stadium: Basic stands geometry + HDRI lighting

### Visual Principles
- Clean, readable at a distance (important for broadcast camera)
- Distinct team colors (no ambiguity)
- Consistent low-poly stylized aesthetic
- No photorealism — clarity over fidelity

---

## Game Rules

| Rule | Value |
|------|-------|
| Players per team | 5 (1 GK + 4 outfield) |
| Half duration | 3 minutes (180 seconds) |
| Total match time | ~7 minutes (including halftime, celebrations) |
| Offsides | No |
| Fouls | Simplified (tackle from behind = free kick) |
| Cards | No |
| VAR | No |
| Extra time | No (draws are valid) |
| Substitutions | No |
| Corner kicks | Simplified (auto-placement) |
| Throw-ins | Simplified (auto-placement) |
| Goal kicks | Simplified (auto-placement) |
| Free kicks | Direct only, from foul position |

---

## Player Controls

### Touch (iOS — Primary)

| Input | Action |
|-------|--------|
| Left joystick (dynamic) | Move active player |
| Pass button | Short pass to nearest teammate |
| Shoot button | Shoot toward goal |
| Tackle button | Slide tackle / pressure |
| Sprint button (hold) | Run faster (1.5x speed) |
| Switch button (tap) | Switch to nearest player to ball |
| Pause button | Open pause menu |

### Keyboard (Development)

| Input | Action |
|-------|--------|
| WASD / Arrows | Move |
| Z | Pass |
| X | Shoot |
| C | Tackle |
| Left Shift (hold) | Sprint |
| Tab | Switch player |
| Escape | Pause |

---

## AI Behavior

### Architecture
- Finite State Machine (FSM) per player
- States: Idle, ChaseBall, ReturnToPosition, SupportAttack, Defend, Shoot, Pass
- Goalkeeper: separate FSM (Position, Dive, Distribute)

### Difficulty Scaling (via ScriptableObject)
| Parameter | Easy | Medium | Hard |
|-----------|------|--------|------|
| Reaction time | 0.8s | 0.4s | 0.1s |
| Pass accuracy | 60% | 80% | 95% |
| Shot accuracy | 40% | 65% | 85% |
| Decision quality | Random-biased | Balanced | Optimal |
| Sprint usage | 30% | 60% | 90% |

### Formation
- Default: 2-1-2 (2 defenders, 1 midfielder, 2 forwards)
- Positions defined as offsets relative to team half
- Players return to formation positions when ball is far

---

## Match Flow

```
MainMenu → TeamSelect → PreKickoff → FirstHalf → HalfTime → SecondHalf → FullTime → Results → MainMenu
                                          ↕                        ↕
                                    GoalCelebration          GoalCelebration
                                          ↕                        ↕
                                       Paused                   Paused
```

### State Transitions
- **PreKickoff**: Ball at center, players at formation positions, 1s countdown
- **FirstHalf**: Active gameplay, timer counting down from 3:00
- **GoalCelebration**: 2s slowmo, zoom to scorer, then reset to PreKickoff
- **HalfTime**: 2s display, teams swap sides
- **SecondHalf**: Same as FirstHalf
- **FullTime**: Whistle, display final score, transition to Results
- **Paused**: Time.timeScale = 0, overlay menu

---

## Completion Criteria

The game is v1-complete when:

1. Full match loop plays from Main Menu through Results and back
2. AI opponents make sensible decisions (pass, shoot, defend, goalkeeper dives)
3. Touch controls work on iOS device
4. Performance: 30fps sustained on iPhone 14 Simulator
5. Bundle size < 200MB
6. All automated tests pass (EditMode + PlayMode)
7. iOS Simulator build compiles without errors
8. Zero crashes during a 10-match fast-forward simulation
