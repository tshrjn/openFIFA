#!/bin/bash
# iPad simulator smoke test: build the Simulator-SDK export, boot an iPad, install, launch, take
# screenshots and a short video, and fail if the app is not running at each checkpoint (mirrors
# ios-factory's ci/screenshots.sh launch check).
# Env: XCODE_DIR, BUNDLE_ID, PRODUCT_NAME, RUNNER_TEMP. Optional: SHOT_TIMES ("10 25 45"), VIDEO_SECONDS (20).
set -euo pipefail
OUT="$RUNNER_TEMP/shots"; LOGS="$RUNNER_TEMP/logs"; mkdir -p "$OUT" "$LOGS"
SHOT_TIMES=${SHOT_TIMES:-10 25 45}; VIDEO_SECONDS=${VIDEO_SECONDS:-20}

UDID=$(xcrun simctl list devices available -j | python3 -c '
import json, re, sys
devs = json.load(sys.stdin)["devices"]
def ver(rt):
    m = re.search(r"iOS-(\d+)-(\d+)", rt); return tuple(map(int, m.groups())) if m else (0, 0)
for rt in sorted((r for r in devs if "iOS" in r), key=ver, reverse=True):
    pads = [d for d in devs[rt] if d["name"].startswith("iPad")]
    pick = next((d for d in pads if d["name"].startswith("iPad Pro 13")), None) \
        or next((d for d in pads if d["name"].startswith("iPad Pro")), pads[0] if pads else None)
    if pick:
        print(pick["udid"]); print(pick["name"] + " / " + rt.split(".")[-1], file=sys.stderr); break
')
[ -n "$UDID" ] || { echo "::error::no iPad simulator available"; xcrun simctl list devices available; exit 1; }
xcrun simctl boot "$UDID" 2>/dev/null || true   # boots while we compile

set +e
xcodebuild build -project "$XCODE_DIR/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release \
  -sdk iphonesimulator -destination "id=$UDID" -derivedDataPath "$RUNNER_TEMP/dd-sim" \
  CODE_SIGNING_ALLOWED=NO > "$LOGS/sim-build.log" 2>&1
rc=$?
set -e
if [ $rc -ne 0 ]; then
  echo "::group::compile errors"; grep -E "error:|fatal error|Undefined symbol|ld: " "$LOGS/sim-build.log" | sort -u | head -80; echo "::endgroup::"
  tail -40 "$LOGS/sim-build.log"; exit $rc
fi
APP=$(find "$RUNNER_TEMP/dd-sim/Build/Products" -maxdepth 2 -name "*.app" -path "*iphonesimulator*" | head -1)
echo "app: $APP"
xcrun simctl bootstatus "$UDID" -b >/dev/null
xcrun simctl status_bar "$UDID" override --time 9:41 --batteryState charged --batteryLevel 100 --wifiBars 3 2>/dev/null || true
xcrun simctl install "$UDID" "$APP"

running() { xcrun simctl spawn "$UDID" launchctl list | grep -q "UIKitApplication:$BUNDLE_ID"; }
xcrun simctl launch --terminate-running-process --stdout="$LOGS/player-stdout.log" --stderr="$LOGS/player-stderr.log" \
  "$UDID" "$BUNDLE_ID" -logFile - >/dev/null
start=$(date +%s)
xcrun simctl io "$UDID" recordVideo --codec=h264 --force "$OUT/smoke.mp4" > "$LOGS/video.log" 2>&1 &
VPID=$!
video_done=0
status=ok
for t in $SHOT_TIMES; do
  now=$(( $(date +%s) - start ))
  [ "$now" -lt "$t" ] && sleep $(( t - now ))
  if [ $video_done = 0 ] && [ $(( $(date +%s) - start )) -ge "$VIDEO_SECONDS" ]; then
    kill -INT "$VPID" 2>/dev/null || true; wait "$VPID" 2>/dev/null || true; video_done=1
  fi
  xcrun simctl io "$UDID" screenshot --type=png "$OUT/t$(printf %03d "$t")s.png" >/dev/null 2>&1 || true
  if ! running; then echo "CRASH: not running at ${t}s" | tee -a "$OUT/status.txt"; status=crash; break; fi
done
[ $video_done = 1 ] || { kill -INT "$VPID" 2>/dev/null || true; wait "$VPID" 2>/dev/null || true; }

xcrun simctl spawn "$UDID" log show --last 5m --style compact \
  --predicate "process == \"$PRODUCT_NAME\"" > "$LOGS/app-console.log" 2>/dev/null || true
find "$HOME/Library/Logs/DiagnosticReports" -newermt "@$start" -name "${PRODUCT_NAME}*" -exec cp {} "$LOGS/" \; 2>/dev/null || true
if ls "$LOGS/${PRODUCT_NAME}"*.ips >/dev/null 2>&1; then echo "CRASH: crash report written" | tee -a "$OUT/status.txt"; status=crash; fi
exc=$(grep -c -E "Exception|NullReference" "$LOGS/player-stdout.log" 2>/dev/null || true)
echo "managed exceptions in player log: ${exc:-0}" >> "$OUT/status.txt"
[ $status = ok ] && echo "ok: running at every checkpoint ($SHOT_TIMES s)" >> "$OUT/status.txt"
cat "$OUT/status.txt"; ls -la "$OUT"
[ $status = ok ]
