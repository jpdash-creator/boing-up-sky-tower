#if FLUFFY_LEVELPLAY
using System;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// Rewarded ads through Unity LevelPlay (package com.unity.services.levelplay 9.x). Compiled only with the scripting define
    /// FLUFFY_LEVELPLAY. Credentials come from AdsConfig (Resources/Services/ads_config.json); with placeholders it stays inactive
    /// ("not ready"). Initialises on first use. The placement name is passed to ShowAd for dashboard reporting.
    /// Replaces the older Gameplay/Ads/LevelPlay path once the revive code uses IAdService.
    /// </summary>
    public sealed class LevelPlayAdService : IAdService, IConsentAware
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { ServiceFactories.LevelPlay = () => new LevelPlayAdService(); }

        sealed class Driver : MonoBehaviour
        {
            public Action Tick;
            void Update() { if (Tick != null) Tick(); }
        }

        LevelPlayRewardedAd ad;
        bool initStarted, initDone, loading, rewardEarned, consent;
        Action<bool> onDone;
        float retryAt = -1f, retryDelay = 30f;
        Driver driver;

        public void SetConsent(bool value)
        {
            consent = value;
            if (initStarted) LevelPlay.SetConsent(value);   // before Init it is applied in Start()
        }

        public bool IsReady(string placement)
        {
            Start();
            return initDone && ad != null && onDone == null && ad.IsAdReady();
        }

        void Start()
        {
            if (initStarted || !AdsConfig.IsConfigured) return;
            initStarted = true;
            var go = new GameObject("LevelPlayAdService");
            UnityEngine.Object.DontDestroyOnLoad(go);
            driver = go.AddComponent<Driver>();
            driver.Tick = Update;
            LevelPlay.SetConsent(consent);                 // privacy calls must precede Init
            LevelPlay.OnInitSuccess += OnInitSuccess;
            LevelPlay.OnInitFailed += OnInitFailed;
            LevelPlay.Init(AdsConfig.AppKey);
        }

        void OnInitSuccess(LevelPlayConfiguration config)
        {
            initDone = true;
            ad = new LevelPlayRewardedAd(AdsConfig.RewardedAdUnitId);
            ad.OnAdLoaded += OnAdLoaded;
            ad.OnAdLoadFailed += OnAdLoadFailed;
            ad.OnAdDisplayFailed += OnAdDisplayFailed;
            ad.OnAdRewarded += OnAdRewarded;
            ad.OnAdClosed += OnAdClosed;
            LoadIfNeeded();
        }

        void OnInitFailed(LevelPlayInitError error)
        {
            Debug.LogWarning("[Ads] LevelPlay init failed: " + error.ErrorMessage);
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;
            initStarted = false;
            retryAt = Time.unscaledTime + retryDelay;
        }

        void LoadIfNeeded()
        {
            if (!initDone || ad == null || loading || ad.IsAdReady()) return;
            loading = true;
            ad.LoadAd();
        }

        void Update()
        {
            if (retryAt < 0f || Time.unscaledTime < retryAt) return;
            retryAt = -1f;
            if (initDone) LoadIfNeeded(); else Start();
        }

        void OnAdLoaded(LevelPlayAdInfo info) { loading = false; retryDelay = 30f; }

        void OnAdLoadFailed(LevelPlayAdError error)
        {
            loading = false;
            Debug.LogWarning("[Ads] rewarded load failed: " + error.ErrorMessage);
            retryAt = Time.unscaledTime + retryDelay;
            retryDelay = Mathf.Min(retryDelay * 2f, 300f);
        }

        public void ShowRewarded(string placement, Action<bool> done)
        {
            if (!IsReady(placement)) { if (done != null) done(false); return; }
            onDone = done;
            rewardEarned = false;
            ad.ShowAd(placement);
        }

        void OnAdRewarded(LevelPlayAdInfo info, LevelPlayReward reward) { rewardEarned = true; }

        // The reward counts only if the SDK reported it before the ad closed.
        void OnAdClosed(LevelPlayAdInfo info) { Finish(rewardEarned); }

        void OnAdDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
        {
            Debug.LogWarning("[Ads] rewarded display failed: " + error.ErrorMessage);
            Finish(false);
        }

        void Finish(bool granted)
        {
            Action<bool> cb = onDone;
            onDone = null;
            rewardEarned = false;
            loading = false;
            LoadIfNeeded();
            if (cb != null) cb(granted);
        }
    }
}
#endif
