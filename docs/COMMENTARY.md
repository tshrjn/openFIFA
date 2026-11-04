# OpenFifa commentary

The commentary system stitches pre-rendered clips together and tags each line by event, following the approach
described in `docs/research/research_notes/Football game fidelity research/commentary_controls.md`.
The code is pure C# in `OpenFifa/Assets/Scripts/Core/Commentary/` and does not use UnityEngine. The line bank is
`OpenFifa/Assets/StreamingAssets/Commentary/lines.en.json`.

## Runtime flow

1. Gameplay turns sim events into a `CommentaryEvent` and calls `engine.Push(e)`. The event carries its type,
   time, minute, team, player name and id, the score after the event, shot quality, distance, goal situation,
   pass count and crowd intensity.
2. Gameplay calls `engine.Tick(now)` every frame.
3. Both calls return `CommentaryCue`s. On `Start`, play the utterance's `Clips` in order on that voice's channel,
   with each clip's audio file named by `ClipId`. On `Stop`, fade that voice out quickly. The audio layer can call
   `NotifyFinished(voice, now)` when the real audio ends.

## Rules (CommentaryEngine)

| Rule | Behaviour |
| --- | --- |
| Category choice | Context first, in the order `late_winner` > `comeback` > `equaliser` > `after_goal` > `second_half`. Then the goal `situation` (`header`, `long_range`, `penalty`, `free_kick`, `counter`, `own_goal`, `tap_in`). Then the generic category. |
| Priority / interruption | When the lead voice is idle, the line starts at once. A line with strictly higher priority cuts off the current line, but only if the current line is `interruptible`. Otherwise one pending line is kept, the highest priority with newest winning ties, and it is dropped once it is stale: after 2 s, or 4.5 s for resolution or dead-ball lines. |
| Crowd gating | Lines that are not resolution lines are skipped when crowd intensity is above the category's `maxCrowd`. |
| Cooldown | A category is not reused within `cooldown` seconds. |
| No-repeat memory | A line is not reused within 15 minutes while an alternative fits. After that, the least recently used line is picked. The seeded xorshift RNG chooses among fresh lines, so the same seed and the same events give the same commentary. |
| Two voices | The play-by-play (`pbp`) voice reacts to everything. The analyst speaks only during stoppages (goal, wide, foul, card, corner, offside, half time, full time), and only after the lead voice has been silent for 0.6 s. It speaks at most once every 15 s, and only with probability `AnalystChance`. A live event such as kick-off, restart, pass, through ball or tackle cuts the analyst and clears its queue. A new lead line also stops the analyst. |
| Name call-outs | `{name}` at the start or end of a line becomes a separate name clip in the line's `nameTone` (`neutral`/`excited`/`question`), followed or preceded by the line clip. A line that needs a name is skipped when the event has none. Analyst lines cannot use names. |

## Line bank format

```json
{"format": "openfootball-commentary-lines", "schemaVersion": 1, "language": "en", "categories": [
  {"id": "shot.saved", "event": "ShotSaved", "voice": "pbp", "priority": 75, "interruptible": true,
   "resolution": true, "cooldown": 0, "maxCrowd": 1, "tone": "excited", "nameTone": "neutral",
   "context": null, "situation": null,
   "lines": ["Saved by the goalkeeper!", {"text": "What a stop!", "minQuality": 0.35}, "{name} goes for it, and it's saved!"]}
]}
```

- `format` must be `"openfootball-commentary-lines"`. Banks with the legacy tag `"openfifa-commentary-lines"` still load.
- A line's id is `<categoryId>.<NN>`, taken from its position in the list. You can override it with `"id"`.
  Add new lines at the end. Reordering changes the clip ids, and the clips then have to be rendered again.
- Optional conditions on a line: `minQuality`/`maxQuality` (shot quality 0-1), `minDistance`/`maxDistance`
  (metres), `minCrowd`, `minPassCount`, `result` (`draw`/`win`, for half-time and full-time lines), plus
  `tone` and `nameTone` overrides.
- `CommentaryLineBank.Load` rejects unknown events, voices, contexts and situations, duplicate ids, a `{name}` in
  the middle of a line, and analyst lines that use `{name}`.
- Every line is original text written for OpenFifa. Do not add real commentators' catchphrases.

## TTS pre-render manifest

`python3 tools/commentary/build_tts_manifest.py [--roster league.json ...] [--out ...] [--csv ...]` lists every
clip the engine can request: one `line.<id>` per bank line, and `name.<slug>.<intonation>` × 3 for every roster
`knownAs`. Each entry has the voice, the intonation and `ttsText`. Name clips end in `.`, `!` or `?` to steer the
intonation. The script calls no TTS API, and its output (under `build/`, which is gitignored) is deterministic.
For the bundled league that is 1,924 clips and about 28K characters. Render the clips later with ElevenLabs
or Kokoro using stock or licensed synthetic voices only. Never clone a real commentator, and disclose AI voices in
store metadata.
