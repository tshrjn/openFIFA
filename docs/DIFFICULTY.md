# Single-player difficulty ladder

Seven levels, Beginner to Ultimate. Each adjacent step is strictly harder for the human, measured against the
scripted gamepad bot (48 seeds per level, 2 x 6-minute halves). `SinglePlayerSessionTests.Difficulty_AllSevenLevels_EachStepGoesClearlyBetterForTheAI`
(Category Slow) fails on any inversion (minimum +0.25 GD and +0.5 balance per match per step, seeds 1..24). It also
checks each keeper's save rate against the 55-80% band on those seeds, allowing min(1.5 binomial standard errors,
4 points) at the band edge, and skips a keeper that faced fewer than 40 shots on target (the Professional and higher
AI keepers, and the Beginner and Amateur user keepers). A 50% keeper fails even on ~120 shots.

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
