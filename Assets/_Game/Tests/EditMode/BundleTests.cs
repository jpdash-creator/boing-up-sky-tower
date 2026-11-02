using System;
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
    /// <summary>Gem bundles (docs/26 section 3): the 1,000 bundle data, atomic buying, the paid refresh, the 6 saved offers, the timer offer and the save round trip.</summary>
    public class BundleTests
    {
        static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }

        static readonly DateTime T0 = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

        ContentRepository repo;
        MemorySaveStore store;
        BundleCatalog catalog;
        DateTime now;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
            catalog = BundleCatalog.FromJson(File.ReadAllText(Path.Combine(ProjectRoot, "Content", "economy", "bundles.json")));
            now = T0;
        }

        ProgressService NewService(BundleCatalog bundles = null)
        {
            var svc = new ProgressService(repo, store, 5);
            svc.Bundles = bundles ?? catalog;
            svc.BundleClock = () => now;
            return svc;
        }

        static BundleDef Def(string id, int price, int coins, params BundlePowerUp[] pw)
        {
            var d = new BundleDef { Id = id, Name = id, Tier = 1, PriceGems = price, Coins = coins };
            d.PowerUps.AddRange(pw);
            return d;
        }

        static BundlePowerUp Pw(string id, int count) { return new BundlePowerUp { Id = id, Count = count }; }

        static string Snapshot(ProgressService svc) { return JsonConvert.SerializeObject(svc.Data); }

        // ---- the data ----

        [Test]
        public void ThousandBundlesWithPriceRangeAndValueBand()
        {
            Assert.AreEqual(1000, catalog.Count);
            PowerUpCatalog pw = PowerUpCatalog.BuiltIn();
            foreach (BundleDef b in catalog.All)
            {
                Assert.That(b.PriceGems, Is.InRange(200, 500), b.Id);
                Assert.AreEqual(0, b.PriceGems % 10, b.Id);
                Assert.That(b.PowerUps.Count, Is.InRange(1, 4), b.Id);
                Assert.AreEqual(b.PowerUps.Count, b.PowerUps.Select(p => p.Id).Distinct().Count(), b.Id);
                foreach (BundlePowerUp p in b.PowerUps) Assert.That(p.Count, Is.InRange(1, 10), b.Id);
                double ratio = b.ValueGems(pw) / b.PriceGems;
                Assert.That(ratio, Is.InRange(1.4, 1.8), b.Id);
            }
        }

        [Test]
        public void WorthDollarsUsesTheShopPriceList()
        {
            PowerUpCatalog pw = PowerUpCatalog.BuiltIn();
            int gemPrice = pw.Get("pw_magnet").GemPrice;
            BundleDef d = Def("w", 200, 2000, Pw("pw_magnet", 2));
            double expectedGems = 2000 / 20.0 + 2.0 * gemPrice;
            Assert.AreEqual(expectedGems * SubscriptionCatalog.UsdPerGem, BundleWorth.Dollars(d), 1e-9);
            Assert.AreEqual(BundleWorth.Dollars(d), BundleWorth.Dollars(d, pw), 1e-9);
            Assert.AreEqual(0.0, BundleWorth.Dollars(null), 1e-9);
            d.CharacterId = "hero_x";
            Assert.AreEqual(expectedGems * SubscriptionCatalog.UsdPerGem + BundleWorth.CharacterUsd, BundleWorth.Dollars(d), 1e-9, "a character adds its dollar value");
            Assert.That(BundleWorth.Text(Def("t", 200, 0, Pw("pw_magnet", 1))), Does.StartWith("Worth $"));
            foreach (BundleDef b in catalog.All) Assert.That(BundleWorth.Dollars(b), Is.GreaterThan(0.0).And.LessThan(20.0), b.Id);
        }

        [Test]
        public void BundlesAreUnique()
        {
            Assert.AreEqual(1000, catalog.All.Select(b => b.Id).Distinct().Count());
            Assert.AreEqual(1000, catalog.All.Select(b => b.Name).Distinct().Count());
            int sigs = catalog.All.Select(b => b.Coins + "|" + string.Join(",", b.PowerUps.OrderBy(p => p.Id).Select(p => p.Id + "x" + p.Count)) + "|" + b.CharacterId).Distinct().Count();
            Assert.AreEqual(1000, sigs, "no two bundles share their contents");
        }

        [Test]
        public void TheResourcesCopyEqualsTheSourceFile()
        {
            BundleCatalog fromResources = BundleCatalog.LoadFromResources();
            Assert.AreEqual(catalog.Count, fromResources.Count, "Assets/Resources/Content/bundles.json must be the copy written by gen_bundles.py");
            Assert.AreEqual(catalog.All[0].Id, fromResources.All[0].Id);
            Assert.AreEqual(catalog.All[999].Coins, fromResources.All[999].Coins);
        }

        [Test]
        public void BrokenJsonGivesAnEmptyCatalogAndSkipsBadEntries()
        {
            Assert.AreEqual(0, BundleCatalog.FromJson("not json").Count);
            BundleCatalog c = BundleCatalog.FromJson("[{\"id\":\"a\",\"name\":\"A\",\"tier\":1,\"priceGems\":200,\"coins\":1000,\"powerups\":[{\"id\":\"pw_nope\",\"count\":1}],\"characterId\":null}," +
                                                      "{\"id\":\"b\",\"name\":\"B\",\"tier\":1,\"priceGems\":200,\"coins\":1000,\"powerups\":[{\"id\":\"pw_magnet\",\"count\":2}],\"characterId\":null}]");
            Assert.AreEqual(1, c.Count);
            Assert.AreEqual("b", c.All[0].Id);
        }

        // ---- buying ----

        [Test]
        public void BuyingPaysGemsAndGrantsEverythingInOneSave()
        {
            ProgressService svc = NewService();
            svc.AddGems(500);
            svc.AddCoins(100);
            svc.GrantPowerUp("pw_magnet", 2);
            BundleDef d = Def("t1", 300, 12000, Pw("pw_magnet", 3), Pw("pw_rocket", 1));
            int changed = 0;
            svc.Changed += () => changed++;
            Assert.AreEqual(BundleResult.Ok, svc.TryBuyBundle(d));
            Assert.AreEqual(1, changed, "one save");
            Assert.AreEqual(500 - 300, svc.Gems);
            Assert.AreEqual(12100, svc.Coins);
            Assert.AreEqual(5, svc.PowerUpCount("pw_magnet"));
            Assert.AreEqual(1, svc.PowerUpCount("pw_rocket"));
        }

        [Test]
        public void BuyingWithTooFewGemsChangesNothing()
        {
            ProgressService svc = NewService();
            svc.AddGems(299);
            string before = Snapshot(svc);
            Assert.AreEqual(BundleResult.NotEnoughGems, svc.TryBuyBundle(Def("t1", 300, 12000, Pw("pw_magnet", 3))));
            Assert.AreEqual(before, Snapshot(svc));
        }

        [Test]
        public void BuyingABundleWithAnUnknownPowerUpIsRefusedWithoutPartialGrant()
        {
            ProgressService svc = NewService();
            svc.AddGems(1000);
            string before = Snapshot(svc);
            Assert.AreEqual(BundleResult.Invalid, svc.TryBuyBundle(Def("bad", 300, 12000, Pw("pw_magnet", 3), Pw("pw_unknown", 1))));
            Assert.AreEqual(before, Snapshot(svc), "nothing was granted and no gems were taken");
            Assert.AreEqual(BundleResult.Invalid, svc.TryBuyBundle((BundleDef)null));
            Assert.AreEqual(BundleResult.Invalid, svc.TryBuyBundle("no_such_bundle"));
        }

        [Test]
        public void GrantsSaturateAndStockHasNoCap()
        {
            ProgressService svc = NewService();
            svc.AddGems(1000);
            svc.AddCoins(int.MaxValue - 5);
            svc.GrantPowerUp("pw_shield", 500);
            Assert.AreEqual(BundleResult.Ok, svc.TryBuyBundle(Def("t1", 200, 10000, Pw("pw_shield", 10))));
            Assert.AreEqual(int.MaxValue, svc.Coins);
            Assert.AreEqual(510, svc.PowerUpCount("pw_shield"), "no stock cap");
        }

        [Test]
        public void BuyingByIdUsesTheListPrice()
        {
            ProgressService svc = NewService();
            BundleDef d = catalog.All[0];
            svc.AddGems(d.PriceGems);
            Assert.AreEqual(BundleResult.Ok, svc.TryBuyBundle(d.Id));
            Assert.AreEqual(0, svc.Gems, "no gems back");
        }

        // ---- the six offers and the refresh ----

        [Test]
        public void SixDistinctOffersAreDrawnOnceAndStayStable()
        {
            ProgressService svc = NewService();
            List<BundleDef> a = svc.CurrentBundleOffers();
            Assert.AreEqual(6, a.Count);
            Assert.AreEqual(6, a.Select(b => b.Id).Distinct().Count());
            List<BundleDef> again = svc.CurrentBundleOffers();
            CollectionAssert.AreEqual(a.Select(b => b.Id), again.Select(b => b.Id), "stable until refreshed");
            var restarted = new ProgressService(repo, store, 5) { Bundles = catalog, BundleClock = () => now };
            CollectionAssert.AreEqual(a.Select(b => b.Id), restarted.CurrentBundleOffers().Select(b => b.Id), "saved across restarts");
        }

        [Test]
        public void RefreshCosts100CoinsOr5GemsAndDrawsSixNewDistinctOnes()
        {
            ProgressService svc = NewService();
            svc.AddCoins(250);
            svc.AddGems(12);
            List<string> first = svc.CurrentBundleOffers().Select(b => b.Id).ToList();

            Assert.AreEqual(BundleResult.Ok, svc.RefreshBundleOffers(true));
            Assert.AreEqual(150, svc.Coins);
            Assert.AreEqual(12, svc.Gems);
            List<string> second = svc.CurrentBundleOffers().Select(b => b.Id).ToList();
            Assert.AreEqual(6, second.Distinct().Count());
            Assert.IsEmpty(second.Intersect(first), "all six are new");

            Assert.AreEqual(BundleResult.Ok, svc.RefreshBundleOffers(false));
            Assert.AreEqual(150, svc.Coins);
            Assert.AreEqual(7, svc.Gems);
            List<string> third = svc.CurrentBundleOffers().Select(b => b.Id).ToList();
            Assert.AreEqual(6, third.Distinct().Count());
            Assert.IsEmpty(third.Intersect(second));
        }

        [Test]
        public void RefreshWithAShortWalletChangesNothing()
        {
            ProgressService svc = NewService();
            svc.AddCoins(99);
            svc.AddGems(4);
            svc.CurrentBundleOffers();
            string before = Snapshot(svc);
            Assert.AreEqual(BundleResult.NotEnoughCoins, svc.RefreshBundleOffers(true));
            Assert.AreEqual(BundleResult.NotEnoughGems, svc.RefreshBundleOffers(false));
            Assert.AreEqual(before, Snapshot(svc));
        }

        [Test]
        public void DrawsAreRepeatableFromTheStoredSeedAndCounter()
        {
            ProgressService a = NewService();
            a.AddCoins(1000);
            ProgressService b = new ProgressService(repo, new MemorySaveStore(), 5) { Bundles = catalog, BundleClock = () => now };
            b.AddCoins(1000);
            CollectionAssert.AreEqual(a.CurrentBundleOffers().Select(x => x.Id), b.CurrentBundleOffers().Select(x => x.Id));
            a.RefreshBundleOffers(true);
            b.RefreshBundleOffers(true);
            CollectionAssert.AreEqual(a.CurrentBundleOffers().Select(x => x.Id), b.CurrentBundleOffers().Select(x => x.Id));
        }

        [Test]
        public void ASmallCatalogStillGivesDistinctOffers()
        {
            var defs = new List<BundleDef>();
            for (int i = 0; i < 8; i++) defs.Add(Def("s" + i, 200, 1000 + i, Pw("pw_magnet", 1)));
            ProgressService svc = NewService(BundleCatalog.FromDefs(defs));
            svc.AddCoins(300);
            Assert.AreEqual(6, svc.CurrentBundleOffers().Select(b => b.Id).Distinct().Count());
            Assert.AreEqual(BundleResult.Ok, svc.RefreshBundleOffers(true));
            Assert.AreEqual(6, svc.CurrentBundleOffers().Select(b => b.Id).Distinct().Count());
            ProgressService empty = NewService(BundleCatalog.FromDefs(new BundleDef[0]));
            Assert.AreEqual(0, empty.CurrentBundleOffers().Count);
            Assert.AreEqual(BundleResult.Invalid, empty.RefreshBundleOffers(true));
        }

        // ---- the timer offer ----

        int TickUntilOffer(ProgressService svc, double step, int maxSeconds)
        {
            int total = 0;
            while (svc.CurrentTimerOffer(now) == null && total < maxSeconds)
            {
                svc.TickPlayTime(step);
                total += (int)step;
            }
            return total;
        }

        [Test]
        public void TimerOfferAppearsAboutEvery45MinutesOfPlay()
        {
            ProgressService svc = NewService();
            Assert.IsNull(svc.CurrentTimerOffer(now));
            int played = TickUntilOffer(svc, 10, 3600);
            Assert.That(played, Is.InRange(40 * 60, 50 * 60 + 10), "45 minutes plus or minus 5");
            Assert.IsNotNull(svc.CurrentTimerOffer(now));
        }

        [Test]
        public void TimerOfferLastsFiveMinutesAndIsThirtyPercentOffRoundedUp()
        {
            ProgressService svc = NewService();
            TickUntilOffer(svc, 10, 3600);
            BundleDef d = svc.CurrentTimerOffer(now);
            Assert.IsNotNull(d);
            Assert.AreEqual((d.PriceGems * 70 + 99) / 100, svc.TimerOfferPrice(d));
            Assert.AreEqual(140, svc.TimerOfferPrice(Def("p", 200, 1)));
            Assert.AreEqual(161, svc.TimerOfferPrice(Def("p", 230, 1)), "230 x 0.7 is exactly 161");
            Assert.AreEqual(137, svc.TimerOfferPrice(Def("p", 195, 1)), "136.5 rounds up");
            Assert.AreEqual(300, svc.TimerOfferSecondsLeft(now));
            Assert.IsNotNull(svc.CurrentTimerOffer(now.AddSeconds(299)));
            Assert.AreEqual(1, svc.TimerOfferSecondsLeft(now.AddSeconds(299)));
            Assert.IsNull(svc.CurrentTimerOffer(now.AddSeconds(300)), "ends after 5 minutes");
            Assert.AreEqual(0, svc.TimerOfferSecondsLeft(now.AddSeconds(300)));
            Assert.AreEqual(BundleResult.Invalid, svc.TryBuyTimerOffer(now.AddSeconds(301)));
        }

        [Test]
        public void BuyingTheTimerOfferPaysTheDiscountAndEndsIt()
        {
            ProgressService svc = NewService();
            TickUntilOffer(svc, 10, 3600);
            BundleDef d = svc.CurrentTimerOffer(now);
            int price = svc.TimerOfferPrice(d);
            svc.AddGems(price - 1);
            string before = Snapshot(svc);
            Assert.AreEqual(BundleResult.NotEnoughGems, svc.TryBuyTimerOffer(now));
            Assert.AreEqual(before, Snapshot(svc));
            svc.AddGems(1);
            Assert.AreEqual(BundleResult.Ok, svc.TryBuyTimerOffer(now));
            Assert.AreEqual(0, svc.Gems, "no gems back");
            Assert.AreEqual(d.Coins, svc.Coins);
            Assert.IsNull(svc.CurrentTimerOffer(now), "one purchase per offer");
        }

        [Test]
        public void NeverDuringARunAndNeverTwiceInARow()
        {
            ProgressService svc = NewService();
            for (int i = 0; i < 70; i++) svc.TickPlayTime(60, true);   // 70 minutes inside a run: the time counts, no offer starts
            Assert.IsNull(svc.CurrentTimerOffer(now));
            svc.TickPlayTime(1, false);
            Assert.IsNotNull(svc.CurrentTimerOffer(now), "starts right after the run");

            string last = svc.CurrentTimerOffer(now).Id;
            for (int round = 0; round < 25; round++)
            {
                svc.DismissTimerOffer();
                Assert.IsNull(svc.CurrentTimerOffer(now));
                TickUntilOffer(svc, 30, 3600);
                BundleDef d = svc.CurrentTimerOffer(now);
                Assert.IsNotNull(d, "round " + round);
                Assert.AreNotEqual(last, d.Id, "never the same bundle twice in a row");
                last = d.Id;
            }
        }

        [Test]
        public void NeverTwiceInARowEvenWithTwoBundles()
        {
            var defs = new List<BundleDef> { Def("x", 200, 5000, Pw("pw_magnet", 1)), Def("y", 210, 5000, Pw("pw_shield", 1)) };
            ProgressService svc = NewService(BundleCatalog.FromDefs(defs));
            string last = null;
            for (int round = 0; round < 12; round++)
            {
                TickUntilOffer(svc, 30, 3600);
                BundleDef d = svc.CurrentTimerOffer(now);
                Assert.IsNotNull(d);
                if (last != null) Assert.AreNotEqual(last, d.Id);
                last = d.Id;
                svc.DismissTimerOffer();
            }
        }

        [Test]
        public void ExpiredOfferIsReplacedAfterTheNextGap()
        {
            ProgressService svc = NewService();
            TickUntilOffer(svc, 10, 3600);
            string first = svc.CurrentTimerOffer(now).Id;
            now = now.AddMinutes(6);   // wall clock passes the expiry
            Assert.IsNull(svc.CurrentTimerOffer(now));
            svc.TickPlayTime(1);        // clears the stale offer
            Assert.IsNull(svc.CurrentTimerOffer(now));
            int played = TickUntilOffer(svc, 10, 3600);
            Assert.That(played, Is.InRange(35 * 60, 51 * 60));
            Assert.AreNotEqual(first, svc.CurrentTimerOffer(now).Id);
        }

        [Test]
        public void TimerStateSurvivesARestart()
        {
            ProgressService svc = NewService();
            TickUntilOffer(svc, 10, 3600);
            string id = svc.CurrentTimerOffer(now).Id;
            now = now.AddSeconds(100);
            var restarted = new ProgressService(repo, store, 5) { Bundles = catalog, BundleClock = () => now };
            BundleDef d = restarted.CurrentTimerOffer(now);
            Assert.IsNotNull(d);
            Assert.AreEqual(id, d.Id);
            Assert.AreEqual(200, restarted.TimerOfferSecondsLeft(now));
        }

        [Test]
        public void SaveRoundTripKeepsOffersAndCounters()
        {
            ProgressService svc = NewService();
            svc.AddCoins(500);
            svc.CurrentBundleOffers();
            svc.RefreshBundleOffers(true);
            svc.TickPlayTime(120);
            SaveData loaded = store.Load();
            Assert.AreEqual(6, loaded.Bundles.Offers.Count);
            Assert.AreEqual(2, loaded.Bundles.Draws);
            Assert.AreNotEqual(0, loaded.Bundles.Seed);
            Assert.IsTrue(loaded.Bundles.TimerStarted);
            // an old save without the field loads with an empty state
            SaveData old = JsonConvert.DeserializeObject<SaveData>("{\"version\":2,\"coins\":5}");
            Assert.IsNotNull(old.Bundles);
            Assert.AreEqual(0, old.Bundles.Offers.Count);
            // a null field is repaired on first use
            old.Bundles = null;
            var m = new MemorySaveStore();
            m.Save(old);
            var healed = new ProgressService(repo, m, 5) { Bundles = catalog, BundleClock = () => now };
            Assert.AreEqual(6, healed.CurrentBundleOffers().Count);
        }

        [Test]
        public void ResetAllClearsTheBundleState()
        {
            ProgressService svc = NewService();
            svc.CurrentBundleOffers();
            svc.ResetAll();
            Assert.AreEqual(0, svc.Data.Bundles.Offers.Count);
            Assert.AreEqual(6, svc.CurrentBundleOffers().Count);
        }
    }
}
