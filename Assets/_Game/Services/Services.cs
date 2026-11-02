using UnityEngine;

namespace BoingUpSkyTower.Monetization
{
    /// <summary>
    /// Static locator: Services.Ads, Services.Payments. The real SDK services are used when the scripting define
    /// BOING_LEVELPLAY / BOING_UNITY_IAP is on (and the package is installed), else the stubs. Resolved once; no per-frame work.
    /// </summary>
    public static class Services
    {
        static IAdService ads;
        static IPaymentService payments;
        static bool consent;

        /// <summary>
        /// GDPR/UMP consent stub: set from the consent flow BEFORE the first ad request. Default false (non-personalised ads).
        /// TODO(owner item): wire the Google UMP flow (docs/33 section 6).
        /// </summary>
        public static bool ConsentGiven
        {
            get { return consent; }
            set
            {
                consent = value;
                var aware = ads as IConsentAware;
                if (aware != null) aware.SetConsent(value);
            }
        }

        public static IAdService Ads
        {
            get
            {
                if (ads == null)
                {
                    ads = CreateAds();
                    var aware = ads as IConsentAware;
                    if (aware != null) aware.SetConsent(consent);
                }
                return ads;
            }
            set { ads = value; }
        }

        public static IPaymentService Payments
        {
            get { if (payments == null) payments = CreatePayments(); return payments; }
            set { payments = value; }
        }

        /// <summary>Tests: forget both services and the consent flag.</summary>
        public static void Reset() { ads = null; payments = null; consent = false; }

        static IAdService CreateAds()
        {
#if BOING_LEVELPLAY
            if (!Application.isEditor && ServiceFactories.LevelPlay != null)
            {
                try { return ServiceFactories.LevelPlay(); }
                catch (System.Exception e) { Debug.LogWarning("[Services] LevelPlay unavailable, using stub: " + e.Message); }
            }
#endif
            return new StubAdService();
        }

        static IPaymentService CreatePayments()
        {
#if BOING_UNITY_IAP
            if (!Application.isEditor && ServiceFactories.UnityIap != null)
            {
                try { return ServiceFactories.UnityIap(); }
                catch (System.Exception e) { Debug.LogWarning("[Services] Unity IAP unavailable, using stub: " + e.Message); }
            }
#endif
            return new StubPaymentService();
        }
    }

    /// <summary>The SDK assemblies register their factory here at startup (they reference this assembly, not the other way round).</summary>
    public static class ServiceFactories
    {
        public static System.Func<IAdService> LevelPlay;
        public static System.Func<IPaymentService> UnityIap;
    }
}
