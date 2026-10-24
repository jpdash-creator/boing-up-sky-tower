"""Draws the character PARTS TEMPLATE (docs/18, owner 2026-10-24: characters and gear are built from separate parts, plug and play).

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/make_parts_template.py [out_dir]

Writes parts_template.png (a flat grey doll taken apart on magenta: each part is a grey silhouette in its own cell) and parts.json (cells, and the offset that
moves a part from the sheet back to its place in the assembled doll on the 1365 x 1649 character canvas).

A character (or a piece of gear) is generated as an image2image of this template: the model fills every silhouette with that part, so the parts come out
separate, complete (nothing hidden behind a neighbour) and in known positions. cut_parts.py then cuts each cell and shifts it back, giving one transparent
PNG per part on the character canvas. Parts, back to front: back (tail, wings), legL, legR, footL, footR, torso (neck, scarf), armL, armR, head.
Gear zones: head -> head; body -> torso, armL, armR; pants -> legL, legR (above the ankle line); boots -> footL, footR (ankle line and below).
"""
import json
import os
import sys
from PIL import Image, ImageDraw

W, H = 1365, 1649          # assembled character canvas (the Momo picture size)
SW, SH = 2880, 2160         # parts sheet size, exactly 4:3 so the image model keeps the layout without stretching
MAGENTA = (255, 0, 255)
GREY, EDGE = (140, 140, 140), (96, 96, 96)

# assembled geometry of the generic doll (same as make_generic_mannequin.py) and the cell each part is exploded to
# name: (kind, assembled box (x0,y0,x1,y1), sheet cell top-left)
PARTS = {
    "head":  ("ellipse", (160, 120, 1205, 1060), (60, 40)),
    "back":  ("cloud",   (1010, 760, 1340, 1260), (1500, 40)),
    "torso": ("round",   (390, 940, 975, 1320), (60, 1250)),
    "armL":  ("round",   (250, 1000, 420, 1330), (780, 1250)),
    "armR":  ("round",   (945, 1000, 1115, 1330), (1060, 1250)),
    "legL":  ("round",   (470, 1180, 650, 1520), (1380, 1250)),
    "legR":  ("round",   (715, 1180, 895, 1520), (1660, 1250)),
    "footL": ("ellipse", (420, 1470, 690, 1600), (1960, 1250)),
    "footR": ("ellipse", (675, 1470, 945, 1600), (1960, 1500)),
}
# the head cell is larger than the doll head so ears, horns and antennae have room; the same margin is kept in the assembled canvas
HEAD_CELL = (0, 0, 1260, 1130)      # assembled-canvas box that the head cell covers (the head part may use all of it)


def draw(d, kind, box, ox, oy):
    x0, y0, x1, y1 = box
    b = [x0 + ox, y0 + oy, x1 + ox, y1 + oy]
    k = dict(fill=GREY, outline=EDGE, width=12)
    if kind == "ellipse":
        d.ellipse(b, **k)
    elif kind == "cloud":
        d.ellipse(b, **k)
    else:
        d.rounded_rectangle(b, radius=min(b[2] - b[0], b[3] - b[1]) // 2 - 4, **k)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Art", "_candidates", "parts")
    os.makedirs(out, exist_ok=True)
    img = Image.new("RGB", (SW, SH), MAGENTA)
    d = ImageDraw.Draw(img)
    meta = {"canvas": [W, H], "sheet": [SW, SH], "parts": {}}
    for name, (kind, box, cell) in PARTS.items():
        bx0, by0, bx1, by1 = box
        if name == "head":
            cx0, cy0, cx1, cy1 = HEAD_CELL
        else:
            pad = 40
            cx0, cy0, cx1, cy1 = bx0 - pad, by0 - pad, bx1 + pad, by1 + pad
        # offset: assembled = sheet - (offset)  ->  sheet = assembled + offset, cell top-left lands on `cell`
        ox, oy = cell[0] - cx0, cell[1] - cy0
        draw(d, kind, box, ox, oy)
        meta["parts"][name] = {"kind": kind, "assembled": list(box), "cell": [cx0 + ox, cy0 + oy, cx1 + ox, cy1 + oy], "offset": [ox, oy],
                               "order": ["back", "legL", "legR", "footL", "footR", "torso", "armL", "armR", "head"].index(name)}
    img.save(os.path.join(out, "parts_template.png"))
    json.dump(meta, open(os.path.join(out, "parts.json"), "w"), indent=2)
    print("wrote", out, img.size)


if __name__ == "__main__":
    main()
