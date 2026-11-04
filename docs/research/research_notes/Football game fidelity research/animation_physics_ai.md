# Football Game Fidelity: Animation, Locomotion, Ball Physics, Team AI, and Unity 6 Tooling

Research date: 2026-11-04. 15 web searches/fetches. Links marked "(not re-fetched)" are canonical URLs from prior knowledge that were not opened in this session. Treat their details as unverified.

## 1. EA Sports FIFA/FC: what has been published about HyperMotion, ML-Flow, locomotion, dribbling, physicality and tactical AI

### Takeaway
EA's public technical disclosure is thin. There is one GDC talk (2022, on the FIFA 22 full-match mocap), and the rest is marketing-level "Pitch Notes". The pattern holds across the generations: capture real 11v11 play (Xsens suits plus LPS tracking in FIFA 22, then volumetric video from real matches in FC 24), use it to train ML systems that generate or select animation (ML-Flow for ball approach), and keep a huge authored animation library behind branching and transition logic. Tactical AI moved to role-based positioning (FC IQ, FC 25) with an ML model fed by Opta data, though that model only assigns role familiarity.

### Cited Findings
- **GDC 2022 Animation Summit: "FIFA 22's Hypermotion: Full-Match Mocap Driving Machine Learning Technology"** (Ryan Cardinal, Luke Shier, David Wong, EA). It covers a full 90-minute capture of two pro teams playing 11v11, recorded with **Xsens inertial suits + a local positioning system (LPS) + static camera footage**. The data improved character athleticism and **ball physics realism**, and it trained **ML-Flow, "a machine-learned procedural animation system"**. — [GDC Vault](https://gdcvault.com/play/1027595/Animation-Summit-FIFA-22-s)
- The talk's rationale: optical mocap studios limit sports capture because of space, atmosphere and safety, so EA moved capture out of the studio. — [GDC Vault](https://gdcvault.com/play/1027595/Animation-Summit-FIFA-22-s)
- **ML-Flow (FIFA 22):** a neural network trained offline on about **8.7 million frames** of capture. At runtime it generates **ball-approach animation**: stride adjustment, running cadence, pose matching and transitions. HyperMotion data also feeds "Full Team Authentic Motion", "Kinetic Battles" (physicality/duels), "Player Humanization" and "Composed Ball Control", plus **4,000+ new animations**. Next-gen consoles only. — [EA Pitch Notes FIFA 22 deep dive](https://www.ea.com/ea-play/news/pitch-notes-fifa-22-gameplay-deep-dive); [EA HyperMotion page](https://ea.com/games/fifa/fifa-22/hypermotion); [80.lv](https://80.lv/articles/ea-uses-machine-learning-for-animations-in-fifa-22). TweakTown reported 7.8M frames instead, which conflicts with EA's 8.7M — [TweakTown](https://www.tweaktown.com/news/80507/fifa-22-uses-machine-learning-for-ultra-realism-on-xbox-series-ps5/index.html)
- **HyperMotionV (FC 24):** "V" stands for volumetric. It was captured from **180+ real pro matches** (men's and women's UCL, La Liga, Premier League) using volumetric video rather than suits. **11,000+ animations**, up from about 6,000 in FIFA 23. "AI Mimic" rebuilds signature movement from volumetric data, giving **1,200+ signature run styles** (e.g. Haaland). That 1,200 figure counts run styles, not all animations. PS5/XSX/PC only. — [EA Pitch Notes FC 24](https://www.ea.com/games/ea-sports-fc/pitch-notes/news/fc-24-gameplay-deepdive); [Trusted Reviews](https://www.trustedreviews.com/explainer/what-is-hypermotion-v-4352934); [TheSixthAxis](https://www.thesixthaxis.com/?p=442091). One outlet's "590 million frames" claim is uncorroborated — [SGO](https://www.sportsgamersonline.com/games/ea-sports-fc-24-will-introduce-hypermotion-v-and-playstyles/)
- **FC IQ (FC 25), the tactical AI:** three parts, **Player Roles, Team Tactics and Smart Tactics**. A "proprietary AI model powered by real-world (Opta) data" assigns role Familiarity (base, +, ++). Team Tactics "modernises positioning across the pitch" to reflect real-world styles. Work Rates, Player Instructions and the CF/LWB/RWB positions were removed. — [EA Pitch Notes FC 25 FC IQ](https://www.ea.com/fr-fr/games/ea-sports-fc/fc-25/news/pitch-notes-fc-25-fc-iq-deep-dive); [EA Pitch Notes FC 25 gameplay](https://www.ea.com/en/games/ea-sports-fc/fc-25/news/pitch-notes-fc-25-gameplay-deep-dive)
- **FC 26 (2025):** Competitive vs Authentic gameplay presets. The dribbling animation library was re-tuned for consistent speed and responsiveness, with dribble-touch timing that differs per preset. Animation speed is normalised by **player height**. Foot-preference logic and **animation branching** were changed. Transitions were improved to reduce "locked-in animation" feel. The game returns to **three acceleration types**. Players far from play get relaxed "low-urgency" locomotion animations. Agile Dribbling was removed. — [EA Pitch Notes FC 26 deep dive](https://www.ea.com/games/ea-sports-fc/fc-26/news/pitch-notes-fc26-gameplay-deep-dive)

### Inferences
- From FIFA 22 to FC 26, the visible problems EA works on are **responsiveness vs realism** (locked-in animations, transition latency) and **ball approach / first touch**. ML-Flow is a learned generator for the "get the foot to the ball at the right stride" problem. A small team gets most of the same benefit from motion matching plus procedural stride warping and foot IK toward a predicted ball contact point.
- "Acceleration types" and height-normalised animation speed suggest the game drives locomotion from a gameplay/physics capsule (attributes decide speed), with animation following via warping. That is "simulation-first, animation-follows", the same principle Ponton's motion matching controller uses (it clamps drift between the simulated and animated character).
- "Low-urgency" animations for distant players work as an LOD/style switch, and the same trick saves CPU on iPad.

### Gaps
- I found no SEED/EA tech paper describing ML-Flow's architecture (network type, inputs). Only the GDC abstract and marketing material are public.
- I found no EA publication on ball physics models (drag/Magnus coefficients), the collision/physicality solver, or "Football Intelligence" internals. (FIFA 23 "Football Intelligence" and FIFA 21 "Positioning Personality" are marketing terms. I did not retrieve sources for them in this session.)
- I did not verify FIFA 23's HyperMotion2 details (about 6,000 animations is implied by the FC 24 comparison).

## 2. Konami eFootball/PES engine and animation

### Takeaway
PES 2015–2021 ran on Konami's in-house **Fox Engine**. eFootball (2021+) moved to a **custom Unreal Engine 4-based engine**. Talk of a move to UE5 (a 2022 trailer mention, a 2026 data-mine) is **unconfirmed**. No Konami technical talks on eFootball animation or AI turned up.

### Cited Findings
- eFootball replaced Fox Engine with "an all-new custom game engine created via Unreal Engine 4". — [Neowin](https://www.neowin.net/amp/konamis-pes-is-now-efootball-a-free-to-play-experience-with-full-cross-play/); [PC Gamer](https://pcgamer.com/rip-pro-evolution-soccer-too-good-for-this-cruel-world-long-live-efootball)
- UE5 claims (a 2023 modding-site report and 2026 data-mine summaries) are weakly sourced. — [pesmodding](https://www.pesmodding.com/2023/08/efootball-2024-and-unreal-engine-5-soon.html); [lilys.ai summary (low reliability)](https://lilys.ai/notes/ms/d5-render-20260219/konami-new-efootball-unreal-engine-5)

### Inferences
- Konami being on Unreal while EA is on Frostbite shows that a general-purpose engine can carry a AAA football title. The engine matters less than the animation data and the tuning.

### Gaps
- No primary Konami sources (GDC/CEDEC talks) were found for eFootball's animation, motion matching, ball physics or AI. Konami's CEDEC talks in Japanese may exist but were not searched.

## 3. Motion matching and neural animation literature

### Takeaway
Motion matching is a nearest-neighbour search over pose and trajectory features in a mocap database, followed by inertialization blending. Ubisoft introduced it (Clavet, GDC 2016), it is now the AAA standard, and it is the most practical path for a small Unity team. Learned motion matching compresses its memory cost. The neural controllers from Starke and Holden (PFNN, MANN, NSM, Local Motion Phases, DeepPhase, Codebook Matching) are research-grade. Their code is **non-commercial**, so use them as inspiration, not as shipping dependencies.

### Cited Findings
- **Clavet, "Motion Matching and The Road to Next-Gen Animation", GDC 2016.** Motion matching was introduced by Clavet and Büttner in 2015, and LMM cites the talk. — [ACM / LMM refs via Unpaywall](https://unpaywall.org/10.1145%2F3386569.3392440); GDC Vault page: https://www.gdcvault.com/play/1023280/Motion-Matching-and-The-Road (not re-fetched)
- **Learned Motion Matching (Holden, Kanoun, Perepichka, Popa; SIGGRAPH 2020, Ubisoft La Forge).** It replaces each stage of MM (feature lookup, transition, pose decompression) with learned networks, so the animation database no longer has to sit in memory. Plain MM memory grows linearly with data. — [Ubisoft La Forge](https://www.ubisoft.com/en-us/studio/laforge/news/3NWqwcMU9lMfumgfPAEfka/learned-motion-matching); [paper PDF](https://www.theorangeduck.com/media/uploads/other_stuff/Learned_Motion_Matching.pdf). Ubisoft claims memory savings of up to 30x for a specific query, and also says LMM had not shipped in a game at the time of the interview. — [Ubisoft news](https://news.ubisoft.com/en-gb/article/27176jslYNMPt7vBfCaRQ1)
- Daniel Holden's blog (theorangeduck.com) has the reference implementations most indie MM systems copy: inertialization, spring-based trajectory prediction, feature normalisation, and a C++ "Code vs Data Driven Displacement" / motion matching demo. — [theorangeduck.com](https://www.theorangeduck.com/) (not re-fetched)
- **AI4Animation (Sebastian Starke) papers:** PFNN (SIGGRAPH 2017), MANN (2018), Neural State Machine (SIGGRAPH Asia 2019), **Local Motion Phases** (SIGGRAPH 2020), **Neural Animation Layering** (SIGGRAPH 2021, paper only, no code), **DeepPhase / Periodic Autoencoders** (SIGGRAPH 2022, PyTorch + Unity code), **Categorical Codebook Matching** (SIGGRAPH 2024). A Python remake, AI4AnimationPy (facebookresearch, 2026), also exists. **License: "only for research or education purposes, not freely available for commercial use"; mocap data CC BY-NC 4.0.** — [AI4Animation GitHub](https://github.com/sebastianstarke/AI4Animation)
- **Local Motion Phases** (Starke, Zhao, Komura, Zaman; EA collaboration; ACM TOG 39(4)). Each bone gets its own phase, which lets the network learn asynchronous limb movement and **ball contact**. Demonstrated on **1v1 basketball**: dribbling, shooting, catching, avoidance, sharp turns. The basketball mocap is **not released**. — [Synced Review](https://syncedreview.com/2020/06/17/local-motion-phases-technique-boosts-basketball-animation-richness-and-realism/); [80.lv](https://80.lv/articles/local-motion-phases-for-learning-multi-contact-character-movements)
- Ubisoft's "Choreograph" animation tool (Far Cry 6) builds on Holden's learned approach. — [Ubisoft Far Cry 6 article](https://www.ubisoft.com/en-us/game/far-cry/far-cry-6/news-updates/27176jslYNMPt7vBfCaRQ1/far-cry-6-how-ai-helped-animate-yaras-hero)
- **Environment-aware Motion Matching** (Ponton et al., SIGGRAPH Asia 2025, ACM TOG) is the recommended citation of the open-source Unity MM package (see §6). — [JLPM22/MotionMatching](https://github.com/JLPM22/MotionMatching)

### Inferences
- Local Motion Phases is the closest published analogue to football dribbling. Ball-contact phases per foot are the key insight, and you can borrow that idea inside motion matching: add a "time-to-next-ball-touch" or foot-contact-phase feature to the MM feature vector.
- For a small team the recommended stack is **classical motion matching** (data-driven, debuggable, commercial-safe) + inertialization + procedural foot/ball IK. Neural controllers would need your own mocap and training infrastructure, and the reference code is non-commercial.

### Gaps
- I found no published football-specific (soccer) motion-control paper with released data in this session. The Local Motion Phases reference list reportedly cites a soccer control paper, but I did not identify it.
- I did not verify NBA 2K or Rocket League talks in this session.

## 4. Football team AI: pitch control, positioning, decision architectures, RL

### Takeaway
The best-documented quantitative model for "where should off-ball players be" is **pitch control**: Spearman (2016/2018, physics-based time-to-intercept) and Fernández & Bornn (2018, a position-and-velocity-dependent influence Gaussian). A game AI can compute it cheaply on a coarse grid and use it as an influence map for off-ball runs, pass selection and pressing. EA's FC IQ shows the commercial direction: formation-relative positioning set by player **roles** plus team-tactic parameters. Google Research Football is the main open RL testbed, but RL agents are not used to drive players in shipped football games.

### Cited Findings
- **Fernández & Bornn, "Wide Open Spaces: A statistical technique for measuring space creation in professional soccer"** (MIT Sloan 2018). Each player's influence on space depends on their position and **velocity relative to the ball**, and pitch value is weighted by ball location. With data from a single game, they show players like Messi and Iniesta occupy higher-value space. — [Barça Innovation Hub](https://elearning.barcainnovationhub.com/the-science-of-space-creation/); [paper PDF mirror](https://code.garrettmills.dev/Archives/papers-we-love_papers-we-love/raw/commit/6f92d68b5e4bea94d9453e93e3801b6fd2dbb49a/sports_analytics/2017-A-statistical-technique-for-measuring-space-creation-in-professional-soccer.pdf)
- **Spearman, "Beyond Expected Goals"** (MIT Sloan 2018). It builds on his 2016 physics-based pitch control (time-to-intercept with reaction time and max speed, plus probabilistic control accumulation) and uses 58 matches of Hudl tracking data. — [Lecture slides (S. Powers)](https://saberpowers.github.io/teaching/smgt432/lecture/tracking/slides.pdf); [Frontiers review](https://www.frontiersin.org/articles/10.3389/fspor.2021.676179)
- Open teaching implementations of pitch control and pass probability exist in Python (Soccermatics; Laurie Shaw's Friends-of-Tracking LaurieOnTracking repo, not re-fetched). — [Soccermatics](https://soccermatics.readthedocs.io/en/latest/_sources/lesson6/PassProbability.md.txt)
- **Google Research Football** (Kurach et al., AAAI 2020): a physics-based 3D football RL environment under a permissive open-source license (Apache 2.0). Baselines are IMPALA, PPO and Ape-X DQN on three full-game difficulty benchmarks, plus an 11-scenario "Football Academy". — [arXiv 1907.11180](https://arxiv.org/abs/1907.11180); [AAAI](https://ojs.aaai.org/index.php/AAAI/article/view/5878/5734)
- FC IQ: role-based positioning plus Team Tactics. ML is used for role familiarity, not (as far as published) for runtime decisions. — [EA FC 25 FC IQ](https://www.ea.com/fr-fr/games/ea-sports-fc/fc-25/news/pitch-notes-fc-25-fc-iq-deep-dive)

### Inferences
- A practical game AI architecture for a small team:
  1. **Team layer (FSM/HTN):** team phase (build-up, transition, settled attack, press, low block) chosen from possession, ball zone and score/time.
  2. **Formation-relative anchors:** each role has a base slot in a formation shape that is translated and compressed with the ball (horizontal/vertical compactness parameters, i.e. FC IQ-style Team Tactics).
  3. **Utility AI per player:** scores candidate actions (pass to X, dribble, shoot, make a run to cell Y, press, cover) using pitch control / influence-map values, pass-interception probability (Spearman-style time-to-intercept) and role weights.
  4. **Steering/locomotion:** arrive, separation and pursuit outputs feed the motion matching trajectory goal.
- Pitch control on a 32x20 grid over 22 players is about 14k player-cell evaluations, which is trivially Burst-able and can update at 5–10 Hz rather than every frame.
- RL (ML-Agents/GRF) is useful for offline tuning or for a narrow sub-skill like goalkeeper positioning. Full-team RL is expensive to train, hard to make readable or tunable, and has no commercial precedent in football games that I found.

### Gaps
- I did not retrieve GDC "AI in FIFA" or NBA 2K AI talks in this session. I am not certain an EA "AI in FIFA" GDC talk exists, so don't cite one without verification.
- I did not retrieve the Rocket League GDC 2018 physics talk ("It IS Rocket Science!", Jared Cone, not re-fetched) or DeepMind's 2021 "From motor control to team play in simulated humanoid football" (Science Robotics, not re-fetched). Both are relevant.

## 5. Ball physics: real models and how games fake them

### Takeaway
A credible football ball model needs gravity, **quadratic drag** (with the drag crisis around 10–20 m/s), the **Magnus force** from spin, spin decay, and a bounce/rolling model with restitution, friction and spin-velocity coupling. Games then layer heavy assistance on top: solving the kick velocity analytically to hit a target, "magnetising" the ball to the dribbler's foot during dribbles, and blending between authored and simulated trajectories. EA says its full-match mocap data improved ball-physics realism, but it has published no model.

### Cited Findings
- EA's FIFA 22 GDC abstract says the full-match capture data was used to improve "the realism of ball physics". — [GDC Vault](https://gdcvault.com/play/1027595/Animation-Summit-FIFA-22-s)
- FIFA 22 "Composed Ball Control" and ML-Flow ball approach both concern the **player-to-ball contact problem**: adjusting stride so contact lands on the right foot at the right frame. — [EA FIFA 22 Pitch Notes](https://www.ea.com/ea-play/news/pitch-notes-fifa-22-gameplay-deep-dive)
- FC 26 changed "foot preference logic and animation branching" for ball control responsiveness. — [EA FC 26 Pitch Notes](https://www.ea.com/games/ea-sports-fc/fc-26/news/pitch-notes-fc26-gameplay-deep-dive)

### Inferences (standard physics, no source fetched this session; verify coefficients before use)
- Forces: F = m·g − ½ρAC_d|v|v + ½ρA C_L r (ω × v)/|ω|·… In practice use **F_magnus = S (ω × v)** with S tuned so a 25 m/s, about 10 rev/s free kick curves 2–4 m over 25 m. Ball: m ≈ 0.43 kg, r ≈ 0.11 m, C_d ≈ 0.2–0.25 above the drag crisis and about 0.45 below it. ρ = 1.2 kg/m³.
- Spin decays exponentially (time constant of a few seconds in the air). On the ground, model rolling resistance plus a slip phase where friction converts spin into linear velocity (backspin "checks", topspin "runs").
- Bounce: normal restitution of about 0.6–0.8 that depends on impact speed. Tangential friction couples spin and velocity. Grass stiffness or wetness can scale these.
- **Integrate the ball with your own fixed-step integrator** (semi-implicit Euler or RK4 at 120–240 Hz) rather than PhysX rigidbodies. That gives determinism (needed for replays and netcode), cheap trajectory prediction for AI (time-to-intercept), and analytic shot and pass solving.
- **Assist techniques:** (a) pass targeting: choose the receiver or lead point, then solve for launch velocity and spin, iterating with the forward simulator; (b) shot assist: pick an aim point inside the goal from stick direction, attributes and error cones, then solve; (c) dribble "ball attachment": while dribbling, the ball is kinematically driven along a spline or spring toward a per-touch target, and each touch is a small impulse timed to the foot-contact event of the current animation. Release to full physics on tackle, deflection or a heavy touch; (d) first touch: blend ball velocity toward the desired post-touch vector, scaled by attributes and incoming speed.

### Gaps
- No primary source fetched for football aerodynamics coefficients (e.g. Goff & Carré, Asai et al. on the Jabulani/Brazuca drag crisis). The report writer should flag the numbers above as standard approximations, not sourced figures.
- No published EA or Konami ball model found.

## 6. Unity 6 tools for animation, IK, AI, ML and mocap (with price and license)

### Takeaway
**Unity has no official motion matching.** Kinematica (preview only, last version 0.8.0-preview) is **deprecated and abandoned**. Unity's "next-gen animation system", promised for the Unity 6 cycle, was reportedly **paused at Unite 2025** in favour of the CoreCLR work. The practical options are the **open-source MxM fork (Frost-Blade Studios)** or **JLPM22's MIT-licensed Motion Matching for Unity (Unity 6+)**, plus Animation Rigging (free) or Final IK ($90) for foot, ball and look IK. ML-Agents 4.x and Sentis (the inference package, `com.unity.ai.inference`) are alive on Unity 6.

### Cited Findings
**Motion matching**
- **Kinematica (DEPRECATED):** only preview versions up to 0.8.0-preview, with Unity's warning that they "might not be safe to use in production". The KinematicaX community fork was **archived Dec 29, 2023**. — [Unity docs 2020.2 Kinematica](https://docs.unity3d.com/2020.2/Documentation/Manual/com.unity.kinematica.html); [KinematicaX](https://github.com/voxell-tech/UnityKinematicaX)
- **Unity's new animation system:** planned as an experimental release during the Unity 6 cycle ([Unity forum Q3 2024](https://discussions.unity.com/t/animation-status-update-q3-2024-unite-announcement/1519289); [Q1 2025 GDC update](https://discussions.unity.com/t/animation-status-update-q1-2025-gdc-roadmap/1614718)). A secondary source then reported that Unity **paused new animation work** to focus on CoreCLR. — [Digital Production, Nov 2025](https://digitalproduction.com/2025/11/26/unitys-2026-roadmap-coreclr-verified-packages-fewer-surprises/). Unity 6.6 is out ([Unity forum](https://discussions.unity.com/t/unity-6-6-is-now-available/1735357)). A Unite Seoul 2026 recap says the Unity 7 preview starts in December 2026 with full release planned for Q1 2027 ([Unity blog, RU](https://unity.com/ru/blog/unite-seoul-keynote-2026-recap)). No animation specifics for Unity 7 were found.
- **MxM / "Motion Matching for Unity" (Frost-Blade Studios fork):** originally a paid Asset Store product by Kenneth Claassen, made **free and open source** when its author left the industry. The community fork is installable via git URL in Package Manager, with docs listing Unity 2020.3+ (2022.3 recommended) and Unity 6 tested. Check the LICENSE file. — [Docs](https://motion-matching-for-unity.readthedocs.io/en/latest/getting-started/); repo: https://github.com/Frost-Blade-Studios/Motion-Matching-for-Unity
- **JLPM22/MotionMatching (Jose Luis Ponton):** **MIT**, Unity 6+ (URP samples), about 605 stars. Features: a character controller with trajectory prediction and clamping of simulation-to-animation drift, inertialization blending, foot locking. It grew out of a master's thesis (2022), and the citation is "Environment-aware Motion Matching", SIGGRAPH Asia 2025. Described as work in progress. — [GitHub](https://github.com/JLPM22/MotionMatching)

**IK / procedural**
- **Final IK (RootMotion):** **$90/seat** on the Asset Store (undated snapshot). Full-body IK, Aim, LookAt, FABRIK, CCD, Grounder. RootMotion's FAQ says it can be used alongside Animation Jobs and Animation Rigging. Sister product **PuppetMaster** handles active ragdoll physics for tackles and falls. — [Asset Store Final IK](https://assetstore.unity.com/packages/tools/animation/14290); [RootMotion FAQ](https://rootmotion.freshdesk.com/support/solutions/articles/77000057786-faq); [PuppetMaster](https://assetstore.unity.com/packages/tools/physics/puppetmaster-48977/reviews)
- **Unity Animation Rigging** (`com.unity.animation.rigging`): free, built on Animation Jobs (Two-Bone IK, Multi-Aim, Damped Transform). Its 2026 maintenance status was not confirmed in this session.

**ML / inference**
- **ML-Agents:** package docs at v4.0/4.1. The Inference Engine page says it uses Sentis, that **CPU (Burst) inference is faster than GPU for most ML-Agents models**, and that Unity does not support inference outside Unity. — [ML-Agents 4.1 docs](https://docs.unity3d.com/Packages/com.unity.ml-agents@4.1/manual/Inference-Engine.html). A third-party source lists ML-Agents 4.1.0 (2026-07-29) as requiring Unity 6000.0+ and bundling Sentis 2.6.1 (unverified against the registry). — [issoh.co.jp](https://www.issoh.co.jp/md/tech/4905)
- **Sentis / "Inference Engine":** the package `com.unity.ai.inference` was branded "Inference Engine" in 2025 and reportedly **renamed back to Sentis from 2.4.0**. It replaced Barracuda as Unity's supported inference runtime. 2026 forum threads report GPU compute crashes and ONNX import issues. — [Unity roadmap (Barracuda → Sentis)](https://activation.unity3d.com/roadmap/2674-replace-barracuda-in-ml-agents); [Unity discussions tag](https://discussions.unity.com/tag/inference-engine/159492)
- **DOTS/Entities:** Unity's roadmap still lists "Advancing DOTS" and an "ECS-based animation" item. Status was not confirmed in this session. — [Unity roadmap DOTS](https://activation.unity3d.com/roadmap/287-advancing-dots); [ECS-based animation](https://activation.unity3d.com/roadmap/1204-ecs-based-animation)

**Animation / mocap content**
- **AA Soccer Player Animations:** about **$67.99**, 135 mocap clips (kicks, falls, throw-ins). — [Unity Asset Store](https://marketplace.unity.com/packages/3d/animations/aa-soccer-player-animations-package-237656)
- **Soccer Player with 27 action animations:** about **$55**. — [Asset Store](https://assetstore.unity.com/packages/3d/characters/soccer-player-with-27-action-animations-87427)
- **Soccer Actions** pack: [Asset Store](https://assetstore.unity.com/packages/3d/animations/soccer-actions-233382) (price not captured). "Football Animation Pack" (267564) is **deprecated / no longer for sale**. — [Asset Store](https://assetstore.unity.com/packages/3d/animations/football-animation-pack-267564)
- **Soccer Players & Stadiums pack:** [Asset Store](https://assetstore.unity.com/packages/slug/105891) (price not captured).
- **MoCap Online:** I found **no soccer-specific player pack**. Its **Stadium Crowd Animation Pack** (108+ mocap crowd anims: cheer, watch, sit, stand; FBX/BIP with Unity versions) costs **$34.99** on itch.io and Fab. — [itch.io](https://mocaponline.itch.io/crowd-anims)
- **Free / low-cost mocap sources (not re-fetched):** Mixamo (free with an Adobe ID, royalty-free in games; https://www.mixamo.com), CMU Graphics Lab Motion Capture Database (free, some soccer/kick clips; http://mocap.cs.cmu.edu), Rokoko (Smartsuit hardware + Rokoko Video markerless), Move.ai (multi-camera markerless capture, subscription). Prices and licenses were not verified this session. **For motion matching you need long unstructured locomotion takes (dance cards), not short game-ready loops.** Most Asset Store packs are loop-and-action clips and will only partially feed an MM database.
- Kevin Iglesias packs (Asset Store, e.g. "Human Animations"/"Basic Motions") were not verified this session for soccer content.

### Inferences
- **Recommended Unity 6 stack (small team):** JLPM22 MotionMatching (MIT, Unity 6-native, simple, research-backed) **or** the MxM fork (more mature tooling, from a former commercial product). On top of that: Animation Rigging for foot and ball IK (free) or Final IK + PuppetMaster ($90 + about $90) for richer full-body IK and active-ragdoll tackles. Custom fixed-step ball physics in Burst. A utility/FSM AI with a pitch control grid in Burst jobs. Use ML-Agents only for offline experiments.
- Mocap plan: buy one or two soccer action packs for kicks, tackles and celebrations, and capture your own locomotion dance cards with markerless capture (Move.ai or Rokoko Video) for the MM database: jog, sprint, cuts, turn-on-the-ball and shielding.
- Don't depend on Unity's future animation system for a 2027 ship date. Build on the Playables/Animator and Animation Jobs APIs that MM packages already use.

### Gaps
- Exact current prices for Rokoko, Move.ai and Kevin Iglesias packs, and the MxM fork license text, were not verified.
- Whether Unity has formally deprecated the Animation Rigging package was not confirmed.
- Stadium and crowd assets beyond MoCap Online's crowd anims were not surveyed in depth (crowd rendering is usually done with GPU-instanced impostors or VAT (vertex animation textures) rather than skinned meshes).

## 7. Performance on iPad (A/M-series) with 22 players

### Takeaway
I found no primary benchmarks for 22-character skinned crowds on iPad in this session. The guidance below is engineering inference. The main budget items are skinning (22 to 25 characters with goalkeepers and referees), motion matching search cost, and IK passes. All are tractable on M-series iPads at 60 fps if you use GPU skinning, Burst-compiled MM search at a reduced rate, and LODs.

### Cited Findings
- ML-Agents/Sentis guidance: CPU (Burst) inference beats GPU for small models, which matters if any neural component (learned MM, a decompressor) runs on device. — [ML-Agents docs](https://docs.unity3d.com/Packages/com.unity.ml-agents@4.1/manual/Inference-Engine.html)
- Learned motion matching motivation: plain MM database memory grows linearly with data, and LMM cuts memory substantially. This matters on memory-limited mobile. — [Ubisoft La Forge](https://www.ubisoft.com/en-us/studio/laforge/news/3NWqwcMU9lMfumgfPAEfka/learned-motion-matching)
- EA uses relaxed "low-urgency" animations for players far from the ball. — [EA FC 26](https://www.ea.com/games/ea-sports-fc/fc-26/news/pitch-notes-fc26-gameplay-deep-dive)

### Inferences
- **Character budget:** about 25 skinned characters. Target 15–25k triangles per player at LOD0 (close camera), 5–8k at LOD1 and 1.5–3k at LOD2. Use 2–3 materials per player (body, kit with a shared atlas plus per-team tint, head) and GPU skinning (Unity 6 supports compute/GPU skinning on Metal). Use about 4 bone influences per vertex, and 2 at distant LODs.
- **Motion matching cost:** run the MM search every 3–6 frames (about 10–20 Hz) per player, staggered across players. Burst/Jobs search with a KD-tree or AABB-pruned brute force. Reduce the feature vector for distant players, or use a cheap blend-tree fallback for players off-camera or far from the ball (an "animation LOD", same idea as EA's low-urgency anims).
- **IK:** full foot and ball IK only for the ball carrier and nearby players. Distant players get no IK.
- **AI:** pitch control/influence grid at 5–10 Hz in Burst. Per-player utility evaluation staggered over frames.
- **Ball:** a single fixed-step simulation at 120–240 Hz is negligible.
- **Crowd:** GPU-instanced impostor or VAT crowd, never skinned meshes. MoCap Online crowd anims can be baked to VAT.
- **Thermals:** iPads throttle under sustained load. Budget for 60 fps on M1/A15-class hardware with headroom (about 12 ms of a 16.6 ms frame), and offer a 30 fps or "ProMotion off" mode on older A-series chips.

### Gaps
- No measured benchmarks (MM search cost per character on Apple silicon, GPU skinning throughput on A-series) were found. The project should prototype and profile with Xcode Instruments / Unity Profiler on target devices early.
