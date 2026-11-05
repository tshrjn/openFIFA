#!/bin/bash
# Render the player-appearance review set (-spLooks) and build contact sheets for the judges.
# Usage: scripts/looks-loop/render.sh <out-dir> [--no-build]
# Writes <out-dir>/looks_*.png, looks.txt and sheets/{faces,angles,bodies,backs,ingame}.png.
set -euo pipefail
OUT="${1:?usage: render.sh <out-dir> [--no-build]}"; BUILD=1; [ "${2:-}" = "--no-build" ] && BUILD=0
HERE="$(cd "$(dirname "$0")" && pwd)"; REPO="$(cd "$HERE/../.." && pwd)"; PROJECT="$REPO/OpenFifa"
UNITY="${UNITY:-$HOME/Unity/6000.3.10f1/Editor/Unity}"
PLAYER="$PROJECT/build/LooksCapture/OpenFootball.x86_64"
mkdir -p "$OUT"
if ! pgrep -f "projectPath $PROJECT" >/dev/null; then rm -f "$PROJECT/Temp/UnityLockfile"; fi
if [ "$BUILD" = 1 ]; then
  xvfb-run -a -s "-screen 0 1280x720x24" "$UNITY" -batchmode -quit -projectPath "$PROJECT" \
    -executeMethod OpenFifa.Gameplay.SinglePlayer.Editor.SinglePlayerSceneBootstrap.BuildLinuxCapture \
    -spBuildOut "$PLAYER" -logFile "$OUT/build.log" || { echo "build failed, see $OUT/build.log"; exit 1; }
fi
timeout 600 xvfb-run -a -s "-screen 0 1280x720x24" "$PLAYER" -screen-width 1280 -screen-height 720 \
  -screen-fullscreen 0 -force-glcore -spBot -spLevel 3 -spSeed 1 -spHome arsenal -spAway chelsea \
  -spCapture "$OUT" -spLooks -logFile "$OUT/player.log" || true
rm -rf "$OUT/frames"
ls "$OUT"/looks_face_p*.png >/dev/null 2>&1 || { echo "no looks stills, see $OUT/player.log"; exit 1; }
python3 "$HERE/sheets.py" "$OUT"
echo "[looks] $OUT/sheets"
