#!/usr/bin/env python3
"""Mixes commentary onto a capture-gameplay.sh recording and muxes an MP4 with sound.

    mix_capture.py <capture dir> [ffmpeg]   -> <capture dir>/gameplay_audio.mp4, commentary_mix.wav

Reads <dir>/commentary_cues.jsonl (written by SinglePlayerCapture: video time, voice, clip ids), the rendered clips
in OpenFifa/Assets/StreamingAssets/Commentary/audio/en (tools/commentary/fetch_clips.sh), and <dir>/frames/frame_*.png.
The crowd bed is synthesised filtered noise (a stand-in for the game's crowd audio), louder around goals.
Needs numpy + soundfile (e.g. the TTS venv) and an ffmpeg with libx264/aac (e.g. imageio-ffmpeg's binary).
"""
import glob
import json
import os
import subprocess
import sys

import numpy as np
import soundfile as sf

SR = 24000
GAP = 0.06
FADE = 0.12
FPS = 30
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CLIPS = os.path.join(ROOT, "OpenFifa/Assets/StreamingAssets/Commentary/audio/en")


def load(voice, cid, cache={}):
    key = (voice, cid)
    if key not in cache:
        x = None
        for v in (voice, "lead", "analyst"):
            p = os.path.join(CLIPS, v, cid + ".ogg")
            if os.path.exists(p):
                x = sf.read(p, dtype="float32")[0]
                if x.ndim > 1:
                    x = x.mean(axis=1)
                break
        cache[key] = x
    return cache[key]


def crowd_bed(n, goal_times, seed=3):
    rng = np.random.default_rng(seed)
    out = np.zeros((n, 2), np.float32)
    f = np.fft.rfftfreq(n, 1 / SR)
    shape = 1 / np.sqrt(np.maximum(f, 20)) * (f > 150) * np.exp(-((f / 4000) ** 2))
    for ch in range(2):
        out[:, ch] = np.fft.irfft(np.fft.rfft(rng.standard_normal(n)) * shape, n).astype(np.float32)
    out /= np.abs(out).max() + 1e-9
    t = np.arange(n) / SR
    lvl = np.full(n, 0.25, np.float32)
    for g in goal_times:
        lvl = np.maximum(lvl, (0.25 + 0.75 * np.exp(-np.maximum(t - g, 0) / 4.0) * (t >= g - 0.3)).astype(np.float32))
    return out * (0.04 + 0.30 * lvl ** 1.6)[:, None]


def main(cap, ffmpeg="ffmpeg"):
    frames = sorted(glob.glob(os.path.join(cap, "frames", "frame_*.png")))
    if not frames:
        sys.exit("no frames in " + cap)
    n = int(len(frames) / FPS * SR)
    rows = [json.loads(l) for l in open(os.path.join(cap, "commentary_cues.jsonl"), encoding="utf-8") if l.strip()]
    track = {"lead": np.zeros(n, np.float32), "analyst": np.zeros(n, np.float32)}
    active, goals = {}, []
    # Broadcast pacing guard: one voice at a time. A routine line (lull, on-the-ball call, analyst colour) is dropped
    # while anyone is still talking; a big moment (goal, shot, save, foul, card, offside, penalty) cuts in.
    BIG = ("line.goal", "line.shot", "line.save", "line.foul", "line.card", "line.offside", "line.penalty",
           "line.analyst.goal", "line.half_time", "line.full_time", "line.kickoff")
    busy_until, kept, dropped = 0, [], 0
    for r in rows:
        if r["cue"] != "Start":
            kept.append(r)
            continue
        big = any(c.startswith(BIG) for c in r["clips"])
        if r["t"] < busy_until and not big:
            dropped += 1
            continue
        dur = sum(len(load(r["voice"], c)) / SR + GAP for c in r["clips"] if load(r["voice"], c) is not None)
        busy_until = r["t"] + dur + 0.35
        kept.append(r)
    rows = kept
    for r in rows:
        v, s0 = r["voice"], int(r["t"] * SR)
        if r["cue"] == "Stop" and v in active:
            _, b = active.pop(v)
            f = int(FADE * SR)
            seg = track[v][s0:min(b, s0 + f)]
            seg *= np.linspace(1, 0, len(seg), dtype=np.float32)
            track[v][s0 + len(seg):b] = 0
            continue
        if r["cue"] != "Start" or s0 >= n:
            continue
        for ov in ("lead", "analyst"):
            if ov in active and active[ov][1] > s0:
                _, b = active.pop(ov)
                f = int(FADE * SR)
                seg = track[ov][s0:min(b, s0 + f)]
                seg *= np.linspace(1, 0, len(seg), dtype=np.float32)
                track[ov][s0 + len(seg):b] = 0
        if any(c.startswith("line.goal") for c in r["clips"]):
            goals.append(r["t"])
        pos = s0 + int(0.03 * SR)
        for cid in r["clips"]:
            x = load(v, cid)
            if x is None or pos >= n:
                continue
            e = min(n, pos + len(x))
            track[v][pos:e] += x[:e - pos]
            pos = e + int(GAP * SR)
        active[v] = (s0, pos)
    speech = track["lead"] + track["analyst"] * 0.95
    env = np.convolve(np.abs(speech), np.ones(2400) / 2400, mode="same")
    mix = crowd_bed(n, goals) * (1 - 0.45 * np.clip(env * 12, 0, 1))[:, None] + speech[:, None] * 0.9
    peak = np.abs(mix).max()
    if peak > 0.98:
        mix *= 0.98 / peak
    wav = os.path.join(cap, "commentary_mix.wav")
    sf.write(wav, mix, SR)
    out = os.path.join(cap, "gameplay_audio.mp4")
    subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-framerate", str(FPS),
                    "-i", os.path.join(cap, "frames", "frame_%05d.png"), "-i", wav,
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20", "-preset", "medium",
                    "-c:a", "aac", "-b:a", "160k", "-shortest", "-movflags", "+faststart", out], check=True)
    print("wrote %s (%d frames, %d cues kept, %d routine lines dropped for pacing, %d goal calls)"
          % (out, len(frames), len(rows), dropped, len(goals)))


if __name__ == "__main__":
    main(*sys.argv[1:3])
