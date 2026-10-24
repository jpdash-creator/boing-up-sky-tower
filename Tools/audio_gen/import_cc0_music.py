"""Imports the downloaded CC0 tracks (see oga_fetch.py) as the game music (owner issue 19, 2026-10-24: no code-made music).

    PYTHONPATH=E:/pylibs python Tools/audio_gen/import_cc0_music.py E:/Unity/music_candidates

Writes Assets/Resources/Audio/music/loop.ogg (menu), region_1..30.ogg and sfx/win.ogg, sfx/fail.ogg: mono, normalised to -3 dB, at most 100 s,
with a short fade at both ends so a loop never clicks. The credits table goes to Content/assets/audio_music_credits.md.
"""
import os
import sys
import numpy as np
import soundfile as sf

SRC = sys.argv[1] if len(sys.argv) > 1 else "E:/Unity/music_candidates"
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")

# slug (file prefix) per region, in game order; chosen by mood and theme
REGIONS = ["green-hills", "magic-town", "summer-park-8bit-tune-loop", "bouncy-tune", "asianoriental2",
           "short-loop-winter-theme", "sunflower-valley", "desert-theme", "pirate-game-tune", "winning-the-race",
           "go-lucky", "crystal-tune-3", "the-gears-of-progress", "jungle-music", "energetic-electro-tune",
           "submarine-game-music", "carnival-rides", "insert-coin", "adventure-time", "magic-space",
           "fantasy-orchestral-theme", "oriental8", "hyyoshi-arabian-syle-backgound-music-rpg", "icy-heights", "old-west-style",
           "happy-adventure-loop", "determination", "alien-blues-8-bit-melody", "cat-in-space", "through-the-portal"]
MENU = "quaint-town"
WIN = ("victorious", 7.0)
FAIL = ("sad-game-over", 6.5)
MAX_SECONDS = 100.0


def find(slug):
    for f in sorted(os.listdir(SRC)):
        if f.startswith(slug + "__"):
            return os.path.join(SRC, f)
    raise SystemExit("missing " + slug)


def load(slug, limit):
    data, sr = sf.read(find(slug), dtype="float32", always_2d=True)
    mono = data.mean(axis=1)
    mono = mono[: int(limit * sr)]
    peak = float(np.max(np.abs(mono))) or 1.0
    mono = mono / peak * 0.7
    fade = int(0.4 * sr)
    if len(mono) > 2 * fade:
        mono[:fade] *= np.linspace(0, 1, fade)
        mono[-fade:] *= np.linspace(1, 0, fade)
    return mono, sr


def write(path, slug, limit):
    mono, sr = load(slug, limit)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    mono = np.ascontiguousarray(mono)
    with sf.SoundFile(path, "w", samplerate=sr, channels=1, format="OGG", subtype="VORBIS") as f:   # one big write crashes the encoder: write in blocks
        for i in range(0, len(mono), 32768):
            f.write(mono[i:i + 32768])
    print("wrote", os.path.relpath(path, OUT), "%.1fs" % (len(mono) / sr), "<-", slug)


def main():
    write(os.path.join(OUT, "music", "loop.ogg"), MENU, MAX_SECONDS)
    for i, slug in enumerate(REGIONS, start=1):
        write(os.path.join(OUT, "music", "region_%d.ogg" % i), slug, MAX_SECONDS)
    write(os.path.join(OUT, "sfx", "win.ogg"), WIN[0], WIN[1])
    write(os.path.join(OUT, "sfx", "fail.ogg"), FAIL[0], FAIL[1])


if __name__ == "__main__":
    main()
