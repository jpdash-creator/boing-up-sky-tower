"""Run from the repo root:  python -m unittest discover -s Tests/content -v

Validates Content/economy/bundles.json (made by Tools/content_cli/gen_bundles.py, docs/26 section 3)."""
import json
import os
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


def value_in_twentieths(b):
    """coins/20 + gems back + power-ups at list rates, scaled by 20 so the checks are exact integers."""
    return b["coins"] + 20 * (b["gems"] + sum(GEM_VALUE[p["id"]] * p["count"] for p in b["powerups"]))


class BundleData(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.bundles = load_bundles()
        cls.schema = content.load("schemas/bundle.schema.json")

    def test_count(self):
        self.assertEqual(len(self.bundles), 1000)

    def test_schema(self):
        errors = []
        for b in self.bundles:
            content.check_schema(b, self.schema, b.get("id", "?"), errors)
        self.assertEqual(errors, [])

    def test_ids_and_names_unique(self):
        self.assertEqual(len(set(b["id"] for b in self.bundles)), 1000)
        self.assertEqual(len(set(b["name"] for b in self.bundles)), 1000)
        self.assertTrue(all(len(b["name"]) <= 24 for b in self.bundles), "names must fit a card")

    def test_price_range_and_steps(self):
        for b in self.bundles:
            self.assertTrue(200 <= b["priceGems"] <= 500, b["id"])
            self.assertEqual(b["priceGems"] % 10, 0, b["id"])

    def test_value_band(self):
        for b in self.bundles:
            v = value_in_twentieths(b)            # value in gems = v / 20
            self.assertGreaterEqual(v * 10, 14 * 20 * b["priceGems"], "%s below 1.4x" % b["id"])
            self.assertLessEqual(v * 10, 18 * 20 * b["priceGems"], "%s above 1.8x" % b["id"])

    def test_contents(self):
        for b in self.bundles:
            ids = [p["id"] for p in b["powerups"]]
            self.assertTrue(1 <= len(ids) <= 4, b["id"])
            self.assertEqual(len(ids), len(set(ids)), "%s repeats a power-up" % b["id"])
            for p in b["powerups"]:
                self.assertTrue(1 <= p["count"] <= 10, b["id"])
            self.assertGreater(b["coins"], 0, b["id"])
            self.assertGreaterEqual(b["gems"], 0, b["id"])
            self.assertIsNone(b["characterId"], "no character bundles are generated yet")

    def test_tier_follows_price(self):
        for b in self.bundles:
            self.assertEqual(b["tier"], gen_bundles.tier_of(b["priceGems"]), b["id"])

    def test_no_duplicate_content_signatures(self):
        sigs = set()
        for b in self.bundles:
            sig = (b["coins"], b["gems"], tuple((p["id"], p["count"]) for p in sorted(b["powerups"], key=lambda p: p["id"])), b["characterId"])
            self.assertNotIn(sig, sigs, b["id"])
            sigs.add(sig)

    def test_all_seven_power_ups_are_used(self):
        used = set(p["id"] for b in self.bundles for p in b["powerups"])
        self.assertEqual(used, set(GEM_VALUE))

    def test_generator_is_deterministic_and_matches_the_files(self):
        text = gen_bundles.to_text(gen_bundles.generate())
        with open(os.path.join(ROOT, "Content", "economy", "bundles.json"), encoding="utf-8", newline="") as f:
            self.assertEqual(f.read(), text, "run Tools/content_cli/gen_bundles.py")
        with open(os.path.join(ROOT, "Assets", "Resources", "Content", "bundles.json"), encoding="utf-8", newline="") as f:
            self.assertEqual(f.read(), text, "the game copy differs: run Tools/content_cli/gen_bundles.py")


if __name__ == "__main__":
    unittest.main()
