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

    def test_every_price_is_200_to_500_gems(self):
        prices = [b["priceGems"] for b in self.bundles]
        for b in self.bundles:
            self.assertTrue(200 <= b["priceGems"] <= 500, b["id"])
            self.assertEqual(b["priceGems"] % 10, 0, b["id"])
        self.assertLess(min(prices), 220, "small bundles exist")
        self.assertGreater(max(prices), 480, "large bundles exist")
        self.assertGreater(len(set(prices)), 25, "many price points")

    def test_worth_multiplier_after_the_coin_cut_and_the_new_card_prices(self):
        """Owner 2026-11-13: the coin part is cut 30 percent (power-ups, prices, hidden card unchanged) and the hidden card is valued at the new hero card
        coin prices (250 / 600 / 1,600 gems, expected 455). The pool was sized for worth 1.5 to 2.5 with the OLD card value (180); the REAL worth is now
        about 1.85 to 3.6 (mean 2.56): the card is worth 275 gems more, the coins 30 percent less. Pinned here on purpose, not loosened silently."""
        ratios = [worth_gems(b) / b["priceGems"] for b in self.bundles]
        for b, r in zip(self.bundles, ratios):
            self.assertTrue(1.8 <= r <= 3.7, "%s ratio %.4f" % (b["id"], r))
        self.assertGreater(len(set(round(r, 1) for r in ratios)), 12, "the multiplier is varied, not constant")
        self.assertAlmostEqual(min(ratios), 1.85, delta=0.05)
        self.assertAlmostEqual(max(ratios), 3.59, delta=0.05)
        self.assertAlmostEqual(sum(ratios) / len(ratios), 2.56, delta=0.05)

    def test_coin_part_is_cut_by_30_percent(self):
        """The coins are 70 percent of what the sizing gave (steps of 50), so the whole pool holds about 70 percent of the old 14.27 million coins."""
        total = sum(b["coins"] for b in self.bundles)
        self.assertAlmostEqual(total / 14270550.0, 0.70, delta=0.01)
        self.assertEqual(gen_bundles.COIN_CUT_PERCENT, 30)

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
            self.assertGreaterEqual(b["coins"], 700, b["id"])   # sizing floor 1,000 minus the 30 percent cut
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
        self.assertGreater(len(set(b["coins"] for b in self.bundles)), 150)   # 188 after the 30 percent cut (was above 200)
        self.assertEqual(set(len(b["powerups"]) for b in self.bundles), {1, 2, 3, 4})
        self.assertEqual(set(b["tier"] for b in self.bundles), {1, 2, 3})

    def test_names_are_short_and_fun(self):
        """Adjective + noun, at most 24 characters, 2,000 distinct (reusing the v0.46 words is allowed)."""
        for b in self.bundles:
            adj, noun = b["name"].split(" ", 1)
            self.assertIn(adj, gen_bundles.ADJECTIVES, b["id"])
            self.assertIn(noun, gen_bundles.NOUNS, b["id"])

    def test_every_bundle_has_its_own_joke(self):
        """Bug List 2 C2.b.vi: 2,000 different one-liners built from a noun joke and an adjective joke (about 100 jokes in all)."""
        self.assertEqual(len(set(b["joke"] for b in self.bundles)), 2000)
        self.assertGreaterEqual(len(gen_bundles.ADJ_JOKES) + len(gen_bundles.NOUN_JOKES), 96)
        for b in self.bundles:
            self.assertTrue(10 <= len(b["joke"]) <= 140, b["id"])
            self.assertEqual(b["joke"], gen_bundles.joke_for(b["name"]), b["id"])
            self.assertNotIn("$", b["joke"])
            low = b["joke"].lower()
            for word in ("dollar", "usd", "gem"):
                self.assertNotIn(word, low, b["id"])

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
