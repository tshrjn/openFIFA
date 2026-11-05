"""Combine the judges' JSON into verdict.json. Keep = majority of answering judges prefer the candidate
(ties count against), and the candidate's mean overall is not below the best's."""
import json, os, sys
out = sys.argv[1]
cand = open(f"{out}/order").read().strip().split("=")[1]; best = "B" if cand == "A" else "A"
dims = ["face", "hair", "skin", "body", "overall"]
v = {"judges": {}, "votes": 0, "answered": 0}
cs = {d: [] for d in dims}; bs = {d: [] for d in dims}
for j in ["claude", "gpt", "gemini"]:
    p = f"{out}/{j}.json"
    if not os.path.exists(p): v["judges"][j] = None; continue
    r = json.load(open(p)); v["answered"] += 1
    pref = r.get("better"); vote = 1 if pref == cand else (0 if pref == "tie" else -1)
    v["votes"] += vote > 0
    v["judges"][j] = {"vote": vote, "cand": r.get(cand), "best": r.get(best), "confidence": r.get("confidence"),
                      "why": r.get("why"), "fixes": r.get("fixes")}
    for d in dims:
        try: cs[d].append(float(r[cand][d])); bs[d].append(float(r[best][d]))
        except Exception: pass
mean = lambda x: round(sum(x) / len(x), 2) if x else None
v["cand_mean"] = {d: mean(cs[d]) for d in dims}; v["best_mean"] = {d: mean(bs[d]) for d in dims}
co, bo = v["cand_mean"]["overall"], v["best_mean"]["overall"]
v["keep"] = v["answered"] >= 2 and v["votes"] * 2 > v["answered"] and co is not None and bo is not None and co >= bo
json.dump(v, open(f"{out}/verdict.json", "w"), indent=1)
print(json.dumps({"keep": v["keep"], "votes": f'{v["votes"]}/{v["answered"]}', "cand": v["cand_mean"], "best": v["best_mean"]}))
