#!/usr/bin/env python3
"""Mixes a commentary cue log (tools/commentary/demo/CommentaryDemo.cs) into a listenable demo.

    mix_demo.py cues.jsonl <audio/en folder> <out prefix>   -> <out>.ogg, <out>.txt (transcript with times)

The crowd bed is synthesised filtered noise following the cue log's crowd level (a stand-in for the game's crowd
audio), so the demo shows how the voices sit over a stadium. Commentary is centred, the bed is wide.
"""
import json
import os
import sys

import numpy as np
import soundfile as sf

SR = 24000
GAP = 0.06
FADE = 0.12


def load(folder, voice, cid, cache={}):
    key = (voice, cid)
    if key not in cache:
        p = os.path.join(folder, voice, cid + ".ogg")
        cache[key] = sf.read(p, dtype="float32")[0] if os.path.exists(p) else None
    return cache[key]


def crowd_bed(levels, n, seed=3):
    rng = np.random.default_rng(seed)
    out = np.zeros((n, 2), np.float32)
    f = np.fft.rfftfreq(n, 1 / SR)
    # Murmur of many voices: pink-ish noise band-limited to roughly 150 Hz - 4 kHz.
    shape = 1 / np.sqrt(np.maximum(f, 20)) * (f > 150) * np.exp(-((f / 4000) ** 2))
    for ch in range(2):
        w = rng.standard_normal(n)
        out[:, ch] = np.fft.irfft(np.fft.rfft(w) * shape, n).astype(np.float32)
    out /= np.abs(out).max() + 1e-9
    lvl = np.interp(np.arange(n), levels[:, 0] * SR, levels[:, 1]).astype(np.float32)
    gain = 0.04 + 0.30 * lvl ** 1.6
    return out * gain[:, None]


def main(cues_path, folder, out_prefix):
    rows = [json.loads(l) for l in open(cues_path, encoding="utf-8") if l.strip()]
    head, rows = rows[0], rows[1:]
    end_t = max(r["t"] for r in rows) + 8
    n = int(end_t * SR)
    voice_track = {"lead": np.zeros(n, np.float32), "analyst": np.zeros(n, np.float32)}
    active = {}  # voice -> (start sample, end sample) of the current utterance
    transcript = ["%s (AI v AI, %ss halves)" % (head["match"], head["half"]), ""]
    levels = [(0.0, 0.25)]
    for r in rows:
        t = r["t"]
        if "crowd" in r:
            levels.append((t, r["crowd"]))
        if "event" in r:
            if r["event"] in ("Goal", "HalfTime", "FullTime"):
                transcript.append("%6.1fs  -- %s %s --" % (t, r["event"], r["score"]))
            continue
        v = r["voice"]
        s0 = int(t * SR)
        if r["cue"] == "Stop" and v in active:
            a, b = active.pop(v)
            f = int(FADE * SR)
            seg = voice_track[v][s0:min(b, s0 + f)]
            seg *= np.linspace(1, 0, len(seg), dtype=np.float32)
            voice_track[v][s0 + len(seg):b] = 0
            continue
        if r["cue"] != "Start":
            continue
        pos = s0 + int(0.03 * SR)
        for cid in r["clips"]:
            x = load(folder, v, cid)
            if x is None:
                continue
            e = min(n, pos + len(x))
            voice_track[v][pos:e] += x[:e - pos]
            pos = e + int(GAP * SR)
        active[v] = (s0, pos)
        mm, ss = divmod(t, 60)
        transcript.append("%2d:%04.1f  %-8s %s" % (mm, ss, "LEAD" if v == "lead" else "CO-COMM", r["text"]))
    levels.append((end_t, 0.25))
    bed = crowd_bed(np.array(levels, np.float32), n)
    speech = voice_track["lead"] + voice_track["analyst"] * 0.95
    # Duck the crowd a little under speech.
    env = np.convolve(np.abs(speech), np.ones(2400) / 2400, mode="same")
    duck = 1 - 0.45 * np.clip(env * 12, 0, 1)
    mix = bed * duck[:, None] + speech[:, None] * 0.9
    peak = np.abs(mix).max()
    if peak > 0.98:
        mix *= 0.98 / peak
    # libsndfile's Vorbis encoder can crash on one huge write: stream it in blocks.
    with sf.SoundFile(out_prefix + ".ogg", "w", SR, 2, format="OGG", subtype="VORBIS") as f:
        for i in range(0, len(mix), SR):
            f.write(np.ascontiguousarray(mix[i:i + SR]))
    with open(out_prefix + ".txt", "w", encoding="utf-8") as f:
        f.write("\n".join(transcript) + "\n")
    print("wrote %s.ogg (%.0f s) and %s.txt (%d lines spoken)" % (out_prefix, end_t, out_prefix,
          sum(1 for r in rows if r.get("cue") == "Start")))


if __name__ == "__main__":
    main(*sys.argv[1:4])
