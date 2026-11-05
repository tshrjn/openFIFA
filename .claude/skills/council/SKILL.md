---
name: council
description: Use when the user says "council", or asks for a three-provider review of an openFIFA PR or change. Runs Claude Opus 5.5, GPT-6.1 Sol and Gemini 4 Argon as read-only reviewers at high effort, scores code and gameplay areas out of 10 each round, and loops until all three approve.
metadata:
  harness: [claude]
  platform: [linux]
  requires: "claude (Anthropic), codex logged in to ChatGPT (OpenAI), agy logged in to Antigravity (Google), gh, jq"
---

# Council

Adapted from Theo's `council` skill (t3dotgg/tstack). Bring in three reviewers from three different providers to
review a change, then fix what they agree on and run them again until all three are satisfied.

| Delegate | Provider | CLI | Effort |
| --- | --- | --- | --- |
| Claude Opus 5.5 | Anthropic | `claude -p --model claude-opus-5-5` | `xhigh` |
| GPT-6.1 Sol | OpenAI | `codex exec -m gpt-6.1-sol` (ChatGPT login) | `xhigh` |
| Gemini 4 Argon | Google | `agy --model gemini-4-argon-high` (Antigravity login) | `high`, the highest it offers |

If the user names other members, use those. If a delegate can't be reached (not logged in, model gone, CLI missing),
**report it and stop. Do not substitute** another model or provider. Tell the user what to provide, for example
`codex login`, `agy` sign-in, or an API key.

## Run a round

```bash
.claude/skills/council/council.sh <pr-number> [--evidence DIR]... [--brief FILE]
# or a local range: council.sh origin/main..HEAD
```

The script snapshots the PR head into a throwaway, write-protected clone and runs the three reviewers in parallel
(about 10-45 minutes). Each one scores against `rubric.md`. It writes
`~/GameDev/review/council/pr-<N>/round-<k>/` (prompt, diff, raw output, `<delegate>.json`, `summary.txt`) and
rebuilds `~/GameDev/review/council/pr-<N>/council-table.md`. It exits 0 only when all three return `approve`.
`--only gpt` reruns one reviewer in the latest round, for example after a timeout.

The reviewers are read-only. Claude is limited to Read, Grep, Glob and `git diff/log/show`. Gemini runs in plan mode
with an allow-list of read commands, and anything else is denied. A denied call ends an Antigravity headless run with
no output, so the script retries once and tells it to run no commands. Codex can't start its own sandbox on CodeBox2
(bwrap: `loopback: Failed RTM_NEWADDR`), so it stays in `read-only` mode with no working shell. For both GPT and
Gemini, the script inlines the full diff, the evidence text and every changed file into `prompt-inline.md`. Do not
loosen any of this to `danger-full-access` or `--dangerously-skip-permissions`.

## What the council judges

The rubric scores 11 areas. The first nine are the ones the owner cares about most: team tactics, team movement,
player movement, animation and skill moves, ball physics, commentary, kits and likeness, Premier League authenticity
and difficulty. The other two are presentation and code. Reviewers score only the areas the change touches or the
evidence shows, and leave the rest `null`. The overall score is capped at the lowest priority area + 1.

Reviewers can't run the game, so **you gather the evidence** before round 1 and pass it with `--evidence`. Match it
to the areas the PR touches:

| Area | Evidence to produce |
| --- | --- |
| Any code | test run output (`tools/csbuild/run_pure_tests.sh`, or Unity EditMode/PlayMode results) saved as a `.log` |
| team_tactics, team_movement, player_movement | AI-vs-AI or bot match stats in `tactics.txt`: shape, line height and width, PPDA, fouls, possession, off-ball speed mix, run counts. Plus a capture: `scripts/capture-gameplay.sh <dir> 40` |
| animation_skills, kits_likeness, presentation | capture stills and `frames/`; for animation, consecutive-frame strips in `sequences/` |
| ball_physics | ball-physics test log, plus 10 fps `sequences/` around passes, shots and bounces |
| commentary | `commentary.txt` (timestamped line + triggering event) from the capture or the commentary demo |
| league_authenticity | stills of the HUD, name panels and kits, plus the roster diff |
| difficulty | ladder test output (`Difficulty_AllSevenLevels...`) and keeper save rates, 144-240 seeds per level |

Keep evidence outside the repo, under `~/GameDev/review/<thread>/<YYYYMMDD-HHMM>-<sha>/`. If the PR claims a gameplay
improvement and there is no evidence for it, the reviewers will score that area `null` or flag the claim, so
produce the evidence instead of arguing.

## Loop

1. Round 1 is a full review. Read `summary.txt` and pull out the useful findings, where the reviewers disagree, and
   the next steps.
2. Sort every finding:
   - **Fix** in-scope `blocker`s, and in-scope `should`s you agree with. Make the smallest change that resolves each.
   - **Push back** on findings you disagree with. Put a one-line reason in the next `--brief` so the reviewers see
     it.
   - **Follow-up** for anything `in_scope: false`, or anything that grows the PR beyond its goal. List these under
     "Follow-ups" in the PR description and don't fix them here.
3. Commit and push the fixes, then run the next round. A fresh review after fixes is a new round. Later rounds check
   the open findings and the code changed since the last round only.
4. Stop when all three return `approve`.

**Fight scope creep.** Repeated reviews tend to grow a PR one "while you're here" at a time, and it is your job to
keep the scope limited. A new finding on code that was already reviewed and hasn't changed doesn't block. Neither does
a request for a feature the PR never claimed. Scores are not a reason to keep going either: a 7/10 `approve` on a
narrow fix is fine. The 9/10 bar belongs to the separate visual hill-climb, not to merging a PR.

**Budget.** The default is 3 rounds. Go to 4-5 only while an in-scope blocker with evidence stays open in balance or
difficulty ordering, save or roster data, multiplayer input, or a crash or perf regression, or when the user asks.
After that, stop and report the open disagreements to the user instead of looping.

## Report

If the work ends in a PR, edit the PR description (do not post a comment). Paste `council-table.md` as a
`## Council review` section placed before the final "Generated with" line. Then add a short "Follow-ups" list for
the out-of-scope findings. The table looks like this:

```markdown
## Council review

Rounds: 2

| Delegate | Round 1 | Round 2 |
| --- | --- | --- |
| Claude Opus 5.5 | 6/10 | 8/10 |
| GPT-6.1 Sol | 5/10 | 8/10 |
| Gemini 4 Argon | 7/10 | 8/10 |
```

If a delegate gave no score, write `no score`. Do not invent one. Use `gh pr edit <n> --body-file <file>`, and never
pipe empty stdin into a body edit. If there is no PR, put the same table in the final reply only.
