# OpenFifa controls

OpenFifa has two control schemes: a console-style gamepad layout and an iPad touch layout. Both produce the same `ControlIntent` (in `OpenFifa/Assets/Scripts/Core/Controls/`), which the match code maps to the sim's player intent. The bindings live in `OpenFifa/Assets/Settings/Input/OpenFifaControls.inputactions`.

The gamepad layout follows the functional conventions of the common console football layout. Those conventions are documented in `docs/research/research_notes/Football game fidelity research/commentary_controls.md`. Code and UI use generic names (South/East/West/North, Shoulder, Trigger) and show platform glyphs at runtime.

## Gamepad: Classic preset (default)

| Button (generic) | Xbox | DualSense | Attack | Defence |
|---|---|---|---|---|
| Left stick | LS | L | Move | Move |
| South | A | Cross | Pass (hold to charge) | Hold: contain |
| West | X | Square | Lob; cross in a crossing zone | Slide tackle |
| North | Y | Triangle | Through ball | — |
| East | B | Circle | Shoot (hold to charge) | Tackle |
| Right trigger | RT | R2 | Sprint | Sprint |
| Left trigger | LT | L2 | Shield | Jockey |
| Left shoulder | LB | L1 | Modifier | Switch player (smart) |
| Right shoulder | RB | R1 | Modifier | Hold: teammate press |
| Right stick flick | RS | R | Skill move (8 directions) | Switch player in the flick direction |
| D-pad | D-pad | D-pad | Tactics (see below) | Tactics |

### Modifier combos (attack)

| Input | Xbox | DualSense | Result |
|---|---|---|---|
| Shoot | B | Circle | Shot |
| Shoot twice (second press before contact) | B, B | Circle, Circle | Low shot |
| LB + Shoot | LB + B | L1 + Circle | Chip shot |
| RB + Shoot | RB + B | R1 + Circle | Finesse shot |
| LB + RB + Shoot | LB + RB + B | L1 + R1 + Circle | Driven (power) shot |
| RB + Pass | RB + A | R1 + Cross | Driven pass |
| LB + Through | LB + Y | L1 + Triangle | Lobbed through ball |
| LB + RB + Through | LB + RB + Y | L1 + R1 + Triangle | Driven through ball |
| LB + RB + Lob | LB + RB + X | L1 + R1 + Square | Driven lob / cross |

A modifier counts if it is held at any point while the kick button is down.

### Alternate preset

This preset puts Shoot on West (X / Square) and Lob/Cross on East (B / Circle). It uses different shot modifiers:

| Input | Result |
|---|---|
| LB + Shoot | Finesse shot |
| RB + Shoot | Driven shot |
| LB + RB + Shoot | Chip shot |

Defence follows the roles in both presets: the Shoot button tackles and the Lob button slides.

### D-pad tactics (press two in a row within 1 s)

| Sequence | Command |
|---|---|
| Up, Up | Offside trap |
| Up, Left | Team press |
| Up, Right | Extra striker |
| Up, Down | Get in the box |
| Right, Left | Defensive mentality |
| Right, Up | Balanced mentality |
| Right, Right | Attacking mentality |
| Down, Down | Substitution |

### Feel

| Setting | Default | Meaning |
|---|---|---|
| Pass full charge | 0.8 s | Hold time for a full pass power bar |
| Shot full charge | 1.0 s | Hold time for a full shot power bar |
| Power curve | `min + (1 - min) * (1 - (1 - x)^2)`, min 0.1 | Rises quickly, then flattens near full |
| Kick buffer | 0.5 s | A kick released during the wind-up fires on the contact frame. Losing the ball drops it. |
| Double tap | 0.25 s | Window for the low-shot second press |
| Stick dead zone | 0.15 radial, rescaled | |
| Flick | ≥ 0.75 from rest < 0.3 | Right-stick flick threshold |
| Assist level | 0.6 | 0 = manual aim, 1 = full assist. Widens the pass cone from 10° to 75°, snaps shots inside the posts and blends pass power to distance. |

## iPad touch

The left side has a **floating stick**: it appears where the thumb lands in the left 40% of the screen (bottom 80%) and follows the thumb past the rim. The right side has **four context buttons**. Each button keeps the meaning it had when the finger went down. If possession changes before the finger lifts, the press is ignored.

| Button | Attack | Defence |
|---|---|---|
| Primary | **Pass**: tap = pass, hold = more power, swipe up = lob (cross in a crossing zone) | **Switch**: tap = smart switch, swipe = switch in that direction |
| Strike | **Shoot**: hold to charge, swipe up = chip, swipe left/right = finesse, swipe down = low shot | **Tackle**: tap = tackle, swipe = slide tackle in that direction |
| Through | **Through**: tap = through ball, swipe up = lobbed through | **Press**: hold = contain, swipe up and hold = teammate press, swipe down and hold = jockey |
| Sprint | Sprint; tap during a kick's charge = driven (see below) | Sprint |

**Driven kicks.** Press a kick button first, then press Sprint while it is still down; release the kick button as usual. Pass becomes a driven pass, Shoot a driven shot and Through a driven through ball (tap kicks only; swipes keep their own meaning). Sprint has to go down at least 0.05 s after the kick button (`TouchControlScheme.DrivenSprintMinDelaySeconds`), so these do not count:

- holding Sprint while running and then pressing a kick button: normal kick;
- pressing Sprint and the kick button together (within 0.05 s): normal kick.

Lifting Sprint again before releasing the kick keeps it driven, so a quick tap on Sprint during the charge is enough.

Kicks charge while the finger is down and fire on release. They go through the same contact-frame buffer as the gamepad. An optional setting turns on auto-sprint when the stick is pushed to the rim. The left-handed setting mirrors the layout.

### Layout data

Positions are normalised (x = fraction of width, y = fraction of height, origin bottom-left). Radii are fractions of screen height, so a button is the same physical size on every iPad. The cluster keeps the same physical distance from the right edge.

| Button | 4:3 centre | 3:2 centre | Radius (× height) |
|---|---|---|---|
| Primary (Pass / Switch) | (0.760, 0.12) | (0.787, 0.12) | 0.070 |
| Strike (Shoot / Tackle) | (0.880, 0.22) | (0.893, 0.22) | 0.085 |
| Through (Through / Press) | (0.902, 0.43) | (0.913, 0.43) | 0.065 |
| Sprint | (0.722, 0.31) | (0.753, 0.31) | 0.065 |

| Parameter | Value |
|---|---|
| Stick spawn zone | x 0–0.40, y 0–0.80 |
| Stick radius | 0.11 × height |
| Stick dead zone | 12% of the stick radius (rescaled) |
| Button hit area | 1.15 × visual radius |
| Swipe threshold | 0.05 × height of finger travel |

`TouchLayout.ForAspect(aspect)` builds the layout for any landscape aspect between these two.

## Input actions asset

`OpenFifaControls.inputactions` has two control schemes, **Gamepad** (`<Gamepad>`) and **Touch** (`<Touchscreen>`):

- **Gameplay map (Gamepad):** Move, RightStick, Sprint, JockeyShield, ModifierLeft, ModifierRight, Pass, LobCross, Through, Shoot, TacticsUp/Down/Left/Right, LeftStickPress, RightStickPress and Pause, bound to the Classic preset.
- **Touch map:** Touch0–4 Position and Press, read into `TouchPoint`s using the slot index as the finger id.

Glue code builds a `GamepadFrame` from the actions with `ControlPreset.ButtonOf(role)`, so rebinding an action in the Input System carries through without code changes.
