"""Synthesises the original music and jingles (owner issue list 18, 2026-10-24). No samples, no downloads: everything is generated here.

    PYTHONPATH=E:/pylibs python Tools/audio_gen/make_music.py

Writes Assets/Resources/Audio/music/loop.ogg (menu, calm), region_1..30.ogg (one bright loop per region) and
Assets/Resources/Audio/sfx/{win,fail,fall}.ogg. Loops are bar-aligned and wrap seamlessly (the tail of every note is folded onto the start).
Everything is major or lydian/mixolydian/pentatonic: warm and light, never ominous.
"""
import os
import numpy as np
import soundfile as sf

SR = 32000
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "Audio")

MODES = {
    "major": [0, 2, 4, 5, 7, 9, 11],
    "lydian": [0, 2, 4, 6, 7, 9, 11],
    "mixo": [0, 2, 4, 5, 7, 9, 10],
    "penta": [0, 2, 4, 7, 9],
}
PROGS = [  # chord roots as scale degrees per bar, 8 bars
    [0, 5, 3, 4, 0, 5, 3, 4],
    [0, 3, 4, 0, 0, 3, 4, 4],
    [0, 4, 5, 3, 0, 4, 5, 3],
    [0, 2, 3, 4, 0, 2, 3, 4],
    [5, 3, 0, 4, 5, 3, 0, 4],
]


def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12.0)


def env(n, a=0.01, d=0.2, s=0.6, r=0.1):
    e = np.ones(n) * s
    ai = max(1, int(a * SR))
    e[:ai] = np.linspace(0, 1, ai)
    di = max(1, int(d * SR))
    seg = e[ai:ai + di]
    e[ai:ai + di] = np.linspace(1, s, len(seg)) if len(seg) else seg
    ri = max(1, min(n, int(r * SR)))
    e[-ri:] *= np.linspace(1, 0, ri)
    return e


def tone(freq, dur, kind="tri", vib=0.0, a=0.01, d=0.2, s=0.6, r=0.1):
    n = max(8, int(dur * SR))
    t = np.arange(n) / SR
    ph = 2 * np.pi * freq * t + (vib * np.sin(2 * np.pi * 5.2 * t) if vib else 0)
    if kind == "sine":
        w = np.sin(ph)
    elif kind == "tri":
        w = 2 / np.pi * np.arcsin(np.sin(ph))
    elif kind == "pluck":
        w = np.sin(ph) + 0.4 * np.sin(2 * ph) * np.exp(-t * 6) + 0.2 * np.sin(3 * ph) * np.exp(-t * 9)
        return w * np.exp(-t * 3.2) * env(n, 0.004, 0.0, 1.0, 0.05)
    elif kind == "bell":
        w = np.sin(ph) + 0.5 * np.sin(2.76 * ph) * np.exp(-t * 5) + 0.25 * np.sin(5.4 * ph) * np.exp(-t * 8)
        return w * np.exp(-t * 2.2) * env(n, 0.002, 0.0, 1.0, 0.05)
    else:
        w = np.sin(ph) + 0.25 * np.sin(2 * ph)
    return w * env(n, a, d, s, r)


def noise_hit(dur):
    n = int(dur * SR)
    x = np.random.RandomState(7).randn(n)
    return x * np.exp(-np.arange(n) / SR * 40)


def add(buf, sig, start, gain, wrap=True):
    i = int(start * SR)
    n = len(sig)
    L = len(buf)
    if i >= L:
        return
    end = min(L, i + n)
    buf[i:end] += sig[:end - i] * gain
    if wrap and i + n > L:   # fold the tail onto the start so the loop is seamless
        rest = min(i + n - L, L)
        buf[:rest] += sig[n - rest:] * gain


def scale_note(mode, root, degree):
    sc = MODES[mode]
    o, d = divmod(degree, len(sc))
    return root + sc[d] + 12 * o


def loop(seed, root, mode, bpm, lead, prog, density=0.6, mood=1.0, bars=8):
    rng = np.random.RandomState(seed)
    beat = 60.0 / bpm
    bar = 4 * beat
    buf = np.zeros(int(bar * bars * SR))
    prog = PROGS[prog % len(PROGS)]
    for b in range(bars):
        cr = prog[b % len(prog)]
        t0 = b * bar
        triad = [scale_note(mode, root, cr + k) for k in (0, 2, 4)]
        for nn in triad:
            add(buf, tone(midi(nn + 12), bar * 1.02, "sine", a=0.25, d=0.5, s=0.7, r=0.3), t0, 0.07 * mood)
        add(buf, tone(midi(triad[0] - 12), beat * 1.7, "sine", a=0.01, d=0.3, s=0.6, r=0.1), t0, 0.22)
        add(buf, tone(midi(triad[0] - 12 + (7 if b % 2 else 0)), beat * 1.5, "sine", a=0.01, d=0.3, s=0.5, r=0.1), t0 + 2 * beat, 0.18)
        arp = [triad[0] + 12, triad[1] + 12, triad[2] + 12, triad[1] + 12]
        for k in range(8):
            if rng.rand() < 0.88:
                add(buf, tone(midi(arp[k % 4] + (12 if k == 6 else 0)), beat, "pluck"), t0 + k * beat / 2, 0.10)
        for k in range(4):
            add(buf, noise_hit(0.04), t0 + k * beat + beat / 2, 0.03)
            if k in (0, 2):
                kn = int(0.12 * SR)
                kick = np.sin(2 * np.pi * 95 * np.arange(kn) / SR) * np.exp(-np.arange(kn) / SR * 22)
                add(buf, kick, t0 + k * beat, 0.16)
        pos = 0.0
        deg = cr + 7
        while pos < bar - 1e-6:
            dur = beat * rng.choice([0.5, 1.0, 1.0, 1.5, 2.0], p=[0.25, 0.3, 0.2, 0.1, 0.15])
            dur = min(dur, bar - pos)
            if rng.rand() < density:
                step = rng.choice([-2, -1, 0, 1, 2, 3], p=[0.1, 0.25, 0.1, 0.25, 0.2, 0.1])
                deg = int(np.clip(deg + step, cr + 2, cr + 11))
                if pos == 0.0 and b % 2 == 0:
                    deg = cr + 7 + int(rng.choice([0, 2, 4]))   # chord tone on the downbeat
                add(buf, tone(midi(scale_note(mode, root, deg) + 12), dur * 0.95, lead, vib=0.4 if lead == "tri" else 0.0), t0 + pos, 0.16 * mood)
            pos += dur
    peak = np.max(np.abs(buf)) or 1
    return (buf / peak * 0.85).astype(np.float32)


def write(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sf.write(path, data, SR, format="OGG", subtype="VORBIS")
    print("wrote", os.path.relpath(path, ROOT), "%.1fs" % (len(data) / SR))


def jingle(notes, step, kind, gain, tail):
    buf = np.zeros(int((step * len(notes) + tail) * SR))
    for i, (n, d) in enumerate(notes):
        add(buf, tone(midi(n), d, kind, a=0.005, d=0.3, s=0.6, r=0.2), i * step, gain, wrap=False)
    return buf


def main():
    music = os.path.join(ROOT, "music")
    # Menu bed: slow, warm, major. Calm and friendly (the old one read as ominous).
    write(os.path.join(music, "loop.ogg"), loop(11, 60, "major", 84, "sine", 0, density=0.5, mood=0.8))
    roots = [60, 62, 65, 67, 59, 64]
    modes = ["major", "lydian", "mixo", "penta", "major", "lydian"]
    leads = ["tri", "sine", "pluck", "bell"]
    for r in range(1, 31):
        s = r - 1
        root = roots[s % 6] + (0 if s < 15 else 2)
        mode = modes[(s * 5 + s // 6) % len(modes)]
        bpm = 100 + (s * 7) % 28
        lead = leads[(s // 2) % 4] if mode != "penta" else "bell"
        write(os.path.join(music, "region_%d.ogg" % r), loop(100 + r, root, mode, bpm, lead, (s * 3) % 5, density=0.55 + 0.02 * (s % 6)))
    sfx = os.path.join(ROOT, "sfx")
    # Level clear: bright rising arpeggio over a held major chord.
    win = jingle([(72, 0.5), (76, 0.5), (79, 0.5), (84, 0.5), (88, 1.2)], 0.16, "bell", 0.3, 1.6)
    for n in (72, 76, 79, 84):
        add(win, tone(midi(n), 1.6, "sine", a=0.02, d=0.4, s=0.5, r=0.6), 0.8, 0.07, wrap=False)
    write(os.path.join(sfx, "win.ogg"), (win / np.max(np.abs(win)) * 0.9).astype(np.float32))
    # You fell: slow, sad, falling line.
    fail = jingle([(72, 0.7), (70, 0.7), (67, 0.7), (63, 0.7), (60, 2.0)], 0.42, "tri", 0.3, 2.2)
    for n in (60, 63, 67):
        add(fail, tone(midi(n - 12), 2.4, "sine", a=0.05, d=0.5, s=0.5, r=0.9), 1.4, 0.08, wrap=False)
    write(os.path.join(sfx, "fail.ogg"), (fail / np.max(np.abs(fail)) * 0.9).astype(np.float32))
    # Fall (the hit / drop sound): a short downward slide.
    n = int(0.9 * SR)
    t = np.arange(n) / SR
    ph = 2 * np.pi * np.cumsum(520 * np.exp(-t * 1.6)) / SR
    slide = (np.sin(ph) * 0.8 + 0.2 * np.sin(2 * ph)) * np.exp(-t * 2.4) * np.minimum(1, t * 80)
    write(os.path.join(sfx, "fall.ogg"), (slide * 0.8).astype(np.float32))


if __name__ == "__main__":
    main()
