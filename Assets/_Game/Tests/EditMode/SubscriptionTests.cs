using System;
using System.IO;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    /// <summary>Subscription economy (docs/26 section 2): buy and extend, one daily claim per local day, the monthly power-up on every 5th claim, expiry, the release gate, save round trip, value text.</summary>
    public class SubscriptionTests
    {
        static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }

        ContentRepository repo;
        MemorySaveStore store;
        DateTime now;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
            now = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        }

        ProgressService NewService()
        {
            var p = new ProgressService(repo, store);
            p.SubscriptionLocalOffset = TimeSpan.Zero;       // local day = UTC day, deterministic on every machine
            p.SubscriptionClock = () => now;
            p.SubscriptionPick = n => 0;
            return p;
        }

        int PowerUpTotal(ProgressService p)
        {
            int n = 0;
            foreach (int c in p.Data.Inventory.Powerups.Values) n += c;
            return n;
        }

        // ---- the table ----

        [Test]
        public void BuiltInCatalogMatchesTheJsonFile()
        {
            SubscriptionCatalog fromJson = SubscriptionCatalog.FromJson(File.ReadAllText(Path.Combine(ProjectRoot, "Content", "economy", "subscriptions.json")));
            SubscriptionCatalog builtIn = SubscriptionCatalog.BuiltIn();
            Assert.AreEqual(2, fromJson.All.Count);
            Assert.AreEqual(builtIn.All.Count, fromJson.All.Count);
            for (int i = 0; i < builtIn.All.Count; i++)
            {
                SubscriptionDef a = builtIn.All[i], b = fromJson.All[i];
                Assert.AreEqual(a.Id, b.Id);
                Assert.AreEqual(a.Name, b.Name, a.Id);
                Assert.AreEqual(a.PriceUsd, b.PriceUsd, 1e-9, a.Id);
                Assert.AreEqual(a.Days, b.Days, a.Id);
                Assert.AreEqual(a.InstantGems, b.InstantGems, a.Id);
                Assert.AreEqual(a.DailyGems, b.DailyGems, a.Id);
                Assert.AreEqual(a.DailyCoins, b.DailyCoins, a.Id);
                Assert.AreEqual(a.PowerUpEveryClaims, b.PowerUpEveryClaims, a.Id);
                Assert.AreEqual(a.PowerUpCount, b.PowerUpCount, a.Id);
            }
        }

        [Test]
        public void TheTablesAreTheOwnerNumbers()
        {
            SubscriptionCatalog c = SubscriptionCatalog.BuiltIn();
            SubscriptionDef w = c.Get("weekly"), m = c.Get("monthly");
            Assert.AreEqual(1.49, w.PriceUsd, 1e-9);
            Assert.AreEqual(4.99, m.PriceUsd, 1e-9);
            Assert.AreEqual(new[] { 7, 100, 10, 500, 0 }, new[] { w.Days, w.InstantGems, w.DailyGems, w.DailyCoins, w.PowerUpEveryClaims });
            Assert.AreEqual(new[] { 30, 200, 10, 600, 5, 1 }, new[] { m.Days, m.InstantGems, m.DailyGems, m.DailyCoins, m.PowerUpEveryClaims, m.PowerUpCount });
        }

        // ---- value text ----

        [Test]
        public void ValueTextIsComputedFromUsdListRates()
        {
            SubscriptionCatalog c = SubscriptionCatalog.BuiltIn();
            Assert.AreEqual("Worth about 2.5x", SubscriptionCatalog.WorthText(c.Get("weekly")));
            Assert.AreEqual("Worth about 3.1x", SubscriptionCatalog.WorthText(c.Get("monthly")));
            Assert.AreEqual("$1.49 / 7 Days", SubscriptionCatalog.PriceText(c.Get("weekly")));
            Assert.AreEqual("$4.99 / 30 Days", SubscriptionCatalog.PriceText(c.Get("monthly")));
        }

        // ---- buy and extend ----

        [Test]
        public void BuyingPaysTheInstantGemsAndStartsThePeriod()
        {
            ProgressService p = NewService();
            Assert.IsFalse(p.IsActive());
            Assert.AreEqual(SubscriptionBuyResult.Ok, p.GrantTestSubscriptionPurchase("weekly", true));
            Assert.AreEqual(100, p.Gems);
            Assert.IsTrue(p.IsActive());
            Assert.AreEqual("weekly", p.ActiveSubscription());
            Assert.AreEqual(7, p.DaysLeft());
            Assert.AreEqual(now.AddDays(7), p.SubscriptionExpiryUtc());
        }

        [Test]
        public void BuyingAgainWhileActiveExtendsTheTime()
        {
            ProgressService p = NewService();
            p.GrantTestSubscriptionPurchase("monthly", true);
            now = now.AddDays(10);
            Assert.AreEqual(20, p.DaysLeft());
            Assert.AreEqual(SubscriptionBuyResult.Ok, p.GrantTestSubscriptionPurchase("monthly", true));
            Assert.AreEqual(50, p.DaysLeft());
            Assert.AreEqual(400, p.Gems);
        }

        [Test]
        public void TheOtherPlanCannotBeBoughtWhileOneRunsButCanAfterExpiry()
        {
            ProgressService p = NewService();
            p.GrantTestSubscriptionPurchase("weekly", true);
            Assert.AreEqual(SubscriptionBuyResult.OtherPlanActive, p.GrantTestSubscriptionPurchase("monthly", true));
            Assert.AreEqual(100, p.Gems, "a refused purchase changes nothing");
            now = now.AddDays(8);
            Assert.AreEqual(SubscriptionBuyResult.Ok, p.GrantTestSubscriptionPurchase("monthly", true));
            Assert.AreEqual("monthly", p.ActiveSubscription());
        }

        [Test]
        public void UnknownPlanIsInvalid()
        {
            ProgressService p = NewService();
            Assert.AreEqual(SubscriptionBuyResult.Invalid, p.GrantTestSubscriptionPurchase("yearly", true));
            Assert.AreEqual(0, p.Gems);
        }

        [Test]
        public void ReleaseBuildsReturnNotAvailableYetAndChangeNothing()
        {
            ProgressService p = NewService();
            Assert.AreEqual(SubscriptionBuyResult.NotAvailableYet, p.GrantTestSubscriptionPurchase("weekly", false));
            Assert.AreEqual(0, p.Gems);
            Assert.IsFalse(p.IsActive());
        }

        // ---- daily claim ----

        [Test]
        public void DailyClaimPaysOncePerLocalDayAcrossMidnight()
        {
            ProgressService p = NewService();
            p.GrantTestSubscriptionPurchase("weekly", true);
            Assert.IsTrue(p.CanClaimToday());
            SubscriptionClaim a = p.ClaimDaily();
            Assert.AreEqual(SubscriptionClaimResult.Ok, a.Result);
            Assert.AreEqual(110, p.Gems);
            Assert.AreEqual(500, p.Coins);
            Assert.IsFalse(p.CanClaimToday());
            Assert.AreEqual(SubscriptionClaimResult.AlreadyClaimedToday, p.ClaimDaily().Result);
            Assert.AreEqual(500, p.Coins, "no second payout the same day");

            now = new DateTime(2026, 11, 1, 23, 59, 0, DateTimeKind.Utc);
            Assert.IsFalse(p.CanClaimToday());
            Assert.AreEqual(TimeSpan.FromMinutes(1), p.TimeToNextClaim(now));
            now = now.AddMinutes(2);   // past local midnight
            Assert.IsTrue(p.CanClaimToday());
            Assert.AreEqual(SubscriptionClaimResult.Ok, p.ClaimDaily().Result);
            Assert.AreEqual(1000, p.Coins);
        }

        [Test]
        public void LocalDayFollowsTheTimeZoneOffset()
        {
            ProgressService p = NewService();
            p.SubscriptionLocalOffset = TimeSpan.FromMinutes(330);   // India
            p.GrantTestSubscriptionPurchase("weekly", true);
            now = new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc);   // 23:30 local on Nov 1
            p.ClaimDaily();
            now = new DateTime(2026, 11, 1, 18, 45, 0, DateTimeKind.Utc);  // 00:15 local on Nov 2
            Assert.IsTrue(p.CanClaimToday());
        }

        [Test]
        public void MissedDaysAreNotStacked()
        {
            ProgressService p = NewService();
            p.GrantTestSubscriptionPurchase("weekly", true);
            now = now.AddDays(4);
            Assert.AreEqual(SubscriptionClaimResult.Ok, p.ClaimDaily().Result);
            Assert.AreEqual(500, p.Coins, "one day's reward only");
            Assert.AreEqual(SubscriptionClaimResult.AlreadyClaimedToday, p.ClaimDaily().Result);
        }

        [Test]
        public void OnePurchasePaysExactlyItsNumberOfDays()
        {
            ProgressService p = NewService();
            p.GrantTestSubscriptionPurchase("weekly", true);
            int paid = 0;
            for (int d = 0; d < 7; d++)
            {
                if (p.ClaimDaily().Result == SubscriptionClaimResult.Ok) paid++;
                now = now.AddHours(24);
                if (d < 6) Assert.IsTrue(p.IsActive());
            }
            // The 8th local day falls inside the 7 x 24 h window because the purchase was at 10:00: still no 8th payout.
            now = p.SubscriptionExpiryUtc().AddMinutes(-1);
            Assert.AreEqual(SubscriptionClaimResult.NotActive, p.ClaimDaily().Result);
            Assert.AreEqual(7, paid);
        }

        [Test]
        public void MonthlyPaysAFreePowerUpOnEveryFifthClaim()
        {
            ProgressService p = NewService();
            p.GrantTestSubscriptionPurchase("monthly", true);
            int powerUpClaims = 0;
            for (int day = 1; day <= 15; day++)
            {
                SubscriptionClaim c = p.ClaimDaily();
                Assert.AreEqual(SubscriptionClaimResult.Ok, c.Result);
                Assert.AreEqual(600, c.Coins);
                bool fifth = day % 5 == 0;
                Assert.AreEqual(fifth, c.PowerUpId != null, "claim " + day);
                if (c.PowerUpId != null) { powerUpClaims++; Assert.AreEqual(1, c.PowerUpCount); Assert.IsNotNull(p.PowerUpDefs.Get(c.PowerUpId)); }
                now = now.AddDays(1);
            }
            Assert.AreEqual(3, powerUpClaims);
            Assert.AreEqual(3, PowerUpTotal(p));
            Assert.AreEqual(15 * 600, p.Coins);
            Assert.AreEqual(200 + 15 * 10, p.Gems);
        }

        [Test]
        public void WeeklyNeverPaysAPowerUp()
        {
            ProgressService p = NewService();
            p.GrantTestSubscriptionPurchase("weekly", true);
            for (int day = 0; day < 7; day++) { Assert.IsNull(p.ClaimDaily().PowerUpId); now = now.AddDays(1); if (day == 6) break; }
            Assert.AreEqual(0, PowerUpTotal(p));
        }

        [Test]
        public void ThePowerUpIsOneOfTheSevenTypes()
        {
            ProgressService p = NewService();
            p.SubscriptionPick = n => { Assert.AreEqual(7, n); return n - 1; };
            p.GrantTestSubscriptionPurchase("monthly", true);
            for (int i = 0; i < 5; i++) { p.ClaimDaily(); now = now.AddDays(1); }
            Assert.AreEqual(1, PowerUpTotal(p));
        }

        // ---- expiry ----

        [Test]
        public void ExpiryEndsTheSubscriptionAndTheClaims()
        {
            ProgressService p = NewService();
            p.GrantTestSubscriptionPurchase("weekly", true);
            now = now.AddDays(7).AddSeconds(-1);
            Assert.IsTrue(p.IsActive());
            Assert.AreEqual(1, p.DaysLeft());
            now = now.AddSeconds(2);
            Assert.IsFalse(p.IsActive());
            Assert.AreEqual(0, p.DaysLeft());
            Assert.IsFalse(p.CanClaimToday());
            Assert.IsNull(p.ActiveSubscription());
            Assert.AreEqual(SubscriptionClaimResult.NotActive, p.ClaimDaily().Result);
            Assert.AreEqual(100, p.Gems, "nothing paid after the end");
        }

        [Test]
        public void ClaimingSaturatesAtTheWalletLimit()
        {
            ProgressService p = NewService();
            p.GrantTestSubscriptionPurchase("weekly", true);
            p.Data.Coins = int.MaxValue - 5;
            p.Data.Gems = int.MaxValue;
            Assert.AreEqual(SubscriptionClaimResult.Ok, p.ClaimDaily().Result);
            Assert.AreEqual(int.MaxValue, p.Coins);
            Assert.AreEqual(int.MaxValue, p.Gems);
        }

        // ---- save ----

        [Test]
        public void StateSurvivesASaveRoundTrip()
        {
            ProgressService p = NewService();
            p.GrantTestSubscriptionPurchase("monthly", true);
            p.ClaimDaily();
            ProgressService again = NewService();
            Assert.IsTrue(again.IsActive());
            Assert.AreEqual("monthly", again.ActiveSubscription());
            Assert.AreEqual(30, again.DaysLeft());
            Assert.IsFalse(again.CanClaimToday(), "today's reward was taken before the restart");
            now = now.AddDays(1);
            Assert.IsTrue(again.CanClaimToday());
            Assert.AreEqual(1, again.Data.Subscription.MonthlyClaims);
        }

        [Test]
        public void OldSavesWithoutTheFieldLoadWithNoSubscription()
        {
            var s = new MemorySaveStore();
            s.Save(new SaveData { Subscription = null });
            var p = new ProgressService(repo, s);
            Assert.IsFalse(p.IsActive());
            Assert.IsFalse(p.CanClaimToday());
            Assert.AreEqual(0, p.DaysLeft());
        }
    }
}
