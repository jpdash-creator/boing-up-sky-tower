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
                    pay.RecoveredPurchase += OnRecovered;
                    pay.ActiveSubscriptionFound += OnActiveSubscription;
                }
                pay.Initialize();
            }
            catch (Exception e) { Debug.LogWarning("[Store] start failed: " + e.Message); }
        }

        /// <summary>Tests: forget the hooks (the services are reset too).</summary>
        public static void ResetForTests() { hooked = false; }

        static void OnRecovered(PurchaseResult r)
        {
            ProgressService p = GameSession.Progress;
            if (p == null) { r.Reject = true; return; }   // not ready: leave the order unconfirmed, the store delivers it again next launch
            p.FirstPurchaseDoubleActive = GameSettings.FirstPurchaseDouble;
            StorePurchaseOutcome o = p.ApplyStorePurchase(r.ProductId, r.TransactionId, DateTime.UtcNow);
            if (o == StorePurchaseOutcome.Granted) { if (Granted != null) Granted(); }
            else if (o != StorePurchaseOutcome.Duplicate) r.Reject = true;
        }

        static void OnActiveSubscription(PurchaseResult r)
        {
            ProgressService p = GameSession.Progress;
            if (p == null) return;
            if (p.ApplyActiveSubscription(r.ProductId, r.TransactionId, DateTime.UtcNow) == StorePurchaseOutcome.Granted && Granted != null) Granted();
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
