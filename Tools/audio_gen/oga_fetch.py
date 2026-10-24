"""Fetches the CC0 music candidates from OpenGameArt (owner approved, 2026-10-24: CC0 only, OpenGameArt and Kenney).

    python Tools/audio_gen/oga_fetch.py list            # reads each page, prints licence, author, files and sizes (downloads nothing)
    python Tools/audio_gen/oga_fetch.py get <out_dir>   # downloads the CC0 audio files of the shortlist into <out_dir> (outside the project)

A page is only used when its licence list is exactly CC0. The credits go to Content/assets/audio_music_credits.md.
"""
import html
import os
import re
import sys
import urllib.request

SLUGS = """green-hills meadow-thoughts happy-clappy-loop magic-town yellow-flowers-and-tall-mossy-trees summer-park-8bit-tune-loop
sunny-side-up-electronic sunny-side-up tropoix bouncy-tune honey-bear jewel-crusher oriental asianoriental2 japaori short-loop-winter-theme
wintery-loop icy-heights sunflower-valley village1 desert-theme caught-in-the-desert-plains pirate-game-tune pirate3-tune the-gears-of-progress
winning-the-race cave-explorer crystal-tune-3 jungle-music jungleloop energetic-electro-tune submarine-game-music nautilus carnival-rides
insert-coin 8bit-title-screen get-equipped-8-bit-drum-loop elevator-music ancient-fairytale fantasy-orchestral-theme oriental8
hyyoshi-arabian-syle-backgound-music-rpg west-winds old-west-style lasso-lady-seamless-loop prehistoric-drum-loop adventure-time
determination alien-blues-8-bit-melody space-station-0 cat-in-space off-to-the-stars through-the-portal magic-space happy-adventure-loop
rainbow go-lucky quaint-town item-shop victorious sad-game-over menu-loop""".split()

BASE = "https://opengameart.org"


def fetch(url):
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0 (BoingUp asset check)"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return r.read()


def parse(slug):
    t = fetch(BASE + "/content/" + slug).decode("utf-8", "replace")
    i = t.find("field-name-field-art-licenses")
    j = t.find("field-name-collect", i)
    lic = re.findall(r"<div class='license-name'>([^<]+)</div>", t[i:j if j > i else i + 2000])
    author = re.search(r'class="username"[^>]*>([^<]+)<', t)
    title = re.search(r"<title>([^|<]+)", t)
    files = re.findall(r'<a href="(https://opengameart.org/sites/default/files/[^"]+\.(?:ogg|mp3|wav))"[^>]*>', t)
    sizes = re.findall(r"([\d.]+ (?:Mb|Kb))", t)
    return {"slug": slug, "title": html.unescape(title.group(1).strip()) if title else slug, "author": author.group(1) if author else "?",
            "licenses": [html.unescape(x.strip()) for x in lic], "files": sorted(set(files)), "sizes": sizes}


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else "list"
    for slug in SLUGS:
        try:
            p = parse(slug)
        except Exception as e:
            print("FAIL", slug, e)
            continue
        cc0 = any("CC0" in x for x in p["licenses"])   # dual licences that include CC0 are used under CC0
        print("%-45s %-18s %s | %s | %s" % (slug, p["author"][:18], ",".join(p["licenses"]) or "?", " ".join(p["sizes"][:3]), " ".join(os.path.basename(f) for f in p["files"][:3])))
        if cmd == "get" and cc0 and p["files"]:
            out = sys.argv[2]
            os.makedirs(out, exist_ok=True)
            rank = {".ogg": 0, ".mp3": 1, ".wav": 2}
            best = sorted(p["files"], key=lambda f: rank.get(os.path.splitext(f)[1].lower(), 9))
            for f in best:
                size = int(urllib.request.urlopen(urllib.request.Request(f, method="HEAD", headers={"User-Agent": "Mozilla/5.0"})).headers.get("Content-Length", 0))
                if size > 12 * 1024 * 1024 or size < 150 * 1024:
                    continue
                dst = os.path.join(out, slug + "__" + os.path.basename(f).replace("%20", "_"))
                if not os.path.exists(dst):
                    open(dst, "wb").write(fetch(f))
                print("   saved", os.path.basename(dst), size // 1024, "KB", p["author"], ",".join(p["licenses"]))
                break


if __name__ == "__main__":
    main()
