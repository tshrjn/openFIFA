# Unity 6 (6000.3) -> iPad build, test and TestFlight pipeline from a Linux VPS (no Mac)

Context assumed: the owner already runs "ios-factory" (Linux VPS -> GitHub Actions `macos-26` runner with Xcode 26 -> App Store Connect API key -> TestFlight) for SwiftUI apps, and the Unity repo already runs GameCI `unity-test-runner` on ubuntu with `UNITY_LICENSE` / `UNITY_EMAIL` / `UNITY_PASSWORD`. Research date: 2026-11-04. 18 tool calls were made, so some items below are marked as gaps or inferences.

## Q1. Can Unity on Linux export the iOS Xcode project, with a macOS runner doing only xcodebuild archive/sign/upload? What are the gotchas?

### Takeaway
Yes. This is the documented GameCI pattern. Job 1 runs `game-ci/unity-builder@v4` with `targetPlatform: iOS` on `ubuntu-latest` and uploads the exported `build/iOS` Xcode project as an artifact. Job 2 runs on macOS: it downloads the artifact, selects Xcode, and runs fastlane (`match` + `build_app` + `upload_to_testflight`) against `Unity-iPhone.xcodeproj` / scheme `Unity-iPhone`. The only macOS minutes are the Xcode IL2CPP compile, archive and upload. The owner's ios-factory signing (ASC API key) can be reused. Two changes are needed: pin Xcode 26.x (GameCI's doc still shows 16.4, which App Store Connect no longer accepts), and use Unity 6000.3.8f1 or later for UIScene lifecycle support.

### Cited Findings
- GameCI's iOS deployment guide has two phases. Phase 1 creates certificates with fastlane `match`, stored in a private Git repo, using an App Store Connect API key so CI avoids 2FA. In phase 2, Unity exports an Xcode project, and fastlane builds, signs and uploads it to TestFlight or the App Store — [GameCI: Deploy to the App Store](https://game.ci/docs/github/deployment/ios)
- The reference `main.yml` has two jobs. The first (`buildForiOSPlatform`) runs on **`ubuntu-latest`** with `actions/checkout@v4`, `actions/cache@v3` (caching `Library`, key `Library-iOS`), `game-ci/unity-builder@v4` (`targetPlatform: iOS`) and `actions/upload-artifact@v4` (`build/iOS`). The second (`releaseToAppStore`) runs on `macos-latest` with `needs: buildForiOSPlatform`, `actions/download-artifact@v4` and `maxim-lobanov/setup-xcode@v1` (the doc uses Xcode `16.4`). It then runs `xcodebuild -downloadPlatform iOS`, adds the SSH deploy key, `chmod +x` on `*.sh` and `usymtool*` (IL2CPP helpers lose their exec bit inside the artifact), `bundle install` and `bundle exec fastlane ios release`. `geekyeggo/delete-artifact@v2` runs with `if: always()` to stay under storage limits — [GameCI iOS](https://game.ci/docs/github/deployment/ios)
- Fastlane lanes: `build` runs `setup_ci`, `sync_certificates` (match `type: "appstore"`) and the automatic-to-manual signing switch ("iPhone Distribution" with the App Store profile), then `build_app` on `Unity-iPhone.xcodeproj`, scheme `Unity-iPhone`, `-allowProvisioningUpdates`. `beta` sets `ITSAppUsesNonExemptEncryption=false` in Info.plist, runs `build`, then `upload_to_testflight(skip_waiting_for_build_processing: true)`. If CocoaPods are used (e.g. Firebase), run `cocoapods(clean_install: true)` and build the `.xcworkspace` — [GameCI iOS](https://game.ci/docs/github/deployment/ios)
- Secrets: `APPSTORE_KEY_ID`, `APPSTORE_ISSUER_ID`, `APPSTORE_P8` (full contents of the .p8), `GH_PAT`, `MATCH_REPOSITORY`, `MATCH_PASSWORD`, `MATCH_DEPLOY_KEY`, `IOS_BUNDLE_ID`, `APPLE_CONNECT_EMAIL`, `APPLE_DEVELOPER_EMAIL`, `APPLE_TEAM_ID` — [GameCI iOS](https://game.ci/docs/github/deployment/ios)
- GameCI gotchas: Apple rate-limits uploads, so trigger TestFlight manually or a few times a day. Missing app icons cause TestFlight upload errors. The Unity bundle ID, `IOS_BUNDLE_ID` and the ASC app record must all match. Re-run `generate_certs` when certificates expire (about 1 to 2 years). match's cleanup can delete identities on a shared Apple account. A "Unknown platform" error is fixed by adjusting `PLATFORMS` in `Gemfile.lock` (`bundle lock --add-platform x86_64-linux`). The doc still recommends having a Mac for debugging — [GameCI iOS](https://game.ci/docs/github/deployment/ios)
- **Xcode version for submission:** to submit an iOS/iPadOS app you must build with Xcode 26.0 or later and the iOS 26/iPadOS 26 SDK. Older Xcode builds cannot be submitted — [Unity Build Automation: Available Xcode versions](https://docs.unity.com/en-us/build-automation/reference/available-xcode-versions)
- Unity 6.3 iOS requirements: minimum iOS 15 (set via `PlayerSettings.iOS.targetOSVersionString`), "Xcode 16 or later recommended", Metal as the graphics API, and ASTC/ETC2/ETC texture compression (DXT unsupported) — [Unity 6.3 manual: iOS requirements and compatibility](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/iphone/ios-introducing/ios-requirements-and-compatibility)
- **UIScene lifecycle:** Apple will require the UIKit scene-based lifecycle when building with the SDK released after iOS 26 (Unity expects the beta in summer 2026 and the public release in autumn 2026). The minimum versions with support are **Unity 6.3 LTS 6000.3.8f1+** (also 6000.4.0b8+, 6000.0.68f1+). Some users reported deep link and universal link regressions after updating — [Unity Discussions: UIScene lifecycle support](https://discussions.unity.com/t/info-apple-update-your-editor-to-receive-uiscene-lifecycle-support/1709065)
- Unity issue tracker: with Xcode 26 and ARKit enabled as a plug-in provider, the iOS build fails with a linker "Assertion failed". The issue is marked "By Design" for 6.3.x — [Unity Issue Tracker](https://issuetracker.unity3d.com/issues/ios-Xcode-26-build-for-ios-fails-on-assertion-failed-when-arkit-is-added-as-a-plug-in-provider)
- An Xcode 15+ `ProductName` change broke IL2CPP in older Unity versions. It is fixed in 6000.1.0a1+, 2022.3.10f1+ and later releases, so it does not affect 6000.3 — [Unity BA Xcode versions](https://docs.unity.com/en-us/build-automation/reference/available-xcode-versions)
- ProMotion: Apple says iPad Pro does not need the special plist key. `CADisableMinimumFrameDurationOnPhone` is required only on iPhone to go above 60 Hz. ProMotion devices support 120 Hz (8 ms) frame timing — [Apple: Optimizing iPhone and iPad apps to support ProMotion displays](https://developer.apple.com/documentation/quartzcore/optimizing-iphone-and-ipad-apps-to-support-promotion-displays.md)
- Older Unity versions (2017-2018) locked iOS to 60 fps. The community workaround was the Info.plist key `CADisableMinimumFrameDuration=YES`, after which an iPad ran at 120 Hz on Unity 2018.4. It is unclear whether that is still needed — [Unity forum: iPad Pro 120Hz ProMotion](https://forum.unity.com/threads/ipadpro-2017-120hz-promotion.484867/)
- Low Power Mode and thermal throttling can cap ProMotion at 60 Hz even when 120 Hz is requested (third-party guide) — [axiom-display-performance skill page](https://openskillindex.com/skills/majiayu000-claude-skill-registry-axiom-display-performance)

### Inferences
- **Recommended YAML shape, adapted from the GameCI doc to the owner's stack. This has not been run.**
  ```yaml
  # .github/workflows/ipad.yml
  on: { workflow_dispatch: {}, push: { tags: ['ipad-v*'] } }
  jobs:
    export-xcode:
      runs-on: ubuntu-latest            # Linux minutes: 1x multiplier
      steps:
        - uses: actions/checkout@v4
          with: { lfs: true }
        - uses: actions/cache@v4
          with: { path: Library, key: Library-iOS-${{ hashFiles('Packages/packages-lock.json') }} }
        - uses: game-ci/unity-builder@v4
          env:
            UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
            UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
            UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
          with:
            unityVersion: 6000.3.8f1      # or later 6000.3.x patch; >= 8f1 for UIScene
            targetPlatform: iOS
            buildMethod: Build.IPad.Export # optional custom static method (sets SDK, device family, etc.)
        - uses: actions/upload-artifact@v4
          with: { name: xcode-ios, path: build/iOS, retention-days: 1 }
    archive-testflight:
      needs: export-xcode
      runs-on: macos-26                  # same label ios-factory already uses (10x billing)
      timeout-minutes: 60
      steps:
        - uses: actions/checkout@v4
        - uses: actions/download-artifact@v4
          with: { name: xcode-ios, path: build/iOS }
        - uses: maxim-lobanov/setup-xcode@v1
          with: { xcode-version: '26' }  # NOT 16.4 as in GameCI doc
        - run: |
            find build/iOS -name '*.sh' -exec chmod +x {} \;
            find build/iOS -name 'usymtool*' -exec chmod +x {} \;
            bundle install
            bundle exec fastlane ios beta   # or reuse ios-factory's xcodebuild archive + altool/ASC-API upload
          env: { APPSTORE_KEY_ID: ..., APPSTORE_ISSUER_ID: ..., APPSTORE_P8: ..., MATCH_PASSWORD: ... }
  ```
- **iPad-only settings in Unity:** set Player Settings > iOS > Target Device = "iPad Only" (`PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPadOnly`), which should produce `UIDeviceFamily = [2]` in the exported Info.plist. Lock orientation to Landscape Left/Right, which suits a football game. iPad apps that support all four orientations are allowed to skip "Requires Full Screen", but a landscape-only iPad app has to set `UIRequiresFullScreen=YES` to opt out of multitasking. This comes from Apple App Store validation behaviour and was not re-verified in this session.
- **Bitcode** is not a concern. Xcode 14+ deprecated it, and Unity 6 exports with it off. (Background knowledge, not re-sourced.)
- **Signing:** if ios-factory already uses an ASC API key with `xcodebuild -allowProvisioningUpdates` and cloud-managed signing, that likely works for `Unity-iPhone.xcodeproj` too and removes the need for the GameCI `match` repo. It does need `DEVELOPMENT_TEAM` set (Unity's `PlayerSettings.iOS.appleDeveloperTeamID`) and the `Unity-iPhone` target to use automatic signing.
- **ProMotion in Unity 6:** set `Application.targetFrameRate = 120` (or `-1`), and keep a Player Settings / Info.plist post-process hook ready to add `CADisableMinimumFrameDuration` if a device measures a 60 Hz cap. This needs checking on real hardware.
- Pin the Unity version to 6000.3.8f1 or later. GameCI images exist per editor patch. Confirm that a `unityci/editor:ubuntu-6000.3.x-ios-3` tag exists for the chosen patch before pinning it.

### Gaps
- I did not fetch the Unity 6.3 page that lists a minimum or maximum supported Xcode for 6000.3, so Xcode 26.x compatibility comes only from the App Store rule and the issue tracker.
- The GameCI docs did not say which Docker image tag (`-ios-`) is published for each 6000.3.x patch. Check Docker Hub `unityci/editor`.
- No source confirmed how Unity 6.3's iOS player maps `Application.targetFrameRate=120` on iPad Pro, or whether the old plist key is still needed.

## Q2. Unity licensing in CI (2026) and Unity Build Automation as an alternative

### Takeaway
GameCI still documents manual `.ulf` activation for Personal licenses (`UNITY_LICENSE` + email/password), which matches what the repo already uses. The same secrets work for `unity-builder` with `targetPlatform: iOS` on Linux. Plus/Pro use `UNITY_SERIAL`, and a floating license server is supported through `unityLicensingServer`. Unity Build Automation now gives free-tier users iOS builds with 100 Mac minutes a month (since March 2026). That covers a small number of IL2CPP iOS builds, but the overage rate was not found.

### Cited Findings
- Personal license: activate in Unity Hub (Preferences > Licenses > Add > "Get a free personal license"). The `.ulf` is at `~/.local/share/unity3d/Unity/Unity_lic.ulf` on Linux. Secrets are `UNITY_LICENSE` (the .ulf contents), `UNITY_EMAIL` and `UNITY_PASSWORD`. The page warns that the .ulf may not be created unless you complete the Add flow — [GameCI Activation](https://game.ci/docs/github/activation)
- Plus/Pro: `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD`. A license server is configured with `unityLicensingServer: <url>`, which takes a floating license before the build and returns it after — [GameCI Activation](https://game.ci/docs/github/activation)
- Unity Build Automation (UBA) free usage, effective 1 March 2026: 200 Windows Standard Compute minutes and 100 Mac Standard Compute minutes per month, plus the first two concurrent machines free — [Unity Discussions: Unity DevOps updates in 2026](https://discussions.unity.com/t/unity-devops-updates-in-2026/1711090); [Unity pricing updates](https://unity.com/products/pricing-updates)
- Unity: free-tier users "will now also be able to build for iOS and macOS". Overage is billed by "standardized Minutes tiers". Storage and egress overage is $0.10/GB — [Unity support: Understanding Unity DevOps charges](https://support.unity.com/hc/en-us/articles/34748492914964-Understanding-Unity-DevOps-charges) (via search summary); a forum thread asks whether the 100 Mac minutes renew monthly — [Unity Discussions](https://discussions.unity.com/t/is-mac-build-100-free-minutes-for-unity-build-after-q1-of-2026-every-month-or-only-once/1696004)
- UBA Xcode images: Tahoe offers Xcode 26.5.0, 26.4.1 and 26.3.0. Sequoia offers 26.2.0, 26.0.1, 16.4.0 and 16.3.0. iOS targets must name a specific Xcode version. Xcode binaries are kept for about 15 months — [UBA Available Xcode versions](https://docs.unity.com/en-us/build-automation/reference/available-xcode-versions)

### Inferences
- Keep GameCI and the existing Personal .ulf secrets. No new licensing work is needed for the Linux export job. The macOS job never runs Unity, so it needs no Unity license.
- UBA is a reasonable zero-setup fallback. It builds and signs inside Unity's own pipeline, but TestFlight upload still needs a separate step or fastlane. It is not cheaper than GitHub once past 100 minutes, and it does not reuse the ios-factory tooling.
- Note that Unity Personal is free only below the revenue/funding threshold. That threshold is not checked here.

### Gaps
- UBA per-minute Mac overage price: not found.
- Whether Unity has tightened Personal .ulf generation in 2026 (some reports suggest serial-only activation for newer Hub versions): the GameCI doc still documents .ulf, and I found no contradicting primary source.

## Q3. Testing on iPad without a physical device: simulator builds, PlayMode tests on iOS, screenshots/video, profiling, device farms

### Takeaway
Unity 6 can export a **Simulator SDK** Xcode project with an **arm64** simulator architecture. This needs a *separate* Unity export, because device and simulator libraries are not interchangeable. The macOS runner can build it with `xcodebuild -sdk iphonesimulator`, boot an iPad simulator, install, launch, and capture screenshots and video with `simctl`, which mirrors ios-factory's screenshot step. The Unity Test Framework can target an iOS player with `-testPlatform iOS` and `iOSTargetSDK: SimulatorSDK` in TestSettings.json, but the run needs a Mac with the Unity Editor, so GameCI on Linux cannot run it. For real-GPU performance and controller checks, cloud farms are cheap per run: AWS Device Farm costs $0.17 per device-minute and Firebase Test Lab $5 per device-hour.

### Cited Findings
- Unity added ARM64 iOS Simulator support (UUM-2238, landed around 2022.3.54f1). Before that, the Simulator SDK architecture was locked to x86_64 — [Unity Issue Tracker UUM-2238](https://issuetracker.unity3d.com/product/unity/issues/guid/UUM-2238); [issue: simulator sdk missing arm64](https://issuetracker.unity3d.com/issues/ios-simulator-sdk-is-missing-arm64-architecture-support)
- Unity scripting docs (6000.7) say the Universal iOS/tvOS Simulator architecture is deprecated and tell users to switch to ARM64 in Player Settings — [Unity ScriptRef AppleMobileArchitectureSimulator.Universal](https://docs.unity.com/ja-jp/engine/6000.7/script-reference/unityeditor/applemobilearchitecturesimulator/universal)
- A device build's libraries cannot be reused for the simulator because they are compiled against the Device SDK. This applies to third-party native plugins too: Live2D shipped x86_64-only simulator libraries that will not run on Apple Silicon simulators — [Unity Discussions](https://discussions.unity.com/t/exporting-for-ios-simulator-but-with-arm64-arch-impossible/838328); [Live2D community](https://community.live2d.com/discussion/comment/4303)
- Unity Test Framework: `-testPlatform` accepts any BuildTarget value. Test settings include an iOS target SDK (`DeviceSDK`/`SimulatorSDK`) set in a TestSettings.json passed with `-testSettingsFile` — [UTF 2.0 command line reference](https://docs.unity3d.com/Packages/com.unity.test-framework@2.0/manual/reference-command-line.html); [Unity 6.2 manual test-framework CLI](https://docs.unity3d.com/Manual/test-framework/reference-command-line.html)
- AWS Device Farm: $0.17 per device-minute metered, or $250 per month per unmetered device slot (iOS slots priced separately). Free trial minutes are reported as 1,000 on AWS's page. The breakeven is about 1,470 min per month (third-party). A 150-minute per-run limit is reported (third-party, unverified) — [AWS Device Farm pricing](https://aws.amazon.com/device-farm/pricing); [devicelab.dev 2026](https://devicelab.dev/blog/aws-device-farm-pricing-2026)
- Firebase Test Lab: physical devices cost $5/device-hour on Blaze, billed per minute and rounded up. Spark's free quota is about 5 physical-device runs a day (third-party). iOS supports XCTest/XCUITest and **Game Loop** tests, where the app implements a scripted loop, which suits Unity games. The iOS device catalog is smaller than Android's — [Firebase Test Lab pricing](https://firebase.google.com/docs/test-lab/usage-quotas-pricing); [Firebase iOS get started](https://firebase.google.com/docs/test-lab/ios/get-started?authuser=3); [back4app blog](https://blog.back4app.com/firebase-test-lab/)

### Inferences
- **Simulator smoke + screenshot job (macOS), modelled on ios-factory:**
  1. Linux job: second `unity-builder` run with a `buildMethod` that sets `PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK` and `PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64`, then exports to `build/iOS-sim`.
  2. macOS job: `xcodebuild -project build/iOS-sim/Unity-iPhone.xcodeproj -scheme Unity-iPhone -sdk iphonesimulator -configuration Release -destination 'platform=iOS Simulator,name=iPad Pro 13-inch (M4)' CODE_SIGNING_ALLOWED=NO build`. Then `xcrun simctl boot`, `simctl install booted <app>`, `simctl launch booted <bundle-id>`, `sleep`, `xcrun simctl io booted screenshot shot.png`, and `xcrun simctl io booted recordVideo match.mp4` (kill it after N seconds). The game can take scripted scenes via launch arguments, e.g. `simctl launch booted id -scene Kickoff -autoplay 30`.
  3. Simulator frame rates are not representative, because the simulator renders through the Mac's GPU. Use it for crash, layout, UI and safe-area checks, not for 120 Hz or perf numbers.
- **PlayMode on iOS:** the practical CI option is to keep PlayMode/EditMode tests on Linux (current GameCI test-runner) and add a simulator "smoke" scene that logs PASS/FAIL to stdout, read with `xcrun simctl launch --console`. Running UTF against an iOS player requires the Unity Editor on the macOS runner, which brings Unity licensing onto macOS and roughly doubles macOS minutes.
- **Perf profiling** with no device in the loop is effectively impossible. Use Firebase Game Loop or AWS Device Farm runs on real iPads, with the game writing its own frame-time CSV for capture, or buy one iPad and use TestFlight + Xcode Instruments over the network (that still needs a Mac). Unity's Profiler can connect over Wi-Fi to a Development build, but the Editor and the device must be on the same network, which is not possible from a remote VPS.
- BrowserStack App Automate was not researched (tool budget).

### Gaps
- Whether Metal rendering of a URP Unity 6 game works fully in the iPad simulator on GitHub's virtualized macOS runners (paravirtualized GPU). It is not confirmed and needs a trial run. GitHub's macOS VMs may lack full Metal feature support.
- BrowserStack App Automate pricing and Unity support: not researched.
- Firebase Game Loop specifics for iOS (URL scheme `firebase-game-loop://`): not verified this session.

## Q4. TestFlight distribution of a game: export compliance, size, automation, internal testers

### Takeaway
Use the same ASC API key as ios-factory. Set `ITSAppUsesNonExemptEncryption=false` in the exported Info.plist (GameCI's `beta` lane does this) to skip the export-compliance prompt. Upload with fastlane `upload_to_testflight` / `pilot`, or with `xcrun altool`/ASC API as ios-factory already does. Notarization (`notarytool`) is not needed for iOS. Internal testers (members of the App Store Connect team) get builds right after processing, with no beta review.

### Cited Findings
- GameCI `beta` lane: `update_info_plist` sets `ITSAppUsesNonExemptEncryption` to false, then `build`, then `upload_to_testflight(skip_waiting_for_build_processing: true)` — [GameCI iOS](https://game.ci/docs/github/deployment/ios)
- Apple rate-limits frequent uploads, so trigger manually or limit to a few runs a day. Missing icons cause upload errors — [GameCI iOS](https://game.ci/docs/github/deployment/ios)
- Codemagic recommends uploading the first version manually and needs the ASC app record to exist first — [Codemagic Unity quick start](https://docs.codemagic.io/yaml-quick-start/building-a-unity-app/)

### Inferences
- Size: Unity IL2CPP iPad builds with HD football assets can be large. Apple's general limits (about 4 GB uncompressed app, 200 MB cellular download prompt) apply. Use Addressables / On-Demand Resources if assets grow. These figures are from background knowledge and were not verified this session.
- Upload the dSYM/symbols that Unity's `usymtool` generates for crash symbolication.

### Gaps
- I did not fetch current Apple TestFlight limits (number of internal testers, build expiry of 90 days, app size caps). They come from background knowledge only.

## Q5. Codemagic as an alternative, and macOS cost estimates for a Unity IL2CPP iOS build

### Takeaway
GitHub macOS standard runners cost **$0.062/min** (cut from $0.08 on 1 Jan 2026) and count **10x** against included private-repo minutes. A Free account's 2,000 minutes therefore covers only about 200 macOS minutes, and a Pro account's 3,000 about 300. Codemagic gives personal accounts **500 free M2 minutes a month**, then $0.095/min, but its Unity docs require a **Unity Plus/Pro serial** on the build machine. That rules out the owner's Personal license unless only the Xcode stage runs on Codemagic. The cheapest setup stays as now: GitHub ubuntu for the Unity export (about $0.006/min) and `macos-26` for the Xcode archive only.

### Cited Findings
- GitHub hosted runner prices: macOS 3/4-core (M1 or Intel) $0.062/min. Linux 2-core x64 $0.006/min, Linux 2-core arm64 $0.005/min. Larger macOS: 12-core `macos_l` $0.077/min and 5-core M2 Pro `macos_xl` $0.102/min, available only to Team or Enterprise Cloud organizations — [GitHub docs: Actions runner pricing](https://docs.github.com/en/billing/reference/actions-runner-pricing)
- Included minutes: Free 2,000/month, Pro 3,000/month. Multipliers are Linux 1, Windows 2, macOS 10. Public repos are free — [GitHub docs: How use of GitHub Actions is measured](https://docs.github.com/en/billing/concepts/product-billing/github-actions)
- On 1 Jan 2026 the macOS rate dropped from $0.080 to $0.062 (third-party reports) — [cicdcalculator](https://cicdcalculator.com/github-actions-pricing-changes-2026); [DEV Community](https://dev.to/shubhamoriginx/github-actions-changed-pricing-in-2026-heres-exactly-what-youre-paying-now-51db)
- Codemagic: "Building Unity apps in a cloud CI/CD environment requires a Unity **Plus** or a **Pro** license". Activation uses `-serial ${UNITY_SERIAL} -username -password` in batch mode, and the seat is returned in `publishing:` with `-returnlicense`. If a build is cancelled, the seat is not returned. The iOS flow is a Unity `-executeMethod BuildScript.BuildIos`, then `xcode-project use-profiles`, then `xcode-project build-ipa`. TestFlight publishing uses `app_store_connect: auth: integration, submit_to_testflight: true, beta_groups: [...]`. The example machine is `mac_mini_m2` — [Codemagic: Building a Unity app](https://docs.codemagic.io/yaml-quick-start/building-a-unity-app/)
- Codemagic pricing: personal accounts get 500 free macOS M2 minutes a month (none on Team accounts). Pay-as-you-go: macOS M2 $0.095/min, M4 $0.114/min, Linux/Windows $0.045/min. Extra concurrency $49/month. The free plan has 1 concurrent build and a 120-minute build limit (third-party) — [Codemagic pricing docs](https://docs.codemagic.io/billing/pricing/); [codemagic.io/pricing](https://codemagic.io/pricing); [freetier.co](https://freetier.co/directory/products/codemagic)
- UBA: 100 free Mac minutes a month (see Q2) — [Unity Discussions](https://discussions.unity.com/t/unity-devops-updates-in-2026/1711090)

### Inferences
- **Timing estimate (not sourced, from general experience):** the Unity iOS export of a mid-size URP project on Linux takes about 10 to 25 min cold and 5 to 10 min with a `Library` cache. The macOS IL2CPP Xcode compile + archive + upload takes about 15 to 30 min on a 3-4 core M1 runner. Most of that is compiling IL2CPP C++, roughly proportional to code size plus generics. A simulator build roughly doubles the Xcode time if run as well.
- **Cost per TestFlight build, using the inferred times:** macOS 25 min x $0.062 = **about $1.55**, or 250 included minutes, which uses most of the Free allowance (2,000/10 = 200 macOS min) in a single build. Linux export: 20 min x $0.006 = $0.12. With ios-factory also consuming macOS minutes, expect most Unity builds to be paid overage. **About 20 TestFlight builds/month = about $30-35.** Adding a simulator screenshot run per build adds about $1.
- Reduce macOS minutes:
  - Cache the IL2CPP build artifacts with `actions/cache` on `build/iOS/Il2CppOutputProject` and `~/Library/Developer/Xcode/DerivedData`. Unity's incremental IL2CPP cache helps only if the export path is stable.
  - Set `IL2CPP Code Generation = Faster (smaller) builds` for TestFlight dev builds.
  - Run the Mac job only on tags or `workflow_dispatch`.
- Codemagic only pays off if the owner moves to Unity Pro, or splits the work (Linux GameCI export on GitHub, then the Xcode stage on Codemagic). The 500 free M2 minutes would cover about 20 Xcode stages a month at no cost, which is materially cheaper than GitHub macOS at 10x. The split is the best cost hack, at the price of a second CI system.

### Gaps
- No source gave measured Xcode IL2CPP compile times for Unity 6.3 on GitHub `macos-26` or Codemagic M2. A benchmark run is needed.
- The `macos-26` label maps to arm64 M1 3-core or similar standard runners. The pricing page does not map labels, which was not confirmed.

## Q6. Game controller and touch input testing on the simulator

### Takeaway
Touch can be exercised on the simulator: mouse clicks act as single touches, and Option-drag gives a two-finger pinch. In CI, use XCUITest taps or in-game scripted input (Unity Input System `InputTestFixture` in EditMode/PlayMode on Linux). For game controllers, no current source confirms that the iOS Simulator passes through physical or MFi controllers. Older reports say it does not. Test controller logic on Linux with the Input System's virtual `Gamepad` device, and validate on a real iPad via TestFlight or a device farm.

### Cited Findings
- Apple: supporting controllers requires adding the Game Controller capability, which adds GameController.framework. Each controller is represented by a `GCController`, and apps must handle connect/disconnect at runtime — [Apple Game Controller docs](https://developer.apple.com/documentation/gamecontroller.md); [WWDC19 session 616](https://developer.apple.com/videos/play/wwdc2019/616/)
- Older reports (2015-2019, mostly tvOS) say controllers were not supported in the simulator. One user paired a controller with the Mac and used it in the tvOS simulator for menus — [Apple Developer Forums thread 17355](https://developer.apple.com/forums/thread/17355); [appletvsimulatorcontroller project](https://awesome.ecosyste.ms/projects/github.com%2Fchaoscoder%2Fappletvsimulatorcontroller)

### Inferences
- In CI (headless macOS VM) no physical controller exists anyway. Controller coverage should come from Unity Input System tests (`InputTestFixture`, `InputSystem.AddDevice<Gamepad>()`, `Press(gamepad.buttonSouth)`) running in the existing GameCI ubuntu test-runner.
- The touch UI for a football game (virtual stick and buttons) can be checked visually in simulator screenshots at iPad resolutions: 13" iPad Pro, 11" iPad Air and iPad mini. Use XCUITest `coordinate(withNormalizedOffset:).tap()` or press/drag for simple automated gestures. Multi-touch beyond two fingers is not practical on the simulator.

### Gaps
- No current (Xcode 26) Apple statement on iOS Simulator controller passthrough was found.
- Unity Input System behaviour with `GCController` in the simulator was not verified.
