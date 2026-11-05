#!/bin/bash
# Render the player-appearance review set (-spLooks) and build contact sheets for the judges.
# Usage: scripts/looks-loop/render.sh <out-dir> [--no-build]
# LOOKS_EXTRA: extra player args, e.g. LOOKS_EXTRA=-spLooksShowcase (rare roster looks on the first portraits).
# Writes <out-dir>/looks_*.png, looks.txt and sheets/{faces,angles,bodies,backs,ingame}.png.
set -euo pipefail
OUT="${1:?usage: render.sh <out-dir> [--no-build]}"; BUILD=1; [ "${2:-}" = "--no-build" ] && BUILD=0
HERE="$(cd "$(dirname "$0")" && pwd)"; REPO="$(cd "$HERE/../.." && pwd)"; PROJECT="$REPO/OpenFifa"
UNITY="${UNITY:-$HOME/Unity/6000.3.10f1/Editor/Unity}"
PLAYER="$PROJECT/build/LooksCapture/OpenFootball.x86_64"
# Fresh output directory: stale stills from an earlier run must never pass as this render.
[ -e "$OUT" ] && [ -n "$(ls -A "$OUT" 2>/dev/null)" ] && { echo "output dir $OUT is not empty"; exit 1; }
mkdir -p "$OUT"
if ! pgrep -f "projectPath $PROJECT" >/dev/null; then rm -f "$PROJECT/Temp/UnityLockfile"; fi
if [ "$BUILD" = 1 ]; then
  xvfb-run -a -s "-screen 0 1280x720x24" "$UNITY" -batchmode -quit -projectPath "$PROJECT" \
    -executeMethod OpenFifa.Gameplay.SinglePlayer.Editor.SinglePlayerSceneBootstrap.BuildLinuxCapture \
    -spBuildOut "$PLAYER" -logFile "$OUT/build.log" || { echo "build failed, see $OUT/build.log"; exit 1; }
fi
timeout 600 xvfb-run -a -s "-screen 0 1280x720x24" "$PLAYER" -screen-width 1280 -screen-height 720 \
  -screen-fullscreen 0 -force-glcore -spBot -spLevel 3 -spSeed 1 -spHome arsenal -spAway chelsea \
  -spCapture "$OUT" -spLooks ${LOOKS_EXTRA:-} -logFile "$OUT/player.log" || { echo "capture failed (exit $?), see $OUT/player.log"; exit 1; }
rm -rf "$OUT/frames"
# The capture writes looks.txt last: require it and every expected still (6 players x 5 shots + 2 match stills).
[ -f "$OUT/looks.txt" ] || { echo "capture incomplete (no looks.txt), see $OUT/player.log"; exit 1; }
for k in 0 1 2 3 4 5; do for shot in face face34 profile body bodyback; do
  [ -f "$OUT/looks_${shot}_p$k.png" ] || { echo "missing looks_${shot}_p$k.png"; exit 1; }; done; done
for f in looks_broadcast.png looks_closeup_ingame.png; do [ -f "$OUT/$f" ] || { echo "missing $f"; exit 1; }; done
python3 "$HERE/sheets.py" "$OUT"
echo "[looks] $OUT/sheets"
