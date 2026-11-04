"""Shared constants for the OpenFifa roster tools (Python stdlib only).

Mirrors OpenFifa/Assets/Scripts/Core/Rosters/RosterSchema.cs. docs/ROSTER_FORMAT.md is the spec.
"""

import json

FORMAT_TAG = "openfootball-roster"
SCHEMA_VERSION = 1

ATTRIBUTE_KEYS = [
    # pace
    "acceleration", "topSpeed",
    # shooting
    "finishing", "shotPower", "longShots", "offTheBall", "volleys", "penalties",
    # passing
    "shortPassing", "longPassing", "vision", "crossing", "curl", "setPieces",
    # dribbling
    "dribbling", "firstTouch", "agility", "balance", "composure",
    # defending
    "marking", "standingTackle", "slidingTackle", "interceptions", "heading",
    # physical
    "stamina", "strength", "jumping", "aggression", "reactions",
    # goalkeeping
    "gkDiving", "gkHandling", "gkDistribution", "gkReflexes", "gkPositioning",
]

POSITIONS = ["GK", "RB", "CB", "LB", "RWB", "LWB", "CDM", "CM", "CAM", "RM", "LM", "RW", "LW", "CF", "ST"]

ATTR_MIN, ATTR_MAX = 1, 20


def clamp(v, lo, hi):
    return lo if v < lo else hi if v > hi else v


def dump_roster(doc, path):
    """Writes a roster with one player per line so diffs stay readable."""
    clubs = doc["clubs"]
    head = {k: v for k, v in doc.items() if k != "clubs"}
    out = ["{"]
    for k, v in head.items():
        out.append("  %s: %s," % (json.dumps(k), json.dumps(v, ensure_ascii=False)))
    out.append('  "clubs": [')
    for ci, club in enumerate(clubs):
        out.append("    {")
        for k, v in club.items():
            if k == "players":
                continue
            out.append("      %s: %s," % (json.dumps(k), json.dumps(v, ensure_ascii=False)))
        out.append('      "players": [')
        for pi, p in enumerate(club["players"]):
            sep = "," if pi < len(club["players"]) - 1 else ""
            out.append("        " + json.dumps(p, ensure_ascii=False) + sep)
        out.append("      ]")
        out.append("    }" + ("," if ci < len(clubs) - 1 else ""))
    out.append("  ]")
    out.append("}")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out) + "\n")
