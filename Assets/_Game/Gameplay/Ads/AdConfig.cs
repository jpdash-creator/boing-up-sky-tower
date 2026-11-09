using UnityEngine;

namespace FluffyClimbers.Gameplay
{
    /// <summary>
    /// LevelPlay credentials. The owner supplies the real app key and rewarded ad unit id later (open item 10 in doc 22, together with
    /// the SDK consent and age-gate wording); until then the placeholders keep the device build in "no ads" mode, so only the gem option shows.
    /// To override without recompiling, put a Resources/AdsConfig.json: {"appKey":"...","rewardedAdUnitId":"..."}.
    /// Consent and age gate: the privacy calls (LevelPlayPrivacySettings) must run before Init; see the open item in doc 22 before release.
    /// </summary>
    public static class AdConfig
    {
        public const string PlaceholderAppKey = "REPLACE_WITH_LEVELPLAY_APP_KEY";
        public const string PlaceholderAdUnit = "REPLACE_WITH_REWARDED_AD_UNIT_ID";
        /// <summary>Dev builds on a device use the instant TestRewardedAds unless this is true (then the real SDK with its test ads is used).</summary>
        public const bool UseLevelPlayInDevBuilds = false;

        [System.Serializable]
        sealed class Data { public string appKey; public string rewardedAdUnitId; }

        static bool loaded;
        static string appKey = PlaceholderAppKey, rewardedAdUnitId = PlaceholderAdUnit;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                TextAsset t = Resources.Load<TextAsset>("AdsConfig");
                if (t == null) return;
                Data d = JsonUtility.FromJson<Data>(t.text);
                if (d == null) return;
                if (!string.IsNullOrEmpty(d.appKey)) appKey = d.appKey;
                if (!string.IsNullOrEmpty(d.rewardedAdUnitId)) rewardedAdUnitId = d.rewardedAdUnitId;
            }
            catch (System.Exception e) { Debug.LogWarning("[Ads] AdsConfig unreadable: " + e.Message); }
        }

        public static string AppKey { get { Load(); return appKey; } }
        public static string RewardedAdUnitId { get { Load(); return rewardedAdUnitId; } }
        public static bool IsConfigured { get { Load(); return appKey != PlaceholderAppKey && rewardedAdUnitId != PlaceholderAdUnit; } }
    }
}
