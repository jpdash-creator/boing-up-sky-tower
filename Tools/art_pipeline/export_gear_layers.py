"""Exports the cut gear layers (Assets/Art/Gear/layers, see cut_gear_layers.py) to the game: every usable layer becomes a PNG the size of the Momo
picture (1365 x 1649) so it can be drawn over Momo at the same rect, in Assets/Resources/Art/GearLayers/<slot>_<n>.png (and <slot>_<n>_back.png for
pieces with a part behind the body). Unusable pieces (cream or white gear that blends with the fur) are skipped, the game then shows no layer.

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/export_gear_layers.py
"""
import json
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
SRC = os.path.join(ROOT, "Assets", "Art", "Gear")
DST = os.path.join(ROOT, "Assets", "Resources", "Art", "GearLayers")


def paste(canvas_size, mo, file, offset):
    layer = Image.open(os.path.join(SRC, file)).convert("RGBA")
    canvas = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    canvas.paste(layer, (offset[0] - mo[0], offset[1] - mo[1]), layer)
    return canvas


def main():
    meta = json.load(open(os.path.join(SRC, "layers.json"), encoding="utf-8"))
    mo, size = meta["momoOffset"], tuple(meta["momoSize"])
    os.makedirs(DST, exist_ok=True)
    n = 0
    for l in meta["layers"]:
        if not l.get("usable", True):
            print("skip", l["slot"], l["index"], "-", l.get("review", ""))
            continue
        paste(size, mo, l["file"], l["offset"]).save(os.path.join(DST, "%s_%d.png" % (l["slot"], l["index"])))
        n += 1
        if l.get("backFile"):
            paste(size, mo, l["backFile"], l["backOffset"]).save(os.path.join(DST, "%s_%d_back.png" % (l["slot"], l["index"])))
    print("exported", n, "layers to", os.path.relpath(DST, ROOT))


if __name__ == "__main__":
    main()
