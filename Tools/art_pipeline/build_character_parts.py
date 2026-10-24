"""Rebuild every character and the gear test parts, then the contact sheets. No OpenArt calls, no Unity.

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/build_character_parts.py [--canvas <scratch dir>]

Inputs that are not in git are read from the main checkout (see the constants below).
Output: Assets/Resources/Characters/<id>/{back,legL,...,head}.png + rig.json + preview.png,
        Assets/Resources/Characters/gear_test/<slot>/<part>.png + gear.json,
        Tools/art_pipeline/previews/contact_plain.png, contact_gear.png
"""
import os
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
MAIN = "E:/Code/boing-up-sky-tower"
MOMO = MAIN + "/Assets/Art/Characters/char_momo/char_momo/char_momo.png"
GEAR = MAIN + "/Assets/Art/Gear/test"


def run(*a):
    r = subprocess.run([sys.executable, *a], env=dict(os.environ, PYTHONPATH="E:/pylibs"))
    if r.returncode != 0:
        raise SystemExit("failed: %s" % " ".join(a))


def main():
    canvas = sys.argv[sys.argv.index("--canvas") + 1] if "--canvas" in sys.argv else tempfile.mkdtemp(prefix="parts_canvas_")
    cut = os.path.join(HERE, "cut_character_parts.py")
    run(os.path.join(HERE, "prepare_character_canvas.py"), "all", "--out", canvas)
    run(cut, "char", "momo", MOMO)
    for c in ("sporeling", "moth", "rootling"):
        run(cut, "char", c, os.path.join(canvas, c + ".png"))
    run(cut, "gear", "test", *["%s=%s/%s_test.png" % (s, GEAR, s) for s in ("head", "body", "pants", "boots")])
    for c in ("sporeling", "moth", "rootling"):
        run(cut, "fit", c, "test")
    prev = os.path.join(HERE, "previews")
    os.makedirs(prev, exist_ok=True)
    ren = os.path.join(HERE, "render_character_composites.py")
    run(ren, "sheet", os.path.join(prev, "contact_plain.png"))
    run(ren, "sheet", os.path.join(prev, "contact_gear.png"), "test")
    run(ren, "wear", "momo", "test", os.path.join(prev, "momo_gear.png"))


if __name__ == "__main__":
    main()
