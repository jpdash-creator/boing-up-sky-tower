"""Draws the generic chibi mannequin (docs/18): a flat grey doll with the template proportions of Momo (big round head about half the height, short body,
short legs, small arms) but NO species features (no ears, tail, fur), on magenta. Used as the pose and size reference for new characters so they do not
turn into Momo clones.

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/make_generic_mannequin.py [out.png]
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFilter

W, H = 1365, 1649
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Art", "_candidates", "mannequin", "mannequin_generic.png")
S = 2
img = Image.new("RGB", (W * S, H * S), (255, 0, 255))
d = ImageDraw.Draw(img)
grey, edge = (140, 140, 140), (96, 96, 96)


def shape(f, *a, **k):
    f(*a, fill=grey, outline=edge, width=14 * S, **k)


# legs and feet
shape(d.rounded_rectangle, [470 * S, 1180 * S, 650 * S, 1520 * S], radius=70 * S)
shape(d.rounded_rectangle, [715 * S, 1180 * S, 895 * S, 1520 * S], radius=70 * S)
shape(d.ellipse, [420 * S, 1470 * S, 690 * S, 1600 * S])
shape(d.ellipse, [675 * S, 1470 * S, 945 * S, 1600 * S])
# arms
shape(d.rounded_rectangle, [250 * S, 1000 * S, 420 * S, 1330 * S], radius=85 * S)
shape(d.rounded_rectangle, [945 * S, 1000 * S, 1115 * S, 1330 * S], radius=85 * S)
# torso
shape(d.rounded_rectangle, [390 * S, 940 * S, 975 * S, 1320 * S], radius=150 * S)
# head: a plain big round head
shape(d.ellipse, [160 * S, 120 * S, 1205 * S, 1060 * S])
img = img.resize((W, H), Image.LANCZOS)
os.makedirs(os.path.dirname(out), exist_ok=True)
img.save(out)
print("wrote", out)
