#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Device rewarded ads through Unity LevelPlay (package com.unity.services.levelplay). Lives in its own Android-only assembly so the rest of
    /// the game compiles without the SDK; it registers itself with <see cref="AdService.DeviceFactory"/>. Without a real app key and ad unit id
    /// (see <see cref="AdConfig"/>) it stays inactive and reports "not ready". Test ads come from the LevelPlay dashboard test mode and test devices.
    /// Consent and age gate: the privacy calls must be made before Init; see the open item in doc 22 before release.
    /// </summary>
    public sealed class LevelPlayRewardedAds : IRewardedAds
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { AdService.DeviceFactory = () => new LevelPlayRewardedAds(); }

        sealed class Driver : MonoBehaviour
        {
            public Action Tick;
            void Update() { if (Tick != null) Tick(); }
        }

        LevelPlayRewardedAd ad;
        bool initStarted, initDone, loading, rewardEarned;
        Action<bool> onFinished;
        float retryAt = -1f, retryDelay = 30f;
        Driver driver;

        public bool IsReady { get { return initDone && ad != null && ad.IsAdReady(); } }

        public void Preload()
        {
            if (!AdConfig.IsConfigured) return;
            if (!initStarted)
            {
                initStarted = true;
                if (driver == null)
                {
                    var go = new GameObject("LevelPlayAds");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    driver = go.AddComponent<Driver>();
                    driver.Tick = Update;
                }
                LevelPlay.OnInitSuccess += OnInitSuccess;
                LevelPlay.OnInitFailed += OnInitFailed;
                LevelPlay.Init(AdConfig.AppKey);
                return;
            }
            LoadIfNeeded();
        }

        void OnInitSuccess(LevelPlayConfiguration config)
        {
            initDone = true;
            ad = new LevelPlayRewardedAd(AdConfig.RewardedAdUnitId);
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
            initStarted = false;                      // a later retry initialises again
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
            if (retryAt >= 0f && Time.unscaledTime >= retryAt)
            {
                retryAt = -1f;
                if (initDone) LoadIfNeeded(); else Preload();
            }
        }

        void OnAdLoaded(LevelPlayAdInfo info) { loading = false; retryDelay = 30f; }

        void OnAdLoadFailed(LevelPlayAdError error)
        {
            loading = false;
            Debug.LogWarning("[Ads] rewarded load failed: " + error.ErrorMessage);
            retryAt = Time.unscaledTime + retryDelay;
            retryDelay = Mathf.Min(retryDelay * 2f, 300f);
        }

        public void Show(Action<bool> finished)
        {
            if (!IsReady || onFinished != null) { if (finished != null) finished(false); return; }
            onFinished = finished;
            rewardEarned = false;
            ad.ShowAd();
        }

        void OnAdRewarded(LevelPlayAdInfo info, LevelPlayReward reward) { rewardEarned = true; }

        // The reward counts only if the SDK reported it before the ad closed; an early close reports false.
        void OnAdClosed(LevelPlayAdInfo info) { Finish(rewardEarned); }

        void OnAdDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
        {
            Debug.LogWarning("[Ads] rewarded display failed: " + error.ErrorMessage);
            Finish(false);
        }

        void Finish(bool granted)
        {
            Action<bool> cb = onFinished;
            onFinished = null;
            rewardEarned = false;
            loading = false;
            LoadIfNeeded();
            if (cb != null) cb(granted);
        }
    }
}
#endif
