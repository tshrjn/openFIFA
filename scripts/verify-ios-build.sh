#!/bin/bash
# OpenFifa iOS Build Verification Script
# Run after Unity generates an iOS build to verify it compiles and meets constraints.
#
# Usage: ./scripts/verify-ios-build.sh [build_path]
# Default build_path: ./build/iOS

set -e

BUILD_PATH="${1:-./build/iOS}"
MAX_BUNDLE_SIZE_MB=200

echo "=== OpenFifa iOS Build Verification ==="
echo "Build path: $BUILD_PATH"
echo ""

# 1. Xcode project exists
echo "[1/5] Checking Xcode project..."
if [ ! -f "$BUILD_PATH/Unity-iPhone.xcodeproj/project.pbxproj" ]; then
    echo "FAIL: Xcode project not found at $BUILD_PATH/Unity-iPhone.xcodeproj"
    exit 1
fi
echo "  OK: Xcode project found"

# 2. Xcode build compiles for iOS Simulator
echo "[2/5] Building for iOS Simulator..."
xcodebuild -project "$BUILD_PATH/Unity-iPhone.xcodeproj" \
    -scheme "Unity-iPhone" \
    -destination 'platform=iOS Simulator,name=iPhone 15 Pro' \
    -configuration Debug \
    build 2>&1 | tee /tmp/openfifa-build.log

if [ ${PIPESTATUS[0]} -ne 0 ]; then
    echo "FAIL: Xcode build failed"
    grep "error:" /tmp/openfifa-build.log || true
    exit 1
fi
echo "  OK: Xcode build succeeded"

# 3. No build errors
echo "[3/5] Checking for build errors..."
if grep -q "error:" /tmp/openfifa-build.log; then
    echo "FAIL: Build errors detected:"
    grep "error:" /tmp/openfifa-build.log
    exit 1
fi
echo "  OK: No build errors"

# 4. App bundle size check
echo "[4/5] Checking bundle size..."
APP_PATH=$(find "$BUILD_PATH" -name "*.app" -path "*/Debug-iphonesimulator/*" | head -1)
if [ -z "$APP_PATH" ]; then
    echo "WARN: Could not find .app bundle for size check (may not be built yet)"
else
    APP_SIZE=$(du -sm "$APP_PATH" | cut -f1)
    if [ "$APP_SIZE" -gt "$MAX_BUNDLE_SIZE_MB" ]; then
        echo "FAIL: App size ${APP_SIZE}MB exceeds ${MAX_BUNDLE_SIZE_MB}MB budget"
        exit 1
    fi
    echo "  OK: App size ${APP_SIZE}MB (budget: ${MAX_BUNDLE_SIZE_MB}MB)"
fi

# 5. Required frameworks present
echo "[5/5] Checking required frameworks..."
PBXPROJ="$BUILD_PATH/Unity-iPhone.xcodeproj/project.pbxproj"
REQUIRED_FRAMEWORKS="Metal UIKit AVFoundation"
for framework in $REQUIRED_FRAMEWORKS; do
    if ! grep -q "$framework" "$PBXPROJ"; then
        echo "FAIL: Missing required framework: $framework"
        exit 1
    fi
    echo "  OK: $framework framework found"
done

echo ""
echo "=== iOS Build Verification PASSED ==="
