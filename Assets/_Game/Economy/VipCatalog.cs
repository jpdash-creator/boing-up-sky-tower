using System.Collections.Generic;
using System.Globalization;

namespace BoingUpSkyTower.Economy
{
    /// <summary>One VIP tier (docs/15 section 6.1). UI only for now: nothing here changes a payout, a jump or an ad until billing, the server and the bonus pipeline exist.</summary>
    public sealed class VipTierDef
    {
        public int Tier;
        public double MonthlyUsd, WeeklyUsd;
        public int JumpPct, GemPct, CoinPct, DailyGems;
        public bool AdFree;
        public string Extras;

        public string Name { get { return "VIP " + Tier; } }

        public string MonthlyText { get { return "$" + MonthlyUsd.ToString("0.00", CultureInfo.InvariantCulture) + " / month"; } }
        public string WeeklyText { get { return "$" + WeeklyUsd.ToString("0.00", CultureInfo.InvariantCulture) + " / week"; } }

        /// <summary>The bonus lines shown on the card details, plain words.</summary>
        public List<string> Lines()
        {
            var l = new List<string>
            {
                "Jump power +" + JumpPct + "%",
                "Gems from runs +" + GemPct + "%",
                "Coins from runs +" + CoinPct + "%",
            };
            l.Add(DailyGems > 0 ? "Daily gems: " + DailyGems : "No daily gems");
            l.Add(AdFree ? "No ad prompts" : "Ads stay on");
            if (!string.IsNullOrEmpty(Extras)) l.Add(Extras);
            return l;
        }
    }

    public static class VipCatalog
    {
        public const int TierCount = 6;

        static readonly VipTierDef[] Tiers =
        {
            new VipTierDef { Tier = 1, MonthlyUsd = 3.99, WeeklyUsd = 1.49, JumpPct = 2, GemPct = 5, CoinPct = 5, DailyGems = 0, AdFree = false, Extras = "Crown hat" },
            new VipTierDef { Tier = 2, MonthlyUsd = 6.99, WeeklyUsd = 2.49, JumpPct = 4, GemPct = 10, CoinPct = 10, DailyGems = 5, AdFree = false, Extras = "" },
            new VipTierDef { Tier = 3, MonthlyUsd = 9.99, WeeklyUsd = 3.49, JumpPct = 6, GemPct = 15, CoinPct = 20, DailyGems = 10, AdFree = true, Extras = "" },
            new VipTierDef { Tier = 4, MonthlyUsd = 14.99, WeeklyUsd = 4.99, JumpPct = 8, GemPct = 20, CoinPct = 30, DailyGems = 15, AdFree = true, Extras = "Rare chest every week" },
            new VipTierDef { Tier = 5, MonthlyUsd = 21.99, WeeklyUsd = 7.49, JumpPct = 10, GemPct = 25, CoinPct = 40, DailyGems = 20, AdFree = true, Extras = "Rare chest every week, exclusive boots" },
            new VipTierDef { Tier = 6, MonthlyUsd = 32.99, WeeklyUsd = 10.99, JumpPct = 12, GemPct = 30, CoinPct = 50, DailyGems = 25, AdFree = true, Extras = "Rare chest every week, exclusive outfit, name badge" },
        };

        public static IReadOnlyList<VipTierDef> All { get { return Tiers; } }

        /// <summary>Tier 1 to 6, or null.</summary>
        public static VipTierDef Get(int tier) { return tier >= 1 && tier <= TierCount ? Tiers[tier - 1] : null; }

        /// <summary>Permanent caps of every bonus source together (docs/15 FR-REGION-2). VIP 6 plus the best hero stays inside them.</summary>
        public const int JumpCapPct = 25, GemCapPct = 40, CoinCapPct = 60;
    }
}
