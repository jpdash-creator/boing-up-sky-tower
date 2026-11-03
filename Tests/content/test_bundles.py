"""Run from the repo root:  python -m unittest discover -s Tests/content -v

Validates Content/economy/bundles.json (made by Tools/content_cli/gen_bundles.py, docs/26 section 3 and docs/37)."""
import json
import os
import re
import sys
import unittest

TOOLS = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "Tools", "content_cli"))
sys.path.insert(0, TOOLS)
import content  # noqa: E402
import gen_bundles  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GEM_VALUE = {"pw_flight": 75, "pw_rocket": 75, "pw_doublejump": 50, "pw_wallstick": 50, "pw_shield": 50, "pw_magnet": 50, "pw_slowtime": 75}


def load_bundles():
    with open(os.path.join(ROOT, "Content", "economy", "bundles.json"), encoding="utf-8") as f:
        return json.load(f)


def card_table_from_csharp():
    """Re-derived here from the C# source so the two tables cannot drift apart."""
    with open(os.path.join(ROOT, "Assets", "_Game", "Economy", "BundleCards.cs"), encoding="utf-8") as f:
        src = f.read()
    weights = [int(x) for x in re.search(r"Weights = \{([^}]*)\}", src).group(1).split(",")]
    gems = [int(x) for x in re.search(r"TierGems = \{([^}]*)\}", src).group(1).split(",")]
    return weights, gems


def worth_gems(b):
    return b["coins"] / 20.0 + sum(GEM_VALUE[p["id"]] * p["count"] for p in b["powerups"]) + gen_bundles.CARD_EXPECTED_GEMS


class BundleData(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.bundles = load_bundles()
        cls.schema = content.load("schemas/bundle.schema.json")

    def test_count_is_2000(self):
        self.assertEqual(len(self.bundles), 2000)

    def test_no_old_bundles_left(self):
        self.assertTrue(all(b["id"].startswith("bnd_") for b in self.bundles), "old bundle_NNNN ids must be gone")
        self.assertTrue(all("characterId" not in b for b in self.bundles))

    def test_schema(self):
        errors = []
        for b in self.bundles:
            content.check_schema(b, self.schema, b.get("id", "?"), errors)
        self.assertEqual(errors, [])

    def test_ids_and_names_unique(self):
        self.assertEqual(len(set(b["id"] for b in self.bundles)), 2000)
        self.assertEqual(len(set(b["name"] for b in self.bundles)), 2000)
        self.assertTrue(all(len(b["name"]) <= 24 for b in self.bundles), "names must fit a card")
        self.assertEqual([b["id"] for b in self.bundles], ["bnd_%04d" % (i + 1) for i in range(2000)], "ids are stable and sequential")

    def test_every_price_is_200_to_600_gems(self):
        prices = [b["priceGems"] for b in self.bundles]
        for b in self.bundles:
            self.assertTrue(200 <= b["priceGems"] <= 600, b["id"])
            self.assertEqual(b["priceGems"] % 10, 0, b["id"])
        self.assertLess(min(prices), 230, "small bundles exist")
        self.assertGreater(max(prices), 570, "large bundles exist")
        self.assertGreater(len(set(prices)), 35, "many price points")

    def test_every_worth_multiplier_is_2_to_3_5_and_they_vary(self):
        """Owner rule (B4.a.ii): worth (coins at coins/20, power-ups at gem prices, card at its expected gem value) is 2.0x to 3.5x the gems paid."""
        ratios = [worth_gems(b) / b["priceGems"] for b in self.bundles]
        for b, r in zip(self.bundles, ratios):
            self.assertTrue(2.0 <= r <= 3.5, "%s ratio %.4f" % (b["id"], r))
        self.assertGreater(len(set(round(r, 1) for r in ratios)), 12, "the multiplier is varied, not constant")
        self.assertLess(min(ratios), 2.1)
        self.assertGreater(max(ratios), 3.4)
        low = sum(1 for r in ratios if r < 2.5)
        high = sum(1 for r in ratios if r >= 3.0)
        self.assertGreater(low, 300)
        self.assertGreater(high, 300)

    def test_card_table_matches_the_csharp_source(self):
        weights, gems = card_table_from_csharp()
        self.assertEqual(weights, [70, 20, 10], "owner odds Uncommon 70, Rare 20, Epic 10")
        self.assertEqual(weights, [w for _, w, _ in gen_bundles.CARD_TIERS])
        self.assertEqual(gems, [g for _, _, g in gen_bundles.CARD_TIERS])
        expected = sum(w * g for w, g in zip(weights, gems)) / float(sum(weights))
        self.assertAlmostEqual(expected, gen_bundles.CARD_EXPECTED_GEMS)

    def test_contents(self):
        for b in self.bundles:
            ids = [p["id"] for p in b["powerups"]]
            self.assertTrue(1 <= len(ids) <= 4, b["id"])
            self.assertEqual(len(ids), len(set(ids)), "%s repeats a power-up" % b["id"])
            for p in b["powerups"]:
                self.assertTrue(1 <= p["count"] <= 10, b["id"])
            self.assertGreaterEqual(b["coins"], 1000, b["id"])
            self.assertEqual(b["coins"] % 50, 0, b["id"])
            self.assertNotIn("gems", b, "%s: bundles contain no gems" % b["id"])
            self.assertIs(b["hiddenCard"], True, "%s: exactly one hidden card" % b["id"])

    def test_tier_follows_price(self):
        for b in self.bundles:
            self.assertEqual(b["tier"], gen_bundles.tier_of(b["priceGems"]), b["id"])

    def test_no_duplicate_content_signatures(self):
        sigs = set()
        for b in self.bundles:
            sig = (b["coins"], tuple((p["id"], p["count"]) for p in sorted(b["powerups"], key=lambda p: p["id"])))
            self.assertNotIn(sig, sigs, b["id"])
            sigs.add(sig)

    def test_all_seven_power_ups_are_used(self):
        used = set(p["id"] for b in self.bundles for p in b["powerups"])
        self.assertEqual(used, set(GEM_VALUE))

    def test_variety(self):
        self.assertGreater(len(set(b["coins"] for b in self.bundles)), 300)
        self.assertEqual(set(len(b["powerups"]) for b in self.bundles), {1, 2, 3, 4})
        self.assertEqual(set(b["tier"] for b in self.bundles), {1, 2, 3})

    def test_names_are_new(self):
        """None of the previous pool's adjectives is used, so no old name comes back."""
        old_adjectives = ["Bouncy", "Springy", "Sunny", "Jolly", "Cheery", "Zippy", "Giddy", "Plucky", "Snazzy", "Fluffy", "Peppy", "Perky", "Merry", "Breezy",
                          "Dandy", "Spiffy", "Lucky", "Mighty", "Mellow", "Wobbly", "Jazzy", "Bubbly", "Sparkly", "Frosty", "Toasty", "Cosmic", "Tiny", "Grand",
                          "Royal", "Wild", "Happy", "Snappy", "Sleepy", "Nifty", "Quirky", "Rowdy", "Silly", "Speedy", "Golden", "Zesty", "Dapper", "Frisky",
                          "Glowy", "Hearty", "Jaunty", "Kooky", "Lofty", "Nimble", "Peachy", "Plump", "Rosy", "Savvy", "Swift", "Twinkly", "Velvet", "Whimsy",
                          "Woolly", "Zany", "Brave", "Candied"]
        self.assertEqual(set(gen_bundles.ADJECTIVES) & set(old_adjectives), set())
        self.assertTrue(all(b["name"].split(" ")[0] not in old_adjectives for b in self.bundles))

    def test_no_dollar_anywhere(self):
        """B4.a.ii: no dollars in the bundle data, the generator or the schema."""
        with open(os.path.join(ROOT, "Content", "economy", "bundles.json"), encoding="utf-8") as f:
            self.assertNotIn("$", f.read())
        for rel in (("Tools", "content_cli", "gen_bundles.py"), ("Content", "schemas", "bundle.schema.json")):
            with open(os.path.join(ROOT, *rel), encoding="utf-8") as f:
                text = f.read().lower()
            self.assertNotIn("usd", text)
            self.assertNotIn("dollar", text)

    def test_generator_is_deterministic_and_matches_the_files(self):
        first = gen_bundles.to_text(gen_bundles.generate())
        second = gen_bundles.to_text(gen_bundles.generate())
        self.assertEqual(first, second, "two runs differ")
        with open(os.path.join(ROOT, "Content", "economy", "bundles.json"), encoding="utf-8", newline="") as f:
            self.assertEqual(f.read(), first, "run Tools/content_cli/gen_bundles.py")
        with open(os.path.join(ROOT, "Assets", "Resources", "Content", "bundles.json"), encoding="utf-8", newline="") as f:
            self.assertEqual(f.read(), first, "the game copy differs: run Tools/content_cli/gen_bundles.py")


if __name__ == "__main__":
    unittest.main()
