"""Generates Content/economy/bundles.json: 2,000 distinct gem bundles, each with ONE hidden hero card (owner Bug List 1 B4.a, docs/37-bundles.md).
Deterministic: the same script always writes the same file. The set REPLACES the previous pool (200 to 600 gems, worth 2.0 to 3.5): ids stay bnd_0001..bnd_2000
but every bundle is new (new seed, new name order, new prices, new worth), and the save's pool version (BundleCatalog.PoolVersion = 3) makes old saved offers redraw.

Run from the repo root:   python Tools/content_cli/gen_bundles.py
Writes Content/economy/bundles.json (the editable source) and the identical copy Assets/Resources/Content/bundles.json (what the game loads
at run time through Resources, see BundleCatalog in Assets/_Game/Economy). Run `python -m unittest discover -s Tests/content` afterwards.

Rules (docs/37):
  - 2,000 bundles. Price 200 to 500 gems (steps of 10), nothing else (Bug List 2 C2.b.i).
  - Contents: coins (steps of 50) + 1 to 4 distinct power-up types (counts 1 to 10 each) + ONE hidden hero card ("hiddenCard": true).
    Which hero is drawn when the bundle is bought (odds Uncommon 70, Rare 20, Epic 10; only heroes of open regions) is decided by the game
    (BundleCards.cs); the file never names a hero.
  - Worth in gems = coins / 20 + power-ups at their gem prices (50 or 75) + the EXPECTED gem value of the card (CARD_TIERS below, equal to
    BundleCards.TierGems = 250 / 600 / 1,600 since 2026-11-13: expected 455). SIZING (unchanged since 2026-11-08): a multiplier is drawn per bundle
    between 1.5 and 2.5 against the OLD card value (180) and coins and power-ups are sized to it. Then (owner 2026-11-13) the coin part is cut by
    COIN_CUT_PERCENT = 30 percent. The REAL worth (new card value, cut coins) is therefore NOT 1.5 to 2.5 any more: it is reported by statistics()
    (about 1.9 to 3.6) and shown on the card by BundleWorth; the card is worth more because hero cards are priced higher in coins.
  - The card table below must equal BundleCards.TierGems / Weights in Assets/_Game/Economy/BundleCards.cs (a unit test compares them).
  - no two bundles share a content signature (coins, power-up counts); no two share a name; names are at most 24 characters (adjective + noun
    from the lists below; reusing the v0.46 words is allowed, the shuffle order is new).
  - every bundle has a "joke" (info popup, Bug List 2 C2.b.vi): the noun's joke followed by the adjective's joke, so 2,000 different names give
    2,000 different lines. No currency amounts, no competitor names.
"""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_SOURCE = os.path.join(ROOT, "Content", "economy", "bundles.json")
OUT_GAME = os.path.join(ROOT, "Assets", "Resources", "Content", "bundles.json")

COUNT = 2000
ID_PREFIX = "bnd_"
PRICE_MIN, PRICE_MAX, PRICE_STEP = 200, 500, 10
WORTH_MIN, WORTH_MAX = 1.5, 2.5          # worth multiplier bounds (worth in gems / gems paid)
WORTH_MARGIN = 0.02                      # drawn targets stay this far inside the bounds
RATIO_TOL = 0.02                         # the real ratio is within this of the drawn target
COIN_STEP = 50
COINS_PER_GEM = 20                       # 1 gem = 20 coins (docs/22)
MAX_DISTINCT_POWERUPS = 4
MAX_COUNT = 10
MIN_COINS = 1000                         # floor of the SIZING coins (before the cut)
COIN_CUT_PERCENT = 30                    # owner 2026-11-13: the COIN part of every bundle is cut by 30 percent after sizing
LEGACY_CARD_EXPECTED_GEMS = 180.0        # card value (100 / 250 / 600) the pool was sized with; the sizing keeps it, so power-ups and prices stay as they were
SEED_SALT = 20261108                     # new seed (the previous pools used 20261103 and 20261105)
NAME_SEED = 8112026                      # new name shuffle (the previous pools used 31102026 and 5112026)

# hidden card: tier -> (draw weight, gem value). Order = Uncommon, Rare, Elite (CharacterRarity.Epic). Equal to BundleCards.Weights / TierGems.
# Gem value = coin price of the card / 20 (owner 2026-11-13: Uncommon 5,000, Rare 12,000, Elite 32,000 coins), so 250 / 600 / 1,600.
CARD_TIERS = [("Uncommon", 70, 250), ("Rare", 20, 600), ("Elite", 10, 1600)]
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

# One joke per adjective and per noun (about 100 in all). A bundle's line = noun joke + " " + adjective joke, so it reads naturally for its name.
ADJ_JOKES = {
    "Gleeful": "Gleeful enough to giggle at its own price tag.",
    "Snuggly": "Snuggly like a hug with excellent manners.",
    "Hoppy": "Hoppy, it will not stand still, we tried.",
    "Flashy": "Flashy, but only on days that end in y.",
    "Cuddly": "Cuddly, and that is not negotiable.",
    "Dreamy": "Dreamy enough to nap next to.",
    "Fizzy": "Fizzy, so shake at your own risk.",
    "Gutsy": "Gutsy, it walked right into your cart.",
    "Humble": "Humble, it will not mention how great it is.",
    "Minty": "Minty, as fresh as a brand new Tuesday.",
    "Noble": "Noble, bows to nobody except snacks.",
    "Pixie": "Pixie sized, sparkle sized, trouble sized.",
    "Rapid": "Rapid, it arrives before you finish tapping.",
    "Shiny": "Shiny, so squint a little.",
    "Tidy": "Tidy, every item folded by a serious squirrel.",
    "Vivid": "Vivid, the colours are almost too loud.",
    "Witty": "Witty, it will out-joke you.",
    "Yummy": "Yummy, though we do not recommend tasting it.",
    "Amber": "Amber glow, stays on after bedtime.",
    "Blazing": "Blazing, but marshmallow safe.",
    "Crisp": "Crisp, shop fresh and ready to crunch.",
    "Dusty": "Dusty, but only from being popular.",
    "Eager": "Eager, already waving from the shelf.",
    "Fabled": "Fabled, bards sing about it badly.",
    "Gentle": "Gentle, it apologises to doors.",
    "Hasty": "Hasty, it left before you finished reading.",
    "Ivory": "Ivory, fancy enough for a tiny crown.",
    "Jumbo": "Jumbo, bigger on the inside, we hope.",
    "Lively": "Lively, it wiggles when nobody is looking.",
    "Misty": "Misty, a bit mysterious, a bit damp.",
    "Nutty": "Nutty, in the best squirrel way.",
    "Oaken": "Oaken, sturdy enough to survive Mondays.",
    "Pearly": "Pearly, polished by very proud clams.",
    "Quaint": "Quaint, comes with a tiny doily.",
    "Rustic": "Rustic, handmade by a charming goat.",
    "Sassy": "Sassy, it talks back, politely.",
    "Tangy": "Tangy, makes your cheeks do a wiggle.",
    "Upbeat": "Upbeat, it hums even when unplugged.",
    "Wacky": "Wacky, the box is in on the joke.",
    "Zingy": "Zingy, wakes up a sleepy Tuesday.",
    "Cozy": "Cozy, pairs well with a blanket and a sigh.",
    "Daring": "Daring, it jumps first and reads maps later.",
    "Fancy": "Fancy, pinky finger out the whole time.",
    "Gallant": "Gallant, it holds the door for your coins.",
    "Honeyed": "Honeyed, sweet talk included.",
    "Jiggly": "Jiggly, a gentle wobble in every item.",
    "Kingly": "Kingly, demands a very small throne.",
    "Lunar": "Lunar, a little over the moon.",
    "Mossy": "Mossy, soft on one side, dignified on the other.",
    "Neon": "Neon, glows in the dark and slightly in the light.",
    "Perfect": "Perfect, except for the part where it is not.",
    "Radiant": "Radiant, no lamp required.",
    "Sugary": "Sugary, a dentist will hear of this.",
    "Tropical": "Tropical, bring a tiny umbrella.",
    "Unbeaten": "Unbeaten, undefeated at staring contests.",
    "Windswept": "Windswept, hair done by a breeze.",
}
NOUN_JOKES = {
    "Pogo Pack": "Bounces first, asks questions never.",
    "Sprout Sack": "Contains zero vegetables, probably.",
    "Nimbus Nook": "A cozy cloud corner with terrible signal.",
    "Beanstalk Bag": "Giants not included, giant appetites are.",
    "Rocket Roll": "Rolls up neatly, blasts off messily.",
    "Bubble Box": "Pop responsibly, one bubble at a time.",
    "Cocoa Cache": "A secret stash, the marshmallows are long gone.",
    "Bounce Tote": "Carries your stuff and sometimes itself.",
    "Echo Chest": "Open it once, hear about it three times.",
    "Feather Fort": "Tickle defence level: maximum.",
    "Gadget Grab": "Whatever that button does, do not press it.",
    "Harvest Haul": "Fresh from the field and loudly proud of it.",
    "Sundae Crate": "Cherry on top, sprinkles on your shoes.",
    "Jetpack Jar": "Pickles not included, lift-off very much is.",
    "Kettle Kit": "Whistles when happy, screams when thrilled.",
    "Lollipop Lot": "Sticky fingers guaranteed, sticky keyboards too.",
    "Marble Mix": "Rolls away the moment you look at it.",
    "Noodle Nest": "Slurp loudly, nobody is judging you.",
    "Orbit Outfit": "Fashion for people who go in circles.",
    "Pancake Pile": "Flipped it, stacked it, now defend it.",
    "Quest Quiver": "The arrows are fine, the snacks are the mission.",
    "Ribbon Roll": "Wrap it, tie it, then lose the end.",
    "Sunbeam Sack": "Bagged daylight, handle with sunglasses.",
    "Treetop Tote": "Squirrels offered to carry it, for a fee.",
    "Umbrella Unit": "It only rains when you leave it at home.",
    "Velcro Vault": "Holds tight and opens with a rude noise.",
    "Waffle Wagon": "Syrup not included, regrets neither.",
    "Yo-yo Yard": "Goes up, comes down, repeats like a pro.",
    "Zeppelin Zip": "Floats politely, zips impolitely.",
    "Biscuit Box": "Biscuit counts may vary after midnight.",
    "Cricket Cart": "Chirps all night, tips never.",
    "Dewdrop Drop": "Tiny splash, huge sparkle, zero towels.",
    "Ember Express": "Toasty delivery, please blow on it first.",
    "Fiddle Fund": "Tuned loosely, played enthusiastically.",
    "Glider Grab": "Catch it before it catches a breeze.",
    "Hammock Hoard": "Contains naps, tightly packed.",
    "Igloo Idea": "It seemed cool at the time.",
    "Jamboree Jar": "A party in a jar, and the lid is losing.",
    "Kaleido Kit": "Shake it once and everything turns pretty.",
    "Lighthouse Lot": "Shines bright, gives directions nobody asked for.",
}


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
    """Bag size: 1 below 300 gems, 2 below 400, else 3."""
    return 1 if price < 300 else (2 if price < 400 else 3)


def build_one(index, attempt):
    r = Rng(index * 1000003 + attempt * 7919 + SEED_SALT)
    steps = (PRICE_MAX - PRICE_MIN) // PRICE_STEP
    price = PRICE_MIN + PRICE_STEP * int(steps * (r.unit() ** 1.1) + 0.5)       # a few more small and middle bundles than big ones
    lo, hi = WORTH_MIN + WORTH_MARGIN, WORTH_MAX - WORTH_MARGIN
    target = lo + (hi - lo) * r.unit()                                           # drawn worth multiplier, uniform over the whole 1.5 to 2.5 span
    content = price * target - LEGACY_CARD_EXPECTED_GEMS                         # coins + power-ups share of the worth, in gems
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
    legacy = (coins / float(COINS_PER_GEM) + sum(POWERUP_VALUE[pid] * n for pid, n in powerups) + LEGACY_CARD_EXPECTED_GEMS) / float(price)
    if abs(legacy - target) > RATIO_TOL or not (WORTH_MIN <= legacy <= WORTH_MAX):
        return None
    sizing_coins = coins
    coins = int(round(coins * (100 - COIN_CUT_PERCENT) / 100.0 / COIN_STEP)) * COIN_STEP   # the 30 percent coin cut
    return price, coins, powerups, sizing_coins


def signature(coins, powerups):
    return (coins, tuple(powerups))


def make_names():
    assert set(NOUN_JOKES) == set(NOUNS) and set(ADJ_JOKES) == set(ADJECTIVES), "one joke per adjective and per noun"
    names = []
    for a in ADJECTIVES:
        for n in NOUNS:
            names.append("%s %s" % (a, n))
    shuffler = Rng(NAME_SEED)
    for i in range(len(names) - 1, 0, -1):                                       # Fisher-Yates with the fixed generator
        j = shuffler.below(i + 1)
        names[i], names[j] = names[j], names[i]
    return names


def joke_for(name):
    """The bundle's one-liner: the noun's joke, then the adjective's joke (name = adjective + noun)."""
    adjective, noun = name.split(" ", 1)
    return NOUN_JOKES[noun] + " " + ADJ_JOKES[adjective]


def generate():
    names = make_names()
    bundles, seen, seen_sizing = [], set(), set()
    for index in range(COUNT):
        built = None
        for attempt in range(500):
            cand = build_one(index, attempt)
            if cand is None:
                continue
            price, coins, powerups, sizing_coins = cand
            if signature(sizing_coins, powerups) in seen_sizing:                 # same retry rule as before the cut, so the same bundles survive
                continue
            seen_sizing.add(signature(sizing_coins, powerups))
            while signature(coins, powerups) in seen:                            # the cut can make two signatures equal: add one coin step
                coins += COIN_STEP
            seen.add(signature(coins, powerups))
            built = (price, coins, powerups)
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
            "joke": joke_for(names[index]),
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
        band = "%d-%d" % (p // 50 * 50, p // 50 * 50 + 49)
        price_bands[band] = price_bands.get(band, 0) + 1
    ratio_bands = {}
    for x in ratios:
        band = "%.1f-%.1f" % (int(x * 4) / 4.0, int(x * 4) / 4.0 + 0.25)
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
        "distinct_jokes": len(set(b["joke"] for b in bundles)),
        "longest_joke": max(len(b["joke"]) for b in bundles),
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
