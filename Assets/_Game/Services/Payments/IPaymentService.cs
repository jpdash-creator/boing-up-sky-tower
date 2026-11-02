using System;

namespace BoingUpSkyTower.Monetization
{
    public enum PurchaseStatus
    {
        Success,
        Cancelled,
        Failed,
        /// <summary>Waiting for approval (Ask to Buy / Google Play pending). Grant nothing yet.</summary>
        Pending,
        /// <summary>Store not connected or product unknown.</summary>
        Unavailable
    }

    public sealed class PurchaseResult
    {
        public PurchaseStatus Status;
        public string ProductId;
        public string TransactionId;
        /// <summary>Store receipt/token for server-side validation (TODO, see docs/33).</summary>
        public string Receipt;
        public string Error;
        public bool Success { get { return Status == PurchaseStatus.Success; } }
    }

    public interface IPaymentService
    {
        bool IsInitialized { get; }
        /// <summary>Connects to the store and fetches the products of ProductCatalog. Idempotent, non-blocking.</summary>
        void Initialize();
        /// <summary>Store-localised price text ("$0.79" or "Rs 75"), or null when unknown (use the catalog USD price then).</summary>
        string GetLocalizedPrice(string productId);
        /// <summary>
        /// Starts a purchase; done runs exactly once on the main thread. The store transaction is confirmed (consumed) only after
        /// done returns, so grant the content and save inside the callback.
        /// </summary>
        void Purchase(string productId, Action<PurchaseResult> done);
        /// <summary>Re-delivers non-consumables and subscriptions (required on iOS). done(success, message).</summary>
        void Restore(Action<bool, string> done);
    }
}
