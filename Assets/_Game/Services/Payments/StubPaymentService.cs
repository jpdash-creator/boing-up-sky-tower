using System;

namespace FluffyClimbers.Monetization
{
    /// <summary>Dev/editor payments: every known product succeeds at once (no money moves). Used when Unity IAP is not compiled in.</summary>
    public sealed class StubPaymentService : IPaymentService
    {
        public bool IsInitialized { get; private set; }
        public event Action<PurchaseResult> RecoveredPurchase;
        public event Action<PurchaseResult> ActiveSubscriptionFound;
        public bool Fail;
        /// <summary>Tests: what the store account owns. Restore reports every subscription here as active (ActiveSubscriptionFound) and any other entry as an unconfirmed order (RecoveredPurchase).</summary>
        public readonly System.Collections.Generic.List<PurchaseResult> Owned = new System.Collections.Generic.List<PurchaseResult>();
        /// <summary>Tests: Restore reports the store as unreachable.</summary>
        public bool RestoreFails;
        public int RestoreCount { get; private set; }
        public int PurchaseCount { get; private set; }

        public void Initialize() { IsInitialized = true; }

        public string GetLocalizedPrice(string productId) { return null; }

        public void Purchase(string productId, Action<PurchaseResult> done)
        {
            var r = new PurchaseResult { ProductId = productId };
            ProductDef p = ProductCatalog.Get(productId);
            if (p == null || !IsInitialized) { r.Status = PurchaseStatus.Unavailable; r.Error = p == null ? "unknown product" : "not initialized"; }
            else if (Fail) { r.Status = PurchaseStatus.Failed; r.Error = "stub failure"; }
            else { r.Status = PurchaseStatus.Success; r.TransactionId = "stub-" + (++PurchaseCount); }
            if (done != null) done(r);
        }

        public void Restore(Action<bool, string> done)
        {
            RestoreCount++;
            if (RestoreFails || !IsInitialized) { if (done != null) done(false, "store not reachable"); return; }
            foreach (PurchaseResult o in Owned)
            {
                ProductDef def = ProductCatalog.Get(o.ProductId);
                var copy = new PurchaseResult { Status = PurchaseStatus.Success, ProductId = o.ProductId, TransactionId = o.TransactionId };
                if (def != null && def.Kind == ProductKind.Subscription) { if (ActiveSubscriptionFound != null) ActiveSubscriptionFound(copy); }
                else if (RecoveredPurchase != null) RecoveredPurchase(copy);
            }
            if (done != null) done(true, "stub");
        }
    }
}
