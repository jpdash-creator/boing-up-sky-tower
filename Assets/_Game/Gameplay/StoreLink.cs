using System;
using FluffyClimbers.Economy;
using FluffyClimbers.Monetization;
using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// Glue between the store (Services.Payments, docs/33) and the save (ProgressService). <see cref="Start"/> runs once at boot: it connects the store and listens for purchases
    /// that finished outside a Buy tap (the app was killed mid-purchase, a pending purchase was approved later) and for subscriptions the store reports as active. Every grant is
    /// idempotent through the purchase ledger (iap:productId:transactionId), so a purchase delivered twice is added once. Never blocks and never throws.
    /// </summary>
    public static class StoreLink
    {
        static bool hooked;
        static IPaymentService hookedService;

        /// <summary>Raised after a recovered purchase or a renewed subscription was added, so an open screen can redraw its wallet.</summary>
        public static event Action Granted;

        public static void Start()
        {
            try
            {
                IPaymentService pay = Services.Payments;
                if (!hooked)
                {
                    hooked = true;
                    hookedService = pay;
                    pay.RecoveredPurchase += OnRecovered;
                    pay.ActiveSubscriptionFound += OnActiveSubscription;
                }
                pay.Initialize();
            }
            catch (Exception e) { Debug.LogWarning("[Store] start failed: " + e.Message); }
        }

        /// <summary>Tests: forget the hooks (the services are reset too).</summary>
        public static void ResetForTests() { hooked = false; hookedService = null; restoring = false; restoreProgress = null; restoreGranted = restoreFound = 0; }

        // ---------------- restore (Settings > Restore, owner 2026-11-10: store purchases only) ----------------

        public enum RestoreStatus { Restored, AlreadyRestored, NothingToRestore, StoreUnavailable }

        public sealed class RestoreResult
        {
            public RestoreStatus Status;
            /// <summary>Entitlements added by this restore (VIP subscription days, recovered unconfirmed orders). Gem and coin packs are consumables and are never re-granted.</summary>
            public int Count;
            public string Message;
        }

        static bool restoring;
        static int restoreGranted, restoreFound;
        static ProgressService restoreProgress;

        /// <summary>Pure: the player-facing title and text of a restore result.</summary>
        public static void RestoreTexts(RestoreResult r, out string title, out string text)
        {
            switch (r.Status)
            {
                case RestoreStatus.Restored: title = "Purchases restored"; text = "Purchases restored: " + r.Count; break;
                case RestoreStatus.AlreadyRestored: title = "Already restored"; text = "Your purchases are already restored."; break;
                case RestoreStatus.StoreUnavailable: title = "Store not reachable"; text = "Could not reach the store. Check your connection and try again."; break;
                default: title = "Nothing to restore"; text = "Nothing to restore"; break;
            }
        }

        /// <summary>
        /// Restore purchases: asks the store (Google Play) what the account owns; active VIP subscriptions (and any unconfirmed order) are re-applied through the same idempotent ledger
        /// as a normal purchase, then the locally recorded subscription days are re-applied. Restoring twice never grants twice. done runs once on the main thread, never throws.
        /// The payment service and progress are passed in so tests can use the stub.
        /// </summary>
        public static void RestoreFromStore(IPaymentService pay, ProgressService progress, Action<RestoreResult> done)
        {
            if (pay == null || progress == null || restoring)
            {
                if (done != null) done(new RestoreResult { Status = RestoreStatus.StoreUnavailable, Message = restoring ? "restore already running" : "no store" });
                return;
            }
            restoring = true; restoreGranted = 0; restoreFound = 0; restoreProgress = progress;
            bool tempHook = hookedService != pay;   // the global hooks listen to Services.Payments only; any other service is listened to for this restore
            if (tempHook) { pay.RecoveredPurchase += OnRecovered; pay.ActiveSubscriptionFound += OnActiveSubscription; }
            Action<RestoreResult> finish = r =>
            {
                restoring = false; restoreProgress = null;
                if (tempHook) { pay.RecoveredPurchase -= OnRecovered; pay.ActiveSubscriptionFound -= OnActiveSubscription; }
                if (done != null) done(r);
            };
            try
            {
                pay.Initialize();
                pay.Restore((ok, msg) =>
                {
                    try
                    {
                        int n = restoreGranted + progress.RestoreFromRecords(DateTime.UtcNow);
                        RestoreStatus st = n > 0 ? RestoreStatus.Restored : restoreFound > 0 ? RestoreStatus.AlreadyRestored : ok ? RestoreStatus.NothingToRestore : RestoreStatus.StoreUnavailable;
                        if (n > 0 && Granted != null) Granted();
                        finish(new RestoreResult { Status = st, Count = n, Message = msg });
                    }
                    catch (Exception e) { Debug.LogWarning("[Store] restore failed: " + e.Message); finish(new RestoreResult { Status = RestoreStatus.StoreUnavailable, Message = e.Message }); }
                });
            }
            catch (Exception e) { Debug.LogWarning("[Store] restore failed: " + e.Message); finish(new RestoreResult { Status = RestoreStatus.StoreUnavailable, Message = e.Message }); }
        }

        static void OnRecovered(PurchaseResult r)
        {
            ProgressService p = restoring && restoreProgress != null ? restoreProgress : GameSession.Progress;
            if (p == null) { r.Reject = true; return; }   // not ready: leave the order unconfirmed, the store delivers it again next launch
            p.FirstPurchaseDoubleActive = GameSettings.FirstPurchaseDouble;
            StorePurchaseOutcome o = p.ApplyStorePurchase(r.ProductId, r.TransactionId, DateTime.UtcNow);
            if (o == StorePurchaseOutcome.Granted) { if (restoring) restoreGranted++; if (Granted != null) Granted(); }
            else if (o != StorePurchaseOutcome.Duplicate) r.Reject = true;
        }

        static void OnActiveSubscription(PurchaseResult r)
        {
            ProgressService p = restoring && restoreProgress != null ? restoreProgress : GameSession.Progress;
            if (p == null) return;
            if (restoring) restoreFound++;
            if (p.ApplyActiveSubscription(r.ProductId, r.TransactionId, DateTime.UtcNow) == StorePurchaseOutcome.Granted)
            {
                if (restoring) restoreGranted++;
                if (Granted != null) Granted();
            }
        }

        /// <summary>The store's own price text for a product ("Rs 75"), or null (no store, not loaded, or the test purchases are on): callers then show the catalog USD price.</summary>
        public static string StorePrice(string productId)
        {
            if (ShopScreen.TestPurchasesEnabled || string.IsNullOrEmpty(productId)) return null;
            try { return Services.Payments.GetLocalizedPrice(productId); }
            catch (Exception) { return null; }
        }
    }
}
