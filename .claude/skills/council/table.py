#!/usr/bin/env python3
"""Summarise a council round and rebuild the PR's "Council review" table.

Usage: table.py <pr-dir> <round-dir>
Writes <round-dir>/summary.txt and <pr-dir>/council-table.md. Exit 0 iff all three reviewers approve this round.
"""
import json
import os
import re
import sys

DELEGATES = [("claude", "Claude Opus 5.5"), ("gpt", "GPT-6.1 Sol"), ("gemini", "Gemini 4 Argon")]
AREAS = ["team_tactics", "team_movement", "player_movement", "animation_skills", "ball_physics", "commentary",
         "kits_likeness", "league_authenticity", "difficulty", "presentation", "code"]


def load(round_dir, key):
    try:
        with open(os.path.join(round_dir, key + ".json")) as f:
            return json.load(f)
    except Exception:
        return None


def score(d):
    o = d.get("overall") if d else None
    return f"{o}/10" if isinstance(o, (int, float)) else "no score"


def main():
    pr_dir, out = sys.argv[1], sys.argv[2]
    rounds = sorted((d for d in os.listdir(pr_dir) if re.fullmatch(r"round-\d+", d)), key=lambda d: int(d[6:]))

    # Summary of this round.
    lines, ok = [], True
    for key, name in DELEGATES:
        d = load(out, key)
        verdict = d.get("verdict", "?") if d else "no output (see %s.err)" % key
        ok = ok and bool(d) and verdict == "approve"
        lines.append(f"{name:16} {score(d):9} {verdict}")
    lines.append("")
    lines.append("ALL APPROVE: " + ("YES" if ok else "no"))
    lines.append("")
    for key, name in DELEGATES:
        d = load(out, key)
        if not d:
            continue
        lines.append(f"## {name}: {d.get('summary', '')}")
        for f in d.get("findings", []):
            scope = "" if f.get("in_scope", True) else " [out of scope -> follow-up]"
            lines.append(f"- {f.get('id')} {f.get('severity')}/{f.get('area')}{scope} {f.get('where', '')}: "
                         f"{f.get('issue', '')} -> {f.get('fix', '')}")
        if d.get("resolved"):
            lines.append("- resolved: " + ", ".join(map(str, d["resolved"])))
        lines.append("")
    with open(os.path.join(out, "summary.txt"), "w") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))

    # PR description section: overall per delegate per round, then the last round's area scores.
    hdr = "| Delegate | " + " | ".join(f"Round {r[6:]}" for r in rounds) + " |"
    md = ["## Council review", "", f"Rounds: {len(rounds)}", "", hdr,
          "| --- |" + " --- |" * len(rounds)]
    for key, name in DELEGATES:
        md.append(f"| {name} | " + " | ".join(score(load(os.path.join(pr_dir, r), key)) for r in rounds) + " |")
    last = [load(os.path.join(pr_dir, rounds[-1]), k) for k, _ in DELEGATES]
    used = [a for a in AREAS if any(d and isinstance(d.get("areas", {}).get(a), (int, float)) for d in last)]
    if used:
        md += ["", f"Area scores, round {rounds[-1][6:]}:", "",
               "| Area | " + " | ".join(n for _, n in DELEGATES) + " |", "| --- |" + " --- |" * len(DELEGATES)]
        for a in used:
            cells = []
            for d in last:
                v = d.get("areas", {}).get(a) if d else None
                cells.append(f"{v}/10" if isinstance(v, (int, float)) else "–")
            md.append(f"| {a} | " + " | ".join(cells) + " |")
    with open(os.path.join(pr_dir, "council-table.md"), "w") as f:
        f.write("\n".join(md) + "\n")
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
