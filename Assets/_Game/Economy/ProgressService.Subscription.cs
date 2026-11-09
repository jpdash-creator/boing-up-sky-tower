using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace FluffyClimbers.Economy
{
    /// <summary>Subscription state in the save (docs/26 section 2). Additive field of SaveData, no version bump.</summary>
    public sealed class SubscriptionData
    {
        /// <summary>"" (none), "weekly" or "monthly".</summary>
        [JsonProperty("type")] public string Type = "";
        /// <summary>End of the paid time, UTC ticks (0 = never bought).</summary>
        [JsonProperty("expiryUtcTicks")] public long ExpiryUtcTicks;
        /// <summary>Local calendar day (yyyyMMdd) of the last daily claim.</summary>
        [JsonProperty("lastClaimDay")] public string LastClaimDay = "";
        /// <summary>Daily claims paid so far on the monthly plan: every 5th one adds the free power-up.</summary>
        [JsonProperty("monthlyClaims")] public int MonthlyClaims;
        /// <summary>Daily claims still owed by the purchases made (one per paid day), so a purchase pays exactly its number of days.</summary>
        [JsonProperty("claimsLeft")] public int ClaimsLeft;
    }

    public enum SubscriptionBuyResult
    {
        Ok,
        /// <summary>Release build: no billing yet (like the gem packs).</summary>
        NotAvailableYet,
        Invalid,
        /// <summary>The other plan is still running: it must end before the player switches.</summary>
        OtherPlanActive
    }

    public enum SubscriptionClaimResult
    {
        Ok,
        NotActive,
        AlreadyClaimedToday
    }

    /// <summary>What one daily claim paid.</summary>
    public sealed class SubscriptionClaim
    {
        public SubscriptionClaimResult Result;
        public int Gems;
        public int Coins;
        /// <summary>Power-up id (pw_...) when this claim included the free power-up, else null.</summary>
        public string PowerUpId;
        public int PowerUpCount;
    }

    /// <summary>
    /// Subscription (docs/26 section 2): weekly Rs 149 / 7 days and monthly Rs 449 / 30 days. Buying pays the instant gems and starts or extends the
    /// period; one daily claim per LOCAL day pays gems + coins (missed days are not stacked) and, on the monthly plan, a free random power-up on
    /// every 5th claim. Every mutation is atomic and saves once. The clock is injectable (tests).
    /// </summary>
    public sealed partial class ProgressService
    {
        SubscriptionCatalog subscriptionDefs;
        Random subscriptionRandom;

        /// <summary>The plans (built-in copy of Content/economy/subscriptions.json; tests and remote overrides may replace it).</summary>
        public SubscriptionCatalog SubscriptionDefs
        {
            get { return subscriptionDefs ?? (subscriptionDefs = SubscriptionCatalog.BuiltIn()); }
            set { subscriptionDefs = value; }
        }

        /// <summary>UTC clock for the parameterless subscription calls. Tests replace it to cross midnight and expiry.</summary>
        public Func<DateTime> SubscriptionClock = () => DateTime.UtcNow;

        /// <summary>Local time zone offset used for the "local day". Null = the device's zone; tests set it to make days deterministic.</summary>
        public TimeSpan? SubscriptionLocalOffset;

        /// <summary>Picks the free power-up: returns a number in [0, exclusiveMax). Null = System.Random; tests replace it.</summary>
        public Func<int, int> SubscriptionPick;

        SubscriptionData Sub
        {
            get
            {
                if (Data.Subscription == null) Data.Subscription = new SubscriptionData();
                if (Data.Subscription.Type == null) Data.Subscription.Type = "";
                if (Data.Subscription.LastClaimDay == null) Data.Subscription.LastClaimDay = "";
                return Data.Subscription;
            }
        }

        /// <summary>Id of the running plan ("weekly" / "monthly") or null when none is active.</summary>
        public string ActiveSubscription() { return ActiveSubscription(SubscriptionClock()); }

        public string ActiveSubscription(DateTime nowUtc)
        {
            return IsActive(nowUtc) ? Sub.Type : null;
        }

        public bool IsActive() { return IsActive(SubscriptionClock()); }

        public bool IsActive(DateTime nowUtc)
        {
            SubscriptionData s = Sub;
            return !string.IsNullOrEmpty(s.Type) && s.ExpiryUtcTicks > nowUtc.Ticks;
        }

        /// <summary>True when this plan is the one running.</summary>
        public bool IsActive(string planId, DateTime nowUtc)
        {
            return IsActive(nowUtc) && Sub.Type == planId;
        }

        public DateTime SubscriptionExpiryUtc()
        {
            long t = Sub.ExpiryUtcTicks;
            return t <= 0 ? DateTime.MinValue : new DateTime(Math.Min(t, DateTime.MaxValue.Ticks), DateTimeKind.Utc);
        }

        public int DaysLeft() { return DaysLeft(SubscriptionClock()); }

        /// <summary>Days left, a started day counts as a day (6 days 2 hours left shows 7); 0 when not active.</summary>
        public int DaysLeft(DateTime nowUtc)
        {
            if (!IsActive(nowUtc)) return 0;
            double days = new TimeSpan(Sub.ExpiryUtcTicks - nowUtc.Ticks).TotalDays;
            return (int)Math.Ceiling(days - 1e-9);
        }

        /// <summary>Calendar day key of the player's LOCAL day at the given UTC moment, "yyyyMMdd".</summary>
        public string LocalDayKey(DateTime nowUtc)
        {
            return LocalTime(nowUtc).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        DateTime LocalTime(DateTime nowUtc)
        {
            DateTime utc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
            TimeSpan offset = SubscriptionLocalOffset ?? TimeZoneInfo.Local.GetUtcOffset(utc);
            return utc + offset;
        }

        /// <summary>Time until the next local midnight (when the next daily reward can be claimed).</summary>
        public TimeSpan TimeToNextClaim(DateTime nowUtc)
        {
            DateTime local = LocalTime(nowUtc);
            return local.Date.AddDays(1) - local;
        }

        public bool CanClaimToday() { return CanClaimToday(SubscriptionClock()); }

        /// <summary>Active, claims still owed, and today's reward not taken yet.</summary>
        public bool CanClaimToday(DateTime nowUtc)
        {
            return IsActive(nowUtc) && Sub.ClaimsLeft > 0 && Sub.LastClaimDay != LocalDayKey(nowUtc);
        }

        /// <summary>
        /// TEST purchase of a plan (development builds with SYSTEM_TEST; the caller passes TestPurchasesEnabled). Release builds return
        /// NotAvailableYet and change nothing, like the gem packs. The real, server-validated purchase will call CreditSubscription directly.
        /// </summary>
        public SubscriptionBuyResult GrantTestSubscriptionPurchase(string planId, bool testPurchasesEnabled) { return GrantTestSubscriptionPurchase(planId, testPurchasesEnabled, SubscriptionClock()); }

        public SubscriptionBuyResult GrantTestSubscriptionPurchase(string planId, bool testPurchasesEnabled, DateTime nowUtc)
        {
            if (!testPurchasesEnabled) return SubscriptionBuyResult.NotAvailableYet;
            SubscriptionDef pd = SubscriptionDefs.Get(planId);   // docs/38: the record is saved in the same single save as the grant
            return GrantWithRecord(ProductIdPrefix + "sub." + planId, PurchaseRecord.KindSubscription, "test-" + nowUtc.Ticks, nowUtc, pd != null ? pd.Days : 0, () => CreditSubscription(planId, nowUtc), r => r == SubscriptionBuyResult.Ok);
        }

        /// <summary>
        /// Credits a paid plan: the instant gems, and the period starts now or, while the same plan is still running, is extended by its days
        /// (buying again stacks). The other plan must have ended first. One step, one save.
        /// </summary>
        public SubscriptionBuyResult CreditSubscription(string planId, DateTime nowUtc)
        {
            SubscriptionDef def = SubscriptionDefs.Get(planId);
            if (def == null || def.Days <= 0) return SubscriptionBuyResult.Invalid;
            SubscriptionData s = Sub;
            bool active = IsActive(nowUtc);
            if (active && s.Type != planId) return SubscriptionBuyResult.OtherPlanActive;

            long from = active ? s.ExpiryUtcTicks : nowUtc.Ticks;
            long add = TimeSpan.TicksPerDay * (long)def.Days;
            s.ExpiryUtcTicks = from > DateTime.MaxValue.Ticks - add ? DateTime.MaxValue.Ticks : from + add;
            s.ClaimsLeft = active ? SaturatingAdd(s.ClaimsLeft, def.Days) : def.Days;   // a lapsed plan forfeits the claims it never used
            s.Type = planId;
            Data.Gems = SaturatingAdd(Data.Gems, def.InstantGems);
            Commit();
            return SubscriptionBuyResult.Ok;
        }

        public SubscriptionClaim ClaimDaily() { return ClaimDaily(SubscriptionClock()); }

        /// <summary>
        /// Claims today's reward (once per local day, missed days are not stacked): the daily gems and coins, and on the monthly plan the free
        /// power-up with every 5th claim. Saturating and atomic: one save. Changes nothing unless the result is Ok.
        /// </summary>
        public SubscriptionClaim ClaimDaily(DateTime nowUtc)
        {
            var claim = new SubscriptionClaim();
            SubscriptionData s = Sub;
            if (!IsActive(nowUtc) || s.ClaimsLeft <= 0) { claim.Result = SubscriptionClaimResult.NotActive; return claim; }
            string today = LocalDayKey(nowUtc);
            if (s.LastClaimDay == today) { claim.Result = SubscriptionClaimResult.AlreadyClaimedToday; return claim; }
            SubscriptionDef def = SubscriptionDefs.Get(s.Type);
            if (def == null) { claim.Result = SubscriptionClaimResult.NotActive; return claim; }

            claim.Gems = def.DailyGems;
            claim.Coins = def.DailyCoins;
            int monthlyClaims = s.MonthlyClaims;
            if (def.PowerUpEveryClaims > 0)
            {
                monthlyClaims++;
                if (monthlyClaims % def.PowerUpEveryClaims == 0 && def.PowerUpCount > 0)
                {
                    PowerUpDef pw = PickFreePowerUp();
                    if (pw != null) { claim.PowerUpId = pw.Id; claim.PowerUpCount = def.PowerUpCount; }
                }
            }

            Data.Gems = SaturatingAdd(Data.Gems, claim.Gems);
            Data.Coins = SaturatingAdd(Data.Coins, claim.Coins);
            if (claim.PowerUpId != null)
            {
                int have = PowerUpCount(claim.PowerUpId);
                Data.Inventory.Powerups[claim.PowerUpId] = (int)Math.Min((long)have + claim.PowerUpCount, int.MaxValue);
            }
            s.MonthlyClaims = monthlyClaims;
            s.ClaimsLeft--;
            s.LastClaimDay = today;
            claim.Result = SubscriptionClaimResult.Ok;
            Commit();
            return claim;
        }

        PowerUpDef PickFreePowerUp()
        {
            IReadOnlyList<PowerUpDef> list = PowerUpDefs.Implemented;
            if (list.Count == 0) return null;
            int i;
            if (SubscriptionPick != null) i = SubscriptionPick(list.Count);
            else
            {
                if (subscriptionRandom == null) subscriptionRandom = new Random();
                i = subscriptionRandom.Next(list.Count);
            }
            return list[Math.Max(0, Math.Min(list.Count - 1, i))];
        }
    }
}
