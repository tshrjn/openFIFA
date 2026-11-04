# OpenFootball commentary

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
4. In the game, `SinglePlayerMatchController` does all of this with `CommentaryAudioPlayer`
   (`Gameplay/SinglePlayer/`). It always reads `StreamingAssets/Commentary/audio/en/index.json` and hands it to the
   engine as its `Catalog`. With a real audio output (not batch mode, not `-spCapture`, not `-spNoCommentary`) it also
   loads clips, compressed and 8 at a time: this match's 22 player names and two clubs first, then kick-off, goal,
   shot and name-call lines, then the rest. It schedules name and line clips back to back on the DSP clock and fades
   on `Stop`. Name clips are unloaded when the match ends. Playback never calls `NotifyFinished`: line timing comes
   from the index, so a seed gives the same commentary (and the same capture `commentary.txt`) with or without
   sound. The controller feeds the crowd level to `SetCrowdIntensity` each frame, and a Stop cue clears the
   subtitle of the line it cut. With no audio bundled, commentary falls back to subtitles only.
5. Capture `commentary.txt` tags name calls `[call]` and logs a line cut off by a bigger moment a second time as
   `[cut]`.

## Rules (CommentaryEngine)

| Rule | Behaviour |
| --- | --- |
| Category choice | Context first, in the order `hat_trick` > `brace` > `late_winner` > `comeback` > `equaliser` > `early_goal` > `late_chance` > `after_goal` > `second_half` > `late_on` > `comfortable` > `tight` > `goalless`. Goal tallies per player (own goals excluded) drive `brace`/`hat_trick`; `late_chance` is a shot from minute 82 by a side level or one behind; the last four describe the scoreline in lulls. Then the goal `situation` (`header`, `long_range`, `penalty`, `free_kick`, `counter`, `own_goal`, `tap_in`). Then the generic category. |
| Priority / interruption | When the lead voice is idle, the line starts at once. A line with strictly higher priority cuts off the current line, but only if the current line is `interruptible`. Otherwise one pending line is kept, the highest priority with newest winning ties, and it is dropped once it is stale: after 2 s, or 4.5 s for resolution or dead-ball lines. |
| Crowd gating | Lines that are not resolution lines are skipped when crowd intensity is above the category's `maxCrowd`. |
| Cooldown | A category is not reused within `cooldown` seconds. |
| No-repeat memory | A line is not reused within 15 minutes while an alternative fits. After that, the least recently used line is picked. The seeded xorshift RNG chooses among fresh lines, so the same seed and the same events give the same commentary. |
| Two voices | The play-by-play (`pbp`) voice reacts to everything. The analyst speaks during stoppages (goal, wide, foul, card, corner, offside, half time, full time) after the lead voice has been silent for 0.6 s. In live play it may use only categories marked `"live": true` (reactions to saves and the woodwork, lull chatter), after 1 s of lead silence. It speaks at most once every 15 s, and only with probability `AnalystChance`. A live event such as kick-off, restart, pass, through ball, tackle or shot cuts the analyst and clears its queue. A new lead line also stops the analyst, except name calls (priority below `AnalystYieldPriority`), which are dropped instead. |
| Strike, then outcome | `ShotTaken` ("Okafor shoots!") is interruptible. The save, miss or goal cuts it, unless the strike call ends within `InterruptGrace` (0.45 s), in which case the outcome waits for it to finish. |
| Name calls | Every completed pass that is not a sequence milestone sends `OnTheBall` with the receiver. The `on_the_ball` category (cooldown 3.5 s) answers with bare name calls ("Okafor...") and short ones ("Into Okafor"), which carry the build-up the way TV commentary does. |
| Lulls | After `LullSeconds` (11 s) without speech in live play, the engine makes a `Lull` event with the current minute and score and gives it to the analyst `AnalystLullShare` of the time, otherwise to the lead voice. Never during stoppages. |
| Own goals | The `own_goal` situation category beats every context (equaliser, late winner, comeback...), and no line outside it may name the player, so a defender is never hailed as the scorer. A shot that goes out for a corner is called as a corner, not a miss. |
| Clip catalog | With `engine.Catalog` set (the rendered index), lines whose line or name clip is not rendered are skipped, so a player added after the last render gets nameless lines. Utterance end times come from the real clip lengths. |
| Name call-outs | `{name}` (player) or `{team}` (club) at the start or end of a line becomes a separate name clip in the line's `nameTone` (`neutral`/`excited`/`question`), followed or preceded by the line clip. A line that is only `{name}` is a bare name call (no line clip). A line that needs a name is skipped when the event has none. Analyst lines cannot use `{name}` (player names are rendered for the lead voice only), but can use `{team}`. |

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
- Category option `"live": true` (analyst only) lets the category speak in live play; see Two voices.
- Every line is original text written for OpenFootball. Do not add real commentators' catchphrases.

## TTS pre-render manifest

`python3 tools/commentary/build_tts_manifest.py [--roster league.json ...] [--out ...] [--csv ...]` lists every
clip the engine can request: one `line.<id>` per bank line, and `name.<slug>.<intonation>` × 3 for every roster
`knownAs`. Each entry has the voice, the intonation and `ttsText`. Name clips end in `.`, `!` or `?` to steer the
intonation. The script calls no TTS API, and its output (under `build/`, which is gitignored) is deterministic.
For the bundled league plus the Premier League pack that is 3,834 clips and about 52K characters. Render the clips later with ElevenLabs
or Kokoro using stock or licensed synthetic voices only. Never clone a real commentator, and disclose AI voices in
store metadata.

`build_tts_manifest.py` also lists `team.<slug>.<intonation>` for every club `name` (what the game speaks) in every bundled league and pack, in
both voices, and skips bare name calls (they play only the name clip).

## Voices and rendering

`tools/commentary/render_tts.py` renders the manifest with Kokoro-82M (Apache-2.0 open weights, run locally with
`kokoro-onnx`, no API). Each commentator is a weighted blend of Kokoro's stock British voices (lead: George + Fable,
co-commentator: Lewis + Daniel), so neither is a copy of one stock voice and neither imitates a real broadcaster.
Intonation is shaped after synthesis: `excited` is about 12% faster, about 1.2 semitones higher and 2.5 dB louder;
every clip is trimmed, high-passed at 90 Hz and levelled to -18 dBFS RMS. Output is 24 kHz mono Ogg Vorbis in
`StreamingAssets/Commentary/audio/en/{lead,analyst}/<clipId>.ogg`, plus `index.json` (length and render hash per
clip). Re-running only renders clips whose text, voice or processing changed, and deletes clips that left the bank.

```bash
python3 -m venv ~/GameDev/.tts/venv && ~/GameDev/.tts/venv/bin/pip install kokoro-onnx soundfile
# kokoro-v1.0.onnx + voices-v1.0.bin from github.com/thewh1teagle/kokoro-onnx releases, into ~/GameDev/.tts
~/GameDev/.tts/venv/bin/python tools/commentary/render_tts.py --jobs 6
```

Licence and disclosure: see `THIRD_PARTY_NOTICES.md` (Kokoro is Apache-2.0; the voices are AI-generated and must be
disclosed as such in store listings). Clip ids transliterate accents (`Ødegaard` -> `name.odegaard.*`) the same way
in `RosterSchema.Slug` and the manifest tool. The clips are about 30 MB of Ogg; if the repo grows uncomfortable,
move `StreamingAssets/Commentary/audio/**/*.ogg` to Git LFS (not set up on CodeBox2 yet).

Change a voice by editing `VOICES` in the script and re-rendering. A licensed studio voice or a better TTS can
replace Kokoro without code changes, as long as it writes the same files and index. Never use a cloned voice of a
real commentator.

## Hearing it without Unity

`tools/commentary/run_demo.sh [seed] [halfSeconds]` plays an AI v AI match headless with the real sim, adapter and
engine (compiled with Unity's bundled .NET), logs every cue, and mixes the clips over a synthetic crowd bed into
`build/commentary-demo/commentary_demo.ogg` with a timed transcript (`.txt`).
`tools/commentary/run_core_tests.sh` runs the commentary and integration EditMode tests the same way.
