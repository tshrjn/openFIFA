#!/usr/bin/env bash
# Headless AI v AI match -> commentary cue log -> mixed audio demo (needs the rendered clips and the TTS venv).
#   tools/commentary/run_demo.sh [seed] [halfSeconds] [homeIdx] [awayIdx]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
U="${UNITY_EDITOR_DATA:-$HOME/Unity/6000.3.10f1/Editor/Data}"
DOTNET="$U/NetCoreRuntime/dotnet"; CSC="$U/DotNetSdkRoslyn/csc.dll"
FW="$(ls -d "$U"/NetCoreRuntime/shared/Microsoft.NETCore.App/*/ | head -1)"
OUT="$ROOT/build/commentary-demo"; mkdir -p "$OUT"
refs=(); for f in "$FW"*.dll; do case "$(basename "$f")" in Microsoft.VisualBasic*|*Native*) ;; *) refs+=("-r:$f");; esac; done
core=$(grep -rL 'using UnityEngine' "$ROOT/OpenFifa/Assets/Scripts/Core" --include=*.cs || true)
"$DOTNET" "$CSC" -nologo -nowarn:1591,0618,0414,0169,0649 -langversion:9 -out:"$OUT/CommentaryDemo.dll" -noconfig -nostdlib "${refs[@]}" $core "$ROOT/tools/commentary/demo/CommentaryDemo.cs" >"$OUT/build.log" || { cat "$OUT/build.log"; exit 1; }
cat > "$OUT/CommentaryDemo.runtimeconfig.json" <<JSON
{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"$(basename "$FW")"}}}
JSON
"$DOTNET" "$OUT/CommentaryDemo.dll" "$ROOT/OpenFifa" "$OUT/cues.jsonl" "${1:-7}" "${2:-120}" "${3:-0}" "${4:-1}"
PY="${TTS_PYTHON:-$HOME/GameDev/.tts/venv/bin/python}"
"$PY" "$ROOT/tools/commentary/mix_demo.py" "$OUT/cues.jsonl" "$ROOT/OpenFifa/Assets/StreamingAssets/Commentary/audio/en" "$OUT/commentary_demo"
