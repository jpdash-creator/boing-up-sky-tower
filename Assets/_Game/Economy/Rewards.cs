using System;
using FluffyClimbers.Content;
using FluffyClimbers.Simulation;

namespace FluffyClimbers.Economy
{
    /// <summary>Tunables that doc 08 leaves open. Defaults are assumptions; they will move to Remote Config.</summary>
    public static class EconomyRules
    {
        /// <summary>Extra coin payout percent per star beyond the first (doc 08: "base + star bonus", amount unspecified).</summary>
        public const int StarBonusPercentPerExtraStar = 10;

        // Economy review 2026-11-13 (owner): clear 56 + 48 per star + 4 per combo step (20 percent below economy v3), times the region coin factor.
        public const int ClearCoins = CoinPayoutRules.ClearCoins;
        public const int StarCoins = CoinPayoutRules.StarCoins;
        /// <summary>Once a level has 3 stars, replays pay this percent (25) of the coins and drop no gems; below 3 stars a replay pays in full.</summary>
        public const int ReplayPercent = CoinPayoutRules.ReplayPercent;
    }

    /// <summary>What a finished run reports to the economy. Build it from SimState and StarEvaluator.</summary>
    public struct RunOutcome
    {
        public bool Completed;
        /// <summary>Bits earned: bit 0 = star 1 (clear), bit 1 = star 2, bit 2 = star 3 (StarEvaluator.Earned).</summary>
        public int StarMask;
        public int Score;
        /// <summary>Gameplay setting 0..4 the run was played in (Relaxed..Insane); the crown (CrownScore) is the base Score with its bonus. 0 when a caller does not fill it.</summary>
        public int Mode;
        /// <summary>Coins picked up and earned by landing rules during the run (SimState.Coins).</summary>
        public int CoinsCollected;
        /// <summary>Normal gem pickups collected in the run (SimState.Gems). Always 0 since 2026-11-13 (no gems on the rows); the reward calculator ignores it.</summary>
        public int GemsCollected;
        /// <summary>Hidden gems collected in the run (SimState.HiddenGems): the goal object of level 15. It pays no gem of its own (level 15's 2 gems are the fixed milestone reward).</summary>
        public int HiddenGems;
        /// <summary>Id of the hero SELECTED for the run (RunSession.HeroId). It pays the hero milestone bonus on a first clear of levels 5/10/15/20/25; empty or unknown = a Common hero.</summary>
        public string HeroId;
        // Reward statistics (docs/26 section 4); zero when a caller does not fill them. The combo and power-up numbers are the NEW ones since the last report.
        /// <summary>Gem pickups the level holds (after a replay strips them) and how many of them the run collected: all taken = "collect all gems".</summary>
        public int GemPickupsTotal, GemPickupsTaken;
        /// <summary>Combo chains that reached 3 landings or more, and the best chain length.</summary>
        public int ComboChains, BestCombo;
        /// <summary>Power-up units used.</summary>
        public int PowerUpsUsed;
        /// <summary>Per-region statistics (Bug List 5 F1.a): the region index the run was played in (0 = unknown, the level's own region is used for a completed run) and the jumps made since the last report.</summary>
        public int Region, Jumps;
        /// <summary>Floors reached (the platform row counter). Stored with the best crown for the crown calculation pop-up; 0 when a caller does not fill it.</summary>
        public int Floors;

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
        public int PickupGems;       // always 0 since 2026-11-13 (no gem pickups on the rows)
        public int HiddenGemReward;  // always 0 since 2026-11-13 (the level 15 hidden gem is the goal object, its 2 gems are RewardGems)
        public int HeroBonusCoins;   // the selected hero's milestone bonus (levels 5/10/15/20/25, first clear), fixed coins by rarity
        public int HeroBonusGems;    // the selected hero's milestone bonus gems (sure gems plus the seeded 50 percent extra one)
        public string UnlockedRegionId;

        public int TotalCoins { get { return PayoutCoins + PickupCoins + HeroBonusCoins; } }
        public int TotalGems { get { return RewardGems + PickupGems + HiddenGemReward + HeroBonusGems; } }
    }

    /// <summary>
    /// Doc 08 section 7 reward rules, deterministic (rolls use the Simulation Rng seeded from the level seed and clear count,
    /// so a server can recompute them).
    /// Economy review 2026-11-13: a failed run pays nothing; the only gems in a level are the FIXED milestone gems of levels 5, 10, 15, 20 and 25 (1, 1, 2, 2, 3, from the level JSON
    /// <c>rewards.gems.firstClearGuaranteed</c>), first clear only; there are no gem pickups on the rows and no gem for the hidden gem; the selected hero adds its milestone bonus on those
    /// first clears (<see cref="CoinPayoutRules.HeroBonusCoins"/>, <see cref="CoinPayoutRules.HeroBonusGems"/>).
    /// </summary>
    public static class RewardCalculator
    {
        public static bool IsMilestone(LevelDef level)
        {
            // Bug List 6 item 4: the class follows the difficulty score now (complex = 75+), so the gem milestones are the special levels 5, 10, 15, 20 and 25 (levels 5, 10, 15 in the 15-level regions).
            return level.Index > 0 && level.Index % 5 == 0;
        }

        /// <summary>The fixed gems a first clear of this level pays (1, 1, 2, 2, 3 on levels 5, 10, 15, 20, 25 of regions 1 to 5), 0 for every other level. The level tiles and the level pop-up show it.</summary>
        public static int MilestoneGemsOf(LevelDef level)
        {
            if (level == null || !IsMilestone(level) || level.Rewards == null || level.Rewards.Gems == null) return 0;
            return CoinPayoutRules.MilestoneGems(level.Rewards.Gems.FirstClearGuaranteed ?? 0);
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

            // Payout: (clear 56 + 48 per star + 4 per combo step) x region factor. Once the level already has 3 stars, replays pay 25 percent and
            // drop no gems (owner rule); levels with fewer stars keep full coins.
            int prevStars = before == null ? 0 : before.Stars;
            bool reducedReplay = CoinPayoutRules.IsReducedReplay(prevStars);
            int comboRaw = run.CoinsCollected < 0 ? 0 : run.CoinsCollected;
            long gross = CoinPayoutRules.Scale(CoinPayoutRules.BaseCoins(stars), region.CoinFactor);
            long combo = CoinPayoutRules.Scale(CoinPayoutRules.ComboCoins(comboRaw), region.CoinFactor);
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
            // Gems: none from pickups or the hidden gem. A first clear of a milestone level (5, 10, 15, 20, 25) pays its fixed gems from the level JSON.
            result.PickupGems = 0;
            result.HiddenGemReward = 0;
            result.RewardGems = 0;
            if (firstClear && IsMilestone(level))
            {
                if (level.Rewards != null && level.Rewards.Gems != null) result.RewardGems = CoinPayoutRules.MilestoneGems(level.Rewards.Gems.FirstClearGuaranteed ?? 0);
                // The hero selected for the run adds its rarity bonus on the same first clears: fixed coins (no region factor) and gems with a seeded 50 percent extra one.
                CharacterSpec hero = CharacterDefs.Get(run.HeroId);
                int rarity = hero == null ? (int)CharacterRarity.Common : (int)hero.Rarity;
                result.HeroBonusCoins = CoinPayoutRules.HeroBonusCoins(rarity);
                result.HeroBonusGems = CoinPayoutRules.HeroBonusGems(rarity, level.Seed, hero == null ? "" : hero.Id);
            }
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
