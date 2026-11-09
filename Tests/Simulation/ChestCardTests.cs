using System;
using System.Collections.Generic;
using System.Linq;
using FluffyClimbers.Economy;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>The hero card of a Treasure Chest (Bug List 5 F3): odds 70/20/10 and 50/40/10, the open-regions rule, the tier fallback, determinism and the worth band.</summary>
    public class ChestCardTests
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

        // three regions, every tier up to Elite exists in each
        static readonly CharacterSpec[] Full =
        {
            Hero("base1", CharacterRarity.Common, 1), Hero("base2", CharacterRarity.Common, 2), Hero("base3", CharacterRarity.Common, 3),
            Hero("u1", CharacterRarity.Uncommon, 0, "base1"), Hero("u2", CharacterRarity.Uncommon, 0, "base2"), Hero("u3", CharacterRarity.Uncommon, 0, "base3"),
            Hero("r1", CharacterRarity.Rare, 0, "base1"), Hero("r2", CharacterRarity.Rare, 0, "base2"), Hero("r3", CharacterRarity.Rare, 0, "base3"),
            Hero("e1", CharacterRarity.Epic, 0, "base1"), Hero("e2", CharacterRarity.Epic, 0, "base2"), Hero("e3", CharacterRarity.Epic, 0, "base3"),
        };

        const double AvgPowerUpGems = 425.0 / 7.0;   // 3 x 75 + 4 x 50 over the seven implemented types

        [Fact]
        public void TheOddsAreTheOwnersNumbers()
        {
            Assert.Equal(new[] { 70, 20, 10 }, ChestRolls.IronWeights);
            Assert.Equal(new[] { 50, 40, 10 }, ChestRolls.GoldenWeights);
            Assert.Equal(new[] { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare }, ChestRolls.IronTiers);
            Assert.Equal(new[] { CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic }, ChestRolls.GoldenTiers);
            Assert.Equal(100, ChestRolls.IronWeights.Sum());
            Assert.Equal(100, ChestRolls.GoldenWeights.Sum());
            Assert.Equal(70, ChestRolls.ChancePercent(ChestKind.Iron, 0));
            Assert.Equal(10, ChestRolls.ChancePercent(ChestKind.Golden, 2));
        }

        [Theory]
        [InlineData(ChestKind.Iron)]
        [InlineData(ChestKind.Golden)]
        public void HundredThousandDrawsFollowTheOdds(ChestKind kind)
        {
            var count = new Dictionary<CharacterRarity, int>();
            ulong s = 4242;
            const int N = 100000;
            for (int i = 0; i < N; i++)
            {
                CharacterSpec c = ChestRolls.DrawHero(kind, Mix(ref s), Mix(ref s), Full, null);
                Assert.NotNull(c);
                int n; count.TryGetValue(c.Rarity, out n); count[c.Rarity] = n + 1;
            }
            CharacterRarity[] tiers = ChestRolls.Tiers(kind);
            Assert.Equal(tiers.Length, count.Count);
            for (int t = 0; t < tiers.Length; t++)
                Assert.InRange(count[tiers[t]] / (double)N, ChestRolls.Weights(kind)[t] / 100.0 - 0.01, ChestRolls.Weights(kind)[t] / 100.0 + 0.01);   // 3 sigma is about 0.0048
        }

        [Fact]
        public void OnlyHeroesOfOpenRegionsAreEverDrawn()
        {
            Func<int, bool> open2 = region => region == 2;   // regions 1 (always) and 2 are open, 3 is not
            foreach (ChestKind kind in new[] { ChestKind.Iron, ChestKind.Golden })
            {
                var seen = new HashSet<string>();
                ulong s = 77;
                for (int i = 0; i < 20000; i++) seen.Add(ChestRolls.DrawHero(kind, Mix(ref s), Mix(ref s), Full, open2).Id);
                Assert.DoesNotContain(seen, id => id.EndsWith("3"));
                Assert.Contains(seen, id => id.EndsWith("1"));
                Assert.Contains(seen, id => id.EndsWith("2"));
            }
        }

        [Fact]
        public void CommonHeroesAreTheBaseHeroOfEachOpenRegion()
        {
            List<CharacterSpec> all = ChestRolls.Pool(CharacterRarity.Common, Full, null);
            Assert.Equal(new[] { "base1", "base2", "base3" }, all.Select(c => c.Id).ToArray());
            List<CharacterSpec> some = ChestRolls.Pool(CharacterRarity.Common, Full, r => false);
            Assert.Equal(new[] { "base1" }, some.Select(c => c.Id).ToArray());   // region 1 is always open
            // the real game (owner 2026-11-09): five ACTIVE Common heroes, no merge tier is ever a Common card, no inactive hero
            Assert.Equal(new[] { "beachotter", "candygirl", "forestgirl", "momo", "monk" }, ChestRolls.Pool(CharacterRarity.Common, CharacterDefs.All, null).Select(c => c.Id).ToArray());
            Assert.Equal(new[] { "momo" }, ChestRolls.Pool(CharacterRarity.Common, CharacterDefs.All, r => false).Select(c => c.Id).ToArray());
        }

        [Fact]
        public void AnEmptyTierFallsBackToTheNextLowerTierThatHasAHero()
        {
            // today's real game: Region 1 (Momo) has Common to Legendary heroes, so a drawn tier is used as it is while region 1 is the only open region
            foreach (CharacterRarity drawn in new[] { CharacterRarity.Common, CharacterRarity.Uncommon, CharacterRarity.Rare, CharacterRarity.Epic })
            {
                CharacterRarity got;
                Assert.True(ChestRolls.TryResolveTier(drawn, CharacterDefs.All, r => false, out got));
                Assert.Equal(drawn, got);
            }
            // Uncommon and Rare exist: Elite gives Rare; Rare only in a closed region: Rare falls to Uncommon
            var upToRare = new[] { Hero("b", CharacterRarity.Common, 1), Hero("u1", CharacterRarity.Uncommon, 0, "b"), Hero("r1", CharacterRarity.Rare, 0, "b") };
            CharacterRarity r;
            Assert.True(ChestRolls.TryResolveTier(CharacterRarity.Epic, upToRare, null, out r)); Assert.Equal(CharacterRarity.Rare, r);
            var closedRare = new[] { Hero("b1", CharacterRarity.Common, 1), Hero("b2", CharacterRarity.Common, 2), Hero("u1", CharacterRarity.Uncommon, 0, "b1"), Hero("r2", CharacterRarity.Rare, 0, "b2") };
            Assert.True(ChestRolls.TryResolveTier(CharacterRarity.Rare, closedRare, x => false, out r)); Assert.Equal(CharacterRarity.Uncommon, r);
            // a Rare hit with only Commons around falls to Common
            var onlyCommon = new[] { Hero("b", CharacterRarity.Common, 1) };
            Assert.True(ChestRolls.TryResolveTier(CharacterRarity.Rare, onlyCommon, null, out r)); Assert.Equal(CharacterRarity.Common, r);
            // nothing at or below the drawn tier goes UP to the lowest tier that exists
            var onlyRare = new[] { Hero("r", CharacterRarity.Rare, 1) };
            Assert.True(ChestRolls.TryResolveTier(CharacterRarity.Uncommon, onlyRare, null, out r)); Assert.Equal(CharacterRarity.Rare, r);
            // no hero at all: no card
            Assert.Null(ChestRolls.DrawHero(ChestKind.Iron, 1, 1, new CharacterSpec[0], null));
        }

        [Fact]
        public void TheRealGameNeverReturnsAnUnavailableTier()
        {
            ulong s = 5;
            var seen = new HashSet<CharacterRarity>();
            for (int i = 0; i < 20000; i++)
                foreach (ChestKind kind in new[] { ChestKind.Iron, ChestKind.Golden })
                    seen.Add(ChestRolls.DrawHero(kind, Mix(ref s), Mix(ref s), CharacterDefs.All, null).Rarity);
            // every active line has Common to Legendary heroes, so every drawn tier must be a tier that really has a hero
            Assert.All(seen, t => Assert.Contains(CharacterDefs.All, c => c.Rarity == t));
            Assert.Contains(CharacterRarity.Rare, seen);
        }

        [Fact]
        public void TheSameRollsGiveTheSameHero()
        {
            for (ulong i = 0; i < 500; i++)
            {
                CharacterSpec a = ChestRolls.DrawHero(ChestKind.Golden, i * 7919, i * 104729, Full, null);
                CharacterSpec b = ChestRolls.DrawHero(ChestKind.Golden, i * 7919, i * 104729, Full.Reverse().ToArray(), null);   // list order never changes a draw
                Assert.Equal(a.Id, b.Id);
            }
        }

        // ---- the one gem table and the worth ----

        [Fact]
        public void TheCardGemTableIsTheBundleTableAndExpectedValuesFollow()
        {
            Assert.Equal(BundleCards.TierGems[0], ChestRolls.CardGems(CharacterRarity.Uncommon));
            Assert.Equal(BundleCards.TierGems[1], ChestRolls.CardGems(CharacterRarity.Rare));
            Assert.Equal(BundleCards.TierGems[2], ChestRolls.CardGems(CharacterRarity.Epic));
            Assert.Equal(ChestRolls.CommonCardGems, ChestRolls.CardGems(CharacterRarity.Common));
            Assert.True(ChestRolls.CardGems(CharacterRarity.Common) < ChestRolls.CardGems(CharacterRarity.Uncommon));
            Assert.Equal(0, ChestRolls.CardGems(CharacterRarity.Legendary));
            Assert.Equal(73.0, ChestRolls.ExpectedCardGems(ChestKind.Iron), 6);    // 0.7 x 40 + 0.2 x 100 + 0.1 x 250
            Assert.Equal(210.0, ChestRolls.ExpectedCardGems(ChestKind.Golden), 6); // 0.5 x 100 + 0.4 x 250 + 0.1 x 600
        }

        [Theory]
        [InlineData(ChestKind.Iron)]
        [InlineData(ChestKind.Golden)]
        public void WorthIsBetween1Point5And2Point5EvenAtTheExtremes(ChestKind kind)
        {
            double w = ChestRolls.WorthMultiplier(kind, AvgPowerUpGems);
            Assert.InRange(w, ChestRolls.WorthMin, ChestRolls.WorthMax);
            double price = ChestRolls.Price(kind, 1), cards = ChestRolls.ExpectedCardGems(kind), n = ChestRolls.PowerUpCount(kind);
            double low = (ChestRolls.MinCoins(kind) / 20.0 + n * 50.0 + cards) / price, high = (ChestRolls.MaxCoins(kind) / 20.0 + n * 75.0 + cards) / price;
            Assert.InRange(low, ChestRolls.WorthMin, ChestRolls.WorthMax);
            Assert.InRange(high, ChestRolls.WorthMin, ChestRolls.WorthMax);
            Assert.Equal(10 * ChestRolls.Price(kind, 1), ChestRolls.Price(kind, 10));
            Assert.StartsWith("Worth x", ChestRolls.WorthText(kind, AvgPowerUpGems));
        }

        [Fact]
        public void WorthTagsAre1Point8And1Point9()
        {
            Assert.Equal("Worth x1.8", ChestRolls.WorthText(ChestKind.Iron, AvgPowerUpGems));
            Assert.Equal("Worth x1.9", ChestRolls.WorthText(ChestKind.Golden, AvgPowerUpGems));
        }

        [Fact]
        public void PricesAreUnchanged()
        {
            Assert.Equal(100, ChestRolls.Price(ChestKind.Iron, 1));
            Assert.Equal(1000, ChestRolls.Price(ChestKind.Iron, 10));
            Assert.Equal(300, ChestRolls.Price(ChestKind.Golden, 1));
            Assert.Equal(3000, ChestRolls.Price(ChestKind.Golden, 10));
            Assert.Equal(0, ChestRolls.Price(ChestKind.Iron, 5));
        }
    }
}
