"""Contact sheet of every candidate frame of one scene: python store_contact_sheet.py <scene_dir> <out.png> (frames labelled index, row, gems, stars)."""
import sys, glob, os, re
from PIL import Image, ImageDraw
d, out = sys.argv[1], sys.argv[2]
files = sorted(glob.glob(os.path.join(d, "seed*", "*.png")))
tw, th = 216, 384
cols = 10
rows = (len(files) + cols - 1) // cols
sheet = Image.new("RGB", (cols * tw, rows * (th + 18)), "white")
dr = ImageDraw.Draw(sheet)
for i, f in enumerate(files):
    im = Image.open(f).convert("RGB").resize((tw, th))
    x, y = (i % cols) * tw, (i // cols) * (th + 18)
    sheet.paste(im, (x, y + 18))
    m = re.search(r"seed(\d)\?/?.*?r(\d+)_g(\d+)_s(\d+)", f.replace("\\", "/"))
    dr.text((x + 2, y + 3), "%d: %s" % (i, "s%s r%s g%s *%s" % m.groups() if m else ""), fill="black")
sheet.save(out)
print(len(files), "frames ->", out)
