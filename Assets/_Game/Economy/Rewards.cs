using System;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Simulation;

namespace BoingUpSkyTower.Economy
{
    /// <summary>Tunables that doc 08 leaves open. Defaults are assumptions; they will move to Remote Config.</summary>
    public static class EconomyRules
    {
        /// <summary>Extra coin payout percent per star beyond the first (doc 08: "base + star bonus", amount unspecified).</summary>
        public const int StarBonusPercentPerExtraStar = 10;

        // Economy v2 (owner, 2026-10-20): coins are paid at the end of a level, never picked up.
        public const int ClearCoins = 30;
        public const int StarCoins = 40;
        /// <summary>Owner 2026-10-24: once a level has been cleared (any stars), replays pay this percent of the coins and drop no gems.</summary>
        public const int ReplayPercent = 67;
    }

    /// <summary>What a finished run reports to the economy. Build it from SimState and StarEvaluator.</summary>
    public struct RunOutcome
    {
        public bool Completed;
        /// <summary>Bits earned: bit 0 = star 1 (clear), bit 1 = star 2, bit 2 = star 3 (StarEvaluator.Earned).</summary>
        public int StarMask;
        public int Score;
        /// <summary>Coins picked up and earned by landing rules during the run (SimState.Coins).</summary>
        public int CoinsCollected;
        /// <summary>Normal gem pickups collected in the run (SimState.Gems).</summary>
        public int GemsCollected;
        /// <summary>Hidden gems collected in the run (SimState.HiddenGems). Pays +1 gem on the first clear of the level only (owner decision 2026-10-19).</summary>
        public int HiddenGems;

        public int Stars
        {
            get
            {
                int n = 0;
                for (int m = StarMask & 7; m != 0; m >>= 1) n += m & 1;
                return n;
            }
        }
    }

    public struct RewardResult
    {
        public bool FirstClear;
        public int Stars;
        public int PayoutCoins;      // end-of-level payout after region factor, star bonus and replay percent
        public int StarBonusCoins;   // part of PayoutCoins that came from the star bonus
        public int ComboCoins;       // part of PayoutCoins that came from combo steps (5 each, region factor applied)
        public int PickupCoins;      // always 0 in economy v2 (kept for old callers)
        public int RewardGems;       // milestone gem rewards (L5/L10/L15)
        public int PickupGems;       // gems picked up during the run
        public int HiddenGemReward;  // +1 gem for a hidden gem, first clear only
        public string UnlockedRegionId;
        /// <summary>Gear piece ids dropped by this clear (docs/17 section B); null or empty when nothing dropped.</summary>
        public System.Collections.Generic.List<string> GearDrops;

        public int TotalCoins { get { return PayoutCoins + PickupCoins; } }
        public int TotalGems { get { return RewardGems + PickupGems + HiddenGemReward; } }
    }

    /// <summary>
    /// Doc 08 section 7 reward rules, deterministic (rolls use the Simulation Rng seeded from the level seed and clear count,
    /// so a server can recompute them).
    /// Assumptions (open for the owner): a failed run pays nothing; in-run pickups always pay in full (only the end-of-level payout
    /// is reduced on replays); normal levels get gems only from in-run pickups (the 3 percent chance in the content is the pickup);
    /// milestone gem rewards (L5, L10, L15) follow the doc 08 table; a hidden gem pays +1 gem on the first clear only.
    /// </summary>
    public static class RewardCalculator
    {
        public static bool IsMilestone(LevelDef level)
        {
            return level.LevelClass == LevelDef.ClassComplex || level.LevelClass == LevelDef.ClassHighlyComplex;
        }

        /// <summary>Most gems one clear of this level can ever pay: 2 on level 15, 1 elsewhere.</summary>
        public static int MaxGems(LevelDef level)
        {
            return level.LevelClass == LevelDef.ClassHighlyComplex ? 2 : 1;
        }

        static int Percent(double chance)
        {
            return (int)Math.Round(chance * 100.0);
        }

        public static RewardResult Compute(LevelDef level, RegionDef region, RunOutcome run, LevelProgress before)
        {
            var result = new RewardResult();
            if (!run.Completed) return result;

            int clearsBefore = before == null ? 0 : before.Clears;
            bool firstClear = clearsBefore == 0;
            int stars = run.Stars;
            result.FirstClear = firstClear;
            result.Stars = stars;

            // Economy v2 payout: (clear 30 + 40 per star + 5 per combo step) x region factor. Once the level has 3 stars,
            // replays pay half and drop no gems (owner rule); levels with fewer stars keep full coins and gems.
            int prevStars = before == null ? 0 : before.Stars;
            bool threeStarReplay = !firstClear;   // any earlier clear: replay
            long factor = (long)Math.Round(region.CoinFactor * 10000);
            int comboRaw = run.CoinsCollected < 0 ? 0 : run.CoinsCollected;
            long baseCoins = EconomyRules.ClearCoins + (long)EconomyRules.StarCoins * stars;
            long gross = (baseCoins * factor + 5000) / 10000;
            long combo = ((long)comboRaw * factor + 5000) / 10000;
            long starPart = ((long)EconomyRules.StarCoins * stars * factor + 5000) / 10000;
            if (threeStarReplay)
            {
                gross = gross * EconomyRules.ReplayPercent / 100;
                combo = combo * EconomyRules.ReplayPercent / 100;
                starPart = starPart * EconomyRules.ReplayPercent / 100;
            }
            result.PayoutCoins = (int)(gross + combo);
            result.StarBonusCoins = (int)starPart;
            result.ComboCoins = (int)combo;
            result.PickupCoins = 0;
            // Gems are rare (owner 2026-10-24): they come only from the level's planned gem pickups, never on a replay, and never above the level's cap.
            result.PickupGems = threeStarReplay || run.GemsCollected < 0 ? 0 : Math.Min(run.GemsCollected, MaxGems(level));
            result.HiddenGemReward = 0;
            result.RewardGems = 0;
            // Gear drops (docs/17): every completed run rolls, replays included, at the odds of the level's place in the region.
            result.GearDrops = GearDrops.Roll(level.Index, level.Seed, clearsBefore);
            return result;
        }
    }

    /// <summary>What an endless run paid (doc 08 section 7, D3).</summary>
    public struct EndlessReward
    {
        public int Floors;
        public int BestBefore;
        public bool NewBest;
        /// <summary>Coins before the daily cap (floors based, region factor applied).</summary>
        public int FullRateCoins;
        /// <summary>Part of FullRateCoins that came from combo landings (5 each, region factor applied).</summary>
        public int ComboCoins;
        /// <summary>Coins actually paid.</summary>
        public int Coins;
        /// <summary>True when the daily full-rate runs were used up and the over-cap multiplier applied.</summary>
        public bool OverCap;
        /// <summary>Paying runs finished today including this one.</summary>
        public int RunsToday;
    }

    /// <summary>
    /// Endless coin rules (owner decision 2026-10-20, doc 08 section 7 and D3): +1 coin per 2 floors and +50 coins for every full 100 floors,
    /// times the region coin factor (rounded half up). The per-landing coin rule does not apply in endless. Per UTC day the first
    /// <c>endlessCoinDailyFullRateRuns</c> (3) paying runs pay in full, later runs pay <c>endlessCoinOverCapMultiplier</c> (0.5) of that.
    /// A run that earns 0 base coins (under 2 floors) is not counted against the cap. Pure integer math, so a server can recompute it.
    /// </summary>
    public static class EndlessRewards
    {
        public const int FloorsPerCoin = 2;
        public const int HundredFloorBonus = 50;

        public static int RawCoins(int floors)
        {
            if (floors <= 0) return 0;
            return floors / FloorsPerCoin + HundredFloorBonus * (floors / 100);
        }

        public static int FullRateCoins(int floors, double regionCoinFactor)
        {
            return FullRateCoins(floors, 0, regionCoinFactor);
        }

        /// <summary>Floor coins plus the run's combo coins (SimState.Coins in endless), both scaled by the region factor.</summary>
        public static int FullRateCoins(int floors, int comboCoins, double regionCoinFactor)
        {
            long factor = (long)Math.Round(regionCoinFactor * 10000);
            long raw = RawCoins(floors) + (comboCoins < 0 ? 0 : comboCoins);
            return (int)((raw * factor + 5000) / 10000);
        }

        public static int OverCapCoins(int fullRateCoins, double overCapMultiplier)
        {
            long pct = (long)Math.Round(overCapMultiplier * 100);
            return (int)(((long)fullRateCoins * pct + 50) / 100);
        }

        /// <summary>UTC day key of a moment, "yyyyMMdd".</summary>
        public static string DayKey(DateTime utc)
        {
            return utc.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
