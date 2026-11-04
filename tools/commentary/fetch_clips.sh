#!/usr/bin/env bash
# Installs the commentary clips that match the committed audio/en/index.json (GitHub Release asset, sha256-checked).
# Run before a Unity build that should speak; captures and tests do not need it (they read only index.json).
set -euo pipefail
exec python3 "$(dirname "$0")/clips.py" fetch "$@"
