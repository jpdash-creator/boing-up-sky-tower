using System;
using FluffyClimbers.Content;
using FluffyClimbers.Monetization;

namespace FluffyClimbers.Economy
{
    /// <summary>How a real-money store purchase ended in the save.</summary>
    public enum StorePurchaseOutcome
    {
        /// <summary>The content was added and saved (one save holds grant and ledger record).</summary>
        Granted,
        /// <summary>This (product, transaction) pair is already in the ledger: nothing is added a second time.</summary>
        Duplicate,
        /// <summary>The product id is not a live ProductCatalog product, or its pack does not exist.</summary>
        UnknownProduct,
        /// <summary>Not granted (for example another subscription plan is still running). The caller must not confirm the store transaction.</summary>
        NotGranted
    }

    /// <summary>
    /// Real purchases from the store (Unity IAP, docs/33). The ledger key of a purchase is iap:&lt;productId&gt;:&lt;transactionId&gt;: the pair is kept in the save's purchase
    /// records, so a purchase that the store delivers twice (retry, restore, recovery after a crash) is never granted twice. The records also travel with the cloud save.
    /// </summary>
    public sealed partial class ProgressService
    {
        /// <summary>The ledger key text of a purchase (documentation and logs; the save stores the two parts in a PurchaseRecord).</summary>
        public static string LedgerKey(string productId, string transactionId) { return "iap:" + productId + ":" + transactionId; }

        /// <summary>True when this purchase is already in the ledger.</summary>
        public bool HasPurchaseRecord(string productId, string transactionId)
        {
            if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(transactionId)) return false;
            foreach (PurchaseRecord p in Purchases)
                if (p.ProductId == productId && p.TransactionId == transactionId) return true;
            return false;
        }

        GemPackDef FindGemPack(int gems)
        {
            foreach (GemPackDef g in ShopCatalog.FromRepository(repo).GemPacks) if (g.Gems == gems) return g;
            return null;
        }

        /// <summary>
        /// Grants a paid gem pack or subscription for a ProductCatalog id, once per (product, transaction). Gem packs honour the first-purchase double (switch on) exactly like the
        /// test path; a subscription pays its instant gems and starts or extends the period. Saves once.
        /// </summary>
        public StorePurchaseOutcome ApplyStorePurchase(string productId, string transactionId, DateTime nowUtc)
        {
            ProductDef def = ProductCatalog.Get(productId);
            if (def == null || !def.Live) return StorePurchaseOutcome.UnknownProduct;
            if (HasPurchaseRecord(productId, transactionId)) return StorePurchaseOutcome.Duplicate;
            string tx = string.IsNullOrEmpty(transactionId) ? "" : transactionId;
            if (def.Kind == ProductKind.Consumable)
            {
                GemPackDef pack = def.Coins == 0 ? FindGemPack(def.Gems) : null;
                if (pack == null) return StorePurchaseOutcome.UnknownProduct;
                ShopResult r = GrantWithRecord(productId, PurchaseRecord.KindConsumable, tx, nowUtc, 0, () => CreditGemPack(pack), x => x == ShopResult.Ok);
                return r == ShopResult.Ok ? StorePurchaseOutcome.Granted : StorePurchaseOutcome.NotGranted;
            }
            if (def.Kind == ProductKind.Subscription)
            {
                string plan = PlanOfProduct(productId);
                if (plan == null) return StorePurchaseOutcome.UnknownProduct;
                SubscriptionBuyResult r = GrantWithRecord(productId, PurchaseRecord.KindSubscription, tx, nowUtc, def.PeriodDays, () => CreditSubscription(plan, nowUtc), x => x == SubscriptionBuyResult.Ok);
                return r == SubscriptionBuyResult.Ok ? StorePurchaseOutcome.Granted : StorePurchaseOutcome.NotGranted;
            }
            return StorePurchaseOutcome.UnknownProduct;
        }

        /// <summary>
        /// The store says this subscription is active now (start-up or Restore). When the game's own time has run out (a renewal happened while the app was closed) a new period
        /// starts WITHOUT the instant gems. Nothing happens while the local period still runs, so one renewal is paid once. One record per product and local day.
        /// </summary>
        public StorePurchaseOutcome ApplyActiveSubscription(string productId, string transactionId, DateTime nowUtc)
        {
            ProductDef def = ProductCatalog.Get(productId);
            string plan = PlanOfProduct(productId);
            if (def == null || def.Kind != ProductKind.Subscription || plan == null) return StorePurchaseOutcome.UnknownProduct;
            if (IsActive(nowUtc)) return StorePurchaseOutcome.Duplicate;
            string tx = (transactionId ?? "") + "|renew|" + LocalDayKey(nowUtc);
            if (HasPurchaseRecord(productId, tx)) return StorePurchaseOutcome.Duplicate;
            SubscriptionBuyResult r = GrantWithRecord(productId, PurchaseRecord.KindSubscription, tx, nowUtc, def.PeriodDays, () => CreditSubscription(plan, nowUtc, false), x => x == SubscriptionBuyResult.Ok);
            return r == SubscriptionBuyResult.Ok ? StorePurchaseOutcome.Granted : StorePurchaseOutcome.NotGranted;
        }
    }
}
