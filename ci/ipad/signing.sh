#!/bin/bash
# Install the distribution identity, the App Store profile and the ASC API key on the runner.
# Same secrets as ios-factory's ci/signing.sh, but the profile comes from a secret (openFIFA is public,
# so it is not committed). Env: DIST_P12_B64, DIST_P12_PASSWORD, ASC_KEY_ID, ASC_KEY_P8_B64,
# IOS_PROFILE_B64, RUNNER_TEMP, GITHUB_ENV.
set -euo pipefail
: "${DIST_P12_B64:?DIST_P12_B64 secret missing (ci/ipad/sync-secrets.sh)}"
: "${IOS_PROFILE_B64:?IOS_PROFILE_B64 secret missing (ci/ipad/ship.sh refreshes it)}"
KEYCHAIN="$RUNNER_TEMP/openfifa.keychain-db"
KP=$(uuidgen)
security create-keychain -p "$KP" "$KEYCHAIN"
security set-keychain-settings -lut 21600 "$KEYCHAIN"
security unlock-keychain -p "$KP" "$KEYCHAIN"
curl -fsSL -o "$RUNNER_TEMP/wwdr.cer" https://www.apple.com/certificateauthority/AppleWWDRCAG3.cer
security import "$RUNNER_TEMP/wwdr.cer" -k "$KEYCHAIN" -T /usr/bin/codesign >/dev/null 2>&1 || true
echo "$DIST_P12_B64" | base64 --decode > "$RUNNER_TEMP/dist.p12"
security import "$RUNNER_TEMP/dist.p12" -k "$KEYCHAIN" -P "$DIST_P12_PASSWORD" -f pkcs12 -A \
  -T /usr/bin/codesign -T /usr/bin/security
rm -f "$RUNNER_TEMP/dist.p12"
security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$KP" "$KEYCHAIN" >/dev/null
# shellcheck disable=SC2046
security list-keychains -d user -s "$KEYCHAIN" $(security list-keychains -d user | tr -d '"')
security find-identity -v -p codesigning "$KEYCHAIN"

PROFILE="$RUNNER_TEMP/AppStore.mobileprovision"
echo "$IOS_PROFILE_B64" | base64 --decode > "$PROFILE"
uuid=$(security cms -D -i "$PROFILE" | plutil -extract UUID raw -)
for dir in "$HOME/Library/MobileDevice/Provisioning Profiles" "$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles"; do
  mkdir -p "$dir"; cp "$PROFILE" "$dir/$uuid.mobileprovision"
done
echo "installed profile $(security cms -D -i "$PROFILE" | plutil -extract Name raw -) ($uuid)"

mkdir -p "$HOME/.appstoreconnect/private_keys"
KEYFILE="$HOME/.appstoreconnect/private_keys/AuthKey_$ASC_KEY_ID.p8"
echo "$ASC_KEY_P8_B64" | base64 --decode > "$KEYFILE"
echo "ASC_KEY_PATH=$KEYFILE" >> "$GITHUB_ENV"
