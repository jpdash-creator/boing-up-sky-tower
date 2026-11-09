#if FLUFFY_UNITY_IAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// Real purchases through Unity IAP v5 (com.unity.purchasing 5.x, StoreController API). Compiled only with the define FLUFFY_UNITY_IAP.
    /// Flow: Connect, FetchProducts(ProductCatalog Live products), PurchaseProduct, OnPurchasePending, the game grants inside the
    /// Purchase callback, then ConfirmPurchase. A purchase found at start-up with no waiting callback (app killed mid-purchase) is reported
    /// through RecoveredPurchase; with no subscriber it is left unconfirmed, so the store re-delivers it next launch (nothing is lost).
    /// TODO(release blocker): receipt validation (Google Play CrossPlatformValidator with GooglePlayTangle, or a server) before granting;
    /// result.Receipt carries the receipt for it. See docs/33 section 7.
    /// </summary>
    public sealed class UnityIapPaymentService : IPaymentService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { ServiceFactories.UnityIap = () => new UnityIapPaymentService(); }

        StoreController store;
        bool started, productsReady;
        readonly Dictionary<string, Action<PurchaseResult>> waiting = new Dictionary<string, Action<PurchaseResult>>();

        public bool IsInitialized { get { return productsReady; } }

        /// <summary>Raised for a purchase that completed outside a Purchase call (recovered at start-up). Grant, save, then it is confirmed.</summary>
        public event Action<PurchaseResult> RecoveredPurchase;

        public async void Initialize()
        {
            if (started) return;
            started = true;
            try
            {
                store = UnityIAPServices.StoreController();
                store.OnStoreConnected += OnStoreConnected;
                store.OnStoreDisconnected += d => Debug.LogWarning("[IAP] store disconnected: " + d.Message);
                store.OnProductsFetched += p => { productsReady = true; store.FetchPurchases(); };
                store.OnProductsFetchFailed += f => Debug.LogWarning("[IAP] product fetch failed: " + f.FailureReason);
                store.OnPurchasePending += OnPending;
                store.OnPurchaseConfirmed += o => { };
                store.OnPurchaseFailed += OnFailed;
                store.OnPurchaseDeferred += OnDeferred;
                store.OnPurchasesFetched += o => { };
                store.OnPurchasesFetchFailed += f => Debug.LogWarning("[IAP] purchases fetch failed: " + f.Message);
                await store.Connect();
            }
            catch (Exception e)
            {
                started = false;
                Debug.LogWarning("[IAP] init failed: " + e.Message);
            }
        }

        void OnStoreConnected()
        {
            var defs = new List<ProductDefinition>();
            for (int i = 0; i < ProductCatalog.All.Count; i++)
            {
                ProductDef p = ProductCatalog.All[i];
                if (!p.Live) continue;
                defs.Add(new ProductDefinition(p.Id, Map(p.Kind)));
            }
            store.FetchProducts(defs);
        }

        static ProductType Map(ProductKind k)
        {
            return k == ProductKind.Subscription ? ProductType.Subscription : k == ProductKind.NonConsumable ? ProductType.NonConsumable : ProductType.Consumable;
        }

        static string IdOf(Order order)
        {
            if (order == null || order.CartOrdered == null) return null;
            var items = order.CartOrdered.Items();
            return items.Count > 0 && items[0].Product != null && items[0].Product.definition != null ? items[0].Product.definition.id : null;
        }

        public string GetLocalizedPrice(string productId)
        {
            if (store == null || !productsReady) return null;
            Product p = store.GetProductById(productId);
            return p != null && p.metadata != null ? p.metadata.localizedPriceString : null;
        }

        public void Purchase(string productId, Action<PurchaseResult> done)
        {
            Product p = store != null && productsReady ? store.GetProductById(productId) : null;
            if (p == null || waiting.ContainsKey(productId))
            {
                if (done != null) done(new PurchaseResult { Status = PurchaseStatus.Unavailable, ProductId = productId, Error = p == null ? "product unavailable" : "purchase in progress" });
                return;
            }
            waiting[productId] = done;
            store.PurchaseProduct(p);
        }

        void OnPending(PendingOrder order)
        {
            string id = IdOf(order);
            var r = new PurchaseResult
            {
                Status = PurchaseStatus.Success, ProductId = id,
                TransactionId = order.Info != null ? order.Info.TransactionID : null,
                Receipt = order.Info != null ? order.Info.Receipt : null
            };
            Action<PurchaseResult> cb;
            if (id != null && waiting.TryGetValue(id, out cb))
            {
                waiting.Remove(id);
                if (cb != null) cb(r);
                store.ConfirmPurchase(order);
            }
            else if (RecoveredPurchase != null)
            {
                RecoveredPurchase(r);
                store.ConfirmPurchase(order);
            }
            // else: leave unconfirmed; the store re-delivers it on the next launch.
        }

        void OnFailed(FailedOrder failed)
        {
            string id = IdOf(failed);
            Action<PurchaseResult> cb;
            if (id == null || !waiting.TryGetValue(id, out cb)) return;
            waiting.Remove(id);
            var r = new PurchaseResult
            {
                ProductId = id, Error = failed.FailureReason + ": " + failed.Details,
                Status = failed.FailureReason == PurchaseFailureReason.UserCancelled ? PurchaseStatus.Cancelled : PurchaseStatus.Failed
            };
            if (cb != null) cb(r);
        }

        void OnDeferred(DeferredOrder deferred)
        {
            string id = IdOf(deferred);
            Action<PurchaseResult> cb;
            if (id == null || !waiting.TryGetValue(id, out cb)) return;
            waiting.Remove(id);
            if (cb != null) cb(new PurchaseResult { Status = PurchaseStatus.Pending, ProductId = id });
        }

        public void Restore(Action<bool, string> done)
        {
            if (store == null || !productsReady) { if (done != null) done(false, "store not ready"); return; }
            store.RestoreTransactions((ok, msg) => { if (done != null) done(ok, msg); });
        }
    }
}
#endif
