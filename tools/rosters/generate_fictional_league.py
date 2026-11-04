#!/usr/bin/env python3
"""Generates OpenFifa's bundled, fully fictional default league.

    python3 tools/rosters/generate_fictional_league.py \
        [--out OpenFifa/Assets/StreamingAssets/Rosters/default_league.json] [--seed 20261104]

Deterministic: same seed -> byte-identical file (uses its own random.Random, no clock, no hash order).
Every club, stadium and player name is invented here. No real league, club, player or ratings data is
read or referenced. Attributes are on OpenFifa's own 1-20 scale (docs/ROSTER_FORMAT.md).
"""

import argparse
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from roster_common import ATTRIBUTE_KEYS, FORMAT_TAG, SCHEMA_VERSION, clamp, dump_roster  # noqa: E402

DEFAULT_SEED = 20261104
REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DEFAULT_OUT = os.path.join(REPO_ROOT, "OpenFifa", "Assets", "StreamingAssets", "Rosters", "default_league.json")

# id, name, shortName, primary, secondary, away, stadium, capacity, formation, reputation
CLUBS = [
    ("kingsmere", "Kingsmere United", "KMU", "#B3122E", "#FFFFFF", "#1C1C24", "The Kingsmere Bowl", 61200, "4-3-3", 5),
    ("ashbury", "Ashbury Athletic", "ASH", "#1B3F8F", "#F2C230", "#FFFFFF", "Ashbury Lane", 58400, "4-2-3-1", 5),
    ("redhaven", "Redhaven City", "RHC", "#6EC1E4", "#0E2240", "#F4F1E8", "Harbourside Park", 54100, "4-3-3", 5),
    ("thornwick", "Thornwick Wanderers", "THW", "#0B6E4F", "#FFFFFF", "#F28C28", "The Bramble Ground", 42300, "4-2-3-1", 4),
    ("calderbrook", "Calderbrook Rovers", "CBR", "#4B1E6D", "#E8E8E8", "#C9A227", "Mill Road", 39800, "4-4-2", 4),
    ("westmoor", "Westmoor Albion", "WMA", "#FFFFFF", "#101820", "#D7263D", "Westmoor Heath Stadium", 36700, "4-3-3", 4),
    ("portelsby", "Port Elsby", "PEL", "#F3D03E", "#002D72", "#2E2E2E", "The Quayside", 33900, "3-4-3", 4),
    ("elmstead", "Elmstead Town", "ELM", "#C8102E", "#000000", "#FFFFFF", "Elmstead Common", 31200, "4-4-2", 3),
    ("hollinsvale", "Hollins Vale", "HOV", "#7A2639", "#86C5DA", "#FFFFFF", "Vale Meadow", 28500, "4-2-3-1", 3),
    ("marrowgate", "Marrowgate Harriers", "MGH", "#00843D", "#FFD100", "#3B3B3B", "Harrier Park", 27100, "4-3-3", 3),
    ("saltcombe", "Saltcombe Mariners", "SCM", "#003B5C", "#FFFFFF", "#E35205", "The Lighthouse Ground", 25600, "5-3-2", 3),
    ("brindlewood", "Brindlewood FC", "BRW", "#E87722", "#1D1D1B", "#BFD7EA", "Brindle Fields", 24300, "4-1-4-1", 3),
    ("corvain", "Corvain Borough", "COB", "#231F20", "#FFFFFF", "#9BCB3B", "Corvain Moor", 23800, "4-4-2", 3),
    ("oakhurst", "Oakhurst Athletic", "OAK", "#5C8A3A", "#F4E9CD", "#4A2C2A", "The Acorn Stadium", 21900, "4-2-3-1", 2),
    ("ferrowby", "Ferrowby United", "FER", "#A50034", "#00A3E0", "#FFFFFF", "Ironworks Road", 20400, "3-5-2", 2),
    ("gallowmere", "Gallowmere Town", "GMT", "#1F4E79", "#E0E0E0", "#F6A800", "Mere Side", 18700, "4-4-2", 2),
    ("dunmarsh", "Dunmarsh Rangers", "DUN", "#2D6A4F", "#000000", "#F0EAD6", "The Marsh End", 17500, "4-3-3", 2),
    ("tillbrook", "Tillbrook Swifts", "TIL", "#87CEEB", "#7B1E1E", "#FFFFFF", "Swift Lane", 15200, "4-4-2", 1),
    ("quarrington", "Quarrington Rovers", "QRV", "#5D3A9B", "#F1C40F", "#2C3E50", "Quarry Bank", 14100, "4-5-1", 1),
    ("lowthercross", "Lowther Cross", "LWX", "#D35400", "#FFFFFF", "#1A5276", "Crossway Park", 12800, "4-2-3-1", 1),
]

# Starting-XI centre of the club quality distribution on the 1-20 scale, by reputation.
QUALITY_BY_REPUTATION = {5: 15.2, 4: 14.2, 3: 13.1, 2: 12.3, 1: 11.6}

# Squad template: (primary, [secondary candidates]) x 25
SQUAD = [
    ("GK", []), ("GK", []), ("GK", []),
    ("CB", []), ("CB", []), ("CB", ["CDM"]), ("CB", ["RB"]),
    ("RB", ["RWB", "RM"]), ("RB", ["CB"]),
    ("LB", ["LWB", "LM"]), ("LB", ["CB"]),
    ("CDM", ["CM"]), ("CDM", ["CB", "CM"]),
    ("CM", ["CDM"]), ("CM", ["CAM"]), ("CM", ["CDM", "RM"]),
    ("CAM", ["CM", "CF"]), ("CAM", ["LW", "RW"]),
    ("RW", ["RM", "ST"]), ("LW", ["LM", "ST"]),
    ("RM", ["RW", "RB"]), ("LM", ["LW", "LB"]),
    ("ST", ["CF"]), ("ST", []), ("ST", ["LW", "RW"]),
]

# Offsets from the player's quality level per position family. Unlisted outfield attributes get
# OUTFIELD_DEFAULT; goalkeeping attributes for outfielders get OUTFIELD_GK.
OUTFIELD_DEFAULT = -2.0
OUTFIELD_GK = -11.0
PROFILES = {
    "GK": {"_default": -7.0, "gkDiving": 1.0, "gkHandling": 0.5, "gkDistribution": -1.0, "gkReflexes": 1.0,
           "gkPositioning": 0.5, "reactions": 0.0, "composure": -1.0, "jumping": -2.0, "strength": -2.5,
           "longPassing": -4.0, "shortPassing": -4.0, "agility": -3.5, "balance": -4.0},
    "CB": {"marking": 1.0, "standingTackle": 1.0, "slidingTackle": 0.5, "interceptions": 0.8, "heading": 1.0,
           "strength": 1.0, "jumping": 0.5, "aggression": 0.5, "finishing": -6.0, "volleys": -6.0, "longShots": -5.0,
           "dribbling": -4.0, "crossing": -4.0, "curl": -5.0, "setPieces": -5.0, "offTheBall": -6.0, "agility": -3.0,
           "penalties": -5.0},
    "FB": {"acceleration": 0.8, "topSpeed": 1.0, "stamina": 1.0, "crossing": 0.5, "standingTackle": 0.5,
           "marking": -0.5, "interceptions": 0.0, "slidingTackle": 0.0, "finishing": -5.0, "volleys": -5.0,
           "heading": -2.5, "offTheBall": -3.5, "longShots": -4.0, "penalties": -4.5, "setPieces": -3.5},
    "CDM": {"interceptions": 1.0, "standingTackle": 0.8, "marking": 0.3, "shortPassing": 0.6, "longPassing": 0.3,
            "stamina": 0.8, "strength": 0.6, "aggression": 0.8, "finishing": -4.5, "volleys": -4.0,
            "offTheBall": -4.0, "dribbling": -2.5, "crossing": -3.5, "agility": -2.5},
    "CM": {"shortPassing": 1.0, "longPassing": 0.6, "vision": 0.8, "firstTouch": 0.5, "stamina": 0.8,
           "composure": 0.5, "finishing": -3.0, "slidingTackle": -2.5, "heading": -3.0, "marking": -3.0,
           "standingTackle": -1.5, "interceptions": -1.0},
    "CAM": {"vision": 1.0, "shortPassing": 0.8, "firstTouch": 0.8, "dribbling": 0.8, "agility": 0.5,
            "composure": 0.5, "longShots": 0.0, "finishing": -0.5, "curl": 0.2, "marking": -6.0,
            "standingTackle": -5.5, "slidingTackle": -6.5, "interceptions": -4.0, "heading": -4.0, "strength": -3.5},
    "WM": {"crossing": 1.0, "acceleration": 0.8, "topSpeed": 0.8, "stamina": 1.0, "dribbling": 0.5,
           "curl": 0.0, "marking": -4.0, "standingTackle": -3.0, "slidingTackle": -4.0, "heading": -4.0,
           "strength": -3.0, "finishing": -2.0},
    "W": {"acceleration": 1.2, "topSpeed": 1.0, "dribbling": 1.0, "agility": 1.0, "balance": 0.5,
          "firstTouch": 0.3, "finishing": -0.5, "crossing": 0.0, "marking": -6.5, "standingTackle": -6.0,
          "slidingTackle": -7.0, "interceptions": -5.0, "heading": -4.5, "strength": -3.5, "stamina": -1.0},
    "ST": {"finishing": 1.2, "offTheBall": 1.0, "shotPower": 0.6, "heading": 0.3, "volleys": 0.3,
           "composure": 0.4, "reactions": 0.4, "penalties": 0.0, "marking": -7.0, "standingTackle": -6.5,
           "slidingTackle": -7.5, "interceptions": -5.5, "longPassing": -3.5, "crossing": -3.5, "vision": -2.5},
}
FAMILY = {"GK": "GK", "CB": "CB", "RB": "FB", "LB": "FB", "RWB": "FB", "LWB": "FB", "CDM": "CDM", "CM": "CM",
          "CAM": "CAM", "RM": "WM", "LM": "WM", "RW": "W", "LW": "W", "CF": "ST", "ST": "ST"}
HEIGHT = {"GK": (186, 6), "CB": (187, 5), "FB": (177, 5), "CDM": (181, 5), "CM": (178, 6), "CAM": (175, 6),
          "WM": (175, 6), "W": (173, 6), "ST": (183, 7)}

FIRST_NAMES = [
    "Aaron", "Adam", "Alfie", "Ali", "Amir", "Anders", "Andre", "Ben", "Callum", "Cameron", "Carlos", "Charlie",
    "Conor", "Dan", "Darius", "David", "Dean", "Diego", "Dylan", "Eddie", "Elias", "Emil", "Ethan", "Felix",
    "Finn", "Gabriel", "George", "Hamza", "Harry", "Hugo", "Ibrahim", "Isaac", "Ivan", "Jack", "Jacob", "Jake",
    "Jamal", "James", "Joel", "Jonah", "Jordan", "Josh", "Julian", "Kai", "Kieran", "Kofi", "Leo", "Liam",
    "Lucas", "Luka", "Malik", "Marco", "Mason", "Mateo", "Max", "Milan", "Musa", "Nathan", "Nico", "Noah",
    "Oliver", "Omar", "Oscar", "Owen", "Pablo", "Rafael", "Reece", "Rhys", "Riley", "Ryan", "Sam", "Sami",
    "Sean", "Seb", "Theo", "Tobias", "Tom", "Tyler", "Victor", "Will", "Yannick", "Yusuf", "Zach", "Zane",
]
# Surnames are assembled from syllables so they are invented rather than copied from anyone.
SURNAME_HEADS = [
    "Ash", "Bram", "Brack", "Cal", "Carr", "Dar", "Dun", "Ell", "Fen", "Garr", "Hal", "Hollo", "Kest", "Lang",
    "Lom", "Marr", "Mor", "Nor", "Orr", "Pell", "Quill", "Rad", "Rook", "Sel", "Stam", "Tal", "Tor", "Vann",
    "Wex", "Yar", "Okon", "Adel", "Bel", "Cosi", "Dravo", "Esk", "Ferr", "Ivo", "Jorr", "Kalu", "Lev", "Mado",
    "Nuar", "Osk", "Pavo", "Rion", "Sabe", "Tevi", "Ulle", "Vossa",
]
SURNAME_TAILS = [
    "berry", "bourne", "brook", "by", "combe", "croft", "dale", "den", "field", "ford", "gate", "ham", "hurst",
    "ley", "low", "mere", "more", "ridge", "shaw", "stone", "thorpe", "ton", "wick", "wood", "worth",
    "ani", "aro", "ek", "ella", "enko", "ez", "ic", "ini", "isse", "ola", "oni", "ov", "uda", "ul", "ewe",
]
NATIONALITIES = (
    [("England", 34), ("Scotland", 5), ("Wales", 4), ("Ireland", 4), ("Northern Ireland", 2)]
    + [("France", 5), ("Spain", 4), ("Portugal", 3), ("Netherlands", 3), ("Belgium", 3), ("Germany", 3),
       ("Denmark", 2), ("Norway", 2), ("Sweden", 2), ("Italy", 2), ("Croatia", 1), ("Serbia", 1), ("Poland", 1),
       ("Brazil", 3), ("Argentina", 2), ("Uruguay", 1), ("Colombia", 1), ("United States", 2), ("Canada", 1),
       ("Nigeria", 2), ("Ghana", 2), ("Senegal", 2), ("Ivory Coast", 1), ("Morocco", 1), ("Cameroon", 1),
       ("Japan", 1), ("South Korea", 1), ("Australia", 1), ("Jamaica", 1)]
)


def weighted(rng, items):
    total = sum(w for _, w in items)
    r = rng.random() * total
    for v, w in items:
        r -= w
        if r < 0:
            return v
    return items[-1][0]


def invent_surname(rng, used):
    for _ in range(200):
        name = rng.choice(SURNAME_HEADS) + rng.choice(SURNAME_TAILS)
        if name not in used:
            used.add(name)
            return name
    raise RuntimeError("surname space exhausted")


def make_attributes(rng, quality, family):
    prof = PROFILES[family]
    attrs = {}
    for key in ATTRIBUTE_KEYS:
        if key in prof:
            off = prof[key]
        elif family == "GK":
            off = prof["_default"]
        elif key.startswith("gk"):
            off = OUTFIELD_GK
        else:
            off = OUTFIELD_DEFAULT
        val = quality + off + rng.gauss(0.0, 1.1)
        attrs[key] = int(clamp(round(val), 1, 20))
    return attrs


def make_player(rng, club_id, index, primary, secondaries, quality, used_surnames, shirt):
    family = FAMILY[primary]
    age = int(clamp(round(rng.gauss(26.0, 4.2)), 17, 37))
    # Youngsters and veterans sit a little lower.
    age_adj = -1.2 if age <= 19 else -0.5 if age <= 21 else -0.6 if age >= 34 else 0.0
    attrs = make_attributes(rng, quality + age_adj, family)
    if age >= 31:  # pace fades first
        for k in ("acceleration", "topSpeed", "agility"):
            attrs[k] = max(1, attrs[k] - (age - 30) // 2)
    hm, hs = HEIGHT[family]
    height = int(clamp(round(rng.gauss(hm, hs)), 160, 205))
    positions = [primary]
    if secondaries and rng.random() < 0.7:
        positions.append(rng.choice(secondaries))
    foot = "left" if (primary in ("LB", "LWB", "LM", "RW") and rng.random() < 0.7) or rng.random() < 0.18 else "right"
    if family == "GK":
        skill = 1
    elif family in ("CB",):
        skill = 2 if rng.random() < 0.8 else 3
    else:
        skill = int(clamp(round(2.2 + (attrs["dribbling"] - 10) / 3.5 + rng.gauss(0, 0.5)), 1, 5))
    weak = int(clamp(round(rng.gauss(3.0, 0.8)), 1, 5))
    last = invent_surname(rng, used_surnames)
    return {
        "id": "%s-%02d" % (club_id, index + 1),
        "firstName": rng.choice(FIRST_NAMES),
        "lastName": last,
        "knownAs": last,
        "shirtNumber": shirt,
        "nationality": weighted(rng, NATIONALITIES),
        "age": age,
        "heightCm": height,
        "positions": positions,
        "preferredFoot": foot,
        "weakFoot": weak,
        "skillMoves": skill,
        "attributes": attrs,
    }


def shirt_numbers(rng, count):
    # Classic numbers for the first choices, the rest from 12..45 without repeats.
    preferred = [1, 13, 31, 5, 6, 4, 15, 2, 22, 3, 23, 8, 16, 10, 18, 14, 20, 21, 7, 11, 17, 19, 9, 24, 29]
    return preferred[:count]


def generate(seed):
    rng = random.Random(seed)
    used_surnames = set()
    clubs = []
    for (cid, name, short, c1, c2, c3, stadium, cap, formation, rep) in CLUBS:
        base = QUALITY_BY_REPUTATION[rep] + rng.uniform(-0.3, 0.3)
        numbers = shirt_numbers(rng, len(SQUAD))
        players = []
        seen_primary = {}
        for i, (primary, secondaries) in enumerate(SQUAD):
            depth = seen_primary.get(primary, 0)
            seen_primary[primary] = depth + 1
            # First choice at a position is the strongest, squad depth tails off.
            depth_adj = 0.6 if depth == 0 else -0.6 if depth == 1 else -1.6
            quality = base + depth_adj + rng.gauss(0.0, 0.8)
            players.append(make_player(rng, cid, i, primary, secondaries, quality, used_surnames, numbers[i]))
        clubs.append({
            "id": cid, "name": name, "shortName": short,
            "colours": {"primary": c1, "secondary": c2, "away": c3},
            "stadium": {"name": stadium, "capacity": cap},
            "formation": formation, "reputation": rep,
            "players": players,
        })
    return {
        "format": FORMAT_TAG,
        "schemaVersion": SCHEMA_VERSION,
        "league": {"id": "opd", "name": "OpenFootball Premier Division", "shortName": "OPD",
                   "country": "Albion (fictional)", "season": "2026/27"},
        "provenance": {
            "source": "fictional-generated",
            "fictional": True,
            "generator": "tools/rosters/generate_fictional_league.py",
            "seed": seed,
            "license": "Same license as OpenFootball",
            "notes": "All clubs, grounds and players are invented. Attributes are OpenFootball's own 1-20 scale, "
                     "generated from position profiles and club reputation; no third-party ratings were used.",
        },
        "clubs": clubs,
    }


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out", default=DEFAULT_OUT)
    ap.add_argument("--seed", type=int, default=DEFAULT_SEED)
    args = ap.parse_args(argv)
    doc = generate(args.seed)
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    dump_roster(doc, args.out)
    n = sum(len(c["players"]) for c in doc["clubs"])
    print("wrote %s: %d clubs, %d players" % (args.out, len(doc["clubs"]), n))


if __name__ == "__main__":
    main()
