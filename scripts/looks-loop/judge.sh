#!/bin/bash
# Three-provider looks council: Claude Opus 5.5, GPT-6.1 Sol (Codex), Gemini 4 Argon (Antigravity).
# Usage: judge.sh <candidate-dir> <best-dir> <out-dir>
# Each judge sees both render sets (labelled A/B in random order), scores each on face/hair/skin/body/overall
# (0-10, EA FC broadcast quality = 9-10) and says which set is better. Writes <out>/{claude,gpt,gemini}.json
# and <out>/verdict.json ({"keep": bool, "votes": ..., "cand_mean": ..., "best_mean": ...}).
set -uo pipefail
CAND="$(realpath "$1")"; BEST="$(realpath "$2")"; OUT="$3"; mkdir -p "$OUT"; OUT="$(realpath "$OUT")"
HERE="$(cd "$(dirname "$0")" && pwd)"; TIMEOUT="${JUDGE_TIMEOUT:-900}"
if [ $((RANDOM % 2)) = 0 ]; then A="$CAND"; B="$BEST"; echo cand=A > "$OUT/order"; else A="$BEST"; B="$CAND"; echo cand=B > "$OUT/order"; fi
# Blind: the judges see anonymous copies (A_faces.png, ...) in a neutral directory, run there, and never see
# iteration names, paths or the order file. Their raw output is copied back to $OUT afterwards.
WORK="$(mktemp -d "${TMPDIR:-/tmp}/looks-judge-XXXXXX")"
SHEETS=(faces angles bodies backs ingame)
IMGS=(); LIST=""
for s in "${SHEETS[@]}"; do
  for L in A B; do d=$([ $L = A ] && echo "$A" || echo "$B"); f="$d/sheets/$s.png"
    [ -f "$f" ] || { echo "missing $f"; exit 1; }
    cp "$f" "$WORK/${L}_$s.png"; IMGS+=("$WORK/${L}_$s.png"); LIST+="- Set $L, $s: $WORK/${L}_$s.png"$'\n'; done; done
cat > "$WORK/prompt.md" <<P
You are a judge on a visual council for OpenFootball, an original football game in Unity. We are hill-climbing the
look of the PLAYERS ONLY: faces, hair, skin and body (proportions, joints/seams, how the kit sits on the body, neckline).
Ignore stadium, pitch, HUD, crowd, kit design/colours and animation timing except where they show the body badly.

Two render sets of the same match moment, A and B. Each set has five contact sheets:
faces (front face close-ups of six players), angles (three-quarter and profile per player), bodies (full body front),
backs (full body back), ingame (broadcast camera and the in-game close-up camera).
Open every image below with your file-read tool and look at them carefully before scoring:
$LIST
Score each set 0-10 per dimension, calibrated against EA SPORTS FC 26 live gameplay = 9-10, a good 2015-era
console football game = 6-7, generic low-poly mannequins with painted faces = 2-3. Be strict and consistent;
small real improvements should move scores by 0.5, not more.
- face: head shape, eyes, nose, mouth, ears, brows, facial hair, believable human structure
- hair: shape, volume, hairline, colour, variety between players
- skin: tone variety and realism, shading, no flat/plastic look, consistent between face/neck/arms/legs
- body: athletic proportions, no visible mannequin seams or joint breaks, natural neck/shoulders/hands, kit fit and neckline
- overall: overall player appearance quality

Then say which set is better overall for player appearance ("A", "B" or "tie"), and give the three most valuable,
concrete fixes for the better set (what is wrong on screen and what to change).
Output ONLY this JSON, no prose before or after:
{"A":{"face":0,"hair":0,"skin":0,"body":0,"overall":0},"B":{"face":0,"hair":0,"skin":0,"body":0,"overall":0},
 "better":"A|B|tie","confidence":"low|medium|high","why":"one or two sentences","fixes":["...","...","..."]}
P
cp "$WORK/prompt.md" "$OUT/prompt.md"
run_claude(){ (cd "$WORK" && timeout "$TIMEOUT" claude -p --model claude-opus-5-5 --permission-mode dontAsk \
   --allowedTools Read < "$WORK/prompt.md" > "$OUT/claude.raw" 2> "$OUT/claude.err"); }
run_gpt(){ local im=(); for f in "${IMGS[@]}"; do im+=(-i "$f"); done
  (cd "$WORK" && timeout "$TIMEOUT" codex exec -m gpt-6.1-sol -s read-only --skip-git-repo-check "${im[@]}" -- - \
   < "$WORK/prompt.md" > "$OUT/gpt.raw" 2> "$OUT/gpt.err"); }
run_gemini(){ (cd "$WORK" && timeout "$TIMEOUT" agy --model gemini-4-argon-high --mode plan --add-dir "$WORK" \
   --print-timeout "${TIMEOUT}s" \
   -p "Read the file $WORK/prompt.md with your file-read tool and follow it exactly. Run NO shell commands; use only your file-read tool. Output only the JSON." \
   < /dev/null > "$OUT/gemini.raw" 2> "$OUT/gemini.err"); }
extract(){ python3 - "$1" "$2" <<'PY'
import json,sys
t=open(sys.argv[1],errors='ignore').read(); best=None
for i,c in enumerate(t):
    if c!='{': continue
    try: j,_=json.JSONDecoder().raw_decode(t[i:])
    except Exception: continue
    if isinstance(j,dict) and 'better' in j: best=j
if best is None: sys.exit(1)
json.dump(best,open(sys.argv[2],'w'),indent=1)
PY
}
for n in claude gpt gemini; do ( "run_$n"; extract "$OUT/$n.raw" "$OUT/$n.json" || rm -f "$OUT/$n.json" ) & done
wait
rm -rf "$WORK"
python3 "$HERE/verdict.py" "$OUT"
