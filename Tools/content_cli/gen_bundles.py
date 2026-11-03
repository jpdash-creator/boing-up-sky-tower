"""Generates Content/economy/bundles.json: 2,000 distinct gem bundles, each with ONE hidden hero card (owner Bug List 1 B4.a, docs/37-bundles.md).
Deterministic: the same script always writes the same file. The set REPLACES the previous pool (540 to 3,000 gems): ids stay bnd_0001..bnd_2000
but every bundle is new (new seed, new names, new prices), and the save's pool version (BundleCatalog.PoolVersion = 2) makes old saved offers redraw.

Run from the repo root:   python Tools/content_cli/gen_bundles.py
Writes Content/economy/bundles.json (the editable source) and the identical copy Assets/Resources/Content/bundles.json (what the game loads
at run time through Resources, see BundleCatalog in Assets/_Game/Economy). Run `python -m unittest discover -s Tests/content` afterwards.

Rules (docs/37):
  - 2,000 bundles. Price 200 to 600 gems (steps of 10), nothing else.
  - Contents: coins (steps of 50) + 1 to 4 distinct power-up types (counts 1 to 10 each) + ONE hidden hero card ("hiddenCard": true).
    Which hero is drawn when the bundle is bought (odds Uncommon 70, Rare 20, Epic 10; only heroes of open regions) is decided by the game
    (BundleCards.cs); the file never names a hero.
  - Worth in gems = coins / 20 + power-ups at their gem prices (50 or 75) + the EXPECTED gem value of the card (CARD_TIER_GEMS below, equal to
    BundleCards.TierGems). The worth multiplier = worth / price is drawn per bundle between 2.0 and 3.5 (varied, not constant) and the coins are
    sized so the real ratio lands within 0.02 of the drawn one, always inside 2.0 to 3.5.
  - The card table below must equal BundleCards.TierGems / Weights in Assets/_Game/Economy/BundleCards.cs (a unit test compares them).
  - no two bundles share a content signature (coins, power-up counts); no two share a name; names are at most 24 characters and none
    equals a name of the previous pool (the adjectives are all new).
"""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_SOURCE = os.path.join(ROOT, "Content", "economy", "bundles.json")
OUT_GAME = os.path.join(ROOT, "Assets", "Resources", "Content", "bundles.json")

COUNT = 2000
ID_PREFIX = "bnd_"
PRICE_MIN, PRICE_MAX, PRICE_STEP = 200, 600, 10
WORTH_MIN, WORTH_MAX = 2.0, 3.5          # worth multiplier bounds (worth in gems / gems paid)
WORTH_MARGIN = 0.02                      # drawn targets stay this far inside the bounds
RATIO_TOL = 0.02                         # the real ratio is within this of the drawn target
COIN_STEP = 50
COINS_PER_GEM = 20                       # 1 gem = 20 coins (docs/22)
MAX_DISTINCT_POWERUPS = 4
MAX_COUNT = 10
MIN_COINS = 1000
SEED_SALT = 20261105                     # new seed (the previous pool used 20261103)
NAME_SEED = 5112026                      # new name shuffle (the previous pool used 31102026)

# hidden card: tier -> (draw weight, gem value). Order = Uncommon, Rare, Elite (CharacterRarity.Epic). Equal to BundleCards.Weights / TierGems.
CARD_TIERS = [("Uncommon", 70, 100), ("Rare", 20, 250), ("Elite", 10, 600)]
CARD_WEIGHT_TOTAL = sum(w for _, w, _ in CARD_TIERS)
CARD_EXPECTED_GEMS = sum(w * g for _, w, g in CARD_TIERS) / float(CARD_WEIGHT_TOTAL)

# power-up id -> gem value at list rates (docs/22 4.3). Order = shop order.
POWERUPS = [("pw_flight", 75), ("pw_rocket", 75), ("pw_doublejump", 50), ("pw_wallstick", 50),
            ("pw_shield", 50), ("pw_magnet", 50), ("pw_slowtime", 75)]
POWERUP_VALUE = dict(POWERUPS)

ADJECTIVES = ["Gleeful", "Snuggly", "Hoppy", "Flashy", "Cuddly", "Dreamy", "Fizzy", "Gutsy", "Humble", "Minty",
              "Noble", "Pixie", "Rapid", "Shiny", "Tidy", "Vivid", "Witty", "Yummy", "Amber", "Blazing",
              "Crisp", "Dusty", "Eager", "Fabled", "Gentle", "Hasty", "Ivory", "Jumbo", "Lively", "Misty",
              "Nutty", "Oaken", "Pearly", "Quaint", "Rustic", "Sassy", "Tangy", "Upbeat", "Wacky", "Zingy",
              "Cozy", "Daring", "Fancy", "Gallant", "Honeyed", "Jiggly", "Kingly", "Lunar", "Mossy", "Neon",
              "Perfect", "Radiant", "Sugary", "Tropical", "Unbeaten", "Windswept"]
NOUNS = ["Pogo Pack", "Sprout Sack", "Nimbus Nook", "Beanstalk Bag", "Rocket Roll", "Bubble Box", "Cocoa Cache", "Bounce Tote",
         "Echo Chest", "Feather Fort", "Gadget Grab", "Harvest Haul", "Sundae Crate", "Jetpack Jar", "Kettle Kit", "Lollipop Lot",
         "Marble Mix", "Noodle Nest", "Orbit Outfit", "Pancake Pile", "Quest Quiver", "Ribbon Roll", "Sunbeam Sack", "Treetop Tote",
         "Umbrella Unit", "Velcro Vault", "Waffle Wagon", "Yo-yo Yard", "Zeppelin Zip", "Biscuit Box", "Cricket Cart", "Dewdrop Drop",
         "Ember Express", "Fiddle Fund", "Glider Grab", "Hammock Hoard", "Igloo Idea", "Jamboree Jar", "Kaleido Kit", "Lighthouse Lot"]


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
    """Bag size: 1 below 300 gems, 2 below 450, else 3."""
    return 1 if price < 300 else (2 if price < 450 else 3)


def build_one(index, attempt):
    r = Rng(index * 1000003 + attempt * 7919 + SEED_SALT)
    steps = (PRICE_MAX - PRICE_MIN) // PRICE_STEP
    price = PRICE_MIN + PRICE_STEP * int(steps * (r.unit() ** 1.2) + 0.5)       # a few more small and middle bundles than big ones
    lo, hi = WORTH_MIN + WORTH_MARGIN, WORTH_MAX - WORTH_MARGIN
    target = lo + (hi - lo) * r.unit()                                           # drawn worth multiplier, uniform over the whole 2.0 to 3.5 span
    content = price * target - CARD_EXPECTED_GEMS                                # coins + power-ups share of the worth, in gems
    ntypes = 1 + r.pick_weighted([30, 35, 22, 13])                               # 1..4 distinct types
    pool = list(range(len(POWERUPS)))
    chosen = []
    for _ in range(ntypes):
        chosen.append(pool.pop(r.below(len(pool))))
    chosen.sort()
    budget = content * (0.20 + 0.40 * r.unit())                                  # share of the content spent on power-ups
    counts = {i: 1 for i in chosen}
    spent = sum(POWERUPS[i][1] for i in chosen)
    if spent > budget:
        return None
    for _ in range(300):                                                         # grow random counts while the budget allows
        i = chosen[r.below(len(chosen))]
        if counts[i] < MAX_COUNT and spent + POWERUPS[i][1] <= budget:
            counts[i] += 1
            spent += POWERUPS[i][1]
    coins_gems = content - spent
    coins = int(round(coins_gems * COINS_PER_GEM / float(COIN_STEP))) * COIN_STEP
    if coins < MIN_COINS:
        return None
    powerups = [(POWERUPS[i][0], counts[i]) for i in chosen]
    ratio = worth_ratio(price, coins, powerups)
    if abs(ratio - target) > RATIO_TOL or not (WORTH_MIN <= ratio <= WORTH_MAX):
        return None
    return price, coins, powerups


def signature(coins, powerups):
    return (coins, tuple(powerups))


def make_names():
    names = []
    for a in ADJECTIVES:
        for n in NOUNS:
            names.append("%s %s" % (a, n))
    shuffler = Rng(NAME_SEED)
    for i in range(len(names) - 1, 0, -1):                                       # Fisher-Yates with the fixed generator
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
    ratios = []
    for b in bundles:
        pw = [(p["id"], p["count"]) for p in b["powerups"]]
        ratios.append(worth_ratio(b["priceGems"], b["coins"], pw))
    prices = [b["priceGems"] for b in bundles]
    ntypes, tiers = {}, {}
    for b in bundles:
        ntypes[len(b["powerups"])] = ntypes.get(len(b["powerups"]), 0) + 1
        tiers[b["tier"]] = tiers.get(b["tier"], 0) + 1
    price_bands = {}
    for p in prices:
        band = "%d-%d" % (p // 100 * 100, p // 100 * 100 + 99)
        price_bands[band] = price_bands.get(band, 0) + 1
    ratio_bands = {}
    for x in ratios:
        band = "%.1f-%.1f" % (int(x * 2) / 2.0, int(x * 2) / 2.0 + 0.5)
        ratio_bands[band] = ratio_bands.get(band, 0) + 1
    return {
        "count": len(bundles),
        "card_expected_gems": round(CARD_EXPECTED_GEMS, 4),
        "price_gems_min_avg_max": (min(prices), round(sum(prices) / float(len(prices)), 1), max(prices)),
        "price_bands": dict(sorted(price_bands.items())),
        "worth_ratio_min_avg_max": (round(min(ratios), 4), round(sum(ratios) / len(ratios), 4), round(max(ratios), 4)),
        "worth_ratio_bands": dict(sorted(ratio_bands.items())),
        "coins_min_max": (min(b["coins"] for b in bundles), max(b["coins"] for b in bundles)),
        "distinct_powerup_types_histogram": dict(sorted(ntypes.items())),
        "tier_histogram": dict(sorted(tiers.items())),
        "distinct_names": len(set(b["name"] for b in bundles)),
        "longest_name": max(len(b["name"]) for b in bundles),
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
