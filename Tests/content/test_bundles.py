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


def card_expected_gems():
    """Re-derived here from the C# source so the two tables cannot drift apart."""
    with open(os.path.join(ROOT, "Assets", "_Game", "Economy", "BundleCards.cs"), encoding="utf-8") as f:
        src = f.read()
    weights = [int(x) for x in re.search(r"Weights = \{([^}]*)\}", src).group(1).split(",")]
    usd = [float(x) for x in re.search(r"TierUsd = \{([^}]*)\}", src).group(1).split(",")]
    per_gem = float(re.search(r"UsdPerGem = ([0-9.]+);", src).group(1))
    return weights, usd, per_gem


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

    def test_price_range_and_steps(self):
        prices = [b["priceGems"] for b in self.bundles]
        for b in self.bundles:
            self.assertTrue(540 <= b["priceGems"] <= 3000, b["id"])
            self.assertEqual(b["priceGems"] % 10, 0, b["id"])
        self.assertLess(min(prices), 700, "small bundles exist")
        self.assertGreater(max(prices), 2800, "large bundles exist")
        self.assertGreater(len(set(prices)), 200, "many price points")

    def test_worth_over_price_is_the_same_for_every_bundle(self):
        """Owner rule: price follows worth (coins + power-ups + EXPECTED card) by one ratio, so no bundle is better value than another."""
        for b in self.bundles:
            ratio = worth_gems(b) / b["priceGems"]
            self.assertAlmostEqual(ratio, gen_bundles.RATIO, delta=gen_bundles.RATIO_TOL + 1e-9, msg=b["id"])

    def test_card_table_matches_the_csharp_source(self):
        weights, usd, per_gem = card_expected_gems()
        self.assertEqual(weights, [w for _, w, _ in gen_bundles.CARD_TIERS])
        self.assertEqual(usd, [u for _, _, u in gen_bundles.CARD_TIERS])
        self.assertEqual(per_gem, gen_bundles.USD_PER_GEM)

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
        self.assertGreater(len(set(b["coins"] for b in self.bundles)), 800)
        self.assertEqual(set(len(b["powerups"]) for b in self.bundles), {1, 2, 3, 4})
        self.assertEqual(set(b["tier"] for b in self.bundles), {1, 2, 3})

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
