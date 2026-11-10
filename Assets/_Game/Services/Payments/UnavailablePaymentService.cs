using System;
#pragma warning disable 0067

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// Release build without a working store (the Unity IAP package or define is missing, or it failed to start): every purchase is refused, nothing is ever granted.
    /// The stub that grants at once exists only in the editor and development builds (see Services.CreatePayments).
    /// </summary>
    public sealed class UnavailablePaymentService : IPaymentService
    {
        public bool IsInitialized { get { return false; } }
        public event Action<PurchaseResult> RecoveredPurchase;
        public event Action<PurchaseResult> ActiveSubscriptionFound;
        public void Initialize() { }
        public string GetLocalizedPrice(string productId) { return null; }
        public void Purchase(string productId, Action<PurchaseResult> done)
        {
            if (done != null) done(new PurchaseResult { Status = PurchaseStatus.Unavailable, ProductId = productId, Error = "store not available in this build" });
        }
        public void Restore(Action<bool, string> done) { if (done != null) done(false, "store not available in this build"); }
    }
}
