using UnityEngine;

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// Static locator: Services.Ads, Services.Payments. The real SDK services are used when the scripting define
    /// FLUFFY_LEVELPLAY / FLUFFY_UNITY_IAP is on (and the package is installed), else the stubs. Resolved once; no per-frame work.
    /// </summary>
    public static class Services
    {
        static IAdService ads;
        static IPaymentService payments;
        static IAccountService account;
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

        /// <summary>The player's Google account and cloud save (docs/35): Play Games with FLUFFY_GPGS on Android, else the stub in the editor and development builds (signs in as "Test Player"), else an unavailable service (guest only).</summary>
        public static IAccountService Account
        {
            get { if (account == null) account = CreateAccount(); return account; }
            set { account = value; }
        }

        /// <summary>Tests: forget all services and the consent flag.</summary>
        public static void Reset() { ads = null; payments = null; account = null; consent = false; }

        static IAccountService CreateAccount()
        {
#if FLUFFY_GPGS
            if (!Application.isEditor && ServiceFactories.GooglePlay != null)
            {
                try { return ServiceFactories.GooglePlay(); }
                catch (System.Exception e) { Debug.LogWarning("[Services] Play Games unavailable, using stub: " + e.Message); }
            }
#endif
            // The fake "Test Player" account is for the editor and development builds only; a release build without the SDK offers guest play only.
            if (Application.isEditor || Debug.isDebugBuild) return new StubAccountService();
            return new UnavailableAccountService();
        }

        static IAdService CreateAds()
        {
#if FLUFFY_LEVELPLAY
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
#if FLUFFY_UNITY_IAP
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
        public static System.Func<IAccountService> GooglePlay;
    }
}
