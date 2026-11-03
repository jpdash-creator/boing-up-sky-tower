"""Generates Content/economy/bundles.json: 2,000 distinct gem bundles, each with ONE hidden hero card (owner Issue 34 item 4, docs/37-bundles.md).
Deterministic: the same script always writes the same file. The set REPLACES the old 1,000 (ids are now bnd_0001..bnd_2000).

Run from the repo root:   python Tools/content_cli/gen_bundles.py
Writes Content/economy/bundles.json (the editable source) and the identical copy Assets/Resources/Content/bundles.json (what the game loads
at run time through Resources, see BundleCatalog in Assets/_Game/Economy). Run `python -m unittest discover -s Tests/content` afterwards.

Rules (docs/37):
  - 2,000 bundles. Contents: coins (steps of 50) + 1 to 4 distinct power-up types (counts 1 to 10 each) + ONE hidden hero card
    ("hiddenCard": true). Which hero is drawn when the bundle is bought, from the odds in BundleCards.cs (weights Uncommon 100, Rare 40,
    Elite 20, Legendary 5); the file never names a hero.
  - Worth (in gems) = coins / 20 + power-ups at their gem prices (50 or 75) + the EXPECTED value of the card (same for every bundle).
    Price = Worth / 1.6, rounded to 10 gems, with the coins sized so the ratio Worth / price is 1.6 within 0.01 for EVERY bundle:
    a cheap bundle is never better value than an expensive one (the old 1.4 to 1.8 spread is gone).
  - Card value table below must equal BundleCards.TierUsd / Weights / UsdPerGem in Assets/_Game/Economy/BundleCards.cs (a unit test compares them).
  - no two bundles share a content signature (coins, power-up counts); no two share a name; names are at most 24 characters.
"""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_SOURCE = os.path.join(ROOT, "Content", "economy", "bundles.json")
OUT_GAME = os.path.join(ROOT, "Assets", "Resources", "Content", "bundles.json")

COUNT = 2000
ID_PREFIX = "bnd_"
PRICE_MIN, PRICE_MAX, PRICE_STEP = 540, 3000, 10
RATIO = 1.6                    # Worth / price, the same for every bundle (the old pool averaged 1.6)
RATIO_TOL = 0.01
COIN_STEP = 50
COINS_PER_GEM = 20             # 1 gem = 20 coins (docs/22)
MAX_DISTINCT_POWERUPS = 4
MAX_COUNT = 10
MIN_COINS = 1000
USD_PER_GEM = 0.011            # SubscriptionCatalog.UsdPerGem

# hidden card: tier -> (draw weight, placeholder dollar value). Order = Uncommon, Rare, Elite (CharacterRarity.Epic), Legendary.
CARD_TIERS = [("Uncommon", 100, 2.99), ("Rare", 40, 7.99), ("Elite", 20, 19.99), ("Legendary", 5, 49.99)]
CARD_WEIGHT_TOTAL = sum(w for _, w, _ in CARD_TIERS)
CARD_EXPECTED_USD = sum(w * usd for _, w, usd in CARD_TIERS) / CARD_WEIGHT_TOTAL
CARD_EXPECTED_GEMS = CARD_EXPECTED_USD / USD_PER_GEM

# power-up id -> gem value at list rates (docs/22 4.3). Order = shop order.
POWERUPS = [("pw_flight", 75), ("pw_rocket", 75), ("pw_doublejump", 50), ("pw_wallstick", 50),
            ("pw_shield", 50), ("pw_magnet", 50), ("pw_slowtime", 75)]
POWERUP_VALUE = dict(POWERUPS)

ADJECTIVES = ["Bouncy", "Springy", "Sunny", "Jolly", "Cheery", "Zippy", "Giddy", "Plucky", "Snazzy", "Fluffy",
              "Peppy", "Perky", "Merry", "Breezy", "Dandy", "Spiffy", "Lucky", "Mighty", "Mellow", "Wobbly",
              "Jazzy", "Bubbly", "Sparkly", "Frosty", "Toasty", "Cosmic", "Tiny", "Grand", "Royal", "Wild",
              "Happy", "Snappy", "Sleepy", "Nifty", "Quirky", "Rowdy", "Silly", "Speedy", "Golden", "Zesty",
              "Dapper", "Frisky", "Glowy", "Hearty", "Jaunty", "Kooky", "Lofty", "Nimble", "Peachy", "Plump",
              "Rosy", "Savvy", "Swift", "Twinkly", "Velvet", "Whimsy", "Woolly", "Zany", "Brave", "Candied"]
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


def worth_gems(coins, powerups):
    """Total value in gems: coins / 20 + power-ups at list rates + the expected hidden card."""
    pw = sum(POWERUP_VALUE[pid] * n for pid, n in powerups)
    return coins / float(COINS_PER_GEM) + pw + CARD_EXPECTED_GEMS


def worth_ratio(price, coins, powerups):
    return worth_gems(coins, powerups) / float(price)


def tier_of(price):
    return 1 if price < 1000 else (2 if price < 2000 else 3)


def build_one(index, attempt):
    r = Rng(index * 1000003 + attempt * 7919 + 20261103)
    steps = (PRICE_MAX - PRICE_MIN) // PRICE_STEP
    price = PRICE_MIN + PRICE_STEP * int(steps * (r.unit() ** 1.5) + 0.5)       # more small and middle bundles than huge ones
    target = price * RATIO                                                     # total value in gems
    content = target - CARD_EXPECTED_GEMS                                      # coins + power-ups share of it
    ntypes = 1 + r.pick_weighted([30, 35, 22, 13])                             # 1..4 distinct types
    pool = list(range(len(POWERUPS)))
    chosen = []
    for _ in range(ntypes):
        chosen.append(pool.pop(r.below(len(pool))))
    chosen.sort()
    budget = content * (0.20 + 0.40 * r.unit())                                # share of the content spent on power-ups
    counts = {i: 1 for i in chosen}
    spent = sum(POWERUPS[i][1] for i in chosen)
    if spent > budget:
        return None
    for _ in range(300):                                                       # grow random counts while the budget allows
        i = chosen[r.below(len(chosen))]
        if counts[i] < MAX_COUNT and spent + POWERUPS[i][1] <= budget:
            counts[i] += 1
            spent += POWERUPS[i][1]
    coins_gems = content - spent
    coins = int(round(coins_gems * COINS_PER_GEM / float(COIN_STEP))) * COIN_STEP
    if coins < MIN_COINS:
        return None
    powerups = [(POWERUPS[i][0], counts[i]) for i in chosen]
    if abs(worth_ratio(price, coins, powerups) - RATIO) > RATIO_TOL:
        return None
    return price, coins, powerups


def signature(coins, powerups):
    return (coins, tuple(powerups))


def make_names():
    names = []
    for a in ADJECTIVES:
        for n in NOUNS:
            names.append("%s %s" % (a, n))
    shuffler = Rng(31102026)
    for i in range(len(names) - 1, 0, -1):                                     # Fisher-Yates with the fixed generator
        j = shuffler.below(i + 1)
        names[i], names[j] = names[j], names[i]
    return names


def generate():
    names = make_names()
    bundles, seen = [], set()
    for index in range(COUNT):
        built = None
        for attempt in range(500):
            cand = build_one(index, attempt)
            if cand is None:
                continue
            price, coins, powerups = cand
            sig = signature(coins, powerups)
            if sig in seen:
                continue
            built = cand
            seen.add(sig)
            break
        if built is None:
            raise RuntimeError("could not build bundle %d" % index)
        price, coins, powerups = built
        bundles.append({
            "id": "%s%04d" % (ID_PREFIX, index + 1),
            "name": names[index],
            "tier": tier_of(price),
            "priceGems": price,
            "coins": coins,
            "powerups": [{"id": pid, "count": n} for pid, n in powerups],
            "hiddenCard": True,
        })
    return bundles


def to_text(bundles):
    lines = [json.dumps(b, separators=(", ", ": "), ensure_ascii=False) for b in bundles]
    return "[\n  " + ",\n  ".join(lines) + "\n]\n"


def statistics(bundles):
    ratios, worth_usd = [], []
    for b in bundles:
        pw = [(p["id"], p["count"]) for p in b["powerups"]]
        ratios.append(worth_ratio(b["priceGems"], b["coins"], pw))
        worth_usd.append(worth_gems(b["coins"], pw) * USD_PER_GEM)
    prices = [b["priceGems"] for b in bundles]
    ntypes, tiers = {}, {}
    for b in bundles:
        ntypes[len(b["powerups"])] = ntypes.get(len(b["powerups"]), 0) + 1
        tiers[b["tier"]] = tiers.get(b["tier"], 0) + 1
    return {
        "count": len(bundles),
        "card_expected_usd": round(CARD_EXPECTED_USD, 4),
        "price_gems_min_max": (min(prices), max(prices)),
        "worth_usd_min_max": (round(min(worth_usd), 2), round(max(worth_usd), 2)),
        "worth_ratio_min_avg_max": (round(min(ratios), 4), round(sum(ratios) / len(ratios), 4), round(max(ratios), 4)),
        "coins_min_max": (min(b["coins"] for b in bundles), max(b["coins"] for b in bundles)),
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
