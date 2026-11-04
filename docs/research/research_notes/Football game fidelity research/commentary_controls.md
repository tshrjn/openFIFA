# Commentary Systems and EA FC-Style Controls (Gamepad + iPad Touch) for OpenFifa (Unity 6, iPad)

Research date: 2026-11-04. Prices and specs come from web sources available on that date. Where sources conflict, this is flagged.

## How FIFA/FC and PES commentary works (stitched lines, tagging, names, priority, play-by-play vs colour)

### Takeaway
FIFA/FC commentary is built from thousands of short ad-libbed clips. A rules-based "commentary AI" sits between game events and audio. It picks lines using objective game parameters (score, pitch position, shot velocity) and gates them by crowd intensity. Player names are recorded separately, in several tones, and stitched in. As of FC 26, EA uses an AI clone of Guy Mowbray's voice, with his permission, for some of the 20,000+ name callouts. Longer emotional and colour lines are still recorded live.

### Cited Findings
- **Ad-libs replaced scripts.** EA's early scripted commentary "wasn't very good" because commentators "are not actors". The team switched to ad-libs, with example lines given only to set tone and length. Talent get target counts per line type ("about 10", "about 15 variations") and are not forced to hit them. — [Designing Sound, interview with EA audio director Jeff MacPherson (2013)](https://designingsound.org/2013/02/spectral-analysis-interview-with-jeff-macpherson/)
- **How lines are performed.** Talent sometimes watch footage or hear crowd audio in headphones to get the emotional reaction right. For events like penalties they react to randomised highlight reels they haven't seen before. — [Designing Sound](https://designingsound.org/2013/02/spectral-analysis-interview-with-jeff-macpherson/)
- **Runtime architecture.** A commentary "AI system" sits between the game and the speech. MacPherson calls the result "emergent behavior" and names line-to-line matching (stitching) as the hardest problem. Context rules are added over time to limit which situations can trigger which lines, without "hardwiring the system". — [Designing Sound](https://designingsound.org/2013/02/spectral-analysis-interview-with-jeff-macpherson/)
- **Event tagging.** Programmers supply triggers, parameters, cues and hints. The objective data includes score, field position and shot velocity. Subjective events (how hard a tackle *looked*) are a known gap, so tackles often get no commentary. — [Designing Sound](https://designingsound.org/2013/02/spectral-analysis-interview-with-jeff-macpherson/)
- **Priority and interruption via crowd intensity.** Some lines play "only if the crowd intensity is below a certain level". Resolution lines, such as a shot going wide, are exceptions. — [Designing Sound](https://designingsound.org/2013/02/spectral-analysis-interview-with-jeff-macpherson/)
- **Localisation tiers.** Commentary quality is prioritised for "MEFIGS" (Mexico, England, France, Italy, Germany, Spain), about 90% of sales, at roughly "80-90%" of English quality. Other languages come later. — [Designing Sound](https://designingsound.org/2013/02/spectral-analysis-interview-with-jeff-macpherson/)
- **AI voice for names in FC 26.** FC 26 has over 20,000 player names, each needing several tones by context. A BBC report says EA uses "AI replicating his [Guy Mowbray's] voice with his permission for some of the names". EA says commentary is "always a collaboration with our talent - not a replacement". FC 26's Steam page carries a generative-AI disclosure added before its 25 Sept 2025 launch. — [PCGamesN (18 Feb 2026)](https://www.pcgamesn.com/ea-sports-fc-26/ai-commentary-lines-guy-mowbray); [VGC](https://www.videogameschronicle.com/news/ea-sports-fc-commentator-lets-ea-use-an-ai-version-of-his-voice-to-record-some-lines-its-revealed)
- **Variation and recording schedule.** Mowbray says longer lines must be in his own words and style, with about ten phrasings for each line. Recording for the next game is weekly from November to July. Co-commentator Sue Smith (FC 24–26) also records multiple variants of her insight and colour lines. — [PCGamesN](https://www.pcgamesn.com/ea-sports-fc-26/ai-commentary-lines-guy-mowbray)
- **Session length and tone matching.** Smith describes intense sessions of four to five hours and says keeping the same voice tone across sessions is hard, which matters for stitching. — [FootballTransfers (Feb 2026)](https://www.footballtransfers.com/us/transfer-news/uk-premier-league/2026/02/the-art-of-video-game-commentary-explained-by-ea-sports-fc-and-fifa-legend)
- **Name variants.** For FIFA 19's Champions League mode, commentators recorded many player names several times for variety. — [Goal.com](https://www.goal.com/en/news/fifa-19-champions-league-mode-the-secrets-from-behind-the-scenes-/17kcfqc22q051jdl3slvq5t7l)
- **Split recording.** Play-by-play and colour commentators are often recorded separately, sometimes in different countries. One commentator felt this "doesn't work as well" as recording together. — [FUTBIN interview with Alan Smith](https://www.futbin.com/news/articles/1445/futbin-meets-alan-smith)
- **Stitch checks at EA (NHL).** EA Sports' NHL team pauses every few cues to export the audio and check that lines will "stitch" well at runtime. — [SVG interview with EA audio director Eric Paul (2021)](https://www.sportsvideo.org/2021/10/28/svg-sit-down-ea-sports-audio-director-eric-paul-talks-sound-for-nhl-22/?print=1)

### Inferences
- A practical OpenFifa architecture copies EA's pattern:
  1. Game events are tagged with objective parameters (event type, team, player ID, pitch zone, xG/shot speed, minute, scoreline).
  2. A rule/priority selector picks a line category.
  3. Line playback has a priority level and an "interruptible" flag. Resolution lines (goal, save, wide) can pre-empt build-up lines.
  4. Lines are gated by a crowd-intensity value.
  5. Repetition is avoided with per-line cooldowns and random variant selection, about 10 variants per category, matching Mowbray's figure.
  6. Play-by-play (lead) and colour (analyst) run as two voices. Colour lines go into dead-ball windows: fouls, throw-ins, replays.
- Name stitching is the scalability bottleneck even for EA. An indie team with fictional rosters should synthesise names in 2–3 intonations ("neutral / rising-excited / falling-resolution") and cache them offline.

### Gaps
- No public GDC talk on the modern FC commentary engine (its rule language, stitch/prosody method, line counts) turned up. The best public source is still the 2013 Designing Sound interview.
- No primary-source detail on PES/eFootball's commentary system was found.
- No published total line count for FC 26 commentary was found.

## Modern alternatives: LLM-generated commentary + TTS (vendors, cost, latency, licensing, prior art)

### Takeaway
A hybrid is the realistic approach for an iPad game:
- **Pre-generated (offline) TTS banks** for names and the high-frequency event lines, built with ElevenLabs, Azure or Kokoro.
- **Optional live LLM "colour" lines** spoken only in dead-ball windows.

Fully live LLM+TTS play-by-play struggles with latency. Research systems address it with parallel generation and event prediction. On cost: cloud TTS runs about $15–80 per million characters. Kokoro-82M (Apache-2.0) can run on-device on iPad. Apple Personal Voice is not suitable.

### Cited Findings
- **ElevenLabs API (official pricing page, fetched Nov 2026):**
  - Per 1K characters: Flash/Turbo $0.04, v4 Turbo $0.04, v3 Conversational $0.04, v4 $0.08, v3 $0.08, v2 Multilingual $0.08.
  - Latency: Flash/Turbo about 75 ms, v4 Turbo about 100 ms, v3 Conversational about 280 ms.
  - Plans: Starter $6/mo (75K premium or 150K Flash characters), Creator $22, Pro $99, Scale $299, Business $990.
  - The page does not state TTS commercial-licence terms.
  — [ElevenLabs API pricing](https://elevenlabs.io/pricing/api)
- **Older ElevenLabs figures (conflict).** Earlier 2026 third-party summaries gave Flash at $0.05 and Multilingual at $0.10 per 1K characters. The official page now shows lower prices. — [invideo (Aug 2026)](https://invideo.io/blog/ai-voice-model-pricing/)
- **ElevenLabs latency caveat.** Published latency excludes application and network latency, so real end-to-end latency is higher. — [smallest.ai summary](https://smallest.ai/blog/elevenlabs-pricing-explained-plans-limits-hidden-costs-calculator)
- **OpenAI gpt-4o-mini-tts.** $0.60 per 1M text-input tokens and $12 per 1M audio-output tokens, with a 2,000-token input limit. That is about $0.015 per minute of audio. — [OpenAI model page](https://developers.openai.com/api/docs/models/gpt-4o-mini-tts); [Neowin launch coverage](https://www.neowin.net/news/openai-announces-next-generation-audio-models-to-power-voice-agents/)
- **Azure neural TTS.**
  - Standard neural: about $15–16 per 1M characters (sources disagree; the more recent ones say $16).
  - Neural HD: about $22 per 1M (reportedly cut from $30 in March 2026).
  - Free tier: 500K characters per month.
  - These figures are third-party; verify on Microsoft's own page.
  — [perkstack price tracker (Jul 2026)](https://perkstack.co/blog/cheapest-azure-tts-api); [texttolab](https://texttolab.com/blog/azure-text-to-speech-pricing)
- **Other cheap TTS options.** Deepgram Aura-2 is about $0.030 per 1K characters. Cartesia Sonic is noted for low streaming latency. — [fast.io roundup](https://fast.io/resources/best-ai-text-to-speech-tools-2026/)
- **Kokoro-82M (open, on-device).**
  - Weights, CoreML conversion and G2P/dictionaries are all Apache-2.0, with "no GPL dependencies".
  - About 80 MB in INT8 and runs on the Apple Neural Engine.
  - The CoreML build needs iOS 18+.
  - 54 built-in voices; it does not clone voices.
  — [soniqo Kokoro guide](https://soniqo.audio/guides/kokoro); [kokoro-mlx (MIT code)](https://github.com/gabrimatic/kokoro-mlx)
- **Piper.** There is a Unity port, but it lists Windows, macOS and Android, not iOS. The active engine repo is `piper1-gpl` (GPL). Each voice model has its own licence. — [piper-unity](https://github.com/unitycoder/piper-unity)
- **Apple AVSpeechSynthesizer / Personal Voice.**
  - Personal Voice needs `requestPersonalVoiceAuthorization` (iOS 17+). The user must first enable "Allow Apps to Request to Use" in Settings.
  - Apple says Personal Voice is sensitive and should be used primarily for augmentative/alternative communication apps.
  - The voice belongs to the user, so it cannot serve as a game commentator voice.
  - Standard system voices via AVSpeechSynthesizer are free and on-device, but they sound robotic for sports.
  — [Apple docs](https://developer.apple.com/tutorials/data/documentation/avfaudio/avspeechsynthesizer/requestpersonalvoiceauthorization(completionhandler:).md); [WWDC23 notes](https://wwdcnotes.com/notes/wwdc23/10033); [Ben Dodson](https://bendodson.com/weblog/2024/04/03/using-your-personal-voice-in-an-ios-app/)
- **Research: latency.** A 2026 IJCAI paper names the sequential pipeline (game input → text → speech → next) as the main latency bottleneck in real-time audio commentary. It uses parallel LLM text generation and deliberately delays the video to keep commentary in sync. — [arXiv 2606.13322 / IJCAI 2026](https://arxiv.org/pdf/2606.13322)
- **Research: event prediction.** A paper on live commentary for soccer video games finds that predicting events and generating commentary ahead of time "significantly reduces response latency". It notes that earlier LLM methods suffer from repetitive phrasing and delayed responses. — [MDPI Mathematics 13(17):2738](https://www.mdpi.com/2227-7390/13/17/2738); related thesis [U. Twente](https://essay.utwente.nl/essays/100983)
- **Research: MatchTime / MatchVoice (EMNLP 2024).** Automatic commentary from real match video. It realigns the timestamps of SoccerNet-Caption, re-annotates 49 test matches, and feeds a visual encoder plus temporal aggregator into an LLM decoder. This is video-based, not game-state-based, so it is less relevant to a game that already knows its events. — [arXiv 2406.18530](https://arxiv.org/html/2406.18530)
- **Hobby prior art.** One project feeds live data (goals, possession) into an LLM prompt template, then TTS (a foosball table). Another is Ably's web demo streaming OpenAI commentary. No shipped indie football game with live LLM commentary was found. — [U. Twente bachelor report](https://bachelorshowcase-eemcs.apps.utwente.nl/content/thg_m4tV/Final%20Report.pdf); [ably-labs repo listing](https://awesome.ecosyste.ms/projects/github.com%2Fably-labs%2Ffootball-data-live-ai-commentary)

### Inferences
- **Budget for an offline bank.** Assume about 3,000 event lines × about 60 characters × 2 voices, plus about 2,000 names × 3 intonations × about 15 characters. That is roughly 450K characters, which costs about $18–36 on ElevenLabs or about $7 on Azure. It is effectively free with on-device Kokoro, though quality is lower. Cost is therefore not the constraint. Voice quality and consistent prosody across stitched clips are.
- **Live LLM budget.** The ball changes state about every 1–2 s, so live play-by-play needs under ~500 ms from event to audio start. LLM time-to-first-token plus TTS time-to-first-audio over mobile networks usually exceeds that. Restrict LLM lines to dead-ball windows (fouls, set pieces, half-time, replays) and queue them at low priority, or generate them speculatively from predicted events, as the MDPI paper does.
- **Synthetic voices and licensing.** Use vendor stock or library voices under a paid plan whose terms allow commercial game redistribution, or Kokoro's Apache-2.0 voices. Never clone a real commentator: right of publicity applies, and EA's Mowbray use required his permission. Disclose AI-generated content in App Store / Steam metadata, as EA did.
- **Local LLM.** An on-device LLM on iPad is plausible for short templated colour lines. A cloud LLM call per match is cheap but needs connectivity and moderation (avoid defamatory or offensive generated lines).

### Gaps
- ElevenLabs' exact licence terms for redistributing generated TTS audio inside a game were not confirmed from the terms of service in this session.
- No measured on-iPad Kokoro latency or real-time factor from a primary source.
- Azure prices are from third-party trackers, not Microsoft's page.
- No first-party Unity integration was confirmed for Kokoro. It would need CoreML via a native plugin, Unity Sentis/Inference Engine with ONNX, or offline pre-rendering.

## Crowd audio, stadium ambience and middleware (FMOD, Wwise, Unity)

### Takeaway
EA's crowd is layered:
- an intensity-graded murmur bed
- per-stadium loops
- reaction one-shots (ooh/aww/cheer/goal)
- "salt and pepper" callouts and horns
- home and away sections that are separated and panned
- artificially shaped dynamics to avoid fatigue

FMOD and Wwise both have free indie tiers and can drive this from game parameters. Unity's built-in AudioMixer can manage a simpler version.

### Cited Findings
- **EA crowd design.**
  - A murmur "bed" with several intensity levels, plus long loops for each stadium type.
  - Reactions: ooh, aww, cheers, goals.
  - "Salt and pepper" layers of individual callouts, horns and whistles to suggest locations.
  - Impulse responses recorded in real stadiums, and multichannel crowd recordings.
  - Home and away fans separated and panned, each reacting to events.
  - Dynamics artificially shaped with troughs and peaks to avoid fatigue.
  — [Designing Sound / MacPherson](https://designingsound.org/2013/02/spectral-analysis-interview-with-jeff-macpherson/)
- **Crowd reuse in another project.** A sound designer built live stadium crowds from FIFA recordings, using Sound Particles. — [Sound Particles blog](https://blog.soundparticles.com/how-paul-boechler-created-live-stadium-crowds-from-fifa-recordings)
- **Wwise licensing.** The Indie licence covers development budgets under $250K. No licence tier has an asset limit. G2 reports it is free per project per platform. Paid tiers scale up to about $40K. — [Audiokinetic blog](https://blog.audiokinetic.com/en/wwise-licensing-and-pricing-philosophy/); [Audiokinetic pricing](https://www.audiokinetic.com/pricing); [G2](https://g2.com/products/wwise/pricing)
- **FMOD licensing.** Sources conflict:
  - A historical MCV article says it is free for budgets under $500K, with up to $15K above that.
  - A 2026 blog says it is free under $200K, then $5K per project.
  - Verify with Firelight.
  — [MCV](https://mcvuk.com/development-news/audio-middleware-fmod-studio-goes-free-for-indies/); [youngju.dev 2026 overview](https://www.youngju.dev/blog/culture/2026-05-16-game-audio-middleware-2026-wwise-sony-fmod-steam-audio-resonance-tempest-3d-metasounds-deep-dive.en)

### Inferences
- **Implementation in OpenFifa.**
  - A global `CrowdIntensity` parameter (0–1), computed from ball proximity to goal, xG of the developing chance, scoreline, minute and home/away possession.
  - Separate home and away excitement parameters.
  - Event stingers for shot, save, goal, foul, card and near-miss.
  - Chant loops triggered when one team is dominant.
  - Ducking of the crowd under commentary.
- **Middleware choice.** FMOD is the most common choice for Unity indies, and its parameter-driven events map directly onto this design. Unity AudioMixer snapshots plus a custom C# director also work and avoid a licence and native plugin.
- **Assets.** The crowd bed must be original or properly licensed. Recording or buying a stadium sample library avoids any EA asset concerns.

### Gaps
- FMOD's current official 2026 pricing was not verified from fmod.com.
- No GDC talk specifically on modern FC crowd systems was found.

## EA FC 26 and eFootball default controller layouts

### Takeaway
FC 26 has four presets: Classic, Alternate, Two Button and One Button. Classic is the de-facto standard: X/A pass, Square/X lob-cross, Triangle/Y through, Circle/B shoot, R2/RT sprint, R-stick skills, L1/LB switch, D-pad tactics. FC 26 also adds Competitive and Authentic *gameplay* presets, which are tuning, not button maps. eFootball moves shoot to Square/X and lofted pass to Circle/B.

### Cited Findings
- **Presets.** FC 26 has four control presets under Customise Controls: Classic, Alternate, Two Button and One Button. — [FIFPlay via search summary](https://www.fifplay.com/fc-26-controls/); [Red Bull settings guide](https://www.redbull.com/eu-en/ea-fc-26-best-controller-and-gameplay-settings)
- **Sources for the tables below.** The Classic tables (PS5 / Xbox) come from [FIFPlay FC 26 Controls](https://www.fifplay.com/fc-26-controls/).

**Movement**

| Action | PS5 | Xbox |
|---|---|---|
| Move | L | L |
| Sprint | R2 | RT |
| Shield / jockey | L2 | LT |
| Controlled sprint | R1 | RB |
| Strafe dribble | L1 + L | LB + L |
| Stop ball | No direction + R2 | No direction + RT |
| Skill moves | R stick | R stick |
| Knock-on | R2 + R flick | RT + R flick |

**Attacking (simple)**

| Action | PS5 | Xbox |
|---|---|---|
| Ground pass | X | A |
| Lob / cross / header | Square | X |
| Through pass | Triangle | Y |
| Shoot / volley / header | Circle | B |
| Low driven shot | Circle twice | B twice |
| Chip shot | L1 + Circle | LB + B |
| Finesse shot | R1 + Circle | RB + B |
| Power shot | L1 + R1 + Circle | LB + RB + B |
| Driven through pass | L1 + R1 + Triangle | LB + RB + Y |
| Fake shot / fake pass | Shoot or pass button, then X + direction | Shoot or pass button, then A + direction |

**Attacking (advanced)**

| Action | PS5 | Xbox |
|---|---|---|
| Driven ground pass | R1 + X | RB + A |
| Lofted ground pass | X twice | A twice |
| Lofted through pass | Triangle twice | Y twice |
| Lobbed through pass | L1 + Triangle | LB + Y |
| High lob | L1 + Square | LB + X |
| Driven lob / cross | L1 + R1 + Square | LB + RB + X |
| Ground cross | Square twice | X twice |
| Trigger run | L1 | LB |
| Call for support | R1 | RB |
| Pass and go | L1 + X | LB + A |
| Flair pass / shot / lob / through | L2 + matching face button | LT + matching face button |
| Precision through / lob | R1 + Triangle / R1 + Square | RB + Y / RB + X |
| Player lock | L3 + R3 | LS + RS |

**Defending**

| Action | PS5 | Xbox |
|---|---|---|
| Change player | L1 | LB |
| Manual change | R stick + direction | R stick + direction |
| Icon switching | R3 | RS |
| Tackle / push / pull | Circle | B |
| Hard tackle | R1 + Circle | RB + B |
| Professional foul | R1 + X | RB + A |
| Slide tackle | Square | X |
| Hard slide | R1 + Square | RB + X |
| Clearance | Circle | B |
| Technical clearance | R1 + Circle | RB + B |
| Contain / physical tackle | Hold X | Hold A |
| Teammate contain / partial team press | Hold R1 | Hold RB |
| Sprint jockey | L2 + R2 | LT + RT |
| Rush GK | Hold Triangle | Hold Y |

**Tactics (D-pad)**

| Input | Action |
|---|---|
| Up | Quick tactics |
| Up, Up | Offside trap |
| Up, Left | Team press |
| Up, Right | Extra striker |
| Up, Down | Get in the box |
| Right | Tactical focus (Right then Up / Left / Right = default / defending / attacking) |
| Left | My tactics (custom tactics 1–4) |
| Down | Tactical suggestions |
| Down, Down | Substitution |

- **FC 26 gameplay presets.** FC 26 splits gameplay into Competitive (online, responsive) and Authentic (offline, realism). Both can be toggled in Kick-off and Career. It also adds "Dynamic Dribbling" and a goalkeeper AI overhaul with new Be-A-Goalkeeper controls. — [EA Pitch Notes FC 26 Gameplay Deep Dive](https://www.ea.com/games/ea-sports-fc/fc-26/news/pitch-notes-fc26-gameplay-deep-dive); [Gaming Nexus](https://www.gamingnexus.com/News/69844/EA-Sports-FC-26-looks-to-appease-everyone-by-breaking-the-gameplay-into-two-modes)
- **eFootball 2026 controller map (PS / Xbox).** The source article is titled 2027 but its image references 2026. It also says controller support on mobile works only in AI modes. — [GamingOnPhone](https://gamingonphone.com/guides/efootball-2026-controller-controls-and-button-mapping-guide)

**Attacking**

| Action | PS | Xbox |
|---|---|---|
| Low pass | X | A |
| Through pass | Triangle | Y |
| Lofted pass / cross | Circle | B |
| Shoot | Square | X |
| Dash | R2 | RT |
| "Stunning" pass / shot | R2 + pass or shot | RT + pass or shot |
| Shield | L2 | LT |

**Defending**

| Action | PS | Xbox |
|---|---|---|
| Pressure | X | A |
| Slide | Circle | B |
| Shoulder charge | Square | X |
| Match-up | L2 | LT |
| Cursor change | L1 or R stick | LB or R stick |
| Bring out GK | Triangle | Y |

### Inferences
- **Recommended OpenFifa default.** Mirror the FC Classic *functional* layout: pass and lob on the bottom and left face buttons, through ball on top, shoot on the right, sprint on the right trigger, shield/jockey on the left trigger, modifiers on the bumpers, skills on the right stick, tactics on the D-pad. This is the layout players' muscle memory expects.
- **Optional preset.** Offer a second preset with the "eFootball-style" shoot and lob swap.
- **Remapping.** Make everything remappable through Input System action maps. A remapping UI is also an accessibility win.
- **Simplified presets.** Add a "Two Button" style preset for casual iPad players, mirroring FC's accessibility presets.
- **Button labels.** Use generic labels (South/East/West/North) internally, and show platform glyphs at runtime.

### Gaps
- No button-by-button source for FC 26's **Alternate** preset was found. Search results confirm it exists but don't detail it. Do not rely on memory; check in-game or find a dedicated guide.
- No source confirmed FC 26-specific changes to jockey or call-press bindings. The Classic table above is the best available.
- No GameSpot/IGN FC 26 control guide was fetched. FIFPlay is a long-running fan reference, not EA.

## Touch controls on mobile (EA FC Mobile, eFootball mobile, Dream League Soccer) and what works on iPad

### Takeaway
Two families dominate:
- **Virtual stick + contextual buttons:** DLS (stick plus three buttons), FC Mobile's button mode, eFootball's classic mode.
- **Gesture/tap-to-target:** FC Mobile's gesture mode, eFootball's Touch & Flick (formerly "Advanced").

The mainstream titles let players mix the two and add "assist" layers, such as eFootball's Smart Assist for shot power and dribble direction. On iPad, the larger screen allows bigger, resizable, repositionable buttons and gestures on the pitch itself. Gamepad support on iPad is a meaningful differentiator, because both big mobile football games handle it unevenly.

### Cited Findings
- **EA FC Mobile modes.**
  - Players can use gestures, buttons, or mix both. EA says buttons give more control of passing techniques.
  - A "Gestures Only" toggle hides the on-screen buttons.
  - Advanced passing must be enabled separately.
  — [EA gameplay controls guide: gesture controls](https://www.ea.com/ea-studios/ea-sports/news/gameplay-controls-guide-gesture-controls); [EA gameplay guide](https://www.ea.com/news/gameplay-guide); [fifamobileguide](https://www.fifamobileguide.com/gameplay-controls)
- **FC Mobile on iPad with a controller.** Evidence conflicts. An EA forum reply said there are no plans for controller support. Several low-quality sites claim it works. Treat it as unverified. — [EA forums](https://forums.ea.com/discussions/fc-mobile-technical-issues-en/re-can-you-connect-a-controller-to-fc/8051321)
- **eFootball mobile.**
  - The control type previously called "Advanced" was renamed "Touch & Flick".
  - Touch & Flick shooting: tap the left side, then tap the right side and flick toward goal (per a 2022 page).
  - "Smart Assist" automatically controls shot power and adjusts dribble direction depending on the situation. It is toggled in Extras > Game Settings > Play Settings.
  — [Konami eFootball new controls](https://www.konami.com/efootball/en-us/page/new_controls); [Konami 2022 patch notes](https://www.konami.com/efootball/en/page/2022/season2_patch-notes_v1-1-4)
- **eFootball Smart Assist trade-off.** Guides recommend turning Smart Assist off for full manual control. Feints have their own setting (Easy / Smart / Advanced). — [GamingOnPhone settings guide](https://gamingonphone.com/guides/efootball-guide-to-the-best-gameplay-and-game-plan-settings-for-a-smooth-experience/)
- **eFootball mobile with a controller.** Controller play on mobile works only in AI modes. — [GamingOnPhone](https://gamingonphone.com/guides/efootball-2026-controller-controls-and-button-mapping-guide)
- **Dream League Soccer.** The official App Store listing describes a virtual joystick on the left and three buttons on the right for passing, shooting, pressing and tackling. Swipe-on-button variants (long pass, finesse) come only from low-quality blogs. — [App Store story](https://apps.apple.com/ng/story/id1494291945)

### Inferences
- **Recommended iPad scheme for OpenFifa.**
  - A *floating* left joystick: it spawns where the thumb lands in the left 40% of the screen, which avoids reach problems on a 13" iPad.
  - Right cluster: Pass (tap = ground, hold = driven), Shoot (hold to charge power; swipe up = chip, swipe curved = finesse), Through (tap), Lob/Cross (tap).
  - A sprint button, or auto-sprint from how far the stick is pushed.
  - Defensive context swaps labels: Pass→Switch, Shoot→Tackle, Through→Press/Contain, Lob→Slide.
  - Gesture extras: a swipe on the right half for skills; tap a teammate to pass to them (FC Mobile-style).
  - Settings: an assist level (Smart-Assist-like aim and power help), button resize and reposition, left-handed mirror.
- Context-sensitive relabelling of the same four buttons between attack and defence is the common pattern across all three games. It keeps the touch surface small.
- **Gamepad on iPad.** Make it first-class, using the FC Classic layout. Hide the touch overlay automatically when a controller connects. This is a differentiator over eFootball's AI-modes-only controller support.

### Gaps
- No usability study comparing joystick and gesture controls on tablets was found.
- No authoritative current (2026) FC Mobile button-layout source; the fan guides may be dated.

## Gamepad support on iPad in Unity 6 (Input System + Apple GameController)

### Takeaway
Unity's Input System package has iOS-specific device layouts: DualSense, DualShock 4, Xbox One Bluetooth, and a generic iOS gamepad, all backed by Apple's GameController framework. Use the standard `Gamepad` abstraction and action maps. Rumble and lightbar over Bluetooth aren't supported through Unity on iOS.

### Cited Findings
- **iOS device classes.** The `UnityEngine.InputSystem.iOS` namespace includes `DualSenseGampadiOS`, a DualShock 4 class, an Xbox One Bluetooth class and a generic iOS Gamepad class. — [Unity Input System iOS API (1.8)](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.8/api/UnityEngine.InputSystem.iOS.html); [DualSenseGampadiOS (1.4)](https://docs.unity3d.com/ja/Packages/com.unity.inputsystem@1.4/api/UnityEngine.InputSystem.iOS.DualSenseGampadiOS.html)
- **Supported devices on iOS.**
  - Unity supports MFi-certified controllers on iOS.
  - Xbox One and PS4 controllers need iOS 13+.
  - Motor rumble and lightbar colour over Bluetooth are not supported.
  - Older manuals say DualSense is recognised as a generic gamepad on iOS; newer packages add a dedicated class.
  — [Supported Devices (1.19 manual)](https://docs.unity.cn/Packages/com.unity.inputsystem%401.19/manual/SupportedDevices.html); [1.4 manual](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.4/manual/SupportedDevices.html)

### Inferences
- **Setup.**
  - Bind actions to `<Gamepad>/buttonSouth` and similar paths, not to device-specific paths.
  - Use `PlayerInput` control schemes (Touch, Gamepad) with auto-switch.
  - Listen to `InputSystem.onDeviceChange` to show or hide the touch UI.
  - Pull button glyphs from `device` type (DualSense versus Xbox).
- **Haptics.** For rumble on iOS, a native GameController plugin (`GCController.haptics`) would be needed.
- Test on a real iPad with DualSense and Xbox Series controllers; Simulator gamepad support is unreliable.

### Gaps
- Which Input System version ships with Unity 6.x, and whether it changes iOS behaviour (for example adaptive triggers or haptics), was not confirmed.
- No source found on Unity support for iPadOS's newer GameController features such as the virtual controller (`GCVirtualController`).

## Legal: can control layouts and gameplay mechanics be copied?

### Takeaway
In the US, rules, mechanics and functional control mappings are generally unprotectable ideas or methods of operation. Expressive presentation can be protected, and copying it too closely can infringe: the look of pieces, UI art, distinctive visual style. This is shown in *Tetris v. Xio* (2012) and in *Spry Fox v. 6waves* (2012, motion to dismiss denied, then settled). Trademarks ("FIFA", "EA SPORTS FC", "FUT", "PlayStyles", league and club names) and trade dress are separate risks. OpenFifa can use an FC-like button mapping but must use its own UI art, names, terminology and HUD styling.

### Cited Findings
- ***Tetris Holding v. Xio Interactive* (D.N.J. 2012).**
  - The court separated unprotectable rules and ideas from protectable expression.
  - It rejected Xio's broad merger / scènes-à-faire defence, saying it would "swallow any protection a game could have".
  - It found that Mino copied Tetris's expressive look (piece design, board dimensions, visual style).
  - Tetris Holding also brought trade-dress claims.
  — [Wikipedia](https://en.wikipedia.org/wiki/Tetris_Holding,_LLC_v._Xio_Interactive,_Inc.); [PC Gamer](https://www.pcgamer.com/court-declares-tetris-clone-a-breach-of-copyright-big-implications-for-derivative-games/); [Mondaq](https://www.mondaq.com/unitedstates/gaming/591564/when-it-comes-to-games-the-practical-limit-to-united-states-copyright-protection-may-only-be-skin-deep)
- **General principle.** Rules and mechanics that drive gameplay are generally not protected, but a mechanic being unprotectable doesn't make all expression of it unprotectable. — [Mondaq](https://www.mondaq.com/unitedstates/gaming/591564/when-it-comes-to-games-the-practical-limit-to-united-states-copyright-protection-may-only-be-skin-deep)
- ***Spry Fox v. Lolapps/6waves* (W.D. Wash. 2012).**
  - The court denied the motion to dismiss, finding Spry Fox plausibly alleged similar *expression* in Yeti Town.
  - At the same time it held many gameplay and UI elements of Triple Town unprotectable and stressed that Spry Fox has no monopoly on the idea.
  - The parties settled, and Spry Fox took ownership of both games' IP.
  - Sources disagree on the ruling date (Sept 18 vs Oct 29, 2012).
  — [Eric Goldman blog](https://blog.ericgoldman.org/?p=10653); [Wikipedia](https://en.wikipedia.org/wiki/Spry_Fox,_LLC_v._Lolapps,_Inc.); [Game Developer](https://gamedeveloper.com/business/there-is-substantial-similarity-between-i-triple-town-i-and-i-yeti-town-i---u-s-court); [PocketGamer settlement](https://www.pocketgamer.com/news/6waves-settles-with-spry-fox-in-yeti-town-triple-town-clone-debate/)
- **Scholarly view.** Prof. Greg Lastowka read the Spry Fox ruling as nudging copyright "a little bit further toward protecting game mechanics". — [Eric Goldman blog](https://blog.ericgoldman.org/?p=10653)
- **Older precedent.** *Data East v. Epyx* (1988) was cited by 6waves for the point that basic gameplay is not protected. — [Goldman blog via search summary](https://blog.ericgoldman.org/?p=10653)

### Inferences
- **Safe to copy.** Assigning pass, shoot, through and lob to face buttons, sprint to a trigger, skills to the right stick and tactics to the D-pad. These are functional control conventions, shared by FC and eFootball themselves.
- **Avoid copying.** EA's HUD layout and art, button-prompt styling, menu trade dress, radar design, and trademarked terms ("FUT", "PlayStyles", "Ultimate Team", "Dynamic Dribbling", "EA SPORTS FC", "FIFA").
- **Real names and likeness.** Real player, club, league and commentator names or likenesses are a separate rights issue (publicity, trademarks, FIFPRO / league licences). Use fictional rosters, and never clone a real commentator's voice.
- Names like "OpenFifa" contain the FIFA trademark. A public release should be renamed to reduce trademark risk; get trademark counsel before launch.
- These are US cases. EU and UK law is similar on ideas versus expression, but no EU-specific source was gathered here.

### Gaps
- No legal source specifically on copying *controller layouts* was found; the conclusion is inferred from the general mechanics doctrine.
- How the Tetris trade-dress claim was resolved was not confirmed in the fetched sources.
- No EU/UK case law was reviewed.
- This is not legal advice; counsel review is recommended before commercial release.
