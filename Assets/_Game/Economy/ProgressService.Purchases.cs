using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace FluffyClimbers.Economy
{
    /// <summary>One real-money purchase kept in the save (docs/38). Survives Reset Progress; restore and cloud sync read it.</summary>
    public sealed class PurchaseRecord
    {
        public const string KindConsumable = "consumable";
        public const string KindNonConsumable = "nonconsumable";
        public const string KindSubscription = "subscription";

        /// <summary>Store product id (ProductCatalog.Id), for example com.jdlabs.fluffyclimbers.sub.monthly.</summary>
        [JsonProperty("productId")] public string ProductId = "";
        [JsonProperty("kind")] public string Kind = KindConsumable;
        /// <summary>Store transaction id (stub purchases: "stub-N" or "test-..."). Empty = unknown.</summary>
        [JsonProperty("transactionId")] public string TransactionId = "";
        [JsonProperty("utcTicks")] public long UtcTicks;
        /// <summary>Subscriptions: days the purchase paid for (7 or 30). 0 otherwise.</summary>
        [JsonProperty("days")] public int Days;
    }

    /// <summary>Purchase history, restore and the admin helpers. Every public change saves once.</summary>
    public sealed partial class ProgressService
    {
        /// <summary>Same prefix as ProductCatalog.Prefix (Economy cannot see the Services assembly; a unit test keeps them equal).</summary>
        public const string ProductIdPrefix = "com.jdlabs.fluffyclimbers.";

        public IReadOnlyList<PurchaseRecord> Purchases
        {
            get
            {
                if (Data.Purchases == null) Data.Purchases = new List<PurchaseRecord>();
                return Data.Purchases;
            }
        }

        /// <summary>
        /// Remembers a paid purchase. The same (product, transaction id) pair is stored once, so a re-delivered receipt does not duplicate.
        /// Returns true when a new record was added.
        /// </summary>
        public bool RecordPurchase(string productId, string kind, string transactionId, DateTime nowUtc, int days = 0)
        {
            if (AddPurchaseRecord(productId, kind, transactionId, nowUtc, days) == null) return false;
            Commit();
            return true;
        }

        /// <summary>Adds the record to the save data WITHOUT saving (the caller's own Commit saves it together with the grant, so a purchase is one save). Returns the record, or null when invalid or already stored.</summary>
        PurchaseRecord AddPurchaseRecord(string productId, string kind, string transactionId, DateTime nowUtc, int days = 0)
        {
            if (string.IsNullOrEmpty(productId)) return null;
            if (Data.Purchases == null) Data.Purchases = new List<PurchaseRecord>();
            if (!string.IsNullOrEmpty(transactionId))
                foreach (PurchaseRecord p in Data.Purchases)
                    if (p.ProductId == productId && p.TransactionId == transactionId) return null;
            var rec = new PurchaseRecord { ProductId = productId, Kind = kind ?? PurchaseRecord.KindConsumable, TransactionId = transactionId ?? "", UtcTicks = nowUtc.Ticks, Days = days };
            Data.Purchases.Add(rec);
            return rec;
        }

        /// <summary>Runs a grant that saves once, with the purchase record added first so that single save holds both; the record is dropped again if the grant fails.</summary>
        T GrantWithRecord<T>(string productId, string kind, string tx, DateTime nowUtc, int days, Func<T> grant, Func<T, bool> ok)
        {
            PurchaseRecord rec = AddPurchaseRecord(productId, kind, tx, nowUtc, days);
            T res = grant();
            if (rec != null && !ok(res)) Data.Purchases.Remove(rec);
            return res;
        }

        /// <summary>The plan id ("weekly", "weekly_plus", "monthly", "monthly_premium") for a subscription product id ("...sub.weekly_plus"), else null.</summary>
        public static string PlanOfProduct(string productId)
        {
            if (productId == null) return null;
            const string marker = ".sub.";
            int at = productId.LastIndexOf(marker, StringComparison.Ordinal);
            if (at < 0) return null;
            string plan = productId.Substring(at + marker.Length);
            return plan == SubscriptionCatalog.Weekly || plan == SubscriptionCatalog.WeeklyPlus || plan == SubscriptionCatalog.Monthly || plan == SubscriptionCatalog.MonthlyPremium ? plan : null;
        }

        /// <summary>
        /// Re-grants what the recorded purchases still entitle the player to: a subscription whose paid days (stacked in purchase order) have not
        /// run out yet and which is not already running that long. Consumables (gems) are never granted again. Gems are not paid a second time.
        /// Returns the number of entitlements restored (0 = nothing to restore).
        /// </summary>
        public int RestoreFromRecords(DateTime nowUtc)
        {
            int restored = 0;
            var subs = new List<PurchaseRecord>();
            foreach (PurchaseRecord p in Purchases)
                if (p.Kind == PurchaseRecord.KindSubscription && PlanOfProduct(p.ProductId) != null && p.Days > 0) subs.Add(p);
            if (subs.Count > 0)
            {
                subs.Sort((a, b) => a.UtcTicks.CompareTo(b.UtcTicks));
                long expiry = 0;
                string plan = null;
                foreach (PurchaseRecord p in subs)
                {
                    long start = p.UtcTicks >= expiry ? p.UtcTicks : expiry;   // a purchase while the time still runs stacks on its end
                    long add = TimeSpan.TicksPerDay * (long)p.Days;
                    expiry = start > DateTime.MaxValue.Ticks - add ? DateTime.MaxValue.Ticks : start + add;
                    plan = PlanOfProduct(p.ProductId);
                }
                SubscriptionData s = Sub;
                if (expiry > nowUtc.Ticks && (!IsActive(nowUtc) || s.ExpiryUtcTicks < expiry))
                {
                    int daysLeft = (int)Math.Ceiling(new TimeSpan(expiry - nowUtc.Ticks).TotalDays - 1e-9);
                    s.Type = plan;
                    s.ExpiryUtcTicks = expiry;
                    s.ClaimsLeft = Math.Max(s.ClaimsLeft, daysLeft);
                    restored++;
                }
            }
            // Non-consumables: none are sold today. When one is added, grant it here (by product id) and count it.
            if (restored > 0) Commit();
            return restored;
        }

        // ---- admin helpers (Settings > Admin, docs/38) ----

        /// <summary>Admin: unlocks every playable region in the save (the real unlock list, not the test flag).</summary>
        public int AdminUnlockAllRegions()
        {
            int added = 0;
            foreach (FluffyClimbers.Content.RegionDef r in repo.Regions)
            {
                if (!IsRegionPlayable(r) || r.Index == 1 || Data.UnlockedRegions.Contains(r.Id)) continue;
                Data.UnlockedRegions.Add(r.Id);
                added++;
            }
            if (added > 0) Commit();
            return added;
        }

        /// <summary>Admin: clears every level of the playable regions with 3 stars (in order, so each one is unlocked when it is played). Returns how many levels were cleared.</summary>
        public int AdminCompleteAllLevels()
        {
            int n = 0;
            foreach (FluffyClimbers.Content.RegionDef r in repo.Regions)
            {
                if (!IsRegionPlayable(r)) continue;
                if (r.Index != 1 && !Data.UnlockedRegions.Contains(r.Id)) Data.UnlockedRegions.Add(r.Id);
                foreach (FluffyClimbers.Content.LevelDef l in repo.LevelsOf(r.Id))
                {
                    ApplyRun(l.Id, new RunOutcome { Completed = true, StarMask = 7, Score = 1000, CoinsCollected = 0 });
                    n++;
                }
            }
            return n;
        }
    }
}
