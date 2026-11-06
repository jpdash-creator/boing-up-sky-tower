using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>
    /// Treasure chest rules of docs/22 point 3 as reworked by Bug List 5 F3: loot ranges, equal power-up chances, exactly one hero card per chest (odds 70/20/10 and 50/40/10, open regions only,
    /// tier fallback), no refunds for duplicates, the worth band 1.5 to 2.5, Buy 10 and spending. The odds themselves are also tested in Tests/Simulation/ChestCardTests.cs.
    /// </summary>
    public class ChestTests
    {
        const int Runs = 100000;

        static ProgressService NewService()
        {
            var svc = new ProgressService(Content.ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
            svc.Data.Bundles = new BundleSaveData { Seed = 4711 };   // a fixed per-account seed: every draw below is repeatable
            return svc;
        }

        static List<PowerUpDef> Types()
        {
            return new List<PowerUpDef>(NewService().PowerUpDefs.Implemented);
        }

        static int TotalCopies(ProgressService svc)
        {
            int n = 0;
            foreach (CharacterSpec c in CharacterDefs.All) n += svc.Copies(c.Id);   // an owned, never counted hero is 1 copy, so the sum only grows by what AddCopies adds
            return n;
        }

        [Test]
        public void PricesAndLootRulesMatchTheSpec()
        {
            Assert.AreEqual(100, ChestRolls.Price(ChestKind.Iron, 1));
            Assert.AreEqual(1000, ChestRolls.Price(ChestKind.Iron, 10));
            Assert.AreEqual(300, ChestRolls.Price(ChestKind.Golden, 1));
            Assert.AreEqual(3000, ChestRolls.Price(ChestKind.Golden, 10));
            Assert.AreEqual(0, ChestRolls.Price(ChestKind.Iron, 5));
            Assert.AreEqual(1, ChestRolls.PowerUpCount(ChestKind.Iron));
            Assert.AreEqual(3, ChestRolls.PowerUpCount(ChestKind.Golden));
            Assert.AreEqual(7, Types().Count, "the seven implemented power-up types");
            Assert.AreEqual(new[] { 70, 20, 10 }, ChestRolls.IronWeights);
            Assert.AreEqual(new[] { 50, 40, 10 }, ChestRolls.GoldenWeights);
        }

        [Test]
        public void IronSimulationKeepsRangesEqualChancesAndOdds()
        {
            Simulate(ChestKind.Iron, 800, 1200, 1);
        }

        [Test]
        public void GoldSimulationKeepsRangesEqualChancesAndOdds()
        {
            Simulate(ChestKind.Golden, 3000, 4200, 3);
        }

        /// <summary>Bug List 5 F3: every chest is worth 1.5 to 2.5 times its Open 1 gems INCLUDING the expected hero card, at the average and at the extremes.</summary>
        [Test]
        public void WorthIsBetween1Point5And2Point5ForEveryChest()
        {
            PowerUpCatalog catalog = PowerUpCatalog.BuiltIn();
            Assert.AreEqual(60.71, ChestRolls.AveragePowerUpGems(catalog), 0.01, "average gem price of one random power-up");
            foreach (ChestKind kind in new[] { ChestKind.Iron, ChestKind.Golden })
            {
                Assert.That(ChestRolls.Worth(kind, catalog), Is.InRange(1.5, 2.5), kind + " worth at the average");
                double cards = ChestRolls.ExpectedCardGems(kind), price = ChestRolls.Price(kind, 1), n = ChestRolls.PowerUpCount(kind);
                double low = (ChestRolls.MinCoins(kind) / 20.0 + n * 50.0 + cards) / price, high = (ChestRolls.MaxCoins(kind) / 20.0 + n * 75.0 + cards) / price;
                Assert.That(low, Is.InRange(1.5, 2.5), kind + " worst coins and cheapest power-ups");
                Assert.That(high, Is.InRange(1.5, 2.5), kind + " best coins and dearest power-ups");
            }
            Assert.AreEqual("Worth x1.8", ChestRolls.WorthLabel(ChestKind.Iron, catalog));
            Assert.AreEqual("Worth x1.9", ChestRolls.WorthLabel(ChestKind.Golden, catalog));
        }

        [Test]
        public void TheHeroCardTableIsTheBundleTable()
        {
            Assert.AreEqual(BundleCards.TierGems[0], ChestRolls.CardGems(CharacterRarity.Uncommon));
            Assert.AreEqual(BundleCards.TierGems[1], ChestRolls.CardGems(CharacterRarity.Rare));
            Assert.AreEqual(BundleCards.TierGems[2], ChestRolls.CardGems(CharacterRarity.Epic));
            Assert.AreEqual(73.0, ChestRolls.ExpectedCardGems(ChestKind.Iron), 1e-9);
            Assert.AreEqual(210.0, ChestRolls.ExpectedCardGems(ChestKind.Golden), 1e-9);
        }

        static void Simulate(ChestKind kind, int minCoins, int maxCoins, int units)
        {
            List<PowerUpDef> types = Types();
            var perType = new Dictionary<string, int>();
            foreach (PowerUpDef d in types) perType[d.Id] = 0;
            var rarities = new Dictionary<CharacterRarity, int>();
            long totalUnits = 0;
            for (int i = 0; i < Runs; i++)
            {
                ChestContents c = ChestRolls.Roll(kind, i, types);
                Assert.That(c.Coins, Is.InRange(minCoins, maxCoins));
                Assert.AreEqual(units, c.PowerUps.Count);
                Assert.IsNotNull(c.Hero, "exactly one hero card per chest");
                foreach (string id in c.PowerUps) { perType[id]++; totalUnits++; }
                int have;
                rarities.TryGetValue(c.Hero.Rarity, out have);
                rarities[c.Hero.Rarity] = have + 1;
            }
            double expectedShare = 1.0 / types.Count;   // about 14.3 percent
            foreach (PowerUpDef d in types)
                Assert.AreEqual(expectedShare, perType[d.Id] / (double)totalUnits, 0.007, d.Id + " should drop about 14.3 percent of the time");
            // Region 1 has Common to Legendary heroes, so every drawn tier exists and is used as drawn (the fallback only matters for closed regions; the pure odds are in ChestCardTests)
            foreach (CharacterRarity r in rarities.Keys)
                Assert.That(r != CharacterRarity.Legendary, "a chest never gives a Legendary hero: " + r);
            if (kind == ChestKind.Iron)
            {
                Assert.AreEqual(0.70, rarities[CharacterRarity.Common] / (double)Runs, 0.01, "Iron: 70 percent Common");
                Assert.AreEqual(0.20, rarities[CharacterRarity.Uncommon] / (double)Runs, 0.01, "Iron: 20 percent Uncommon");
                Assert.AreEqual(0.10, rarities[CharacterRarity.Rare] / (double)Runs, 0.01, "Iron: 10 percent Rare");
                Assert.IsFalse(rarities.ContainsKey(CharacterRarity.Epic), "Iron never gives Elite");
            }
            else
            {
                Assert.IsFalse(rarities.ContainsKey(CharacterRarity.Common), "Gold never gives a Common hero");
                Assert.AreEqual(0.50, rarities[CharacterRarity.Uncommon] / (double)Runs, 0.01, "Gold: 50 percent Uncommon");
                Assert.AreEqual(0.40, rarities[CharacterRarity.Rare] / (double)Runs, 0.01, "Gold: 40 percent Rare");
                Assert.AreEqual(0.10, rarities[CharacterRarity.Epic] / (double)Runs, 0.01, "Gold: 10 percent Elite");
            }
        }

        [Test]
        public void ExactlyOneHeroCopyIsAddedPerChestAndNothingIsRefunded()
        {
            foreach (ChestKind kind in new[] { ChestKind.Iron, ChestKind.Golden })
            {
                ProgressService svc = NewService();
                svc.Data.Gems = 100000;
                int copies0 = TotalCopies(svc), gems0 = svc.Gems;
                for (int round = 0; round < 25; round++)   // many openings of the same chest: duplicates are certain with five region heroes
                {
                    List<ChestContents> got;
                    int before = TotalCopies(svc), gems = svc.Gems;
                    Assert.AreEqual(ChestOpenResult.Ok, svc.TryOpenChests(kind, 1, out got));
                    Assert.AreEqual(1, got.Count);
                    Assert.IsNotNull(got[0].Hero);
                    Assert.AreEqual(before + 1, TotalCopies(svc), "exactly one hero copy per chest");
                    Assert.AreEqual(gems - ChestRolls.Price(kind, 1), svc.Gems, "no gems come back, not even for a duplicate");
                }
                Assert.AreEqual(copies0 + 25, TotalCopies(svc));
                Assert.AreEqual(gems0 - 25 * ChestRolls.Price(kind, 1), svc.Gems);
            }
        }

        [Test]
        public void OpenTenAddsTenHeroCopiesAndTheHeroIsKeptEvenWhenOwned()
        {
            ProgressService svc = NewService();
            svc.Data.Gems = 100000;
            svc.AddCopies("momo", 1);
            int before = TotalCopies(svc);
            List<ChestContents> got;
            Assert.AreEqual(ChestOpenResult.Ok, svc.TryOpenChests(ChestKind.Iron, 10, out got));
            Assert.AreEqual(10, got.Count);
            Assert.AreEqual(before + 10, TotalCopies(svc));
            foreach (ChestContents c in got) Assert.IsNotNull(c.Hero);
        }

        [Test]
        public void OnlyHeroesOfOpenRegionsDropAndTheOpenRegionsGrowTheirPool()
        {
            ProgressService svc = NewService();   // only region 1 is open
            svc.Data.Gems = 1000000;
            var seen = new HashSet<string>();
            for (int i = 0; i < 80; i++)
            {
                List<ChestContents> got;
                Assert.AreEqual(ChestOpenResult.Ok, svc.TryOpenChests(i % 2 == 0 ? ChestKind.Iron : ChestKind.Golden, 1, out got));
                seen.Add(got[0].Hero.Id);
                Assert.AreEqual(1, BundleCards.RegionOf(got[0].Hero, CharacterDefs.All), got[0].Hero.Id + " belongs to an open region");
            }
            Assert.That(seen.Count, Is.GreaterThan(0));
            Assert.IsFalse(seen.Contains("forestgirl"), "region 2 is locked");
            Assert.IsFalse(seen.Contains("monk"), "region 5 is locked");
        }

        [Test]
        public void TheSameAccountSeedAndCounterGiveTheSameChests()
        {
            ProgressService a = NewService(), b = NewService();
            a.Data.Gems = b.Data.Gems = 100000;
            List<ChestContents> ra, rb;
            Assert.AreEqual(ChestOpenResult.Ok, a.TryOpenChests(ChestKind.Golden, 10, out ra));
            Assert.AreEqual(ChestOpenResult.Ok, b.TryOpenChests(ChestKind.Golden, 10, out rb));
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(ra[i].Hero.Id, rb[i].Hero.Id);
                Assert.AreEqual(ra[i].Coins, rb[i].Coins);
                CollectionAssert.AreEqual(ra[i].PowerUps, rb[i].PowerUps);
            }
            // another account seed gives another sequence
            ProgressService c = NewService();
            c.Data.Bundles.Seed = 99;
            c.Data.Gems = 100000;
            List<ChestContents> rc;
            Assert.AreEqual(ChestOpenResult.Ok, c.TryOpenChests(ChestKind.Golden, 10, out rc));
            bool differs = false;
            for (int i = 0; i < 10; i++) differs |= rc[i].Coins != ra[i].Coins || rc[i].Hero.Id != ra[i].Hero.Id;
            Assert.IsTrue(differs);
        }

        [Test]
        public void BuyTenIsTenIndependentOpenings()
        {
            foreach (ChestKind kind in new[] { ChestKind.Iron, ChestKind.Golden })
            {
                ProgressService ten = NewService(), one = NewService();
                ten.Data.Gems = one.Data.Gems = 5000;
                List<ChestContents> a;
                Assert.AreEqual(ChestOpenResult.Ok, ten.TryOpenChests(kind, 10, out a));
                Assert.AreEqual(10, a.Count);
                var b = new List<ChestContents>();
                for (int i = 0; i < 10; i++)
                {
                    List<ChestContents> single;
                    Assert.AreEqual(ChestOpenResult.Ok, one.TryOpenChests(kind, 1, out single));
                    b.Add(single[0]);
                }
                int coinsA = 0, coinsB = 0;
                for (int i = 0; i < 10; i++)
                {
                    CollectionAssert.AreEqual(b[i].PowerUps, a[i].PowerUps);
                    Assert.AreEqual(b[i].Coins, a[i].Coins);
                    Assert.AreEqual(b[i].Hero.Id, a[i].Hero.Id);
                    coinsA += a[i].Coins;
                    coinsB += b[i].Coins;
                }
                Assert.AreEqual(coinsB, coinsA);
                Assert.AreEqual(one.Gems, ten.Gems, "ten openings cost exactly ten single prices");
                Assert.AreEqual(5000 - ChestRolls.Price(kind, 10), ten.Gems);
                Assert.AreEqual(one.Coins, ten.Coins);
                foreach (PowerUpDef d in ten.PowerUpDefs.Implemented) Assert.AreEqual(one.PowerUpCount(d.Id), ten.PowerUpCount(d.Id), d.Id);
                Assert.AreEqual(one.Data.ChestOpens, ten.Data.ChestOpens);
                Assert.AreEqual(TotalCopies(one), TotalCopies(ten));
            }
        }

        [Test]
        public void TooFewGemsSpendsAndGrantsNothing()
        {
            ProgressService svc = NewService();
            int coins = svc.Coins, copies = TotalCopies(svc);
            var before = new Dictionary<string, int>();
            foreach (PowerUpDef d in svc.PowerUpDefs.Implemented) before[d.Id] = svc.PowerUpCount(d.Id);
            svc.Data.Gems = ChestRolls.IronPrice - 1;
            List<ChestContents> got;
            Assert.AreEqual(ChestOpenResult.NotEnoughGems, svc.TryOpenChests(ChestKind.Iron, 1, out got));
            Assert.IsNull(got);
            Assert.AreEqual(ChestRolls.IronPrice - 1, svc.Gems);
            Assert.AreEqual(coins, svc.Coins);
            Assert.AreEqual(0, svc.Data.ChestOpens);
            svc.Data.Gems = ChestRolls.GoldenPriceTen - 1;
            Assert.AreEqual(ChestOpenResult.NotEnoughGems, svc.TryOpenChests(ChestKind.Golden, 10, out got));
            Assert.AreEqual(ChestRolls.GoldenPriceTen - 1, svc.Gems);
            Assert.AreEqual(coins, svc.Coins);
            Assert.AreEqual(copies, TotalCopies(svc));
            foreach (PowerUpDef d in svc.PowerUpDefs.Implemented) Assert.AreEqual(before[d.Id], svc.PowerUpCount(d.Id), d.Id);
        }

        [Test]
        public void AnInvalidOpenCountIsRefused()
        {
            ProgressService svc = NewService();
            svc.Data.Gems = 5000;
            List<ChestContents> got;
            Assert.AreEqual(ChestOpenResult.Invalid, svc.TryOpenChests(ChestKind.Iron, 3, out got));
            Assert.AreEqual(5000, svc.Gems);
        }
    }
}
