using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FluffyClimbers.Economy;
using FluffyClimbers.Monetization;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>Product ids, prices and ad placements (docs/33). Ids are permanent store ids: these tests guard against accidental changes.</summary>
    public class ServicesTests
    {
        static readonly Regex IdFormat = new Regex("^com\\.jdlabs\\.fluffyclimbers\\.[a-z]+\\.[a-z0-9_]+$");

        [Test]
        public void ProductIdsAreUniqueAndWellFormed()
        {
            var seen = new HashSet<string>();
            foreach (ProductDef p in ProductCatalog.All)
            {
                Assert.IsTrue(p.Id.StartsWith(ProductCatalog.Prefix), p.Id);
                Assert.IsTrue(IdFormat.IsMatch(p.Id), "store-valid id: " + p.Id);
                Assert.LessOrEqual(p.Id.Length, 100, p.Id);
                Assert.IsTrue(seen.Add(p.Id), "duplicate " + p.Id);
                if (p.PromoId != null) Assert.IsTrue(seen.Add(p.PromoId), "duplicate promo id " + p.PromoId);
            }
        }

        [Test]
        public void PricesArePositiveAndPromoIsCheaper()
        {
            foreach (ProductDef p in ProductCatalog.All)
            {
                Assert.Greater(p.PriceUsd, 0.0, p.Id);
                Assert.IsFalse(string.IsNullOrEmpty(p.DisplayName), p.Id);
                if (p.PromoPriceUsd > 0.0) Assert.Less(p.PromoPriceUsd, p.PriceUsd, p.Id);
                if (p.Kind == ProductKind.Subscription) Assert.Greater(p.PeriodDays, 0, p.Id);
            }
        }

        [Test]
        public void GemPackProductsMatchTheShopPrices()
        {
            ShopCatalog shop = ShopCatalog.BuiltIn();
            Assert.AreEqual(shop.GemPacks.Count, ProductCatalog.All.Count(p => p.Kind == ProductKind.Consumable && p.Coins == 0));
            foreach (var pack in shop.GemPacks)
            {
                ProductDef p = ProductCatalog.ForGemPack(pack.Gems);
                Assert.IsNotNull(p, "product for " + pack.Gems + " gems");
                Assert.AreEqual(pack.UsdList, p.PriceUsd, 0.0001, p.Id);
                Assert.AreEqual(pack.UsdPromo, p.PromoPriceUsd, 0.0001, p.Id);
                Assert.AreEqual(pack.Gems, p.Gems, p.Id);
                Assert.IsTrue(p.Live, p.Id);
            }
        }

        [Test]
        public void SubscriptionProductsMatchTheSubscriptionCatalog()
        {
            SubscriptionCatalog subs = SubscriptionCatalog.BuiltIn();
            foreach (SubscriptionDef def in subs.All)
            {
                ProductDef p = ProductCatalog.Get(ProductCatalog.ForSubscription(def.Id));
                Assert.IsNotNull(p, def.Id);
                Assert.AreEqual(ProductKind.Subscription, p.Kind);
                Assert.AreEqual(def.PriceUsd, p.PriceUsd, 0.0001, def.Id);
                Assert.AreEqual(def.Days, p.PeriodDays, def.Id);
                Assert.AreEqual(def.InstantGems, p.Gems, def.Id);
            }
            Assert.AreEqual(subs.All.Count, ProductCatalog.All.Count(p => p.Kind == ProductKind.Subscription));
        }

        [Test]
        public void AdPlacementsAreUniqueLowercaseAndBuiltIsASubset()
        {
            var seen = new HashSet<string>();
            foreach (string p in AdPlacements.All)
            {
                Assert.IsTrue(Regex.IsMatch(p, "^[a-z0-9_]+$"), p);
                Assert.IsTrue(seen.Add(p), "duplicate " + p);
            }
            foreach (string b in AdPlacements.Built) Assert.Contains(b, AdPlacements.All.ToList());
        }

        [Test]
        public void StubAdServiceCompletesTrueOnce()
        {
            var ads = new StubAdService { Immediate = true };
            int calls = 0; bool last = false;
            Assert.IsTrue(ads.IsReady(AdPlacements.Revive));
            ads.ShowRewarded(AdPlacements.Revive, g => { calls++; last = g; });
            Assert.AreEqual(1, calls);
            Assert.IsTrue(last);
            ads.Ready = false;
            ads.ShowRewarded(AdPlacements.Revive, g => { calls++; last = g; });
            Assert.AreEqual(2, calls);
            Assert.IsFalse(last, "not ready completes false");
        }

        [Test]
        public void StubPaymentServiceGrantsKnownProductsOnly()
        {
            var pay = new StubPaymentService();
            PurchaseResult r = null;
            pay.Purchase(ProductCatalog.Gems100, x => r = x);
            Assert.AreEqual(PurchaseStatus.Unavailable, r.Status, "before Initialize");
            pay.Initialize();
            pay.Purchase(ProductCatalog.Gems100, x => r = x);
            Assert.IsTrue(r.Success);
            pay.Purchase("com.other.unknown", x => r = x);
            Assert.AreEqual(PurchaseStatus.Unavailable, r.Status);
        }

        [Test]
        public void LocatorFallsBackToStubsAndConsentDefaultsOff()
        {
            Services.Reset();
            Assert.IsInstanceOf<StubAdService>(Services.Ads);
            Assert.IsInstanceOf<StubPaymentService>(Services.Payments);
            Assert.IsFalse(Services.ConsentGiven);
            Services.Reset();
        }
    }
}
