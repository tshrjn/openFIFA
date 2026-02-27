# OpenFifa: Game Specification

> **This document is frozen.** It defines what we're building. Changes require explicit approval.
> Last updated: 2026-02-27

---

## Game Concept

**OpenFifa** is a **5v5 AAA arcade-style soccer game** for macOS (desktop). It targets premium-quality gameplay with fast, fun matches over simulation realism. Matches are short (6 minutes total), controls follow standard FIFA-style mappings (keyboard/mouse + Xbox-like gamepad), and the visual style is clean and readable.

The game is single-player vs AI for v1, with local multiplayer (keyboard + gamepad) as the final feature. **AAA Gameplay** is a core design goal — every interaction should feel polished, responsive, and premium.

---

## Target Platforms

| Platform | Priority | Notes |
|----------|----------|-------|
| macOS | Primary | macOS 14+ Sonoma, native .app bundle, keyboard/mouse + gamepad at 60fps |
| iPad | Deferred | Touch controls deferred to future release |
| Android | Future | Not in v1 scope |

---

## Goals

1. **AAA Gameplay** — Premium-quality feel in every interaction; a complete match should be polished and enjoyable
2. **Smooth performance** — 60fps on macOS and modern iPads, no frame hitches during gameplay
3. **Full match loop** — Main Menu → Team Select → Match → Results → Main Menu
4. **Test-driven quality** — Every feature has automated tests, zero human visual verification required
5. **Agent-buildable** — Every component can be built by an AI coding agent in one session
6. **Standard FIFA controls** — Keyboard/mouse and Xbox-like gamepad with FIFA-style button mapping

---

## Non-Goals (v1)

- Online multiplayer / matchmaking
- Commentary / play-by-play audio
- Real team/player licensing
- 11v11 full-size matches
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
| Min macOS Version | 14.0 (Sonoma) |
| Min iPadOS Version | 17.0 |
| Max Bundle Size | 200 MB |
| Target Frame Rate | 60fps target, 30fps minimum |
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

### Phase 6: Asset Integration (AAA-Quality Assets)
- Players: Professional high-fidelity humanoids (30K tri LOD0) with detailed team jerseys, numbers, and crests
- Ball: High-detail soccer ball model (5K tri) with 4K PBR textures
- Pitch: 4K textured with mowed-band grass pattern, corner flags, penalty areas
- Goals: 3D goal posts with net mesh and realistic materials
- Stadium: Full stadium with 8 crowd sections, floodlights, tunnels, advertising boards

### Phase 7: AAA Polish
- LOD system for characters (LOD0 30K, LOD1 5K, LOD2 1K) and stadium geometry
- Dynamic stadium lighting with floodlight towers and time-of-day presets
- Crowd animation system reacting to game events (goals, near-misses, fouls)
- Jersey customization with numbers, names, and team crests
- Weather and pitch particle effects (rain, snow, grass spray)
- Broadcast camera system with TV-style multi-angle replays

### Visual Principles
- Photorealistic quality with broadcast-camera clarity
- Distinct team colors (no ambiguity)
- AAA visual fidelity targeting EA FC / FIFA / PES standards
- LOD system maintains 60fps with high-fidelity assets

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

Standard FIFA-style controls supporting both keyboard/mouse and Xbox-like gamepad.

### Keyboard / Mouse

| Input | Action |
|-------|--------|
| WASD / Arrow keys | Move active player |
| Space | Pass (short pass to nearest teammate) |
| W (action) | Through ball |
| D / Left Click | Shoot toward goal |
| S | Slide tackle / pressure |
| Left Shift (hold) | Sprint (1.5x speed) |
| Q | Switch to nearest player to ball |
| E | Lob pass |
| Escape | Open pause menu |

### Xbox-like Gamepad

| Input | Action |
|-------|--------|
| Left Stick | Move active player |
| A button | Pass (short pass to nearest teammate) |
| Y button | Through ball |
| B button | Shoot toward goal |
| X button | Slide tackle / pressure |
| RT (hold) | Sprint (1.5x speed) |
| LB | Switch to nearest player to ball |
| RB | Lob pass |
| Start / Menu | Open pause menu |

### Local Multiplayer

| Player | Default Control Scheme |
|--------|----------------------|
| Player 1 | Keyboard / Mouse |
| Player 2 | Gamepad |

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
3. Keyboard/mouse and gamepad controls work with FIFA-style button mapping on macOS
4. Performance: 60fps sustained on macOS
5. Bundle size < 200MB
6. All automated tests pass (EditMode + PlayMode)
7. macOS .app bundle builds and runs without errors
8. Zero crashes during a 10-match fast-forward simulation
9. AAA gameplay feel — responsive controls, smooth animations, polished game feel with controller rumble feedback
