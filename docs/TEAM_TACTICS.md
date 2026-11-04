# Team tactics: formations, styles and the AI manager

The team AI (`Scripts/Core/TeamAI/`) plays from a `TeamAITactics` instance per side. This page covers the strategy layer on top of the phase machine and the per-player utility decisions.

## Formations

`TeamAIFormation` has eight templates (slot 0 is always the keeper):

| Enum | Shape | Notes |
|------|-------|-------|
| `F433` | 4-3-3 | Out of possession, the wide forwards drop into the midfield line in a mid or low block, so the team defends as a 4-5-1. |
| `F4231` | 4-2-3-1 | |
| `F442` | 4-4-2 | |
| `F352` | 3-5-2 | The wing-backs drop into a back five out of possession. |
| `F4141` | 4-1-4-1 | Roster "4-5-1" maps here. |
| `F41212` | 4-1-2-1-2 (diamond) | No wingers. The full-backs give the width. |
| `F343` | 3-4-3 | Back five out of possession, front three high when pressing. |
| `F532` | 5-3-2 | Roster "5-4-1" maps here. |

Kick-off spots are generated from the template (`RosterMatchBuilder.KickOffSpot`), and `SimFormationMapper.BestFit` considers all eight.

## Style sliders

Every slider runs from 0 to 1, and 0.5 is neutral. A default `new TeamAITactics()` plays exactly as the AI did before styles existed.

| Field | What it changes |
|-------|-----------------|
| `Width` | How wide the block spans. |
| `Depth` | Back-line height (±0.1 of the pitch). |
| `PressingIntensity` | When the team enters the high press, how long it counter-presses, and how many pressers go. |
| `Directness` | On-ball pass scoring. Forward progress is weighted ×0.75 to ×1.25 and interception risk ×1.2 to ×0.8, and lofted balls forward become more attractive. Below 0.4, the lone holding midfielder drops between split centre-backs in build-up, provided the side plays a back two. |
| `ForwardRuns` | Players allowed to run in behind at once (1, 2 or 3). At 0.7 and above, central midfielders make late runs into the final third. Full-backs only overlap at 0.25 and above, and from deeper at 0.75 and above. It also sets how far the wide forwards track back. |
| `CounterAttack` | Above 0.5, the team breaks for up to `TeamAIBrain.CounterWindow` (6 s) after winning the ball in open play. During the break, forwards start runs from up to 12% of the pitch behind the offside line, and the on-ball directness rises by up to 0.4. |

`TeamAITactics.FromStyle(TeamAIStyle, formation)` gives the named presets: Balanced, Possession, HighPress (gegenpress), CounterAttack, LongBall, WingPlay and ParkTheBus. `TryParseStyle` accepts names such as "park the bus", "high-press" and "gegenpress".

## Mentality and the AI manager

`TeamAIMentality` runs from -2 (UltraDefensive) to +2 (AllOutAttack). Each step moves line height and pressing by 0.12, forward runs by 0.15 and width by 0.05. At ±2 the team also goes more direct. `TeamAIManager.Apply` combines a base tactics object with the manager's extra mentality, and the brain plays the result, `TeamAIBrain.Effective`. `TeamAIBrain.Tactics` remains the user's or the roster's instructions.

`TeamAIManager.Choose(goalDiff, progress, playerDiff)` picks the mentality:

- **Trailing:** Attacking after 55% of the match, or straight away when two goals down. AllOutAttack after 85%, or after 60% when two or more down.
- **Leading by one:** Defensive after 65%, UltraDefensive after 85%.
- **Leading by two or more:** Defensive after 75%.
- **Numbers:** a man down is one step more defensive, and a man up while not winning is one step more attacking.

A change goes in at the next stoppage, or after it has been wanted for `ChangeDelay` (20 s) of open play. `SinglePlayerMatchSession` turns the manager on for the AI side only, so the human's AI team-mates keep the user's tactics.

`TeamAIWorld.Score` and `TeamAIWorld.MatchProgress` (0 at kick-off, 0.5 at half-time, 1 at full time) are filled by `SimTeamAIAdapter.Fill`.

## Rosters

A club can name a style in the roster JSON with an optional `"style"` key, for example `"style": "counter"`. The validator rejects names it does not know. Without a style, `RosterMatchBuilder.TacticsFor` keeps the reputation-based sliders.
