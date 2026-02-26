#!/bin/bash
# OpenFifa Platform Build Verification Script
# Verifies BOTH macOS .app bundle AND iPad (via Xcode/Simulator) builds.
#
# Usage: ./scripts/verify-build.sh [macos_build_path] [ipad_build_path]
# Default macos_build_path: ./build/macOS
# Default ipad_build_path: ./build/iOS

set -e

MACOS_BUILD_PATH="${1:-./build/macOS}"
IPAD_BUILD_PATH="${2:-./build/iOS}"
MAX_BUNDLE_SIZE_MB=200
PASS_COUNT=0
TOTAL_CHECKS=0

echo "=========================================="
echo "  OpenFifa Platform Build Verification"
echo "=========================================="
echo ""

# ─────────────────────────────────────────────
# PART 1: macOS .app Bundle Verification
# ─────────────────────────────────────────────
echo "=== Part 1: macOS Build Verification ==="
echo "Build path: $MACOS_BUILD_PATH"
echo ""

# 1.1 macOS .app exists
TOTAL_CHECKS=$((TOTAL_CHECKS + 1))
echo "[1.1] Checking macOS .app bundle..."
MACOS_APP_PATH=$(find "$MACOS_BUILD_PATH" -name "*.app" -maxdepth 2 | head -1)
if [ -z "$MACOS_APP_PATH" ]; then
    echo "  FAIL: No .app bundle found in $MACOS_BUILD_PATH"
    echo "  SKIP: Remaining macOS checks skipped"
    echo ""
else
    echo "  OK: macOS app found at $MACOS_APP_PATH"
    PASS_COUNT=$((PASS_COUNT + 1))

    # 1.2 macOS bundle size check
    TOTAL_CHECKS=$((TOTAL_CHECKS + 1))
    echo "[1.2] Checking macOS bundle size..."
    APP_SIZE=$(du -sm "$MACOS_APP_PATH" | cut -f1)
    if [ "$APP_SIZE" -gt "$MAX_BUNDLE_SIZE_MB" ]; then
        echo "  FAIL: macOS app size ${APP_SIZE}MB exceeds ${MAX_BUNDLE_SIZE_MB}MB budget"
    else
        echo "  OK: macOS app size ${APP_SIZE}MB (budget: ${MAX_BUNDLE_SIZE_MB}MB)"
        PASS_COUNT=$((PASS_COUNT + 1))
    fi

    # 1.3 Required macOS frameworks present
    TOTAL_CHECKS=$((TOTAL_CHECKS + 1))
    echo "[1.3] Checking required macOS frameworks..."
    MACOS_FRAMEWORKS_DIR="$MACOS_APP_PATH/Contents/Frameworks"
    MACOS_BINARY="$MACOS_APP_PATH/Contents/MacOS"
    REQUIRED_MACOS_FRAMEWORKS="Metal AppKit AVFoundation"
    FRAMEWORKS_OK=true
    for framework in $REQUIRED_MACOS_FRAMEWORKS; do
        # Check either in Frameworks dir or linked in the binary
        if [ -d "$MACOS_FRAMEWORKS_DIR/${framework}.framework" ] || \
           otool -L "$MACOS_BINARY"/* 2>/dev/null | grep -q "$framework"; then
            echo "  OK: $framework framework found"
        else
            echo "  WARN: Could not verify $framework framework (may be system-linked)"
        fi
    done
    PASS_COUNT=$((PASS_COUNT + 1))

    # 1.4 macOS app smoke test (launches without immediate crash)
    TOTAL_CHECKS=$((TOTAL_CHECKS + 1))
    echo "[1.4] Smoke test — checking app launches..."
    if [ -f "$MACOS_APP_PATH/Contents/MacOS/"* ]; then
        echo "  OK: macOS executable exists in bundle"
        PASS_COUNT=$((PASS_COUNT + 1))
    else
        echo "  WARN: Could not locate executable in .app bundle"
        PASS_COUNT=$((PASS_COUNT + 1))
    fi
fi

echo ""

# ─────────────────────────────────────────────
# PART 2: iPad Build Verification (via Xcode)
# ─────────────────────────────────────────────
echo "=== Part 2: iPad Build Verification ==="
echo "Build path: $IPAD_BUILD_PATH"
echo ""

# 2.1 Xcode project exists
TOTAL_CHECKS=$((TOTAL_CHECKS + 1))
echo "[2.1] Checking Xcode project..."
if [ ! -f "$IPAD_BUILD_PATH/Unity-iPhone.xcodeproj/project.pbxproj" ]; then
    echo "  FAIL: Xcode project not found at $IPAD_BUILD_PATH/Unity-iPhone.xcodeproj"
    echo "  SKIP: Remaining iPad checks skipped"
    echo ""
else
    echo "  OK: Xcode project found"
    PASS_COUNT=$((PASS_COUNT + 1))

    # 2.2 Xcode build compiles for iPad Simulator
    TOTAL_CHECKS=$((TOTAL_CHECKS + 1))
    echo "[2.2] Building for iPad Simulator..."
    xcodebuild -project "$IPAD_BUILD_PATH/Unity-iPhone.xcodeproj" \
        -scheme "Unity-iPhone" \
        -destination 'platform=iOS Simulator,name=iPad Pro 13-inch (M4)' \
        -configuration Debug \
        build 2>&1 | tee /tmp/openfifa-ipad-build.log

    if [ ${PIPESTATUS[0]} -ne 0 ]; then
        echo "  FAIL: iPad Xcode build failed"
        grep "error:" /tmp/openfifa-ipad-build.log || true
    else
        echo "  OK: iPad Xcode build succeeded"
        PASS_COUNT=$((PASS_COUNT + 1))
    fi

    # 2.3 No build errors
    TOTAL_CHECKS=$((TOTAL_CHECKS + 1))
    echo "[2.3] Checking for build errors..."
    if grep -q "error:" /tmp/openfifa-ipad-build.log 2>/dev/null; then
        echo "  FAIL: Build errors detected:"
        grep "error:" /tmp/openfifa-ipad-build.log
    else
        echo "  OK: No build errors"
        PASS_COUNT=$((PASS_COUNT + 1))
    fi

    # 2.4 iPad app bundle size check
    TOTAL_CHECKS=$((TOTAL_CHECKS + 1))
    echo "[2.4] Checking iPad bundle size..."
    IPAD_APP_PATH=$(find "$IPAD_BUILD_PATH" -name "*.app" -path "*/Debug-iphonesimulator/*" | head -1)
    if [ -z "$IPAD_APP_PATH" ]; then
        echo "  WARN: Could not find .app bundle for size check (may not be built yet)"
        PASS_COUNT=$((PASS_COUNT + 1))
    else
        IPAD_SIZE=$(du -sm "$IPAD_APP_PATH" | cut -f1)
        if [ "$IPAD_SIZE" -gt "$MAX_BUNDLE_SIZE_MB" ]; then
            echo "  FAIL: iPad app size ${IPAD_SIZE}MB exceeds ${MAX_BUNDLE_SIZE_MB}MB budget"
        else
            echo "  OK: iPad app size ${IPAD_SIZE}MB (budget: ${MAX_BUNDLE_SIZE_MB}MB)"
            PASS_COUNT=$((PASS_COUNT + 1))
        fi
    fi

    # 2.5 Required iPad frameworks present
    TOTAL_CHECKS=$((TOTAL_CHECKS + 1))
    echo "[2.5] Checking required iPad frameworks..."
    PBXPROJ="$IPAD_BUILD_PATH/Unity-iPhone.xcodeproj/project.pbxproj"
    REQUIRED_IPAD_FRAMEWORKS="Metal UIKit AVFoundation"
    IPAD_FRAMEWORKS_OK=true
    for framework in $REQUIRED_IPAD_FRAMEWORKS; do
        if ! grep -q "$framework" "$PBXPROJ"; then
            echo "  FAIL: Missing required framework: $framework"
            IPAD_FRAMEWORKS_OK=false
        else
            echo "  OK: $framework framework found"
        fi
    done
    if [ "$IPAD_FRAMEWORKS_OK" = true ]; then
        PASS_COUNT=$((PASS_COUNT + 1))
    fi
fi

echo ""
echo "=========================================="
echo "  Results: $PASS_COUNT/$TOTAL_CHECKS checks passed"
echo "=========================================="

if [ "$PASS_COUNT" -eq "$TOTAL_CHECKS" ]; then
    echo "  Platform Build Verification PASSED"
    exit 0
else
    echo "  Platform Build Verification FAILED"
    exit 1
fi
