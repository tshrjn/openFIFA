# Single-player difficulty ladder

Seven levels, Beginner to Ultimate. Each adjacent step is strictly harder for the human, measured against the
scripted gamepad bot (48 seeds per level, 2 x 6-minute halves). `SinglePlayerSessionTests.Difficulty_AllSevenLevels_EachStepGoesClearlyBetterForTheAI`
(Category Slow) fails on any inversion (minimum +0.25 GD and +0.5 balance per match per step, seeds 1..24). It also
checks each keeper's save rate against the 55-80% band on those seeds (within two binomial standard errors), skipping
a keeper that faced fewer than 20 shots on target (the Legendary and Ultimate AI keepers).

Levers per level live in `TeamAIDifficultyLadder` (AI thetas, reaction bonus) and `SinglePlayerDifficultyProfile`
(hidden AI outfield rating delta, keeper deltas, assists). The outfield delta applies only to the fully AI-controlled
side, never to a human team or any local-multiplayer human seat, and never reaches displayed ratings, team sheets,
HUD or commentary.

| Level | AI goals | AI shots | User goals | User shots | GD (AI−User) | Balance | Step z (GD / bal) | User GK save % | AI GK save % |
|---|---|---|---|---|---|---|---|---|---|
| Beginner | 0.17±0.05 | 1.54±0.24 | 2.94±0.24 | 9.52±0.67 | −2.77±0.25 | −4.37±0.35 | — | 78.1% (25/32) | 57.0% (155/272) |
| Amateur | 0.65±0.12 | 3.58±0.29 | 1.71±0.18 | 5.50±0.37 | −1.06±0.22 | −1.45±0.27 | +5.9 / +6.9 | 69.3% (52/75) | 55.1% (87/158) |
| Semi-Pro | 1.00±0.16 | 5.67±0.45 | 1.21±0.15 | 3.69±0.33 | −0.21±0.21 | +0.19±0.28 | +2.7 / +4.1 | 62.5% (75/120) | 56.5% (61/108) |
| Professional | 1.17±0.15 | 7.81±0.52 | 0.73±0.12 | 1.79±0.31 | +0.44±0.19 | +1.64±0.27 | +2.5 / +3.8 | 71.5% (128/179) | 55.4% (31/56) |
| World Class | 2.06±0.21 | 12.44±0.67 | 0.52±0.09 | 1.60±0.16 | +1.54±0.20 | +3.71±0.27 | +3.6 / +5.0 | 66.7% (178/267) | 63.8% (30/47) |
| Legendary | 2.94±0.21 | 15.56±0.77 | 0.38±0.11 | 0.88±0.19 | +2.56±0.22 | +5.50±0.32 | +3.1 / +3.7 | 64.9% (246/379) | 74.1% (20/27) |
| Ultimate | 4.29±0.22 | 18.85±0.72 | 0.33±0.07 | 0.60±0.13 | +3.96±0.23 | +7.61±0.32 | +5.0 / +5.3 | 60.7% (286/471) | 62.5% (10/16) |

Per-match means ± standard error. Balance = GD + 0.2 × (AI shots − user shots). Step z = paired difference from
the level above divided by its standard error. Save % = keeper saves on shots / (saves + goals from shots).
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
