#!/bin/bash
# Archive the device export (manual signing written by IosBuildPostprocess), export an App Store IPA
# and upload it (ios-factory's ci/archive-upload.sh adapted to Unity-iPhone.xcodeproj).
# Env: XCODE_DIR, BUNDLE_ID, PROFILE_NAME, BUILD_NUMBER, APPLE_TEAM_ID, ASC_KEY_ID, ASC_ISSUER_ID, ASC_KEY_PATH, RUNNER_TEMP.
set -euo pipefail
LOGS="$RUNNER_TEMP/logs"; mkdir -p "$LOGS"
ARCHIVE="$RUNNER_TEMP/OpenFootball.xcarchive"; EXPORT="$RUNNER_TEMP/export"
PBX="$XCODE_DIR/Unity-iPhone.xcodeproj/project.pbxproj"
: "${APPLE_TEAM_ID:?APPLE_TEAM_ID repo variable missing}"
grep -q "PROVISIONING_PROFILE_SPECIFIER = \"$PROFILE_NAME\"" "$PBX" || {
  echo "::error::$PBX lacks PROVISIONING_PROFILE_SPECIFIER \"$PROFILE_NAME\" (Unity export ran without UNITY_IOS?)"; exit 1; }

set +e
xcodebuild archive -project "$XCODE_DIR/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release \
  -destination generic/platform=iOS -archivePath "$ARCHIVE" DEVELOPMENT_TEAM="$APPLE_TEAM_ID" \
  > "$LOGS/archive.log" 2>&1
rc=$?
set -e
if [ $rc -ne 0 ]; then
  echo "::group::archive errors"; grep -E "error:|fatal error|Undefined symbol|ld: |Code Signing|Provisioning" "$LOGS/archive.log" | sort -u | head -80; echo "::endgroup::"
  tail -40 "$LOGS/archive.log"; exit $rc
fi

cat > "$RUNNER_TEMP/export.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>method</key><string>app-store-connect</string>
  <key>destination</key><string>export</string>
  <key>teamID</key><string>$APPLE_TEAM_ID</string>
  <key>signingStyle</key><string>manual</string>
  <key>signingCertificate</key><string>Apple Distribution</string>
  <key>provisioningProfiles</key><dict><key>$BUNDLE_ID</key><string>$PROFILE_NAME</string></dict>
  <key>uploadSymbols</key><true/>
  <key>manageAppVersionAndBuildNumber</key><false/>
</dict></plist>
PLIST
xcodebuild -exportArchive -archivePath "$ARCHIVE" -exportOptionsPlist "$RUNNER_TEMP/export.plist" \
  -exportPath "$EXPORT" > "$LOGS/export.log" 2>&1 || { tail -60 "$LOGS/export.log"; exit 1; }
IPA=$(find "$EXPORT" -name "*.ipa" | head -1)
echo "IPA: $IPA ($(du -h "$IPA" | cut -f1))"

if xcrun altool --upload-app --type ios --file "$IPA" --apiKey "$ASC_KEY_ID" --apiIssuer "$ASC_ISSUER_ID" \
     --output-format normal > "$LOGS/upload.log" 2>&1; then
  tail -5 "$LOGS/upload.log"
else
  echo "altool upload failed, trying xcodebuild upload:"; tail -20 "$LOGS/upload.log"
  sed -i '' 's#<string>export</string>#<string>upload</string>#' "$RUNNER_TEMP/export.plist"
  xcodebuild -exportArchive -archivePath "$ARCHIVE" -exportOptionsPlist "$RUNNER_TEMP/export.plist" \
    -exportPath "$EXPORT-upload" -authenticationKeyPath "$ASC_KEY_PATH" \
    -authenticationKeyID "$ASC_KEY_ID" -authenticationKeyIssuerID "$ASC_ISSUER_ID" \
    > "$LOGS/upload-xcodebuild.log" 2>&1 || { tail -60 "$LOGS/upload-xcodebuild.log"; exit 1; }
  tail -5 "$LOGS/upload-xcodebuild.log"
fi
echo "uploaded build $BUILD_NUMBER of $BUNDLE_ID"
