#!/usr/bin/env bash
# Usage: tools/perfbench/run.sh [matches] [seed]   (INTEGRATION_DIR optionally overrides where Core/Integration lives)
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"; root="$(cd "$here/../.." && pwd)"
U="${UNITY_EDITOR_DATA:-$HOME/Unity/6000.3.10f1/Editor/Data}"
DOTNET="$U/NetCoreRuntime/dotnet"; CSC="$U/DotNetSdkRoslyn/csc.dll"
FX="$(ls -d "$U"/NetCoreRuntime/shared/Microsoft.NETCore.App/*/ | tail -1)"
out="${OUT_DIR:-$here/bin}"; mkdir -p "$out"
core="$root/OpenFifa/Assets/Scripts/Core"
mapfile -t srcs < <(find "$core" -name '*.cs' ${INTEGRATION_DIR:+-not -path "$core/Integration/*"})
[ -n "${INTEGRATION_DIR:-}" ] && mapfile -t -O "${#srcs[@]}" srcs < <(find "$INTEGRATION_DIR" -name '*.cs')
refs=(); for f in "$FX"*.dll; do case "$(basename "$f")" in Microsoft.VisualBasic*|mscorlib.dll|netstandard.dll|System.Private.*) ;; *) refs+=("-r:$f");; esac; done
refs+=("-r:$FX/System.Private.CoreLib.dll" "-r:$FX/netstandard.dll" "-r:$FX/mscorlib.dll")
"$DOTNET" "$CSC" -nologo -nowarn:CS1591,CS0618,CS8632 -optimize+ -langversion:9 -nostdlib -noconfig -target:exe -out:"$out/perfbench.dll" "${refs[@]}" "$here/Bench.cs" "${srcs[@]}" >&2
v="$(basename "$FX")"
printf '{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"},"configProperties":{"System.GC.Concurrent":false,"System.Runtime.TieredCompilation":false}}}' "$v" > "$out/perfbench.runtimeconfig.json"
exec "$DOTNET" "$out/perfbench.dll" "$@"
