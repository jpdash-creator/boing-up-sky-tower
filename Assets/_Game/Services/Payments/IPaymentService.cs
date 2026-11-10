using System;

namespace FluffyClimbers.Monetization
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
        /// <summary>
        /// Set by the Purchase callback (or a RecoveredPurchase handler) when the content could NOT be granted (for example another subscription plan is running):
        /// the transaction is then left unconfirmed, so Google Play refunds it automatically after about 3 days and nothing is lost for the player.
        /// </summary>
        public bool Reject;
        public bool Success { get { return Status == PurchaseStatus.Success; } }
    }

    public interface IPaymentService
    {
        bool IsInitialized { get; }
        /// <summary>Connects to the store and fetches the products of ProductCatalog. Idempotent, non-blocking.</summary>
        void Initialize();
        /// <summary>
        /// Raised for a purchase that completed outside a Purchase call (the app was killed mid-purchase, or a pending purchase was found at start-up). The handler grants,
        /// saves and must be idempotent (ledger key iap:productId:transactionId); the transaction is confirmed after the handler returns, unless it sets Reject. With no handler it stays unconfirmed and is delivered again next launch.
        /// </summary>
        event Action<PurchaseResult> RecoveredPurchase;
        /// <summary>Raised at start-up and after Restore for every subscription the store reports as currently active (a renewal the game could not see). No transaction is confirmed for it.</summary>
        event Action<PurchaseResult> ActiveSubscriptionFound;
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
