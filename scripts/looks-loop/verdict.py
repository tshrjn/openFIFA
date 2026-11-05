"""Combine the judges' JSON into verdict.json.

A ballot counts only when it is complete: "better" is A, B or tie, and both A and B carry finite 0-10 scores for
every dimension. Keep = a strict majority of valid ballots prefer the candidate (ties count against), at least two
ballots are valid, and the candidate's mean overall is not below the best's (means over valid ballots only).
"""
import json, math, os, sys

DIMS = ["face", "hair", "skin", "body", "overall"]


def valid_scores(x):
    if not isinstance(x, dict): return False
    for d in DIMS:
        v = x.get(d)
        if isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v) or not 0 <= v <= 10:
            return False
    return True


def decide(ballots, cand):
    """ballots: {judge: parsed JSON or None}; cand: "A" or "B". Returns the verdict dict."""
    best = "B" if cand == "A" else "A"
    v = {"judges": {}, "votes": 0, "answered": 0, "invalid": []}
    cs = {d: [] for d in DIMS}; bs = {d: [] for d in DIMS}
    for j, r in ballots.items():
        if r is None: v["judges"][j] = None; continue
        if not (isinstance(r, dict) and r.get("better") in ("A", "B", "tie") and valid_scores(r.get("A")) and valid_scores(r.get("B"))):
            v["judges"][j] = None; v["invalid"].append(j); continue
        v["answered"] += 1
        vote = 1 if r["better"] == cand else (0 if r["better"] == "tie" else -1)
        v["votes"] += vote > 0
        v["judges"][j] = {"vote": vote, "cand": r[cand], "best": r[best], "confidence": r.get("confidence"),
                          "why": r.get("why"), "fixes": r.get("fixes")}
        for d in DIMS:
            cs[d].append(float(r[cand][d])); bs[d].append(float(r[best][d]))
    mean = lambda x: round(sum(x) / len(x), 2) if x else None
    v["cand_mean"] = {d: mean(cs[d]) for d in DIMS}; v["best_mean"] = {d: mean(bs[d]) for d in DIMS}
    co, bo = v["cand_mean"]["overall"], v["best_mean"]["overall"]
    v["keep"] = v["answered"] >= 2 and v["votes"] * 2 > v["answered"] and co is not None and co >= bo
    return v


if __name__ == "__main__":
    out = sys.argv[1]
    cand = open(f"{out}/order").read().strip().split("=")[1]
    ballots = {}
    for j in ["claude", "gpt", "gemini"]:
        p = f"{out}/{j}.json"
        ballots[j] = json.load(open(p)) if os.path.exists(p) else None
    v = decide(ballots, cand)
    json.dump(v, open(f"{out}/verdict.json", "w"), indent=1)
    print(json.dumps({"keep": v["keep"], "votes": f'{v["votes"]}/{v["answered"]}', "invalid": v["invalid"],
                      "cand": v["cand_mean"], "best": v["best_mean"]}))
