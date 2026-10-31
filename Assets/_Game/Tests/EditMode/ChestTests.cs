using System.Collections.Generic;
using BoingUpSkyTower.Economy;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Treasure chest rules of docs/22 point 3: loot ranges, equal power-up chances, value, Buy 10 and spending.</summary>
    public class ChestTests
    {
        const int Runs = 100000, CoinsPerGem = 20;
        const double IronReferenceGems = 86.0, GoldenReferenceGems = 242.0;

        static ProgressService NewService()
        {
            return new ProgressService(Content.ContentRepository.LoadFromResources(), new MemorySaveStore(), 5);
        }

        static List<PowerUpDef> Types()
        {
            return new List<PowerUpDef>(NewService().PowerUpDefs.Implemented);
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
            Assert.IsFalse(ChestRolls.CharactersEnabled);
        }

        [Test]
        public void IronSimulationKeepsRangesEqualChancesAndValue()
        {
            Simulate(ChestKind.Iron, 400, 600, 1, IronReferenceGems);
        }

        [Test]
        public void GoldenSimulationKeepsRangesEqualChancesAndValue()
        {
            Simulate(ChestKind.Golden, 1000, 1400, 3, GoldenReferenceGems);
        }

        static void Simulate(ChestKind kind, int minCoins, int maxCoins, int units, double referenceGems)
        {
            List<PowerUpDef> types = Types();
            var perType = new Dictionary<string, int>();
            var gemValue = new Dictionary<string, double>();
            foreach (PowerUpDef d in types) { perType[d.Id] = 0; gemValue[d.Id] = (double)d.CoinPrice / CoinsPerGem; }
            double totalGems = 0.0;
            long totalUnits = 0;
            for (int i = 0; i < Runs; i++)
            {
                ChestContents c = ChestRolls.Roll(kind, i, types);
                Assert.That(c.Coins, Is.InRange(minCoins, maxCoins));
                Assert.AreEqual(units, c.PowerUps.Count);
                Assert.AreEqual(0, c.Characters.Count);
                double gems = c.Coins / (double)CoinsPerGem;
                foreach (string id in c.PowerUps) { perType[id]++; totalUnits++; gems += gemValue[id]; }
                totalGems += gems;
            }
            double expectedShare = 1.0 / types.Count;   // about 14.3 percent
            foreach (PowerUpDef d in types)
                Assert.AreEqual(expectedShare, perType[d.Id] / (double)totalUnits, 0.007, d.Id + " should drop about 14.3 percent of the time");
            double average = totalGems / Runs;
            Assert.AreEqual(referenceGems, average, referenceGems * 0.01, kind + " average value in gems");
            Assert.Less(average, ChestRolls.Price(kind, 1), "a chest never beats its price on average");
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
                    coinsA += a[i].Coins;
                    coinsB += b[i].Coins;
                }
                Assert.AreEqual(coinsB, coinsA);
                Assert.AreEqual(one.Gems, ten.Gems, "ten openings cost exactly ten single prices");
                Assert.AreEqual(5000 - ChestRolls.Price(kind, 10), ten.Gems);
                Assert.AreEqual(one.Coins, ten.Coins);
                foreach (PowerUpDef d in ten.PowerUpDefs.Implemented) Assert.AreEqual(one.PowerUpCount(d.Id), ten.PowerUpCount(d.Id), d.Id);
                Assert.AreEqual(one.Data.ChestOpens, ten.Data.ChestOpens);
            }
        }

        [Test]
        public void TooFewGemsSpendsAndGrantsNothing()
        {
            ProgressService svc = NewService();
            int coins = svc.Coins;
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
