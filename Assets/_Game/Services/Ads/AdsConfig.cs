using UnityEngine;

namespace BoingUpSkyTower.Monetization
{
    /// <summary>
    /// LevelPlay credentials from Resources/Services/ads_config.json: {"appKey","rewardedAdUnitId","testMode"}. The shipped file holds
    /// placeholders, so a build without real ids stays on "not ready" (no ads) and never crashes. See docs/33 section 5.
    /// </summary>
    public static class AdsConfig
    {
        public const string PlaceholderAppKey = "REPLACE_WITH_LEVELPLAY_APP_KEY";
        public const string PlaceholderAdUnit = "REPLACE_WITH_REWARDED_AD_UNIT_ID";
        public const string ResourcePath = "Services/ads_config";

        [System.Serializable]
        sealed class Data { public string appKey; public string rewardedAdUnitId; public bool testMode = true; }

        static bool loaded;
        static string appKey = PlaceholderAppKey, rewardedAdUnitId = PlaceholderAdUnit;
        static bool testMode = true;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                TextAsset t = Resources.Load<TextAsset>(ResourcePath);
                if (t == null) return;
                Data d = JsonUtility.FromJson<Data>(t.text);
                if (d == null) return;
                if (!string.IsNullOrEmpty(d.appKey)) appKey = d.appKey;
                if (!string.IsNullOrEmpty(d.rewardedAdUnitId)) rewardedAdUnitId = d.rewardedAdUnitId;
                testMode = d.testMode;
            }
            catch (System.Exception e) { Debug.LogWarning("[Ads] ads_config.json unreadable: " + e.Message); }
        }

        public static string AppKey { get { Load(); return appKey; } }
        public static string RewardedAdUnitId { get { Load(); return rewardedAdUnitId; } }
        /// <summary>When true the LevelPlay test suite / validation may be opened in development builds.</summary>
        public static bool TestMode { get { Load(); return testMode; } }
        public static bool IsConfigured { get { Load(); return appKey != PlaceholderAppKey && rewardedAdUnitId != PlaceholderAdUnit; } }
    }
}
