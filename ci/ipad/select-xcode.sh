#!/bin/bash
# Select the newest Xcode 26.x on the macOS runner (App Store Connect only accepts Xcode 26+ builds).
set -euo pipefail
XC=$(ls -d /Applications/Xcode_26*.app 2>/dev/null | sort -V | tail -1 || true)
[ -n "$XC" ] || XC=$(xcode-select -p | sed 's#/Contents/Developer##')
sudo xcode-select -s "$XC"
xcodebuild -version
xcodebuild -version | head -1 | grep -q 'Xcode 26' || { echo "::error::need Xcode 26, got $(xcodebuild -version | head -1)"; exit 1; }
