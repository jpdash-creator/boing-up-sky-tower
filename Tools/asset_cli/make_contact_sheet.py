"""Builds a local HTML contact sheet of all candidate images so the owner can pick one per asset by number.

    python Tools/asset_cli/make_contact_sheet.py

Reads Art/_candidates/<asset id>/<n>.png, writes 480 px thumbnails to Art/_pick/thumbs/ (git-ignored) and
Art/_pick/index.html. Costs nothing. Open the html in a browser; picks are told to Claude as "<asset id>: <n>".
"""
import html
import json
import os

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CAND = os.path.join(ROOT, "Art", "_candidates")
OUT = os.path.join(ROOT, "Art", "_pick")
THUMBS = os.path.join(OUT, "thumbs")

GROUPS = [("Regions 1-5 (T1-A)", lambda a: a.startswith("reg00")),
          ("Core UI and map (T1-B)", lambda a: a.startswith("ui_") or a.startswith("map_")),
          ("Pickups, currency, VFX, trail, icon (T1-C)", lambda a: a.startswith(("pck_", "cur_", "vfx_", "trl_", "fnd_")))]


def main():
    os.makedirs(THUMBS, exist_ok=True)
    with open(os.path.join(ROOT, "Content", "assets", "manifest.json"), encoding="utf-8") as f:
        names = {a["id"]: a["name"] for a in json.load(f)["assets"]}
    assets = sorted(d for d in os.listdir(CAND) if os.path.isdir(os.path.join(CAND, d)))
    body = []
    for title, pred in GROUPS:
        items = [a for a in assets if pred(a)]
        if not items:
            continue
        body.append("<h2>%s <small>(%d assets)</small></h2>" % (html.escape(title), len(items)))
        for a in items:
            files = sorted((f for f in os.listdir(os.path.join(CAND, a)) if f.lower().endswith((".png", ".jpg", ".jpeg"))),
                           key=lambda f: int(os.path.splitext(f)[0]))
            cells = []
            for fn in files:
                n = os.path.splitext(fn)[0]
                src = os.path.join(CAND, a, fn)
                th = os.path.join(THUMBS, "%s_%s.jpg" % (a, n))
                if not os.path.exists(th):
                    with Image.open(src) as im:
                        im.thumbnail((480, 480))
                        im.convert("RGB").save(th, "JPEG", quality=82)
                cells.append('<figure><a href="../_candidates/%s/%s" target="_blank"><img loading="lazy" src="thumbs/%s_%s.jpg"></a>'
                             '<figcaption>%s</figcaption></figure>' % (a, fn, a, n, n))
            body.append('<section><h3>%s <small>%s</small></h3><div class="row">%s</div></section>' %
                        (html.escape(a), html.escape(names.get(a, "")), "".join(cells)))
    page = ("<!doctype html><meta charset='utf-8'><title>Sky Hopper - pick candidates</title>"
            "<style>body{font-family:system-ui;margin:16px;background:#1b1b24;color:#eee}h2{margin-top:32px;border-bottom:1px solid #444}"
            "small{color:#999;font-weight:normal}.row{display:flex;gap:10px;flex-wrap:wrap}figure{margin:0;text-align:center}"
            "img{max-height:260px;max-width:340px;border-radius:6px;background:#808080}figcaption{font-size:22px;font-weight:bold}"
            "section{margin:14px 0}</style><h1>Pick one image per asset</h1>"
            "<p>Tell Claude your picks as <code>asset_id: number</code> (or 'all 1' for the first of everything). Click an image to open it full size.</p>"
            + "".join(body))
    with open(os.path.join(OUT, "index.html"), "w", encoding="utf-8") as f:
        f.write(page)
    print("assets:", len(assets), "->", os.path.join(OUT, "index.html"))


if __name__ == "__main__":
    main()
