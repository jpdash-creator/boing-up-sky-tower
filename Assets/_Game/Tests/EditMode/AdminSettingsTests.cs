using System;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Monetization;
using NUnit.Framework;
using UnityEngine;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Settings admin tools, Reset Progress and Restore Purchases (docs/38).</summary>
    public class AdminSettingsTests
    {
        ContentRepository repo;
        int oldUnlocked;
        static readonly DateTime Now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            oldUnlocked = PlayerPrefs.GetInt("settings.adminunlocked", 0);
            PlayerPrefs.SetInt("settings.adminunlocked", 0);
            oldKeys = new int[TestKeys.Length];
            for (int i = 0; i < TestKeys.Length; i++) oldKeys[i] = PlayerPrefs.GetInt(TestKeys[i], -1);
            oldAllChars = ProgressService.TestAllCharacters;
            AdminAccess.SetCodeForTests(null);
        }

        int[] oldKeys;
        bool oldAllChars;

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.SetInt("settings.adminunlocked", oldUnlocked);
            for (int i = 0; i < TestKeys.Length; i++)
            {
                if (oldKeys[i] < 0) PlayerPrefs.DeleteKey(TestKeys[i]);
                else PlayerPrefs.SetInt(TestKeys[i], oldKeys[i]);
            }
            ProgressService.TestAllCharacters = oldAllChars;
            AdminAccess.DevelopmentBuildOverride = null;
            AdminAccess.SetCodeForTests(null);
            GameSettings.ApplyFlags();
        }

        ProgressService NewService() { return new ProgressService(repo, new MemorySaveStore(), 5); }

        // ---- admin gating ----

        [Test]
        public void AdminButtonIsAvailableInDevelopmentBuildsAndAfterTheCode()
        {
            AdminAccess.DevelopmentBuildOverride = true;
            Assert.IsTrue(AdminAccess.Available, "development build");
            AdminAccess.DevelopmentBuildOverride = false;
            Assert.IsFalse(AdminAccess.Available, "release build, not unlocked");
            Assert.IsTrue(AdminAccess.TryUnlock(AdminGate.DefaultCode));
            Assert.IsTrue(AdminAccess.Available, "release build after the code");
            AdminAccess.Lock();
            Assert.IsFalse(AdminAccess.Available, "locked again");
        }

        [Test]
        public void WrongCodeDoesNothing()
        {
            AdminAccess.DevelopmentBuildOverride = false;
            foreach (string bad in new[] { "", "   ", null, "0000-TES", "1234", "0000-TEST-" })
            {
                Assert.IsFalse(AdminAccess.TryUnlock(bad), "code: " + bad);
                Assert.IsFalse(AdminAccess.Unlocked);
                Assert.IsFalse(AdminAccess.Available);
            }
        }

        [Test]
        public void ConfigFileCodeIsTheDefaultDocumentedOne()
        {
            Assert.AreEqual("0000-TEST", AdminGate.DefaultCode);
            Assert.AreEqual("0000-TEST", AdminAccess.Code, "Resources/Services/admin_config.json ships the default code");
        }

        [Test]
        public void CodeFromConfigIsUsed()
        {
            AdminAccess.DevelopmentBuildOverride = false;
            AdminAccess.SetCodeForTests("ABCD-1234");
            Assert.IsFalse(AdminAccess.TryUnlock("0000-TEST"));
            Assert.IsTrue(AdminAccess.TryUnlock(" abcd-1234 "), "spaces and case are ignored");
        }

        // ---- Admin popup grouping: System Test master switch (Bug List 1 B3) ----

        static readonly string[] TestKeys = { "settings.systemtest", "settings.gamejumptest", "settings.shoptest", "settings.charactertest" };

        /// <summary>An install where the admin code was accepted: the test switches are real (a plain editor run has none).</summary>
        static void AdminInstall() { PlayerPrefs.SetInt("settings.adminunlocked", 1); }

        [Test]
        public void AdminGroupsAreSystemTestThenGameShopCharacter()
        {
            CollectionAssert.AreEqual(new[] { "Game Test", "Shop Test", "Character Test" }, AdminAccess.TestSwitchLabels);
            Assert.AreEqual("PROD READY", AdminAccess.PillText(false), "System Test OFF = PROD READY");
            Assert.AreEqual("TEST MODE", AdminAccess.PillText(true));
            Assert.IsFalse(AdminAccess.TestToolsEnabled(false), "the rows below are disabled while System Test is off");
            Assert.IsTrue(AdminAccess.TestToolsEnabled(true));
        }

        [Test]
        public void SystemTestOffClearsEveryTestSwitchAndStaysOff()
        {
            AdminInstall();
            GameSettings.SystemTest = true;
            GameSettings.GameJumpTest = true;
            GameSettings.ShopTest = true;
            GameSettings.CharacterTest = true;
            Assert.IsTrue(AdminAccess.AnyTestOn);
            Assert.IsFalse(GameSettings.ProdReady);
            Assert.IsTrue(GameSettings.TestUnlockAll, "Game Test + System Test: every level open");
            Assert.IsTrue(ProgressService.TestAllCharacters, "Character Test: every hero owned");

            GameSettings.SystemTest = false;   // PROD READY
            Assert.IsTrue(GameSettings.ProdReady);
            Assert.IsFalse(GameSettings.GameJumpTest);
            Assert.IsFalse(GameSettings.ShopTest);
            Assert.IsFalse(GameSettings.CharacterTest);
            Assert.IsFalse(AdminAccess.AnyTestOn);
            Assert.IsFalse(GameSettings.TestUnlockAll);
            Assert.IsFalse(ProgressService.TestAllCharacters, "the flags are pushed into the progress rules at once");
            foreach (string k in TestKeys) Assert.AreEqual(0, PlayerPrefs.GetInt(k, -1), k + " is stored as off (persists)");

            GameSettings.SystemTest = true;    // back on: the three switches are not silently on again
            Assert.IsFalse(GameSettings.GameJumpTest);
            Assert.IsFalse(GameSettings.ShopTest);
            Assert.IsFalse(GameSettings.CharacterTest);
        }

        [Test]
        public void SubSwitchesOnlyCountWhileSystemTestIsOn()
        {
            AdminInstall();
            GameSettings.SystemTest = false;
            PlayerPrefs.SetInt("settings.shoptest", 1);
            PlayerPrefs.SetInt("settings.charactertest", 1);
            Assert.IsFalse(GameSettings.ShopTest, "a stored Shop Test does nothing with System Test off");
            Assert.IsFalse(GameSettings.CharacterTest);
            GameSettings.ApplyFlags();
            Assert.IsFalse(ProgressService.TestAllCharacters);
            Assert.IsFalse(Gameplay.ShopScreen.TestPurchasesEnabled, "no test purchases in PROD READY");
        }

        [Test]
        public void EachSwitchDrivesOnlyItsOwnArea()
        {
            AdminInstall();
            GameSettings.SystemTest = true;
            GameSettings.GameJumpTest = false;
            GameSettings.ShopTest = true;
            GameSettings.CharacterTest = false;
            Assert.IsTrue(Gameplay.ShopScreen.TestPurchasesEnabled);
            Assert.IsFalse(ProgressService.TestAllCharacters);
            Assert.IsFalse(GameSettings.TestUnlockAll);
            GameSettings.ShopTest = false;
            GameSettings.CharacterTest = true;
            Assert.IsFalse(Gameplay.ShopScreen.TestPurchasesEnabled);
            Assert.IsTrue(ProgressService.TestAllCharacters);
        }

        [Test]
        public void PlainBuildWithoutTheCodeHasNoTestSwitchOn()
        {
            Assert.IsFalse(GameSettings.DevBuild);
            Assert.IsFalse(GameSettings.SystemTest);
            Assert.IsFalse(GameSettings.ShopTest);
            Assert.IsFalse(GameSettings.CharacterTest);
            Assert.IsFalse(GameSettings.GameJumpTest);
            Assert.IsTrue(GameSettings.ProdReady);
        }

        // ---- reset keeps purchases ----

        [Test]
        public void ResetWipesProgressButKeepsThePurchaseList()
        {
            var svc = NewService();
            svc.AddCoins(500);
            svc.AddGems(80);
            svc.AddCopies("forestgirl", 1);
            Assert.IsTrue(svc.RecordPurchase(ProductCatalog.SubMonthly, PurchaseRecord.KindSubscription, "tx-1", Now, 30));
            svc.CreditSubscription("monthly", Now);
            svc.ResetAll();
            Assert.AreEqual(0, svc.Coins);
            Assert.AreEqual(0, svc.Gems);
            Assert.IsFalse(svc.IsActive(Now), "the subscription state is wiped with the progress");
            Assert.AreEqual(1, svc.Purchases.Count, "the purchase record survives");
            Assert.AreEqual(ProductCatalog.SubMonthly, svc.Purchases[0].ProductId);
        }

        [Test]
        public void PurchasesSurviveSaveAndLoad()
        {
            var store = new MemorySaveStore();
            var svc = new ProgressService(repo, store, 5);
            svc.RecordPurchase(ProductCatalog.Gems100, PurchaseRecord.KindConsumable, "tx-9", Now);
            var again = new ProgressService(repo, store, 5);
            Assert.AreEqual(1, again.Purchases.Count);
            Assert.AreEqual("tx-9", again.Purchases[0].TransactionId);
        }

        [Test]
        public void SameTransactionIsRecordedOnce()
        {
            var svc = NewService();
            Assert.IsTrue(svc.RecordPurchase(ProductCatalog.Gems100, PurchaseRecord.KindConsumable, "tx-1", Now));
            Assert.IsFalse(svc.RecordPurchase(ProductCatalog.Gems100, PurchaseRecord.KindConsumable, "tx-1", Now));
            Assert.IsTrue(svc.RecordPurchase(ProductCatalog.Gems100, PurchaseRecord.KindConsumable, "tx-2", Now));
            Assert.AreEqual(2, svc.Purchases.Count);
        }

        // ---- restore ----

        [Test]
        public void RestoreRegrantsTheSubscriptionAfterAReset()
        {
            var svc = NewService();
            svc.RecordPurchase(ProductCatalog.SubWeekly, PurchaseRecord.KindSubscription, "tx-1", Now, 7);
            svc.CreditSubscription("weekly", Now);
            svc.ResetAll();
            Assert.IsFalse(svc.IsActive(Now.AddDays(1)));
            Assert.AreEqual(1, svc.RestoreFromRecords(Now.AddDays(1)));
            Assert.IsTrue(svc.IsActive(Now.AddDays(1)));
            Assert.AreEqual("weekly", svc.ActiveSubscription(Now.AddDays(1)));
            Assert.AreEqual(6, svc.DaysLeft(Now.AddDays(1)));
            Assert.AreEqual(0, svc.Gems, "restore never pays the instant gems a second time");
            Assert.AreEqual(0, svc.RestoreFromRecords(Now.AddDays(1)), "a second restore finds nothing new");
        }

        [Test]
        public void RestoreStacksPurchasesAndSkipsExpiredOnes()
        {
            var svc = NewService();
            svc.RecordPurchase(ProductCatalog.SubWeekly, PurchaseRecord.KindSubscription, "a", Now, 7);
            svc.RecordPurchase(ProductCatalog.SubWeekly, PurchaseRecord.KindSubscription, "b", Now.AddDays(1), 7);   // bought while the first still ran: stacks
            Assert.AreEqual(1, svc.RestoreFromRecords(Now.AddDays(2)));
            Assert.AreEqual(12, svc.DaysLeft(Now.AddDays(2)), "14 paid days, 2 passed");
            var old = NewService();
            old.RecordPurchase(ProductCatalog.SubWeekly, PurchaseRecord.KindSubscription, "c", Now.AddDays(-30), 7);
            Assert.AreEqual(0, old.RestoreFromRecords(Now), "an ended subscription is nothing to restore");
        }

        [Test]
        public void RestoreNeverRegrantsGemPacksAndWorksWithNoRecords()
        {
            var svc = NewService();
            Assert.AreEqual(0, svc.RestoreFromRecords(Now), "nothing recorded");
            svc.RecordPurchase(ProductCatalog.Gems500, PurchaseRecord.KindConsumable, "g1", Now);
            Assert.AreEqual(0, svc.RestoreFromRecords(Now));
            Assert.AreEqual(0, svc.Gems);
        }

        [Test]
        public void StubPaymentRestoreCompletesAndRecordedStubPurchasesRestore()
        {
            var pay = new StubPaymentService();
            pay.Initialize();
            bool ok = false;
            pay.Restore((success, msg) => ok = success);
            Assert.IsTrue(ok);

            var svc = NewService();
            PurchaseResult res = null;
            pay.Purchase(ProductCatalog.SubMonthly, r => res = r);
            Assert.IsTrue(res.Success);
            svc.RecordPurchase(res.ProductId, PurchaseRecord.KindSubscription, res.TransactionId, Now, 30);
            svc.ResetAll();
            Assert.AreEqual(1, svc.RestoreFromRecords(Now));
            Assert.AreEqual("monthly", svc.ActiveSubscription(Now));
        }

        [Test]
        public void TestPurchasesAreRecordedWithCatalogProductIds()
        {
            Assert.AreEqual(ProductCatalog.Prefix, ProgressService.ProductIdPrefix);
            var svc = NewService();
            GemPackDef pack = ShopCatalog.FromRepository(repo).GemPacks[0];
            Assert.AreEqual(ShopResult.Ok, svc.GrantTestGemPurchase(pack));
            Assert.AreEqual(1, svc.Purchases.Count);
            Assert.IsNotNull(ProductCatalog.Get(svc.Purchases[0].ProductId), svc.Purchases[0].ProductId + " must be a ProductCatalog id");
            Assert.AreEqual(SubscriptionBuyResult.Ok, svc.GrantTestSubscriptionPurchase("weekly", true, Now));
            Assert.AreEqual(ProductCatalog.SubWeekly, svc.Purchases[1].ProductId);
            Assert.AreEqual(7, svc.Purchases[1].Days);
            Assert.AreEqual("weekly", ProgressService.PlanOfProduct(ProductCatalog.SubWeekly));
            Assert.AreEqual("monthly", ProgressService.PlanOfProduct(ProductCatalog.SubMonthly));
            Assert.IsNull(ProgressService.PlanOfProduct(ProductCatalog.Gems50));
        }

        // ---- admin tools ----

        [Test]
        public void HeroTestAddsCopies()
        {
            var svc = NewService();
            int before = svc.Copies("forestgirl");
            Assert.AreEqual(before + 2, svc.AddCopies("forestgirl", 2));
        }

        [Test]
        public void AdminUnlockAndClearAllLevels()
        {
            var svc = NewService();
            Assert.Greater(svc.AdminUnlockAllRegions(), 0);
            Assert.AreEqual(0, svc.AdminUnlockAllRegions(), "second call finds nothing to unlock");
            Assert.AreEqual(75, svc.AdminCompleteAllLevels(), "5 playable regions x 15 levels");
            Assert.IsTrue(svc.IsLevelCleared("lvl_001_01"));
        }

        // ---- Main rewards test switch (Bug List 4 E3) ----

        [Test]
        public void MainRewardsSwitchMakesEveryMainRewardReadyUnclaimedAndUnpaid()
        {
            AdminInstall();
            GameSettings.SystemTest = true;
            var svc = NewService();
            int gems = svc.Gems, coins = svc.Coins, stars = svc.TotalStars();
            Assert.IsFalse(svc.HasClaimable(RewardTab.Main), "fresh save: nothing ready");
            Assert.IsTrue(svc.AdminSetMainRewardsReady(true));
            int regions = 0, starCards = 0;
            foreach (RewardEntry e in svc.ClaimableEntries(RewardTab.Main))
            {
                Assert.IsTrue(svc.IsRewardReady(RewardTab.Main, e), e.Id + " ready");
                Assert.IsFalse(svc.IsRewardClaimed(RewardTab.Main, e), e.Id + " unclaimed");
                if (e.Id.StartsWith("r_")) regions++; else starCards++;
            }
            Assert.AreEqual(5, regions);
            Assert.Greater(starCards, 0);
            Assert.AreEqual(gems, svc.Gems, "nothing paid");
            Assert.AreEqual(coins, svc.Coins);
            Assert.AreEqual(stars, svc.TotalStars(), "real progress untouched");
            Assert.IsTrue(svc.HasClaimable(RewardTab.Main));

            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Main, "r_1").Result, "claiming works afterwards");
            Assert.IsTrue(svc.IsRewardClaimed(RewardTab.Main, svc.RewardDefs.Find(RewardTab.Main, "r_1")));
            Assert.AreEqual(RewardClaimResult.Ok, svc.ClaimAll(RewardTab.Main).Result);
            Assert.IsFalse(svc.HasClaimable(RewardTab.Main), "everything claimed");
        }

        [Test]
        public void MainRewardsSwitchOffRestoresRealProgressAndSurvivesSave()
        {
            AdminInstall();
            GameSettings.SystemTest = true;
            var store = new MemorySaveStore();
            var svc = new ProgressService(repo, store, 5);
            svc.AdminSetMainRewardsReady(true);
            var again = new ProgressService(repo, store, 5);
            Assert.IsTrue(again.AdminMainRewardsReady, "persisted in the save");
            Assert.IsTrue(again.HasClaimable(RewardTab.Main));
            Assert.IsFalse(again.AdminSetMainRewardsReady(false));
            Assert.IsFalse(again.HasClaimable(RewardTab.Main), "back to the real progress");
            foreach (RewardEntry e in again.ClaimableEntries(RewardTab.Main)) Assert.IsFalse(again.IsRewardReady(RewardTab.Main, e));
        }

        [Test]
        public void SystemTestOffDropsTheMainRewardsOverride()
        {
            AdminInstall();
            GameSettings.SystemTest = true;
            var svc = NewService();
            svc.AdminSetMainRewardsReady(true);
            GameSettings.SystemTest = false;   // PROD READY: ApplyFlags withdraws the permission
            Assert.IsFalse(svc.AdminMainRewardsReady, "ignored and cleared without System Test");
            Assert.IsFalse(svc.HasClaimable(RewardTab.Main));
            GameSettings.SystemTest = true;
            Assert.IsFalse(svc.AdminMainRewardsReady, "stays off when System Test comes back");
            GameSettings.SystemTest = false;
            Assert.IsFalse(svc.AdminSetMainRewardsReady(true), "cannot be switched on in PROD READY");
        }
    }
}
