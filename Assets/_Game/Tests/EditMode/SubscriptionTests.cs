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
            Assert.AreEqual(4, fromJson.All.Count);
            Assert.AreEqual(builtIn.All.Count, fromJson.All.Count);
            for (int i = 0; i < builtIn.All.Count; i++)
            {
                SubscriptionDef a = builtIn.All[i], b = fromJson.All[i];
                Assert.AreEqual(a.Id, b.Id);
                Assert.AreEqual(a.Order, b.Order, a.Id);
                Assert.AreEqual(a.Featured, b.Featured, a.Id);
                CollectionAssert.AreEqual(a.PowerUpOnClaims, b.PowerUpOnClaims, a.Id);
                Assert.AreEqual(a.Chests.Count, b.Chests.Count, a.Id);
                for (int k = 0; k < a.Chests.Count; k++)
                {
                    Assert.AreEqual(a.Chests[k].Kind, b.Chests[k].Kind, a.Id);
                    CollectionAssert.AreEqual(a.Chests[k].Claims, b.Chests[k].Claims, a.Id);
                }
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
            SubscriptionDef w = c.Get("weekly"), wp = c.Get("weekly_plus"), m = c.Get("monthly"), mp = c.Get("monthly_premium");
            Assert.AreEqual(new[] { "weekly", "weekly_plus", "monthly", "monthly_premium" }, new[] { c.All[0].Id, c.All[1].Id, c.All[2].Id, c.All[3].Id }, "card order");
            Assert.AreEqual(1.49, w.PriceUsd, 1e-9);
            Assert.AreEqual(2.99, wp.PriceUsd, 1e-9);
            Assert.AreEqual(4.99, m.PriceUsd, 1e-9);
            Assert.AreEqual(9.99, mp.PriceUsd, 1e-9);
            Assert.AreEqual(new[] { 7, 100, 10, 500 }, new[] { w.Days, w.InstantGems, w.DailyGems, w.DailyCoins });
            Assert.AreEqual(new[] { 7, 250, 20, 1000, 2 }, new[] { wp.Days, wp.InstantGems, wp.DailyGems, wp.DailyCoins, wp.PowerUpEveryClaims });
            Assert.AreEqual(new[] { 30, 200, 10, 600, 5, 1 }, new[] { m.Days, m.InstantGems, m.DailyGems, m.DailyCoins, m.PowerUpEveryClaims, m.PowerUpCount });
            Assert.AreEqual(new[] { 30, 300, 20, 1000, 5, 1 }, new[] { mp.Days, mp.InstantGems, mp.DailyGems, mp.DailyCoins, mp.PowerUpEveryClaims, mp.PowerUpCount });
            Assert.AreEqual(1, w.TotalPowerUps());
            Assert.AreEqual(3, wp.TotalPowerUps());
            Assert.AreEqual(6, m.TotalPowerUps());
            Assert.AreEqual(6, mp.TotalPowerUps());
            Assert.AreEqual(0, w.Chests.Count + m.Chests.Count);
            Assert.AreEqual(1, wp.TotalChests(ChestKind.Iron));
            Assert.AreEqual(0, wp.TotalChests(ChestKind.Golden));
            Assert.AreEqual(3, mp.TotalChests(ChestKind.Golden));
            CollectionAssert.AreEqual(new[] { 1, 11, 21 }, mp.Chests[0].Claims);
        }

        [Test]
        public void CardTextsListTheContentsAndTheDaysEachRepeats()
        {
            SubscriptionCatalog c = SubscriptionCatalog.BuiltIn();
            SubscriptionDef mp = c.Get("monthly_premium"), w = c.Get("weekly"), wp = c.Get("weekly_plus"), m = c.Get("monthly");
            Assert.AreEqual("Now: 300 gems", SubscriptionCatalog.NowLine(mp));
            Assert.AreEqual("20 gems every day for 30 days", SubscriptionCatalog.DailyGemsLine(mp));
            Assert.AreEqual("1,000 coins every day for 30 days", SubscriptionCatalog.DailyCoinsLine(mp));
            Assert.AreEqual("Power-up every 5th day (6 in all)", SubscriptionCatalog.PowerUpLine(mp));
            Assert.AreEqual("Gold chest on days 1, 11 and 21", SubscriptionCatalog.ChestLine(mp.Chests[0]));
            Assert.AreEqual("In all: 900 gems + 30,000 coins", SubscriptionCatalog.TotalsLine1(mp));
            Assert.AreEqual("6 power-ups + 3 Gold chests", SubscriptionCatalog.TotalsLine2(mp));
            Assert.AreEqual("Power-up on day 4", SubscriptionCatalog.PowerUpLine(w));
            Assert.AreEqual("1 power-up", SubscriptionCatalog.TotalsLine2(w));
            Assert.AreEqual("Power-up every 2nd day (3 in all)", SubscriptionCatalog.PowerUpLine(wp));
            Assert.AreEqual("Iron chest on day 1", SubscriptionCatalog.ChestLine(wp.Chests[0]));
            Assert.AreEqual("3 power-ups + 1 Iron chest", SubscriptionCatalog.TotalsLine2(wp));
            Assert.AreEqual("6 power-ups", SubscriptionCatalog.TotalsLine2(m));
            Assert.AreEqual("PAY $4.99", SubscriptionScreenLabel(m));
            StringAssert.Contains("Gold chest on days 1, 11 and 21", SubscriptionCatalog.Describe(mp), "the accessibility label lists the contents");
        }

        static string SubscriptionScreenLabel(SubscriptionDef def) { return FluffyClimbers.Gameplay.SubscriptionScreen.PayLabel(SubscriptionCatalog.AmountText(def)); }

        [Test]
        public void ProductIdsMapToThePlans()
        {
            Assert.AreEqual("weekly", ProgressService.PlanOfProduct(FluffyClimbers.Monetization.ProductCatalog.SubWeekly));
            Assert.AreEqual("weekly_plus", ProgressService.PlanOfProduct(FluffyClimbers.Monetization.ProductCatalog.SubWeeklyPlus));
            Assert.AreEqual("monthly", ProgressService.PlanOfProduct(FluffyClimbers.Monetization.ProductCatalog.SubMonthly));
            Assert.AreEqual("monthly_premium", ProgressService.PlanOfProduct(FluffyClimbers.Monetization.ProductCatalog.SubMonthlyPremium));
            Assert.IsNull(ProgressService.PlanOfProduct("com.jdlabs.fluffyclimbers.sub.yearly"));
            foreach (SubscriptionDef d in SubscriptionCatalog.BuiltIn().All)
                Assert.AreEqual(d.Id, ProgressService.PlanOfProduct(FluffyClimbers.Monetization.ProductCatalog.ForSubscription(d.Id)));
        }

        // ---- value text ----

        [Test]
        public void ValueTextIsComputedFromUsdListRates()
        {
            SubscriptionCatalog c = SubscriptionCatalog.BuiltIn();
            // price measure: gems + coins / 20 + power-ups x 60.7 + chests at their gem price (Iron 100, Gold 300), at $0.011 per gem, divided by the price
            Assert.AreEqual("Worth about 3.0x", SubscriptionCatalog.WorthText(c.Get("weekly")));
            Assert.AreEqual("Worth about 3.8x", SubscriptionCatalog.WorthText(c.Get("weekly_plus")));
            Assert.AreEqual("Worth about 3.9x", SubscriptionCatalog.WorthText(c.Get("monthly")));
            Assert.AreEqual("Worth about 4.0x", SubscriptionCatalog.WorthText(c.Get("monthly_premium")));
            Assert.AreEqual(405.7, SubscriptionCatalog.TotalGemValue(c.Get("weekly")), 1e-6);
            Assert.AreEqual(1022.1, SubscriptionCatalog.TotalGemValue(c.Get("weekly_plus")), 1e-6);
            Assert.AreEqual(1764.2, SubscriptionCatalog.TotalGemValue(c.Get("monthly")), 1e-6);
            Assert.AreEqual(3664.2, SubscriptionCatalog.TotalGemValue(c.Get("monthly_premium")), 1e-6);
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
        public void StarterPaysOneFreePowerUpOnClaimFourOnly()
        {
            ProgressService p = NewService();
            p.GrantTestSubscriptionPurchase("weekly", true);
            for (int day = 1; day <= 7; day++)
            {
                SubscriptionClaim c = p.ClaimDaily();
                Assert.AreEqual(SubscriptionClaimResult.Ok, c.Result);
                Assert.AreEqual(day == 4, c.PowerUpId != null, "claim " + day);
                Assert.AreEqual(0, c.Chests.Count);
                now = now.AddDays(1);
            }
            Assert.AreEqual(1, PowerUpTotal(p));
        }

        [Test]
        public void PlusPaysAPowerUpOnEverySecondClaimAndAnIronChestOnTheFirst()
        {
            ProgressService p = NewService();
            Assert.AreEqual(SubscriptionBuyResult.Ok, p.GrantTestSubscriptionPurchase("weekly_plus", true));
            Assert.AreEqual(250, p.Gems);
            int freePowerUps = 0, chests = 0, chestPowerUps = 0, chestCoins = 0;
            for (int day = 1; day <= 7; day++)
            {
                SubscriptionClaim c = p.ClaimDaily();
                Assert.AreEqual(SubscriptionClaimResult.Ok, c.Result);
                Assert.AreEqual(20, c.Gems);
                Assert.AreEqual(1000, c.Coins);
                Assert.AreEqual(day % 2 == 0, c.PowerUpId != null, "claim " + day);
                if (c.PowerUpId != null) freePowerUps++;
                Assert.AreEqual(day == 1 ? 1 : 0, c.Chests.Count, "claim " + day);
                foreach (SubscriptionChestResult r in c.Chests)
                {
                    Assert.AreEqual(ChestKind.Iron, r.Kind);
                    Assert.AreEqual(ChestRolls.PowerUpCount(ChestKind.Iron), r.Contents.PowerUps.Count);
                    Assert.GreaterOrEqual(r.Contents.Coins, ChestRolls.IronMinCoins);
                    Assert.LessOrEqual(r.Contents.Coins, ChestRolls.IronMaxCoins);
                    chests++; chestPowerUps += r.Contents.PowerUps.Count; chestCoins += r.Contents.Coins;
                }
                now = now.AddDays(1);
            }
            Assert.AreEqual(3, freePowerUps);
            Assert.AreEqual(1, chests);
            Assert.AreEqual(1, p.Data.ChestOpens, "the chest used the normal seeded roll counter");
            Assert.AreEqual(freePowerUps + chestPowerUps, PowerUpTotal(p));
            Assert.AreEqual(7 * 1000 + chestCoins, p.Coins);
            Assert.AreEqual(250 + 7 * 20, p.Gems, "a chest from a plan costs no gems");
        }

        [Test]
        public void PremiumOpensAGoldChestOnClaimsOneElevenAndTwentyOne()
        {
            ProgressService p = NewService();
            Assert.AreEqual(SubscriptionBuyResult.Ok, p.GrantTestSubscriptionPurchase("monthly_premium", true));
            int chests = 0, freePowerUps = 0, chestPowerUps = 0, chestCoins = 0;
            for (int day = 1; day <= 30; day++)
            {
                SubscriptionClaim c = p.ClaimDaily();
                Assert.AreEqual(SubscriptionClaimResult.Ok, c.Result, "claim " + day);
                bool chestDay = day == 1 || day == 11 || day == 21;
                Assert.AreEqual(chestDay ? 1 : 0, c.Chests.Count, "claim " + day);
                Assert.AreEqual(day % 5 == 0, c.PowerUpId != null, "claim " + day);
                if (c.PowerUpId != null) freePowerUps++;
                foreach (SubscriptionChestResult r in c.Chests)
                {
                    Assert.AreEqual(ChestKind.Golden, r.Kind);
                    Assert.AreEqual(ChestRolls.GoldenPowerUps, r.Contents.PowerUps.Count);
                    Assert.GreaterOrEqual(r.Contents.Coins, ChestRolls.GoldenMinCoins);
                    Assert.LessOrEqual(r.Contents.Coins, ChestRolls.GoldenMaxCoins);
                    chests++; chestPowerUps += r.Contents.PowerUps.Count; chestCoins += r.Contents.Coins;
                }
                now = now.AddDays(1);
            }
            Assert.AreEqual(3, chests);
            Assert.AreEqual(6, freePowerUps);
            Assert.AreEqual(3, p.Data.ChestOpens);
            Assert.AreEqual(freePowerUps + chestPowerUps, PowerUpTotal(p));
            Assert.AreEqual(30 * 1000 + chestCoins, p.Coins);
            Assert.AreEqual(300 + 30 * 20, p.Gems);
        }

        [Test]
        public void AnExtensionContinuesTheClaimNumberingAndRepeatsTheChestDays()
        {
            ProgressService p = NewService();
            p.GrantTestSubscriptionPurchase("monthly_premium", true);
            int chests = 0;
            for (int day = 1; day <= 25; day++) { chests += p.ClaimDaily().Chests.Count; now = now.AddDays(1); }
            Assert.AreEqual(3, chests);
            Assert.AreEqual(SubscriptionBuyResult.Ok, p.GrantTestSubscriptionPurchase("monthly_premium", true), "the same plan can be extended while it runs");
            Assert.AreEqual(25, p.Data.Subscription.MonthlyClaims, "the numbering continues");
            Assert.AreEqual(5 + 30, p.Data.Subscription.ClaimsLeft);
            for (int claim = 26; claim <= 31; claim++)
            {
                SubscriptionClaim c = p.ClaimDaily();
                Assert.AreEqual(SubscriptionClaimResult.Ok, c.Result, "claim " + claim);
                Assert.AreEqual(claim == 31 ? 1 : 0, c.Chests.Count, "claim " + claim + ": the schedule repeats every 30 claims (position 1)");
                now = now.AddDays(1);
            }
        }

        [Test]
        public void AFreshPurchaseAfterTheEndStartsTheNumberingAgain()
        {
            ProgressService p = NewService();
            p.GrantTestSubscriptionPurchase("weekly_plus", true);
            for (int day = 1; day <= 3; day++) { p.ClaimDaily(); now = now.AddDays(1); }
            Assert.AreEqual(3, p.Data.Subscription.MonthlyClaims);
            now = now.AddDays(10);   // the plan ended
            Assert.IsFalse(p.IsActive());
            p.GrantTestSubscriptionPurchase("weekly_plus", true);
            Assert.AreEqual(0, p.Data.Subscription.MonthlyClaims);
            SubscriptionClaim first = p.ClaimDaily();
            Assert.AreEqual(1, first.Chests.Count, "claim 1 of the new purchase opens the Iron chest again");
        }

        [Test]
        public void OnlyOnePlanRunsAtATime()
        {
            foreach (string running in new[] { "weekly", "weekly_plus", "monthly", "monthly_premium" })
            {
                store = new MemorySaveStore();   // a fresh account per plan: the store is shared by NewService
                ProgressService p = NewService();
                Assert.AreEqual(SubscriptionBuyResult.Ok, p.GrantTestSubscriptionPurchase(running, true));
                int gems = p.Gems;
                foreach (SubscriptionDef other in p.SubscriptionDefs.All)
                {
                    if (other.Id == running) continue;
                    Assert.AreEqual(SubscriptionBuyResult.OtherPlanActive, p.GrantTestSubscriptionPurchase(other.Id, true), running + " blocks " + other.Id);
                }
                Assert.AreEqual(gems, p.Gems, "a refused purchase changes nothing");
                Assert.AreEqual(running, p.ActiveSubscription());
            }
        }

        [Test]
        public void ChestClaimSurvivesASaveRoundTrip()
        {
            ProgressService p = NewService();
            p.GrantTestSubscriptionPurchase("weekly_plus", true);
            p.ClaimDaily();
            ProgressService again = NewService();
            Assert.AreEqual(1, again.Data.ChestOpens);
            Assert.AreEqual(p.Coins, again.Coins);
            Assert.AreEqual(PowerUpTotal(p), PowerUpTotal(again));
            Assert.IsFalse(again.CanClaimToday(), "exactly once per local day, also after a restart");
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
