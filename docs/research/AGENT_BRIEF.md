# Build brief for parallel agents (feature/fidelity-core)

Goal: move OpenFifa toward EA FC-level gameplay on iPad (11v11), with commentary and FC Classic-style controls,
using only original assets and no EA branding, audio or data.

Rules for every agent:
- Work ONLY in ~/GameDev/openfifa-fidelity (branch feature/fidelity-core). Never touch ~/GameDev/openfifa,
  ~/routelabs, CI workflows, scripts/, Materials/, shaders, or kit/jersey code (another thread owns kits and offside presentation).
- Stay inside the folders assigned to you. Do NOT git commit, stash, reset or checkout; the coordinator commits.
- Read CLAUDE.md (repo conventions) and the relevant section of
  docs/research/reports/Football game fidelity research.md before writing code.
- Pure logic goes under OpenFifa/Assets/Scripts/Core/<YourArea>/ (asmdef OpenFifa.Core, noEngineReferences: no UnityEngine).
  Namespace OpenFifa.Core. Use the shared OpenFifa.Core.SimVec3 and PitchSpec in Scripts/Core/Sim/ (do not edit them;
  if you need more math, add a static helper in your own folder).
- Determinism: no unseeded System.Random, no DateTime, no unordered Dictionary iteration in sim logic.
- Tests: NUnit EditMode tests in OpenFifa/Assets/Tests/Editor/<YourArea>/, named Method_Scenario_Expected,
  [Category("FID_<AREA>")] (underscore: Unity NUnit rejects "-" in category names). Unity is not licensed yet, so also prove behaviour now:
  - compile check:
    `D=~/Unity/6000.3.10f1/Editor/Data/MonoBleedingEdge; cd OpenFifa/Assets/Scripts/Core && $D/bin/mono $D/lib/mono/4.5/csc.exe -nologo -langversion:9 -target:library -out:<scratch>/core.dll -recurse:*.cs`
  - logic check: write a throwaway console Main in YOUR scratch dir that references core.dll
    (`csc -r:<scratch>/core.dll`) and asserts the key behaviours; run it with `$D/bin/mono`. Do not commit it.
  - The Core compile must stay green for everyone. If it fails because of another agent's in-progress file, compile
    only your files plus Scripts/Core/Sim/SimVec3.cs and PitchSpec.cs.
- No new third-party code or assets unless your brief says so; record any you add (URL, license) in docs/ASSETS.md
  under a heading for your area.
- Finish with a short report: files added, what is verified (and how), what is untested, open questions.
