#!/bin/bash
# Record headless gameplay for visual review.
#
# Usage: scripts/capture-gameplay.sh <out-dir> [seconds] [--no-build]
#   out-dir   e.g. ~/GameDev/visual-review/<thread>/<YYYYMMDD-HHMM>-<sha>
# Produces: <out-dir>/gameplay.webp, still_01..05.png (evenly spaced), frames/ (deleted unless KEEP_FRAMES=1)
#
# Builds a Linux player from the Unity project next to this script (unless --no-build),
# runs it under Xvfb with software Vulkan/GL, AI on both teams, fixed 30 fps capture.
set -euo pipefail

OUT="${1:?usage: capture-gameplay.sh <out-dir> [seconds] [--no-build]}"
SECONDS_TO_RECORD="${2:-30}"
REPO="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$REPO/OpenFifa"
UNITY="${UNITY:-$HOME/Unity/6000.3.10f1/Editor/Unity}"
PLAYER="$PROJECT/build/LinuxCapture/OpenFootball.x86_64"
mkdir -p "$OUT/frames"

if [[ " $* " != *" --no-build "* ]]; then
  echo "[capture] building Linux player..."
  # A real (virtual) display: -nographics player builds can stall on shader compilation.
  xvfb-run -a -s "-screen 0 1280x720x24" "$UNITY" -batchmode -quit -projectPath "$PROJECT" \
    -executeMethod OpenFifa.Editor.CaptureBuild.BuildLinuxCapture \
    -logFile "$OUT/build.log" || { echo "build failed, see $OUT/build.log"; exit 1; }
fi

echo "[capture] recording ${SECONDS_TO_RECORD}s..."
xvfb-run -a -s "-screen 0 1280x720x24" "$PLAYER" \
  -screen-width 1280 -screen-height 720 -screen-fullscreen 0 \
  -ofCapture "$OUT/frames" -ofCaptureSeconds "$SECONDS_TO_RECORD" -ofCaptureFps 30 -ofAutoplay \
  -logFile "$OUT/player.log" || true

N=$(ls "$OUT/frames" | wc -l)
echo "[capture] $N frames"
[ "$N" -gt 0 ] || { echo "no frames captured, see $OUT/player.log"; exit 1; }

# Animated WebP preview (10 fps, 640x360) via Pillow; the box's only ffmpeg build lacks a PNG decoder.
python3 - "$OUT" <<'PY'
import sys, glob
from PIL import Image
out = sys.argv[1]
frames = sorted(glob.glob(out + "/frames/frame_*.png"))[::3]
ims = [Image.open(f).convert("RGB").resize((640, 360)) for f in frames]
ims[0].save(out + "/gameplay.webp", save_all=True, append_images=ims[1:], duration=100, loop=0, quality=70)
PY

for k in 1 2 3 4 5; do
  idx=$(( (N * (2 * k - 1)) / 10 )); [ "$idx" -ge 1 ] || idx=1
  cp "$OUT/frames/$(printf 'frame_%05d.png' "$idx")" "$OUT/still_0$k.png"
done
[ "${KEEP_FRAMES:-0}" = "1" ] || rm -rf "$OUT/frames"
echo "[capture] wrote $OUT/gameplay.webp and 5 stills"
