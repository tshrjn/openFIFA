# Local multiplayer (two controllers)

Local play for up to four controllers on one screen. The main target is two players: head-to-head, or co-op against the AI. Each human controls one player at a time. The team AI plays every other player, and the humans steer their AI teammates with calls.

## Modes

Controllers join in the middle of the side-select screen. Push the left stick left for Home or right for Away. Each side holds at most two humans. Controllers left in the middle watch the match.

| Humans per side | Mode |
|---|---|
| 1 on one side, 0 on the other | Solo vs AI |
| 1 and 1 | Head-to-head |
| 2 on one side, 0 on the other | Co-op vs AI |
| 2 and 1, or 2 and 2 | Co-op head-to-head |

If a controller drops out during a match, its seat keeps its side, its slot colour and its player, and the match pauses. The next controller that connects takes over the seat. Code: `Core/LocalMultiplayer/ControllerAssignment.cs`.

## Calls to AI teammates

These sit on top of the single-player controls in `docs/CONTROLS.md` and don't change them.

| When | Input (generic / Xbox / DualSense) | Call | What the AI does |
|---|---|---|---|
| Attack, a teammate has the ball, you are off it | Pass: South / A / Cross | Call for the ball | The carrier strongly prefers a pass to your feet. It won't play it into a blocked lane. |
| Same | Through: North / Y / Triangle | Call for it in behind | It prefers a through ball into the space ahead of you (any position, not just forwards). |
| Attack, any time | Tap LB / L1 on its own | Player run | The best-placed AI attacker makes a run in behind the line. |
| Attack, any time | Hold LB / L1 + flick right stick | Directed run | An AI teammate sprints 18 m in the flicked direction, from your player. |
| Defence | Hold RB / R1 | Teammate press | The AI teammate who can get to the ball first presses the carrier, as a second man if you are the first. |

Calls expire (pass 1.6 s, run 2.5 s, press 0.15 s after RB is released), so a stale call never sticks. A pass call clears when the caller receives the ball or the team loses it.

Input details (`TeammateCallResolver`):
- Pass and through presses used as calls are hidden from the kick resolver. You don't get an accidental first-time pass when the ball arrives. While the ball is in flight to you (nobody owns it), those buttons are first-time kicks as normal.
- LB is still the kick modifier. Any face button or kick charge during the hold cancels the run tap. While LB is held, the right stick is hidden from the skill-move resolver.

## Co-op switching

Each human seat has its own `HumanPlayerSelector`. The session passes every selector the other seats' players as `otherHumans`, and a selector never switches onto one of them. With two seats on one team:
- Only the passer follows his own pass. If your partner passes to an AI teammate, you stay on your player.
- When an AI teammate wins the ball, the nearer human takes him.
- Manual and directional switches skip your partner's player.

## Team AI changes

- `TeamAIPlayer.HumanControlled` players get no AI command. They are never picked as supporters or directed runners. A human can still be the first presser or the ball chaser, so the AI doesn't double up unless asked.
- `TeamAIWorld.Directives[team]` (`TeamAIUserDirectives`) carries the calls. `TeamAIBrain` reads them:
  - A pass call adds `PassCallBonus` (0.45) to passes toward the caller in `TeamAIOnBall`, after technique scaling. Interception risk still costs up to 0.9, so the AI won't pass into a blocked lane.
  - A run request sets `DirectedRunnerIndex`. That player sprints to the target, or in behind the offside line when there is no target.
  - A press request sets `RequestedPresserIndex`. That player closes down like a first presser even in a low block.

## Playing it

Open `Assets/Scenes/LocalMultiplayerMatch.unity` (the single-player scene plus `LocalMultiplayerMatch`). Every connected pad shows up in the middle of the side-select screen. Push left or right, then press A / Cross to kick off. Markers in each seat's colour (P1 blue, P2 red, P3 green, P4 yellow) float over the controlled players. If a pad drops out, the match pauses with a prompt, and A / Cross on any pad takes the empty seat.

Command line: `-lmBots` drives both seats with `ScriptedGamepadBot` (for captures), `-lmMode coop|h2h` sets the mode, and `-lmAutoStart` skips side select.

## Code

- `LocalMatch` seats every controller in a `SinglePlayerMatchSession`. Seat 1's side is the session's `HumanTeam`, and the other seats are added with `AddHumanSlot`. In head-to-head, both teams' AI teammates play at the level's teammate strength and both keepers keep their real ratings. A dropped pad hands its player to the AI and pauses the match.
- `LocalGamepadInput` is one seat's `IHumanInputSource`: raw frame → `TeammateCallResolver` (writes `World.Directives`) → `GamepadControlResolver`.
- With auto-switch on (every level today), a human almost always holds the team's ball carrier. So "call for the ball" mostly signals your co-op partner, which the HUD can show. The AI-carrier path is there for a future player-lock option.

Tests: `Tests/Editor/LocalMultiplayer/` (category `LOCAL_2P`).
