"""Splits lb_frame.png into a top piece (ribbon, drawn at uniform scale) and a body piece (9-sliced by the importer: flat middle stretches vertically).
Run: python Tools/art_pipeline/split_lb_frame.py   (after process_leaderboard_ui.py)"""
import os
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
D = os.path.join(ROOT, "Assets", "Resources", "Art", "Ui")
CUT = 300
im = Image.open(os.path.join(D, "lb_frame.png")).convert("RGBA")
w, h = im.size
im.crop((0, 0, w, CUT)).save(os.path.join(D, "lb_frame_top.png"))
im.crop((0, CUT, w, h)).save(os.path.join(D, "lb_frame_body.png"))
print("top", w, CUT, "body", w, h - CUT)
