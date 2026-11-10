#!/usr/bin/env python3
"""Builds the public legal pages docs/policy.html and docs/terms.html from POLICY.md and TERMS.md, in the site design (docs/style.css, shared with the hand-designed docs/index.html).

Run from the repo root after any change to those documents:  python Tools/legal_html.py
Needs the `markdown-it-py` package (pip install markdown-it-py). The pages are static, self-contained (one CSS file, no scripts, no external requests)
and work on GitHub Pages (Settings > Pages > Deploy from branch main, folder /docs).
"""
import html
import re
import sys
from pathlib import Path

from markdown_it import MarkdownIt

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "docs"
SITE = "Fluffy Climbers: Tower Jump"
EMAIL = "jpdash.creator@gmail.com"

# (source file, output file, nav label, page title)
PAGES = [
    ("POLICY.md", "policy.html", "Privacy", SITE + " Privacy Policy"),
    ("TERMS.md", "terms.html", "Terms", SITE + " Terms of Use"),
]

LINK_MAP = {"POLICY": "policy.html", "POLICY.md": "policy.html", "TERMS": "terms.html", "TERMS.md": "terms.html",
            "index": "index.html", "index.md": "index.html"}


def slug(text):
    """GitHub style heading id, so the contents links of the documents (#1-who-we-are) work."""
    s = re.sub(r"<[^>]+>", "", text).strip().lower()
    s = re.sub(r"[^\w\- ]", "", s)
    return s.replace(" ", "-")


def render(md_text):
    md = MarkdownIt("commonmark", {"html": False, "linkify": False}).enable("table")
    tokens = md.parse(md_text)
    for i, t in enumerate(tokens):
        if t.type == "heading_open":
            inline = tokens[i + 1]
            t.attrSet("id", slug(inline.content))
        if t.type == "inline" and t.children:
            for c in t.children:
                if c.type == "link_open":
                    href = c.attrGet("href") or ""
                    base, _, frag = href.partition("#")
                    if base in LINK_MAP:
                        c.attrSet("href", LINK_MAP[base] + ("#" + frag if frag else ""))
                    elif href.startswith("http"):
                        c.attrSet("rel", "noopener")
    return md.renderer.render(tokens, md.options, {})


def first_text(md_text):
    for line in md_text.splitlines():
        line = line.strip()
        if line and not line.startswith("#") and not line.startswith(">"):
            return re.sub(r"[\[\]*_`]", "", re.sub(r"\(([^)]*)\)", "", line))[:160]
    return SITE


def page(title, description, body, current, heading, updated):
    nav = "\n".join(
        '        <a href="%s"%s>%s</a>' % (out, ' aria-current="page"' if out == current else "", html.escape(label))
        for _, out, label, _ in PAGES)
    return """<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{title}</title>
  <meta name="description" content="{desc}">
  <meta name="color-scheme" content="light dark">
  <link rel="icon" href="favicon.png" type="image/png">
  <link rel="apple-touch-icon" href="apple-touch-icon.png">
  <link rel="stylesheet" href="style.css">
</head>
<body>
  <a class="skip" href="#main">Skip to content</a>
  <header class="site-header">
    <div class="wrap">
      <a class="brand" href="index.html"><img src="img/app-icon.webp" width="42" height="42" alt=""><span>Fluffy Climbers</span></a>
      <nav class="nav" aria-label="Main">
        <a href="index.html">Home</a>
{nav}
      </nav>
    </div>
  </header>
  <main id="main">
    <div class="read-head">
      <div class="wrap">
        <h1>{heading}</h1>
        <p>{updated}</p>
      </div>
    </div>
    <article class="prose">
{body}
    </article>
  </main>
  <footer class="site-footer">
    <div class="wrap">
      <div>
        <a class="brand" href="index.html"><img src="img/app-icon.webp" width="42" height="42" alt=""><span>Fluffy Climbers</span></a>
        <p>Fluffy Climbers: Tower Jump is a 2D jumping game for Android, coming soon to Google Play.</p>
      </div>
      <div>
        <h2>Read</h2>
        <ul>
          <li><a href="policy.html">Privacy Policy</a></li>
          <li><a href="terms.html">Terms of Use</a></li>
        </ul>
      </div>
      <div>
        <h2>Contact</h2>
        <ul>
          <li><a href="mailto:{email}">{email}</a></li>
        </ul>
      </div>
      <p class="fine">Published by J. P. Dash, Bengaluru, India.</p>
    </div>
  </footer>
</body>
</html>
""".format(title=html.escape(title), desc=html.escape(description), nav=nav, body=body, heading=html.escape(heading), updated=html.escape(updated), email=EMAIL)


def main():
    OUT.mkdir(exist_ok=True)
    for src, out, _label, title in PAGES:
        md_text = (ROOT / src).read_text(encoding="utf-8")
        body = render(md_text)
        m = re.search(r"^# (.+)$", md_text, re.M)
        heading = m.group(1).strip() if m else title
        u = re.search(r"^Last updated:.*$", md_text, re.M)
        updated = u.group(0) if u else ""
        # the page heading is in the banner, so drop the document's own first h1 from the body
        body = re.sub(r"<h1[^>]*>.*?</h1>\s*", "", body, count=1, flags=re.S)
        body = re.sub(r"<p>Last updated:[^<]*</p>\s*", "", body, count=1)   # the banner shows it
        (OUT / out).write_text(page(title, first_text(md_text), body, out, heading, updated), encoding="utf-8", newline="\n")
        print("wrote docs/%s (%d bytes)" % (out, (OUT / out).stat().st_size))
    (OUT / ".nojekyll").write_text("", encoding="utf-8")
    print("index.html and style.css are hand-designed files and are not touched by this script")


if __name__ == "__main__":
    sys.exit(main())
