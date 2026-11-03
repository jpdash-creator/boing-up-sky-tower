using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BoingUpSkyTower.Economy;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>The hidden hero card of a bundle (docs/37): tier odds 70:20:10, the pick among a tier, the open-regions rule, the fallback when a tier is empty, and the 2,000-bundle price and worth rules.</summary>
    public class BundleCardTests
    {
        static ulong Mix(ref ulong s)
        {
            unchecked
            {
                s += 0x9E3779B97F4A7C15UL;
                ulong z = s;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        static CharacterSpec Hero(string id, CharacterRarity r, int region = 0, string line = null) { return new CharacterSpec { Id = id, Name = id, Rarity = r, Region = region, Line = line }; }

        static readonly CharacterSpec[] Everything =
        {
            Hero("c1", CharacterRarity.Common), Hero("u1", CharacterRarity.Uncommon), Hero("u2", CharacterRarity.Uncommon),
            Hero("r1", CharacterRarity.Rare), Hero("e1", CharacterRarity.Epic),
        };

        [Fact]
        public void WeightsAreTheOwnersOdds()
        {
            Assert.Equal(new[] { 70, 20, 10 }, BundleCards.Weights);
            Assert.Equal(100, BundleCards.WeightTotal);
            Assert.Equal(new[] { CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic }, BundleCards.Tiers);
            Assert.Equal(new[] { 70, 20, 10 }, new[] { BundleCards.ChancePercent(0), BundleCards.ChancePercent(1), BundleCards.ChancePercent(2) });
            Assert.Equal(new[] { 100, 250, 600 }, BundleCards.TierGems);
            Assert.Equal(180.0, BundleCards.ExpectedGems, 6);   // (70 x 100 + 20 x 250 + 10 x 600) / 100
        }

        [Fact]
        public void HundredThousandDrawsFollowTheOddsAndNeverGiveACommon()
        {
            var count = new Dictionary<CharacterRarity, int>();
            ulong s = 12345;
            const int N = 100000;
            for (int i = 0; i < N; i++)
            {
                CharacterSpec c = BundleCards.Draw(Mix(ref s), Mix(ref s), Everything);
                Assert.NotNull(c);
                Assert.NotEqual(CharacterRarity.Common, c.Rarity);
                int n; count.TryGetValue(c.Rarity, out n); count[c.Rarity] = n + 1;
            }
            double[] expect = { 0.70, 0.20, 0.10 };
            for (int t = 0; t < 3; t++)
            {
                double got = count.ContainsKey(BundleCards.Tiers[t]) ? count[BundleCards.Tiers[t]] / (double)N : 0.0;
                Assert.InRange(got, expect[t] - 0.01, expect[t] + 0.01);   // about 3 sigma is 0.0046
            }
        }

        [Fact]
        public void TheCharacterIsPickedUniformlyInsideTheTier()
        {
            var many = new[] { Hero("u1", CharacterRarity.Uncommon), Hero("u2", CharacterRarity.Uncommon), Hero("u3", CharacterRarity.Uncommon), Hero("u4", CharacterRarity.Uncommon) };
            var count = new Dictionary<string, int>();
            ulong s = 99;
            for (int i = 0; i < 40000; i++)
            {
                CharacterSpec c = BundleCards.Draw(0, Mix(ref s), many);   // tier roll 0 = Uncommon
                int n; count.TryGetValue(c.Id, out n); count[c.Id] = n + 1;
            }
            Assert.Equal(4, count.Count);
            foreach (int n in count.Values) Assert.InRange(n, 9500, 10500);
        }

        [Fact]
        public void AnEmptyTierFallsBackToTheHighestExistingTierBelow()
        {
            // only Uncommon exists (today's game): every drawn tier lands on Uncommon
            var onlyU = new[] { Hero("c1", CharacterRarity.Common), Hero("u1", CharacterRarity.Uncommon) };
            foreach (CharacterRarity drawn in BundleCards.Tiers)
            {
                CharacterRarity got;
                Assert.True(BundleCards.TryResolveTier(drawn, onlyU, out got));
                Assert.Equal(CharacterRarity.Uncommon, got);
            }
            // Uncommon and Rare exist: Epic gives Rare, not Uncommon
            var upToRare = new[] { Hero("u1", CharacterRarity.Uncommon), Hero("r1", CharacterRarity.Rare) };
            CharacterRarity r;
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Epic, upToRare, out r)); Assert.Equal(CharacterRarity.Rare, r);
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Uncommon, upToRare, out r)); Assert.Equal(CharacterRarity.Uncommon, r);
            // Uncommon missing but Rare exists: a drawn Uncommon goes UP to the lowest existing tier
            var noU = new[] { Hero("r1", CharacterRarity.Rare) };
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Uncommon, noU, out r)); Assert.Equal(CharacterRarity.Rare, r);
            // no card hero at all: no card (the purchase still works)
            Assert.False(BundleCards.TryResolveTier(CharacterRarity.Rare, new[] { Hero("c1", CharacterRarity.Common) }, out r));
            Assert.Null(BundleCards.Draw(1, 1, new[] { Hero("c1", CharacterRarity.Common) }));
        }

        // ---- the open-regions rule (B4.a.iii) ----

        static readonly CharacterSpec[] ThreeRegions =
        {
            Hero("base1", CharacterRarity.Common, 1), Hero("base2", CharacterRarity.Common, 2), Hero("base3", CharacterRarity.Common, 3),
            Hero("u1", CharacterRarity.Uncommon, 0, "base1"), Hero("u2", CharacterRarity.Uncommon, 0, "base2"), Hero("u3", CharacterRarity.Uncommon, 0, "base3"),
            Hero("r2", CharacterRarity.Rare, 0, "base2"), Hero("e3", CharacterRarity.Epic, 0, "base3"),
        };

        [Fact]
        public void ARegionIsReadFromTheCharacterOrItsBaseHero()
        {
            Assert.Equal(2, BundleCards.RegionOf(ThreeRegions[1], ThreeRegions));
            Assert.Equal(3, BundleCards.RegionOf(ThreeRegions[5], ThreeRegions));
            Assert.Equal(2, BundleCards.RegionOf(ThreeRegions[6], ThreeRegions));
            Assert.Equal(0, BundleCards.RegionOf(Hero("loose", CharacterRarity.Uncommon), ThreeRegions));
            Assert.Equal(1, BundleCards.RegionOf(CharacterDefs.Get("momo_u"), CharacterDefs.All));
            Assert.Equal(5, BundleCards.RegionOf(CharacterDefs.Get("monk_u"), CharacterDefs.All));
        }

        [Fact]
        public void OnlyHeroesOfOpenRegionsAreEverDrawn()
        {
            Func<int, bool> open2 = region => region == 2;   // regions 1 (always) and 2 are open, 3 is not
            var seen = new HashSet<string>();
            var count = new Dictionary<CharacterRarity, int>();
            ulong s = 31337;
            const int N = 100000;
            for (int i = 0; i < N; i++)
            {
                CharacterSpec c = BundleCards.Draw(Mix(ref s), Mix(ref s), ThreeRegions, open2);
                Assert.NotNull(c);
                seen.Add(c.Id);
                int n; count.TryGetValue(c.Rarity, out n); count[c.Rarity] = n + 1;
            }
            Assert.True(seen.SetEquals(new[] { "u1", "u2", "r2" }), string.Join(",", seen));   // u3 and e3 (region 3) never
            // Epic has nothing open: its 10 percent fall back to Rare (the next lower tier that exists), so Uncommon 70 and Rare 20 + 10
            Assert.InRange(count[CharacterRarity.Uncommon] / (double)N, 0.69, 0.71);
            Assert.InRange(count[CharacterRarity.Rare] / (double)N, 0.29, 0.31);
            Assert.False(count.ContainsKey(CharacterRarity.Epic));
        }

        [Fact]
        public void OnlyRegionOneOpenGivesTheRegionOneHeroesAndATierWithoutOneFallsBackDown()
        {
            Func<int, bool> none = region => false;
            Assert.Null(BundleCards.Draw(0, 0, new[] { Hero("c2", CharacterRarity.Common, 2), Hero("u2", CharacterRarity.Uncommon, 0, "c2") }, none));   // nothing open: no card, the purchase still works
            CharacterRarity r;
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Epic, ThreeRegions, none, out r)); Assert.Equal(CharacterRarity.Uncommon, r);   // region 1 has an Uncommon only
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Rare, ThreeRegions, none, out r)); Assert.Equal(CharacterRarity.Uncommon, r);
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Epic, ThreeRegions, region => region == 3, out r)); Assert.Equal(CharacterRarity.Epic, r);
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Epic, ThreeRegions, region => region == 2, out r)); Assert.Equal(CharacterRarity.Rare, r);
            var ids = new HashSet<string>();
            ulong s = 4;
            for (int i = 0; i < 2000; i++) ids.Add(BundleCards.Draw(Mix(ref s), Mix(ref s), ThreeRegions, none).Id);
            Assert.True(ids.SetEquals(new[] { "u1" }), string.Join(",", ids));
        }

        [Fact]
        public void TodaysFiveUncommonHeroesFollowTheOpenRegions()
        {
            var ids = new HashSet<string>();
            ulong s = 7;
            for (int i = 0; i < 3000; i++) ids.Add(BundleCards.Draw(Mix(ref s), Mix(ref s), CharacterDefs.All).Id);
            Assert.True(ids.SetEquals(new[] { "momo_u", "forestgirl_u", "beachotter_u", "candygirl_u", "monk_u" }), string.Join(",", ids));
            ids.Clear();
            for (int i = 0; i < 500; i++) ids.Add(BundleCards.Draw(Mix(ref s), Mix(ref s), CharacterDefs.All, region => false).Id);
            Assert.True(ids.SetEquals(new[] { "momo_u" }), string.Join(",", ids));   // new account: Momo's line only
            ids.Clear();
            for (int i = 0; i < 1500; i++) ids.Add(BundleCards.Draw(Mix(ref s), Mix(ref s), CharacterDefs.All, region => region <= 3).Id);
            Assert.True(ids.SetEquals(new[] { "momo_u", "forestgirl_u", "beachotter_u" }), string.Join(",", ids));
        }

        [Fact]
        public void NewCharactersArePickedUpWithoutCodeChanges()
        {
            var all = new List<CharacterSpec>(CharacterDefs.All) { Hero("new_epic", CharacterRarity.Epic) };
            bool seen = false;
            ulong s = 5;
            for (int i = 0; i < 5000 && !seen; i++) seen = BundleCards.Draw(Mix(ref s), Mix(ref s), all).Id == "new_epic";
            Assert.True(seen);
        }

        [Fact]
        public void DrawsAreRepeatable()
        {
            for (ulong a = 0; a < 50; a++)
                Assert.Equal(BundleCards.Draw(a * 7919, a * 104729, Everything).Id, BundleCards.Draw(a * 7919, a * 104729, Everything).Id);
        }

        // ---- the shipped bundle file ----

        static List<JsonElement> LoadBundles()
        {
            string path = Path.Combine(ContentLoader.FindRoot(), "Content", "economy", "bundles.json");
            using (JsonDocument d = JsonDocument.Parse(File.ReadAllText(path)))
                return d.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
        }

        static readonly Dictionary<string, int> PowerGems = new Dictionary<string, int>
        {
            { "pw_flight", 75 }, { "pw_rocket", 75 }, { "pw_doublejump", 50 }, { "pw_wallstick", 50 }, { "pw_shield", 50 }, { "pw_magnet", 50 }, { "pw_slowtime", 75 }
        };

        [Fact]
        public void TwoThousandBundlesAllCost200To600AndAreWorth2To3_5TimesWithNoDollars()
        {
            string path = Path.Combine(ContentLoader.FindRoot(), "Content", "economy", "bundles.json");
            Assert.DoesNotContain("$", File.ReadAllText(path));
            List<JsonElement> list = LoadBundles();
            Assert.Equal(2000, list.Count);
            Assert.Equal(2000, list.Select(b => b.GetProperty("id").GetString()).Distinct().Count());
            Assert.All(list, b => Assert.StartsWith("bnd_", b.GetProperty("id").GetString()));   // no old bundle_NNNN left
            var ratios = new List<double>();
            foreach (JsonElement b in list)
            {
                Assert.True(b.GetProperty("hiddenCard").GetBoolean());
                Assert.False(b.TryGetProperty("characterId", out _));
                int price = b.GetProperty("priceGems").GetInt32();
                Assert.InRange(price, 200, 600);
                double worth = b.GetProperty("coins").GetInt32() / 20.0 + BundleCards.ExpectedGems;
                foreach (JsonElement p in b.GetProperty("powerups").EnumerateArray()) worth += PowerGems[p.GetProperty("id").GetString()] * p.GetProperty("count").GetInt32();
                double ratio = worth / price;
                Assert.InRange(ratio, 2.0, 3.5);
                ratios.Add(ratio);
            }
            Assert.True(ratios.Max() - ratios.Min() > 1.2, "the multiplier is varied, not constant: " + ratios.Min() + ".." + ratios.Max());
            Assert.True(ratios.Select(r => Math.Round(r, 1)).Distinct().Count() > 12);
        }
    }
}
