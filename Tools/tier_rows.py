"""Draws the merge-tier rows of Region 1 (F4.1) exactly like TierRules.Generate does (same SplitMix32-style generator, same chain exactness rule) and prints
C# TierRow lines for MergeTierDefs.Rows. Pure arithmetic, no game code runs. Nothing is random at run time: the printed numbers are pasted as constants.

    python Tools/tier_rows.py

Seeds: the five original Uncommon rows used TierRules.BaseSeed + line index (Momo 0 ... Bao 4). The Region 1 rows use BaseSeed + line index + 1000 * tier
(tier 1 Uncommon, 2 Rare, 3 Elite, 4 Legendary) so every row has its own seed; Momo's Uncommon row is the existing constant and is not redrawn.
A hero with an identity stat always gets that stat boosted first (Bumble speed, Ribbi jump, Dandy control, Pip wall bounce), the other boosted stats are drawn.
"""
from decimal import Decimal, ROUND_HALF_UP
import math

BASE_SEED = 20261104
LINES = ["momo", "forestgirl", "beachotter", "candygirl", "monk", "bumble", "ribbi", "dandy", "pip", "nib", "spike", "moss", "fenn", "pinch", "shelly", "bloop", "coco"]
# Common stats (launch, speed, control, wall hundredths) of the Region 1 heroes (Characters.cs)
COMMON = {"momo": (19, 9, 30, 77), "bumble": (19, 10, 31, 76), "ribbi": (21, 9, 28, 74), "dandy": (19, 8, 33, 72), "pip": (19, 9, 30, 84),
          "forestgirl": (19, 10, 29, 79), "nib": (19, 10, 29, 75), "spike": (20, 8, 28, 80), "moss": (19, 8, 31, 85), "fenn": (20, 10, 31, 74),
          "beachotter": (18, 8, 32, 72), "pinch": (19, 8, 29, 82), "shelly": (18, 8, 33, 74), "bloop": (21, 9, 28, 73), "coco": (19, 10, 30, 75)}
IDENTITY = {"momo": None, "forestgirl": None, "bumble": 1, "ribbi": 0, "dandy": 2, "pip": 3, "nib": None, "spike": None, "moss": 3, "fenn": None,
            "beachotter": None, "pinch": 3, "shelly": 2, "bloop": 0, "coco": None}
BIG_RUN = {"nib", "fenn", "coco"}   # identity is the run bonus / coin find: the best of two draws for the run coins and gems   # index into (Jump, Speed, Control, Wall)
RULES = {  # rarity: (boosted stats, coins min, coins max, gems min, gems max)
    1: (2, 50, 100, 1, 5),
    2: (3, 200, 500, 5, 10),
    3: (3, 500, 1000, 5, 10),
    4: (4, 1000, 2000, 5, 10),
}
PCT_MIN, PCT_MAX = 10, 25
TIER_NAMES = {1: "Uncommon", 2: "Rare", 3: "Epic", 4: "Legendary"}
NAMES = {
    ("momo", 2): "Cloudknight Momo", ("momo", 3): "Skyplate Momo", ("momo", 4): "Sunlord Momo",
    ("bumble", 1): "Honeycloak Bumble", ("bumble", 2): "Hivemage Bumble", ("bumble", 3): "Combweaver Bumble", ("bumble", 4): "Archmage Bumble",
    ("ribbi", 1): "Leafcrown Ribbi", ("ribbi", 2): "Barkplate Ribbi", ("ribbi", 3): "Mossknight Ribbi", ("ribbi", 4): "Jadecrown Ribbi",
    ("dandy", 1): "Seedling Dandy", ("dandy", 2): "Blossom Dandy", ("dandy", 3): "Petalcloak Dandy", ("dandy", 4): "Bloomqueen Dandy",
    ("forestgirl", 2): "Acornplate Capi", ("forestgirl", 3): "Oakmantle Capi", ("forestgirl", 4): "Grovequeen Capi",
    ("nib", 1): "Leafscarf Nib", ("nib", 2): "Barkguard Nib", ("nib", 3): "Oakknight Nib", ("nib", 4): "Acornking Nib",
    ("spike", 1): "Logplate Spike", ("spike", 2): "Thornguard Spike", ("spike", 3): "Oakbrace Spike", ("spike", 4): "Quillking Spike",
    ("moss", 1): "Vinebelt Moss", ("moss", 2): "Shellplate Moss", ("moss", 3): "Ferncrown Moss", ("moss", 4): "Shellking Moss",
    ("fenn", 1): "Scoutvest Fenn", ("fenn", 2): "Coinguard Fenn", ("fenn", 3): "Goldcape Fenn", ("fenn", 4): "Foxking Fenn",
    ("beachotter", 2): "Shellplate Otto", ("beachotter", 3): "Wavecloak Otto", ("beachotter", 4): "Pearlking Otto",
    ("pinch", 1): "Shellvest Pinch", ("pinch", 2): "Clawguard Pinch", ("pinch", 3): "Tidecrown Pinch", ("pinch", 4): "Reefking Pinch",
    ("shelly", 1): "Coralstrap Shelly", ("shelly", 2): "Reefplate Shelly", ("shelly", 3): "Pearltiara Shelly", ("shelly", 4): "Coralqueen Shelly",
    ("bloop", 1): "Coralband Bloop", ("bloop", 2): "Reefmail Bloop", ("bloop", 3): "Bubblecrown Bloop", ("bloop", 4): "Bubbleking Bloop",
    ("coco", 1): "Palmwrap Coco", ("coco", 2): "Leafmail Coco", ("coco", 3): "Junglecape Coco", ("coco", 4): "Palmking Coco",
    ("pip", 1): "Redcape Pip", ("pip", 2): "Crownguard Pip", ("pip", 3): "Thornplate Pip", ("pip", 4): "Goldshell Pip",
}
SUFFIX = {1: "u", 2: "r", 3: "e", 4: "l"}


def r1(v):
    """One decimal, half away from zero on the value rounded to 15 significant digits (what .NET prints for "0.0")."""
    return float(Decimal(format(v, ".14e")).quantize(Decimal("0.1"), rounding=ROUND_HALF_UP))   # .NET "0.0" formats from 15 significant digits (4.05 prints 4.1)


def boosted(prev, pct):
    return prev if pct == 0 else r1(prev * (100 + pct) / 100.0)


def percent(prev, now):
    if prev <= 0:
        return 0
    return int(Decimal((now / prev - 1.0) * 100.0).quantize(Decimal("1"), rounding=ROUND_HALF_UP))


def is_exact(prev, pct):
    return percent(prev, boosted(prev, pct)) == pct


def common_shown(line):
    l, sp, ct, w = COMMON[line]
    return [r1(l * l / 80.0), float(sp), float(ct), r1(sp * w / 100.0)]


class Rng:
    def __init__(self, seed):
        self.s = seed & 0xFFFFFFFF

    def nxt(self):
        self.s = (self.s + 0x9E3779B9) & 0xFFFFFFFF
        z = self.s
        z = ((z ^ (z >> 16)) * 0x85EBCA6B) & 0xFFFFFFFF
        z = ((z ^ (z >> 13)) * 0xC2B2AE35) & 0xFFFFFFFF
        return z ^ (z >> 16)

    def rng(self, lo, hi):
        return lo + self.nxt() % (hi - lo + 1)


def generate(line, rarity, prev, seed):
    count, cmin, cmax, gmin, gmax = RULES[rarity]
    rng = Rng(seed)
    order = [0, 1, 2, 3]
    ident = IDENTITY[line]
    if ident is not None:
        order.remove(ident)
        order.insert(0, ident)
        start = 1   # the identity stat stays first; only the rest is shuffled
    else:
        start = 0
    for i in range(start, count):
        j = i + rng.rng(0, len(order) - 1 - i)
        order[i], order[j] = order[j], order[i]
    chosen = set(order[:count])
    pcts = [0, 0, 0, 0]
    for s in range(4):
        if s not in chosen:
            continue
        before = prev[s]
        pct = rng.rng(PCT_MIN, PCT_MAX)
        tries = 0
        while tries < 64 and not is_exact(before, pct):
            pct = rng.rng(PCT_MIN, PCT_MAX)
            tries += 1
        p = PCT_MIN
        while not is_exact(before, pct) and p <= PCT_MAX:
            pct = p
            p += 1
        pcts[s] = pct
    coins = rng.rng(cmin // 10, cmax // 10) * 10
    gems = rng.rng(gmin, gmax)
    if line in BIG_RUN:
        coins = max(coins, rng.rng(cmin // 10, cmax // 10) * 10)
        gems = max(gems, rng.rng(gmin, gmax))
    return pcts, coins, gems


def apply(prev, pcts):
    return [boosted(prev[i], pcts[i]) for i in range(4)]


EXISTING_U = {"momo": ([0, 17, 0, 13], 80, 4), "forestgirl": ([20, 0, 13, 0], 90, 1), "beachotter": ([15, 0, 0, 12], 50, 2)}   # constants already in MergeTierDefs.Rows


def main():
    for li, line in enumerate(LINES):
        if line not in COMMON:
            continue
        prev = common_shown(line)
        print("// %s common shown: Jump %.1f Speed %.1f Control %.1f Wall %.1f" % (line, *prev))
        for rarity in (1, 2, 3, 4):
            if rarity == 1 and line in EXISTING_U:
                pcts, coins, gems = EXISTING_U[line]
            else:
                pcts, coins, gems = generate(line, rarity, prev, BASE_SEED + li + 1000 * rarity)
            if rarity == 1 and line in EXISTING_U:
                nxt = apply(prev, pcts)
                prev = nxt
                continue
            tid = line + "_" + SUFFIX[rarity]
            fields = []
            for lab, v in zip(("Jump", "Speed", "Control", "Wall"), pcts):
                if v:
                    fields.append("%sPct = %d" % (lab, v))
            nxt = apply(prev, pcts)
            for i in range(4):
                if pcts[i]:
                    assert is_exact(prev[i], pcts[i])
            print('new TierRow { LineId = "%s", Id = "%s", ArtId = "%s", Name = "%s", Rarity = CharacterRarity.%s, %s, RunCoinsAdd = %d, RunGemsAdd = %d },   // shown %.1f / %.1f / %.1f / %.1f' % (
                line, tid, tid, NAMES[(line, rarity)], TIER_NAMES[rarity], ", ".join(fields), coins, gems, *nxt))
            prev = nxt


if __name__ == "__main__":
    main()
