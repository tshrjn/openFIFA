# Existing football games: how they were built and what transfers to OpenFifa (Unity 6, iPad)

Researched 2026-11-04. Sources are mostly web pages and search-result snippets; where a claim rests only on a snippet it is marked "(snippet)". Older information is dated.

## Q1. Google Research Football / Gameplay Football: architecture, physics, AI, licence, reuse

### Takeaway
Google Research Football (GRF) is a heavily customised fork of Bastiaan Konings Schuiling's C++ "Gameplay Football". GRF is Apache-2.0 and the original GameplayFootball repo is under the Unlicense, so both can legally be reused. GRF was archived on 2026-08-19, though. Its real value to a Unity team is as a design reference (rule set, action set, rule-based AI with a difficulty knob, a 10 Hz decision rate), not as code to port.

### Cited Findings
- GRF is "an RL environment based on open-source game Gameplay Football". The maintainers thank Bastiaan Konings Schuiling, "who authored and open-sourced the original version of this game." — [GitHub google-research/football](https://github.com/google-research/football)
- The repo was archived on 19 Aug 2026 and is read-only. It points users to a newer game server at research-football.dev. — [GitHub google-research/football](https://github.com/google-research/football)
- GRF licence: Apache License 2.0 (LICENSE file). — [GRF LICENSE](https://raw.githubusercontent.com/google-research/football/master/LICENSE)
- The original repo BazkieBumpercar/GameplayFootball has the description "Football game, discontinued". Its licence is SPDX "Unlicense", it was last pushed 2021-03-29, and it has about 252 stars (GitHub API, checked 2026-11-04). — [GitHub API: BazkieBumpercar/GameplayFootball](https://api.github.com/repos/BazkieBumpercar/GameplayFootball)
- The paper calls it "an advanced, physics-based 3D simulator" in "highly optimized C++". It is built on a heavily customised version of GameplayFootball, renders 1280×720 RGB, runs with or without GPU rendering, and does about 140M steps/day on a single 6-core machine. — [Kurach et al., arXiv 1907.11180 (ar5iv HTML)](https://ar5iv.labs.arxiv.org/html/1907.11180)
- Agents and humans share one interface: "a single action per 100 ms" (10 Hz decisions). A full game is 3000 steps. The paper's footnote says this is 300 s at 10 fps, but the text also says "55 minutes", so the paper contradicts itself here. — [GitHub](https://github.com/google-research/football); [ar5iv](https://ar5iv.labs.arxiv.org/html/1907.11180)
- Action set (19): 8 movement directions plus Short Pass, High Pass, Long Pass, Shot, Do-Nothing, Sliding, Dribble, Stop-Dribble, Sprint, Stop-Moving and Stop-Sprint. Movement and sprint persist until a matching stop action. A "switch active player" action was removed because policies learned to exploit it. — [ar5iv](https://ar5iv.labs.arxiv.org/html/1907.11180)
- Rules simulated: goals, fouls, yellow and red cards, offsides, handballs, goal kicks, throw-ins ("side kicks"), corners and penalties. Players have varying stats and tire over the match. — [ar5iv](https://ar5iv.labs.arxiv.org/html/1907.11180)
- Built-in opponent: a rule-based bot with difficulty θ ∈ [0,1] (easy 0.05, medium 0.6, hard 0.95). Non-active teammates are driven by a separate rule-based bot. The opponent can be swapped for a trained model. — [ar5iv](https://ar5iv.labs.arxiv.org/html/1907.11180)
- There are stochastic (default) and deterministic modes. In stochastic mode the same shot from the top of the box can have different outcomes. — [ar5iv](https://ar5iv.labs.arxiv.org/html/1907.11180)
- Observations: pixels, a "Super Mini Map" (four 72×96 binary planes), or a 115-float vector. — [ar5iv](https://ar5iv.labs.arxiv.org/html/1907.11180)
- Scenarios include `academy_empty_goal_close`, `academy_pass_and_shoot_with_keeper`, `academy_run_to_score_with_keeper` and `11_vs_11_easy_stochastic`. — [GitHub](https://github.com/google-research/football)

### Inferences
- Both licences allow reuse. Apache-2.0 requires keeping the NOTICE and attribution; the Unlicense is effectively public domain. Porting C++/SDL/OpenGL code to Unity C# is still a rewrite, so the reusable parts are design artifacts. That means the rule-state machine (set pieces, offside, cards), the rule-based teammate and opponent AI structure, the single difficulty scalar, and the separation between a 10 Hz decision tick and a faster physics/animation tick.
- The removed "switch player" action is a concrete warning: player-switching logic is a known exploit surface and needs explicit design (cooldowns, auto-switch rules).
- A headless deterministic mode like GRF's is worth copying, for automated AI regression tests and possibly training ML-Agents policies.

### Gaps
- I could not extract the PDF's details on GameplayFootball's internal physics model (ball drag/spin, integration step) or its animation system. My PDF tooling was unavailable and the ar5iv summary did not cover them. Reading the source (`third_party/gfootball_engine`) would answer this.
- I did not verify whether the original GameplayFootball media assets carry the same Unlicense terms as the code.

## Q2. Other open-source football games on GitHub

### Takeaway
I found no complete, permissively licensed 11v11 3D Unity football game. What exists is small: arcade/2D prototypes, a GPL-3.0 Unity arcade game with behaviour-tree AI, and ML-Agents soccer experiments. None comes close to GRF's completeness.

### Cited Findings
- **matinaghaei/2D-Soccer-Game**: a top-down Unity soccer game with AI players. The licence was not confirmed in the snippet. — [GitHub](https://github.com/matinaghaei/2d-soccer-game) (snippet)
- **Dubium0/FutbolSim**: a Unity arcade football game whose single-player AI uses behaviour trees. GPL-3.0, so it is copyleft and unsuitable to copy into a closed-source game. — [GitHub](https://github.com/Dubium0/FutbolSim) (snippet)
- **AI-Soccer (2v2)**: Unity ML-Agents, trained with MA-POCA over 12M steps. — [portfolio page](https://astro-personal-portfolio.onrender.com/projects/ai-soccer) (snippet)
- **"Football Game Engine Basic"**: a commercial Unity Asset Store template (not open source). — [Unity Asset Store](https://assetstore-fallback.unity.com/packages/templates/packs/football-game-engine-basic-153048) (snippet)
- **klymenko-vlad/pro-football-26**: a GitHub project with a PR adding motion capture for players (v0.26.0). — [GitHub PR #4](https://github.com/klymenko-vlad/pro-football-26/pull/4) (snippet; completeness unknown)
- A Unity forum showcase of procedural soccer character animation lists the hard problems as precise movement and "foot placement / planting without 'sliding' and 'skating'". — [Unity Discussions](https://discussions.unity.com/t/procedural-character-animation-soccer/769433) (snippet)

### Inferences
- GRF's C++ engine remains the most complete open football simulator. A Unity team should expect to build its own core and borrow designs, not code.
- ML-Agents (MA-POCA) is a realistic way to prototype or tune off-ball positioning, though shipping RL-driven AI on iPad adds inference cost and is hard to control. Rule-based AI with utility scoring (as in GRF) is the safer default.

### Gaps
- I did not locate or verify "OpenSoccer" or "Yorg"; my queries did not return them. Yorg, as I recall, is a racing game, not football, so it is probably irrelevant (unverified).
- I did not check licences and star counts for most repos beyond GameplayFootball and GRF.
- I did not research Godot soccer projects.

## Q3. Indie/AA and commercial football games: engine, team, praise and criticism

### Takeaway
Engine choice barely predicts feel quality. Unity (Sociable Soccer, FM26's visuals), Unreal (Rematch, UFL, eFootball) and proprietary engines (First Touch) have all produced both praised and criticised games. The repeated failure modes are "robotic" movement, goalkeepers, and balance. The repeated success factors are responsive, precise ball control and a ball that is tuned as the core mechanic.

### Cited Findings
**Rematch (Sloclap, 2025)**
- Built on Unreal Engine 5, directed by Pierre Tarno. — [Wikipedia](https://en.wikipedia.org/wiki/Rematch_(video_game)); Epic's interview with Tarno and TD Benoit Jacquier (29 Sep 2025): [Unreal Engine](https://www.unrealengine.com/en-US/developer-interviews/rematch-reinvents-football-using-unreal-engine-5) (page returned 403; details from snippet only)
- Tarno calls the ball "the basic brick of the game". Small changes alter players' timing and distance judgment. Bounce height and curve/spin are treated as sensitive tuning parameters because they affect muscle memory. Kicks must feel "satisfying and responsive", but the ball can't travel so fast or far that defenders and keepers can't react. For example, keeper punts shouldn't cross the pitch uncontested. The team sometimes compromises realism for flow, and the game has "a lot of fighting game DNA". — [Console & PC Gaming](https://consolepcgaming.com/how-rematch-developers-balance-the-balls-feel-and-function-in-gameplay)
- Tarno has said patching struggled to keep up with the complexity of the game's player physics. — [GamesRadar](https://www.gamesradar.com/games/sports/players-are-very-precise-and-very-demanding-rematch-was-a-very-strong-learning-curve-for-the-sifu-devs-but-sloclap-is-still-working-to-find-the-back-of-the-net) (snippet; full article unreadable)

**UFL (Strikerz Inc., Dec 2024)**
- Unreal Engine 5. Reviewers found the visuals decent but below EA FC. The gameplay was called "utterly disappointing" by The Xbox Hub, while Sports Gamers Online found it "pretty good" but said players "move robotic". OpenCritic excerpts cite goalkeepers and balance issues. It was delayed after a heavily criticised beta. — [The Xbox Hub](https://www.thexboxhub.com/ufl-review/); [SGO](https://www.sportsgamersonline.com/games/soccer/ufl-review-a-solid-foundation/amp/); [OpenCritic](https://opencritic.com/game/17857/-); [SoccerGaming beta 2](https://soccergaming.com/ufl-second-open-beta-gameplay-improvements-and-ongoing-challenges-ahead-of-release/) (all via search snippets)

**eFootball (Konami, 2021 onward)**
- PES moved from FOX Engine to Unreal Engine 4. eFootball 2022 was Metacritic's worst-reviewed game of 2021, with criticism of graphics, content and controls. Konami cited the new engine and platforms as contributing causes. Konami also claimed four times more animations than earlier PES games, with a more complex animation system. — [VGC](https://www.videogameschronicle.com/news/konami-insists-its-taking-criticism-of-efootball-very-seriously-ahead-of-big-update); [NME](https://www.nme.com/news/efootball-2022-feedback-being-taken-very-seriously-by-konami-3201674); [PC Gamer](https://pcgamer.com/pes-efootball-2022-release-date) (via snippets)

**Football Manager match engine (Sports Interactive)**
- The 3D match engine debuted in FM2009 with motion-captured animations (SEGA Sports R&D data). — [Worth Playing (2008)](https://worthplaying.com/article/2008/9/4/news/54504-football-manager-2009-psppc-announced-screens/)
- FM24 began moving from mocap to "motion matching" using real-match data. — [Fingaz](https://fingaz.co.zw/?p=297129) (snippet; secondary source)
- FM26 combines Hawk-Eye skeletal volumetric tracking data with SI's own mocap. — [Hawk-Eye](https://www.hawkeyeinnovations.com/news/4369378/hawkeye-and-sports-interactive-team-up-to-power-football-managers-new-era)
- FM26 moved to Unity for rendering. Core simulation (match engine logic, tactics, decision-making) stays proprietary. Critics praised the match engine (PC Gamer 75: "fantastic match engine update"; Top Gear: "brilliant") while criticising the UI, bugs and stability. FM26 has the series' lowest Metacritic score. — [Wikipedia FM26](https://en.wikipedia.org/wiki/Football_Manager_26); [PC Gamer](https://www.pcgamer.com/games/sports/football-manager-26-review); [Top Gear](https://topgear.com/car-news/gaming/football-manager-26-review-fantastic-match-engine-tangled-mess-menus)

**Sociable Soccer (Jon Hare / Tower Studios with Combo Breaker)**
- A Sensible Soccer spiritual successor built in Unity ("We're in Unity!"). It is fully 3D, so it can use cameras other than top-down. It was designed for touch and controller from the start, shipped on Apple Arcade (premium, with card purchases removed), and is cross-platform. The design goal is the "fast controls and depth" of Sensible Soccer, and recent work focused on tightening controls. — [Pocket Gamer interview](https://www.pocketgamer.com/sociable-soccer/an-interview-with-jon-hare-on-sociable-soccer-apple-arcade-and-how-his-team-made); [Wikipedia: Jon Hare](https://en.wikipedia.org/wiki/Jon_Hare)
- Sociable Soccer 25 was announced for PC and consoles for November 2024. — (search snippet, source list in [Jon Hare Wikipedia](https://en.wikipedia.org/wiki/Jon_Hare))

**First Touch Games (Dream League Soccer, Score! Match, Score! Hero)**
- DLS was built on an updated First Touch Soccer engine. Director Charles Chapman called it "our new 2012 engine", which suggests a proprietary engine (2012 info). — [AppSpy](https://www.appspy.com/news/first-touch-soccer-developers-working-on-free-to-play-follow-up-dream-league-soccer/); [Pocket Gamer](https://www.pocketgamer.com/dream-league-soccer/first-touch-games-kicks-off-with-free-to-play-dream-league-soccer-for-ios/) (via snippet)
- The studio's catalogue: Dream League Soccer, Score! Hero, Ultimate Clash Soccer, Score! Match, 8 Ball Hero. — [ftgames.com](https://www.ftgames.com/games)

**Pro Soccer Online (Skywall Studios LLC)**
- A physics-based, input-driven online 6v6 game. The store text credits "the physics-based system governing the ball's movement, collisions, and player interactions" for realism and unpredictability. Steam reviews are "Very Positive" (about 23k). Store pages list a release date of 5 Dec 2025 and also label it early access. — [Steam](https://store.steampowered.com/app/1583320/Pro_Soccer_Online/); [Discord listing](https://discord.com/game/pro-soccer-online-1124351980840698066)

**Kopanito All-Stars Soccer / Active Soccer 2 DX**
- Kopanito is by Merixgames, its first game, started July 2014. — [Steam](https://store.steampowered.com/app/399820/Kopanito_AllStars_Soccer/)
- Active Soccer 2 DX is by The Fox Software, described as an extended, polished version of Active Soccer 2. — [OpenCritic](https://opencritic.com/game/2530/view); [SWOS United forum](https://community.swosunited.com/forum/other-systems-remakes/26209-active-soccer-2-dx-soon-available-on-xbox-one)

### Inferences
- Rematch shows that a team new to football can make the ball the centre of tuning (bounce, curve, kick power ceilings). For OpenFifa that suggests treating ball parameters as a versioned, data-driven config and protecting players' muscle memory after launch.
- UFL and eFootball 2022 show that matching EA visually does not rescue gameplay. "Robotic" movement and goalkeepers are the first things reviewers punish, and an engine migration mid-franchise is very risky.
- FM26 splits a proprietary simulation from a Unity presentation layer. That is a good architecture for OpenFifa: a deterministic C# simulation core (ball physics, rules, AI decisions) separated from Unity animation and rendering.
- Sociable Soccer is the closest public precedent for a Unity football game designed for both touch and controller on Apple hardware.

### Gaps
- No reliable engine or team-size data found for Kopanito, Active Soccer, Pro Soccer Online (Unity unconfirmed), Soccer Story, Football Simulator, Score! Match or SWOS remakes.
- I found no First Touch Games GDC talk, postmortem or engine interview beyond 2012 quotes. Whether DLS now uses Unity is unverified.
- I did not research EA FC 26 or 27 details here; that is out of scope for this note.
- No Rematch netcode details were found (a "Netcode 2.0" headline exists but no specifics).

## Q4. Postmortems, devlogs, GDC talks

### Takeaway
The most transferable public technical material comes from EA's GDC talks: animation warping for responsiveness, and HyperMotion full-match mocap feeding ML. Indie devlogs mostly document foot-sliding and touch-timing problems.

### Cited Findings
- GDC, "Animation Warping for Responsiveness in FIFA Soccer" (EA): turn animations are procedurally altered on the fly to reach a target point at a target time, and warping can beat blending for responsiveness. Techniques include IK-driven ball touches, volleys, height warping, face-angle warping and time warping, which keep momentum and stop players running through the ball. — [GDC Vault](https://gdcvault.com/play/1012342/Animation-Warping-for-Responsiveness-in); [slides mirror](https://slidetodoc.com/animation-warping-for-responsiveness-in-fifa-soccer-paul/)
- GDC Animation Summit, "FIFA 22's HyperMotion: Full-Match Mocap Driving Machine Learning Technology". — [GDC Vault](https://gdcvault.com/play/1027746/Animation-Summit-FIFA-22-s)
- An indie football player-controller devlog describes automatic positioning and animation selection based on the incoming ball's angle and which foot is used. — [itch.io RawFormula devlog](https://rawformula-prototypes.itch.io/football-player/devlog/1555012/football-player-controller) (snippet)
- A soccer design note says movement animations may be interrupted halfway when a new direction or velocity is input, to keep the game responsive. — [properlydecent.com blog](http://properlydecent.com/blog/soccer_game_development_concepts/) (snippet; page failed to load over TLS)

### Inferences
- For Unity 6, the EA approach maps to root motion plus runtime warping (target-matching / `MatchTarget`, or custom root-motion scaling and rotation), with Animation Rigging IK for the foot-to-ball contact.

### Gaps
- No First Touch, Sloclap or Strikerz postmortem was found on Game Developer or GDC Vault in this pass.

## Q5. Common pitfalls for small teams

### Takeaway
The recurring pitfalls are the animation-vs-responsiveness tradeoff, foot sliding and "plop" toward the ball, desynced kick timing, ball over-tuning, player-switch exploits, and AI or goalkeeper quality. Each has a known mitigation.

### Cited Findings
- **Responsiveness vs animation:** warping beats blending for hitting a target point and time. — [GDC Vault (FIFA)](https://gdcvault.com/play/1012342/Animation-Warping-for-Responsiveness-in)
- **Ball "magnetism" and sliding:** a community note says that sliding toward the ball in the movement direction lets players "cheat" by moving faster than the animation intended, and slow dribbling suffers when the player "plops" toward the ball before each touch. — (search snippet; source listed in the result set for [Unity Discussions](https://discussions.unity.com/t/procedural-character-animation-soccer/769433) and [itch.io devlog](https://rawformula-prototypes.itch.io/football-player/devlog/1555012/football-player-controller); exact page not confirmed)
- **Foot planting:** "foot placement / planting without 'sliding' and 'skating'" is named as a core difficulty. — [Unity Discussions](https://discussions.unity.com/t/procedural-character-animation-soccer/769433)
- **Kick timing:** fire the ball impulse from an animation event or notify at the foot-contact frame. — (search snippet referencing an Unreal guide; URL not captured)
- **Motion matching for locomotion** handles sports locomotion better than hand-built blend trees (vendor claim). — [Mocap Online](https://mocaponline.com/blogs/mocap-news/sports-motion-capture-guide)
- **Ball tuning and muscle memory:** bounce and spin changes disrupt players. — [Console & PC Gaming (Rematch)](https://consolepcgaming.com/how-rematch-developers-balance-the-balls-feel-and-function-in-gameplay)
- **Exploits:** GRF removed player switching because agents exploited it. — [ar5iv](https://ar5iv.labs.arxiv.org/html/1907.11180)
- **Goalkeepers and "robotic" movement** were the top review complaints for UFL. — [OpenCritic](https://opencritic.com/game/17857/-); [SGO](https://www.sportsgamersonline.com/games/soccer/ufl-review-a-solid-foundation/amp/)
- **Physics complexity slowing patches** (Rematch). — [GamesRadar](https://www.gamesradar.com/games/sports/players-are-very-precise-and-very-demanding-rematch-was-a-very-strong-learning-curve-for-the-sifu-devs-but-sloclap-is-still-working-to-find-the-back-of-the-net) (snippet)

### Inferences
- Recommended hybrid for OpenFifa: motion matching or blend trees for locomotion; interruptible actions with warping (time, angle, height) for touches, passes and shots; IK for contact; the ball impulse fired from the contact-frame event; a capped, visible "magnetism" (assisted ball attachment) tuned so dribble speed never exceeds the animation's natural speed.
- Keep simulation (ball physics, rules, AI decisions at about 10 Hz like GRF) deterministic and separate from presentation. That enables headless tests and replays and makes tuning safe.
- Budget early for goalkeeper AI and animation coverage, since reviewers judge them first.

### Gaps
- I found no quantitative data (input-latency targets in ms, magnetism radii, ball drag coefficients) from any shipped football game.
- No iPad-specific performance postmortem for a football game was found.
