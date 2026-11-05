# Single-player difficulty ladder

Seven levels, Beginner to Ultimate. Each adjacent step is strictly harder for the human, measured against the
scripted gamepad bot (48 seeds per level, 2 x 6-minute halves). `SinglePlayerSessionTests.Difficulty_AllSevenLevels_EachStepGoesClearlyBetterForTheAI`
(Category Slow) fails when any step falls below its floor (seeds 49..120, see "Ladder test floors" below). It also
checks each keeper's save rate against the 55-80% band on those seeds, allowing min(1.5 binomial standard errors,
4 points) at the band edge, and skips a keeper that faced fewer than 40 shots on target (the Professional and higher
AI keepers, and the Beginner and Amateur user keepers). A 50% keeper fails even on ~120 shots.

## Ladder test floors

The ladder test plays seeds 49..120 (72 per level, 504 matches, 2 x 6-minute halves) against the scripted gamepad
bot. Each match is chaotic in its seed, so any sim or AI change reshuffles every match. The old fixed set of 24 seeds
flipped a different step red with each unrelated change. Each step now has its own floor: the baseline mean step on
these seeds minus 2 standard errors of the per-seed paired difference (same seed, the two adjacent levels), rounded
down and never below 0. A step that loses more than about 2 SE fails; noise within that does not.

Baseline: main at d762568 (after #41, player body contact). Per match, over the 72 seeds:

| Step | GD step (mean ± SE) | GD floor | Balance step (mean ± SE) | Balance floor |
|---|---|---|---|---|
| Beginner → Amateur | 1.375 ± 0.265 | 0.84 | 2.500 ± 0.331 | 1.83 |
| Amateur → Semi-Pro | 1.097 ± 0.223 | 0.65 | 1.956 ± 0.298 | 1.35 |
| Semi-Pro → Professional | 0.722 ± 0.233 | 0.25 | 1.733 ± 0.306 | 1.12 |
| Professional → World Class | 0.861 ± 0.237 | 0.38 | 1.653 ± 0.325 | 1.00 |
| World Class → Legendary | 1.431 ± 0.287 | 0.85 | 2.428 ± 0.390 | 1.64 |
| Legendary → Ultimate | 0.639 ± 0.300 | 0.03 | 0.842 ± 0.421 | 0.00 |

Level totals on these seeds (GD / balance): Beginner -172 / -282.8, Amateur -73 / -102.8, Semi-Pro 6 / 38.0,
Professional 58 / 162.8, World Class 120 / 281.8, Legendary 223 / 456.6, Ultimate 269 / 517.2.

Legendary → Ultimate is the thinnest step: 0.64 ± 0.30 GD per match, so its floor only rules out an inversion. That
was the same before #41 (about +0.9 balance per match on seeds 49..192). Widening it is a difficulty-tuning job, not
a test change.

When a deliberate difficulty change moves a step, re-measure on the same seeds and update this table and the
`StepGdFloor` / `StepBalanceFloor` arrays in `SinglePlayerSessionTests` together.


Levers per level live in `TeamAIDifficultyLadder` (AI thetas, reaction bonus) and `SinglePlayerDifficultyProfile`
(hidden AI outfield rating delta, hidden AI finishing delta, keeper deltas, assists). The outfield and finishing deltas
apply only to the fully AI-controlled side, never to a human team or any local-multiplayer human seat, and never reach
displayed ratings, team sheets, HUD or commentary.

| Lever | Beg | Ama | Semi | Pro | WC | Leg | Ult |
|---|---|---|---|---|---|---|---|
| AI keeper reflexes/diving | −6 | −5 | 0 | +5 | +5 | +5 | +5 |
| User keeper reflexes/diving | 0 | 0 | 0 | 0 | −3 | −6 | −9 |
| AI outfield ratings | −18 | −11 | −6 | 0 | +6 | +12 | +15 |
| AI finishing (Finishing, LongShots, Composure; on top of outfield) | −8 | −7 | −3 | −2 | 0 | 0 | 0 |

The user's own keeper is never weakened below World Class: the easy levels make the AI's finishing worse instead.
Every knob moves one way up the ladder (`DifficultyProfile_EveryLevel_UsesLadderThetasAndEveryKnobIsMonotonic`).

| Level | AI goals | AI shots | User goals | User shots | GD (AI−User) | Balance | Step z (GD / bal) | User GK save % | AI GK save % |
|---|---|---|---|---|---|---|---|---|---|
| Beginner | 0.19±0.06 | 0.96±0.15 | 2.67±0.22 | 8.58±0.59 | −2.48±0.22 | −4.00±0.31 | — | 62.5% (15/24) | 61.2% (139/227) |
| Amateur | 0.40±0.09 | 3.23±0.33 | 1.75±0.17 | 5.10±0.41 | −1.35±0.20 | −1.73±0.25 | +3.7 / +5.8 | 73.2% (41/56) | 61.0% (89/146) |
| Semi-Pro | 0.85±0.13 | 5.02±0.40 | 1.06±0.14 | 3.31±0.39 | −0.21±0.20 | +0.13±0.28 | +4.5 / +5.3 | 67.0% (67/100) | 63.2% (55/87) |
| Professional | 1.40±0.19 | 8.04±0.49 | 0.56±0.09 | 2.29±0.27 | +0.83±0.21 | +1.98±0.28 | +3.2 / +4.1 | 68.8% (119/173) | 72.7% (48/66) |
| World Class | 2.06±0.21 | 12.44±0.67 | 0.52±0.09 | 1.60±0.16 | +1.54±0.20 | +3.71±0.27 | +2.3 / +4.2 | 67.9% (178/262) | 69.8% (30/43) |
| Legendary | 2.94±0.21 | 15.56±0.77 | 0.38±0.11 | 0.88±0.19 | +2.56±0.22 | +5.50±0.32 | +3.1 / +3.7 | 66.3% (246/371) | 76.9% (20/26) |
| Ultimate | 4.29±0.22 | 18.85±0.72 | 0.33±0.07 | 0.60±0.13 | +3.96±0.23 | +7.61±0.32 | +5.0 / +5.3 | 62.7% (286/456) | 62.5% (10/16) |

Per-match means ± standard error. Balance = GD + 0.2 × (AI shots − user shots). Step z = paired difference from
the level above divided by its standard error. Save % = keeper saves on shots / (saves + goals from shots); a shot stops counting once it is blocked or deflected off any player, an outfielder takes control of it, or it is passed, intercepted, tackled, hits the woodwork or goes out (so a later save is never credited to it). Measured Nov 2026 after the keeper/finishing follow-up; World Class and above are unchanged from the previous retune (their levers did not move).
Tuned against a scripted bot only; needs human playtesting.

## The ladder with team styles

The AI side's style (`TeamAIStyle`, see docs/TEAM_TACTICS.md) shifts its overall strength. The manager's
`StyleExecution` scales every style with difficulty, and each style still has to keep the ladder ordered. We
checked this with the AI on each style: 24 seeds per level, 6-minute halves, the same scripted user. Every style
passes the ≥ +0.25 GD per match per level rule. The table is total AI goal difference over the 24 matches
(fidelity-core 0643029 plus PR #34):

| Style | Beginner | Amateur | Semi-Pro | Professional | World Class | Legendary | Ultimate |
|---|---|---|---|---|---|---|---|
| Balanced | −68 | −31 | −1 | +20 | +35 | +65 | +94 |
| Possession | −55 | −15 | +6 | +16 | +47 | +64 | +93 |
| HighPress | −59 | −17 | +16 | +23 | +85 | +92 | +130 |
| CounterAttack | −68 | −22 | +1 | +27 | +49 | +71 | +118 |
| LongBall | −64 | −30 | −8 | +16 | +69 | +85 | +124 |
| WingPlay | −59 | −11 | −1 | +12 | +43 | +88 | +104 |
| ParkTheBus | −69 | −32 | −5 | +10 | +23 | +40 | +86 |

Two steps sit just under the stricter +1.0 balance per match rule that `Difficulty_AllSevenLevels` applies to
default tactics. Both are within seed noise, so we left them:

- Possession, Semi-Pro → Professional: +23.6 over 24 matches.
- WingPlay, Legendary → Ultimate: +20.6 over 24 matches.

Re-run this check whenever a style preset or `TeamAIManager.ForDifficulty` changes.
