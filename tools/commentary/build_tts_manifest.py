#!/usr/bin/env python3
"""Builds the offline TTS pre-render manifest for OpenFifa commentary. Does NOT call any TTS service.

    python3 tools/commentary/build_tts_manifest.py \
        [--lines OpenFifa/Assets/StreamingAssets/Commentary/lines.en.json] \
        [--roster OpenFifa/Assets/StreamingAssets/Rosters/default_league.json ...] \
        [--out build/commentary/tts_manifest.en.json] [--csv build/commentary/tts_manifest.en.csv]

Every clip the engine can request is listed once, with the id the runtime uses as its audio file name:
  line.<lineId>                     one per bank line, in the category's voice and tone
  name.<slug>.<neutral|excited|question>  every roster KnownAs, lead voice only
  team.<slug>.<neutral|excited|question>  every club name, both voices
Clip ids, the {name} split and the slug rule mirror CommentaryLineBank.cs / CommentaryClip.cs / RosterSchema.Slug.
The output is deterministic (sorted, no timestamps). A renderer (ElevenLabs, Kokoro...) can consume it later;
use stock or licensed synthetic voices only, never a clone of a real commentator.
"""

import argparse
import csv
import json
import os
import sys

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DEFAULT_LINES = os.path.join(REPO_ROOT, "OpenFifa", "Assets", "StreamingAssets", "Commentary", "lines.en.json")
DEFAULT_ROSTER = os.path.join(REPO_ROOT, "OpenFifa", "Assets", "StreamingAssets", "Rosters", "default_league.json")
INTONATIONS = ["neutral", "excited", "question"]
NAME_TOKEN = "{name}"
TEAM_TOKEN = "{team}"
TTS_PUNCT = {"neutral": ".", "excited": "!", "question": "?"}
VOICE_IDS = {"pbp": "lead", "analyst": "analyst"}


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


def split_name(raw):
    """Returns (text, placement, kind): placement none/before/after, kind player/team. Text is "" for a bare name
    call. Mirrors CommentaryLineBank.SplitName."""
    name_at, team_at = raw.find(NAME_TOKEN), raw.find(TEAM_TOKEN)
    if name_at >= 0 and team_at >= 0:
        raise ValueError("use either {name} or {team}: %r" % raw)
    token, first, kind = NAME_TOKEN, name_at, "player"
    if team_at >= 0:
        token, first, kind = TEAM_TOKEN, team_at, "team"
    if first < 0:
        return raw, "none", None
    if raw.find(token, first + 1) >= 0:
        raise ValueError("%s appears twice: %r" % (token, raw))
    if first == 0:
        text = raw[len(token):].lstrip(" ,.!?;:-")
        if all(ch in ".!?, " for ch in text):
            if kind == "team":
                raise ValueError("a bare call must be {name}: %r" % raw)
            text = ""
        return text, "before", kind
    tail = raw[first + len(token):]
    if any(ch not in ".!? " for ch in tail):
        raise ValueError("%s must be at the start or end: %r" % (token, raw))
    text = raw[:first].rstrip(" ,")
    if not text:
        raise ValueError("line has no text besides %s: %r" % (token, raw))
    return text, "after", kind


def line_clips(bank):
    clips = []
    for cat in bank["categories"]:
        voice = cat.get("voice", "pbp")
        cat_tone = cat.get("tone", "neutral")
        cat_name_tone = cat.get("nameTone", "neutral")
        for i, entry in enumerate(cat["lines"]):
            if isinstance(entry, str):
                raw, lid, tone, name_tone = entry, "%s.%02d" % (cat["id"], i + 1), cat_tone, cat_name_tone
            else:
                raw = entry["text"]
                lid = entry.get("id", "%s.%02d" % (cat["id"], i + 1))
                tone = entry.get("tone", cat_tone)
                name_tone = entry.get("nameTone", cat_name_tone)
            text, placement, kind = split_name(raw.strip())
            if not text:
                continue  # bare name call: only the name clip plays
            clips.append({
                "clipId": "line." + lid,
                "kind": "line",
                "voice": VOICE_IDS[voice],
                "intonation": tone,
                "text": text,
                "category": cat["id"],
                "event": cat["event"],
                "namePlacement": placement,
                "nameKind": kind,
                "nameIntonation": name_tone if placement != "none" else None,
            })
    return clips


def name_clips(roster_paths):
    names = set()
    for path in roster_paths:
        with open(path, encoding="utf-8") as f:
            doc = json.load(f)
        for club in doc["clubs"]:
            for p in club["players"]:
                names.add(p.get("knownAs") or p["lastName"])
    by_slug = {}
    for n in sorted(names):
        s = slug(n)
        if s in by_slug and by_slug[s] != n:
            sys.stderr.write("warning: %r and %r share clip slug %s\n" % (by_slug[s], n, s))
        by_slug.setdefault(s, n)
    clips = []
    for s in sorted(by_slug):
        for tone in INTONATIONS:
            clips.append({
                "clipId": "name.%s.%s" % (s, tone),
                "kind": "name",
                "voice": VOICE_IDS["pbp"],
                "intonation": tone,
                "text": by_slug[s],
                "ttsText": by_slug[s] + TTS_PUNCT[tone],
            })
    return clips


def team_clips(roster_paths):
    names = set()
    for path in roster_paths:
        with open(path, encoding="utf-8") as f:
            doc = json.load(f)
        for club in doc["clubs"]:
            names.add(club.get("spokenName") or club["name"])
    clips = []
    for n in sorted(names, key=slug):
        for voice in ("lead", "analyst"):
            for tone in INTONATIONS:
                clips.append({
                    "clipId": "team.%s.%s" % (slug(n), tone),
                    "kind": "team",
                    "voice": voice,
                    "intonation": tone,
                    "text": n,
                    "ttsText": n + TTS_PUNCT[tone],
                })
    return clips


def build(lines_path, roster_paths):
    with open(lines_path, encoding="utf-8") as f:
        bank = json.load(f)
    clips = line_clips(bank) + name_clips(roster_paths) + team_clips(roster_paths)
    for c in clips:
        c.setdefault("ttsText", c["text"])
    ids = [c["voice"] + "/" + c["clipId"] for c in clips]
    seen, dupes = set(), set()
    for i in ids:
        (dupes if i in seen else seen).add(i)
    dupes = sorted(dupes)
    if dupes:
        raise ValueError("duplicate clip ids: %s" % ", ".join(dupes))
    clips.sort(key=lambda c: (c["voice"], c["clipId"]))
    total_chars = sum(len(c["ttsText"]) for c in clips)
    return {
        "format": "openfifa-tts-manifest",
        "schemaVersion": 1,
        "language": bank.get("language", "en"),
        "voices": {"lead": "play-by-play (stock/licensed synthetic voice)",
                   "analyst": "co-commentator (stock/licensed synthetic voice)"},
        "audioFormat": {"container": "ogg", "sampleRate": 24000, "channels": 1,
                        "fileName": "<clipId>.ogg", "folder": "StreamingAssets/Commentary/audio/<language>/<voice>/"},
        "stats": {"clips": len(clips),
                  "lineClips": sum(1 for c in clips if c["kind"] == "line"),
                  "nameClips": sum(1 for c in clips if c["kind"] == "name"),
                  "teamClips": sum(1 for c in clips if c["kind"] == "team"),
                  "characters": total_chars},
        "clips": clips,
    }


def main(argv=None):
    ap = argparse.ArgumentParser(description="Build the commentary TTS pre-render manifest (no API calls).")
    ap.add_argument("--lines", default=DEFAULT_LINES)
    ap.add_argument("--roster", action="append", help="roster JSON (repeatable); default: bundled league")
    ap.add_argument("--out", default=os.path.join(REPO_ROOT, "build", "commentary", "tts_manifest.en.json"))
    ap.add_argument("--csv", help="also write a CSV (clipId, voice, intonation, ttsText)")
    a = ap.parse_args(argv)
    manifest = build(a.lines, a.roster or [DEFAULT_ROSTER])
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(manifest, f, indent=1, ensure_ascii=False)
        f.write("\n")
    if a.csv:
        with open(a.csv, "w", encoding="utf-8", newline="") as f:
            w = csv.writer(f, lineterminator="\n")
            w.writerow(["clipId", "kind", "voice", "intonation", "ttsText"])
            for c in manifest["clips"]:
                w.writerow([c["clipId"], c["kind"], c["voice"], c["intonation"], c["ttsText"]])
    s = manifest["stats"]
    print("wrote %s: %d clips (%d lines, %d names), %d characters"
          % (a.out, s["clips"], s["lineClips"], s["nameClips"], s["characters"]))


if __name__ == "__main__":
    main()
