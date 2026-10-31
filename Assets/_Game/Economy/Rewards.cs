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

        // Economy v3 (owner, issue list 24 point 6, 2026-10-25): clear 70 + 60 per star + 5 per combo step, times the region coin factor.
        public const int ClearCoins = CoinPayoutRules.ClearCoins;
        public const int StarCoins = CoinPayoutRules.StarCoins;
        /// <summary>Once a level has 3 stars, replays pay this percent of the coins and drop no gems; below 3 stars a replay pays in full.</summary>
        public const int ReplayPercent = CoinPayoutRules.ReplayPercent;
        /// <summary>Anti-tamper ceiling on pickup gems one run can report (a level holds far fewer: about 15 on the longest at the highest difficulty).</summary>
        public const int MaxPickupGemsPerRun = 400;
        /// <summary>Doc 25: the first clear of level 15 pays this extra bonus (one double gem, worth 2) on top of the milestone gems.</summary>
        public const int Level15DoubleGemBonus = 2;
        /// <summary>Doc 25: the hidden bonus gem of level 8 pays its value (1, or 2 for a double one) on the first clear only.</summary>
        public const int Level8HiddenGemMax = 2;
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
        // Reward statistics (docs/26 section 4); zero when a caller does not fill them. The combo and power-up numbers are the NEW ones since the last report.
        /// <summary>Gem pickups the level holds (after a replay strips them) and how many of them the run collected: all taken = "collect all gems".</summary>
        public int GemPickupsTotal, GemPickupsTaken;
        /// <summary>Combo chains that reached 3 landings or more, and the best chain length.</summary>
        public int ComboChains, BestCombo;
        /// <summary>Power-up units used.</summary>
        public int PowerUpsUsed;

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

            // Economy v3 payout: (clear 70 + 60 per star + 5 per combo step) x region factor. Once the level already has 3 stars, replays pay half and
            // drop no gems (owner rule); levels with fewer stars keep full coins and gems.
            int prevStars = before == null ? 0 : before.Stars;
            bool reducedReplay = CoinPayoutRules.IsReducedReplay(prevStars);
            int comboRaw = run.CoinsCollected < 0 ? 0 : run.CoinsCollected;
            long gross = CoinPayoutRules.Scale(CoinPayoutRules.BaseCoins(stars), region.CoinFactor);
            long combo = CoinPayoutRules.Scale(comboRaw, region.CoinFactor);
            long starPart = CoinPayoutRules.Scale((long)EconomyRules.StarCoins * stars, region.CoinFactor);
            if (reducedReplay)
            {
                gross = gross * EconomyRules.ReplayPercent / 100;
                combo = combo * EconomyRules.ReplayPercent / 100;
                starPart = starPart * EconomyRules.ReplayPercent / 100;
            }
            result.PayoutCoins = (int)(gross + combo);
            result.StarBonusCoins = (int)starPart;
            result.ComboCoins = (int)combo;
            result.PickupCoins = 0;
            // Gems (issue list 24 point 5): the gems the player picked up in the run (kept as reported, up to an anti-tamper ceiling), none on a
            // replay of a 3-star level, plus the milestone gems of a first clear of level 5, 10 and 15 (L15 guarantees 5).
            result.PickupGems = reducedReplay || run.GemsCollected < 0 ? 0 : Math.Min(run.GemsCollected, EconomyRules.MaxPickupGemsPerRun);
            result.HiddenGemReward = 0;
            result.RewardGems = 0;
            if (firstClear && IsMilestone(level) && level.Rewards != null && level.Rewards.Gems != null)
            {
                GemRewardDef g = level.Rewards.Gems;
                result.RewardGems = CoinPayoutRules.MilestoneGems(level.Seed, g.Chance, g.Min, g.Max, g.FirstClearGuaranteed ?? 0);
            }
            if (firstClear && level.Index == 15) result.RewardGems += EconomyRules.Level15DoubleGemBonus;
            if (firstClear && level.Index == 8) result.HiddenGemReward = Math.Min(Math.Max(run.HiddenGems, 0), EconomyRules.Level8HiddenGemMax);
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
