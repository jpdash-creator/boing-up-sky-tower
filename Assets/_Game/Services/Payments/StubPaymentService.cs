using System;

namespace BoingUpSkyTower.Monetization
{
    /// <summary>Dev/editor payments: every known product succeeds at once (no money moves). Used when Unity IAP is not compiled in.</summary>
    public sealed class StubPaymentService : IPaymentService
    {
        public bool IsInitialized { get; private set; }
        public bool Fail;
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

        public void Restore(Action<bool, string> done) { if (done != null) done(true, "stub"); }
    }
}
