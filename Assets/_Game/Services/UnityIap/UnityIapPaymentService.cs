#if FLUFFY_UNITY_IAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// Real purchases through Unity IAP v5 (com.unity.purchasing 5.4.4, StoreController API; every call below was checked against the package source in Library/PackageCache).
    /// Compiled only with the define FLUFFY_UNITY_IAP.
    /// Flow: Connect, FetchProducts (ProductCatalog Live products), FetchPurchases (pending orders from an earlier run arrive as OnPurchasePending), PurchaseProduct,
    /// OnPurchasePending, VALIDATE, the game grants and saves inside the Purchase callback, then ConfirmPurchase. A pending order with no waiting callback (app killed
    /// mid-purchase) goes to RecoveredPurchase; with no subscriber it stays unconfirmed and the store delivers it again next launch. Google refunds an unconfirmed
    /// purchase after about 3 days, so a failure here never costs the player money for nothing.
    /// Transaction id: the Google orderId (stable for one purchase), else the purchase token. The game's ledger key is iap:productId:transactionId (ProgressService.ApplyStorePurchase).
    /// Receipt validation (local): UnityEngine.Purchasing.Security.CrossPlatformValidator with the Google Play licence key from Resources/Services/iap_config.json
    /// (IapConfig), checking the RSA signature, the package name and the product id. No key: development builds grant with a warning; RELEASE builds refuse the
    /// purchase (Failed, logged) and the release preflight fails, so an unvalidated purchase is never granted silently. Local validation only stops casual tampering;
    /// validating on a server with the Google Play Developer API is the stronger option (docs/33 section 7).
    /// </summary>
    public sealed class UnityIapPaymentService : IPaymentService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { ServiceFactories.UnityIap = () => new UnityIapPaymentService(); }

        StoreController store;
        bool started, productsReady;
        readonly Dictionary<string, Action<PurchaseResult>> waiting = new Dictionary<string, Action<PurchaseResult>>();
        CrossPlatformValidator validator;
        bool validatorTried;

        public bool IsInitialized { get { return productsReady; } }

        public event Action<PurchaseResult> RecoveredPurchase;
        public event Action<PurchaseResult> ActiveSubscriptionFound;

        public async void Initialize()
        {
            if (started) return;
            started = true;
            try
            {
                store = UnityIAPServices.StoreController();
                store.OnStoreConnected += OnStoreConnected;
                store.OnStoreDisconnected += d => Debug.LogWarning("[IAP] store disconnected: " + (d != null ? d.message : ""));
                store.OnProductsFetched += p => { productsReady = true; store.FetchPurchases(); };
                store.OnProductsFetchFailed += f => Debug.LogWarning("[IAP] product fetch failed: " + (f != null ? f.FailureReason : ""));
                store.OnPurchasePending += OnPending;
                store.OnPurchaseConfirmed += o => { };
                store.OnPurchaseFailed += OnFailed;
                store.OnPurchaseDeferred += OnDeferred;
                store.OnPurchasesFetched += OnFetched;
                store.OnPurchasesFetchFailed += f =>
                {
                    Debug.LogWarning("[IAP] purchases fetch failed: " + (f != null ? f.message : ""));
                    FinishRestore(false, f != null ? f.message : "purchases fetch failed");
                };
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
            return items != null && items.Count > 0 && items[0].Product != null && items[0].Product.definition != null ? items[0].Product.definition.id : null;
        }

        static string TransactionOf(Order order)
        {
            if (order == null || order.Info == null) return null;
            if (order.Info.Google != null && !string.IsNullOrEmpty(order.Info.Google.OrderId)) return order.Info.Google.OrderId;
            return order.Info.TransactionID;
        }

        public string GetLocalizedPrice(string productId)
        {
            if (store == null || !productsReady) return null;
            Product p = store.GetProductById(productId);
            return p != null && p.metadata != null && !string.IsNullOrEmpty(p.metadata.localizedPriceString) ? p.metadata.localizedPriceString : null;
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
            try { store.PurchaseProduct(p); }
            catch (Exception e)
            {
                waiting.Remove(productId);
                if (done != null) done(new PurchaseResult { Status = PurchaseStatus.Failed, ProductId = productId, Error = e.Message });
            }
        }

        // ---------------- validation ----------------

        /// <summary>True when the receipt is genuine for this product (or validation is legitimately skipped in a development build). error says why not.</summary>
        bool Validate(Order order, string productId, out string error)
        {
            error = null;
            byte[] key = IapConfig.LicenseKeyBytes;
            if (key == null)
            {
                if (Debug.isDebugBuild) { Debug.LogWarning("[IAP] no Play licence key in iap_config.json: the receipt of " + productId + " is NOT validated (development build)."); return true; }
                error = "no Play licence key configured: purchase refused";
                Debug.LogError("[IAP] " + error + " (" + productId + ")");
                return false;
            }
            if (Application.platform != RuntimePlatform.Android)
            {
                if (Debug.isDebugBuild) return true;   // the validator exists on Android only
                error = "receipt validation is not available on this platform";
                return false;
            }
            try
            {
                if (!validatorTried)
                {
                    validatorTried = true;
                    validator = new CrossPlatformValidator(key, Application.identifier);
                }
                if (validator == null) { error = "receipt validator not available"; return false; }
                IPurchaseReceipt[] receipts = validator.Validate(order.Info.Receipt);
                foreach (IPurchaseReceipt r in receipts)
                {
                    var g = r as GooglePlayReceipt;
                    if (g != null && g.productID == productId && g.purchaseState == GooglePurchaseState.Purchased) return true;
                }
                error = "receipt does not match the product";
                return false;
            }
            catch (Exception e)
            {
                error = "receipt invalid (" + e.GetType().Name + ")";
                Debug.LogError("[IAP] " + error + " for " + productId + ": " + e.Message);
                return false;
            }
        }

        // ---------------- store events ----------------

        void OnPending(PendingOrder order)
        {
            string id = IdOf(order);
            Action<PurchaseResult> cb = null;
            if (id != null && waiting.TryGetValue(id, out cb)) waiting.Remove(id);

            string error = id == null ? "unknown product" : null;
            if (id != null) Validate(order, id, out error);
            var r = new PurchaseResult
            {
                Status = error == null ? PurchaseStatus.Success : PurchaseStatus.Failed, ProductId = id, TransactionId = TransactionOf(order),
                Receipt = order.Info != null ? order.Info.Receipt : null, Error = error
            };

            if (error != null)
            {
                // never grant: the order stays unconfirmed (Google refunds it after about 3 days)
                if (cb != null) cb(r);
                return;
            }
            if (cb != null)
            {
                cb(r);
                if (!r.Reject) store.ConfirmPurchase(order);
            }
            else if (RecoveredPurchase != null)
            {
                RecoveredPurchase(r);
                if (!r.Reject) store.ConfirmPurchase(order);
            }
            // else: leave unconfirmed; the store re-delivers it on the next launch.
        }

        void OnFetched(Orders orders)
        {
            try { ReportOwned(orders); }
            finally { FinishRestore(true, null); }
        }

        void ReportOwned(Orders orders)
        {
            if (orders == null || ActiveSubscriptionFound == null || orders.ConfirmedOrders == null) return;
            for (int i = 0; i < orders.ConfirmedOrders.Count; i++)
            {
                ConfirmedOrder o = orders.ConfirmedOrders[i];
                string id = IdOf(o);
                ProductDef def = ProductCatalog.Get(id);
                if (def == null || def.Kind != ProductKind.Subscription) continue;
                string error;
                if (!Validate(o, id, out error)) continue;
                ActiveSubscriptionFound(new PurchaseResult { Status = PurchaseStatus.Success, ProductId = id, TransactionId = TransactionOf(o), Receipt = o.Info != null ? o.Info.Receipt : null });
            }
        }

        void OnFailed(FailedOrder failed)
        {
            string id = IdOf(failed);
            Action<PurchaseResult> cb;
            if (id == null || !waiting.TryGetValue(id, out cb)) return;
            waiting.Remove(id);
            PurchaseStatus st = failed.FailureReason == PurchaseFailureReason.UserCancelled ? PurchaseStatus.Cancelled
                : failed.FailureReason == PurchaseFailureReason.ExistingPurchasePending ? PurchaseStatus.Pending : PurchaseStatus.Failed;
            if (cb != null) cb(new PurchaseResult { ProductId = id, Error = failed.FailureReason + ": " + failed.Details, Status = st });
        }

        void OnDeferred(DeferredOrder deferred)
        {
            string id = IdOf(deferred);
            Action<PurchaseResult> cb;
            if (id == null || !waiting.TryGetValue(id, out cb)) return;
            waiting.Remove(id);
            if (cb != null) cb(new PurchaseResult { Status = PurchaseStatus.Pending, ProductId = id });
        }

        Action<bool, string> restoreDone;

        void FinishRestore(bool ok, string msg)
        {
            Action<bool, string> d = restoreDone;
            restoreDone = null;
            if (d != null) d(ok, msg);
        }

        /// <summary>
        /// Restore (Google Play): the plugin's RestoreTransactions does nothing on Android (it only calls back), so this asks the store for the account's owned purchases
        /// with FetchPurchases. Active subscriptions arrive through ActiveSubscriptionFound, unconfirmed orders through RecoveredPurchase; done runs once after the fetch
        /// (or after 15 seconds without an answer). Consumables that were already confirmed are not owned any more and are never returned.
        /// </summary>
        public async void Restore(Action<bool, string> done)
        {
            if (store == null || !productsReady) { if (done != null) done(false, "store not ready"); return; }
            if (restoreDone != null) { if (done != null) done(false, "restore already running"); return; }
            restoreDone = done;
            try
            {
                store.FetchPurchases();
                await System.Threading.Tasks.Task.Delay(15000);
                FinishRestore(false, "store did not answer");   // no-op when the fetch already finished
            }
            catch (Exception e) { FinishRestore(false, e.Message); }
        }
    }
}
#endif
