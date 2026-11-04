#!/bin/bash
# Unpack an exported Unity Xcode project and restore exec bits (IL2CPP helpers lose them in artifacts).
# usage: prepare-xcode.sh <tarball> <dest-dir>; appends BUNDLE_ID, PRODUCT_NAME, PROFILE_NAME, XCODE_DIR to GITHUB_ENV.
set -euo pipefail
tgz=$1; dest=$2
mkdir -p "$dest"; tar -xzf "$tgz" -C "$dest"; rm -f "$tgz"
find "$dest" \( -name '*.sh' -o -name 'usymtool*' -o -name 'il2cpp' -o -name 'MapFileParser*' \) -type f -exec chmod +x {} +
# anything under IL2CPP build/deploy that is a Mach-O binary
find "$dest" -path '*IL2CPP/build/deploy*' -type f ! -name '*.*' -exec chmod +x {} + 2>/dev/null || true
[ -d "$dest/Unity-iPhone.xcodeproj" ] || { echo "::error::no Unity-iPhone.xcodeproj in $tgz"; ls -la "$dest"; exit 1; }
# shellcheck disable=SC1091
set -a; source "$dest/openfifa-export.env"; set +a
{ echo "BUNDLE_ID=$BUNDLE_ID"; echo "PRODUCT_NAME=$PRODUCT_NAME"; echo "PROFILE_NAME=$PROFILE_NAME"; echo "XCODE_DIR=$dest"; } >> "${GITHUB_ENV:-/dev/null}"
echo "prepared $EXPORT_KIND project: $BUNDLE_ID build $BUILD_NUMBER (Unity $UNITY_VERSION)"
