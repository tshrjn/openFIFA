# iPad build, test and TestFlight pipeline

OpenFifa is built as an **iPad-only** app from the Linux VPS, with no Mac. The pipeline reuses the owner's
ios-factory setup (`~/ios-factory`): the same `macos-26` runner, the same App Store Connect (ASC) API key,
the same distribution certificate, and profile naming. Status: **written and linted but never run.** See
"Unverified" below.

```
VPS                                  GitHub Actions (tshrjn/openFIFA, ipad.yml)                    Apple
ci/ipad/ship.sh
  checks secrets, app record,
  refreshes the App Store profile
  -> gh workflow run ipad.yml ---->  export   (ubuntu-latest, GameCI unity-builder, Unity 6000.3.10f1)
                                       IosBuild.ExportFromCommandLine -> device + simulator Xcode projects
                                     simulator (macos-26, Xcode 26): build -sdk iphonesimulator, boot iPad,
                                       install, launch, screenshots at 10/25/45 s, 20 s video, crash check
                                     upload   (macos-26, Xcode 26): archive, export IPA, altool upload ---> App Store Connect
  <- gh run download: shots, logs
  -> asc wait / asc internal (ios-factory) ------------------------------------------------------> TestFlight
  <- TESTFLIGHT_URL, in OpenFifa/.build/<n>/result.env
```

## Files

| File | Role |
|---|---|
| `OpenFifa/Assets/Editor/Build/IosBuild.cs` | `-executeMethod` entry points: `ExportFromCommandLine`, `ExportDevice`, `ExportSimulator`. Applies the iOS settings in code, exports, then restores them so `ProjectSettings/` is never modified |
| `.../IosBuildPostprocess.cs` | After our exports only: Info.plist keys, placeholder icon, per-target manual signing, `openfifa-export.env` |
| `.../IosPlistXml.cs`, `IosSettingsSnapshot.cs`, `IosExportContext.cs`, `IosExportKind.cs` | Helpers |
| `.github/workflows/ipad.yml` | `workflow_dispatch` with inputs `build_number`, `upload` (default false), `screenshots` (default true) |
| `ci/ipad/select-xcode.sh` | Picks the newest `/Applications/Xcode_26*.app` and fails if it is not Xcode 26 |
| `ci/ipad/prepare-xcode.sh` | Untars the export (tar keeps exec bits) and re-applies `chmod +x` to `*.sh`, `usymtool*` and the IL2CPP binaries |
| `ci/ipad/sim-smoke.sh` | Simulator build, launch check, screenshots and video (modelled on ios-factory `ci/screenshots.sh`) |
| `ci/ipad/signing.sh`, `ci/ipad/archive-upload.sh` | ios-factory's signing and upload steps adapted to `Unity-iPhone.xcodeproj` |
| `ci/ipad/ship.sh` | VPS helper modelled on `ios-ship`: checks, dispatch, wait, download into `OpenFifa/.build/<n>/` |
| `ci/ipad/sync-secrets.sh` | One-time: copies ios-factory's signing secrets to the openFIFA repo |
| `ci/ipad/AppIcon-1024.png` | Placeholder icon from ios-factory `make-icon`, used only while the Unity project has no iOS icon |

## Settings applied at build time (IosBuild.cs)

- Bundle ID `com.tshrjn.openfifa`, following ios-factory's `com.tshrjn.<slug>` convention. Override with `-ipadBundleId`.
- Target device **iPad only**. **Landscape left and right only**. `UIRequiresFullScreen=YES`: a landscape-only iPad app has to opt out of multitasking.
- IL2CPP, ARM64, Metal only, IL2CPP code generation "optimize size" to cut Xcode compile time, minimum iOS 16.0.
- `CFBundleVersion` comes from `-ipadBuildNumber` (ship.sh passes 10-second ticks, as ios-ship does). `-ipadMarketingVersion` is optional.
- `ITSAppUsesNonExemptEncryption=false` is written into Info.plist by the postprocess step.
- Device export: `DeviceSDK`, with manual signing on the app target: `Apple Distribution` plus the profile `ios-factory com.tshrjn.openfifa`, which is the name ios-factory's `asc profile` creates. `UnityFramework` gets the identity but no profile. `DEVELOPMENT_TEAM` comes from the repo variable `APPLE_TEAM_ID`.
- Simulator export: `SimulatorSDK` with ARM64, unsigned.

## How to run (from the VPS, after the one-time steps)

```bash
cd ~/GameDev/openfifa-fidelity
ci/ipad/ship.sh --check            # export + iPad simulator screenshots (needs only the Unity secrets)
ci/ipad/ship.sh                    # also TestFlight; prints TESTFLIGHT_URL
ci/ipad/ship.sh --ref main --no-shots
```

`ship.sh` never pushes. The ref has to be on GitHub already. Results go to `OpenFifa/.build/<n>/`: `shots/`
(PNGs, `smoke.mp4`, `status.txt`), `logs-*`, `failed.log` and `result.env`.
**`OpenFifa/.build/` is not gitignored.** The existing `**/[Bb]uild/` rule does not match `.build`. The owner should add `OpenFifa/.build/` to `.gitignore`.

## Costs

- **openFIFA is a public repo**, so standard GitHub-hosted runners (Linux and `macos-26`) are free and
  artifact storage does not count against a quota. Each run currently costs $0.
- If the repo goes private, macOS bills at $0.062/min and counts 10x against included minutes (2,000 on Free,
  3,000 on Pro). Estimated runner time, not measured: export 10-25 min on Linux (about $0.10), simulator job
  20-35 min on macOS, upload job 20-35 min on macOS. That is about **$2.50-4.50 per check+TestFlight run** and
  about $1.50-2 for `--check` alone. A private repo on the Free plan would use up its included minutes in one or two runs.
- To save minutes: use `--no-shots` for upload-only runs, and dispatch by hand only (the workflow has no push trigger).

## One-time steps for the owner (each needs your go-ahead)

1. **Unity licence secrets on tshrjn/openFIFA.** `gh secret list -R tshrjn/openFIFA` shows **no secrets at all** today.
   That means the existing `openfifa-ci.yml` cannot activate Unity either. Add `UNITY_LICENSE` (the contents of the Personal
   `.ulf`, `~/.local/share/unity3d/Unity/Unity_lic.ulf` after activating in Unity Hub), `UNITY_EMAIL` and `UNITY_PASSWORD`.
   Use `UNITY_SERIAL` instead if you have Plus/Pro (this requires a small workflow change).
2. **ios-factory's own one-time setup**, which has not been done yet: `~/ios-factory/.secrets` holds only `fastlane-home/`,
   `factory.env` does not exist, and `tshrjn/ios-factory` has no secrets or variables. So:
   create an ASC API key with Admin role, `scp` `AuthKey_<KEYID>.p8` plus `asc.env` into `~/ios-factory/.secrets/` (see
   ios-factory README), then run `~/ios-factory/bin/ios-setup` (it creates `factory.env`, `APPLE_TEAM_ID` and the
   distribution cert).
3. **Copy the signing secrets to openFIFA:** `ci/ipad/sync-secrets.sh` sets `DIST_P12_B64`, `DIST_P12_PASSWORD`,
   `ASC_KEY_ID`, `ASC_ISSUER_ID`, `ASC_KEY_P8_B64` and the variable `APPLE_TEAM_ID`. `IOS_PROFILE_B64` is set
   automatically by `ship.sh` from `asc profile` on the first upload run.
4. **App record for `com.tshrjn.openfifa`.** Apple's API cannot create apps:
   `~/ios-factory/bin/asc bundle com.tshrjn.openfifa "OpenFifa"`, then either `ios-apple-login` (2FA, typed by you) followed by
   `~/ios-factory/bin/ios-create-app com.tshrjn.openfifa "OpenFifa" openfifa`, or create it on
   appstoreconnect.apple.com (iOS, bundle `com.tshrjn.openfifa`, SKU `openfifa`). The name "OpenFifa" may be taken
   or trigger trademark review. `ios-create-app` retries with a suffix. Note that ios-factory's spare `slot` records
   use other bundle IDs, so claiming one would mean changing `-ipadBundleId`.
5. **Merge `ipad.yml` to `main` once.** `workflow_dispatch` only sees workflows on the default branch. After that,
   `--ref feature/...` runs the branch's copy.
6. **TestFlight app** on the iPad, signed in as the ASC account holder (the internal tester `asc internal` adds).
7. Add `OpenFifa/.build/` to `.gitignore`.

## Unverified / open risks

- **Never executed.** Validation so far: the C# compiles against the Unity 6000.3.10f1 editor DLLs. The plist helper
  was checked with a throwaway test. `ipad.yml` passes YAML parsing and actionlint. The scripts pass `bash -n`.
  shellcheck is not installed. The `#if UNITY_IOS` signing block (which uses `UnityEditor.iOS.Xcode.PBXProject`) was **not**
  compiled, because the iOS module is not installed locally. `archive-upload.sh` refuses to archive if the profile setting is missing.
- The GameCI image `unityci/editor:ubuntu-6000.3.10f1-ios-3` exists on Docker Hub (checked). Unity 6000.3.10f1 is at or above 8f1, so UIScene support is covered.
- **Metal in the iPad simulator on GitHub's virtualised Macs** is unconfirmed. If Unity cannot create a Metal device there,
  the smoke test will report a crash or black screenshots even though the device build is fine.
- Simulator screenshots of a landscape app may come out in portrait framebuffer orientation (rotated). Simulator frame rates mean nothing.
- Signing a Unity project with manual per-target settings plus `exportArchive` is the standard approach but untested here. If
  UnityFramework signing complains, the next thing to try is `CODE_SIGNING_ALLOWED=NO` on the framework target.
- The placeholder icon replaces Unity's AppIcon set only when no iOS icon is configured. Swap in real art before an external TestFlight build.
- Packages: `com.coplaydev.coplay` is fetched from a GitHub `#beta` branch, and `com.unity.purchasing`/`ads` are present. Either could break
  a headless iOS export.
- The existing `openfifa-ci.yml` calls `BuildScript.BuildIPad` and `BuildScript.BuildMacOS`, which do not exist (the real methods are
  `BuildIOS` and `BuildMacOSDev`). It also runs Unity on `macos-latest`. `ipad.yml` is independent of it.
- The 22-player performance target and ProMotion (120 Hz) can only be checked on a real iPad via TestFlight.
- Apple rate-limits frequent uploads, so keep TestFlight uploads to a few a day.
