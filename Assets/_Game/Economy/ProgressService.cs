using System;
using BoingUpSkyTower.Content;

namespace BoingUpSkyTower.Economy
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
    public sealed partial class ProgressService
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
            Data.Migrate();
            EnsureEndlessFields();
            EnsurePowerUpFields();
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

        // ---- power-ups (docs/15 FR-PWR): owned stock, loadout, shop purchases ----

        PowerUpCatalog powerUpDefs;

        /// <summary>The power-up table (built-in copy of Content/economy/powerups.json; tests and remote overrides may replace it).</summary>
        public PowerUpCatalog PowerUpDefs
        {
            get { return powerUpDefs ?? (powerUpDefs = PowerUpCatalog.BuiltIn()); }
            set { powerUpDefs = value; }
        }

        /// <summary>Saves written before power-ups get an empty inventory and loadout; stored loadout is kept at exactly 3 entries.</summary>
        void EnsurePowerUpFields()
        {
            if (Data.Inventory == null) Data.Inventory = new InventoryData();
            if (Data.Inventory.Powerups == null) Data.Inventory.Powerups = new System.Collections.Generic.Dictionary<string, int>();
            if (Data.Loadout == null) Data.Loadout = new System.Collections.Generic.List<string>();
            while (Data.Loadout.Count < PowerUpCatalog.SlotCount) Data.Loadout.Add("");
            if (Data.Loadout.Count > PowerUpCatalog.SlotCount) Data.Loadout.RemoveRange(PowerUpCatalog.SlotCount, Data.Loadout.Count - PowerUpCatalog.SlotCount);
            // counts are never negative or above the stack cap; unknown ids are dropped
            var bad = new System.Collections.Generic.List<string>();
            var fix = new System.Collections.Generic.List<string>();
            foreach (var kv in Data.Inventory.Powerups)
            {
                PowerUpDef d = PowerUpDefs.Get(kv.Key);
                if (d == null || kv.Value <= 0) bad.Add(kv.Key);
                else if (kv.Value > d.MaxStack) fix.Add(kv.Key);
            }
            foreach (string id in bad) Data.Inventory.Powerups.Remove(id);
            foreach (string id in fix) Data.Inventory.Powerups[id] = PowerUpDefs.Get(id).MaxStack;
        }

        /// <summary>Owned units of a power-up (0 for an unknown id).</summary>
        public int PowerUpCount(string id)
        {
            int n;
            return id != null && Data.Inventory.Powerups.TryGetValue(id, out n) ? n : 0;
        }

        /// <summary>Adds units to the stock (shop, chests, rewards), capped at the stack limit. Returns how many were added (0 when unknown, invalid or full).</summary>
        public int GrantPowerUp(string id, int count)
        {
            PowerUpDef def = PowerUpDefs.Get(id);
            if (def == null || count <= 0) return 0;
            int have = PowerUpCount(id);
            int add = Math.Min(Math.Max(0, def.MaxStack - have), count);
            if (add <= 0) return 0;
            Data.Inventory.Powerups[id] = have + add;
            Commit();
            return add;
        }

        /// <summary>
        /// Uses up units from the stock (a power-up was activated in a run) and saves at once. Fails without changing anything when fewer
        /// are owned. A loadout slot whose stock reaches zero is emptied.
        /// </summary>
        public bool TryConsumePowerUp(string id, int count = 1)
        {
            if (count <= 0) return false;
            int have = PowerUpCount(id);
            if (have < count) return false;
            if (have == count)
            {
                Data.Inventory.Powerups.Remove(id);
                for (int i = 0; i < Data.Loadout.Count; i++) if (Data.Loadout[i] == id) Data.Loadout[i] = "";
            }
            else
            {
                Data.Inventory.Powerups[id] = have - count;
            }
            Commit();
            return true;
        }

        /// <summary>True when the region that unlocks this power-up has been reached.</summary>
        public bool IsPowerUpUnlocked(PowerUpDef def)
        {
            return def != null && (TestUnlockAll || def.UnlockRegion <= HighestUnlockedRegionIndex());
        }

        /// <summary>
        /// Buys <paramref name="quantity"/> units with coins or gems: debits the wallet and adds the stock in one step and saves once
        /// (atomic). Nothing changes unless the result is Ok.
        /// </summary>
        public PowerUpBuyResult TryBuyPowerUp(string id, PowerUpCurrency currency, int quantity = 1)
        {
            PowerUpDef def = PowerUpDefs.Get(id);
            if (def == null || !def.Implemented || quantity <= 0 || quantity > def.MaxStack) return PowerUpBuyResult.Invalid;
            if (!IsPowerUpUnlocked(def)) return PowerUpBuyResult.Locked;
            int price = PowerUpCatalog.PriceFor(def, currency, quantity);
            if (price <= 0) return PowerUpBuyResult.Invalid;
            int have = PowerUpCount(id);
            if (have + quantity > def.MaxStack) return PowerUpBuyResult.StackFull;
            if (currency == PowerUpCurrency.Coins)
            {
                if (Data.Coins < price) return PowerUpBuyResult.NotEnoughCoins;
                Data.Coins -= price;
            }
            else
            {
                if (Data.Gems < price) return PowerUpBuyResult.NotEnoughGems;
                Data.Gems -= price;
            }
            Data.Inventory.Powerups[id] = have + quantity;
            Commit();
            return PowerUpBuyResult.Ok;
        }

        /// <summary>A power-up may sit in a loadout slot when it exists in the simulation, is unlocked and at least one unit is owned.</summary>
        public bool IsLoadoutCandidate(string id)
        {
            PowerUpDef def = PowerUpDefs.Get(id);
            return def != null && def.Implemented && IsPowerUpUnlocked(def) && PowerUpCount(id) > 0;
        }

        /// <summary>The validated loadout: always 3 entries, an id or null for an empty slot. Unowned or unknown entries read as empty and no id appears twice.</summary>
        public string[] GetLoadout()
        {
            var result = new string[PowerUpCatalog.SlotCount];
            for (int i = 0; i < result.Length; i++)
            {
                string id = i < Data.Loadout.Count ? Data.Loadout[i] : "";
                if (string.IsNullOrEmpty(id) || !IsLoadoutCandidate(id)) continue;
                bool duplicate = false;
                for (int j = 0; j < i; j++) if (result[j] == id) duplicate = true;
                if (!duplicate) result[i] = id;
            }
            return result;
        }

        /// <summary>
        /// Puts a power-up into a slot (or empties the slot with null or ""). Rejects a bad slot number or an id that is not a loadout
        /// candidate. An id already in another slot moves (one type per slot, docs/15 section 3.1). Saves at once.
        /// </summary>
        public bool SetLoadoutSlot(int slot, string id)
        {
            if (slot < 0 || slot >= PowerUpCatalog.SlotCount) return false;
            string[] now = GetLoadout();
            if (string.IsNullOrEmpty(id))
            {
                now[slot] = null;
            }
            else
            {
                if (!IsLoadoutCandidate(id)) return false;
                for (int i = 0; i < now.Length; i++) if (now[i] == id) now[i] = null;
                now[slot] = id;
            }
            for (int i = 0; i < now.Length; i++) Data.Loadout[i] = now[i] ?? "";
            Commit();
            return true;
        }

        /// <summary>Replaces the whole loadout. Fails (and changes nothing) when more than 3 ids are given or any id is invalid or repeated.</summary>
        public bool SetLoadout(System.Collections.Generic.IList<string> ids)
        {
            if (ids == null || ids.Count > PowerUpCatalog.SlotCount) return false;
            var seen = new System.Collections.Generic.List<string>();
            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (!IsLoadoutCandidate(id) || seen.Contains(id)) return false;
                seen.Add(id);
            }
            for (int i = 0; i < PowerUpCatalog.SlotCount; i++)
                Data.Loadout[i] = i < ids.Count && ids[i] != null ? ids[i] : "";
            Commit();
            return true;
        }

        /// <summary>Units of this power-up one run carries: the owned count, at most the type's carry cap.</summary>
        public int CarryUnits(string id)
        {
            PowerUpDef def = PowerUpDefs.Get(id);
            if (def == null) return 0;
            return Math.Min(PowerUpCount(id), def.CarryCap);
        }

        /// <summary>The three slots a run starts with (type and units carried); empty slots have PowerUp.None and 0 units.</summary>
        public LoadoutSlot[] BuildRunLoadout()
        {
            string[] ids = GetLoadout();
            var slots = new LoadoutSlot[PowerUpCatalog.SlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                PowerUpDef def = PowerUpDefs.Get(ids[i]);
                if (def == null) continue;
                slots[i].Type = def.Type;
                slots[i].Units = CarryUnits(ids[i]);
            }
            return slots;
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

        /// <summary>
        /// The region the player is working on (owner 2026-10-24: the home page follows it): the first region, in order, that can be entered and still has
        /// a level that is not cleared; when everything playable is cleared, the last playable region.
        /// </summary>
        public RegionDef CurrentRegion()
        {
            RegionDef last = null;
            foreach (RegionDef r in repo.Regions)
            {
                if (!CanEnterRegion(r)) continue;
                last = r;
                foreach (LevelDef l in repo.LevelsOf(r.Id)) if (!IsLevelCleared(l.Id)) return r;
            }
            return last ?? repo.GetRegionByIndex(1);
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
        public EndlessReward ApplyEndlessRun(string regionId, int floors, string dayKey, int comboCoins = 0)
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

            int full = EndlessRewards.FullRateCoins(floors, comboCoins, region.CoinFactor);
            result.FullRateCoins = full;
            result.ComboCoins = (int)(((long)(comboCoins < 0 ? 0 : comboCoins) * (long)Math.Round(region.CoinFactor * 10000) + 5000) / 10000);
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
            EnsurePowerUpFields();
            Commit();
        }

        void Commit()
        {
            store.Save(Data);
            Changed?.Invoke();
        }
    }
}
