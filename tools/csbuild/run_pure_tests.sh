#!/usr/bin/env bash
# Runs every EditMode test file that only needs Core (no UnityEngine/UnityEditor/Gameplay usings) under .NET, outside Unity.
set -uo pipefail
ROOT="$(cd "${1:-$(dirname "$0")/../..}" && pwd)"
U="$HOME/Unity/6000.3.10f1/Editor/Data"; DOTNET="$U/NetCoreRuntime/dotnet"; CSC="$U/DotNetSdkRoslyn/csc.dll"
FW="$(ls -d "$U"/NetCoreRuntime/shared/Microsoft.NETCore.App/*/ | head -1)"
NUNIT="$(find "$ROOT/OpenFifa/Library/PackageCache" -path '*com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll' | head -1)"
OUT="${OUT:-$ROOT/build/csbuild/pure}"; rm -rf "$OUT"; mkdir -p "$OUT"
refs=(); for f in "$FW"*.dll; do case "$(basename "$f")" in Microsoft.VisualBasic*|*Native*) ;; *) refs+=("-r:$f");; esac; done
core=$(find "$ROOT/OpenFifa/Assets/Scripts/Core" -name '*.cs')
"$DOTNET" "$CSC" -nologo -nowarn:1591,0618,0414,0169,0649,8632 -langversion:9 -target:library -out:"$OUT/Core.dll" -noconfig -nostdlib "${refs[@]}" $core >"$OUT/core.log" || { cat "$OUT/core.log" | grep error | head; exit 1; }
mapfile -t tests < <(grep -rL 'using UnityEngine\|using UnityEditor\|OpenFifa\.Gameplay\|OpenFifa\.UI\|OpenFifa\.AI\|using OpenFifa\.Editor\|UnityEngine\.' "$ROOT/OpenFifa/Assets/Tests/Editor" --include=*.cs)
"$DOTNET" "$CSC" -nologo -nowarn:1591,0618,8632 -langversion:9 -target:library -out:"$OUT/Tests.dll" -noconfig -nostdlib "${refs[@]}" -r:"$OUT/Core.dll" -r:"$NUNIT" "${tests[@]}" >"$OUT/tests.log" || { grep error "$OUT/tests.log" | head -20; exit 1; }
echo "compiled ${#tests[@]} pure test files"
cp "$NUNIT" "$OUT/"
sed -n '/^cat > "\$OUT\/Runner.cs"/,/^CS$/p' "$ROOT/tools/commentary/run_core_tests.sh" | sed '1d;$d' > "$OUT/Runner.cs"
"$DOTNET" "$CSC" -nologo -langversion:9 -out:"$OUT/Runner.dll" -noconfig -nostdlib "${refs[@]}" -r:"$NUNIT" "$OUT/Runner.cs" >"$OUT/runner.log" || { cat "$OUT/runner.log"; exit 1; }
printf '{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$(basename "$FW")" > "$OUT/Runner.runtimeconfig.json"
cd "$ROOT/OpenFifa"; "$DOTNET" "$OUT/Runner.dll" "$OUT/Tests.dll" ${TEST_FILTER:-}
