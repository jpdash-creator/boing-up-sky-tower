"""Fails when a baked platform piece contains two stacked platforms (owner issue 15, Sunny Beach level 7).

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/check_stacked.py
"""
import glob
import os
import sys

sys.path.insert(0, "E:/pylibs")
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

import slice_width_sheet as sw  # noqa: E402

root = os.path.join(os.path.dirname(__file__), "..", "..")
bad = 0
files = sorted(glob.glob(os.path.join(root, "Assets", "Art", "Regions", "region_00*", "tiles", "reg00*_widths", "reg00*_*.png")))
for f in files:
    if len(sw.split_merged(np.asarray(Image.open(f).convert("RGBA")))) > 1:
        bad += 1
        print("STACKED:", os.path.basename(f))
print(len(files), "pieces checked,", bad, "stacked")
sys.exit(1 if bad else 0)
