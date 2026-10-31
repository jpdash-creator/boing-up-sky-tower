using System;
using UnityEngine;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// Chooses the rewarded-ad implementation (test ads in the editor and development builds, LevelPlay on device), pre-loads, and
    /// wraps Show so a callback fires once and never grants on error, early close or a second tap while an ad is up.
    /// </summary>
    public static class AdService
    {
        /// <summary>Set by the LevelPlay assembly (Gameplay/Ads/LevelPlay) on device builds; null elsewhere.</summary>
        public static Func<IRewardedAds> DeviceFactory;

        static IRewardedAds rewarded;
        static bool showing;

        /// <summary>The active provider (created on first use). Tests can assign their own.</summary>
        public static IRewardedAds Rewarded
        {
            get { if (rewarded == null) rewarded = Create(); return rewarded; }
            set { rewarded = value; showing = false; }
        }

        static IRewardedAds Create()
        {
#if UNITY_EDITOR
            return new TestRewardedAds();
#else
            if (Debug.isDebugBuild && !AdConfig.UseLevelPlayInDevBuilds) return new TestRewardedAds();
            if (DeviceFactory != null)
            {
                try { return DeviceFactory(); }
                catch (Exception e) { Debug.LogWarning("[Ads] provider failed to start: " + e.Message); }
            }
            return new NoRewardedAds();
#endif
        }

        /// <summary>Call early (boot or run start). Idempotent; never blocks.</summary>
        public static void Preload()
        {
            try { Rewarded.Preload(); } catch (Exception e) { Debug.LogWarning("[Ads] preload: " + e.Message); }
        }

        public static bool IsRewardedReady
        {
            get
            {
                if (showing) return false;
                try { return Rewarded.IsReady; } catch { return false; }
            }
        }

        /// <summary>True while an ad is on screen (taps on the offer must be ignored).</summary>
        public static bool IsShowing { get { return showing; } }

        /// <summary>Shows a rewarded ad; <paramref name="onFinished"/> gets true only for a completed, rewarded view.</summary>
        public static void ShowRewarded(Action<bool> onFinished)
        {
            if (showing || !IsRewardedReady)
            {
                if (onFinished != null) onFinished(false);
                return;
            }
            showing = true;
            bool done = false;
            try
            {
                Rewarded.Show(granted =>
                {
                    if (done) return;
                    done = true;
                    showing = false;
                    Preload();
                    if (onFinished != null) onFinished(granted);
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] show failed: " + e.Message);
                if (!done) { done = true; showing = false; if (onFinished != null) onFinished(false); }
            }
        }
    }
}
