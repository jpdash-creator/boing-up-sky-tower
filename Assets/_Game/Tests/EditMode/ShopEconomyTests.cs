using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace BoingUpSkyTower.Tests
{
    /// <summary>
    /// Shop economics of docs/22 point 4: the gem and coin pack tables (4.1, 4.2), power-up gem prices (4.3), the validators (4.6), the
    /// first-purchase double (switch OFF by default, once per pack, packs 50 to 1,000 only) and the launch promo ON and OFF.
    /// </summary>
    public class ShopEconomyTests
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

        GemPackDef GemPack(int gems) { return catalog.GemPacks.First(p => p.Gems == gems); }
        CoinPackDef CoinPack(int coins) { return catalog.CoinPacks.First(p => p.Coins == coins); }

        // ---- 4.1 gem packs ----

        [Test]
        public void GemPackTableIsTheOwnerTable()
        {
            int[] gems = { 50, 100, 200, 500, 1000, 2000, 5000, 10000, 15000 };
            double[] usd = { 0.79, 1.49, 2.49, 5.99, 10.99, 20.99, 49.99, 89.99, 119.99 };
            int[] inr = { 75, 142, 237, 569, 1044, 1994, 4749, 8549, 11399 };
            Assert.AreEqual(gems.Length, catalog.GemPacks.Count);
            for (int i = 0; i < gems.Length; i++)
            {
                Assert.AreEqual(gems[i], catalog.GemPacks[i].Gems, "gems " + i);
                Assert.AreEqual(usd[i], catalog.GemPacks[i].UsdList, 1e-9, "usd " + gems[i]);
                Assert.AreEqual(inr[i], catalog.GemPacks[i].InrList, "inr " + gems[i]);
            }
        }

        [Test]
        public void CoinPackTableIsTheOwnerTable()
        {
            int[] coins = { 1000, 2000, 5000, 10000, 20000, 50000, 100000 };
            int[] gems = { 50, 100, 250, 500, 900, 2200, 4000 };
            Assert.AreEqual(coins.Length, catalog.CoinPacks.Count);
            for (int i = 0; i < coins.Length; i++)
            {
                Assert.AreEqual(coins[i], catalog.CoinPacks[i].Coins);
                Assert.AreEqual(gems[i], catalog.CoinPacks[i].GemsList);
            }
        }

        // ---- 4.6 validators ----

        [Test]
        public void DollarsPerGemStrictlyImproveFromTheHundredPackUp()
        {
            List<GemPackDef> up = catalog.GemPacks.Where(p => p.Gems >= 100).OrderBy(p => p.Gems).ToList();
            for (int i = 1; i < up.Count; i++)
            {
                Assert.Less(up[i].UsdList / up[i].Gems, up[i - 1].UsdList / up[i - 1].Gems, "list $/gem " + up[i].Id + " vs " + up[i - 1].Id);
                Assert.Less(up[i].UsdPromo / up[i].Gems, up[i - 1].UsdPromo / up[i - 1].Gems, "promo $/gem " + up[i].Id + " vs " + up[i - 1].Id);
            }
            Assert.Less(catalog.GemPacks.First(p => p.Gems == 100).UsdList, catalog.GemPacks.First(p => p.Gems == 200).UsdList, "prices rise with the pack size");
        }

        [Test]
        public void CoinsPerGemNeverDropAsPacksGrow()
        {
            double[] expected = { 20.0, 20.0, 20.0, 20.0, 22.2, 22.7, 25.0 };
            List<CoinPackDef> packs = catalog.CoinPacks.OrderBy(p => p.Coins).ToList();
            for (int i = 0; i < packs.Count; i++)
            {
                double rate = (double)packs[i].Coins / packs[i].GemsList;
                Assert.AreEqual(expected[i], rate, 0.05, packs[i].Id);
                if (i > 0) Assert.GreaterOrEqual(rate + 1e-9, (double)packs[i - 1].Coins / packs[i - 1].GemsList, packs[i].Id);
                Assert.LessOrEqual(packs[i].GemsPromo, packs[i].GemsList, packs[i].Id);
            }
        }

        [Test]
        public void FirstPurchaseFlagIsOnPacks50To1000Only()
        {
            foreach (GemPackDef p in catalog.GemPacks)
                Assert.AreEqual(p.Gems <= 1000, p.FirstPurchaseDouble, p.Id);
            Assert.AreEqual(5, catalog.GemPacks.Count(p => p.FirstPurchaseDouble), "50, 100, 200, 500, 1000");
        }

        [Test]
        public void PackIdsAndSkusAreUnique()
        {
            Assert.AreEqual(catalog.GemPacks.Count, catalog.GemPacks.Select(p => p.Id).Distinct().Count());
            Assert.AreEqual(catalog.GemPacks.Count, catalog.GemPacks.Select(p => p.Sku).Distinct().Count());
            Assert.AreEqual(catalog.CoinPacks.Count, catalog.CoinPacks.Select(p => p.Id).Distinct().Count());
        }

        [Test]
        public void ContentJsonAndManifestCarryTheSameTables()
        {
            string dir = Path.Combine(ProjectRoot, "Content", "economy");
            var gems = JsonConvert.DeserializeObject<List<GemPackDef>>(File.ReadAllText(Path.Combine(dir, "gem_packs.json")));
            Assert.AreEqual(9, gems.Count);
            Assert.IsTrue(gems.All(g => g.FirstPurchaseDouble == (g.Gems <= 1000)));
            ShopCatalog fromRepo = ShopCatalog.FromRepository(repo);
            Assert.AreEqual(catalog.GemPacks.Select(p => p.Id), fromRepo.GemPacks.Select(p => p.Id), "the manifest equals the built-in table (rebuild the manifest if this fails)");
            Assert.AreEqual(catalog.CoinPacks.Select(p => p.Id), fromRepo.CoinPacks.Select(p => p.Id));
        }

        // ---- 4.3 power-up gem prices ----

        [Test]
        public void PowerUpGemPricesAre50And75()
        {
            PowerUpCatalog c = PowerUpCatalog.BuiltIn();
            foreach (PowerUp t in new[] { PowerUp.Magnet, PowerUp.Shield, PowerUp.WallStick, PowerUp.DoubleJump })
            {
                Assert.AreEqual(1000, c.Get(t).CoinPrice, t.ToString());
                Assert.AreEqual(50, c.Get(t).GemPrice, t.ToString());
            }
            foreach (PowerUp t in new[] { PowerUp.SlowTime, PowerUp.Rocket, PowerUp.Flight })
            {
                Assert.AreEqual(1500, c.Get(t).CoinPrice, t.ToString());
                Assert.AreEqual(75, c.Get(t).GemPrice, t.ToString());
            }
        }

        [Test]
        public void PowerUpJsonHasTheSameGemPrices()
        {
            PowerUpCatalog fromJson = PowerUpCatalog.FromJson(File.ReadAllText(Path.Combine(ProjectRoot, "Content", "economy", "powerups.json")));
            foreach (PowerUpDef d in PowerUpCatalog.BuiltIn().Implemented)
                Assert.AreEqual(d.GemPrice, fromJson.Get(d.Id).GemPrice, d.Id);
        }

        // ---- first-purchase double ----

        [Test]
        public void FirstPurchaseSwitchIsOffByDefaultEverywhere()
        {
            PlayerPrefs.DeleteKey("settings.firstpurchasedouble");
            PlayerPrefs.DeleteKey("settings.launchpromo");
            Assert.IsFalse(GameSettings.FirstPurchaseDouble, "GameSettings");
            Assert.IsFalse(GameSettings.LaunchPromo, "GameSettings promo");
            var svc = NewService();
            Assert.IsFalse(svc.FirstPurchaseDoubleActive, "ProgressService");
            Assert.IsFalse(svc.LaunchPromoActive, "ProgressService promo");
            Assert.IsFalse(svc.IsFirstPurchaseAvailable(GemPack(100)));
        }

        [Test]
        public void WithTheSwitchOffEveryPackGivesExactlyItsGemsAndNoFlagIsSet()
        {
            foreach (GemPackDef pack in catalog.GemPacks)
            {
                var svc = new ProgressService(repo, new MemorySaveStore());
                Assert.AreEqual(ShopResult.Ok, svc.GrantTestGemPurchase(pack), pack.Id);
                Assert.AreEqual(pack.Gems, svc.Gems, pack.Id);
                Assert.IsFalse(svc.FirstPurchaseUsed(pack.Id), pack.Id);
                Assert.AreEqual(ShopResult.Ok, svc.GrantTestGemPurchase(pack));
                Assert.AreEqual(2 * pack.Gems, svc.Gems, pack.Id + " second purchase");
            }
        }

        [Test]
        public void WithTheSwitchOnThePacks50To1000DoubleOnTheFirstPurchaseOnly()
        {
            foreach (GemPackDef pack in catalog.GemPacks.Where(p => p.Gems <= 1000))
            {
                var svc = new ProgressService(repo, new MemorySaveStore());
                svc.FirstPurchaseDoubleActive = true;
                Assert.IsTrue(svc.IsFirstPurchaseAvailable(pack), pack.Id);
                Assert.AreEqual(2 * pack.Gems, svc.GemsFromPack(pack), pack.Id);
                Assert.AreEqual(ShopResult.Ok, svc.GrantTestGemPurchase(pack), pack.Id);
                Assert.AreEqual(2 * pack.Gems, svc.Gems, pack.Id + " first purchase doubled");
                Assert.IsTrue(svc.FirstPurchaseUsed(pack.Id), pack.Id);
                Assert.IsFalse(svc.IsFirstPurchaseAvailable(pack), pack.Id);
                Assert.AreEqual(pack.Gems, svc.GemsFromPack(pack), pack.Id + " later purchases are plain");
                Assert.AreEqual(ShopResult.Ok, svc.GrantTestGemPurchase(pack));
                Assert.AreEqual(3 * pack.Gems, svc.Gems, pack.Id + " second purchase plain");
            }
        }

        [Test]
        public void PacksAbove1000NeverDouble()
        {
            foreach (GemPackDef pack in catalog.GemPacks.Where(p => p.Gems > 1000))
            {
                var svc = new ProgressService(repo, new MemorySaveStore());
                svc.FirstPurchaseDoubleActive = true;
                Assert.IsFalse(svc.IsFirstPurchaseAvailable(pack), pack.Id);
                Assert.AreEqual(ShopResult.Ok, svc.GrantTestGemPurchase(pack));
                Assert.AreEqual(pack.Gems, svc.Gems, pack.Id);
                Assert.IsFalse(svc.FirstPurchaseUsed(pack.Id), pack.Id);
            }
        }

        [Test]
        public void TheFlagIsPerPackAndStoredInTheSave()
        {
            var svc = NewService();
            svc.FirstPurchaseDoubleActive = true;
            svc.GrantTestGemPurchase(GemPack(100));
            Assert.AreEqual(200, svc.Gems);
            Assert.IsTrue(svc.IsFirstPurchaseAvailable(GemPack(200)), "another pack still has its double");
            ProgressService again = NewService();
            again.FirstPurchaseDoubleActive = true;
            Assert.IsTrue(again.FirstPurchaseUsed("gempack_100"), "persisted through the save store");
            Assert.IsFalse(again.IsFirstPurchaseAvailable(GemPack(100)));
            Assert.IsTrue(again.IsFirstPurchaseAvailable(GemPack(200)));
            Assert.IsTrue(again.Data.FirstPurchase.Contains("gempack_100"));
            Assert.AreEqual(1, again.Data.FirstPurchase.Count);
        }

        [Test]
        public void SwitchingOffAfterwardsKeepsTheFlagAndTurningOnLaterStillDoublesAnUnusedPack()
        {
            var svc = NewService();
            svc.GrantTestGemPurchase(GemPack(50));               // switch off: plain, flag not used up
            Assert.AreEqual(50, svc.Gems);
            svc.FirstPurchaseDoubleActive = true;
            Assert.IsTrue(svc.IsFirstPurchaseAvailable(GemPack(50)), "the owner turns the switch on later");
            svc.GrantTestGemPurchase(GemPack(50));
            Assert.AreEqual(150, svc.Gems, "50 plain, then 100 doubled");
            svc.FirstPurchaseDoubleActive = false;
            Assert.IsFalse(svc.IsFirstPurchaseAvailable(GemPack(100)));
            Assert.IsTrue(svc.FirstPurchaseUsed("gempack_50"));
        }

        [Test]
        public void InvalidPacksNeverSetAFlagOrChangeGems()
        {
            var svc = NewService();
            svc.FirstPurchaseDoubleActive = true;
            Assert.AreEqual(ShopResult.Invalid, svc.CreditGemPack(null));
            Assert.AreEqual(ShopResult.Invalid, svc.CreditGemPack(new GemPackDef { Id = "gempack_0", Gems = 0, FirstPurchaseDouble = true }));
            Assert.AreEqual(0, svc.Gems);
            Assert.AreEqual(0, svc.Data.FirstPurchase.Count);
            Assert.AreEqual(0, svc.GemsFromPack(null));
            Assert.IsFalse(svc.IsFirstPurchaseAvailable(null));
        }

        [Test]
        public void GemTotalsSaturateOnTheDouble()
        {
            var svc = NewService();
            svc.FirstPurchaseDoubleActive = true;
            svc.AddGems(int.MaxValue - 10);
            Assert.AreEqual(ShopResult.Ok, svc.GrantTestGemPurchase(GemPack(1000)));
            Assert.AreEqual(int.MaxValue, svc.Gems);
        }

        [Test]
        public void SavesWithoutOrWithANullFirstPurchaseListStillLoad()
        {
            var old = JsonConvert.DeserializeObject<SaveData>("{\"version\":2,\"coins\":1,\"gems\":2}");
            Assert.IsNotNull(old.FirstPurchase);
            Assert.AreEqual(0, old.FirstPurchase.Count);
            var nullList = new MemorySaveStore();
            nullList.Save(new SaveData { Gems = 5, FirstPurchase = null });
            var svc = new ProgressService(repo, nullList);
            svc.FirstPurchaseDoubleActive = true;
            Assert.IsTrue(svc.IsFirstPurchaseAvailable(GemPack(100)));
            Assert.AreEqual(ShopResult.Ok, svc.GrantTestGemPurchase(GemPack(100)));
            Assert.AreEqual(205, svc.Gems);
            Assert.IsTrue(svc.FirstPurchaseUsed("gempack_100"));
        }

        // ---- coin pack purchase flow per pack, promo ON and OFF ----

        [Test]
        public void EveryCoinPackBuysAtItsListPriceWithThePromoOff()
        {
            foreach (CoinPackDef pack in catalog.CoinPacks)
            {
                var svc = new ProgressService(repo, new MemorySaveStore());
                Assert.IsFalse(svc.LaunchPromoActive);
                Assert.AreEqual(pack.GemsList, svc.CoinPackPrice(pack), pack.Id);
                svc.AddGems(pack.GemsList - 1);
                Assert.AreEqual(ShopResult.NotEnoughGems, svc.TryBuyCoinPack(pack), pack.Id + " one gem short");
                Assert.AreEqual(pack.GemsList - 1, svc.Gems, pack.Id);
                Assert.AreEqual(0, svc.Coins, pack.Id);
                svc.AddGems(1);
                Assert.AreEqual(ShopResult.Ok, svc.TryBuyCoinPack(pack), pack.Id);
                Assert.AreEqual(0, svc.Gems, pack.Id);
                Assert.AreEqual(pack.Coins, svc.Coins, pack.Id);
            }
        }

        [Test]
        public void EveryCoinPackBuysAtItsPromoPriceWithThePromoOn()
        {
            foreach (CoinPackDef pack in catalog.CoinPacks)
            {
                var svc = new ProgressService(repo, new MemorySaveStore());
                svc.LaunchPromoActive = true;
                Assert.AreEqual(pack.GemsPromo, svc.CoinPackPrice(pack), pack.Id);
                svc.AddGems(pack.GemsPromo);
                Assert.AreEqual(ShopResult.Ok, svc.TryBuyCoinPack(pack), pack.Id);
                Assert.AreEqual(0, svc.Gems, pack.Id);
                Assert.AreEqual(pack.Coins, svc.Coins, pack.Id);
            }
        }

        [Test]
        public void GemPackUsdFollowsThePromoSwitch()
        {
            foreach (GemPackDef pack in catalog.GemPacks)
            {
                Assert.AreEqual(pack.UsdList, ShopCatalog.GemPackUsd(pack, false), 1e-9, pack.Id);
                Assert.AreEqual(pack.UsdPromo, ShopCatalog.GemPackUsd(pack, true), 1e-9, pack.Id);
            }
        }

        [Test]
        public void PromoAndFirstPurchaseWorkTogether()
        {
            var svc = NewService();
            svc.LaunchPromoActive = true;
            svc.FirstPurchaseDoubleActive = true;
            svc.GrantTestGemPurchase(GemPack(500));
            Assert.AreEqual(1000, svc.Gems, "the double changes gems, not the price");
            Assert.AreEqual(ShopResult.Ok, svc.TryBuyCoinPack(CoinPack(10000)));   // promo price 250
            Assert.AreEqual(750, svc.Gems);
            Assert.AreEqual(10000, svc.Coins);
        }

        // ---- coin pack shortfall link ----

        [Test]
        public void SmallestGemPackCoveringTheShortfall()
        {
            Assert.AreEqual(50, catalog.GemPacks[catalog.SmallestGemPackCovering(1)].Gems);
            Assert.AreEqual(50, catalog.GemPacks[catalog.SmallestGemPackCovering(50)].Gems);
            Assert.AreEqual(100, catalog.GemPacks[catalog.SmallestGemPackCovering(51)].Gems);
            Assert.AreEqual(500, catalog.GemPacks[catalog.SmallestGemPackCovering(201)].Gems);
            Assert.AreEqual(5000, catalog.GemPacks[catalog.SmallestGemPackCovering(2001)].Gems);
            Assert.AreEqual(15000, catalog.GemPacks[catalog.SmallestGemPackCovering(15000)].Gems);
            Assert.AreEqual(15000, catalog.GemPacks[catalog.SmallestGemPackCovering(99999)].Gems, "none covers: the largest pack");
            Assert.AreEqual(-1, new ShopCatalog(new List<GemPackDef>(), new List<CoinPackDef>()).SmallestGemPackCovering(10));
        }

        [Test]
        public void TheBiggestCoinPackNeedsMoreGemsThanThePlayerHasAndTheShortfallIsExact()
        {
            var svc = NewService();
            svc.AddGems(1000);
            CoinPackDef pack = CoinPack(100000);
            int shortfall = svc.CoinPackPrice(pack) - svc.Gems;
            Assert.AreEqual(3000, shortfall);
            Assert.AreEqual(5000, catalog.GemPacks[catalog.SmallestGemPackCovering(shortfall)].Gems, "3,000 gems is covered by the 5,000 pack");
        }
    }
}
