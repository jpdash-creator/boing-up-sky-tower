using System;
using BoingTower.Content;

namespace BoingTower.Economy
{
    public enum SkipResult
    {
        Ok,
        AlreadyUnlocked,
        NotPlayable,
        NotNextRegion,
        NotEnoughGems
    }

    public enum ShopResult
    {
        Ok,
        NotEnoughGems,
        Invalid
    }

    /// <summary>
    /// Owns the player's local progress: wallet, per-level results, region unlocks. Every mutation saves immediately.
    /// Rules (doc 01 FR-P5, FR-P6; doc 08):
    ///  - Region 1 is always unlocked. Clearing level 15 of a region unlocks the next region (if it is playable).
    ///  - Or pay the region's gem skip price to unlock the NEXT locked region without clearing the previous one.
    ///  - Teaser regions can never be entered. Only regions with index &lt;= PlayableRegionLimit are playable in this build
    ///    (launch scope is 5 regions; the content still marks 6-20 live, so the limit lives here until the content is updated).
    ///  - Inside a region, level N unlocks when level N-1 has been cleared (assumption, doc 02 is silent).
    /// </summary>
    public sealed class ProgressService
    {
        readonly ContentRepository repo;
        readonly ISaveStore store;

        public SaveData Data { get; private set; }
        public int PlayableRegionLimit { get; private set; }

        /// <summary>Raised after any change to coins, gems or progress.</summary>
        public event Action Changed;

        public ProgressService(ContentRepository repo, ISaveStore store, int playableRegionLimit = 5)
        {
            this.repo = repo;
            this.store = store;
            PlayableRegionLimit = playableRegionLimit;
            Data = store.Load();
            EnsureEndlessFields();
        }

        /// <summary>Saves written before endless mode (or with null fields) get safe defaults.</summary>
        void EnsureEndlessFields()
        {
            if (Data.EndlessBest == null) Data.EndlessBest = new System.Collections.Generic.Dictionary<string, int>();
            if (Data.EndlessDay == null) Data.EndlessDay = "";
        }

        public int Coins { get { return Data.Coins; } }
        public int Gems { get { return Data.Gems; } }

        // ---- wallet ----

        public void AddCoins(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException("amount");
            Data.Coins = SaturatingAdd(Data.Coins, amount);
            Commit();
        }

        public void AddGems(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException("amount");
            Data.Gems = SaturatingAdd(Data.Gems, amount);
            Commit();
        }

        public bool TrySpendCoins(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException("amount");
            if (Data.Coins < amount) return false;
            Data.Coins -= amount;
            Commit();
            return true;
        }

        public bool TrySpendGems(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException("amount");
            if (Data.Gems < amount) return false;
            Data.Gems -= amount;
            Commit();
            return true;
        }

        static int SaturatingAdd(int a, int b)
        {
            long sum = (long)a + b;
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        // ---- shop (docs/14) ----

        /// <summary>
        /// Launch promotion switch (default on). While on, packs cost their promotional price. Later driven by the remote config flag
        /// shop.launchPromo.enabled; when it is off the list price applies.
        /// </summary>
        public bool LaunchPromoActive { get; set; } = true;

        /// <summary>Gem price of a coin pack under the current promotion state.</summary>
        public int CoinPackPrice(CoinPackDef pack)
        {
            return ShopCatalog.CoinPackGems(pack, LaunchPromoActive);
        }

        /// <summary>
        /// TEST purchase (development builds only, the caller checks): credits the pack's gems without billing and saves at once.
        /// Real purchases will go through the server-validated flow instead (docs/13, docs/14).
        /// </summary>
        public ShopResult GrantTestGemPurchase(GemPackDef pack)
        {
            if (pack == null || pack.Gems <= 0) return ShopResult.Invalid;
            Data.Gems = SaturatingAdd(Data.Gems, pack.Gems);
            Commit();
            return ShopResult.Ok;
        }

        /// <summary>
        /// Buys a coin pack with gems: debits the gems and credits the coins in one step and saves once (atomic: either both happen or
        /// neither). Not enough gems changes nothing.
        /// </summary>
        public ShopResult TryBuyCoinPack(CoinPackDef pack)
        {
            if (pack == null || pack.Coins <= 0) return ShopResult.Invalid;
            int price = CoinPackPrice(pack);
            if (price <= 0) return ShopResult.Invalid;
            if (Data.Gems < price) return ShopResult.NotEnoughGems;
            Data.Gems -= price;
            Data.Coins = SaturatingAdd(Data.Coins, pack.Coins);
            Commit();
            return ShopResult.Ok;
        }

        // ---- regions ----

        public bool IsRegionPlayable(RegionDef region)
        {
            return region.IsLive && region.Index <= PlayableRegionLimit;
        }

        /// <summary>Testing aid for development builds (Settings > "Test: all levels open"): every level and region of the playable set counts as unlocked. Off in tests and release builds.</summary>
        public static bool TestUnlockAll;

        public bool IsRegionUnlocked(RegionDef region)
        {
            return TestUnlockAll || region.Index == 1 || Data.UnlockedRegions.Contains(region.Id);
        }

        public bool CanEnterRegion(RegionDef region)
        {
            return IsRegionPlayable(region) && IsRegionUnlocked(region);
        }

        /// <summary>Index of the highest unlocked region (1 when nothing else is unlocked).</summary>
        public int HighestUnlockedRegionIndex()
        {
            int best = 1;
            foreach (string id in Data.UnlockedRegions)
            {
                RegionDef r = repo.GetRegion(id);
                if (r.Index > best) best = r.Index;
            }
            return best;
        }

        public int GemSkipPrice(RegionDef region)
        {
            return region.Unlock.GemSkip ?? 0;
        }

        public SkipResult TrySkipToRegion(RegionDef region)
        {
            if (IsRegionUnlocked(region)) return SkipResult.AlreadyUnlocked;
            if (!IsRegionPlayable(region)) return SkipResult.NotPlayable;
            if (region.Index != HighestUnlockedRegionIndex() + 1) return SkipResult.NotNextRegion;
            int price = GemSkipPrice(region);
            if (Data.Gems < price) return SkipResult.NotEnoughGems;
            Data.Gems -= price;
            Data.UnlockedRegions.Add(region.Id);
            Commit();
            return SkipResult.Ok;
        }

        // ---- levels ----

        public LevelProgress GetProgress(string levelId)
        {
            LevelProgress p;
            return Data.Levels.TryGetValue(levelId, out p) ? p : null;
        }

        public bool IsLevelCleared(string levelId)
        {
            LevelProgress p = GetProgress(levelId);
            return p != null && p.Clears > 0;
        }

        public bool IsLevelUnlocked(LevelDef level)
        {
            RegionDef region = repo.GetRegion(level.Region);
            if (!CanEnterRegion(region)) return false;
            if (TestUnlockAll || level.Index <= 1) return true;
            string previousId = region.Levels[level.Index - 2];
            return IsLevelCleared(previousId);
        }

        public int TotalStars()
        {
            int n = 0;
            foreach (LevelProgress p in Data.Levels.Values) n += p.Stars;
            return n;
        }

        public int StarsInRegion(RegionDef region)
        {
            int n = 0;
            foreach (string id in region.Levels)
            {
                LevelProgress p = GetProgress(id);
                if (p != null) n += p.Stars;
            }
            return n;
        }

        /// <summary>
        /// Applies a finished run: pays rewards, records the best result, unlocks the next region after level 15.
        /// A run that did not complete changes nothing. Throws if the level is locked (callers must not start locked levels).
        /// </summary>
        public RewardResult ApplyRun(string levelId, RunOutcome run)
        {
            LevelDef level = repo.GetLevel(levelId);
            RegionDef region = repo.GetRegion(level.Region);
            if (!IsLevelUnlocked(level)) throw new InvalidOperationException("Level is locked: " + levelId);
            if (!run.Completed) return new RewardResult();

            LevelProgress before = GetProgress(levelId);
            RewardResult reward = RewardCalculator.Compute(level, region, run, before);

            LevelProgress p = before ?? new LevelProgress();
            p.Clears++;
            p.StarMask |= run.StarMask & 7;
            p.Stars = new RunOutcome { StarMask = p.StarMask }.Stars;
            if (run.Score > p.BestScore) p.BestScore = run.Score;
            Data.Levels[levelId] = p;

            Data.Coins = SaturatingAdd(Data.Coins, reward.TotalCoins);
            Data.Gems = SaturatingAdd(Data.Gems, reward.TotalGems);

            if (level.Index == region.Levels.Count)
            {
                RegionDef next = repo.TryGetRegionByIndex(region.Index + 1);
                if (next != null && IsRegionPlayable(next) && !IsRegionUnlocked(next))
                {
                    Data.UnlockedRegions.Add(next.Id);
                    reward.UnlockedRegionId = next.Id;
                }
            }
            Commit();
            return reward;
        }

        // ---- endless ----

        /// <summary>Best floor reached in the region's endless tower (0 = never played).</summary>
        public int EndlessBestFloor(string regionId)
        {
            int best;
            return Data.EndlessBest != null && Data.EndlessBest.TryGetValue(regionId, out best) ? best : 0;
        }

        /// <summary>Paying endless runs finished on the given UTC day (0 when the counter belongs to another day).</summary>
        public int EndlessRunsOn(string dayKey)
        {
            return Data.EndlessDay == dayKey ? Data.EndlessRunsToday : 0;
        }

        /// <summary>
        /// Applies a finished endless run (the player fell): saves the best floor, pays the floors based coins (EndlessRewards) with the
        /// daily cap rule of the remote config, and saves immediately. <paramref name="dayKey"/> is the UTC day, "yyyyMMdd".
        /// The region must be enterable.
        /// </summary>
        public EndlessReward ApplyEndlessRun(string regionId, int floors, string dayKey)
        {
            RegionDef region = repo.GetRegion(regionId);
            if (!CanEnterRegion(region)) throw new InvalidOperationException("Region is locked: " + regionId);
            if (floors < 0) floors = 0;
            EnsureEndlessFields();

            var result = new EndlessReward();
            result.Floors = floors;
            result.BestBefore = EndlessBestFloor(regionId);
            result.NewBest = floors > result.BestBefore;
            if (result.NewBest) Data.EndlessBest[regionId] = floors;

            int full = EndlessRewards.FullRateCoins(floors, region.CoinFactor);
            result.FullRateCoins = full;
            result.Coins = full;
            if (full > 0)
            {
                if (Data.EndlessDay != dayKey) { Data.EndlessDay = dayKey; Data.EndlessRunsToday = 0; }
                EconomyConfigDef eco = repo.RemoteConfig.Economy;
                if (Data.EndlessRunsToday >= eco.EndlessCoinDailyFullRateRuns)
                {
                    result.OverCap = true;
                    result.Coins = EndlessRewards.OverCapCoins(full, eco.EndlessCoinOverCapMultiplier);
                }
                Data.EndlessRunsToday++;
                result.RunsToday = Data.EndlessRunsToday;
                Data.Coins = SaturatingAdd(Data.Coins, result.Coins);
            }
            else
            {
                result.RunsToday = EndlessRunsOn(dayKey);
            }
            Commit();
            return result;
        }

        /// <summary>Erases all progress, coins and gems (Settings > Reset progress).</summary>
        public void ResetAll()
        {
            Data = new SaveData();
            Commit();
        }

        void Commit()
        {
            store.Save(Data);
            Changed?.Invoke();
        }
    }
}
