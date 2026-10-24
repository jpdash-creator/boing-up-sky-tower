"""Exports one tile icon per worn gear layer: a tight crop of Assets/Resources/Art/GearLayers/<slot>_<n>.png on a transparent square
(256 x 256, 8 percent margin), saved to Assets/Resources/Art/GearIcons/<slot>_<n>.png. The game loads them by the same naming as GearLayers.Find,
so a piece without a layer has no icon and (GearCatalog.HasArt) never drops. Back parts (<slot>_<n>_back.png) are drawn behind the front part.

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/export_gear_icons.py
"""
import json
import os
import re
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
SRC = os.path.join(ROOT, "Assets", "Resources", "Art", "GearLayers")
DST = os.path.join(ROOT, "Assets", "Resources", "Art", "GearIcons")
SIZE, MARGIN = 256, 0.08


CHARS = os.path.join(ROOT, "Assets", "Resources", "Characters")


def to_icon(comp):
    box = comp.getchannel("A").point(lambda a: 255 if a > 12 else 0).getbbox()
    if box is None:
        return None
    crop = comp.crop(box)
    k = int(SIZE * (1 - 2 * MARGIN)) / max(crop.size)
    crop = crop.resize((max(1, round(crop.width * k)), max(1, round(crop.height * k))), Image.LANCZOS)
    out = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    out.paste(crop, ((SIZE - crop.width) // 2, (SIZE - crop.height) // 2), crop)
    return out


def export_part_sets():
    """Gear cut into parts (Resources/Characters/gear_<set>/gear.json): the icon is a tight crop of the composite of the slot's parts."""
    n = 0
    for d in sorted(os.listdir(CHARS)):
        gj = os.path.join(CHARS, d, "gear.json")
        if not d.startswith("gear_") or not os.path.exists(gj):
            continue
        gear = json.load(open(gj, encoding="utf-8"))
        for slot, sd in gear["slots"].items():
            comp = None
            for e in sd["parts"]:
                im = Image.open(os.path.join(CHARS, d, e["file"])).convert("RGBA")
                comp = im if comp is None else Image.alpha_composite(comp, im)
            icon = to_icon(comp) if comp is not None else None
            if icon is not None:
                icon.save(os.path.join(DST, "%s_%s.png" % (slot, d[len("gear_"):])))
                n += 1
    print("exported", n, "icons from gear parts")


def main():
    os.makedirs(DST, exist_ok=True)
    export_part_sets()
    n = 0
    for f in sorted(os.listdir(SRC)):
        if not re.match(r"^[a-z]+_\d+\.png$", f):
            continue
        front = Image.open(os.path.join(SRC, f)).convert("RGBA")
        comp = front
        back = os.path.join(SRC, f.replace(".png", "_back.png"))
        if os.path.exists(back):
            comp = Image.alpha_composite(Image.open(back).convert("RGBA"), front)
        box = comp.getchannel("A").point(lambda a: 255 if a > 12 else 0).getbbox()
        if box is None:
            print("skip empty", f)
            continue
        crop = comp.crop(box)
        side = max(crop.size)
        inner = int(SIZE * (1 - 2 * MARGIN))
        k = inner / side
        crop = crop.resize((max(1, round(crop.width * k)), max(1, round(crop.height * k))), Image.LANCZOS)
        out = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
        out.paste(crop, ((SIZE - crop.width) // 2, (SIZE - crop.height) // 2), crop)
        out.save(os.path.join(DST, f))
        n += 1
    print("exported", n, "icons to", os.path.relpath(DST, ROOT))


if __name__ == "__main__":
    main()
