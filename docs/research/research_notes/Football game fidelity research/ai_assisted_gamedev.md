# AI-Assisted Game Development with Coding Agents (Unity focus, headless Linux VPS)

Research date: 2026-11-04. Sources were fetched on that date unless marked. Vendor blogs (StraySpark, Cinevva, aitoolsatlas, wavespeed) are secondary or commercially biased, and the notes flag them where it matters.

## 1. Unity MCP servers and agent bridges in 2026: capabilities and headless Linux fit

### Takeaway
Unity's official path is now the **Unity CLI**, which is free and experimental. It drives the Editor through the `com.unity.pipeline` package, has an "MCP mode" for existing MCP clients, and comes with an official Claude Code plugin of 29 skills. The older in-Editor MCP server in `com.unity.ai.assistant` is deprecated. Among community servers, CoplayDev has the most traction, and IvanMurzak's is the most explicit about headless use: it has a Docker image, `-batchmode -nographics` CI examples, Linux server binaries and a runtime-in-build mode. That makes it the best documented fit for a Linux VPS.

### Cited Findings
**Official Unity**
- Unity AI went to open beta in about May 2026 with an in-Editor agent (Assistant), an AI Gateway and an MCP server. — [Cinevva news](https://app.cinevva.com/news/2026-05-04-unity-ai-open-beta); [Unity blog: getting started](https://unity.com/blog/unity-ai-how-to-get-started)
- The in-Editor MCP server ships in `com.unity.ai.assistant`, needs Unity 6 (6000.0) or later, and is set up under Project Settings > AI > Unity MCP Server ("Unity Bridge: Running"). — [Unity docs: Unity MCP get started](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.17/manual/integration/unity-mcp-get-started.html)
- **Pricing history** (source is a competing vendor):
  - May 2026: the MCP server needed a paid Unity AI plan, which drew backlash.
  - 15 Jul 2026: Unity announced the Unity CLI and said "Both the CLI and MCP will be free, with no concurrency limits".
  - By Oct 2026, Unity's FAQ said the CLI, MCP and official plugin are free and need no AI credits.
  - The in-Editor MCP server from the paywall period is deprecated in favour of the CLI's `unity mcp` mode.
  - The CLI was at 1.0.0-beta.13 as of 7 Oct 2026. Unity AI Assistant itself costs about $10/month for Personal after a trial.
  - — [StraySpark: Is Unity MCP free](https://www.strayspark.studio/blog/is-unity-mcp-free-2026) (vendor of a paid competing MCP, so it may be biased)
- Unity's announcement calls the Unity CLI "terminal-native" for coding agents, CI and custom tooling. "Unity MCP remains fully supported" for existing setups, and the CLI includes an MCP Mode. — [Unity Discussions announcement](https://discussions.unity.com/t/announcing-the-unity-cli-a-new-way-to-connect-your-tools-and-agents/1731104); [Unity on X](https://x.com/unity/status/2079389530260414898?lang=en)
- The Unity CLI is a standalone binary. It doesn't need the Hub desktop app, it targets CI and build agents, and it gives structured JSON/TSV output with predictable exit codes. **Controlling the Editor needs the Unity Pipeline package.** The docs page was last updated 2026-10-20. — [Unity docs: Unity CLI intro](https://docs.unity.com/en-us/unity-cli/unity-cli.md); [Use the Unity CLI](https://docs.unity.com/en-us/hub/use-unity-cli.md)
- The install docs cover macOS and Linux, and say the CLI can run on headless CI workers without the Hub. The older Hub CLI (`-- --headless`) is still supported, but there is a migration guide away from it. Both are experimental. — [Unity docs: CLI overview](https://docs.unity.com/en-us/hub/cli-overview)
- **Official Unity Plugin for Claude Code** (9 Sep 2026):
  - It has 29 skills. The `unity-cli` skill creates and modifies GameObjects, edits scenes and assets, inspects the hierarchy, runs C#, and handles editor installs, licences, builds and logs.
  - Skill areas: UI, 2D, rendering, audio, scene/gameplay, monetization, multiplayer and localization.
  - It is listed at claude.com/plugins/unity, with docs at docs.unity.com/en-us/ai/unity-plugin/claude-code.
  - The post does not cover headless or Linux use, except a package-management skill that does headless/CI package installs.
  - — [Unity blog: Official Unity Plugin for Claude Code](https://unity.com/blog/unity-plugin-for-claude-code)
- Unity's explainer on MCP for game development. — [Unity blog](https://unity.com/blog/mcp-servers-game-development)

**Community servers** (star counts as of 21 Oct 2026 per StraySpark)
- **CoplayDev/unity-mcp ("MCP for Unity")**:
  - MIT licence, about 15.3k stars, "50 focused MCP tool entrypoints".
  - Tool groups cover vfx, animation, ui and testing, plus assets, scenes, scripts, running tests, profiling and builds.
  - Has Roslyn script validation, multi-instance routing and a "Remote-hosted server with auth" guide.
  - Supports Unity 2021.3 through 6.x and needs Python 3.10+ (uv). Latest release v10.3.0 on 2026-10-04.
  - — [GitHub CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp); [StraySpark](https://www.strayspark.studio/blog/is-unity-mcp-free-2026)
- **IvanMurzak/Unity-MCP**:
  - Apache-2.0, about 4.5k stars, 71 tools and 46 prompts.
  - Default visible tools: `rql-query`, `rql-mutate`, `rql-schema`, `screenshot-isolated`, `script-execute` (Roslyn compile and run C#), `tests-run` (EditMode/PlayMode) and `unity-tool-list`. Game View, Scene View and camera screenshot tools are hidden by default.
  - **Runtime mode** (`UnityMcpPluginRuntime`) works inside a compiled game; there is a chess-bot sample.
  - **Official Docker image** with streamableHttp and stdio transports, **CI example with `-batchmode -nographics`**, server binaries for Linux x64 and arm64, and an npm `unity-mcp-cli` with a `--headless` option.
  - — [GitHub IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP); [CLI README](https://github.com/IvanMurzak/Unity-MCP/blob/main/cli/README.md); [FAQ](https://github.com/IvanMurzak/Unity-MCP/wiki/FAQ)
- **CoderGamester/mcp-unity**: open source, about 2k stars, TypeScript server, includes a WSL2 troubleshooting section. — [StraySpark](https://www.strayspark.studio/blog/is-unity-mcp-free-2026); [issoh.co.jp overview](https://www.issoh.co.jp/tech/details/6890/)
- The common architecture is three layers: agent client, MCP server (Python for CoplayDev, TypeScript for IvanMurzak and CoderGamester), and Unity Editor bridge. — [issoh.co.jp](https://www.issoh.co.jp/tech/details/6890/)
- **Codeturion/unity-headless-cli-skill** (MIT):
  - Leaves a `-batchmode` Editor running without `-quit` and drives it with `unity command <name> --project-path` and `unity command eval "<C#>"`, with 200 to 600 ms round trips and no recompile.
  - The pipeline server binds to localhost with a bearer token. Custom `[CliCommand]` classes extend it, and setup writes a CLAUDE.md section.
  - Archived on 26 Oct 2026 and moved to the `Codeturion/skills` monorepo.
  - — [GitHub](https://github.com/codeturion/unity-headless-cli-skill)
- Others seen but not evaluated: Benjamin-Curlier/unity-kit (Claude Code plugin), `unityctl` (NuGet), AnkleBreaker unity-mcp-plugin, and a "Unity's Official MCP vs Unreal/Godot/Blender" comparison. — [unity-kit](https://github.com/Benjamin-Curlier/unity-kit); [unityctl](https://www.nuget.org/packages/unityctl/0.6.5); [StraySpark comparison](https://www.strayspark.studio/blog/unity-mcp-server-vs-unreal-godot-blender-2026)

### Inferences
- For a Linux VPS, the lowest-friction stack looks like this:
  - A persistent `-batchmode` Unity Editor running `com.unity.pipeline`, driven by the official `unity` CLI. Use the Unity Claude Code plugin skills, or MCP mode for MCP clients.
  - IvanMurzak Unity-MCP as a fallback or complement, when you want MCP-native screenshots, tests and Roslyn exec and a Dockerised server.
- With `-nographics`, no tool can produce real rendered screenshots. Visual capture needs a GPU context, either real or virtual (see section 2).
- Expect churn: the CLI and Pipeline are beta/`-exp`, Unity has already deprecated one MCP server, and one community skill was archived within weeks.

### Gaps
- No source confirmed that the official `unity mcp` mode or the Pipeline package works with a Linux Editor in batchmode. The docs say CLI binaries exist for Linux but don't explicitly confirm Editor control on Linux.
- No per-tool head-to-head comparison of CoplayDev, IvanMurzak and CoderGamester was found. StraySpark's table is the only one, and it comes from a competing vendor.
- CoplayDev's headless/batchmode behaviour isn't documented in its README.

## 2. Running the Unity 6 Editor headless on Linux

### Takeaway
- Licensing:
  - Personal: online activation with email and password works in containers, but manual `.alf`/`.ulf` activation does **not** work for Personal.
  - Pro: activate with a serial plus email and password, and return the seat afterwards.
  - Persist the licence cache directory across container rebuilds.
- GameCI (`unityci/editor` images) is the standard Docker route.
- Rendered screenshots need a virtual X server such as `xvfb-run`, without `-nographics`. Fully software Vulkan (lavapipe) is plausible but undocumented for Unity.
- **A Linux Editor cannot produce an iOS app.** Unity's docs say the final build step requires Xcode on macOS, or Unity Build Automation.

### Cited Findings
**Licensing**
- Unity's manual (`.alf`→`.ulf`) activation "doesn't work with floating license subscriptions or Unity Personal". Personal users must sign in via the Hub. — [Unity 6.3 docs: manual activation](https://docs.unity3d.com/Manual/ManualActivationCmdWin.html); [Mac variant](https://docs.unity3d.com/Manual/ManualActivationCmdMac.html)
- GameCI's documented secrets:
  - Personal: `UNITY_LICENSE` (contents of `.ulf`), `UNITY_EMAIL` and `UNITY_PASSWORD`.
  - Pro: `UNITY_SERIAL`, `UNITY_EMAIL` and `UNITY_PASSWORD`.
  - The Linux `.ulf` lives at `~/.local/share/unity3d/Unity/Unity_lic.ulf`. A "Return License" step frees Pro seats, and a `unityLicensingServer` option covers floating licences.
  - The page says licences aren't tied to a platform.
  - — [GameCI activation docs](https://game.ci/docs/github/activation)
- A devcontainer licensing guide (community) goes further:
  - Personal "cannot use `.alf` / `.ulf` at all". Use online activation with `UNITY_EMAIL`/`UNITY_PASSWORD`. Pro needs serial, email and password.
  - A `.ulf` is bound to machine identifiers and won't move between containers.
  - Persist `/root/.local/share/unity3d` and `/root/.config/unity3d` via a host mount.
  - Error meanings: "Found 0 entitlement groups" or "com.unity.editor.headless was not found" is an account or entitlement issue. "No license activation found for this computer" usually means a fresh container. "Machine bindings don't match" means a `.ulf` from another machine.
  - It conflicts with GameCI on whether a Personal `.ulf` can be reused; the newer Unity docs side with the devcontainer guide.
  - — [Ambiguous-Interactive devcontainer licensing wiki](https://github.com/Ambiguous-Interactive/unity-helpers/wiki/Guides-Unity-Devcontainer-Licensing); compare [GameCI](https://game.ci/docs/github/activation)
- Unity's issue tracker: Docker masking `/etc/machine-id` broke licence activation in containers. Marked fixed in 2020.2 (old). — [Unity Issue Tracker](https://issuetracker.unity3d.com/issues/cannot-activate-license-within-a-docker-container)

**Headless and rendering**
- Old reports (2015–2016) say the Linux Editor in batchmode needed an X display even with `-nographics`, so people wrapped it in `xvfb-run`. A Unity 5 standalone also crashed (core dump) under xvfb-run with Nvidia drivers. Both are dated. — [Unity forum: docker and unity on linux](https://forum.unity3d.com/threads/docker-and-unity-on-linux.427542/); [Medium: Unity3D in headless Ubuntu (2015)](https://medium.com/@h0lycattle/running-unity3d-in-a-virtualized-headless-ubuntu-environment-1adf95c05994); [Unity discussions: xvfb Nvidia core dump](https://discussions.unity.com/t/ubuntu-xvfb-run-a-unity-application-with-nvidia-leads-to-a-core-dumped/652145)
- GPU-less Vulkan pattern from other engines: run under Xvfb and point `VK_ICD_FILENAMES` at the Mesa lavapipe ICD. This is documented for a Bevy app and the MotrixSim engine, not Unity. — [bglga screenshots commit](https://git.hoyer.xyz/harald/bglga/commit/c6dcf9d72838bde32318f2318bf3cb5cc30c98a9); [MotrixSim headless CPU rendering](https://motrixsim.readthedocs.io/en/latest/user_guide/render/headless_cpu.html)
- IvanMurzak Unity-MCP ships a CI example running Unity with `-batchmode -nographics`, which is suited to tests and compiles, not rendering. — [GitHub](https://github.com/IvanMurzak/Unity-MCP)

**iOS from Linux**
- Unity builds an Xcode project, and Xcode builds the app. "Xcode is only available for macOS, so if your development machine doesn't run macOS, you can't build an application locally." The suggested alternative is Unity Build Automation. iOS Build Support is the module you need. — [Unity 6.4 docs: iOS environment setup](https://docs.unity3d.com/Manual/ios-environment-setup.html); [Unity 6000.5 docs](https://docs.unity.com/en-us/engine/6000.5/manual/platform-specific/iphone/getting-started/ios-environment-setup.md)
- A 2019.x forum thread reports a Linux user whose iOS "Build" button did nothing while the command line seemed to work, fixed around 2019.3/2020.1. — [Unity discussions](https://discussions.unity.com/t/build-for-ios-not-exporting-xcode-project/765514)

### Inferences
- The docs do not say outright that a Linux Editor *cannot export* the Xcode project folder. Their wording only rules out the final Xcode compile on Linux. A practical pipeline is to export or generate the Xcode project on Linux if the iOS module installs there, then compile and sign on a Mac runner or Unity Build Automation. **Verify this on the box.** GameCI's Docker images have historically published `-ios-` tagged editor images for Linux (unverified here).
- On a VPS with no GPU:
  - Run compile, EditMode/PlayMode tests and logic sims with `-batchmode -nographics`.
  - For screenshots, run without `-nographics` under `xvfb-run -s "-screen 0 1920x1080x24"`, using `-force-glcore` (Mesa llvmpipe) or `-force-vulkan` with `VK_ICD_FILENAMES` pointing at lavapipe.
  - Expect slow frames. Test early, because Unity-specific confirmation is missing.
- Alternative for visuals: build a WebGL player on the VPS and screenshot it with headless Chromium (Playwright). Chromium's SwiftShader handles WebGL on CPU. The Mowtopia case study (section 3) used web builds for verification.

### Gaps
- No primary source found confirming Unity 6 Personal online activation in batchmode on Linux. In particular, the exact `-username/-password` flags for Personal (as opposed to `-serial`) were not verified, and it's unclear whether the new Unity CLI `unity auth` or licence commands handle it.
- No Unity-specific source on lavapipe or llvmpipe rendering quality or stability for Unity 6.
- No current confirmation of `unityci/editor` Unity 6 tags and the module matrix (`ios`, `linux-il2cpp`, `webgl`). The GameCI Docker page was not fetched.
- Whether the iOS Build Support module installs on and exports from the Linux Editor in Unity 6 is unverified.

## 3. Documented case studies of building games with Claude/LLM agents

### Takeaway
The best documented Unity case is Mowtopia (Oct 2026), built with Claude Code from a 7-milestone PRD. It shows the patterns that work:
- one milestone at a time, with hard stops
- a test count that grows with every milestone
- verification in a real web build
- side-by-side comparison against concept art

Its main failure was parallel sessions overwriting scenes and prefabs. A crop of Claude-driven playtest tools appeared in Oct–Nov 2026. No Unity football game built by an agent was found. The only football example is a non-Unity Sensible Soccer clone.

### Cited Findings
**Mowtopia** (Frederik Roessell, 21 Oct 2026; Unity 6; Claude Code; PRD written with Codex)
- Seven milestones: playable sphere, grass cutting, collection/stacking, selling, upgrades, art/polish, commercial expansion. Each milestone has "Deliverables", "Acceptance criteria", and sometimes an explicit stop. Milestones 1 and 2 stopped for human validation of movement and mowing.
- Result: most of the game in about 2 days, 60 automated tests (15 → 24 → 33 → 46 → 60) and a 15 MB compressed web build.
- Rules lived in AGENTS.md:
  - "Implement one milestone at a time."
  - "Do not claim a build has been tested unless it has actually been run."
  - "Do not implement features outside the current milestone."
  - Build incrementally, support WebGL from the start, and use the Unity Profiler rather than optimizing blindly.
- Visual verification:
  - Browser checks of the web build caught invisible clippings spawning inside tall grass, which the tests missed.
  - Editor screenshots taken outside Play mode were misleading (mowed grass still drawn), so captures moved to Play mode or the web build.
  - Milestone 6 sign-off compared the game side by side with approved concept art.
  - Lighting was changed one change at a time, with before/after captures.
- Failure modes:
  - A scratch-copy copy-back overwrote the main scene and three prefabs while another session was active, and a stale copy-back removed approved assets.
  - The fix was "every file has one owner" per session, mirroring before each rebuild, content-hash checks before copy-back, and git with LFS. Sessions stage only their own files.
  - A material-slot bug rendered trees solid red.
  - The PRD economy was unaffordable and had to be retuned from 1 to 3 coins per unit.
- Still untested at publication: touch controls on a real phone, frame rate, and how mowing feels.
- — [Substack: How I build a Unity game with Claude Code, one PRD milestone at a time](https://froessell.substack.com/p/how-i-build-a-unity-game-with-claude)

**Other cases**
- A designer with no programming experience vibe-coded a Unity minigame ("Hedgie's Easter Egg Hunt") with Claude in a 2-day hackathon. Small problems piled up that prompting alone couldn't fix. About 441 days old, so circa Aug 2025. — [Medium (ArtCenter)](https://medium.com/artcenter-graduate-interaction-design/i-used-ai-to-code-a-game-in-unity-heres-what-i-learned-cd680f2dad56)
- Tutorial guidance (2026):
  - Split work into well-scoped systems. Asking for "the whole game" in one prompt is a common mistake.
  - Claude Code still needs a human for Editor-only tasks (lighting, Animator graphs, prefab wiring) unless it is paired with a Unity MCP.
  - — [AI for Anything tutorial](https://aiforanything.io/blog/claude-code-unity-game-development-tutorial-2026); see also [ai-game.dev tutorial](https://ai-game.dev/blog/claude-code-unity-tutorial)
- **Football:** Claude Code recreated a Sensible Soccer-style game (not Unity): 5v5, WASD/arrows, spacebar kick, goals and simple AI opponents. No automated verification is described. — [Medium: Recreate Sensible Soccer with Claude Code](https://noamso.medium.com/playing-with-claude-code-to-recreate-sensible-soccer-d86fe7e51aca)
- Football *simulation* repo (not agent-built, but relevant to deterministic simulation): [tmlabonte/football-sim](https://github.com/tmlabonte/football-sim)

**Agent playtesting tools (Unity)**
- **GhazaGG/Unity-Playtest-for-Claude**, a Claude Code skill built on Unity's Pipeline CLI:
  - Editor probes find objects stuck in colliders, blocked spawns, missing references and prefabs missing from the network list, and take Scene view screenshots.
  - Scripted Play Mode drives the real Input System bindings and waits for game *state* rather than fixed delays, then takes Game view screenshots.
  - A host + client mode adds latency and sends illegal intents to check the server rejects them.
  - The report is pass/fail/skip, and records whether a script or a human verified each case. "Feel, sound and visuals are never marked passed by a script."
  - The author's eval: the baseline agent marked "the door swings open" as passed from a screenshot, while the skill deferred it to a person.
  - Only edits through the Editor, never hand-edits `.unity`, `.prefab` or `.meta` files.
  - Needs Unity 6 with `com.unity.pipeline` 0.8–0.9 exp. Tested on Windows.
  - — [GitHub](https://github.com/GhazaGG/Unity-Playtest-for-Claude); [mcpmarket listing](https://mcpmarket.com/tools/skills/unity-ai-playtest)
- **masontmorris/unity-playtest-agent**: an autonomous playtester that learns controls and state from the game code, plays in real time, reproduces issues and exports bug reports with evidence. Hackathon project (Oct 26–Nov 4 2026), partly still "planned". — [GitHub](https://github.com/masontmorris/unity-playtest-agent)
- **yalza/AgentPlay**: a Claude agent plays the Unity game over a TCP bridge, with plans, reflexes, a learned playbook, and a realtime mode with model routing and plan prefetch. — [GitHub](https://github.com/yalza/AgentPlay)
- **crane-valley/cluewren**: your coding agent playtests the game open in a Unity (6000.0+), Godot or Unreal editor and reports bugs. — [GitHub](https://github.com/crane-valley/cluewren)

### Inferences
- The repeatable recipe looks like this:
  1. A PRD with numbered milestones, each with acceptance criteria and a stop gate.
  2. AGENTS.md or CLAUDE.md rules against claiming untested work and against scope creep.
  3. Tests added every milestone.
  4. A real-player build (web build) checked visually by a human or a vision model against reference art.
  5. File ownership and git discipline when running parallel agents.
- Screenshot-based "pass" judgments by agents are unreliable for motion and feel. Keep them human-gated or video-based.

### Gaps
- No Unity football or sports game built with agents was documented, and nothing on the animation-heavy sports workflows (mocap retargeting, ball physics tuning) done via agents.
- No Anthropic first-party blog on game development with Claude was found in this pass (not searched exhaustively).
- No quantitative failure-rate data was found.

## 4. Prompt and spec patterns for agent-built game repos

### Takeaway
The patterns that work, all documented in the sources above:
- spec-first PRDs with milestone gates
- AGENTS.md or CLAUDE.md honesty and scope rules
- Editor changes made only through tools, never by hand-editing YAML scenes or prefabs
- state-based (not time-based) Play Mode tests
- an explicit human-verified column for visuals and feel

Deterministic simulation and vision-model scoring are promising but thinly documented for Unity.

### Cited Findings
- PRD skeleton used in practice:
  - Header metadata: version, status, engine, language, render pipeline, platforms, genre, visual direction.
  - Sections: vision, technical requirements, folder architecture, per-system specs, art direction, UI/UX, audio, saving, platforms, performance, milestones, out-of-scope list, definition of done, and an "initial agent instruction" block.
  - — [Mowtopia](https://froessell.substack.com/p/how-i-build-a-unity-game-with-claude)
- Rules worth copying verbatim: "Do not claim a build has been tested unless it has actually been run", "Implement one milestone at a time", and "Every file has one owner". — [Mowtopia](https://froessell.substack.com/p/how-i-build-a-unity-game-with-claude)
- Verification rules:
  - Turn each feature's "How to test" into evidence.
  - Wait for state rather than fixed delays.
  - Use the real input path.
  - Never auto-pass feel, sound or visuals.
  - Revert project settings the build touched (a `doctor.py` check).
  - Never hand-edit `.unity`, `.prefab` or `.meta` files.
  - — [Unity-Playtest-for-Claude](https://github.com/GhazaGG/Unity-Playtest-for-Claude)
- Setup tools write a CLAUDE.md section describing how to drive the headless Editor (command reference doc plus `eval`). — [unity-headless-cli-skill](https://github.com/codeturion/unity-headless-cli-skill)
- Don't prompt for "the whole game". Scope by system. Editor-UI work needs MCP or a human. — [AI for Anything](https://aiforanything.io/blog/claude-code-unity-game-development-tutorial-2026)
- Runtime MCP inside a built game (IvanMurzak's chess-bot sample) lets an LLM act as an in-game agent or bot, which can serve automated playtesting. — [IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP)

### Inferences
For a football game, structure the spec so that match logic is a deterministic, fixed-timestep simulation separate from presentation. Concretely:
- Seeded RNG, and no `Time.deltaTime` in the sim.
- Headless EditMode tests can replay seeded matches and assert outcomes and invariants (ball in bounds, offside calls, possession stats).
- Bot-vs-bot matches double as soak tests.
- Vision-model checks, such as comparing a Play-mode capture to reference art or broadcast-camera frames, should give advisory scores, not pass/fail.

This is a synthesis, not documented in a Unity football source.

### Gaps
- No published Unity example of vision-model scoring of screenshots or video, or of deterministic-sim test harnesses built with agents.
- No sourced CLAUDE.md template specific to Unity game repos beyond the rules quoted above. The Unity plugin docs (docs.unity.com/en-us/ai/unity-plugin/claude-code) were not fetched.

## 5. AI tools for game assets: 3D generation, video mocap, textures, and commercial licensing

### Takeaway
Hosted 3D generators (Meshy, Tripo, Rodin) generally allow commercial use only on paid plans. The open-weight Hunyuan3D licence excludes the EU, UK and South Korea and caps users. MIT-licensed TRELLIS 2 is cited as the clean self-hosted option. For video mocap, the free tiers of Move.ai and DeepMotion are non-commercial. Rokoko Vision and Plask need a check of their current terms of service. Almost all sources here are secondary roundups, so verify every licence on the vendor's own page before shipping.

### Cited Findings
**3D model generation**
- **Meshy:** free-tier outputs are non-commercial. Pro is about $20/month (around 1,000 credits, private commercial licence). — [Cinevva guide](https://app.cinevva.com/guides/ai-3d-model-generators); [3dprinting.com](https://3dprinting.com/software-guides/ai-3d-model-generators/)
- **Tripo:** sources conflict.
  - One says the free tier gives 200 monthly credits under CC BY 4.0 (attribution, commercial allowed).
  - Another says free outputs are non-commercial.
  - Paid Pro is about $12/month with commercial rights.
  - — [Sorceress](https://sorceress.games/blog/best-free-ai-3d-model-generator-honest-2026-picks); [Krea](https://www.krea.ai/blog/best-ai-3d-model-generators-2026); [RapidDirect](https://www.rapiddirect.com/blog/best-8-ai-3d-model-generators/)
- **Rodin (Hyper3D):** commercial use on paid plans. The free trial is watermarked. Creator is about $30/month and Business about $120/month. — [3DAIStudio comparison](https://www.3daistudio.com/blog/rodin-2-5-vs-tripo-vs-hunyuan-3d-comparison); [neural4d comparison](https://www.neural4d.com/zh/features/neural4d-vs-tripo-vs-meshy-vs-rodin)
- **Hunyuan3D (Tencent, open weights):** community licence that excludes the EU, UK and South Korea and needs a separate commercial licence above a monthly-active-user threshold (the threshold was not confirmed in the fetched snippet). — [Cinevva guide](https://app.cinevva.com/guides/ai-3d-model-generators); [Pixazo open-source guide](https://www.pixazo.ai/blog/?p=2770888)
- **Microsoft TRELLIS 2:** MIT licence, native PBR, self-hostable. Recommended as the open option. — [Sorceress](https://sorceress.games/blog/best-free-ai-3d-model-generator-honest-2026-picks)
- Release timeline and leaderboard. — [Cinevva timeline 2026](https://app.cinevva.com/guides/ai-3d-model-generation-timeline-2026); [Pixazo leaderboard](https://www.pixazo.ai/leaderboard/ai-3d-model-generation)

**AI motion capture from video**
- **Move.ai:** the Free plan has no processing credits. Personal is $15/month and **non-commercial**. Commercial use needs Standard at $50/month or above. Verified March 2026. — [aitoolsatlas Move.ai pricing](https://aitoolsatlas.ai/tools/move-ai/pricing); [free vs paid](https://aitoolsatlas.ai/tools/move-ai/free-vs-paid)
- **DeepMotion:** free tier is 60 seconds/month, non-commercial. Paid plans start around $15. — [wavespeed DeepMotion alternatives](https://wavespeed.ai/blog/3d-model-access/deepmotion-alternatives/); [neolemon](https://www.neolemon.com/blog/best-ai-motion-capture-tools-for-character-animation)
- **Rokoko Vision:** free single-camera vision mocap; dual camera is paid. The free tier is reportedly limited to 15-second clips, and accuracy drops with occlusion. Commercial terms for the free tier were not stated. — [RenderHub](https://www.renderhub.com/blog/budget-motion-capture-options-worth-considering); [Film Threat](https://filmthreat.com/features/best-ai-motion-capture-tools-for-independent-filmmakers/)
- **Plask:** free tier is about 15 seconds/day. Paid plans are about $18–19/month, and commercial use is on paid plans (Basic and above). Free-tier commercial rights are unclear. — [toolbit Plask](https://www.toolbit.ai/ai-tool/plask-motion); [SourceForge Move.ai vs Plask](https://www.sourceforge.net/software/compare/Move.ai-vs-Plask)
- Overview of the 2026 landscape (Move.ai, Cascadeur, DeepMotion, Wonder Studio, Rokoko, Plask). — [youngju.dev deep dive (May 2026)](https://www.youngju.dev/blog/culture/2026-05-16-ai-motion-capture-animation-2026-move-ai-cascadeur-deepmotion-wonder-studio-rokoko-plask-animatediff-runway-deep-dive.en); [Cinevva AI animation generators](https://app.cinevva.com/ko/guides/ai-animation-generators-3d)

### Inferences
- For a football game, the mocap tools matter most for player animation: running, kicking, celebrations. Budget for a commercial tier, either Move.ai Standard or paid DeepMotion or Plask. Occlusion between players in match footage will hurt single-camera tools, so record isolated actors.
- Plan to clean up and retarget AI 3D outputs to a Humanoid rig in Unity, since they need cleanup for game-ready topology and rigging.

### Gaps
- No primary vendor licence pages were fetched. Every pricing and licence figure above comes from secondary sources, and several conflict, especially Tripo's free tier and Hunyuan3D's MAU threshold.
- AI texture generation tools (for example Meshy texturing, Substance/Adobe Firefly, Stable Diffusion-based PBR generators, Unity AI's own generators) and their licences were not researched in this pass.
- Whether Unity AI generators (sprites, textures, animation in Unity AI beta) grant clear commercial rights was not verified.
- Licensing implications of using real broadcast football footage as mocap input (player likeness, broadcast rights) were not covered.
