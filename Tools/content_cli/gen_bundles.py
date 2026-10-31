"""Generates Content/economy/bundles.json: 1,000 distinct gem bundles (docs/26 section 3). Deterministic: the same script always writes the same file.

Run from the repo root:   python Tools/content_cli/gen_bundles.py
Writes Content/economy/bundles.json (the editable source) and the identical copy Assets/Resources/Content/bundles.json (what the game loads
at run time through Resources, see BundleCatalog in Assets/_Game/Economy). Run `python -m unittest discover -s Tests/content` afterwards.

Rules (owner brief, docs/26 and docs/22):
  - 1,000 bundles, price 200 to 500 gems in steps of 10.
  - contents: coins + 1 to 4 distinct power-up types (counts 1 to 10 each) + gems back; characterId is in the format but none is generated yet.
  - value at list rates = coins / 20 + gems back + power-ups (50 gems for Magnet, Shield, Wall Stick, Double Jump; 75 for Slow Time, Rocket, Flight)
    must be 1.4 to 1.8 times the price.
  - no two bundles share a content signature (coins, gems back, power-up counts, character); no two share a name.
"""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_SOURCE = os.path.join(ROOT, "Content", "economy", "bundles.json")
OUT_GAME = os.path.join(ROOT, "Assets", "Resources", "Content", "bundles.json")

COUNT = 1000
PRICE_MIN, PRICE_MAX, PRICE_STEP = 200, 500, 10
VALUE_MIN, VALUE_MAX = 1.4, 1.8
COINS_PER_GEM = 20            # 1 gem = 20 coins (docs/22)
MAX_DISTINCT_POWERUPS = 4
MAX_COUNT = 10
MIN_COINS = 1000

# power-up id -> gem value at list rates (docs/22 4.3). Order = shop order.
POWERUPS = [("pw_flight", 75), ("pw_rocket", 75), ("pw_doublejump", 50), ("pw_wallstick", 50),
            ("pw_shield", 50), ("pw_magnet", 50), ("pw_slowtime", 75)]
POWERUP_VALUE = dict(POWERUPS)

ADJECTIVES = ["Bouncy", "Springy", "Sunny", "Jolly", "Cheery", "Zippy", "Giddy", "Plucky", "Snazzy", "Fluffy",
              "Peppy", "Perky", "Merry", "Breezy", "Dandy", "Spiffy", "Lucky", "Mighty", "Mellow", "Wobbly",
              "Jazzy", "Bubbly", "Sparkly", "Frosty", "Toasty", "Cosmic", "Tiny", "Grand", "Royal", "Wild",
              "Happy", "Snappy", "Sleepy", "Nifty", "Quirky", "Rowdy", "Silly", "Speedy", "Golden", "Zesty"]
NOUNS = ["Boing Sack", "Tower Pack", "Cloud Kit", "Sky Stash", "Treasure Bag", "Jump Crate", "Bonus Bundle", "Goody Bag",
         "Hop Haul", "Loot Sack", "Gem Satchel", "Coin Cache", "Star Chest", "Climb Kit", "Boost Box", "Surprise Sack",
         "Pocket Haul", "Momo Mix", "Backpack", "Tower Treat", "Comet Crate", "Rainbow Bag", "Spring Stash", "Picnic Pack",
         "Cannon Crate", "Balloon Bag", "Jelly Jar", "Berry Basket", "Fireworks Box", "Moon Pouch", "Cupcake Cache", "Parade Pack",
         "Windy Wagon", "Puddle Pouch", "Lantern Lot", "Acorn Stash", "Kite Kit", "Honey Hoard", "Pebble Pack", "Muffin Mix"]


class Rng:
    """splitmix64: tiny, fully specified, identical on every Python version."""
    MASK = (1 << 64) - 1

    def __init__(self, seed):
        self.s = seed & self.MASK

    def next(self):
        self.s = (self.s + 0x9E3779B97F4A7C15) & self.MASK
        z = self.s
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & self.MASK
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & self.MASK
        return z ^ (z >> 31)

    def below(self, n):
        return self.next() % n

    def unit(self):
        return (self.next() >> 11) / float(1 << 53)

    def pick_weighted(self, weights):
        total = sum(weights)
        r = self.below(total)
        for i, w in enumerate(weights):
            if r < w:
                return i
            r -= w
        return len(weights) - 1


def value_x20(coins, gems_back, powerups):
    """Value at list rates, in units of 1/20 gem (all integers: no rounding in the checks)."""
    pw = sum(POWERUP_VALUE[pid] * n for pid, n in powerups)
    return coins + COINS_PER_GEM * (gems_back + pw)


def in_band(price, coins, gems_back, powerups):
    v = value_x20(coins, gems_back, powerups)
    return round(VALUE_MIN * 10) * COINS_PER_GEM * price <= v * 10 and v * 10 <= round(VALUE_MAX * 10) * COINS_PER_GEM * price


def tier_of(price):
    return 1 if price < 300 else (2 if price < 400 else 3)


def build_one(index, attempt):
    r = Rng(index * 1000003 + attempt * 7919 + 20261031)
    price = PRICE_MIN + PRICE_STEP * r.below((PRICE_MAX - PRICE_MIN) // PRICE_STEP + 1)
    ratio = VALUE_MIN + 0.02 + (VALUE_MAX - VALUE_MIN - 0.04) * r.unit()        # stays clear of the band edges
    target_gems = price * ratio                                                # value in gems
    ntypes = 1 + r.pick_weighted([30, 35, 22, 13])                             # 1..4 distinct types
    # choose the distinct types (partial Fisher-Yates)
    pool = list(range(len(POWERUPS)))
    chosen = []
    for _ in range(ntypes):
        chosen.append(pool.pop(r.below(len(pool))))
    chosen.sort()
    budget = target_gems * (0.22 + 0.45 * r.unit())                           # share of the value spent on power-ups
    counts = {i: 1 for i in chosen}
    spent = sum(POWERUPS[i][1] for i in chosen)
    if spent > budget:
        return None
    for _ in range(60):                                                        # grow random counts while the budget allows
        i = chosen[r.below(len(chosen))]
        if counts[i] < MAX_COUNT and spent + POWERUPS[i][1] <= budget:
            counts[i] += 1
            spent += POWERUPS[i][1]
    gems_back = 5 * r.below(int(price * 0.18) // 5 + 1)
    if r.below(5) == 0:
        gems_back = 0
    coins_gems = target_gems - spent - gems_back
    coins = int(round(coins_gems * COINS_PER_GEM / 100.0)) * 100
    if coins < MIN_COINS:
        return None
    powerups = [(POWERUPS[i][0], counts[i]) for i in chosen]
    if not in_band(price, coins, gems_back, powerups):
        return None
    return price, coins, gems_back, powerups


def signature(coins, gems_back, powerups, character):
    return (coins, gems_back, tuple(powerups), character or "")


def generate():
    names = []
    for a in ADJECTIVES:
        for n in NOUNS:
            names.append("%s %s" % (a, n))
    shuffler = Rng(31102026)
    for i in range(len(names) - 1, 0, -1):                                     # Fisher-Yates with the fixed generator
        j = shuffler.below(i + 1)
        names[i], names[j] = names[j], names[i]
    bundles, seen = [], set()
    for index in range(COUNT):
        built = None
        for attempt in range(500):
            cand = build_one(index, attempt)
            if cand is None:
                continue
            price, coins, gems_back, powerups = cand
            sig = signature(coins, gems_back, powerups, "")
            if sig in seen:
                continue
            built = cand
            seen.add(sig)
            break
        if built is None:
            raise RuntimeError("could not build bundle %d" % index)
        price, coins, gems_back, powerups = built
        bundles.append({
            "id": "bundle_%04d" % (index + 1),
            "name": names[index],
            "tier": tier_of(price),
            "priceGems": price,
            "coins": coins,
            "gems": gems_back,
            "powerups": [{"id": pid, "count": n} for pid, n in powerups],
            "characterId": None,
        })
    return bundles


def to_text(bundles):
    lines = [json.dumps(b, separators=(", ", ": "), ensure_ascii=False) for b in bundles]
    return "[\n  " + ",\n  ".join(lines) + "\n]\n"


def statistics(bundles):
    ratios = []
    for b in bundles:
        pw = [(p["id"], p["count"]) for p in b["powerups"]]
        ratios.append(value_x20(b["coins"], b["gems"], pw) / float(COINS_PER_GEM * b["priceGems"]))
    prices = [b["priceGems"] for b in bundles]
    ntypes = {}
    for b in bundles:
        ntypes[len(b["powerups"])] = ntypes.get(len(b["powerups"]), 0) + 1
    tiers = {}
    for b in bundles:
        tiers[b["tier"]] = tiers.get(b["tier"], 0) + 1
    return {
        "count": len(bundles),
        "price_min_max": (min(prices), max(prices)),
        "value_ratio_min_avg_max": (round(min(ratios), 3), round(sum(ratios) / len(ratios), 3), round(max(ratios), 3)),
        "coins_min_max": (min(b["coins"] for b in bundles), max(b["coins"] for b in bundles)),
        "gems_back_min_max": (min(b["gems"] for b in bundles), max(b["gems"] for b in bundles)),
        "distinct_powerup_types_histogram": dict(sorted(ntypes.items())),
        "tier_histogram": dict(sorted(tiers.items())),
        "distinct_names": len(set(b["name"] for b in bundles)),
    }


def main():
    bundles = generate()
    text = to_text(bundles)
    for path in (OUT_SOURCE, OUT_GAME):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        print("wrote", os.path.relpath(path, ROOT))
    for k, v in statistics(bundles).items():
        print("%s: %s" % (k, v))


if __name__ == "__main__":
    main()
