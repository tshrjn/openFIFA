#!/usr/bin/env python3
"""Renders the commentary TTS manifest to audio with Kokoro-82M (Apache-2.0 open-weight TTS, stock voices).

    # one-time setup (outside the repo):
    python3 -m venv ~/.tts/venv && ~/.tts/venv/bin/pip install kokoro-onnx soundfile
    curl -LO .../kokoro-v1.0.onnx ; curl -LO .../voices-v1.0.bin   (github.com/thewh1teagle/kokoro-onnx releases)
    # render (incremental: unchanged clips are skipped):
    ~/.tts/venv/bin/python tools/commentary/render_tts.py --model-dir ~/.tts --jobs 6

Voices are blends of Kokoro's stock British voices, so neither commentator is a copy of any one stock voice, and
neither is modelled on a real broadcaster. Do not swap in a cloned voice of a real commentator.

Output: OpenFifa/Assets/StreamingAssets/Commentary/audio/<lang>/<voice>/<clipId>.ogg plus index.json
({voice: {clipId: [seconds, hash]}}), which the game reads to know which clips exist and how long they are.
"""

import argparse
import hashlib
import json
import os
import subprocess
import sys
from concurrent.futures import ProcessPoolExecutor

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
HERE = os.path.dirname(os.path.abspath(__file__))
SR = 24000

# Delivery per voice. "blend" weights mix Kokoro stock styles into an original voice.
VOICES = {
    "lead": {"blend": {"bm_george": 0.65, "bm_fable": 0.35}, "speed": 1.08},
    "analyst": {"blend": {"bm_lewis": 0.55, "bm_daniel": 0.45}, "speed": 1.0},
}
# Intonation: tempo factor, pitch factor (resample trick), gain in dB.
TONES = {
    "neutral": (1.0, 1.0, 0.0),
    "excited": (1.12, 1.07, 2.5),
    "question": (1.0, 1.02, 0.0),
}
RENDER_VERSION = 2  # bump when the processing chain changes, to force a re-render

_kokoro = None


def clip_hash(clip):
    v = VOICES[clip["voice"]]
    key = json.dumps([RENDER_VERSION, clip["ttsText"], clip["intonation"], v, TONES[clip["intonation"]]], sort_keys=True)
    return hashlib.sha1(key.encode("utf-8")).hexdigest()[:12]


def _init(model_dir, threads):
    global _kokoro
    os.environ["OMP_NUM_THREADS"] = str(threads)
    import onnxruntime as ort
    from kokoro_onnx import Kokoro
    opts = ort.SessionOptions()
    opts.intra_op_num_threads = threads
    opts.inter_op_num_threads = 1
    sess = ort.InferenceSession(os.path.join(model_dir, "kokoro-v1.0.onnx"), opts, providers=["CPUExecutionProvider"])
    _kokoro = Kokoro.from_session(sess, os.path.join(model_dir, "voices-v1.0.bin"))


def _style(voice):
    import numpy as np
    blend = VOICES[voice]["blend"]
    total = sum(blend.values())
    return sum(_kokoro.get_voice_style(n) * (w / total) for n, w in blend.items()).astype(np.float32)


def process(audio, pitch, gain_db):
    """Pitch shift by resampling (tempo was pre-compensated), trim, high-pass, level, fade."""
    import numpy as np
    x = np.asarray(audio, dtype=np.float64)
    if pitch != 1.0:
        n = int(round(len(x) / pitch))
        x = np.interp(np.linspace(0, len(x) - 1, n), np.arange(len(x)), x)
    # Trim leading/trailing silence (keep 40 ms of air before, 80 ms after).
    env = np.abs(x)
    thr = max(1e-4, env.max() * 0.02)
    idx = np.where(env > thr)[0]
    if len(idx):
        a = max(0, idx[0] - int(0.04 * SR))
        b = min(len(x), idx[-1] + int(0.08 * SR))
        x = x[a:b]
    # One-pole high-pass at ~90 Hz: broadcast mics roll off the rumble.
    alpha = 1.0 / (1.0 + 2 * np.pi * 90 / SR)
    y = np.empty_like(x)
    prev_x = prev_y = 0.0
    for i in range(len(x)):  # short clips; plain loop is fine
        prev_y = alpha * (prev_y + x[i] - prev_x)
        prev_x = x[i]
        y[i] = prev_y
    # Level: RMS of the voiced part to -18 dBFS, plus tone gain, peak-limited to -1 dBFS.
    voiced = y[np.abs(y) > np.abs(y).max() * 0.05] if len(y) else y
    rms = np.sqrt(np.mean(voiced ** 2)) if len(voiced) else 1.0
    y *= (10 ** ((-18 + gain_db) / 20)) / max(rms, 1e-6)
    peak = np.abs(y).max() if len(y) else 0
    lim = 10 ** (-1 / 20)
    if peak > lim:
        y = np.tanh(y / lim * 0.9) * lim / np.tanh(0.9)
    f = min(len(y) // 4, int(0.01 * SR))
    if f > 0:
        y[:f] *= np.linspace(0, 1, f)
        y[-f:] *= np.linspace(1, 0, f)
    return y.astype(np.float32)


def render_one(args):
    clip, out_path = args
    import soundfile as sf
    tempo, pitch, gain = TONES[clip["intonation"]]
    speed = VOICES[clip["voice"]]["speed"] * tempo / pitch  # pitch-up by resampling also speeds up
    audio, sr = _kokoro.create(clip["ttsText"], voice=_style(clip["voice"]), speed=speed, lang="en-gb")
    assert sr == SR, sr
    y = process(audio, pitch, gain)
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    sf.write(out_path, y, SR, format="OGG", subtype="VORBIS")
    return clip["voice"], clip["clipId"], round(len(y) / SR, 3)


def index_text(doc):
    """One clip per line, so re-renders diff cleanly."""
    head = {k: v for k, v in doc.items() if k != "voices"}
    out = [json.dumps(head, ensure_ascii=False)[:-1] + ', "voices": {']
    voices = list(doc["voices"].items())
    for vi, (voice, clips) in enumerate(voices):
        out.append('  "%s": {' % voice)
        items = list(clips.items())
        for i, (cid, v) in enumerate(items):
            out.append('    "%s": [%s, "%s"]%s' % (cid, v[0], v[1], "," if i < len(items) - 1 else ""))
        out.append("  }" + ("," if vi < len(voices) - 1 else ""))
    out.append("}}")
    return "\n".join(out) + "\n"


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--model-dir", default=os.path.expanduser("~/GameDev/.tts"))
    ap.add_argument("--lines", default=None, help="line bank (default: the bundled en bank)")
    ap.add_argument("--roster", action="append")
    ap.add_argument("--out", default=os.path.join(REPO_ROOT, "OpenFifa", "Assets", "StreamingAssets", "Commentary", "audio", "en"))
    ap.add_argument("--jobs", type=int, default=max(1, (os.cpu_count() or 2) // 2))
    ap.add_argument("--threads", type=int, default=2, help="onnx threads per job")
    ap.add_argument("--only", help="render only clip ids containing this text (auditions)")
    a = ap.parse_args(argv)

    sys.path.insert(0, HERE)
    import build_tts_manifest as m
    manifest = m.build(a.lines or m.DEFAULT_LINES, a.roster or [m.DEFAULT_ROSTER])
    clips = [c for c in manifest["clips"] if not a.only or a.only in c["clipId"]]

    index_path = os.path.join(a.out, "index.json")
    old = {}
    if os.path.exists(index_path):
        with open(index_path, encoding="utf-8") as f:
            old = json.load(f).get("voices", {})
    index = {"lead": {}, "analyst": {}}
    todo = []
    for c in clips:
        h = clip_hash(c)
        path = os.path.join(a.out, c["voice"], c["clipId"] + ".ogg")
        prev = old.get(c["voice"], {}).get(c["clipId"])
        if prev and prev[1] == h and os.path.exists(path):
            index[c["voice"]][c["clipId"]] = prev
        else:
            todo.append((c, path, h))
    print("%d clips, %d to render with %d jobs" % (len(clips), len(todo), a.jobs), flush=True)

    hashes = {(c["voice"], c["clipId"]): h for c, _, h in todo}
    done = 0
    with ProcessPoolExecutor(a.jobs, initializer=_init, initargs=(a.model_dir, a.threads)) as ex:
        for voice, cid, dur in ex.map(render_one, [(c, p) for c, p, _ in todo], chunksize=4):
            index[voice][cid] = [dur, hashes[(voice, cid)]]
            done += 1
            if done % 100 == 0:
                print("  %d/%d" % (done, len(todo)), flush=True)

    if not a.only:  # drop clips that left the manifest
        keep = {(c["voice"], c["clipId"]) for c in clips}
        for voice in ("lead", "analyst"):
            folder = os.path.join(a.out, voice)
            if os.path.isdir(folder):
                for fn in os.listdir(folder):
                    if fn.endswith(".ogg") and (voice, fn[:-4]) not in keep:
                        os.remove(os.path.join(folder, fn))
    else:
        for voice in index:
            for cid, v in old.get(voice, {}).items():
                index[voice].setdefault(cid, v)

    doc = {"format": "openfootball-commentary-audio", "schemaVersion": 1, "language": manifest["language"],
           "sampleRate": SR, "engine": "kokoro-82m v1.0 (Apache-2.0), stock voice blends",
           "voices": {v: dict(sorted(index[v].items())) for v in ("lead", "analyst")}}
    os.makedirs(a.out, exist_ok=True)
    with open(index_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(index_text(doc))
    print("wrote %s" % index_path)


if __name__ == "__main__":
    main()
