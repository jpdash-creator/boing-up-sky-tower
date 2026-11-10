using System;
using System.Text;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Monetization;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>Real store purchases (docs/33), the Play Games config and the cloud conflict rule (docs/35): ids, ledger, idempotency, release safety.</summary>
    public class StorePurchaseTests
    {
        ContentRepository repo;
        static readonly DateTime Now = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void SetUp() { repo = ContentRepository.LoadFromResources(); }

        ProgressService NewService() { return new ProgressService(repo, new MemorySaveStore(), 5); }

        [Test]
        public void EveryGemPackSkuIsTheCatalogProductId()
        {
            foreach (GemPackDef pack in ShopCatalog.BuiltIn().GemPacks)
            {
                ProductDef p = ProductCatalog.ForGemPack(pack.Gems);
                Assert.IsNotNull(p, pack.Id);
                Assert.AreEqual(p.Id, pack.Sku, "the pack sku must be the Play Console product id");
            }
        }

        [Test]
        public void ContentRepositoryGemPacksUseCatalogIds()
        {
            foreach (GemPackDef pack in ShopCatalog.FromRepository(repo).GemPacks)
                Assert.AreEqual(ProductCatalog.ForGemPack(pack.Gems).Id, pack.Sku);
        }

        [Test]
        public void GemPurchaseIsGrantedOnceAndTheLedgerKeyIsStable()
        {
            var svc = NewService();
            int before = svc.Gems;
            Assert.AreEqual(StorePurchaseOutcome.Granted, svc.ApplyStorePurchase(ProductCatalog.Gems100, "GPA.1", Now));
            Assert.AreEqual(before + 100, svc.Gems);
            Assert.AreEqual(StorePurchaseOutcome.Duplicate, svc.ApplyStorePurchase(ProductCatalog.Gems100, "GPA.1", Now), "the same order twice adds nothing");
            Assert.AreEqual(before + 100, svc.Gems);
            Assert.AreEqual(StorePurchaseOutcome.Granted, svc.ApplyStorePurchase(ProductCatalog.Gems100, "GPA.2", Now), "a new order is a new purchase");
            Assert.AreEqual(2, svc.Purchases.Count);
            Assert.IsTrue(svc.HasPurchaseRecord(ProductCatalog.Gems100, "GPA.1"));
            Assert.AreEqual("iap:" + ProductCatalog.Gems100 + ":GPA.1", ProgressService.LedgerKey(ProductCatalog.Gems100, "GPA.1"));
        }

        [Test]
        public void UnknownAndPlannedProductsAreNotGranted()
        {
            var svc = NewService();
            Assert.AreEqual(StorePurchaseOutcome.UnknownProduct, svc.ApplyStorePurchase("com.other.gems", "x", Now));
            Assert.AreEqual(StorePurchaseOutcome.UnknownProduct, svc.ApplyStorePurchase(ProductCatalog.StarterPack, "x", Now), "the starter pack is not live");
            Assert.AreEqual(0, svc.Purchases.Count);
        }

        [Test]
        public void SubscriptionPurchaseStartsThePlanOnceAndRefusesTheOtherPlan()
        {
            var svc = NewService();
            Assert.AreEqual(StorePurchaseOutcome.Granted, svc.ApplyStorePurchase(ProductCatalog.SubMonthly, "GPA.m1", Now));
            Assert.AreEqual("monthly", svc.ActiveSubscription(Now));
            Assert.AreEqual(StorePurchaseOutcome.Duplicate, svc.ApplyStorePurchase(ProductCatalog.SubMonthly, "GPA.m1", Now));
            int purchases = svc.Purchases.Count;
            Assert.AreEqual(StorePurchaseOutcome.NotGranted, svc.ApplyStorePurchase(ProductCatalog.SubWeekly, "GPA.w1", Now), "the other plan is running");
            Assert.AreEqual(purchases, svc.Purchases.Count, "a refused purchase leaves no record");
        }

        [Test]
        public void ActiveStoreSubscriptionRenewsOnlyAfterTheLocalTimeRanOut()
        {
            var svc = NewService();
            Assert.AreEqual(StorePurchaseOutcome.Granted, svc.ApplyStorePurchase(ProductCatalog.SubWeekly, "GPA.w1", Now));
            int gems = svc.Gems;
            Assert.AreEqual(StorePurchaseOutcome.Duplicate, svc.ApplyActiveSubscription(ProductCatalog.SubWeekly, "tok", Now.AddDays(3)), "still running locally: nothing to add");
            DateTime later = Now.AddDays(8);
            Assert.AreEqual(StorePurchaseOutcome.Granted, svc.ApplyActiveSubscription(ProductCatalog.SubWeekly, "tok", later), "renewed in the store while the app was closed");
            Assert.AreEqual(gems, svc.Gems, "a renewal pays no instant gems");
            Assert.AreEqual("weekly", svc.ActiveSubscription(later));
            Assert.AreEqual(StorePurchaseOutcome.Duplicate, svc.ApplyActiveSubscription(ProductCatalog.SubWeekly, "tok", later), "not twice");
        }

        [Test]
        public void ReleaseStoreFallbackRefusesEveryPurchase()
        {
            var pay = new UnavailablePaymentService();
            PurchaseResult res = null;
            pay.Purchase(ProductCatalog.Gems50, r => res = r);
            Assert.AreEqual(PurchaseStatus.Unavailable, res.Status);
            Assert.IsFalse(res.Success);
            bool ok = true;
            pay.Restore((s, m) => ok = s);
            Assert.IsFalse(ok);
        }

        // ---- config files ----

        [Test]
        public void PlayGamesProjectIdNeedsTenOrMoreDigits()
        {
            Assert.IsFalse(PlayGamesConfig.IsValidProjectId(PlayGamesConfig.Placeholder));
            Assert.IsFalse(PlayGamesConfig.IsValidProjectId(null));
            Assert.IsFalse(PlayGamesConfig.IsValidProjectId("123456789"));
            Assert.IsFalse(PlayGamesConfig.IsValidProjectId("12345678901a"));
            Assert.IsTrue(PlayGamesConfig.IsValidProjectId("1234567890"));
            Assert.IsTrue(PlayGamesConfig.IsValidProjectId("123456789012"));
            Assert.AreEqual("123456789012", PlayGamesConfig.ParseProjectId("{\"projectId\": \" 123456789012 \"}"));
            Assert.AreEqual(PlayGamesConfig.Placeholder, PlayGamesConfig.ParseProjectId("not json"));
        }

        [Test]
        public void LicenseKeyPlaceholderAndJunkDecodeToNull()
        {
            Assert.IsNull(IapConfig.DecodeKey(IapConfig.Placeholder));
            Assert.IsNull(IapConfig.DecodeKey("not base64!!"));
            Assert.IsNull(IapConfig.DecodeKey(Convert.ToBase64String(new byte[10])), "too short for an RSA key");
            Assert.IsNotNull(IapConfig.DecodeKey(Convert.ToBase64String(new byte[162])));
            Assert.AreEqual(IapConfig.Placeholder, IapConfig.ParseKey("{}"));
        }

        // ---- cloud conflict rule ----

        static byte[] Blob(int levels, int gems, string savedUtc)
        {
            var d = new SaveData();
            for (int i = 0; i < levels; i++) d.Levels["L" + i] = new LevelProgress { Clears = 1, Stars = 1 };
            d.Gems = gems;
            byte[] bytes = SaveSync.Serialize(d);
            SaveEnvelope env = SaveSync.TryDeserialize(bytes);
            env.SavedUtc = savedUtc;
            return Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(env));
        }

        [Test]
        public void ConflictPicksTheSaveWithMoreProgressThenTheNewerOne()
        {
            byte[] few = Blob(2, 900, "2026-11-10T10:00:00.0000000Z");
            byte[] many = Blob(5, 10, "2026-11-09T10:00:00.0000000Z");
            Assert.AreEqual(1, SaveSync.PickGreater(few, many), "more levels cleared wins even when older");
            Assert.AreEqual(0, SaveSync.PickGreater(many, few));
            byte[] newer = Blob(5, 10, "2026-11-10T10:00:00.0000000Z");
            Assert.AreEqual(1, SaveSync.PickGreater(many, newer), "same progress: the newer save wins");
            Assert.AreEqual(0, SaveSync.PickGreater(many, new byte[] { 1, 2, 3 }), "a corrupt blob loses");
            Assert.AreEqual(1, SaveSync.PickGreater(new byte[0], many));
        }
    }
}
