#!/usr/bin/env python3
"""Ships the rendered commentary clips outside git, as a versioned GitHub Release asset.

Only StreamingAssets/Commentary/audio/<lang>/index.json is committed: clip id -> [seconds, renderHash, sha256], plus
"clipsRelease", the release tag holding exactly those files. The engine chooses lines from the index alone, so
commentary (and capture commentary.txt) is the same whether or not the .ogg files are present.

    clips.py pack  --tag commentary-audio-v2 [--publish]   # after render_tts.py: tar.zst + set clipsRelease (+ gh release)
    clips.py fetch                                          # build machines: download, verify sha256, unpack
    clips.py verify                                         # check local files against the index

Needs tar and zstd on PATH; pack --publish and fetch use the gh CLI when available, else plain HTTPS (public repo).
"""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import urllib.request

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
AUDIO = os.path.join(REPO_ROOT, "OpenFifa", "Assets", "StreamingAssets", "Commentary", "audio")
REPO = os.environ.get("OPENFOOTBALL_REPO", "tshrjn/openFIFA")


def asset_name(lang, tag):
    return "commentary-clips-%s-%s.tar.zst" % (lang, tag)


def load_index(lang):
    with open(os.path.join(AUDIO, lang, "index.json"), encoding="utf-8") as f:
        return json.load(f)


def expected(doc):
    for voice, clips in doc["voices"].items():
        for cid, v in clips.items():
            yield os.path.join(voice, cid + ".ogg"), (v[2] if len(v) > 2 else None)


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def verify(lang, root=None):
    root = root or os.path.join(AUDIO, lang)
    bad = []
    for rel, digest in expected(load_index(lang)):
        p = os.path.join(root, rel)
        if not os.path.exists(p):
            bad.append(rel + " missing")
        elif digest is None:
            bad.append(rel + " has no sha256 in the index (re-run render_tts.py)")
        elif sha256(p) != digest:
            bad.append(rel + " hash mismatch")
    return bad


def rewrite_release(lang, tag):
    path = os.path.join(AUDIO, lang, "index.json")
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import render_tts
    doc = load_index(lang)
    doc["clipsRelease"] = tag
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(render_tts.index_text(doc))


def pack(a):
    bad = verify(a.lang)
    if bad:
        sys.exit("local clips do not match the index:\n  " + "\n  ".join(bad[:20]))
    out = os.path.join(REPO_ROOT, "build", "commentary", asset_name(a.lang, a.tag))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    files = sorted(rel for rel, _ in expected(load_index(a.lang)))
    with tempfile.NamedTemporaryFile("w", delete=False) as lst:
        lst.write("\n".join(files) + "\n")
    subprocess.check_call(["tar", "--sort=name", "--mtime=2026-01-01", "--owner=0", "--group=0", "--numeric-owner",
                           "-C", os.path.join(AUDIO, a.lang), "-I", "zstd -19 -T0", "-cf", out, "-T", lst.name])
    os.unlink(lst.name)
    rewrite_release(a.lang, a.tag)
    print("packed %d clips -> %s (%.1f MB); index clipsRelease = %s" % (len(files), out, os.path.getsize(out) / 1e6, a.tag))
    if a.publish:
        notes = ("Rendered commentary clips (%s) for audio/%s/index.json. Kokoro-82M (Apache-2.0) stock-voice blends; "
                 "AI-generated voices, see THIRD_PARTY_NOTICES.md. Install with tools/commentary/fetch_clips.sh." % (len(files), a.lang))
        subprocess.check_call(["gh", "release", "create", a.tag, out, "--repo", REPO, "--title", "Commentary clips " + a.tag,
                               "--notes", notes, "--prerelease"])
        print("published release %s" % a.tag)


def fetch(a):
    doc = load_index(a.lang)
    tag = doc.get("clipsRelease")
    if not tag:
        sys.exit("index.json has no clipsRelease: the clips were re-rendered and not packed yet (clips.py pack)")
    if not verify(a.lang):
        print("commentary clips already present and verified (%s)" % tag)
        return
    name = asset_name(a.lang, tag)
    tmp = tempfile.mkdtemp()
    dest = os.path.join(tmp, name)
    local = os.environ.get("OPENFOOTBALL_CLIPS_FILE")  # offline / testing: a tarball already on disk
    if local:
        shutil.copy(local, dest)
        rc = 0
    elif shutil.which("gh") and not a.no_gh:
        rc = subprocess.call(["gh", "release", "download", tag, "--repo", REPO, "--pattern", name, "--dir", tmp])
    else:
        rc = 1
    if rc != 0:
        url = "https://github.com/%s/releases/download/%s/%s" % (REPO, tag, name)
        print("downloading " + url)
        urllib.request.urlretrieve(url, dest)
    stage = os.path.join(tmp, "x")
    os.makedirs(stage)
    subprocess.check_call(["tar", "-C", stage, "-I", "zstd -d", "-xf", dest])
    bad = verify(a.lang, stage)
    if bad:
        sys.exit("downloaded clips do not match index.json:\n  " + "\n  ".join(bad[:20]))
    for voice in ("lead", "analyst"):
        target = os.path.join(AUDIO, a.lang, voice)
        shutil.rmtree(target, ignore_errors=True)
        shutil.move(os.path.join(stage, voice), target)
    shutil.rmtree(tmp, ignore_errors=True)
    print("installed commentary clips %s into %s" % (tag, os.path.join(AUDIO, a.lang)))


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--lang", default="en")
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("pack")
    p.add_argument("--tag", required=True)
    p.add_argument("--publish", action="store_true")
    f = sub.add_parser("fetch")
    f.add_argument("--no-gh", action="store_true")
    sub.add_parser("verify")
    a = ap.parse_args(argv)
    if a.cmd == "pack":
        pack(a)
    elif a.cmd == "fetch":
        fetch(a)
    else:
        bad = verify(a.lang)
        print("ok" if not bad else "\n".join(bad))
        sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
