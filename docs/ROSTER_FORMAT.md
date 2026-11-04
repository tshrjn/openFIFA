# OpenFifa roster format (schema v1)

OpenFifa reads leagues, clubs and players from one JSON file per league. The game ships one fictional league.
Players can add their own leagues as local files. This page is the spec. The code that enforces it is in
`OpenFifa/Assets/Scripts/Core/Rosters/` (`RosterSchema`, `RosterValidator`, `RosterLoader`, `OverallCalculator`).

## Data policy

- **The bundled roster is fully fictional.** The league, every club, ground and player name was invented by
  `tools/rosters/generate_fictional_league.py`, and the attributes are on OpenFifa's own scale. The game does not
  ship any real league, club, crest, player name or third-party rating, including EA FC or sofifa data.
- **User rosters stay on the user's device.** `LocalFileRosterSource` reads `*.roster.json` files the user puts in
  the app's Documents folder (on iPad: Files app > On My iPad > OpenFifa). This works like PES option files.
  OpenFifa does not download, host or link to roster packs. Do not commit rosters that hold real-world data.
- There is no scraper in this repo, and none should be added. See
  `docs/research/research_notes/Football game fidelity research/player_data_licensing.md` for the reasons.

## Sources

| Source | Kind | Where |
| --- | --- | --- |
| `BundledRosterSource` | `Bundled` | `StreamingAssets/Rosters/default_league.json`. Gameplay passes `Application.streamingAssetsPath`, or a text reader where StreamingAssets is not a folder. |
| `LocalFileRosterSource` | `LocalFile` | `<Documents>/*.roster.json`, listed by `Discover(dir)` and sorted by file name. 8 MB limit. |

Both sources implement `IRosterSource.Load()`, which returns a `RosterLoadResult` holding a `RosterLeague` (or null)
and every error and warning. Loading never throws on bad data. `RosterLoadResult.Describe()` gives a readable
report, for example:

```
Could not load my_league.roster.json (2 errors, 1 warnings)
error: clubs[3].players[12].attributes.finishing: must be from 1 to 20 (got 25) [line 412]
error: clubs[3].formation: must be an outfield shape whose lines add up to 10, like "4-3-3" (got "4-4-3") [line 380]
warning: clubs[0].players[2].attributes.sprintSpeed: unknown attribute (ignored); check the spelling against docs/ROSTER_FORMAT.md [line 31]
```

## File layout

```json
{
  "format": "openfootball-roster",
  "schemaVersion": 1,
  "league": {"id": "opd", "name": "OpenFootball Premier Division", "shortName": "OPD", "country": "Albion (fictional)", "season": "2026/27"},
  "provenance": {"source": "fictional-generated", "fictional": true, "generator": "...", "seed": 20261104, "notes": "..."},
  "clubs": [
    {
      "id": "kingsmere", "name": "Kingsmere United", "shortName": "KMU",
      "colours": {"primary": "#B3122E", "secondary": "#FFFFFF", "away": "#1C1C24"},
      "stadium": {"name": "The Kingsmere Bowl", "capacity": 61200},
      "formation": "4-3-3", "reputation": 5,
      "players": [
        {"id": "kingsmere-01", "firstName": "Reece", "lastName": "Sabeic", "knownAs": "Sabeic", "shirtNumber": 1,
         "nationality": "England", "age": 30, "heightCm": 200, "positions": ["GK"], "preferredFoot": "right",
         "weakFoot": 3, "skillMoves": 1, "attributes": {"acceleration": 8, "...": "all 34 keys"}}
      ]
    }
  ]
}
```

The parser is strict JSON: no comments, no trailing commas, no duplicate keys. Unknown fields only produce a
warning, so newer files still load in older builds.

### Root

| Field | Type | Rule |
| --- | --- | --- |
| `format` | string | must be `"openfootball-roster"`. The legacy tag `"openfifa-roster"` is still accepted on load. |
| `schemaVersion` | int | `1`. Older versions are migrated. Newer versions are rejected with an "update OpenFifa" message. |
| `league.id` | id | lower-case letters, digits, `-`, `_`, `.`; up to 48 characters |
| `league.name` / `shortName` | string | up to 60 / 8 characters |
| `league.country`, `league.season` | string | optional |
| `provenance.source` | string | required, e.g. `fictional-generated` or `user-import` |
| `provenance.fictional` | bool | optional. `true` only when every name is invented. |
| `provenance.notes`, `generator`, `seed`, `license` | | optional, free-form |
| `clubs` | array | 2 to 64 clubs |

### Club

| Field | Type | Rule |
| --- | --- | --- |
| `id` | id | unique in the file |
| `name` | string | up to 40 characters |
| `shortName` | string | exactly 3 upper-case letters or digits, unique |
| `colours.primary` / `secondary` / `away` | string | `#RRGGBB` (home shirt, shorts/trim, away/clash) |
| `stadium.name`, `stadium.capacity` | string, int | capacity 1,000 to 150,000 |
| `formation` | string | outfield lines that add up to 10: `4-3-3`, `4-2-3-1`, `3-5-2`... |
| `reputation` | int | 1 to 5. Drives generation, AI difficulty and commentary tone. |
| `players` | array | 11 to 40 players. At least one player must have `GK` as their first position. Shirt numbers must be unique. |

### Player

| Field | Type | Rule |
| --- | --- | --- |
| `id` | id | unique across the whole file |
| `firstName` | string | may be empty |
| `lastName` | string | required |
| `knownAs` | string | optional. Shown on the HUD and spoken by commentary. Defaults to `lastName`. |
| `shirtNumber` | int | 1 to 99 |
| `nationality` | string | required, free text |
| `age` | int | 15 to 45 |
| `heightCm` | int | 150 to 215 |
| `positions` | array | 1 to 4 codes, no repeats. The first one is the primary position. Codes: `GK RB CB LB RWB LWB CDM CM CAM RM LM RW LW CF ST`. |
| `preferredFoot` | string | `"left"` or `"right"` |
| `weakFoot`, `skillMoves` | int | 1 to 5 stars |
| `attributes` | object | all 34 keys below, each a whole number from 1 to 20 |

## Attributes (1-20)

OpenFifa uses its own 1-20 scale (FM-style). It deliberately differs from the 0-99 scale of other games, and no
value is derived from or checked against third-party ratings.

| Group | Keys |
| --- | --- |
| Pace | `acceleration`, `topSpeed` |
| Shooting | `finishing`, `shotPower`, `longShots`, `offTheBall`, `volleys`, `penalties` |
| Passing | `shortPassing`, `longPassing`, `vision`, `crossing`, `curl`, `setPieces` |
| Dribbling | `dribbling`, `firstTouch`, `agility`, `balance`, `composure` |
| Defending | `marking`, `standingTackle`, `slidingTackle`, `interceptions`, `heading` |
| Physical | `stamina`, `strength`, `jumping`, `aggression`, `reactions` |
| Goalkeeping | `gkDiving`, `gkHandling`, `gkDistribution`, `gkReflexes`, `gkPositioning` |

A group summary (`GroupTenths`) is the plain mean of the group, in tenths.

## Overall rating

The overall is computed, never stored. Each position family has integer weights that add up to 100:

```
overallTenths = round_half_up( sum(weight * attribute) / 10 )     // 10..200, shown as 1.0..20.0
```

Everything is integer maths, so the result is the same on every platform. A player's `OverallTenths` uses their
primary position. `OverallTenthsAt(pos)` rates them anywhere else.

| Family (positions) | Weights |
| --- | --- |
| GK | gkDiving 22, gkReflexes 22, gkPositioning 20, gkHandling 16, gkDistribution 8, reactions 8, composure 4 |
| CB | marking 16, standingTackle 16, interceptions 14, heading 12, strength 10, slidingTackle 8, jumping 6, reactions 6, composure 4, shortPassing 4, topSpeed 4 |
| Full-back (RB, LB, RWB, LWB) | standingTackle 12, topSpeed 10, stamina 10, crossing 10, interceptions 10, acceleration 8, slidingTackle 8, marking 8, shortPassing 8, dribbling 6, reactions 6, agility 4 |
| CDM | interceptions 14, shortPassing 14, standingTackle 12, longPassing 10, marking 8, strength 8, stamina 8, composure 8, reactions 8, vision 6, aggression 4 |
| CM | shortPassing 16, longPassing 12, vision 12, firstTouch 10, dribbling 8, stamina 8, composure 8, reactions 8, interceptions 6, standingTackle 4, longShots 4, offTheBall 4 |
| CAM | vision 14, shortPassing 14, firstTouch 12, dribbling 12, composure 8, reactions 8, longShots 8, finishing 8, agility 6, offTheBall 6, acceleration 4 |
| Wide mid (RM, LM) | crossing 14, dribbling 12, acceleration 10, topSpeed 10, shortPassing 10, stamina 10, firstTouch 8, vision 6, agility 6, reactions 6, curl 4, offTheBall 4 |
| Winger (RW, LW) | dribbling 16, acceleration 12, topSpeed 10, firstTouch 10, finishing 10, agility 8, crossing 8, offTheBall 8, shortPassing 6, reactions 6, composure 6 |
| CF | finishing 14, firstTouch 12, offTheBall 10, dribbling 10, shortPassing 10, reactions 10, vision 8, composure 8, longShots 6, acceleration 6, shotPower 6 |
| ST | finishing 20, offTheBall 12, reactions 10, heading 8, shotPower 8, composure 8, firstTouch 8, acceleration 8, topSpeed 6, strength 6, dribbling 6 |

If you change a weight, update `OverallCalculator.cs`, this table and `OverallCalculatorTests` together.

## Versioning and migration

`RosterSchema.CurrentVersion` is 1. To make a breaking change:

1. Bump `CurrentVersion` and update `RosterValidator`, `RosterLoader` and this page.
2. Add an `IRosterMigration` with `FromVersion = old`. It edits the parsed `JsonNode` tree in place.
3. Register it in `RosterMigrator.CreateDefault()`. The loader then runs every step from the file's version up to
   the current one, rewriting `schemaVersion` after each step. If a step is missing, loading fails with a clear
   message. `RosterValidatorTests.Load_OldVersionWithRegisteredMigration_Upgrades` shows the pattern.

Additive, optional fields don't need a version bump. Older builds only warn about them.

## Tools (Python 3, stdlib only)

| Command | What it does |
| --- | --- |
| `python3 tools/rosters/generate_fictional_league.py` | Regenerates `default_league.json`. Deterministic: seed 20261104 always gives byte-identical output. 20 clubs × 25 players, quality driven by club reputation, position profiles and age. |
| `python3 tools/rosters/import_template/import_csv.py players.csv --clubs clubs.csv --scale 99 --out my.roster.json` | Converts a user's own CSV into the roster format. |
| `python3 tools/rosters/import_template/import_csv.py --to-csv league.json --out players.csv --clubs-out clubs.csv` | Converts a roster back to CSV, on the 1-20 scale. |
| `python3 -m unittest discover -s tools/rosters -p 'test_*.py'` | Tool tests: determinism, CSV ↔ JSON round trip, alias mapping, errors. |

### CSV columns

`players_template.csv` and `clubs_template.csv` sit in `tools/rosters/import_template/`.

- Player metadata: `id` (optional, generated if blank), `club`, `first_name`, `last_name`, `known_as`,
  `shirt_number`, `nationality`, `age`, `height_cm`, `positions` (separated by `;`, `/`, `,` or `|`, e.g.
  `ST;LW`), `preferred_foot` (`left`/`right`/`L`/`R`), `weak_foot`, `skill_moves`. Blank values fall back to
  sensible defaults.
- Attributes: use any of the 34 keys. Common alternative headers are mapped too. Matching ignores case,
  spaces, `-` and `_`. For example, `sprint_speed` maps to `topSpeed`, `ball_control` to `firstTouch`,
  `positioning` to `offTheBall`, `heading_accuracy` to `heading`, `free_kick_accuracy` to `setPieces`, `curve` to
  `curl` and `gk_kicking` to `gkDistribution`. The full list is `ATTRIBUTE_ALIASES` in `import_csv.py`.
- Any missing attribute is filled from a group column (`pace`, `shooting`, `passing`, `dribbling`,
  `defending`, `physical`, `goalkeeping`) when one exists. Keeper attributes default to 3 for outfielders.
  Anything else is an error that names the column.
- Scale: `--scale 99` (the default) maps 0-99 linearly to 1-20 with `1 + round(v * 19 / 99)`. `--scale 20`
  takes the values as they are.
- Club CSV (optional): `club, id, short_name, primary, secondary, away, stadium, capacity, formation, reputation`.
  Clubs that are missing from it get grey colours, a 4-4-2 and reputation 3.

`import_template/examples/` holds a two-club round-trip fixture taken from the fictional league. The C# test
`RosterLoaderTests.CsvImportExample_Load_MatchesBundledClubs` loads the imported JSON and checks every attribute
against the bundled roster.
