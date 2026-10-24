"""Builds the gear mannequin from a character picture (docs/18 section B): a flat mid-grey doll with the character's silhouette on a magenta background.
Grey and magenta are colours no gear uses, so a worn piece is cut out later by colour distance from them.

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/make_mannequin.py [character.png] [out.png]
"""
import os
import sys
import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Assets", "Art", "Characters", "char_momo", "char_momo", "char_momo.png")
dst = sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "Art", "_candidates", "mannequin", "mannequin_momo.png")

im = Image.open(src).convert("RGBA")
a = np.asarray(im).astype(np.float32)
alpha = a[..., 3] / 255.0
lum = (0.3 * a[..., 0] + 0.59 * a[..., 1] + 0.11 * a[..., 2]) / 255.0
# soft form shading in a narrow band around mid grey (never reaches the magenta or the colours of the gear)
grey = 122.0 + 40.0 * np.clip(lum, 0, 1)
out = np.zeros(a.shape[:2] + (3,), np.float32)
out[...] = (255, 0, 255)
for c in range(3):
    out[..., c] = out[..., c] * (1 - alpha) + grey * alpha
img = Image.fromarray(out.astype(np.uint8), "RGB")
img.save(dst)
print("wrote", dst, img.size)
