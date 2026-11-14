using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace FluffyClimbers.Economy
{
    /// <summary>Subscription state in the save (docs/26 section 2). Additive field of SaveData, no version bump.</summary>
    public sealed class SubscriptionData
    {
        /// <summary>"" (none) or a plan id: "weekly", "weekly_plus", "monthly" or "monthly_premium".</summary>
        [JsonProperty("type")] public string Type = "";
        /// <summary>End of the paid time, UTC ticks (0 = never bought).</summary>
        [JsonProperty("expiryUtcTicks")] public long ExpiryUtcTicks;
        /// <summary>Local calendar day (yyyyMMdd) of the last daily claim.</summary>
        [JsonProperty("lastClaimDay")] public string LastClaimDay = "";
        /// <summary>Daily claims paid so far in the running purchase (all four plans; the name is historic): the claim number that decides the free power-up and the chest days. Reset by a fresh purchase, continued by an extension.</summary>
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
        /// <summary>Chests this claim opened (empty on most days). Their coins, power-ups and hero card are already in the save.</summary>
        public readonly List<SubscriptionChestResult> Chests = new List<SubscriptionChestResult>();
    }

    /// <summary>One chest a daily claim opened for the player.</summary>
    public sealed class SubscriptionChestResult
    {
        public ChestKind Kind;
        public ChestContents Contents;
    }

    /// <summary>
    /// Subscription (docs/26 section 2, four plans since 2026-11-13: Weekly Starter, Weekly Plus, Monthly Standard, Monthly Premium). Buying pays the instant gems and
    /// starts or extends the period (one plan at a time); one daily claim per LOCAL day pays gems + coins (missed days are not stacked), a free random power-up on
    /// the plan's claim days and the plan's chests on their claim days (opened at once through the normal chest roll). Every mutation is atomic and saves once. The clock is injectable (tests).
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
        public SubscriptionBuyResult CreditSubscription(string planId, DateTime nowUtc, bool payInstantGems = true)
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
            if (!active) s.MonthlyClaims = 0;   // a fresh purchase starts the claim numbering again; an extension continues it (the schedule repeats every Days claims)
            s.Type = planId;
            if (payInstantGems) Data.Gems = SaturatingAdd(Data.Gems, def.InstantGems);   // false: a renewal the store reports (no new instant gems)
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
            int monthlyClaims = Math.Max(0, s.MonthlyClaims) + 1;   // this claim's number in the running purchase (1 = first claim)
            int position = def.Position(monthlyClaims);             // 1..Days; an extension repeats the schedule
            int units = def.PowerUpsAt(position);
            if (units > 0)
            {
                PowerUpDef pw = PickFreePowerUp();
                if (pw != null) { claim.PowerUpId = pw.Id; claim.PowerUpCount = units; }
            }

            // Chests of this claim: the normal chest roll (seeded by the save's chest counter and the account seed), nothing is paid in gems.
            List<PowerUpDef> types = null;
            BundleSaveData bundleState = null;
            for (int i = 0; i < def.Chests.Count; i++)
            {
                SubscriptionChestGrant g = def.Chests[i];
                if (Array.IndexOf(g.Claims, position) < 0) continue;
                if (types == null)
                {
                    types = new List<PowerUpDef>(PowerUpDefs.Implemented);
                    if (types.Count == 0) break;
                    bundleState = BundleState;
                    EnsureBundleSeed(bundleState);
                }
                ChestContents c = ChestRolls.Roll(g.ChestKind, Data.ChestOpens++, types, CharacterDefs.All, IsBundleRegionOpen, bundleState.Seed);
                claim.Chests.Add(new SubscriptionChestResult { Kind = g.ChestKind, Contents = c });
            }

            Data.Gems = SaturatingAdd(Data.Gems, claim.Gems);
            Data.Coins = SaturatingAdd(Data.Coins, claim.Coins);
            if (claim.PowerUpId != null)
            {
                int have = PowerUpCount(claim.PowerUpId);
                Data.Inventory.Powerups[claim.PowerUpId] = (int)Math.Min((long)have + claim.PowerUpCount, int.MaxValue);
            }
            for (int i = 0; i < claim.Chests.Count; i++)
            {
                ChestContents c = claim.Chests[i].Contents;
                foreach (string pid in c.PowerUps) Data.Inventory.Powerups[pid] = SaturatingAdd(PowerUpCount(pid), 1);   // like a bought chest: no stock cap
                Data.Coins = SaturatingAdd(Data.Coins, c.Coins);
            }
            s.MonthlyClaims = monthlyClaims;
            s.ClaimsLeft--;
            s.LastClaimDay = today;
            claim.Result = SubscriptionClaimResult.Ok;
            bool saved = false;
            for (int i = 0; i < claim.Chests.Count; i++)
            {
                CharacterSpec hero = claim.Chests[i].Contents.Hero;
                if (hero != null && CharacterInventory.Exists(hero.Id)) { AddCopies(hero.Id, 1); saved = true; }   // its first save also holds everything above
            }
            if (!saved) Commit();
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
