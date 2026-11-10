using UnityEngine;

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// AdMob settings from Resources/Services/ads_config.json (docs/33). Schema:
    /// { "androidAppId", "rewardedAdUnitIds": { "default", "revive", "double_reward", "daily_powerup", "shop_free_gems" },
    ///   "testMode", "tagForChildDirectedTreatment" ("unspecified"|"true"|"false"), "maxAdContentRating" ("G"|"PG"|"T"|"MA"),
    ///   "debugGeography" ("disabled"|"eea"|"not_eea"; development builds only), "testDeviceIds": [] }.
    /// Rules: the shipped file holds placeholders. The editor and development builds with placeholders (or testMode=true) use Google's public SAMPLE ids;
    /// a RELEASE build with placeholders shows NO ads (revive falls back to gems) and logs one warning; sample ids ship in release only with testMode=true.
    /// </summary>
    public static class AdsConfig
    {
        public const string PlaceholderAppId = "REPLACE_WITH_ADMOB_APP_ID";
        public const string PlaceholderUnitId = "REPLACE_WITH_REWARDED_UNIT_ID";
        public const string SampleAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string SampleRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";
        public const string ResourcePath = "Services/ads_config";

        [System.Serializable]
        sealed class Units { public string @default; public string revive; public string double_reward; public string daily_powerup; public string shop_free_gems; }

        [System.Serializable]
        sealed class Data
        {
            public string androidAppId;
            public Units rewardedAdUnitIds;
            public bool testMode;
            public string tagForChildDirectedTreatment;
            public string maxAdContentRating;
            public string debugGeography;
            public string[] testDeviceIds;
        }

        static bool loaded, warned;
        static Data data;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                TextAsset t = Resources.Load<TextAsset>(ResourcePath);
                if (t != null) data = JsonUtility.FromJson<Data>(t.text);
            }
            catch (System.Exception e) { Debug.LogWarning("[Ads] ads_config.json unreadable: " + e.Message); }
            if (data == null) data = new Data();
            if (data.rewardedAdUnitIds == null) data.rewardedAdUnitIds = new Units();
        }

        /// <summary>Tests: forget the cached file.</summary>
        public static void Reset() { loaded = false; warned = false; data = null; }

        public static bool IsPlaceholder(string id) { return string.IsNullOrEmpty(id) || id.StartsWith("REPLACE_WITH"); }

        /// <summary>True in the editor and in development builds (sample ids allowed when ids are placeholders).</summary>
        public static bool IsDevContext { get { return Application.isEditor || Debug.isDebugBuild; } }

        public static bool TestMode { get { Load(); return data.testMode; } }
        public static string RawAppId { get { Load(); return data.androidAppId; } }
        public static string TagForChildDirectedTreatment { get { Load(); return string.IsNullOrEmpty(data.tagForChildDirectedTreatment) ? "unspecified" : data.tagForChildDirectedTreatment.ToLowerInvariant(); } }
        public static string MaxAdContentRating { get { Load(); return string.IsNullOrEmpty(data.maxAdContentRating) ? "T" : data.maxAdContentRating.ToUpperInvariant(); } }
        public static string DebugGeography { get { Load(); return string.IsNullOrEmpty(data.debugGeography) ? "disabled" : data.debugGeography.ToLowerInvariant(); } }
        public static string[] TestDeviceIds { get { Load(); return data.testDeviceIds ?? new string[0]; } }

        /// <summary>Pure rule shared with the build hooks: the app id to use, or null for "no ads".</summary>
        public static string ResolveAppId(string raw, bool testMode, bool devContext)
        {
            if (testMode) return SampleAppId;
            if (!IsPlaceholder(raw)) return raw.Trim();
            return devContext ? SampleAppId : null;
        }

        /// <summary>Pure rule: the rewarded unit id for a placement ("default" fallback), or null for "no ads".</summary>
        public static string ResolveUnitId(string placementId, string defaultId, bool testMode, bool devContext)
        {
            if (testMode) return SampleRewardedUnitId;
            // Editor and development builds ALWAYS use Google's sample unit, never the real one: clicking or loading your own real ads on a test
            // device breaks AdMob policy (invalid traffic) and can get the account limited.
            if (devContext) return SampleRewardedUnitId;
            string id = !IsPlaceholder(placementId) ? placementId : defaultId;
            if (!IsPlaceholder(id)) return id.Trim();
            return null;
        }

        public static string AppId { get { Load(); return ResolveAppId(data.androidAppId, data.testMode, IsDevContext); } }

        public static string RewardedUnitId(string placement)
        {
            Load();
            Units u = data.rewardedAdUnitIds;
            string specific = null;
            if (placement == AdPlacements.Revive || placement == "revive_30" || placement == "revive_60") specific = u.revive;
            else if (placement == AdPlacements.DoubleReward) specific = u.double_reward;
            else if (placement == AdPlacements.DailyPowerUp) specific = u.daily_powerup;
            else if (placement == AdPlacements.ShopFreeGems) specific = u.shop_free_gems;
            return ResolveUnitId(specific, u.@default, data.testMode, IsDevContext);
        }

        /// <summary>True when ads can run at all (app id and a default/revive unit resolve). False in a release build with placeholders.</summary>
        public static bool IsConfigured { get { return AppId != null && RewardedUnitId(AdPlacements.Revive) != null; } }

        /// <summary>Logs one clear warning (once) when ads are off because of placeholders.</summary>
        public static void WarnIfUnconfigured()
        {
            if (warned || IsConfigured) return;
            warned = true;
            Debug.LogWarning("[Ads] ads_config.json still has placeholder AdMob ids and this is a release build: ads are OFF (revive uses gems). Put the real androidAppId and rewardedAdUnitIds in Resources/Services/ads_config.json (docs/33).");
        }
    }
}
