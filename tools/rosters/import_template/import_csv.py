#!/usr/bin/env python3
"""Convert a user's own CSV squad list into an OpenFifa roster JSON (docs/ROSTER_FORMAT.md), and back.

    # CSV -> roster JSON (attributes on a 0-99 scale are mapped to OpenFifa's 1-20 scale)
    python3 tools/rosters/import_template/import_csv.py players.csv --clubs clubs.csv \
        --league-name "My League" --scale 99 --out my_league.roster.json

    # roster JSON -> CSV (always written on the 1-20 scale)
    python3 tools/rosters/import_template/import_csv.py --to-csv league.json --out players.csv --clubs-out clubs.csv

Copy the resulting *.roster.json into the OpenFifa folder in the iPad Files app (or the macOS Documents folder)
and pick it from the roster menu. This tool only converts data the user already has; it does not download
anything. Do not commit or redistribute rosters containing real-world names or third-party ratings.
Python stdlib only.
"""

import argparse
import csv
import os
import sys

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
from roster_common import (ATTR_MAX, ATTR_MIN, ATTRIBUTE_KEYS, FORMAT_TAG, POSITIONS,  # noqa: E402
                           SCHEMA_VERSION, clamp, dump_roster)

PLAYER_COLUMNS = ["id", "club", "first_name", "last_name", "known_as", "shirt_number", "nationality", "age",
                  "height_cm", "positions", "preferred_foot", "weak_foot", "skill_moves"]
CLUB_COLUMNS = ["club", "id", "short_name", "primary", "secondary", "away", "stadium", "capacity", "formation",
                "reputation"]

# Accepted header spellings for each attribute (case-insensitive; spaces, '-' and '_' ignored).
ATTRIBUTE_ALIASES = {
    "acceleration": ["acceleration", "accel"],
    "topSpeed": ["topspeed", "sprintspeed", "speed", "pace_speed"],
    "finishing": ["finishing"],
    "shotPower": ["shotpower", "power"],
    "longShots": ["longshots", "longshot"],
    "offTheBall": ["offtheball", "positioning", "attackingpositioning", "movement"],
    "volleys": ["volleys", "volley"],
    "penalties": ["penalties", "penalty"],
    "shortPassing": ["shortpassing", "shortpass", "passing_short"],
    "longPassing": ["longpassing", "longpass"],
    "vision": ["vision"],
    "crossing": ["crossing", "crosses"],
    "curl": ["curl", "curve"],
    "setPieces": ["setpieces", "freekick", "freekickaccuracy", "freekicks"],
    "dribbling": ["dribbling"],
    "firstTouch": ["firsttouch", "ballcontrol", "control"],
    "agility": ["agility"],
    "balance": ["balance"],
    "composure": ["composure"],
    "marking": ["marking", "defensiveawareness", "awareness"],
    "standingTackle": ["standingtackle", "tackling", "tackle"],
    "slidingTackle": ["slidingtackle"],
    "interceptions": ["interceptions", "interception", "anticipation"],
    "heading": ["heading", "headingaccuracy", "headers"],
    "stamina": ["stamina", "workrate"],
    "strength": ["strength"],
    "jumping": ["jumping", "jump"],
    "aggression": ["aggression"],
    "reactions": ["reactions", "reaction"],
    "gkDiving": ["gkdiving", "diving"],
    "gkHandling": ["gkhandling", "handling"],
    "gkDistribution": ["gkdistribution", "gkkicking", "kicking", "distribution"],
    "gkReflexes": ["gkreflexes", "reflexes"],
    "gkPositioning": ["gkpositioning", "keeperpositioning"],
}
# Optional group columns used to fill any attribute the CSV leaves out.
GROUP_OF = {}
for _k in ATTRIBUTE_KEYS[0:2]:
    GROUP_OF[_k] = "pace"
for _k in ATTRIBUTE_KEYS[2:8]:
    GROUP_OF[_k] = "shooting"
for _k in ATTRIBUTE_KEYS[8:14]:
    GROUP_OF[_k] = "passing"
for _k in ATTRIBUTE_KEYS[14:19]:
    GROUP_OF[_k] = "dribbling"
for _k in ATTRIBUTE_KEYS[19:24]:
    GROUP_OF[_k] = "defending"
for _k in ATTRIBUTE_KEYS[24:29]:
    GROUP_OF[_k] = "physical"
for _k in ATTRIBUTE_KEYS[29:34]:
    GROUP_OF[_k] = "goalkeeping"
GROUP_COLUMNS = ["pace", "shooting", "passing", "dribbling", "defending", "physical", "goalkeeping"]


class ImportError_(Exception):
    pass


def norm(header):
    return "".join(ch for ch in header.strip().lower() if ch not in " -_")


def build_header_map(headers):
    """Maps normalised CSV headers to attribute keys / group names. Reports ambiguous duplicates."""
    lookup = {}
    for key, aliases in ATTRIBUTE_ALIASES.items():
        for a in aliases:
            lookup[norm(a)] = key
    attr_cols, group_cols, unknown = {}, {}, []
    known_meta = {norm(c) for c in PLAYER_COLUMNS}
    for h in headers:
        n = norm(h)
        if n in known_meta:
            continue
        if n in lookup:
            key = lookup[n]
            if key in attr_cols:
                raise ImportError_("columns '%s' and '%s' both map to attribute %s" % (attr_cols[key], h, key))
            attr_cols[key] = h
        elif n in GROUP_COLUMNS:
            group_cols[n] = h
        else:
            unknown.append(h)
    return attr_cols, group_cols, unknown


def to_scale20(raw, scale, where):
    try:
        v = float(raw)
    except ValueError:
        raise ImportError_("%s: '%s' is not a number" % (where, raw))
    if scale == 20:
        if v < ATTR_MIN or v > ATTR_MAX:
            raise ImportError_("%s: %s is outside 1-20" % (where, raw))
        return int(round(v))
    if v < 0 or v > 99:
        raise ImportError_("%s: %s is outside 0-99" % (where, raw))
    # 0..99 -> 1..20, linear, round half up
    return int(clamp(int(1 + v * 19.0 / 99.0 + 0.5), ATTR_MIN, ATTR_MAX))


def slug(text):
    out, pending = [], False
    for ch in text:
        c = ch.lower() if "A" <= ch <= "Z" else ch
        if ("a" <= c <= "z") or ("0" <= c <= "9"):
            if pending and out:
                out.append("_")
            pending = False
            out.append(c)
        else:
            pending = True
    return "".join(out)


def get(row, col, default=""):
    for k, v in row.items():
        if k is not None and norm(k) == norm(col):
            return (v or "").strip()
    return default


def int_field(row, col, default, lo, hi, where):
    raw = get(row, col)
    if raw == "":
        return default
    try:
        v = int(round(float(raw)))
    except ValueError:
        raise ImportError_("%s: %s '%s' is not a number" % (where, col, raw))
    if v < lo or v > hi:
        raise ImportError_("%s: %s %d is outside %d-%d" % (where, col, v, lo, hi))
    return v


def parse_positions(raw, where):
    parts = [p.strip().upper() for p in raw.replace("/", ";").replace(",", ";").replace("|", ";").split(";")]
    parts = [p for p in parts if p]
    if not parts:
        raise ImportError_("%s: positions is empty (e.g. 'ST;LW')" % where)
    for p in parts:
        if p not in POSITIONS:
            raise ImportError_("%s: unknown position '%s' (use %s)" % (where, p, ", ".join(POSITIONS)))
    out = []
    for p in parts:
        if p not in out:
            out.append(p)
    return out[:4]


def default_short_name(name, taken):
    letters = [c for c in name.upper() if "A" <= c <= "Z"]
    cand = "".join(letters[:3]).ljust(3, "X")
    i = 0
    while cand in taken:
        i += 1
        cand = cand[:2] + str(i % 10)
    taken.add(cand)
    return cand


def read_clubs(path):
    clubs = {}
    if not path:
        return clubs
    with open(path, newline="", encoding="utf-8-sig") as f:
        for i, row in enumerate(csv.DictReader(f), start=2):
            name = get(row, "club")
            if not name:
                raise ImportError_("%s line %d: club is empty" % (path, i))
            clubs[name] = row
    return clubs


def csv_to_roster(players_csv, clubs_csv=None, scale=99, league_name="Imported League", league_id="imported",
                  season="", source_note=""):
    club_meta = read_clubs(clubs_csv)
    with open(players_csv, newline="", encoding="utf-8-sig") as f:
        reader = csv.DictReader(f)
        headers = reader.fieldnames or []
        attr_cols, group_cols, unknown = build_header_map(headers)
        rows = list(reader)
    if unknown:
        sys.stderr.write("warning: ignoring unknown columns: %s\n" % ", ".join(unknown))
    clubs, order = {}, []
    used_ids, taken_short = set(), set()
    for i, row in enumerate(rows, start=2):
        where = "%s line %d" % (os.path.basename(players_csv), i)
        club_name = get(row, "club")
        if not club_name:
            raise ImportError_("%s: club is empty" % where)
        last = get(row, "last_name")
        if not last:
            raise ImportError_("%s: last_name is empty" % where)
        attrs = {}
        for key in ATTRIBUTE_KEYS:
            if key in attr_cols and get(row, attr_cols[key]) != "":
                attrs[key] = to_scale20(get(row, attr_cols[key]), scale, "%s: %s" % (where, attr_cols[key]))
            else:
                group = GROUP_OF[key]
                # "dribbling" is both an attribute and a group name; its column doubles as the group value.
                group_col = group_cols.get(group) or attr_cols.get(group)
                if group_col and get(row, group_col) != "":
                    attrs[key] = to_scale20(get(row, group_col), scale, "%s: %s" % (where, group))
                elif group == "goalkeeping":
                    attrs[key] = 3  # outfielders rarely have keeper ratings
                else:
                    raise ImportError_("%s: no value for attribute %s (add a '%s' or '%s' column)"
                                       % (where, key, key, group))
        if club_name not in clubs:
            meta = club_meta.get(club_name, {})
            cid = get(meta, "id") or slug(club_name) or "club%d" % (len(order) + 1)
            short = (get(meta, "short_name") or default_short_name(club_name, taken_short)).upper()
            taken_short.add(short)
            clubs[club_name] = {
                "id": cid, "name": club_name, "shortName": short,
                "colours": {"primary": get(meta, "primary") or "#808080",
                            "secondary": get(meta, "secondary") or "#FFFFFF",
                            "away": get(meta, "away") or "#202020"},
                "stadium": {"name": get(meta, "stadium") or club_name + " Ground",
                            "capacity": int_field(meta, "capacity", 20000, 1000, 150000, club_name)},
                "formation": get(meta, "formation") or "4-4-2",
                "reputation": int_field(meta, "reputation", 3, 1, 5, club_name),
                "players": [],
            }
            order.append(club_name)
        club = clubs[club_name]
        pid = get(row, "id") or "%s-%02d" % (club["id"], len(club["players"]) + 1)
        if pid in used_ids:
            raise ImportError_("%s: duplicate player id '%s'" % (where, pid))
        used_ids.add(pid)
        foot = (get(row, "preferred_foot") or "right").lower()
        if foot in ("l", "left"):
            foot = "left"
        elif foot in ("r", "right"):
            foot = "right"
        else:
            raise ImportError_("%s: preferred_foot '%s' must be left or right" % (where, foot))
        shirt_default = len(club["players"]) + 1
        club["players"].append({
            "id": pid,
            "firstName": get(row, "first_name"),
            "lastName": last,
            "knownAs": get(row, "known_as") or last,
            "shirtNumber": int_field(row, "shirt_number", shirt_default, 1, 99, where),
            "nationality": get(row, "nationality") or "Unknown",
            "age": int_field(row, "age", 25, 15, 45, where),
            "heightCm": int_field(row, "height_cm", 180, 150, 215, where),
            "positions": parse_positions(get(row, "positions"), where),
            "preferredFoot": foot,
            "weakFoot": int_field(row, "weak_foot", 3, 1, 5, where),
            "skillMoves": int_field(row, "skill_moves", 2, 1, 5, where),
            "attributes": attrs,
        })
    return {
        "format": FORMAT_TAG,
        "schemaVersion": SCHEMA_VERSION,
        "league": {"id": league_id, "name": league_name, "shortName": (league_id[:8] or "IMP").upper(),
                   "country": "", "season": season},
        "provenance": {"source": "user-import", "fictional": False,
                       "generator": "tools/rosters/import_template/import_csv.py",
                       "notes": source_note or "Converted from a user-supplied CSV. Not distributed with OpenFifa."},
        "clubs": [clubs[n] for n in order],
    }


def roster_to_csv(doc, players_out, clubs_out=None):
    with open(players_out, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(PLAYER_COLUMNS + ATTRIBUTE_KEYS)
        for club in doc["clubs"]:
            for p in club["players"]:
                w.writerow([p["id"], club["name"], p.get("firstName", ""), p["lastName"], p.get("knownAs", ""),
                            p["shirtNumber"], p["nationality"], p["age"], p["heightCm"], ";".join(p["positions"]),
                            p["preferredFoot"], p["weakFoot"], p["skillMoves"]]
                           + [p["attributes"][k] for k in ATTRIBUTE_KEYS])
    if clubs_out:
        with open(clubs_out, "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f, lineterminator="\n")
            w.writerow(CLUB_COLUMNS)
            for c in doc["clubs"]:
                w.writerow([c["name"], c["id"], c["shortName"], c["colours"]["primary"], c["colours"]["secondary"],
                            c["colours"]["away"], c["stadium"]["name"], c["stadium"]["capacity"], c["formation"],
                            c["reputation"]])


def main(argv=None):
    import json
    ap = argparse.ArgumentParser(description="CSV <-> OpenFifa roster JSON")
    ap.add_argument("players", nargs="?", help="players CSV (CSV -> JSON mode)")
    ap.add_argument("--clubs", help="optional clubs CSV (colours, stadium, formation...)")
    ap.add_argument("--scale", type=int, choices=(20, 99), default=99,
                    help="attribute scale used in the CSV: 99 (0-99, mapped to 1-20) or 20 (already 1-20)")
    ap.add_argument("--league-name", default="Imported League")
    ap.add_argument("--league-id", default="imported")
    ap.add_argument("--season", default="")
    ap.add_argument("--to-csv", metavar="ROSTER_JSON", help="convert a roster JSON back to CSV (1-20 scale)")
    ap.add_argument("--clubs-out", help="with --to-csv: also write a clubs CSV")
    ap.add_argument("--out", required=True)
    a = ap.parse_args(argv)
    try:
        if a.to_csv:
            with open(a.to_csv, encoding="utf-8") as f:
                roster_to_csv(json.load(f), a.out, a.clubs_out)
            print("wrote %s" % a.out)
            return 0
        if not a.players:
            ap.error("players CSV is required unless --to-csv is used")
        doc = csv_to_roster(a.players, a.clubs, a.scale, a.league_name, a.league_id, a.season)
        dump_roster(doc, a.out)
        n = sum(len(c["players"]) for c in doc["clubs"])
        print("wrote %s: %d clubs, %d players" % (a.out, len(doc["clubs"]), n))
        if not a.out.endswith(".roster.json"):
            sys.stderr.write("note: OpenFifa only lists files ending in .roster.json\n")
        return 0
    except ImportError_ as e:
        sys.stderr.write("error: %s\n" % e)
        return 1


if __name__ == "__main__":
    sys.exit(main())
