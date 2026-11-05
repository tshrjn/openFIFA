#!/usr/bin/env bash
# Recompiles every OpenFifa asmdef assembly with Unity's own compiler arguments (Library/Bee/*.rsp from the last editor
# compile: defines, package and engine references) but the current source files. No editor or licence needed.
set -uo pipefail
P="$(cd "${1:-$(dirname "$0")/../..}" && pwd)/OpenFifa"; cd "$P"
U="$HOME/Unity/6000.3.10f1/Editor/Data"; DOTNET="$U/NetCoreRuntime/dotnet"; CSC="$U/DotNetSdkRoslyn/csc.dll"
OUT="${OUT:-$P/../build/csbuild/asm}"; rm -rf "$OUT"; mkdir -p "$OUT"
DAG=$(ls -d Library/Bee/artifacts/*E.dag | head -1)
fail=0
build() { # name asmdef-dir
  local name="$1" dir="$2" rsp="$DAG/$1.rsp"
  [ -f "$rsp" ] || { rsp="$DAG/OpenFifa.Gameplay.rsp"; }
  local nested; nested=$(find "$dir" -mindepth 2 -name '*.asmdef' -printf '%h\n')
  local new="$OUT/$name.rsp"
  grep -v '^"Assets\|^-out:\|^-refout:\|OpenFifa\.[A-Za-z.]*\.\(dll\|ref\.dll\)"\?$\|^-warnaserror' "$rsp" > "$new"
  # Package/engine references: union over all editor-dag response files (main may have added asmdef references).
  cat "$DAG"/*.rsp | grep '^-r:' | grep -v 'OpenFifa\.' | sort -u > "$OUT/allrefs.txt"
  grep -v '^-r:' "$new" > "$new.tmp"; cat "$OUT/allrefs.txt" >> "$new.tmp"; mv "$new.tmp" "$new"
  local asmdef; asmdef=$(find "$dir" -maxdepth 1 -name '*.asmdef' | head -1)
  for r in $(python3 -c "import json;print(' '.join(x for x in json.load(open('$asmdef')).get('references',[]) if x.startswith('OpenFifa')))"); do
    echo "-r:\"$OUT/$r.dll\"" >> "$new"; done
  echo "-out:\"$OUT/$name.dll\"" >> "$new"
  while IFS= read -r f; do local skip=0; for n in $nested; do [[ "$f" == "$n"/* ]] && skip=1; done; [ $skip = 0 ] && echo "\"$f\"" >> "$new"; done < <(find "$dir" -name '*.cs' | sort)
  if "$DOTNET" "$CSC" -nologo -noconfig @"$new" > "$OUT/$name.log" 2>&1; then
    echo "OK   $name ($(grep -c '^"Assets' "$new") files, $(grep -c ': warning' "$OUT/$name.log") warnings)"
  else echo "FAIL $name"; grep ': error' "$OUT/$name.log" | sort -u | head -20; fail=1; fi
}
S=Assets/Scripts
build OpenFifa.Core $S/Core
build OpenFifa.AI $S/AI
build OpenFifa.Gameplay $S/Gameplay
build OpenFifa.UI $S/UI
build OpenFifa.Audio $S/Audio
build OpenFifa.Editor Assets/Editor
build OpenFifa.Animations.Editor Assets/Animations/Editor
build OpenFifa.SinglePlayer.Editor $S/Gameplay/SinglePlayer/Editor
[ -d $S/Gameplay/Stadium/Editor ] && build OpenFifa.Stadium.Editor $S/Gameplay/Stadium/Editor
[ -d $S/Gameplay/LocalMultiplayer/Editor ] && build OpenFifa.LocalMultiplayer.Editor $S/Gameplay/LocalMultiplayer/Editor
build OpenFifa.Tests.Editor Assets/Tests/Editor
exit $fail
