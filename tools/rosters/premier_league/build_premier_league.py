#!/usr/bin/env python3
"""Builds the Premier League 2026/27 roster pack from hand-written squad sheets.

    python3 tools/rosters/premier_league/build_premier_league.py \
        [--squads tools/rosters/premier_league/squads.txt] \
        [--out OpenFifa/Assets/StreamingAssets/Rosters/Packs/premier_league.roster.json]

The squad sheet (squads.txt) is the source of truth: one line per player with name, number, position(s),
nationality, birth year, height, foot, a 0-99 rating, a few trait tags and a look. This script expands each line
into the 34 OpenFootball attributes (1-20), calibrated so the in-game overall (OverallCalculator) matches the
rating, plus the appearance block. Clubs, grounds and kits live in CLUBS below. Deterministic: no randomness
beyond a hash of the player id.

Data policy: per Tushar (2026-11-04) real names are used for now and licensing is handled later. Ratings are
OpenFootball's own estimates, not copied from any game or ratings site. Swap or delete the pack file to change
leagues; the game falls back to the fictional default league.
"""

import argparse
import hashlib
import os
import sys
import unicodedata

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from roster_common import ATTRIBUTE_KEYS, FORMAT_TAG, SCHEMA_VERSION, clamp, dump_roster  # noqa: E402

REPO_ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
DEFAULT_SQUADS = os.path.join(HERE, "squads.txt")
DEFAULT_OUT = os.path.join(REPO_ROOT, "OpenFifa", "Assets", "StreamingAssets", "Rosters", "Packs",
                           "premier_league.roster.json")
SEASON_YEAR = 2026


def kit(primary, secondary, tertiary, pattern, shorts, socks, number, outline):
    return {"primary": primary, "secondary": secondary, "tertiary": tertiary, "pattern": pattern,
            "shortsColor": shorts, "socksColor": socks, "numberColor": number, "numberOutline": outline}


# id: (name, short, stadium, capacity, formation, reputation, kits{home, away, third, gk})
# Kits are each club's traditional colours, not a specific season's licensed design.
CLUBS = {
    "arsenal": ("Arsenal", "ARS", "Emirates Stadium", 60704, "4-3-3", 5, {
        "home": kit("#EF0107", "#FFFFFF", "#063672", "solid", "#FFFFFF", "#EF0107", "#FFFFFF", "#063672"),
        "away": kit("#F2E6C9", "#063672", "#EF0107", "solid", "#F2E6C9", "#F2E6C9", "#063672", "#F2E6C9"),
        "third": kit("#1B2A4A", "#9FD3F0", "#FFFFFF", "chevron", "#1B2A4A", "#1B2A4A", "#9FD3F0", "#1B2A4A"),
        "gk": kit("#2C2C2C", "#C8F031", "#C8F031", "solid", "#2C2C2C", "#2C2C2C", "#C8F031", "#2C2C2C")}),
    "aston-villa": ("Aston Villa", "AVL", "Villa Park", 42918, "4-2-3-1", 4, {
        "home": kit("#670E36", "#95BFE5", "#FEE505", "halves", "#FFFFFF", "#670E36", "#FFFFFF", "#670E36"),
        "away": kit("#FFFFFF", "#670E36", "#95BFE5", "solid", "#FFFFFF", "#FFFFFF", "#670E36", "#FFFFFF"),
        "third": kit("#95BFE5", "#0B1F3A", "#670E36", "solid", "#0B1F3A", "#95BFE5", "#0B1F3A", "#95BFE5"),
        "gk": kit("#F4D000", "#111111", "#111111", "solid", "#F4D000", "#F4D000", "#111111", "#F4D000")}),
    "bournemouth": ("AFC Bournemouth", "BOU", "Vitality Stadium", 11307, "4-2-3-1", 3, {
        "home": kit("#DA291C", "#111111", "#FFFFFF", "stripes", "#111111", "#111111", "#FFFFFF", "#111111"),
        "away": kit("#FFFFFF", "#DA291C", "#111111", "solid", "#FFFFFF", "#FFFFFF", "#DA291C", "#FFFFFF"),
        "third": kit("#5BC2E7", "#0B1F3A", "#FFFFFF", "solid", "#5BC2E7", "#5BC2E7", "#0B1F3A", "#5BC2E7"),
        "gk": kit("#2E7D32", "#111111", "#111111", "solid", "#2E7D32", "#2E7D32", "#FFFFFF", "#111111")}),
    "brentford": ("Brentford", "BRE", "Gtech Community Stadium", 17250, "4-2-3-1", 3, {
        "home": kit("#E30613", "#FFFFFF", "#111111", "stripes", "#111111", "#111111", "#111111", "#FFFFFF"),
        "away": kit("#111111", "#FFC72C", "#FFC72C", "solid", "#111111", "#111111", "#FFC72C", "#111111"),
        "third": kit("#FFFFFF", "#E30613", "#111111", "solid", "#FFFFFF", "#FFFFFF", "#E30613", "#FFFFFF"),
        "gk": kit("#7CCB3E", "#111111", "#111111", "solid", "#7CCB3E", "#7CCB3E", "#111111", "#7CCB3E")}),
    "brighton": ("Brighton & Hove Albion", "BHA", "Amex Stadium", 31876, "4-2-3-1", 3, {
        "home": kit("#0057B8", "#FFFFFF", "#FFCD00", "stripes", "#0057B8", "#FFFFFF", "#0057B8", "#FFFFFF"),
        "away": kit("#FFCD00", "#0057B8", "#0057B8", "solid", "#FFCD00", "#FFCD00", "#0057B8", "#FFCD00"),
        "third": kit("#111111", "#5BC2E7", "#FFFFFF", "solid", "#111111", "#111111", "#5BC2E7", "#111111"),
        "gk": kit("#E4007C", "#111111", "#111111", "solid", "#E4007C", "#E4007C", "#FFFFFF", "#111111")}),
    "chelsea": ("Chelsea", "CHE", "Stamford Bridge", 40173, "4-2-3-1", 5, {
        "home": kit("#034694", "#FFFFFF", "#DBA111", "solid", "#034694", "#FFFFFF", "#FFFFFF", "#034694"),
        "away": kit("#F5F0E1", "#034694", "#D1D3D4", "solid", "#F5F0E1", "#F5F0E1", "#034694", "#F5F0E1"),
        "third": kit("#111111", "#EE242C", "#EE242C", "solid", "#111111", "#111111", "#EE242C", "#111111"),
        "gk": kit("#00A859", "#111111", "#111111", "solid", "#00A859", "#00A859", "#111111", "#00A859")}),
    "coventry": ("Coventry City", "COV", "Coventry Building Society Arena", 32609, "4-2-3-1", 2, {
        "home": kit("#73B7E8", "#FFFFFF", "#0B1F3A", "solid", "#73B7E8", "#73B7E8", "#FFFFFF", "#0B1F3A"),
        "away": kit("#E2231A", "#111111", "#111111", "solid", "#111111", "#E2231A", "#FFFFFF", "#111111"),
        "third": kit("#FFFFFF", "#73B7E8", "#0B1F3A", "solid", "#FFFFFF", "#FFFFFF", "#0B1F3A", "#FFFFFF"),
        "gk": kit("#2E7D32", "#111111", "#111111", "solid", "#2E7D32", "#2E7D32", "#FFFFFF", "#111111")}),
    "crystal-palace": ("Crystal Palace", "CRY", "Selhurst Park", 25486, "3-4-3", 3, {
        "home": kit("#1B458F", "#C4122E", "#FFFFFF", "stripes", "#1B458F", "#1B458F", "#FFFFFF", "#1B458F"),
        "away": kit("#FFFFFF", "#C4122E", "#1B458F", "sash", "#FFFFFF", "#FFFFFF", "#1B458F", "#FFFFFF"),
        "third": kit("#111111", "#A7A9AC", "#C4122E", "solid", "#111111", "#111111", "#FFFFFF", "#111111"),
        "gk": kit("#F4D000", "#111111", "#111111", "solid", "#F4D000", "#F4D000", "#111111", "#F4D000")}),
    "everton": ("Everton", "EVE", "Hill Dickinson Stadium", 52888, "4-2-3-1", 3, {
        "home": kit("#003399", "#FFFFFF", "#FFFFFF", "solid", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#003399"),
        "away": kit("#F2E14C", "#003399", "#003399", "solid", "#003399", "#F2E14C", "#003399", "#F2E14C"),
        "third": kit("#111111", "#F26522", "#F26522", "solid", "#111111", "#111111", "#F26522", "#111111"),
        "gk": kit("#6CC24A", "#111111", "#111111", "solid", "#6CC24A", "#6CC24A", "#111111", "#6CC24A")}),
    "fulham": ("Fulham", "FUL", "Craven Cottage", 29589, "4-2-3-1", 3, {
        "home": kit("#FFFFFF", "#111111", "#CC0000", "solid", "#111111", "#FFFFFF", "#111111", "#FFFFFF"),
        "away": kit("#111111", "#CC0000", "#FFFFFF", "solid", "#111111", "#111111", "#FFFFFF", "#111111"),
        "third": kit("#B5121B", "#111111", "#FFFFFF", "solid", "#B5121B", "#B5121B", "#FFFFFF", "#B5121B"),
        "gk": kit("#F4D000", "#111111", "#111111", "solid", "#F4D000", "#F4D000", "#111111", "#F4D000")}),
    "hull": ("Hull City", "HUL", "MKM Stadium", 25586, "4-2-3-1", 2, {
        "home": kit("#F5A12D", "#111111", "#111111", "stripes", "#111111", "#F5A12D", "#111111", "#F5A12D"),
        "away": kit("#FFFFFF", "#111111", "#F5A12D", "solid", "#FFFFFF", "#FFFFFF", "#111111", "#FFFFFF"),
        "third": kit("#0B1F3A", "#F5A12D", "#F5A12D", "solid", "#0B1F3A", "#0B1F3A", "#F5A12D", "#0B1F3A"),
        "gk": kit("#6CC24A", "#111111", "#111111", "solid", "#6CC24A", "#6CC24A", "#111111", "#6CC24A")}),
    "ipswich": ("Ipswich Town", "IPS", "Portman Road", 30056, "4-2-3-1", 2, {
        "home": kit("#3A64A3", "#FFFFFF", "#DE2323", "solid", "#FFFFFF", "#3A64A3", "#FFFFFF", "#3A64A3"),
        "away": kit("#DE2323", "#FFFFFF", "#111111", "solid", "#DE2323", "#DE2323", "#FFFFFF", "#DE2323"),
        "third": kit("#111111", "#3A64A3", "#3A64A3", "solid", "#111111", "#111111", "#FFFFFF", "#111111"),
        "gk": kit("#F4D000", "#111111", "#111111", "solid", "#F4D000", "#F4D000", "#111111", "#F4D000")}),
    "leeds": ("Leeds United", "LEE", "Elland Road", 37645, "4-3-3", 3, {
        "home": kit("#FFFFFF", "#1D428A", "#FFCD00", "solid", "#FFFFFF", "#FFFFFF", "#1D428A", "#FFFFFF"),
        "away": kit("#1D428A", "#FFCD00", "#FFCD00", "solid", "#1D428A", "#1D428A", "#FFCD00", "#1D428A"),
        "third": kit("#FFCD00", "#1D428A", "#1D428A", "solid", "#FFCD00", "#FFCD00", "#1D428A", "#FFCD00"),
        "gk": kit("#111111", "#9B1B30", "#9B1B30", "solid", "#111111", "#111111", "#FFFFFF", "#111111")}),
    "liverpool": ("Liverpool", "LIV", "Anfield", 61276, "4-2-3-1", 5, {
        "home": kit("#C8102E", "#FFFFFF", "#F6EB61", "solid", "#C8102E", "#C8102E", "#FFFFFF", "#C8102E"),
        "away": kit("#FFFFFF", "#C8102E", "#111111", "solid", "#FFFFFF", "#FFFFFF", "#C8102E", "#FFFFFF"),
        "third": kit("#0B1F3A", "#00B2A9", "#00B2A9", "solid", "#0B1F3A", "#0B1F3A", "#00B2A9", "#0B1F3A"),
        "gk": kit("#111111", "#00B2A9", "#00B2A9", "solid", "#111111", "#111111", "#00B2A9", "#111111")}),
    "man-city": ("Manchester City", "MCI", "Etihad Stadium", 61470, "4-1-4-1", 5, {
        "home": kit("#6CABDD", "#FFFFFF", "#1C2C5B", "solid", "#FFFFFF", "#6CABDD", "#FFFFFF", "#1C2C5B"),
        "away": kit("#1C2C5B", "#6CABDD", "#FFFFFF", "solid", "#1C2C5B", "#1C2C5B", "#FFFFFF", "#1C2C5B"),
        "third": kit("#F2F2F2", "#7A2E8E", "#7A2E8E", "solid", "#F2F2F2", "#F2F2F2", "#7A2E8E", "#F2F2F2"),
        "gk": kit("#F4D000", "#111111", "#111111", "solid", "#F4D000", "#F4D000", "#111111", "#F4D000")}),
    "man-united": ("Manchester United", "MUN", "Old Trafford", 74197, "3-4-3", 4, {
        "home": kit("#DA291C", "#FFFFFF", "#111111", "solid", "#FFFFFF", "#111111", "#FFFFFF", "#DA291C"),
        "away": kit("#FFFFFF", "#DA291C", "#111111", "solid", "#FFFFFF", "#FFFFFF", "#DA291C", "#FFFFFF"),
        "third": kit("#0B1F3A", "#FBE122", "#FBE122", "solid", "#0B1F3A", "#0B1F3A", "#FBE122", "#0B1F3A"),
        "gk": kit("#2E7D32", "#111111", "#111111", "solid", "#2E7D32", "#2E7D32", "#FFFFFF", "#111111")}),
    "newcastle": ("Newcastle United", "NEW", "St James' Park", 52305, "4-3-3", 4, {
        "home": kit("#111111", "#FFFFFF", "#41B6E6", "stripes", "#111111", "#111111", "#FFFFFF", "#111111"),
        "away": kit("#3A6F4E", "#FFFFFF", "#FFFFFF", "solid", "#3A6F4E", "#3A6F4E", "#FFFFFF", "#3A6F4E"),
        "third": kit("#41B6E6", "#111111", "#111111", "solid", "#FFFFFF", "#41B6E6", "#111111", "#41B6E6"),
        "gk": kit("#F4D000", "#111111", "#111111", "solid", "#F4D000", "#F4D000", "#111111", "#F4D000")}),
    "nottingham-forest": ("Nottingham Forest", "NFO", "City Ground", 30404, "4-2-3-1", 3, {
        "home": kit("#DD0000", "#FFFFFF", "#FFFFFF", "solid", "#FFFFFF", "#DD0000", "#FFFFFF", "#DD0000"),
        "away": kit("#FFFFFF", "#DD0000", "#111111", "solid", "#111111", "#FFFFFF", "#DD0000", "#FFFFFF"),
        "third": kit("#0B1F3A", "#B3A369", "#B3A369", "solid", "#0B1F3A", "#0B1F3A", "#B3A369", "#0B1F3A"),
        "gk": kit("#6CC24A", "#111111", "#111111", "solid", "#6CC24A", "#6CC24A", "#111111", "#6CC24A")}),
    "sunderland": ("Sunderland", "SUN", "Stadium of Light", 48707, "4-2-3-1", 3, {
        "home": kit("#EB172B", "#FFFFFF", "#111111", "stripes", "#111111", "#EB172B", "#111111", "#FFFFFF"),
        "away": kit("#111111", "#EB172B", "#EB172B", "solid", "#111111", "#111111", "#EB172B", "#111111"),
        "third": kit("#B9D9EB", "#0B1F3A", "#0B1F3A", "solid", "#0B1F3A", "#B9D9EB", "#0B1F3A", "#B9D9EB"),
        "gk": kit("#6CC24A", "#111111", "#111111", "solid", "#6CC24A", "#6CC24A", "#111111", "#6CC24A")}),
    "tottenham": ("Tottenham Hotspur", "TOT", "Tottenham Hotspur Stadium", 62850, "4-3-3", 4, {
        "home": kit("#FFFFFF", "#132257", "#132257", "solid", "#132257", "#FFFFFF", "#132257", "#FFFFFF"),
        "away": kit("#132257", "#FFFFFF", "#9FD3F0", "solid", "#132257", "#132257", "#FFFFFF", "#132257"),
        "third": kit("#D4F04B", "#132257", "#132257", "solid", "#132257", "#D4F04B", "#132257", "#D4F04B"),
        "gk": kit("#F26522", "#111111", "#111111", "solid", "#F26522", "#F26522", "#111111", "#F26522")}),
}

HAIR_COLOURS = {"bk": "#141010", "db": "#2B1D14", "br": "#4A3222", "lb": "#7A5A3A", "bl": "#C9A66B",
                "pb": "#E6D8A8", "rd": "#8C3B1E", "gr": "#8A8A8A", "wh": "#D8D8D8"}
BOOT_COLOURS = ["#111111", "#FFFFFF", "#E30613", "#1E64FF", "#F5E50A", "#FF6A13", "#00C2A8", "#D4F04B",
                "#FF3EA5", "#7A2E8E", "#C0C0C0", "#0B1F3A"]
HAIR_STYLES = {"buzz", "short", "curly", "afro", "long_tied", "bald", "braids"}
FACIAL = {"none", "stubble", "beard", "moustache"}
BUILDS = {"slim", "average", "stocky"}
POSITIONS = {"GK", "RB", "CB", "LB", "RWB", "LWB", "CDM", "CM", "CAM", "RM", "LM", "RW", "LW", "CF", "ST"}

# --- attribute model ----------------------------------------------------------------------------------------
# Offsets from a player's level per position family; mirrors tools/rosters/generate_fictional_league.py.
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

# Trait tags in squads.txt -> attribute nudges (points on the 1-20 scale).
TRAITS = {
    "pace": {"acceleration": 2.5, "topSpeed": 2.5},
    "quick": {"acceleration": 2.0, "agility": 1.5},
    "slow": {"acceleration": -2.5, "topSpeed": -3.0},
    "aerial": {"heading": 2.5, "jumping": 2.0},
    "power": {"strength": 2.5, "shotPower": 1.0},
    "engine": {"stamina": 2.5, "aggression": 1.0},
    "passer": {"shortPassing": 2.0, "longPassing": 2.0, "vision": 1.5},
    "vision": {"vision": 2.5, "longPassing": 1.0},
    "dribbler": {"dribbling": 2.0, "agility": 1.5, "balance": 1.5},
    "finisher": {"finishing": 2.0, "composure": 1.5, "offTheBall": 1.5},
    "shot": {"longShots": 2.5, "shotPower": 2.0},
    "fk": {"setPieces": 3.0, "curl": 2.0},
    "cross": {"crossing": 2.5, "curl": 1.0},
    "tackler": {"standingTackle": 2.0, "slidingTackle": 2.0, "aggression": 1.5},
    "reader": {"interceptions": 2.0, "marking": 2.0},
    "creator": {"vision": 2.0, "shortPassing": 1.5, "firstTouch": 1.0},
    "pen": {"penalties": 3.0},
    "shotstop": {"gkReflexes": 2.0, "gkDiving": 1.5},
    "sweeper": {"gkDistribution": 2.0, "acceleration": 3.0, "composure": 2.0},
    "dist": {"gkDistribution": 3.0, "longPassing": 3.0, "shortPassing": 2.0},
    "handler": {"gkHandling": 2.0, "gkPositioning": 1.5},
}

# OverallCalculator.cs weights (keep in step; docs/ROSTER_FORMAT.md has the same table).
WEIGHTS = {
    "GK": {"gkDiving": 22, "gkReflexes": 22, "gkPositioning": 20, "gkHandling": 16, "gkDistribution": 8,
           "reactions": 8, "composure": 4},
    "CB": {"marking": 16, "standingTackle": 16, "interceptions": 14, "heading": 12, "strength": 10,
           "slidingTackle": 8, "jumping": 6, "reactions": 6, "composure": 4, "shortPassing": 4, "topSpeed": 4},
    "FB": {"standingTackle": 12, "topSpeed": 10, "stamina": 10, "crossing": 10, "interceptions": 10,
           "acceleration": 8, "slidingTackle": 8, "marking": 8, "shortPassing": 8, "dribbling": 6, "reactions": 6,
           "agility": 4},
    "CDM": {"interceptions": 14, "shortPassing": 14, "standingTackle": 12, "longPassing": 10, "marking": 8,
            "strength": 8, "stamina": 8, "composure": 8, "reactions": 8, "vision": 6, "aggression": 4},
    "CM": {"shortPassing": 16, "longPassing": 12, "vision": 12, "firstTouch": 10, "dribbling": 8, "stamina": 8,
           "composure": 8, "reactions": 8, "interceptions": 6, "standingTackle": 4, "longShots": 4, "offTheBall": 4},
    "CAM": {"vision": 14, "shortPassing": 14, "firstTouch": 12, "dribbling": 12, "composure": 8, "reactions": 8,
            "longShots": 8, "finishing": 8, "agility": 6, "offTheBall": 6, "acceleration": 4},
    "WM": {"crossing": 14, "dribbling": 12, "acceleration": 10, "topSpeed": 10, "shortPassing": 10, "stamina": 10,
           "firstTouch": 8, "vision": 6, "agility": 6, "reactions": 6, "curl": 4, "offTheBall": 4},
    "W": {"dribbling": 16, "acceleration": 12, "topSpeed": 10, "firstTouch": 10, "finishing": 10, "agility": 8,
          "crossing": 8, "offTheBall": 8, "shortPassing": 6, "reactions": 6, "composure": 6},
    "CF": {"finishing": 14, "firstTouch": 12, "offTheBall": 10, "dribbling": 10, "shortPassing": 10,
           "reactions": 10, "vision": 8, "composure": 8, "longShots": 6, "acceleration": 6, "shotPower": 6},
    "ST": {"finishing": 20, "offTheBall": 12, "reactions": 10, "heading": 8, "shotPower": 8, "composure": 8,
           "firstTouch": 8, "acceleration": 8, "topSpeed": 6, "strength": 6, "dribbling": 6},
}
WEIGHT_FAMILY = dict(FAMILY, CF="CF")


def overall_tenths(attrs, pos):
    w = WEIGHTS[WEIGHT_FAMILY[pos]]
    return (sum(v * attrs[k] for k, v in w.items()) + 5) // 10


def target_tenths(rating):
    """0-99 rating -> overall tenths on the 1-20 scale (the CSV importer's mapping, 1 + v*19/99)."""
    return int(round(10 * (1 + rating * 19 / 99.0)))


def jitter(pid, key):
    h = hashlib.sha256((pid + ":" + key).encode("utf-8")).digest()
    return (h[0] / 255.0 - 0.5) * 1.6  # +-0.8


def make_attributes(pid, positions, rating, traits, age):
    primary = positions[0]
    fam = FAMILY[primary]
    prof = PROFILES[fam]
    base = {}
    for key in ATTRIBUTE_KEYS:
        if key in prof:
            off = prof[key]
        elif fam == "GK":
            off = prof["_default"]
        elif key.startswith("gk"):
            off = OUTFIELD_GK
        else:
            off = OUTFIELD_DEFAULT
        base[key] = off + jitter(pid, key)
    # Secondary positions pull their key attributes up a little.
    for pos in positions[1:]:
        for key in WEIGHTS[WEIGHT_FAMILY[pos]]:
            if not key.startswith("gk"):
                base[key] = max(base[key], -0.5 + jitter(pid, key + pos))
    for t in traits:
        for key, d in TRAITS.get(t, {}).items():
            base[key] += d
    if age >= 31:
        for key in ("acceleration", "topSpeed", "agility"):
            base[key] -= (age - 30) * 0.5
    if fam == "GK":
        for key in ATTRIBUTE_KEYS:
            if not key.startswith("gk") and key not in ("reactions", "composure", "jumping", "strength",
                                                        "longPassing", "shortPassing", "agility", "balance"):
                base[key] = min(base[key], -9.0)

    want = target_tenths(rating)
    level = want / 10.0
    attrs = {}
    for _ in range(12):
        attrs = {k: int(clamp(round(level + base[k]), 1, 20)) for k in ATTRIBUTE_KEYS}
        got = overall_tenths(attrs, primary)
        if got == want:
            break
        level += (want - got) / 10.0
    # Integer rounding can leave the overall a few tenths off; nudge single weighted attributes to land on it.
    weights = WEIGHTS[WEIGHT_FAMILY[primary]]
    for _ in range(60):
        resid = want * 10 - sum(w * attrs[k] for k, w in weights.items())  # in hundredths
        if -5 <= resid < 5:
            break
        step = 1 if resid > 0 else -1
        options = [(w, k) for k, w in weights.items() if 1 <= attrs[k] + step <= 20 and w <= abs(resid) + 5]
        if not options:
            break
        # Largest weight that does not overshoot; ties broken by the attribute furthest from its base shape.
        w, k = max(options, key=lambda o: (o[0], -step * (attrs[o[1]] - level - base[o[1]])))
        attrs[k] += step
    return attrs


def stars(traits, prefix, default):
    for t in traits:
        if t.startswith(prefix) and t[len(prefix):].isdigit():
            return int(clamp(int(t[len(prefix):]), 1, 5))
    return default


def slug(text):
    out, pending = [], False
    for ch in text.lower():
        if ("a" <= ch <= "z") or ("0" <= ch <= "9"):
            if pending and out:
                out.append("-")
            pending = False
            out.append(ch)
        else:
            pending = True
    return "".join(out)


_SPECIAL = str.maketrans({"ø": "o", "Ø": "O", "đ": "d", "Đ": "D", "ł": "l", "Ł": "L", "ß": "ss", "ı": "i",
                          "æ": "ae", "Æ": "AE"})


def ascii_fold(text):
    """"Ødegaard" -> "Odegaard": shirt fonts and ids stay plain ASCII."""
    text = text.translate(_SPECIAL)
    return "".join(c for c in unicodedata.normalize("NFKD", text) if not unicodedata.combining(c))


def parse_squads(path):
    clubs, cur, errors = {}, None, []
    with open(path, encoding="utf-8") as f:
        for ln, raw in enumerate(f, 1):
            line = raw.strip()
            if not line or line.startswith("#"):
                continue
            if line.startswith("@"):
                cur = line[1:].strip()
                if cur not in CLUBS:
                    errors.append("%s:%d unknown club %s" % (path, ln, cur))
                clubs[cur] = []
                continue
            parts = [p.strip() for p in line.split("|")]
            if len(parts) != 12:
                errors.append("%s:%d expected 12 fields, got %d" % (path, ln, len(parts)))
                continue
            clubs[cur].append((ln, parts))
    if errors:
        raise SystemExit("\n".join(errors))
    return clubs


def build_player(club_id, ln, parts):
    num, first, last, known, pos, nat, born, cm, foot, rating, traits, look = parts
    positions = pos.split("/")
    for p in positions:
        if p not in POSITIONS:
            raise SystemExit("line %d: bad position %s" % (ln, p))
    traits = traits.split() if traits and traits != "-" else []
    if last == "-":  # mononym: "Evanilson|-|Evanilson"
        first, last = "", first
    known = known or last
    shirt_name = next((t[3:] for t in traits if t.startswith("sn=")), None) or ascii_fold(known).upper()
    shirt_name = ascii_fold(shirt_name.replace("_", " ")).upper()
    pid = club_id + "-" + slug(ascii_fold(first + " " + last))
    age = SEASON_YEAR - int(born)
    rating = int(rating)
    attrs = make_attributes(pid, positions, rating, traits, age)
    fam = FAMILY[positions[0]]
    skill_default = 1 if fam == "GK" else 2 if fam in ("CB", "CDM") else 3 if rating < 80 or fam in ("FB", "CM") else 4
    look = look.split()
    if len(look) != 5:
        raise SystemExit("line %d: look needs 5 parts (skin hair colour facial build)" % ln)
    skin, hair, hcol, facial, build = look
    if (hair != "?" and hair not in HAIR_STYLES) or (facial != "?" and facial not in FACIAL) \
            or build not in BUILDS or hcol not in HAIR_COLOURS:
        raise SystemExit("line %d: bad look %s" % (ln, " ".join(look)))
    boot = BOOT_COLOURS[hashlib.sha256(pid.encode()).digest()[1] % len(BOOT_COLOURS)]
    appearance = {"skinTone": round((int(skin) - 1) / 8.0, 3)}
    if hair != "?":
        appearance["hairStyle"] = hair
    appearance["hairColor"] = HAIR_COLOURS[hcol]
    if facial != "?":
        appearance["facialHair"] = facial
    appearance.update({"heightCm": int(cm), "build": build, "bootColor": boot})
    return {
        "id": pid,
        "firstName": first,
        "lastName": last,
        "knownAs": known,
        "shirtNumber": int(num),
        "squadNumber": int(num),
        "shirtName": shirt_name,
        "nationality": nat,
        "age": age,
        "heightCm": int(cm),
        "positions": positions,
        "preferredFoot": "left" if foot == "L" else "right",
        "weakFoot": stars(traits, "wf", 3),
        "skillMoves": stars(traits, "sm", skill_default),
        "attributes": attrs,
        "appearance": appearance,
    }


def build(squads_path):
    squads = parse_squads(squads_path)
    clubs = []
    for cid, (name, short, stadium, cap, formation, rep, kits) in CLUBS.items():
        if cid not in squads:
            raise SystemExit("no squad for " + cid)
        k = kits
        players = [build_player(cid, ln, parts) for ln, parts in squads[cid]]
        clubs.append({
            "id": cid, "name": name, "shortName": short,
            "colours": {"primary": k["home"]["primary"], "secondary": k["home"]["shortsColor"],
                        "away": k["away"]["primary"]},
            "stadium": {"name": stadium, "capacity": cap},
            "formation": formation, "reputation": rep,
            "kits": kits,
            "players": players,
        })
    clubs.sort(key=lambda c: c["name"])
    return {
        "format": FORMAT_TAG,
        "schemaVersion": SCHEMA_VERSION,
        "league": {"id": "premier-league", "name": "Premier League", "shortName": "EPL", "country": "England",
                   "season": "2026/27"},
        "provenance": {
            "source": "real-world-pack",
            "fictional": False,
            "generator": "tools/rosters/premier_league/build_premier_league.py",
            "license": "Real names used for development only; licensing to be sorted before release.",
            "notes": "Squads as of the end of the 2026 summer window (best effort; check before release). "
                     "Ratings and attributes are OpenFootball's own estimates on its 1-20 scale. Kits use club "
                     "colours, not licensed designs. Appearance is approximate.",
        },
        "clubs": clubs,
    }


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--squads", default=DEFAULT_SQUADS)
    ap.add_argument("--out", default=DEFAULT_OUT)
    args = ap.parse_args(argv)
    doc = build(args.squads)
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    dump_roster(doc, args.out)
    n = sum(len(c["players"]) for c in doc["clubs"])
    print("wrote %s: %d clubs, %d players" % (args.out, len(doc["clubs"]), n))


if __name__ == "__main__":
    main()
