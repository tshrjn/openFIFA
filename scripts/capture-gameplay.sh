#!/bin/bash
# Record headless gameplay for visual review.
#
# Usage: scripts/capture-gameplay.sh <out-dir> [seconds] [--no-build] [--legacy-4v4] [-- extra player args]
#   out-dir   e.g. ~/GameDev/visual-review/<thread>/<YYYYMMDD-HHMM>-<sha>
#   extra player args are passed to the player, e.g. -- -spForceOffside -spWeather rain -spLevel 5 -spSeed 7
# Produces: <out-dir>/gameplay.webp, still_01..05.png (evenly spaced), frames/ (deleted unless KEEP_FRAMES=1),
#           plus whatever the capture driver writes (capture.txt, commentary.txt, named stills).
#
# Default: the 11v11 single-player match (SinglePlayerSceneBootstrap.BuildLinuxCapture + SinglePlayerCapture),
# scripted bot on the user's side at Professional, seed 2. --legacy-4v4 records main's old 4v4 Match.unity.
# Builds under Xvfb with a real GL context (not -nographics: player builds can stall on shader compilation).
set -euo pipefail

OUT="${1:?usage: capture-gameplay.sh <out-dir> [seconds] [--no-build] [--legacy-4v4] [-- extra args]}"
shift
SECONDS_TO_RECORD=40
if [[ "${1:-}" =~ ^[0-9]+$ ]]; then SECONDS_TO_RECORD="$1"; shift; fi
BUILD=1; LEGACY=0; EXTRA=()
while [ $# -gt 0 ]; do
  case "$1" in
    --no-build) BUILD=0 ;;
    --legacy-4v4) LEGACY=1 ;;
    --) shift; EXTRA=("$@"); break ;;
    *) echo "unknown option $1"; exit 2 ;;
  esac
  shift
done

REPO="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$REPO/OpenFifa"
UNITY="${UNITY:-$HOME/Unity/6000.3.10f1/Editor/Unity}"
mkdir -p "$OUT/frames"
rm -f "$PROJECT/Temp/UnityLockfile"

if [ "$LEGACY" = 1 ]; then
  PLAYER="$PROJECT/build/LinuxCapture/OpenFootball.x86_64"
  METHOD=OpenFifa.Editor.CaptureBuild.BuildLinuxCapture
  BUILD_ARGS=()
  RUN_ARGS=(-ofCapture "$OUT/frames" -ofCaptureSeconds "$SECONDS_TO_RECORD" -ofCaptureFps 30 -ofAutoplay)
else
  PLAYER="$PROJECT/build/SPCapture/OpenFootball.x86_64"
  METHOD=OpenFifa.Gameplay.SinglePlayer.Editor.SinglePlayerSceneBootstrap.BuildLinuxCapture
  BUILD_ARGS=(-spBuildOut "$PLAYER")
  RUN_ARGS=(-force-glcore -spBot -spLevel 3 -spSeed 2 -spCapture "$OUT/frames"
            -spCaptureSeconds "$SECONDS_TO_RECORD" -spCaptureFps 30)
fi

if [ "$BUILD" = 1 ]; then
  echo "[capture] building Linux player ($METHOD)..."
  xvfb-run -a -s "-screen 0 1280x720x24" "$UNITY" -batchmode -quit -projectPath "$PROJECT" \
    -executeMethod "$METHOD" "${BUILD_ARGS[@]}" -logFile "$OUT/build.log" \
    || { echo "build failed, see $OUT/build.log"; exit 1; }
fi

echo "[capture] recording ${SECONDS_TO_RECORD}s..."
xvfb-run -a -s "-screen 0 1280x720x24" "$PLAYER" \
  -screen-width 1280 -screen-height 720 -screen-fullscreen 0 \
  "${RUN_ARGS[@]}" "${EXTRA[@]}" -logFile "$OUT/player.log" || true

N=$(find "$OUT/frames" -name '*.png' | wc -l)
echo "[capture] $N frames"
[ "$N" -gt 0 ] || { echo "no frames captured, see $OUT/player.log"; exit 1; }

# Animated WebP preview (10 fps, 640x360) via Pillow; the box's only ffmpeg build lacks a PNG decoder.
python3 - "$OUT" <<'PY'
import sys, glob, shutil
from PIL import Image
out = sys.argv[1]
frames = sorted(glob.glob(out + "/frames/**/*.png", recursive=True))
ims = [Image.open(f).convert("RGB").resize((640, 360)) for f in frames[::3]]
ims[0].save(out + "/gameplay.webp", save_all=True, append_images=ims[1:], duration=100, loop=0, quality=70)
for k in range(5):
    shutil.copy(frames[min(len(frames) - 1, len(frames) * (2 * k + 1) // 10)], f"{out}/still_0{k + 1}.png")
PY

[ "${KEEP_FRAMES:-0}" = "1" ] || rm -rf "$OUT/frames"
echo "[capture] wrote $OUT/gameplay.webp and 5 stills"
