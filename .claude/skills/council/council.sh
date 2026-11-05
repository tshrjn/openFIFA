#!/usr/bin/env bash
# One council round: three read-only reviewers from three providers score a PR (or a git range) in parallel.
#
# Usage: council.sh <pr-number | base..head> [--evidence DIR]... [--brief FILE] [--only claude|gpt|gemini]
#   --evidence DIR  captures/stats for the gameplay areas (frames/, sequences/, gameplay.webp, commentary.txt,
#                   tactics.txt, *.log). Repeatable. Images (up to 24) are attached where the CLI supports it.
#   --brief FILE    extra context for the reviewers (what the change is for, what to focus on).
#   --only NAME     rerun one reviewer in the latest round (e.g. after it timed out), keeping the other two.
# Output: $COUNCIL_OUT (default ~/GameDev/review/council/<pr-N|range>/round-<k>/) with
#   prompt.md, diff.patch, {claude,gpt,gemini}.{raw,err,json}, summary.txt; and council-table.md one level up.
# Exit 0 iff all three reviewers return verdict "approve".
set -uo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(git -C "$HERE" rev-parse --show-toplevel)"
TARGET="${1:?usage: council.sh <pr-number|base..head> [--evidence DIR]... [--brief FILE] [--only NAME]}"; shift
EVID=(); BRIEF=""; ONLY=""
while [ $# -gt 0 ]; do
  case "$1" in
    --evidence) EVID+=("$(cd "$2" && pwd)"); shift ;;
    --brief) BRIEF="$(cat "$2")"; shift ;;
    --only) ONLY="$2"; shift ;;
    *) echo "unknown option $1"; exit 2 ;;
  esac; shift
done
TIMEOUT="${COUNCIL_TIMEOUT:-2700}"   # seconds per reviewer

# Resolve base and head commits.
if [[ "$TARGET" =~ ^[0-9]+$ ]]; then
  KEY="pr-$TARGET"
  INFO="$(gh pr view "$TARGET" -R tshrjn/openFIFA --json title,body,baseRefName,headRefOid,url,mergeCommit)" || exit 2
  BASE_REF="$(jq -r .baseRefName <<<"$INFO")"; HEAD="$(jq -r .headRefOid <<<"$INFO")"
  git -C "$REPO" fetch -q origin "$BASE_REF" "pull/$TARGET/head" || exit 2
  BASE="$(git -C "$REPO" merge-base "origin/$BASE_REF" "$HEAD")"
  MERGED="$(jq -r '.mergeCommit.oid // empty' <<<"$INFO")"
  # Already merged: the head is an ancestor of the base branch, so diff against the merge commit's first parent.
  [ "$BASE" = "$HEAD" ] && [ -n "$MERGED" ] && BASE="$(git -C "$REPO" merge-base "$MERGED^1" "$HEAD")"
  CONTEXT="PR #$TARGET: $(jq -r .title <<<"$INFO")
$(jq -r .url <<<"$INFO")

PR description:
$(jq -r .body <<<"$INFO" | head -c 12000)"
else
  KEY="$(tr './' '__' <<<"$TARGET")"
  BASE="$(git -C "$REPO" rev-parse "${TARGET%%..*}")" && HEAD="$(git -C "$REPO" rev-parse "${TARGET##*..}")" || exit 2
  CONTEXT="Change $TARGET
$(git -C "$REPO" log --format='- %s' "$BASE..$HEAD" | head -40)"
fi

ROOT="${COUNCIL_OUT:-$HOME/GameDev/review/council}/$KEY"; mkdir -p "$ROOT"
LAST="$(ls -d "$ROOT"/round-* 2>/dev/null | sed 's/.*round-//' | sort -n | tail -1)"
if [ -n "$ONLY" ]; then N="${LAST:?no round to rerun}"; else N=$(( ${LAST:-0} + 1 )); fi
OUT="$ROOT/round-$N"; mkdir -p "$OUT"
PREV=""; [ "$N" -gt 1 ] && PREV="$ROOT/round-$((N-1))"

# Throwaway snapshot of the head commit, so reviewers never see (or touch) anyone's working checkout.
SNAP="$(mktemp -d "${TMPDIR:-/tmp}/council-XXXXXX")"
cleanup(){ chmod -R u+w "$SNAP" 2>/dev/null; rm -rf "$SNAP"; }; trap cleanup EXIT
git clone -q --shared --no-checkout "$REPO" "$SNAP/repo" && git -C "$SNAP/repo" checkout -q "$HEAD" || exit 2
git -C "$SNAP/repo" diff "$BASE" "$HEAD" > "$OUT/diff.patch"
[ -s "$OUT/diff.patch" ] || { echo "empty diff for $BASE..$HEAD"; rm -f "$OUT/diff.patch"; rmdir "$OUT" 2>/dev/null; exit 2; }
chmod -R a-w "$SNAP/repo"
echo "$BASE..$HEAD" > "$OUT/range.txt"

mapfile -t IMGS < <(for d in "${EVID[@]}"; do find "$d" -type f \( -name '*.png' -o -name '*.jpg' \) | sort; done | head -24)
mapfile -t TEXTS < <(for d in "${EVID[@]}"; do find "$d" -type f \( -name '*.txt' -o -name '*.log' -o -name '*.md' -o -name '*.json' \) | sort; done | head -40)

PREV_FINDINGS=""
if [ -n "$PREV" ]; then
  PREV_FINDINGS="$(python3 - "$PREV" <<'PY'
import json,os,sys
for n in ('claude','gpt','gemini'):
    try: d=json.load(open(os.path.join(sys.argv[1],n+'.json')))
    except Exception: continue
    for f in d.get('findings',[]):
        if f.get('in_scope') and f.get('severity') in ('blocker','should'):
            print(f"- [{n}] {f.get('id')} ({f.get('severity')}, {f.get('area')}) {f.get('where','')}: {f.get('issue','')}")
PY
)"
fi

cat > "$OUT/prompt.md" <<EOF
$(cat "$HERE/rubric.md")

## This round

Round $N. Snapshot of the change (head $HEAD): $SNAP/repo
Full diff ($BASE..$HEAD): $OUT/diff.patch ($(wc -l < "$OUT/diff.patch") lines). Also: \`git diff $BASE $HEAD\` in the snapshot.

$CONTEXT

${BRIEF:+Brief from the author:
$BRIEF
}
$( [ -n "$PREV" ] && printf 'Open in-scope findings from round %s (check these first; then review only code changed since %s):\n%s\n' "$((N-1))" "$(sed 's/.*\.\.//' "$PREV/range.txt")" "${PREV_FINDINGS:-(none)}")

Evidence images (also attached where your harness supports it):
$( [ ${#IMGS[@]} -gt 0 ] && printf '%s\n' "${IMGS[@]}" || echo "(none)")

Evidence text files (read them):
$( [ ${#TEXTS[@]} -gt 0 ] && printf '%s\n' "${TEXTS[@]}" || echo "(none)")

Diff (first 60 KB; read $OUT/diff.patch for the rest):
\`\`\`diff
$(head -c 60000 "$OUT/diff.patch")
\`\`\`
EOF

# Codex can't run shell commands on CodeBox2 (its bwrap sandbox fails with "loopback: Failed RTM_NEWADDR"),
# and Antigravity headless ends the run with no output on any command outside its allow-list. So GPT and Gemini
# get everything inline: the full diff, the evidence text and the post-change text of every changed file (~400 KB).
{
  cat "$OUT/prompt.md"
  echo; echo "## Inline context (do not rely on shell commands, they may fail here; everything you need is below, and you may still read other files in the snapshot)"
  echo; echo '### Full diff'; echo '```diff'; head -c 200000 "$OUT/diff.patch"; echo '```'
  for t in "${TEXTS[@]}"; do echo; echo "### Evidence: $t"; echo '```'; head -c 20000 "$t"; echo '```'; done
  budget=400000
  while IFS= read -r f; do
    p="$SNAP/repo/$f"; [ -f "$p" ] || continue
    s=$(wc -c < "$p"); [ "$s" -gt 80000 ] && continue; [ "$budget" -lt "$s" ] && break; budget=$((budget - s))
    grep -Iq . "$p" || continue
    echo; echo "### File after the change: $f"; echo '```'; cat "$p"; echo '```'
  done < <(git -C "$SNAP/repo" diff --name-only --diff-filter=AM "$BASE" "$HEAD")
} > "$OUT/prompt-inline.md"

ADD=("$OUT" "${EVID[@]}")
POINTER="Read the file $OUT/prompt-inline.md with your file-read tool and follow it exactly. Your output must be only the JSON it asks for. Read files with your file-read tool. Do not run shell commands except, if you must, plain git diff, git log, git show, cat, grep, ls, head, tail, wc or find with no pipes. Any other command or any write ends your run with no output."

run_claude(){
  (cd "$SNAP/repo" && timeout "$TIMEOUT" claude -p --model claude-opus-5-5 --effort xhigh --permission-mode dontAsk \
     --add-dir "${ADD[@]}" --allowedTools Read Grep Glob "Bash(git diff:*)" "Bash(git log:*)" "Bash(git show:*)" \
     < "$OUT/prompt.md" > "$OUT/claude.raw" 2> "$OUT/claude.err"); }
run_gpt(){
  local im=(); for f in "${IMGS[@]}"; do im+=(-i "$f"); done
  (cd "$SNAP/repo" && timeout "$TIMEOUT" codex exec -m gpt-6.1-sol -c model_reasoning_effort='"xhigh"' \
     -s read-only --skip-git-repo-check "${im[@]}" -- - \
     < "$OUT/prompt-inline.md" > "$OUT/gpt.raw" 2> "$OUT/gpt.err"); }
run_gemini(){
  # Plan mode plus an allow-list of read-only commands. Anything else (writes included) is auto-denied headless.
  local set='{"permissions":{"allow":["command(git diff)","command(git log)","command(git show)","command(git status)","command(cat)","command(grep)","command(ls)","command(head)","command(tail)","command(wc)","command(find)"]}}'
  local ad=(); for d in "${ADD[@]}"; do ad+=(--add-dir "$d"); done
  (cd "$SNAP/repo" && timeout "$TIMEOUT" agy --model gemini-4-argon-high --mode plan --settings "$set" \
     "${ad[@]}" --print-timeout "${TIMEOUT}s" -p "$POINTER" \
     < /dev/null > "$OUT/gemini.raw" 2> "$OUT/gemini.err")
  # A denied tool call ends an Antigravity headless run with no output; retry once.
  if grep -q 'no output produced' "$OUT/gemini.raw" "$OUT/gemini.err" 2>/dev/null; then
    mv "$OUT/gemini.raw" "$OUT/gemini.denied.raw"
    (cd "$SNAP/repo" && timeout "$TIMEOUT" agy --model gemini-4-argon-high --mode plan --settings "$set" \
       "${ad[@]}" --print-timeout "${TIMEOUT}s" -p "$POINTER Your previous attempt was cut off by a denied command: run NO shell commands this time." \
       < /dev/null > "$OUT/gemini.raw" 2>> "$OUT/gemini.err")
  fi; }

extract(){ python3 - "$1" "$2" <<'PY'
import json,sys
t=open(sys.argv[1],errors='ignore').read()
best=None
for i,c in enumerate(t):
    if c!='{': continue
    try: j,_=json.JSONDecoder().raw_decode(t[i:])
    except Exception: continue
    if isinstance(j,dict) and 'verdict' in j: best=j
if best is None: sys.exit(1)
json.dump(best,open(sys.argv[2],'w'),indent=1)
PY
}

for n in claude gpt gemini; do
  [ -n "$ONLY" ] && [ "$ONLY" != "$n" ] && continue
  ( "run_$n"; extract "$OUT/$n.raw" "$OUT/$n.json" || rm -f "$OUT/$n.json" ) &
done
wait

python3 "$HERE/table.py" "$ROOT" "$OUT"
