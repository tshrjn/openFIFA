#!/usr/bin/env bash
# Compiles the pure-C# Core (files without `using UnityEngine`) plus the chosen EditMode test folders with Unity's
# bundled .NET runtime and Roslyn, then runs the NUnit tests with a tiny reflection runner. No editor needed.
#   tools/commentary/run_core_tests.sh [TestDir ...]   (default: Commentary Integration)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
U="${UNITY_EDITOR_DATA:-$HOME/Unity/6000.3.10f1/Editor/Data}"
DOTNET="$U/NetCoreRuntime/dotnet"
CSC="$U/DotNetSdkRoslyn/csc.dll"
FW="$(ls -d "$U"/NetCoreRuntime/shared/Microsoft.NETCore.App/*/ | head -1)"
NUNIT="${NUNIT_DLL:-$(find "$HOME/GameDev" -path '*com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll' 2>/dev/null | head -1)}"
OUT="$ROOT/build/cstest"
rm -rf "$OUT"; mkdir -p "$OUT"
dirs=("$@"); [ ${#dirs[@]} -eq 0 ] && dirs=(Commentary Integration)
refs=(); for f in "$FW"*.dll; do case "$(basename "$f")" in Microsoft.VisualBasic*|*Native*) ;; *) refs+=("-r:$f");; esac; done
core=$(grep -rL 'using UnityEngine' "$ROOT/OpenFifa/Assets/Scripts/Core" --include=*.cs || true)
"$DOTNET" "$CSC" -nologo -nowarn:1591,0618,0414,0169,0649 -langversion:9 -target:library -out:"$OUT/Core.dll" -noconfig -nostdlib "${refs[@]}" $core >"$OUT/core.log" || { cat "$OUT/core.log"; exit 1; }
tests=(); for d in "${dirs[@]}"; do while IFS= read -r f; do tests+=("$f"); done < <(find "$ROOT/OpenFifa/Assets/Tests/Editor/$d" -name "*.cs"); done
tests+=("$ROOT/OpenFifa/Assets/Tests/Editor/Rosters/RosterTestData.cs")
"$DOTNET" "$CSC" -nologo -nowarn:1591,0618 -langversion:9 -target:library -out:"$OUT/Tests.dll" -noconfig -nostdlib "${refs[@]}" -r:"$OUT/Core.dll" -r:"$NUNIT" "${tests[@]}" >"$OUT/tests.log" || { cat "$OUT/tests.log"; exit 1; }
cp "$NUNIT" "$OUT/"
cat > "$OUT/Runner.cs" <<'CS'
using System; using System.Linq; using System.Reflection; using NUnit.Framework;
static class Runner { static int Main(string[] a) {
  var asm = Assembly.LoadFrom(a[0]); int pass=0, fail=0; string filter = a.Length>1 ? a[1] : null;
  foreach (var t in asm.GetTypes().Where(t => t.GetCustomAttributes(typeof(TestFixtureAttribute), true).Any())) {
    foreach (var m in t.GetMethods().Where(m => m.GetCustomAttributes(typeof(TestAttribute), true).Any() || m.GetCustomAttributes(typeof(TestCaseAttribute), true).Any())) {
      if (filter != null && !(t.Name+"."+m.Name).Contains(filter)) continue;
      var cases = m.GetCustomAttributes(typeof(TestCaseAttribute), true).Cast<TestCaseAttribute>().Select(c => c.Arguments).ToList();
      if (cases.Count == 0) cases.Add(null);
      foreach (var args in cases) {
        var o = Activator.CreateInstance(t);
        try {
          foreach (var s in t.GetMethods().Where(x => x.GetCustomAttributes(typeof(SetUpAttribute), true).Any())) s.Invoke(o, null);
          m.Invoke(o, args); pass++;
        } catch (TargetInvocationException e) {
          var ie = e.InnerException; if (ie is SuccessException || ie is IgnoreException || ie is InconclusiveException) { pass++; continue; }
          fail++; Console.WriteLine("FAIL " + t.Name + "." + m.Name + ": " + ie.Message.Split('\n')[0] + (ie is AssertionException ? "" : "\n" + ie.StackTrace?.Split('\n').FirstOrDefault()));
        } finally { foreach (var s in t.GetMethods().Where(x => x.GetCustomAttributes(typeof(TearDownAttribute), true).Any())) try { s.Invoke(o, null); } catch {} }
      }
    }
  }
  Console.WriteLine("passed " + pass + ", failed " + fail); return fail == 0 ? 0 : 1; } }
CS
"$DOTNET" "$CSC" -nologo -langversion:9 -out:"$OUT/Runner.dll" -noconfig -nostdlib "${refs[@]}" -r:"$NUNIT" "$OUT/Runner.cs" >"$OUT/runner.log" || { cat "$OUT/runner.log"; exit 1; }
cat > "$OUT/Runner.runtimeconfig.json" <<JSON
{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"$(basename "$FW")"}}}
JSON
cd "$ROOT/OpenFifa"   # tests resolve Assets/... relative paths from the Unity project root
"$DOTNET" "$OUT/Runner.dll" "$OUT/Tests.dll" ${TEST_FILTER:-}
