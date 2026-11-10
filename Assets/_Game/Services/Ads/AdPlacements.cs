using System.Collections.Generic;

namespace FluffyClimbers.Monetization
{
    /// <summary>
    /// Every rewarded-ad placement (docs/13 section 4.2, docs/33). The string is also the AdMob placement key: ads_config.json maps each
    /// placement to a rewarded ad unit id (fallback "default"). Only Revive is built today.
    /// </summary>
    public static class AdPlacements
    {
        /// <summary>Failed overlay: one free revive per run (built).</summary>
        public const string Revive = "revive";
        /// <summary>Results screen after a win: x2 coins of that run (planned).</summary>
        public const string DoubleReward = "double_reward";
        /// <summary>Rewards/home: one free random power-up per day (planned).</summary>
        public const string DailyPowerUp = "daily_powerup";
        /// <summary>Shop: small gem reward, capped (planned, optional).</summary>
        public const string ShopFreeGems = "shop_free_gems";

        public static readonly IReadOnlyList<string> All = new[] { Revive, DoubleReward, DailyPowerUp, ShopFreeGems };
        public static readonly IReadOnlyList<string> Built = new[] { Revive };
    }
}
