# Player, Team and Ratings Data Sources and Licensing Risk for OpenFifa (Premier League 2026/27)

Scope: where to get PL 2026/27 rosters and attributes, what each source allows, and how indie games handle real names. Researched 2026-11-04. This is not legal advice. Several primary terms pages (sofifa, Transfermarkt, API-Football, Premier League licensing) could not be retrieved directly and are flagged as gaps.

## 1. sofifa.com and EA FC ratings: what it has, terms, copyright/database rights, takedowns

### Takeaway
Sofifa mirrors EA's FC player ratings (EA itself publishes 17,000+ player ratings on ea.com). sofifa's own terms could not be retrieved, but sofifa sits behind Cloudflare rate limiting, and dataset authors themselves warn that commercial use may breach the source's terms. Under EU/UK law, individual facts and scores are probably not copyright works, but EA's ratings database is a strong candidate for the EU/UK sui generis database right (substantial investment). Shipping EA-derived ratings in a public game (even OSS) is the highest-risk data choice available.

### Cited Findings
- EA runs its own official ratings database that lists ratings and playstyles for 17,000+ players, e.g. per-player pages at ea.com/.../ratings/player-ratings/... — [EA ratings page](https://ea.com/en/games/ea-sports-fc/ratings/player-ratings/jacopo-petriccione/225381)
- Open-source sofifa scrapers add a 300 ms delay per request to avoid Cloudflare rate limiting on sofifa.com, which suggests the site actively throttles automated access — [prashantghimire/sofifa-web-scraper](https://github.com/prashantghimire/sofifa-web-scraper)
- The soccerdata library (which scrapes sofifa among others) tells users to comply with the terms of service of the sites they scrape — [soccerdata on PyPI](https://pypi.org/project/soccerdata/)
- A sofifa-derived API project states its data is for educational and non-commercial use only, and that commercial use may violate the source site's terms — [uesleibros/crset](https://github.com/uesleibros/crset)
- Kaggle "complete player dataset" series (stefanoleone992, FIFA 15 to EA FC 24) was scraped from sofifa.com; a DagsHub mirror of the FC 24 edition labels it "CC0: Public Domain", but that label is the uploader's assertion and cannot launder EA's underlying rights. A FIFA 23 mirror on Hugging Face claims sofifa's robots.txt imposed no limitation at scrape time, which is not a licence — [Baselight: stefanoleone992 FC 24](https://baselight.app/u/kaggle/dataset/stefanoleone992_ea_sports_fc_24_complete_player_dataset); [DagsHub EA_SPORTS mirror](https://dagshub.com/DagsHub/open-source-ml-datasets/src/main/EA_SPORTS); [HF jsulz/FIFA23](https://huggingface.co/datasets/jsulz/FIFA23/blob/main/README.md)
- EU law: Football Dataco v Yahoo (CJEU C-604/10, 1 Mar 2012) held that fixture lists are not protected by database copyright: compiling them took significant work and skill but lacked the creativity ("own intellectual creation") required; "skill and labour" alone no longer suffices — [SCL](https://www.scl.org/2407-football-dataco-no-database-copyright-protection-for-fixture-lists/); [Kluwer Copyright Blog](https://legalblogs.wolterskluwer.com/copyright-blog/football-dataco-skill-and-labour-is-dead/); [IPPT judgment PDF](https://www.ippt.eu/sites/default/files/2012/IPPT20120301_ECJ_Football_Dataco_v_Yahoo.pdf)
- Earlier CJEU cases (Fixtures Marketing C-46/02, 2004) held the sui generis right did not protect fixture lists, because the investment was in creating the data, not in obtaining/verifying it — [CMS](https://cms.law/en/gbr/legal-updates/football-fixture-lists-has-the-final-whistle-been-blown-for-database-copyright)
- Football Dataco v Sportradar (CJEU C-173/11, Oct 2012): copying match data from a database protected by the sui generis right and sending it to users in another member state counts as "re-utilisation"; the claim is assessed where the public is targeted (here English matches, English betting firms, English language). Separately, the English Court of Appeal rejected the copyright claim because what was copied was "mere data" — [SCL](https://www.scl.org/2595-database-right-ecj-judgment-in-football-data-co-v-sportradar/); [NatLawReview](https://natlawreview.com/article/football-dataco-ltd-v-sportradar-gmbh-copyright-and-database-right-infringement-and); [EWHC 2012/1185](https://caselaw.nationalarchives.gov.uk/ewhc/ch/2012/1185)

### Inferences
- EA ratings are produced by EA's data-reviewer network and staff (investment in obtaining and verifying data as well as creating it), so unlike fixture lists, they are a much stronger candidate for sui generis protection in the EU/UK. Extracting the whole PL subset (a "substantial part") would likely be extraction/re-utilisation. A single player's OVR quoted in a review is not the risk; a bundled 500-player attribute table is.
- Individual numeric ratings are probably not copyright works on their own, but the selection/arrangement of 30+ named attributes, plus EA trademark associations ("PAC/SHO/PAS" card layout, OVR presentation), adds trade dress and trademark exposure if mimicked.
- Even if no IP right attaches, scraping sofifa breaches its (unverified) terms, which is a contract issue, and App Store 5.2.2 requires permission for content taken from third-party services.
- "CC0" labels on Kaggle FIFA datasets are not reliable provenance: the uploader did not own the data.

### Gaps
- sofifa.com's terms of use page could not be retrieved; its exact anti-scraping/commercial clauses are unverified.
- No public record found of EA or sofifa issuing DMCA/takedowns against GitHub ratings datasets or indie games. Absence of evidence, not evidence of tolerance.
- No source found confirming whether EA FC 27 ratings exist on sofifa as of Nov 2026 (EA FC 26 launched Sept 2025; FC 27 would be expected ~Sept 2026), unverified.
- No US case directly on video-game ratings; US has no database right (Feist), so US risk is mainly contract/ToS and trademark.

## 2. Rights involved in real players and clubs (FIFPRO, Premier League, personality rights, case studies, App Store 5.2)

### Takeaway
Three separate rights stacks apply: (a) player name/image/likeness (collective via FIFPRO, plus individual rights and club-held rights, e.g. Zlatan's via AC Milan), (b) club and league IP (crests, kits, names, PL marks, licensed per club/league), and (c) platform rules (Apple 5.2.1 requires that you own or license the IP). Even well-funded publishers (Konami, Sports Interactive) ship fake names when a licence is missing. Apple's guideline is explicit: no third-party trademarks without permission, and the submitter should own or license the IP.

### Cited Findings
**FIFPRO collective licensing**
- FIFPRO Commercial Enterprises B.V. runs collective licensing of players' NIL (name, image, likeness) and personal data for video games, apps and campaigns, covering "tens of thousands" of men's and women's players; developers apply through a "Game Developers" contact form. No indie programme is advertised and licensees are not listed — [FIFPRO Commercial](https://www.fifpro.org/who-we-are/commercial)
- FIFPRO says it acquires image rights via player unions in nearly 60 countries and makes them available to EA and other video game clients — [Push Square](https://www.pushsquare.com/news/2020/11/fifa_21_rights_row_rumbles_on_as_fifpro_agents_weigh_in); [The Week](https://theweek.com/gaming/108772/zlatan-vs-ea-sports-image-rights-fifa-video-game)
- The FIFPRO licence sits alongside rights from clubs, leagues and the players themselves; individual rights for solo endorsements stay with the player — [Freshfields](https://technologyquotient.freshfields.com/post/102gpgj/foul-play-ibrahimovic-and-the-role-of-digital-likeness-in-sports-video-games); [Lewis Silkin](https://sportsinsights.lewissilkin.com/post/102gl2f/image-rights-in-the-spotlight-again-as-zlatan-fires-shot-at-ea-sports-and-fifpro)
- Indie precedent: Jon Hare's Sociable Soccer secured a FIFPRO licence, with Hare saying real player names are "vital" — [GamesIndustry.biz (mirror)](https://dcmpx.remotevs.com/biz/gamesindustry/www/SL/sociable-soccers-jon-hare-says-real-player-names-are-vital-as-studio-secures-fifpro-licence)

**Zlatan / FIFPRO dispute (Nov 2020)**
- On 23 Nov 2020 Ibrahimović tweeted that he never allowed FIFA or FIFPro to profit from his image; Gareth Bale and agent Mino Raiola joined in, and reports said hundreds of players considered legal action — [GamesRadar](https://www.gamesradar.com/zlatan-ibrahimovic-claims-fifa-21-is-using-his-likeness-without-any-agreement-ea-disagrees); [VGC](https://www.videogameschronicle.com/news/hundreds-of-fifa-21-stars-considering-legal-action-over-the-use-of-their-likenesses)
- EA replied that it held his rights through his club AC Milan, and that the row was a dispute between third parties — [Dexerto](https://www.dexerto.com/fifa/ea-sports-responds-to-zlatan-ibrahimovics-fifa-21-licensing-threats-1469512/); [90min (IT)](https://www.90min.com/it/posts/ea-sports-risponde-a-ibrahimovic-rispettiamo-gli-accordi-e-spunta-un-contratto-con-il-milan)
- Commentary notes the dispute showed how fragmented image rights are (union, club, individual) — [Freeths](https://www.freeths.co.uk/insights-events/legal-articles/2021/zlatan-fires-shots-at-ea-games-and-fifapro-drawing-attention-to-the-world-of-image-rights-once-more/)

**PES / eFootball unlicensed teams**
- PES 2020: only Arsenal and Manchester United were licensed in the Premier League; others got fake names, e.g. Chelsea as "Chelsea B"/"London FC", Man City "Manchester B", Spurs "Tottenham WB", West Ham "West Ham RB"/"East London", Watford "Watford BY" — [GamesAtlas PES 2020 list](https://gamesatlas.com/pes-2020/features/pes-2020-all-the-real-names-of-the-unlicensed-teams-and-leagues-of-efootball-pes-2020); [GamesAtlas Chelsea](https://www.gamesatlas.com/pes-2020/teams/chelsea); [Footy Headlines](https://www.footyheadlines.com/2019/09/released-this-week-fake-heres-how-best-10-teams-appear-in-pes-2020.html)
- Konami designed fake crests for PL clubs — [NSS Sports](https://www.nss-sports.com/en/lifestyle/29653/logos-fake-efootball-pes-premier-league); [Footy Headlines eFootball 2022](https://www.footyheadlines.com/2021/10/how-all-fake-premier-league-teams-look.html)
- Note: real player names were generally kept in PES (via FIFPRO); the fake-out was club/league IP. "Man Red" could not be confirmed as an official PES name in these sources (it is commonly used by fans; older PES used "Man Utd"-style variants), and sources disagree on whether Tottenham was licensed in eFootball 2022.

**Football Manager**
- FM26 contains fake names for a number of competitions, clubs and awards due to licensing; the community "real names fix" (FM Scout, not endorsed by SI/SEGA) deletes `fake.edt`, some `.lnc`/`.dbc` files to restore real names; examples reported: Inter as "Bre-Neu", Atalanta as "Bergamo" — [FM Scout FM26 fix](https://www.fmscout.com/a-fm26-real-names-license-fix.html)
- Germany and Japan national teams have fake players in FM for years (FM18 to FM26) due to licensing; files like `japan_fake.dbc` and `japan_unlicensed_random_names.dbc` — [FM Scout FM26 unlock](https://www.fmscout.com/a-fm26-unlock-germany-japan-national-teams.html); [FM Scout FM23](https://www.fmscout.com/a-fm23-unlock-germany-japan-national-teams.html)
- FM22 called Manchester United "Man UFC" for licensing reasons — [Goal](https://www.goal.com/en/news/why-manchester-united-called-man-ufc-football-manager-2022/1v2ua2jzxcgl51pr9l8p5gt1li)

**New Star Games (Retro Goal / Retro Bowl)**
- Retro Goal lacks official licensing for real-world clubs — [Wikipedia: Retro Goal](https://en.wikipedia.org/wiki/Retro_Goal)
- Retro Bowl reached tens of millions of players with no NFL licence and no real player names; a separately licensed "NFL Retro Bowl '25" launched in 2024 — [Awful Announcing](https://awfulannouncing.com/gaming/while-ea-fights-over-nfl-licenses-retro-bowl-is-winning-football-gaming-without-one.html)

**Apple App Store**
- 5.2 Intellectual Property: "Make sure your app only includes content that you created or that you have a license to use. Your app may be removed if you've stepped over the line..." — [Apple Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)
- 5.2.1: "Don't use protected third-party material such as trademarks, copyrighted works, or patented ideas in your app without permission... Apps should be submitted by the person or legal entity that owns or has licensed the intellectual property and other relevant rights." — [Apple Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)
- 5.2.2: if the app "uses, accesses, monetizes access to, or displays content from a third-party service", you must be permitted under that service's terms; "Authorization must be provided upon request." — [Apple Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)
- 2.3.9: you are responsible for rights to all material in icons, screenshots and previews, and should show fictional rather than real-person data — [Apple Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)
- 4.1(c): you cannot use another developer's icon, brand or product name in your app's icon or name without approval — [Apple Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)
- Reviewers commonly reject under 5.2.1 and ask for documentary evidence of rights in App Review Information; one dev reported documentation alone did not resolve it — [Apple Dev Forums 826067](https://developer.apple.com/forums/thread/826067); [Apple Dev Forums 114606](https://developer.apple.com/forums/thread/114606)

### Inferences
- For an App Store build, real PL club names + crests + kits are the most likely rejection/takedown trigger (trademarks are registered, and enforcement is routine). Real player names are a lesser but real risk (UK has no general personality right but passing off/false endorsement applies, as in Irvine v Talksport and Fenty v Topshop; EU states like Germany/Italy/France recognise personality rights). These UK cases are from general knowledge, not retrieved here; verify.
- TestFlight external testing also goes through Beta App Review, so 5.2.1 can bite before App Store launch.
- Open-source on GitHub does not reduce trademark/personality exposure; GitHub honours DMCA (copyright) and trademark complaints.
- A FIFPRO licence is the only realistic route to real names; it is a commercial negotiation. Sociable Soccer shows an indie can get one, but costs are unknown.

### Gaps
- Premier League licensing pages (club crests, league marks, "interactive games" licensing programme) were not retrieved; licensee list and terms for 2026/27 are unknown. EA's PL licence status for EA FC 27 not confirmed here.
- FIFPRO licence fees, minimums and indie terms are unpublished.
- No sources retrieved on the "Hero" football game licensing; not covered.
- No record of what Apple did with specific football apps using real names without licence.

## 3. Open or permissive data sources

### Takeaway
Truly clean sources are few: Wikidata (CC0 structured data) and openfootball (public-domain dedication) for squads/fixtures; StatsBomb open data is free but non-commercial under a user agreement and covers limited competitions; football-data.org has a free tier with mandatory attribution. FBref (now without Opta data since ~Jan 2026), Transfermarkt and sofifa forbid or restrict scraping and derivative tools. Kaggle FIFA datasets have no real provenance.

### Cited Findings
| Source | What it has | Terms / licence |
|---|---|---|
| Wikidata | Player items, clubs, DOB, nationality, position, membership (P54) | Structured data under CC0; non-main namespaces CC BY-SA 4.0 — [Wikidata:Licensing](https://www.wikidata.org/wiki/Wikidata:Licensing). Critics note CC0 does not change the rights in upstream sources and EU database right may still matter — [OSM-talk 2017](https://lists.openstreetmap.org/pipermail/talk/2017-November/079492.html) |
| Wikipedia squad lists | Current squad tables | Text CC BY-SA (attribution + share-alike); not confirmed whether a squad list is a protectable compilation (gap) |
| openfootball / football.json | Fixtures, results, clubs (incl. England) | "schema, data and scripts are dedicated to the public domain"; england/worldcup repos tagged cc0-1.0 — [football.json](https://github.com/openfootball/football.json); [openfootball/england](https://www.github.com/openfootball/england); [ecosyste.ms](https://awesome.ecosyste.ms/projects/github.com%2Fopenfootball%2Fengland) |
| StatsBomb Open Data | Event data for selected competitions/seasons (not a full current PL) | Free for public non-commercial use, governed by a User Agreement (LICENSE.pdf in repo); attribution with StatsBomb logo requested — [statsbombpy](https://pypi.org/project/statsbombpy); [repo fork README](https://github.com/eddwebster/open-data) |
| football-data.org | Competitions, fixtures, standings, (squads disputed on free tier) | Free tier must show "Football data provided by the Football-Data.org API"; terms changeable at any time; ~10 req/min free — [Terms](https://api.football-data.org/register); [thestatsapi blog](https://www.thestatsapi.com/blog/football-data-org-free-tier-limits-2026). Sources conflict on whether squads are free |
| FBref / Sports Reference | Season stats | "you should not create websites or tools based on data you scrape from Sports Reference ... or use our data to train generative AI models without our permission"; excessive bots get blocked — [SR Data Use](https://www.sports-reference.com/data_use.html). Opta terminated its feed to SR and required deletion of Opta data (~Jan 2026) — [Awful Announcing](https://awfulannouncing.com/soccer/sports-reference-pulls-advanced-data-agreement-violation-dispute.html) |
| Transfermarkt | Squads, market values, contracts | Terms reportedly bar access/copy "using bots, spiders, screen scraping or other automated processes" and reserve TDM; secondary source only — [soccerdata FAQ](https://soccerdata.readthedocs.io/en/stable/_sources/faq.rst.txt); [R-pkg-devel 2018](https://stat.ethz.ch/pipermail/r-package-devel/2018q1/002405.html) |
| Kaggle FIFA/FC datasets | Full EA attributes | Scraped from sofifa; licence labels (CC0/MIT) are uploader assertions — see section 1 |
| API-Football | Squads, stats | Not researched (gap) |

### Inferences
- Squad membership facts (player X plays for club Y, position, nationality, age) are low-risk facts; Wikidata CC0 is the cleanest bulk source and can be supplemented by user-maintained edits. Pulling the entire Transfermarkt or FBref PL dataset is a ToS breach and, in the EU, possibly database-right extraction.
- StatsBomb open data is fine for building/calibrating a ratings model offline; shipping derived numbers in a free OSS game is probably within "non-commercial", but an App Store paid app or IAP would not be. Read LICENSE.pdf.
- football-data.org is usable for fixtures/standings in-app with the attribution string.

### Gaps
- API-Football terms, sofifa terms and Transfermarkt's terms page were not fetched directly.
- StatsBomb LICENSE.pdf text not read directly; which 2025/26 or 2026/27 PL data (if any) is open is unknown.
- Whether football-data.org free tier includes PL squads in 2026 is contested.

## 4. Deriving original ratings from open stats

### Takeaway
No source retrieved documents a specific published method, so this section is mostly design inference. The legally safer pattern is: compute attributes from raw factual performance data (minutes, goals, xG, passes, tackles, sprint/age proxies) with your own formulas, so ratings are your own creative output and not a copy of EA's database.

### Cited Findings
- Facts and "mere data" are not copyright-protected in the UK/EU; the risk arises from extracting a substantial part of a protected database — [NatLawReview on Dataco v Sportradar](https://natlawreview.com/article/football-dataco-ltd-v-sportradar-gmbh-copyright-and-database-right-infringement-and); [SCL Dataco v Yahoo](https://www.scl.org/2407-football-dataco-no-database-copyright-protection-for-fixture-lists/)
- StatsBomb open data provides event-level data (passes, shots, pressures with locations) for selected competitions, free for non-commercial use — [statsbombpy](https://pypi.org/project/statsbombpy)

### Inferences
- Suggested pipeline: (1) squad facts from Wikidata (CC0); (2) per-90 rates from a licensed or open stats source (StatsBomb open for calibration; football-data.org for basic counts); (3) map to attributes via percentile rank within position (e.g. Finishing = f(non-penalty goals minus xG, shot conversion), Passing = f(completion %, progressive passes), Defending = f(tackles+interceptions per 90 adjusted for possession), Pace = proxy from age, position and dribble/carry data since tracking speed data is rarely open); (4) blend with a hand-tuned "reputation" prior that your team or community edits; (5) never use EA/sofifa numbers as a training target or a sanity check committed to the repo, because fitting your model to EA's outputs could reproduce a substantial part of their database.
- Keep a provenance log (source + licence per field) in the repo to answer Apple 5.2.2 "authorization upon request" and any takedown.
- Use your own attribute names/scale (e.g. 1 to 20 like FM, or different groupings) to avoid EA trade dress.

### Gaps
- No academic or industry source on attribute-from-stats methods was retrieved in this pass; worth a dedicated search (e.g. "player rating model from event data", VAEP, xT).

## 5. Community edit/option file patterns and recommended safe approach

### Takeaway
The industry-standard workaround is shipping fictional/neutral data and letting users import real names via community files: PES option files, Football Manager real-name fixes. These are third-party, unaffiliated, and the publisher does not distribute them. For OpenFifa, ship clean fictional or fact-only data and provide an import format, but do not host or link to real-IP packs from the official repo or app.

### Cited Findings
- PES option files are community-made downloads that overwrite fake names, crests and kits for unlicensed teams; guides describe them as third-party files with no affiliation to Konami, imported via Edit mode (PS4 via USB, PC via file copy) — [GamesRadar PES 2021 option file](https://gamesradar.com/pes-2021-option-file); [GamesAtlas PES 2020 edit tutorial](https://gamesatlas.com/pes-2020/features/pes-2020-edit-mode-tutorial-how-to-import-option-files-teams-and-uniforms); [PC Gamer](https://www.pcgamer.com/uk/pes-2020-option-file-pc-download)
- FM's real-names fix is a fan add-on "not endorsed by Sports Interactive or SEGA" — [FM Scout](https://www.fmscout.com/a-fm26-real-names-license-fix.html)
- Retro Goal does not let users edit team or player names even in the paid version, i.e. no import path at all — [Wikipedia: Retro Goal](https://en.wikipedia.org/wiki/Retro_Goal) (user-review claim from search snippet; treat as low confidence)

### Inferences
Recommended approach (most to least safe):
1. **Shipped build (GitHub + App Store):** fictional league/club names, original crests/kits, fictional players OR real names omitted; ratings computed by OpenFifa's own model. Zero PL, club or EA marks in app name, icon, screenshots or metadata (Apple 2.3.9, 4.1, 5.2.1).
2. **Editor + documented open schema (JSON/CSV)** for teams, players, attributes, kits; local import from the Files app on iPad. This mirrors PES Edit mode. Apple allows user-generated/imported content, but avoid in-app browsing/downloading of real-IP packs, which could make the app look like a distributor (risk of 5.2 rejection).
3. **Do not host** real PL/EA data packs in the official repo, releases or a linked "official" community site; a separate community may do so at its own risk (precedent: FM Scout, PES option-file communities). Note that knowingly facilitating could still attract contributory claims; keep a clear separation and DMCA contact.
4. **Optional middle tier:** real player names with fictional clubs only after obtaining a FIFPRO licence (Sociable Soccer precedent). Real names without licence: moderate risk in the EU (personality rights) and UK (passing off/false endorsement), and App Store 5.2.1 exposure.
5. **Never:** bundling sofifa/Kaggle EA attribute tables, EA FC card visuals, PL crests/kits/lion logo, or real club names in a store build.

### Gaps
- No documented legal case found holding a game publisher liable for user-imported option files, and no case clearing them either; the legal status is untested in these sources.
- Apple's stance on games whose import feature is primarily used for unlicensed real data is undocumented.
