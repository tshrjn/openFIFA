"""Contact sheets for the looks judges: faces, angles (3/4 + profile), bodies (front), backs, ingame."""
import glob, os, sys
from PIL import Image, ImageDraw
out = sys.argv[1]
os.makedirs(out + "/sheets", exist_ok=True)

def sheet(paths, cols, name, cell=(640, 360)):
    paths = [p for p in paths if os.path.exists(p)]
    if not paths: return
    rows = (len(paths) + cols - 1) // cols
    c = Image.new("RGB", (cols * cell[0], rows * cell[1]), (20, 20, 20))
    d = ImageDraw.Draw(c)
    for i, p in enumerate(paths):
        im = Image.open(p).convert("RGB")
        # Centre crop to the cell's aspect, then resize.
        w, h = im.size; a = cell[0] / cell[1]
        if w / h > a: nw = int(h * a); im = im.crop(((w - nw) // 2, 0, (w - nw) // 2 + nw, h))
        im = im.resize(cell)
        x, y = (i % cols) * cell[0], (i // cols) * cell[1]
        c.paste(im, (x, y)); d.text((x + 6, y + 4), os.path.basename(p)[6:-4], fill=(255, 255, 0))
    c.save(f"{out}/sheets/{name}.png")

P = lambda pat: sorted(glob.glob(f"{out}/{pat}"))
sheet(P("looks_face_p*.png"), 3, "faces")
ang = []
for f in P("looks_face34_p*.png"): ang += [f, f.replace("face34", "profile")]
sheet(ang, 4, "angles", (480, 270))
sheet(P("looks_body_p*.png"), 3, "bodies")
sheet(P("looks_bodyback_p*.png"), 3, "backs")
sheet([f"{out}/looks_broadcast.png", f"{out}/looks_closeup_ingame.png"], 2, "ingame", (960, 540))
