using System;
using System.Collections.Generic;
using FluffyClimbers.Content;

namespace FluffyClimbers.Economy
{
    /// <summary>
    /// Main tab = per-region goals (Bug List 5 F1.a, docs/39). A region's goals open when the region is unlocked and count ONLY play inside that region.
    /// Stars, cleared levels, 3-star levels, no-wall clears and the best endless floor are read from the existing save (so a player who already has them
    /// finds the goals READY, never auto-paid). Jumps, combos, play time, coins, power-ups and hard clears are the additive per-region counters
    /// (<see cref="RegionStats"/>), counted from the build that introduced them. The tables live in <see cref="RegionGoalTable"/>.
    /// </summary>
    public sealed partial class ProgressService
    {
        // ---- the lists ----

        /// <summary>The goals of a region in listing order. Empty for regions 6 to 30 (Coming Soon) and for an invalid index.</summary>
        public IReadOnlyList<RegionGoal> RegionGoals(int region) { return RewardDefs.RegionGoals(region); }

        /// <summary>True for a region that has no goals yet (6 to 30): the Main tab shows it as "Coming Soon" and nothing can be claimed.</summary>
        public bool IsRegionComingSoon(int region) { return RewardDefs.IsComingSoon(region); }

        /// <summary>True when the goals of the region are open: it has goals and the region is unlocked (or the Admin Main switch is on).</summary>
        public bool IsRegionGoalsOpen(int region)
        {
            if (RewardDefs.IsComingSoon(region)) return false;
            if (AdminMainRewardsReady) return true;
            RegionDef def = repo.TryGetRegionByIndex(region);
            return def != null && IsRegionPlayable(def) && IsRegionUnlocked(def);
        }

        bool GoalsOpen(RewardEntry e)
        {
            RegionGoal g = e as RegionGoal;
            return g != null && IsRegionGoalsOpen(g.Region);
        }

        // ---- progress ----

        /// <summary>The lifetime counters of a region (a zeroed copy when nothing was counted yet; changing it does not change the save).</summary>
        public RegionStats RegionStatsOf(int region)
        {
            RegionStats s = RegionStatsFor(region, false);
            if (s == null) return new RegionStats();
            return new RegionStats { Jumps = s.Jumps, Combos = s.Combos, PlaySeconds = s.PlaySeconds, Coins = s.Coins, PowerUps = s.PowerUps, HardClears = s.HardClears };
        }

        /// <summary>Levels of the region with 3 stars.</summary>
        public int RegionThreeStarLevels(int region)
        {
            RegionDef def = repo.TryGetRegionByIndex(region);
            if (def == null) return 0;
            int n = 0;
            foreach (string id in def.Levels)
            {
                LevelProgress p = GetProgress(id);
                if (p != null && p.Stars >= 3) n++;
            }
            return n;
        }

        /// <summary>Clears of the region's no-wall level (level 10), as stored in the level progress (replays count).</summary>
        public int RegionNoWallClears(int region)
        {
            RegionDef def = repo.TryGetRegionByIndex(region);
            if (def == null || def.Levels.Count < RegionGoalTable.NoWallLevelIndex) return 0;
            LevelProgress p = GetProgress(def.Levels[RegionGoalTable.NoWallLevelIndex - 1]);
            return p != null ? p.Clears : 0;
        }

        /// <summary>The real value of a goal's counter, without the Admin override.</summary>
        int RealGoalProgress(RegionGoal g)
        {
            RegionDef def = repo.TryGetRegionByIndex(g.Region);
            if (def == null) return 0;
            RegionStats s = RegionStatsFor(g.Region, false);
            switch (g.Type)
            {
                case RegionGoalType.Stars: return StarsInRegion(def);
                case RegionGoalType.Levels: return RegionLevelsCleared(g.Region);
                case RegionGoalType.ThreeStarLevels: return RegionThreeStarLevels(g.Region);
                case RegionGoalType.Jumps: return s != null ? s.Jumps : 0;
                case RegionGoalType.Combos: return s != null ? s.Combos : 0;
                case RegionGoalType.PlayMinutes: return s != null ? s.PlaySeconds / 60 : 0;
                case RegionGoalType.Coins: return s != null ? s.Coins : 0;
                case RegionGoalType.PowerUps: return s != null ? s.PowerUps : 0;
                case RegionGoalType.HardClears: return s != null ? s.HardClears : 0;
                case RegionGoalType.NoWallClears: return RegionNoWallClears(g.Region);
                case RegionGoalType.GoldMedalLevels: return GoldMedalLevelsInRegion(g.Region);
                default: return EndlessBestFloor(def.Id);
            }
        }

        /// <summary>Progress of a goal, comparable with <c>goal.Target</c> (shown for every goal, claimed or not). With the Admin switch on it is at least the target.</summary>
        public int RegionGoalProgress(RegionGoal goal)
        {
            return goal == null ? 0 : MainProgress(goal);
        }

        /// <summary>Same, with the region explicit (the goal must belong to it, otherwise 0).</summary>
        public int RegionGoalProgress(int region, RegionGoal goal)
        {
            return goal != null && goal.Region == region ? MainProgress(goal) : 0;
        }

        public bool IsRegionGoalClaimed(RegionGoal goal) { return goal != null && IsRewardClaimed(RewardTab.Main, goal); }

        /// <summary>True when the goal is complete, its region is open and it is not claimed yet.</summary>
        public bool IsRegionGoalReady(RegionGoal goal) { return goal != null && IsRewardReady(RewardTab.Main, goal); }

        /// <summary>Goals of the region that are claimed / ready right now.</summary>
        public int RegionGoalsClaimedCount(int region)
        {
            int n = 0;
            foreach (RegionGoal g in RegionGoals(region)) if (IsRegionGoalClaimed(g)) n++;
            return n;
        }

        public int RegionGoalsReadyCount(int region)
        {
            int n = 0;
            foreach (RegionGoal g in RegionGoals(region)) if (IsRegionGoalReady(g)) n++;
            return n;
        }

        /// <summary>True when something in the region can be claimed (red dot on the region circle).</summary>
        public bool HasClaimableInRegion(int region)
        {
            foreach (RegionGoal g in RegionGoals(region)) if (IsRegionGoalReady(g)) return true;
            return false;
        }

        /// <summary>True when any region has a goal to claim (red dot on the Main tab).</summary>
        public bool HasClaimableMain() { return HasClaimable(RewardTab.Main); }

        // ---- claiming ----

        /// <summary>Claims one goal: gems, coins, power-up units and, for the last Levels goal of a region, the region's Common hero (1 copy). Atomic and saved once.</summary>
        public RewardClaimOutcome TryClaimRegionGoal(string goalId) { return TryClaimReward(RewardTab.Main, goalId); }

        /// <summary>Claims every ready goal of one region in one step (atomic, one save). Result is NotReady when nothing was ready.</summary>
        public RewardClaimOutcome ClaimAllRegion(int region)
        {
            var outcome = new RewardClaimOutcome { Result = RewardClaimResult.NotReady };
            RolloverRewards();
            var ready = new List<RegionGoal>();
            foreach (RegionGoal g in RegionGoals(region)) if (IsRegionGoalReady(g)) ready.Add(g);
            if (ready.Count == 0) return outcome;
            foreach (RegionGoal g in ready) PayReward(RewardTab.Main, g, outcome);
            outcome.Result = RewardClaimResult.Ok;
            FinishClaim(outcome);
            return outcome;
        }

        // ---- recording from endless runs ----

        /// <summary>Endless run result for the reward statistics: coins paid go to the region's coin counter, the floor to the daily and weekly "reach floor N" task. Called by ApplyEndlessRun before its save.</summary>
        void TrackEndlessRun(RegionDef region, int floors, int coinsPaid)
        {
            RolloverRewards();
            RewardsData r = Rw;
            MaxStat(r.Day, RewardStat.EndlessFloor, floors);
            MaxStat(r.Week, RewardStat.EndlessFloor, floors);
            if (floors >= RewardStat.MinEndlessRunFloors) AddBoth(RewardStat.EndlessRuns, 1);   // Main Bug List 2: "finish N Endless runs" (Time Attack passes 0 floors and never counts)
            if (coinsPaid > 0) AddBoth(RewardStat.CoinsEarned, coinsPaid);
            if (coinsPaid > 0)
            {
                RegionStats s = RegionStatsFor(region.Index, true);
                if (s != null) s.Coins = SaturatingAdd(s.Coins, coinsPaid);
            }
        }
    }
}
