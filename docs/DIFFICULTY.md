# Single-player difficulty ladder

Seven levels, Beginner to Ultimate. Each adjacent step is strictly harder for the human, measured against the
scripted gamepad bot (48 seeds per level, 2 x 6-minute halves). `SinglePlayerSessionTests.Difficulty_AllSevenLevels_EachStepGoesClearlyBetterForTheAI`
(Category Slow) fails on any inversion (minimum +0.25 GD and +0.5 balance per match per step, seeds 1..24).

Levers per level live in `TeamAIDifficultyLadder` (AI thetas, reaction bonus) and `SinglePlayerDifficultyProfile`
(hidden AI outfield rating delta, keeper deltas, assists). The outfield delta applies only to the fully AI-controlled
side, never to a human team or any local-multiplayer human seat, and never reaches displayed ratings, team sheets,
HUD or commentary.

| Level | AI goals | AI shots | User goals | User shots | GD (AI−User) | Balance | Step z (GD / bal) | User GK save % | AI GK save % |
|---|---|---|---|---|---|---|---|---|---|
| Beginner | 0.15±0.05 | 1.54±0.24 | 2.94±0.24 | 9.50±0.67 | −2.79±0.24 | −4.38±0.35 | — | 81.2% (26/32) | 57.0% (155/272) |
| Amateur | 0.62±0.12 | 3.54±0.29 | 1.73±0.18 | 5.42±0.37 | −1.10±0.22 | −1.48±0.27 | +5.9 / +6.9 | 69.4% (50/72) | 54.4% (86/158) |
| Semi-Pro | 1.00±0.16 | 5.62±0.45 | 1.23±0.16 | 3.75±0.33 | −0.23±0.21 | +0.15±0.29 | +2.8 / +4.1 | 62.2% (74/119) | 57.7% (64/111) |
| Professional | 1.15±0.14 | 8.21±0.57 | 0.75±0.12 | 1.92±0.31 | +0.40±0.19 | +1.65±0.28 | +2.5 / +3.9 | 72.9% (137/188) | 53.4% (31/58) |
| World Class | 1.92±0.21 | 12.27±0.65 | 0.56±0.09 | 1.48±0.13 | +1.35±0.20 | +3.51±0.26 | +3.4 / +4.7 | 68.2% (176/258) | 58.7% (27/46) |
| Legendary | 2.90±0.21 | 15.10±0.77 | 0.35±0.11 | 0.79±0.18 | +2.54±0.22 | +5.40±0.33 | +3.5 / +3.9 | 64.7% (240/371) | 76.9% (20/26) |
| Ultimate | 4.23±0.22 | 18.88±0.73 | 0.33±0.07 | 0.60±0.13 | +3.90±0.23 | +7.55±0.31 | +4.7 / +5.3 | 61.5% (291/473) | 66.7% (10/15) |

Per-match means ± standard error. Balance = GD + 0.2 × (AI shots − user shots). Step z = paired difference from
the level above divided by its standard error. Save % = keeper saves on shots / (saves + goals from shots).
Tuned against a scripted bot only; needs human playtesting.
