#!/bin/bash
# One autoresearch step: render the working tree, have the council compare it with the current best, log it.
# Usage: step.sh <iter-name> "<what changed>" ["<region hint for the judges>"] [--no-build]
# The hint names where to look (e.g. "nose and brow geometry"), never the direction of the change: a directional
# hint ("stronger", "more open") tells a judge which side is the candidate once they spot the difference.
# State lives in $LOOKS_ROOT (default ~/GameDev/review/looks-loop): iters/<name>/, best -> iters/<kept>, log.tsv.
# The first run (no best yet) is the baseline and is kept. The caller commits a kept change and reverts the rest.
set -euo pipefail
NAME="$1"; DESC="$2"; shift 2
HINT=""; if [ $# -gt 0 ] && [ "$1" != "--no-build" ]; then HINT="$1"; shift; fi
HERE="$(cd "$(dirname "$0")" && pwd)"; ROOT="${LOOKS_ROOT:-$HOME/GameDev/review/looks-loop}"
IT="$ROOT/iters/$NAME"; mkdir -p "$ROOT/iters"
python3 "$HERE/test_verdict.py" >/dev/null 2>&1 || { echo "verdict tests fail: python3 $HERE/test_verdict.py"; exit 1; }
"$HERE/render.sh" "$IT" "$@"
SHA=$(git -C "$HERE" rev-parse --short HEAD)$(git -C "$HERE" diff --quiet HEAD -- ../../OpenFifa || echo "+dirty")
[ -f "$ROOT/log.tsv" ] || printf 'time\titer\tsha\tkeep\tvotes\tcand_face\tcand_hair\tcand_skin\tcand_body\tcand_overall\tbest_overall\tdesc\n' > "$ROOT/log.tsv"
if [ ! -e "$ROOT/best" ]; then
  ln -sfn "iters/$NAME" "$ROOT/best"
  printf '%s\t%s\t%s\tbaseline\t-\t\t\t\t\t\t\t%s\n' "$(date -u +%FT%TZ)" "$NAME" "$SHA" "$DESC" >> "$ROOT/log.tsv"
  echo "baseline set"; exit 0
fi
BEST="$(readlink -f "$ROOT/best")"
"$HERE/judge.sh" "$IT" "$BEST" "$IT/judge" "$HINT"
python3 - "$IT/judge/verdict.json" "$ROOT/log.tsv" "$NAME" "$SHA" "$DESC" <<'PY'
import json, sys, time
v = json.load(open(sys.argv[1])); c = v["cand_mean"]; b = v["best_mean"]
row = [time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()), sys.argv[3], sys.argv[4], "KEEP" if v["keep"] else "revert",
       f'{v["votes"]}/{v["answered"]}'] + [str(c[d]) for d in ["face", "hair", "skin", "body", "overall"]] + [str(b["overall"]), sys.argv[5]]
open(sys.argv[2], "a").write("\t".join(row) + "\n")
PY
if python3 -c "import json,sys;sys.exit(0 if json.load(open('$IT/judge/verdict.json'))['keep'] else 1)"; then
  ln -sfn "iters/$NAME" "$ROOT/best"; echo "KEEP $NAME"
else echo "REVERT $NAME"; fi
