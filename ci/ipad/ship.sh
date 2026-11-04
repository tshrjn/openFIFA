#!/bin/bash
# Build OpenFifa for iPad on GitHub Actions (.github/workflows/ipad.yml) from the VPS, modelled on
# ios-factory's bin/ios-ship. Does NOT push: the ref must already be on GitHub.
# usage: ci/ipad/ship.sh [--check] [--no-shots] [--ref <branch>] [--notes "what to test"]
#   --check     export + simulator smoke test + screenshots only (needs only the Unity secrets)
#   (default)   also archive, sign, upload to TestFlight, wait for Apple and add the internal testers
# Output: OpenFifa/.build/<build number>/{shots,logs,result.env,failed.log}
set -euo pipefail
REPO=${OPENFIFA_REPO:-tshrjn/openFIFA}
FACTORY=${IOS_FACTORY:-$HOME/ios-factory}
BUNDLE_ID=${IPAD_BUNDLE_ID:-com.tshrjn.openfootball}
APP_NAME=${IPAD_APP_NAME:-OpenFootball}
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
log() { printf '\033[1;34m==>\033[0m %s\n' "$*" >&2; }
die() { printf '\033[1;31mxx\033[0m %s\n' "$*" >&2; exit "${2:-1}"; }
kv() { sed -n "s/^$1=//p" | tail -1; }

check=0; shots=true; notes="Kick off a match and send feedback from TestFlight."
ref=$(git -C "$ROOT" rev-parse --abbrev-ref HEAD)
while [ $# -gt 0 ]; do
  case $1 in
    --check) check=1 ;; --no-shots) shots=false ;;
    --ref) ref=$2; shift ;; --notes) notes=$2; shift ;;
    *) die "unknown option $1" ;;
  esac; shift
done
[ $check = 1 ] && [ $shots = false ] && die "--check with --no-shots does nothing"

# The ref must be pushed and match local HEAD (this script never pushes; the coordinator does).
remote_sha=$(git -C "$ROOT" ls-remote origin "refs/heads/$ref" | cut -f1)
[ -n "$remote_sha" ] || die "branch $ref is not on origin; push it first"
[ "$remote_sha" = "$(git -C "$ROOT" rev-parse HEAD)" ] || log "warning: origin/$ref ($remote_sha) differs from local HEAD; building origin's"
gh workflow view ipad.yml -R "$REPO" >/dev/null 2>&1 || die "ipad.yml is not on $REPO's default branch yet; workflow_dispatch needs it there once"

needed="UNITY_LICENSE UNITY_EMAIL UNITY_PASSWORD"
[ $check = 0 ] && needed="$needed DIST_P12_B64 DIST_P12_PASSWORD ASC_KEY_ID ASC_ISSUER_ID ASC_KEY_P8_B64 IOS_PROFILE_B64"
have=$(gh secret list -R "$REPO" --json name -q '.[].name')
missing=""; for s in $needed; do grep -qx "$s" <<<"$have" || missing="$missing $s"; done
[ -z "$missing" ] || die "missing secrets on $REPO:$missing (see docs/IPAD_PIPELINE.md)" 3

BN=$(( $(date +%s) / 10 ))
out="$ROOT/OpenFifa/.build/$BN"; mkdir -p "$out"

app_id=""
if [ $check = 0 ]; then
  ASC="$FACTORY/bin/asc"
  [ -f "$FACTORY/.secrets/asc.env" ] || die "no App Store Connect API key in $FACTORY/.secrets (ios-factory one-time setup)" 3
  app_id=$("$ASC" app "$BUNDLE_ID" 2>/dev/null | kv app_id || true)
  [ -n "$app_id" ] || die "no App Store Connect app record for $BUNDLE_ID. Create it once:
   $FACTORY/bin/asc bundle $BUNDLE_ID \"$APP_NAME\" && $FACTORY/bin/ios-create-app $BUNDLE_ID \"$APP_NAME\" openfootball
   (needs a valid ios-apple-login session) or on https://appstoreconnect.apple.com/apps (iOS, bundle $BUNDLE_ID, SKU openfootball)" 10
  # Refresh the App Store profile and push it when it changed.
  "$ASC" profile "$BUNDLE_ID" "$out/AppStore.mobileprovision" >/dev/null
  sum=$(sha256sum "$out/AppStore.mobileprovision" | cut -c1-16)
  if [ "$(cat "$FACTORY/.secrets/openfifa-profile-sync" 2>/dev/null)" != "$sum" ]; then
    log "pushing IOS_PROFILE_B64 to $REPO"
    base64 -w0 "$out/AppStore.mobileprovision" | gh secret set IOS_PROFILE_B64 -R "$REPO"
    echo "$sum" > "$FACTORY/.secrets/openfifa-profile-sync"
  fi
  rm -f "$out/AppStore.mobileprovision"
fi

upload=$([ $check = 1 ] && echo false || echo true)
log "dispatching ipad.yml on $ref (build $BN, upload=$upload, screenshots=$shots)"
gh workflow run ipad.yml -R "$REPO" --ref "$ref" -f build_number="$BN" -f upload="$upload" -f screenshots="$shots" >/dev/null
run=""
for _ in $(seq 1 40); do
  run=$(gh run list -R "$REPO" -w ipad.yml -L 20 --json databaseId,displayTitle \
        -q ".[] | select(.displayTitle | contains(\"#$BN \")) | .databaseId" | head -1)
  [ -n "$run" ] && break; sleep 3
done
[ -n "$run" ] || die "could not find the workflow run for build $BN"
log "run: https://github.com/$REPO/actions/runs/$run"
ok=1
gh run watch "$run" -R "$REPO" --exit-status --interval 30 > "$out/watch.log" 2>&1 || ok=0
for a in ipad-shots ipad-logs-export ipad-logs-sim ipad-logs-upload; do
  gh run download "$run" -R "$REPO" -n "$a-$BN" -D "$out/${a#ipad-}" >/dev/null 2>&1 || true
done
if [ $ok = 0 ]; then
  gh run view "$run" -R "$REPO" --log-failed 2>/dev/null | cut -f3- | grep -v '^\s*$' | tail -150 > "$out/failed.log" || true
  cat "$out/failed.log" >&2
  die "build $BN failed; logs in OpenFifa/.build/$BN" 1
fi
[ -d "$out/shots" ] && { log "screenshots: $out/shots"; cat "$out/shots/status.txt" 2>/dev/null || true; }
{ echo "BUILD_NUMBER=$BN"; echo "RUN_URL=https://github.com/$REPO/actions/runs/$run"; } > "$out/result.env"
if [ $check = 1 ]; then echo "CHECK_OK=1"; echo "SHOTS=$out/shots"; exit 0; fi

log "waiting for Apple to process build $BN"
build_id=$("$ASC" wait "$app_id" "$BN" | kv build_id)
tf=$("$ASC" internal "$app_id" --build-id "$build_id" | kv testflight_url)
{ echo "BUILD_ID=$build_id"; echo "TESTFLIGHT_URL=$tf"; echo "NOTES=$notes"; } >> "$out/result.env"
cat "$out/result.env"
