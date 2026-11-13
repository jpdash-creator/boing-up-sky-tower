#!/usr/bin/env python3
"""Original upbeat menu-music loops for Fluffy Climbers (owner request 2026-11-12: replace the ominous loop.ogg).

Pure numpy synthesis, no samples, no third-party music: every note is generated here, so the result is ours (no licence).
Five themes, 16 bars each, seamless loops (notes and echoes wrap around the loop point).
  python Tools/music_gen/compose_menu_themes.py [outdir]
Writes <name>.wav (44.1 kHz stereo 16 bit) and <name>_preview.wav (22.05 kHz mono) per theme, plus a stats line.
Deterministic: fixed seeds, no clock.
"""
import os
import sys
import wave
import numpy as np

SR = 44100
MAJOR = [0, 2, 4, 5, 7, 9, 11]
RNG = np.random.default_rng(20261112)


def mtof(m):
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


def sc(root, k):
    return root + MAJOR[k % 7] + 12 * (k // 7)


def triad(root, deg):
    return [sc(root, deg), sc(root, deg + 2), sc(root, deg + 4)]


def tt(n):
    return np.arange(n) / SR


def envelope(n, atk, rel_start, rel):
    e = np.ones(n)
    a = max(1, int(atk * SR))
    e[:a] = np.linspace(0, 1, a)
    r0 = int(rel_start * SR)
    if r0 < n:
        m = n - r0
        e[r0:] *= np.exp(-np.arange(m) / (rel * SR))
    return e


# ---------------- instruments (mono float arrays) ----------------

def marimba(f, dur, vel=1.0):
    n = int(SR * min(dur + 0.35, 1.4))
    t = tt(n)
    y = np.sin(2 * np.pi * f * t) * np.exp(-t / 0.32)
    y += 0.38 * np.sin(2 * np.pi * 4 * f * t) * np.exp(-t / 0.05)
    y += 0.12 * np.sin(2 * np.pi * 9.8 * f * t) * np.exp(-t / 0.02)
    y *= np.minimum(1.0, t / 0.002)
    return y * vel


def marimba_soft(f, dur, vel=1.0):
    """Warmer, rounder marimba: weaker upper partials, slightly longer body (used by the lowered Sunny Skip)."""
    n = int(SR * min(dur + 0.4, 1.5))
    t = tt(n)
    y = np.sin(2 * np.pi * f * t) * np.exp(-t / 0.38)
    y += 0.14 * np.sin(2 * np.pi * 4 * f * t) * np.exp(-t / 0.05)
    y += 0.03 * np.sin(2 * np.pi * 9.8 * f * t) * np.exp(-t / 0.02)
    y *= np.minimum(1.0, t / 0.004)
    return y * vel


def lowpass_circular(x, fc):
    """First-order low-pass applied in the FFT domain, so the loop stays seamless."""
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1.0 / SR)
    return np.fft.irfft(X / np.sqrt(1.0 + (f / fc) ** 2), len(x))


def bell(f, dur, vel=1.0, ring=1.0):
    n = int(SR * min(dur + 1.2 * ring, 2.2))
    t = tt(n)
    y = np.zeros(n)
    for ratio, amp, dec in ((1.0, 1.0, 0.9), (2.76, 0.35, 0.5), (5.4, 0.18, 0.25), (8.9, 0.07, 0.12)):
        y += amp * np.sin(2 * np.pi * f * ratio * t) * np.exp(-t / (dec * ring))
    y *= np.minimum(1.0, t / 0.001)
    return y * vel * 0.6


def glock(f, dur, vel=1.0):
    return bell(f, dur, vel * 0.8, 0.6)


def pluck(f, dur, vel=1.0, damp=0.996):
    n = int(SR * (dur + 0.25))
    L = max(2, int(SR / f))
    y = np.zeros(n)
    init = RNG.uniform(-1, 1, L)
    init = (init + np.roll(init, 1) + np.roll(init, 2)) / 3.0
    y[:L] = init[:min(L, n)]
    pos = L
    prev = y[:L].copy()
    while pos < n:
        cur = damp * 0.5 * (prev + np.roll(prev, 1))
        m = min(L, n - pos)
        y[pos:pos + m] = cur[:m]
        prev = cur
        pos += L
    y *= envelope(n, 0.001, dur, 0.08)
    return y * vel * 0.9


def tri_bass(f, dur, vel=1.0):
    n = int(SR * (dur + 0.12))
    t = tt(n)
    y = (2 / np.pi) * np.arcsin(np.sin(2 * np.pi * f * t)) + 0.25 * np.sin(2 * np.pi * 2 * f * t)
    y *= envelope(n, 0.004, dur, 0.04) * np.exp(-t / 0.5)
    return y * vel * 0.8


def square(f, dur, vel=1.0, duty=0.25):
    n = int(SR * (dur + 0.08))
    t = tt(n)
    ph = (f * t) % 1.0
    y = np.where(ph < duty, 1.0, -1.0)
    y = np.convolve(y, np.ones(4) / 4.0, mode="same")
    y *= envelope(n, 0.002, dur, 0.03)
    return y * vel * 0.32


def whistle(f, dur, vel=1.0):
    n = int(SR * (dur + 0.15))
    t = tt(n)
    vib = 1 + 0.006 * np.sin(2 * np.pi * 5.2 * t) * np.minimum(1.0, t / 0.18)
    ph = 2 * np.pi * np.cumsum(f * vib) / SR
    y = np.sin(ph) + 0.12 * np.sin(2 * ph)
    y *= envelope(n, 0.03, dur, 0.06)
    return y * vel * 0.5


def blip(f, dur, vel=1.0):
    n = int(SR * 0.22)
    t = tt(n)
    fr = f * (1 + 1.6 * np.exp(-t / 0.022))
    ph = 2 * np.pi * np.cumsum(fr) / SR
    y = np.sin(ph) * np.exp(-t / 0.075)
    y *= np.minimum(1.0, t / 0.002)
    return y * vel * 0.7


def kick(vel=1.0):
    n = int(SR * 0.3)
    t = tt(n)
    fr = 48 + 95 * np.exp(-t / 0.03)
    ph = 2 * np.pi * np.cumsum(fr) / SR
    return np.sin(ph) * np.exp(-t / 0.09) * vel


def noise(n):
    return RNG.uniform(-1, 1, n)


def hat(vel=1.0, open_=False):
    n = int(SR * (0.16 if open_ else 0.05))
    t = tt(n)
    y = np.diff(noise(n + 1))
    return y * np.exp(-t / (0.06 if open_ else 0.012)) * vel * 0.5


def shaker(vel=1.0):
    n = int(SR * 0.09)
    t = tt(n)
    y = np.diff(noise(n + 1))
    y *= np.minimum(1.0, t / 0.008) * np.exp(-t / 0.03)
    return y * vel * 0.55


def clap(vel=1.0):
    n = int(SR * 0.22)
    t = tt(n)
    base = np.diff(noise(n + 1))
    y = np.zeros(n)
    for off in (0.0, 0.011, 0.022):
        i = int(off * SR)
        y[i:] += base[:n - i] * np.exp(-np.arange(n - i) / (SR * 0.012))
    y += base * np.exp(-t / 0.07) * 0.5
    y = np.convolve(y, np.ones(3) / 3.0, mode="same")
    return y * vel * 0.6


# ---------------- mixing ----------------

class Mix:
    def __init__(self, n):
        self.n = n
        self.buses = {}

    def bus(self, name):
        if name not in self.buses:
            self.buses[name] = [np.zeros(self.n), np.zeros(self.n)]
        return self.buses[name]

    def add(self, name, start, sig, pan=0.0, gain=1.0):
        b = self.bus(name)
        l = gain * np.sqrt(0.5 * (1 - pan))
        r = gain * np.sqrt(0.5 * (1 + pan))
        s = int(start * SR) % self.n
        m = len(sig)
        if m >= self.n:
            sig = sig[:self.n - 1]
            m = len(sig)
        e = s + m
        if e <= self.n:
            b[0][s:e] += sig * l
            b[1][s:e] += sig * r
        else:
            k = self.n - s
            b[0][s:] += sig[:k] * l
            b[1][s:] += sig[:k] * r
            b[0][:m - k] += sig[k:] * l
            b[1][:m - k] += sig[k:] * r

    def echo(self, name, d1, d2, g1, g2):
        b = self.bus(name)
        L, R = b
        i1, i2 = int(d1 * SR), int(d2 * SR)
        newL = L + g1 * np.roll(R, i1) + g2 * np.roll(L, i2)
        newR = R + g1 * np.roll(L, i1) + g2 * np.roll(R, i2)
        b[0], b[1] = newL, newR

    def render(self, gains):
        L = np.zeros(self.n)
        R = np.zeros(self.n)
        for name, (bl, br) in self.buses.items():
            g = gains.get(name, 1.0)
            L += bl * g
            R += br * g
        return L, R


# ---------------- composing ----------------

def compose(spec):
    bpm = spec["bpm"]
    step = 60.0 / bpm / 2.0          # one eighth note
    bars = 16
    n = int(round(bars * 8 * step * SR))
    mix = Mix(n)
    root = spec["root"]
    prog = spec["progA"] * 2 + spec["progB"] * 2
    swing = spec.get("swing", 0.0)

    def when(bar, s):
        t = (bar * 8 + s) * step
        if s % 2 == 1:
            t += swing * step
        return t

    for bar in range(bars):
        deg = prog[bar]
        tri = triad(root, deg)
        ct = [tri[0], tri[1], tri[2], tri[0] + 12, tri[1] + 12, tri[2] + 12]
        phrase_bar = bar % 4
        pats = spec["lead_pats"]
        pat = pats[0] if phrase_bar < 2 else (pats[1] if phrase_bar == 2 else pats[2])
        if bar >= 8:
            pat = pats[3] if phrase_bar < 3 else pats[2]
        # lead
        for s, idx in enumerate(pat):
            if idx is None:
                continue
            note = ct[idx] + spec["lead_shift"]
            dur = step * (1.6 if s == 7 else 0.95)
            sig = spec["lead"](mtof(note), dur, 0.85 if s % 2 == 0 else 0.7)
            mix.add("lead", when(bar, s), sig, spec.get("lead_pan", 0.0), 1.0)
        # arpeggio / strum layer
        arp = spec.get("arp")
        if arp:
            for s, idx in enumerate(spec["arp_pat"]):
                if idx is None:
                    continue
                note = ct[idx] + spec.get("arp_shift", 0)
                sig = arp["fn"](mtof(note), step * 0.9, 0.5)
                mix.add("arp", when(bar, s), sig, arp.get("pan", 0.3) * (1 if s % 2 else -1), 1.0)
        strum = spec.get("strum")
        if strum:
            for s in strum["steps"]:
                for k, nt in enumerate(tri + [tri[0] + 12]):
                    sig = pluck(mtof(nt + strum.get("shift", 0)), step * 1.4, 0.55, 0.994)
                    mix.add("arp", when(bar, s) + k * 0.012, sig, -0.25 + 0.17 * k, 1.0)
        # bass
        bass = spec["bass"]
        broot = tri[0] - 24
        for s, kind in bass["pat"]:
            note = broot + (7 if kind == 1 else (12 if kind == 2 else 0))
            sig = bass["fn"](mtof(note), step * bass.get("len", 0.8), 0.9)
            mix.add("bass", when(bar, s), sig, 0.0, 1.0)
        # sparkle on the bar start
        if spec.get("sparkle") and phrase_bar in (0, 2):
            sig = glock(mtof(ct[5] + 12), step * 2, 0.5)
            mix.add("sparkle", when(bar, 0) + step * 3, sig, 0.5, 1.0)
        # drums
        d = spec["drums"]
        for s in range(8):
            t = when(bar, s)
            if s in d.get("kick", ()):
                mix.add("drums", t, kick(0.95), 0.0, 1.0)
            if s in d.get("clap", ()):
                mix.add("drums", t, clap(0.8), 0.1, 1.0)
            if s in d.get("hat", ()):
                mix.add("drums", t, hat(0.8 if s % 2 == 0 else 0.55), -0.25, 1.0)
            if s in d.get("ohat", ()):
                mix.add("drums", t, hat(0.7, True), 0.25, 1.0)
            if s in d.get("shaker", ()):
                mix.add("drums", t, shaker(0.7 if s % 2 == 0 else 0.5), 0.3, 1.0)

    mix.echo("lead", step * 3, step * 5, 0.26, 0.14)
    if "arp" in mix.buses:
        mix.echo("arp", step * 2, step * 4, 0.16, 0.08)
    if "sparkle" in mix.buses:
        mix.echo("sparkle", step * 3, step * 6, 0.3, 0.2)
    L, R = mix.render(spec["gains"])
    if spec.get("lowpass"):
        L, R = lowpass_circular(L, spec["lowpass"]), lowpass_circular(R, spec["lowpass"])
    peak = max(np.max(np.abs(L)), np.max(np.abs(R)), 1e-9)
    L, R = L / peak * 0.9, R / peak * 0.9
    L, R = np.tanh(L * 1.1) / np.tanh(1.1), np.tanh(R * 1.1) / np.tanh(1.1)
    g = spec.get("outgain", 1.0)   # level match after the low-pass (keeps RMS equal to the original)
    return L * g, R * g


def save_wav(path, L, R=None, rate=SR):
    if R is None:
        data = (np.clip(L, -1, 1) * 32767).astype("<i2")
        ch = 1
    else:
        data = np.empty(len(L) * 2, dtype="<i2")
        data[0::2] = (np.clip(L, -1, 1) * 32767).astype("<i2")
        data[1::2] = (np.clip(R, -1, 1) * 32767).astype("<i2")
        ch = 2
    with wave.open(path, "wb") as w:
        w.setnchannels(ch)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(data.tobytes())


def preview(L, R):
    m = (L + R) * 0.5
    m = m[:len(m) // 2 * 2].reshape(-1, 2).mean(axis=1)
    return m


# ---------------- the five themes ----------------
# lead_pats: index into the chord tones [root, 3rd, 5th, root+8ve, 3rd+8ve, 5th+8ve] for 8 eighth notes
THEMES = [
    {
        "name": "1_sunny_skip",
        "title": "Sunny Skip",
        "desc": "C major, 124 bpm. Marimba hook, plucked bass, shaker and handclaps. Bright and bouncy.",
        "bpm": 124, "root": 60, "progA": [0, 4, 5, 3], "progB": [5, 3, 0, 4], "swing": 0.08,
        "lead": marimba, "lead_shift": 12, "lead_pan": 0.0,
        "lead_pats": [[3, None, 4, 3, 5, 4, 3, None], [4, None, 5, 4, 3, 2, 3, None], [5, 4, 3, 4, 2, None, 3, None],
                      [3, 4, 5, None, 5, 4, 3, 2]],
        "arp": {"fn": pluck, "pan": 0.4}, "arp_pat": [None, 0, None, 1, None, 2, None, 1], "arp_shift": 12,
        "bass": {"fn": tri_bass, "pat": [(0, 0), (3, 0), (4, 1), (6, 0)], "len": 0.9},
        "drums": {"kick": (0, 4), "clap": (2, 6), "shaker": (0, 1, 2, 3, 4, 5, 6, 7)},
        "sparkle": True,
        "gains": {"lead": 0.95, "arp": 0.55, "bass": 0.9, "drums": 0.75, "sparkle": 0.5},
    },
    {
        "name": "2_music_box_meadow",
        "title": "Music Box Meadow",
        "desc": "G major, 108 bpm. Music-box melody over soft glockenspiel arpeggios, gentle kick and shaker. Sweet and calm.",
        "bpm": 108, "root": 55, "progA": [0, 5, 3, 4], "progB": [3, 4, 0, 5], "swing": 0.0,
        "lead": bell, "lead_shift": 24, "lead_pan": -0.1,
        "lead_pats": [[3, None, 4, None, 5, None, 4, 3], [5, None, 4, None, 3, None, 4, None],
                      [4, 3, 4, 5, 3, None, None, None], [3, 4, 5, None, 4, 3, 4, None]],
        "arp": {"fn": glock, "pan": 0.5}, "arp_pat": [0, 1, 2, 1, 0, 1, 2, 1], "arp_shift": 12,
        "bass": {"fn": tri_bass, "pat": [(0, 0), (4, 1)], "len": 1.8},
        "drums": {"kick": (0, 4), "shaker": (2, 6)},
        "sparkle": True,
        "gains": {"lead": 0.85, "arp": 0.45, "bass": 0.85, "drums": 0.55, "sparkle": 0.45},
    },
    {
        "name": "3_pogo_party",
        "title": "Pogo Party",
        "desc": "F major, 138 bpm. Chiptune-bubbly square lead, bouncy bass, claps and hats. Fast and playful.",
        "bpm": 138, "root": 53, "progA": [0, 3, 4, 0], "progB": [5, 3, 4, 4], "swing": 0.0,
        "lead": square, "lead_shift": 24, "lead_pan": 0.0,
        "lead_pats": [[3, 5, 4, 3, 5, None, 4, None], [4, 5, 4, 3, 2, None, 3, None], [5, 4, 3, 2, 3, 4, 5, None],
                      [3, 3, 5, 5, 4, 4, 3, None]],
        "arp": {"fn": marimba, "pan": 0.5}, "arp_pat": [0, None, 2, None, 1, None, 2, None], "arp_shift": 24,
        "bass": {"fn": square, "pat": [(0, 0), (1, 0), (3, 2), (4, 0), (5, 0), (7, 1)], "len": 0.7},
        "drums": {"kick": (0, 4), "clap": (2, 6), "hat": (0, 1, 2, 3, 4, 5, 6, 7), "ohat": (3, 7)},
        "sparkle": False,
        "gains": {"lead": 0.8, "arp": 0.4, "bass": 0.55, "drums": 0.7},
    },
    {
        "name": "4_ukulele_breeze",
        "title": "Ukulele Breeze",
        "desc": "D major, 116 bpm. Strummed ukulele chords, whistled melody, handclaps and tambourine shaker. Relaxed holiday feel.",
        "bpm": 116, "root": 62, "progA": [0, 4, 5, 3], "progB": [0, 3, 4, 4], "swing": 0.12,
        "lead": whistle, "lead_shift": 12, "lead_pan": 0.0,
        "lead_pats": [[2, None, 3, 4, None, 3, 2, None], [3, None, 4, 5, None, 4, 3, None],
                      [4, 3, 2, None, 3, None, None, None], [2, 3, 4, None, 5, 4, 3, None]],
        "strum": {"steps": (0, 2, 3, 5, 7), "shift": 0},
        "bass": {"fn": pluck, "pat": [(0, 0), (4, 1)], "len": 1.2},
        "drums": {"kick": (0, 4), "clap": (2, 6), "shaker": (1, 3, 5, 7)},
        "sparkle": False,
        "gains": {"lead": 0.95, "arp": 0.7, "bass": 0.9, "drums": 0.6},
    },
    {
        "name": "5_bubble_pop",
        "title": "Bubble Pop",
        "desc": "A major, 132 bpm. Popping bubble arpeggios, pizzicato bass and sparkling chimes. Light and giggly.",
        "bpm": 132, "root": 57, "progA": [0, 5, 3, 4], "progB": [3, 4, 5, 0], "swing": 0.05,
        "lead": blip, "lead_shift": 24, "lead_pan": 0.0,
        "lead_pats": [[3, 4, 5, 4, 3, 4, 5, None], [5, 4, 3, 4, 5, None, 4, None], [4, 5, 4, 3, 4, None, 3, None],
                      [3, 5, 4, 5, 3, 4, 5, 4]],
        "arp": {"fn": glock, "pan": 0.5}, "arp_pat": [None, 2, None, 1, None, 2, None, 0], "arp_shift": 24,
        "bass": {"fn": pluck, "pat": [(0, 0), (2, 0), (4, 1), (6, 0)], "len": 0.7},
        "drums": {"kick": (0, 4), "hat": (1, 3, 5, 7), "clap": (2, 6)},
        "sparkle": True,
        "gains": {"lead": 0.9, "arp": 0.5, "bass": 0.9, "drums": 0.65, "sparkle": 0.55},
    },
    {
        "name": "1_sunny_skip_low",
        "title": "Sunny Skip (lowered)",
        "desc": "Sunny Skip transposed 5 semitones down (G major), same tempo, softer marimba and a gentle low-pass. Lowered 2026-11-13.",
        "bpm": 124, "root": 55, "progA": [0, 4, 5, 3], "progB": [5, 3, 0, 4], "swing": 0.08,
        "lead": marimba_soft, "lead_shift": 12, "lead_pan": 0.0,
        "lead_pats": [[3, None, 4, 3, 5, 4, 3, None], [4, None, 5, 4, 3, 2, 3, None], [5, 4, 3, 4, 2, None, 3, None],
                      [3, 4, 5, None, 5, 4, 3, 2]],
        "arp": {"fn": pluck, "pan": 0.4}, "arp_pat": [None, 0, None, 1, None, 2, None, 1], "arp_shift": 12,
        "bass": {"fn": tri_bass, "pat": [(0, 0), (3, 0), (4, 1), (6, 0)], "len": 0.9},
        "drums": {"kick": (0, 4), "clap": (2, 6), "shaker": (0, 1, 2, 3, 4, 5, 6, 7)},
        "sparkle": True, "lowpass": 3800, "outgain": 0.83,
        "gains": {"lead": 0.95, "arp": 0.55, "bass": 0.9, "drums": 0.7, "sparkle": 0.4},
    },
]


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join("E:/tools/audio_download", "menu_themes")
    os.makedirs(out, exist_ok=True)
    for spec in THEMES:
        L, R = compose(spec)
        save_wav(os.path.join(out, spec["name"] + ".wav"), L, R)
        save_wav(os.path.join(out, spec["name"] + "_preview.wav"), preview(L, R), None, SR // 2)
        rms = 20 * np.log10(np.sqrt(np.mean((L ** 2 + R ** 2) / 2)) + 1e-9)
        print("%-22s %5.1f s  rms %.1f dBFS  %s" % (spec["name"], len(L) / SR, rms, spec["title"]))


if __name__ == "__main__":
    main()
