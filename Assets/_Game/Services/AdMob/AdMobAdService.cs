using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// Rewarded ads through Google AdMob (Google Mobile Ads Unity plugin 11.x). Compiled only with FLUFFY_ADMOB; on device only (the editor uses StubAdService).
    /// Order: request configuration -> UMP consent (ConsentInformation.Update, ConsentForm.LoadAndShowConsentFormIfRequired) -> MobileAds.Initialize only when
    /// CanRequestAds -> load the rewarded ad. After every show the next ad is loaded; failed loads retry with exponential backoff. Ids come from AdsConfig.
    /// All SDK callbacks may arrive on another thread, so each is marshalled to the main thread (MobileAdsEventExecutor.ExecuteInUpdate). The update
    /// driver is enabled only while a retry or a finish is pending, so nothing runs per frame while idle.
    /// </summary>
    public sealed class AdMobAdService : IAdService, IConsentAware, IAdLifecycle, IPrivacyOptions, IAdFailureInfo
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { ServiceFactories.AdMob = () => new AdMobAdService(); }

        sealed class Driver : MonoBehaviour
        {
            public Action Tick;
            void Update() { if (Tick != null) Tick(); }
        }

        sealed class Slot
        {
            public string UnitId;
            public RewardedAd Ad;
            public bool Loading;
            public int Failures;
            public float RetryAt = -1f;
        }

        const float BackoffStart = 5f, BackoffMax = 120f;

        readonly Dictionary<string, Slot> slots = new Dictionary<string, Slot>();
        readonly List<Slot> slotList = new List<Slot>();
        Driver driver;
        bool begun, sdkStarted, sdkReady, canRequestAds, consent;
        // current show
        Action<bool> pendingDone;
        Slot showingSlot;
        RewardedAd showingAd;
        volatile bool rewardEarned;
        bool failedToShow;
        int closeCountdown;   // frames left before the close is finalised (lets a late reward callback land)
        float showStartedAt;  // realtime of the current show: a watchdog ends a show that never reports back
        const float ShowWatchdogSeconds = 150f;   // longest rewarded video plus slack; after this the show is treated as failed
        public bool LastShowFailed { get; private set; }

        public void SetConsent(bool value) { consent = value; }

        // ---- start-up -------------------------------------------------------------------------------------------------

        public void Begin()
        {
            if (begun) return;
            begun = true;
            if (!AdsConfig.IsConfigured) { AdsConfig.WarnIfUnconfigured(); return; }
            try
            {
                var go = new GameObject("AdMobAdService");
                UnityEngine.Object.DontDestroyOnLoad(go);
                driver = go.AddComponent<Driver>();
                driver.Tick = Tick;
                driver.enabled = false;
                ApplyRequestConfiguration();
                if (ConsentInformation.CanRequestAds()) StartSdk();   // consent was gathered in an earlier session
                GatherConsent();
            }
            catch (Exception e) { Debug.LogWarning("[Ads] AdMob start failed, ads off: " + e.Message); }
        }

        static void ApplyRequestConfiguration()
        {
            var cfg = new RequestConfiguration();
            string rating = AdsConfig.MaxAdContentRating;
            cfg.MaxAdContentRating = rating == "G" ? MaxAdContentRating.G : rating == "PG" ? MaxAdContentRating.PG : rating == "MA" ? MaxAdContentRating.MA : MaxAdContentRating.T;
            string child = AdsConfig.TagForChildDirectedTreatment;
            cfg.AgeRestrictedTreatment = (child == "true" || child == "child") ? AgeRestrictedTreatment.Child : AgeRestrictedTreatment.Unspecified;
            string[] ids = AdsConfig.TestDeviceIds;
            if (ids.Length > 0) cfg.TestDeviceIds = new List<string>(ids);
            MobileAds.SetRequestConfiguration(cfg);
        }

        void GatherConsent()
        {
            var p = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
            if (Debug.isDebugBuild)
            {
                string g = AdsConfig.DebugGeography;
                if (g != "disabled")
                {
                    p.ConsentDebugSettings = new ConsentDebugSettings
                    {
                        DebugGeography = g == "not_eea" ? DebugGeography.NotEEA : DebugGeography.EEA,
                        TestDeviceHashedIds = new List<string>(AdsConfig.TestDeviceIds)
                    };
                }
            }
            ConsentInformation.Update(p, updateError => OnMain(() =>
            {
                if (updateError != null) Debug.LogWarning("[Ads] consent update: " + updateError.Message);
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => OnMain(() =>
                {
                    if (formError != null) Debug.LogWarning("[Ads] consent form: " + formError.Message);
                    RefreshConsent();
                }));
            }));
        }

        void RefreshConsent()
        {
            canRequestAds = ConsentInformation.CanRequestAds();
            Services.ConsentGiven = canRequestAds;
            if (canRequestAds) StartSdk();
        }

        void StartSdk()
        {
            if (sdkStarted) return;
            sdkStarted = true;
            MobileAds.Initialize(status => OnMain(() =>
            {
                sdkReady = true;
                Debug.Log("[Ads] Mobile Ads SDK initialised, consent can request ads=" + canRequestAds);
                RequestLoad(GetSlot(AdsConfig.RewardedUnitId(AdPlacements.Revive)));
            }));
        }

        // ---- privacy options -------------------------------------------------------------------------------------------

        public bool PrivacyOptionsRequired
        {
            get
            {
                try { return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required; }
                catch (Exception) { return false; }
            }
        }

        public void ShowPrivacyOptions(Action onClosed)
        {
            try
            {
                ConsentForm.ShowPrivacyOptionsForm(error => OnMain(() =>
                {
                    if (error != null) Debug.LogWarning("[Ads] privacy options: " + error.Message);
                    RefreshConsent();
                    if (onClosed != null) onClosed();
                }));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] privacy options failed: " + e.Message);
                if (onClosed != null) onClosed();
            }
        }

        // ---- loading ----------------------------------------------------------------------------------------------------

        Slot GetSlot(string unitId)
        {
            if (unitId == null) return null;
            Slot s;
            if (!slots.TryGetValue(unitId, out s))
            {
                s = new Slot { UnitId = unitId };
                slots[unitId] = s;
                slotList.Add(s);
            }
            return s;
        }

        void RequestLoad(Slot s)
        {
            if (s == null || !sdkReady || s.Loading || s.Ad != null) return;
            s.Loading = true;
            s.RetryAt = -1f;
            try
            {
                RewardedAd.Load(s.UnitId, new AdRequest(), (ad, error) => OnMain(() => OnLoaded(s, ad, error)));
            }
            catch (Exception e)
            {
                s.Loading = false;
                Debug.LogWarning("[Ads] load threw: " + e.Message);
                ScheduleRetry(s);
            }
        }

        void OnLoaded(Slot s, RewardedAd ad, LoadAdError error)
        {
            s.Loading = false;
            if (error != null || ad == null)
            {
                Debug.LogWarning("[Ads] rewarded load failed: " + (error != null ? error.ToString() : "no ad"));
                ScheduleRetry(s);
                return;
            }
            s.Failures = 0;
            s.Ad = ad;
            Debug.Log("[Ads] rewarded ad loaded (unit " + s.UnitId + ")");
        }

        void ScheduleRetry(Slot s)
        {
            s.Failures++;
            float delay = BackoffStart;
            for (int i = 1; i < s.Failures && delay < BackoffMax; i++) delay *= 2f;
            if (delay > BackoffMax) delay = BackoffMax;
            s.RetryAt = Time.unscaledTime + delay;
            if (driver != null) driver.enabled = true;
        }

        void Tick()
        {
            bool needed = false;
            if (closeCountdown > 0 && --closeCountdown == 0) Finish();
            if (closeCountdown > 0) needed = true;
            if (pendingDone != null)
            {
                // A show that never reports closed/failed (callback lost, app interrupted) must not freeze the revive screen forever.
                if (Time.realtimeSinceStartup - showStartedAt > ShowWatchdogSeconds)
                {
                    Debug.LogWarning("[Ads] show watchdog: no close/fail callback, ending the show as failed");
                    failedToShow = true;
                    Finish();
                }
                else needed = true;
            }
            float now = Time.unscaledTime;
            for (int i = 0; i < slotList.Count; i++)
            {
                Slot s = slotList[i];
                if (s.RetryAt < 0f) continue;
                if (now >= s.RetryAt) RequestLoad(s);
                else needed = true;
            }
            if (!needed && driver != null) driver.enabled = false;
        }

        // ---- IAdService -------------------------------------------------------------------------------------------------

        public bool IsReady(string placement)
        {
            if (!begun) Begin();
            if (!sdkReady || pendingDone != null) return false;
            Slot s = GetSlot(AdsConfig.RewardedUnitId(placement));
            if (s == null) return false;
            if (s.Ad == null) { RequestLoad(s); return false; }
            try { return s.Ad.CanShowAd(); }
            catch (Exception) { return false; }
        }

        public void ShowRewarded(string placement, Action<bool> onDone)
        {
            LastShowFailed = false;
            if (!IsReady(placement))
            {
                Debug.LogWarning("[Ads] ShowRewarded('" + placement + "'): no ad ready, nothing shown");
                LastShowFailed = true;
                if (onDone != null) onDone(false);
                return;
            }
            Debug.Log("[Ads] showing rewarded ad for '" + placement + "'");
            showStartedAt = Time.realtimeSinceStartup;
            if (driver != null) driver.enabled = true;   // runs the watchdog while the ad is up
            Slot s = GetSlot(AdsConfig.RewardedUnitId(placement));
            RewardedAd ad = s.Ad;
            s.Ad = null;
            pendingDone = onDone ?? (_ => { });
            showingSlot = s;
            showingAd = ad;
            rewardEarned = false;
            failedToShow = false;
            closeCountdown = 0;
            try
            {
                ad.OnAdFullScreenContentClosed += OnClosedRaw;
                ad.OnAdFullScreenContentFailed += OnFailedRaw;
                ad.Show(reward => { rewardEarned = true; });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] show threw: " + e.Message);
                failedToShow = true;
                Finish();
            }
        }

        void OnClosedRaw() { OnMain(() => { if (showingAd == null) return; closeCountdown = 2; if (driver != null) driver.enabled = true; }); }
        void OnFailedRaw(AdError error) { OnMain(() => { if (showingAd == null) return; Debug.LogWarning("[Ads] show failed: " + error); failedToShow = true; Finish(); }); }

        /// <summary>Completes the current show exactly once (true only when the reward was earned), then loads the next ad.</summary>
        void Finish()
        {
            Action<bool> cb = pendingDone;
            if (cb == null) return;
            pendingDone = null;
            closeCountdown = 0;
            bool granted = rewardEarned && !failedToShow;
            LastShowFailed = failedToShow;
            Debug.Log("[Ads] show finished: reward earned=" + rewardEarned + ", failed=" + failedToShow + ", granted=" + granted);
            RewardedAd ad = showingAd;
            Slot s = showingSlot;
            showingAd = null;
            showingSlot = null;
            try { if (ad != null) ad.Destroy(); } catch (Exception) { }
            RequestLoad(s);
            try { cb(granted); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void OnMain(Action a) { MobileAdsEventExecutor.ExecuteInUpdate(a); }
    }
}
