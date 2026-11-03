using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BoingUpSkyTower.Economy;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>The hidden hero card of a bundle (docs/37): tier odds 100:40:20:5, the pick among a tier, the fallback when a tier is empty, and the 2,000-bundle price rule.</summary>
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

        static CharacterSpec Hero(string id, CharacterRarity r) { return new CharacterSpec { Id = id, Name = id, Rarity = r }; }

        static readonly CharacterSpec[] Everything =
        {
            Hero("c1", CharacterRarity.Common), Hero("u1", CharacterRarity.Uncommon), Hero("u2", CharacterRarity.Uncommon),
            Hero("r1", CharacterRarity.Rare), Hero("e1", CharacterRarity.Epic), Hero("l1", CharacterRarity.Legendary),
        };

        [Fact]
        public void WeightsAreTheOwnersOdds()
        {
            Assert.Equal(new[] { 100, 40, 20, 5 }, BundleCards.Weights);
            Assert.Equal(165, BundleCards.WeightTotal);
            Assert.Equal(new[] { CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic, CharacterRarity.Legendary }, BundleCards.Tiers);
            Assert.Equal(7.687, BundleCards.ExpectedUsd, 3);   // (100 x 2.99 + 40 x 7.99 + 20 x 19.99 + 5 x 49.99) / 165
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
            double[] expect = { 100 / 165.0, 40 / 165.0, 20 / 165.0, 5 / 165.0 };
            for (int t = 0; t < 4; t++)
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
            // Uncommon and Rare exist: Elite and Legendary give Rare, not Uncommon
            var upToRare = new[] { Hero("u1", CharacterRarity.Uncommon), Hero("r1", CharacterRarity.Rare) };
            CharacterRarity r;
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Epic, upToRare, out r)); Assert.Equal(CharacterRarity.Rare, r);
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Legendary, upToRare, out r)); Assert.Equal(CharacterRarity.Rare, r);
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Uncommon, upToRare, out r)); Assert.Equal(CharacterRarity.Uncommon, r);
            // Uncommon missing but Rare exists: a drawn Uncommon goes UP to the lowest existing tier
            var noU = new[] { Hero("r1", CharacterRarity.Rare) };
            Assert.True(BundleCards.TryResolveTier(CharacterRarity.Uncommon, noU, out r)); Assert.Equal(CharacterRarity.Rare, r);
            // no card hero at all: no card (the purchase still works)
            Assert.False(BundleCards.TryResolveTier(CharacterRarity.Rare, new[] { Hero("c1", CharacterRarity.Common) }, out r));
            Assert.Null(BundleCards.Draw(1, 1, new[] { Hero("c1", CharacterRarity.Common) }));
        }

        [Fact]
        public void TodaysFiveUncommonHeroesAreTheOnlyCards()
        {
            var ids = new HashSet<string>();
            ulong s = 7;
            for (int i = 0; i < 3000; i++) ids.Add(BundleCards.Draw(Mix(ref s), Mix(ref s), CharacterDefs.All).Id);
            Assert.True(ids.SetEquals(new[] { "momo_u", "forestgirl_u", "beachotter_u", "candygirl_u", "monk_u" }), string.Join(",", ids));
        }

        [Fact]
        public void NewCharactersArePickedUpWithoutCodeChanges()
        {
            var all = new List<CharacterSpec>(CharacterDefs.All) { Hero("new_legend", CharacterRarity.Legendary) };
            bool seen = false;
            ulong s = 5;
            for (int i = 0; i < 5000 && !seen; i++) seen = BundleCards.Draw(Mix(ref s), Mix(ref s), all).Id == "new_legend";
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
        public void TwoThousandNewBundlesAllHoldOneHiddenCardAndAnEqualWorthToPriceRatio()
        {
            List<JsonElement> list = LoadBundles();
            Assert.Equal(2000, list.Count);
            Assert.Equal(2000, list.Select(b => b.GetProperty("id").GetString()).Distinct().Count());
            Assert.All(list, b => Assert.StartsWith("bnd_", b.GetProperty("id").GetString()));   // no old bundle_NNNN left
            double ratioMin = 9, ratioMax = 0;
            int pMin = int.MaxValue, pMax = 0;
            foreach (JsonElement b in list)
            {
                Assert.True(b.GetProperty("hiddenCard").GetBoolean());
                Assert.False(b.TryGetProperty("characterId", out _));
                int price = b.GetProperty("priceGems").GetInt32();
                double worth = b.GetProperty("coins").GetInt32() / 20.0 + BundleCards.ExpectedGems;
                foreach (JsonElement p in b.GetProperty("powerups").EnumerateArray()) worth += PowerGems[p.GetProperty("id").GetString()] * p.GetProperty("count").GetInt32();
                double ratio = worth / price;
                ratioMin = Math.Min(ratioMin, ratio); ratioMax = Math.Max(ratioMax, ratio);
                pMin = Math.Min(pMin, price); pMax = Math.Max(pMax, price);
            }
            Assert.InRange(ratioMin, 1.59, 1.61);
            Assert.InRange(ratioMax, 1.59, 1.61);
            Assert.True(pMin >= 540 && pMax <= 3000 && pMax - pMin > 2000, pMin + ".." + pMax);
        }
    }
}
