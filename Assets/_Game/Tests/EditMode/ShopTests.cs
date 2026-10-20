using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkyHopper.Content;
using SkyHopper.Economy;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace SkyHopper.Tests
{
    /// <summary>Shop economy (docs/14): the owner's pack tables, test gem purchases, coin packs bought with gems, launch promo on and off.</summary>
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
            int[] gems = { 10, 20, 50, 100, 200, 500, 1000 };
            double[] list = { 0.99, 1.99, 3.99, 7.99, 11.99, 19.99, 29.99 };
            double[] promo = { 0.49, 0.99, 1.99, 3.99, 5.99, 9.99, 14.99 };
            Assert.AreEqual(7, catalog.GemPacks.Count);
            for (int i = 0; i < gems.Length; i++)
            {
                Assert.AreEqual(gems[i], catalog.GemPacks[i].Gems);
                Assert.AreEqual(list[i], catalog.GemPacks[i].UsdList, 1e-9);
                Assert.AreEqual(promo[i], catalog.GemPacks[i].UsdPromo, 1e-9);
            }
            int[] coins = { 100, 200, 500, 1000, 2000, 5000, 10000 };
            int[] gemsList = { 10, 20, 50, 100, 200, 500, 1000 };
            int[] gemsPromo = { 5, 10, 25, 50, 100, 200, 500 };
            Assert.AreEqual(7, catalog.CoinPacks.Count);
            for (int i = 0; i < coins.Length; i++)
            {
                Assert.AreEqual(coins[i], catalog.CoinPacks[i].Coins);
                Assert.AreEqual(gemsList[i], catalog.CoinPacks[i].GemsList);
                Assert.AreEqual(gemsPromo[i], catalog.CoinPacks[i].GemsPromo);
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
            Assert.AreEqual(7, fromRepo.GemPacks.Count);
            Assert.AreEqual(7, fromRepo.CoinPacks.Count);
            Assert.AreEqual(14.99, fromRepo.GemPacks.Last().UsdPromo, 1e-9);
            Assert.AreEqual(500, fromRepo.CoinPacks.Last().GemsPromo);
        }

        [Test]
        public void PriceFormatIsInvariant()
        {
            Assert.AreEqual("$0.49", ShopCatalog.FormatUsd(0.49));
            Assert.AreEqual("$29.99", ShopCatalog.FormatUsd(29.99));
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
            svc.AddGems(100);
            Assert.IsTrue(svc.LaunchPromoActive, "promo defaults to on");
            Assert.AreEqual(25, svc.CoinPackPrice(CoinPack(500)));
            Assert.AreEqual(ShopResult.Ok, svc.TryBuyCoinPack(CoinPack(500)));
            Assert.AreEqual(75, svc.Gems);
            Assert.AreEqual(500, svc.Coins);
            ProgressService again = NewService();
            Assert.AreEqual(75, again.Gems, "saved at once");
            Assert.AreEqual(500, again.Coins, "saved at once");
        }

        [Test]
        public void CoinPackUsesTheListPriceWhenThePromoIsOff()
        {
            var svc = NewService();
            svc.LaunchPromoActive = false;
            svc.AddGems(100);
            Assert.AreEqual(50, svc.CoinPackPrice(CoinPack(500)));
            Assert.AreEqual(ShopResult.Ok, svc.TryBuyCoinPack(CoinPack(500)));
            Assert.AreEqual(50, svc.Gems);
            Assert.AreEqual(500, svc.Coins);
        }

        [Test]
        public void CoinPackWithTooFewGemsChangesNothing()
        {
            var svc = NewService();
            svc.AddGems(24);
            svc.AddCoins(7);
            Assert.AreEqual(ShopResult.NotEnoughGems, svc.TryBuyCoinPack(CoinPack(500)));
            Assert.AreEqual(24, svc.Gems);
            Assert.AreEqual(7, svc.Coins);
            svc.LaunchPromoActive = false;
            svc.AddGems(1);
            Assert.AreEqual(ShopResult.NotEnoughGems, svc.TryBuyCoinPack(CoinPack(500)), "25 gems is enough only with the promo");
        }

        [Test]
        public void ExactBalanceBuysTheWholePack()
        {
            var svc = NewService();
            svc.AddGems(500);
            Assert.AreEqual(ShopResult.Ok, svc.TryBuyCoinPack(CoinPack(10000)));
            Assert.AreEqual(0, svc.Gems);
            Assert.AreEqual(10000, svc.Coins);
        }

        [Test]
        public void CoinTotalsSaturateInsteadOfOverflowing()
        {
            var svc = NewService();
            svc.AddCoins(int.MaxValue - 10);
            svc.AddGems(5);
            Assert.AreEqual(ShopResult.Ok, svc.TryBuyCoinPack(CoinPack(100)));
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

        // ---- value table (docs/14 section 6) ----

        [Test]
        public void ValueTableHasTheDocumentedGemsPerDollar()
        {
            GemPackDef top = GemPack(1000);
            Assert.AreEqual(33.3, top.Gems / top.UsdList, 0.05, "list");
            Assert.AreEqual(66.7, top.Gems / top.UsdPromo, 0.05, "promo");
            GemPackDef small = GemPack(10);
            Assert.AreEqual(10.1, small.Gems / small.UsdList, 0.05);
            Assert.AreEqual(20.4, small.Gems / small.UsdPromo, 0.05);
        }
    }
}
