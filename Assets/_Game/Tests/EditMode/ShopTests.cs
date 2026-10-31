using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Shop economy (docs/14, docs/22 point 4): the owner's pack tables, test gem purchases, coin packs bought with gems, launch promo on and off. The price validators and first-purchase tests are in ShopEconomyTests.</summary>
    public class ShopTests
    {
        static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }

        ContentRepository repo;
        MemorySaveStore store;
        ShopCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
            catalog = ShopCatalog.BuiltIn();
        }

        ProgressService NewService() { return new ProgressService(repo, store); }

        CoinPackDef CoinPack(int coins) { return catalog.CoinPacks.First(p => p.Coins == coins); }
        GemPackDef GemPack(int gems) { return catalog.GemPacks.First(p => p.Gems == gems); }

        // ---- the tables ----

        [Test]
        public void BuiltInTablesMatchTheOwnerBrief()
        {
            int[] gems = { 50, 100, 200, 500, 1000, 2000, 5000, 10000, 15000 };
            double[] list = { 0.79, 1.49, 2.49, 5.99, 10.99, 20.99, 49.99, 89.99, 119.99 };
            int[] inr = { 75, 142, 237, 569, 1044, 1994, 4749, 8549, 11399 };
            Assert.AreEqual(9, catalog.GemPacks.Count);
            for (int i = 0; i < gems.Length; i++)
            {
                Assert.AreEqual(gems[i], catalog.GemPacks[i].Gems);
                Assert.AreEqual(list[i], catalog.GemPacks[i].UsdList, 1e-9);
                Assert.AreEqual(inr[i], catalog.GemPacks[i].InrList);
                Assert.LessOrEqual(catalog.GemPacks[i].UsdPromo, catalog.GemPacks[i].UsdList);
            }
            int[] coins = { 1000, 2000, 5000, 10000, 20000, 50000, 100000 };
            int[] gemsList = { 50, 100, 250, 500, 900, 2200, 4000 };
            Assert.AreEqual(7, catalog.CoinPacks.Count);
            for (int i = 0; i < coins.Length; i++)
            {
                Assert.AreEqual(coins[i], catalog.CoinPacks[i].Coins);
                Assert.AreEqual(gemsList[i], catalog.CoinPacks[i].GemsList);
                Assert.LessOrEqual(catalog.CoinPacks[i].GemsPromo, catalog.CoinPacks[i].GemsList);
            }
        }

        [Test]
        public void BuiltInTablesEqualTheContentJsonFiles()
        {
            string dir = Path.Combine(ProjectRoot, "Content", "economy");
            var gems = JsonConvert.DeserializeObject<List<GemPackDef>>(File.ReadAllText(Path.Combine(dir, "gem_packs.json")));
            var coins = JsonConvert.DeserializeObject<List<CoinPackDef>>(File.ReadAllText(Path.Combine(dir, "coin_packs.json")));
            Assert.AreEqual(catalog.GemPacks.Count, gems.Count);
            for (int i = 0; i < gems.Count; i++)
            {
                Assert.AreEqual(catalog.GemPacks[i].Id, gems[i].Id);
                Assert.AreEqual(catalog.GemPacks[i].Gems, gems[i].Gems);
                Assert.AreEqual(catalog.GemPacks[i].UsdList, gems[i].UsdList, 1e-9);
                Assert.AreEqual(catalog.GemPacks[i].UsdPromo, gems[i].UsdPromo, 1e-9);
                Assert.AreEqual(catalog.GemPacks[i].InrList, gems[i].InrList);
                Assert.AreEqual(catalog.GemPacks[i].FirstPurchaseDouble, gems[i].FirstPurchaseDouble);
                Assert.AreEqual(catalog.GemPacks[i].Sku, gems[i].Sku);
                Assert.AreEqual(catalog.GemPacks[i].Badge, gems[i].Badge);
            }
            Assert.AreEqual(catalog.CoinPacks.Count, coins.Count);
            for (int i = 0; i < coins.Count; i++)
            {
                Assert.AreEqual(catalog.CoinPacks[i].Id, coins[i].Id);
                Assert.AreEqual(catalog.CoinPacks[i].Coins, coins[i].Coins);
                Assert.AreEqual(catalog.CoinPacks[i].GemsList, coins[i].GemsList);
                Assert.AreEqual(catalog.CoinPacks[i].GemsPromo, coins[i].GemsPromo);
            }
        }

        [Test]
        public void PromoNeverExceedsListAndSkusAreUnique()
        {
            foreach (GemPackDef p in catalog.GemPacks) Assert.LessOrEqual(p.UsdPromo, p.UsdList, p.Id);
            foreach (CoinPackDef p in catalog.CoinPacks) Assert.LessOrEqual(p.GemsPromo, p.GemsList, p.Id);
            Assert.AreEqual(catalog.GemPacks.Count, catalog.GemPacks.Select(p => p.Sku).Distinct().Count());
        }

        [Test]
        public void RepositoryPacksInTheNewFormatAreUsedOtherwiseTheBuiltInTablesApply()
        {
            // Whatever the manifest holds (old format before the regeneration, new format after), the catalog is complete and valid.
            ShopCatalog fromRepo = ShopCatalog.FromRepository(repo);
            Assert.AreEqual(9, fromRepo.GemPacks.Count);
            Assert.AreEqual(7, fromRepo.CoinPacks.Count);
            Assert.AreEqual(119.99, fromRepo.GemPacks.Last().UsdList, 1e-9);
            Assert.AreEqual(4000, fromRepo.CoinPacks.Last().GemsList);
        }

        [Test]
        public void PriceFormatIsInvariant()
        {
            Assert.AreEqual("$0.79", ShopCatalog.FormatUsd(0.79));
            Assert.AreEqual("$119.99", ShopCatalog.FormatUsd(119.99));
        }

        // ---- test gem purchases ----

        [Test]
        public void TestGemPurchaseGrantsGemsAndSavesAtOnce()
        {
            var svc = NewService();
            int changed = 0;
            svc.Changed += () => changed++;
            Assert.AreEqual(ShopResult.Ok, svc.GrantTestGemPurchase(GemPack(100)));
            Assert.AreEqual(100, svc.Gems);
            Assert.AreEqual(0, svc.Coins);
            Assert.AreEqual(1, changed);
            Assert.AreEqual(100, NewService().Gems, "persisted through the save store");
        }

        [Test]
        public void TestGemPurchaseOfNothingIsInvalid()
        {
            var svc = NewService();
            Assert.AreEqual(ShopResult.Invalid, svc.GrantTestGemPurchase(null));
            Assert.AreEqual(ShopResult.Invalid, svc.GrantTestGemPurchase(new GemPackDef { Id = "gempack_zero", Gems = 0 }));
            Assert.AreEqual(0, svc.Gems);
        }

        // ---- coin packs bought with gems ----

        [Test]
        public void CoinPackDebitsGemsAndCreditsCoinsAtThePromoPrice()
        {
            var svc = NewService();
            svc.AddGems(300);
            Assert.IsFalse(svc.LaunchPromoActive, "promo defaults to off");
            svc.LaunchPromoActive = true;
            Assert.AreEqual(125, svc.CoinPackPrice(CoinPack(5000)));
            Assert.AreEqual(ShopResult.Ok, svc.TryBuyCoinPack(CoinPack(5000)));
            Assert.AreEqual(175, svc.Gems);
            Assert.AreEqual(5000, svc.Coins);
            ProgressService again = NewService();
            Assert.AreEqual(175, again.Gems, "saved at once");
            Assert.AreEqual(5000, again.Coins, "saved at once");
        }

        [Test]
        public void CoinPackUsesTheListPriceWhenThePromoIsOff()
        {
            var svc = NewService();
            svc.AddGems(300);
            Assert.AreEqual(250, svc.CoinPackPrice(CoinPack(5000)));
            Assert.AreEqual(ShopResult.Ok, svc.TryBuyCoinPack(CoinPack(5000)));
            Assert.AreEqual(50, svc.Gems);
            Assert.AreEqual(5000, svc.Coins);
        }

        [Test]
        public void CoinPackWithTooFewGemsChangesNothing()
        {
            var svc = NewService();
            svc.LaunchPromoActive = true;
            svc.AddGems(124);
            svc.AddCoins(7);
            Assert.AreEqual(ShopResult.NotEnoughGems, svc.TryBuyCoinPack(CoinPack(5000)));
            Assert.AreEqual(124, svc.Gems);
            Assert.AreEqual(7, svc.Coins);
            svc.AddGems(1);
            svc.LaunchPromoActive = false;
            Assert.AreEqual(ShopResult.NotEnoughGems, svc.TryBuyCoinPack(CoinPack(5000)), "125 gems is enough only with the promo");
            Assert.AreEqual(125, svc.Gems);
        }

        [Test]
        public void ExactBalanceBuysTheWholePack()
        {
            var svc = NewService();
            svc.AddGems(4000);
            Assert.AreEqual(ShopResult.Ok, svc.TryBuyCoinPack(CoinPack(100000)));
            Assert.AreEqual(0, svc.Gems);
            Assert.AreEqual(100000, svc.Coins);
        }

        [Test]
        public void CoinTotalsSaturateInsteadOfOverflowing()
        {
            var svc = NewService();
            svc.AddCoins(int.MaxValue - 10);
            svc.AddGems(50);
            Assert.AreEqual(ShopResult.Ok, svc.TryBuyCoinPack(CoinPack(1000)));
            Assert.AreEqual(int.MaxValue, svc.Coins);
            Assert.AreEqual(0, svc.Gems);
        }

        [Test]
        public void InvalidCoinPacksAreRejected()
        {
            var svc = NewService();
            svc.AddGems(50);
            Assert.AreEqual(ShopResult.Invalid, svc.TryBuyCoinPack(null));
            Assert.AreEqual(ShopResult.Invalid, svc.TryBuyCoinPack(new CoinPackDef { Id = "coinpack_free", Coins = 100, GemsList = 0, GemsPromo = 0 }));
            Assert.AreEqual(50, svc.Gems);
            Assert.AreEqual(0, svc.Coins);
        }

        // ---- old saves ----

        [Test]
        public void OldSaveWithoutShopFieldsStillLoads()
        {
            var data = JsonConvert.DeserializeObject<SaveData>("{\"version\":1,\"coins\":12,\"gems\":3}");
            Assert.AreEqual(12, data.Coins);
            Assert.AreEqual(3, data.Gems);
        }

        // ---- value table (docs/22 4.1) ----

        [Test]
        public void ValueTableHasTheDocumentedGemsPerDollar()
        {
            GemPackDef top = GemPack(15000);
            Assert.AreEqual(125.0, top.Gems / top.UsdList, 0.05, "list");
            GemPackDef small = GemPack(50);
            Assert.AreEqual(63.3, small.Gems / small.UsdList, 0.05);
            Assert.AreEqual(67.1, GemPack(100).Gems / GemPack(100).UsdList, 0.05);
        }
    }
}
