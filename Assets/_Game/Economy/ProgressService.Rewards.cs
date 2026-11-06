using System;
using System.Collections.Generic;
using System.Globalization;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Simulation;
using Newtonsoft.Json;

namespace BoingUpSkyTower.Economy
{
    /// <summary>Counters and claims of one reward period: a local day (Key = yyyyMMdd) or a week (Key = the Monday, yyyyMMdd).</summary>
    public sealed class RewardPeriod
    {
        [JsonProperty("key")] public string Key = "";
        /// <summary>Counter name (RewardStat) to value.</summary>
        [JsonProperty("stats")] public Dictionary<string, int> Stats = new Dictionary<string, int>();
        /// <summary>Ids of the rewards already claimed in this period.</summary>
        [JsonProperty("claimed")] public List<string> Claimed = new List<string>();
    }

    /// <summary>Lifetime counters of the play inside ONE region (Main goals, docs/39). Counted from the build that introduced them; never reset.</summary>
    public sealed class RegionStats
    {
        [JsonProperty("jumps")] public int Jumps;
        /// <summary>Combo chains that reached 3 landings.</summary>
        [JsonProperty("combos")] public int Combos;
        [JsonProperty("playSec")] public int PlaySeconds;
        /// <summary>Coins earned in the region (level payouts and endless payouts).</summary>
        [JsonProperty("coins")] public int Coins;
        [JsonProperty("powerUps")] public int PowerUps;
        /// <summary>Levels cleared on Hard or Insane.</summary>
        [JsonProperty("hardClears")] public int HardClears;
    }

    /// <summary>Everything the Rewards screen needs from the save (docs/26 section 4). Additive part of SaveData, no version bump.</summary>
    public sealed class RewardsData
    {
        [JsonProperty("day")] public RewardPeriod Day = new RewardPeriod();
        [JsonProperty("week")] public RewardPeriod Week = new RewardPeriod();
        /// <summary>Obsolete since Bug List 5 (the star cards and region rewards are gone): old claimed ids stay here untouched and are ignored.</summary>
        [JsonProperty("mainClaimed")] public List<string> MainClaimed = new List<string>();
        /// <summary>Ids of the region goals already claimed (g_region_type_tier; claimed once, never reset). Additive field, no version bump.</summary>
        [JsonProperty("goalsClaimed")] public List<string> GoalsClaimed = new List<string>();
        /// <summary>Per-region activity counters, key = region index ("1" ... "5"). Additive field: old saves start every counter at 0 (stars, clears and best endless floor are read from the existing save fields).</summary>
        [JsonProperty("regions")] public Dictionary<string, RegionStats> Regions = new Dictionary<string, RegionStats>();
        /// <summary>Best combo chain ever reached (lifetime).</summary>
        [JsonProperty("bestCombo")] public int BestCombo;
        /// <summary>Claims made so far: seeds the random power-up picks so they are repeatable.</summary>
        [JsonProperty("claims")] public int Claims;
        /// <summary>Admin "Main rewards" test switch (docs/38): every Main reward counts as complete (not claimed, nothing paid). Cleared when System Test goes off.</summary>
        [JsonProperty("testMainReady", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool TestMainReady;
    }

    public enum RewardClaimResult
    {
        Ok,
        NotReady,
        AlreadyClaimed,
        Invalid
    }

    /// <summary>What a claim paid. PowerUpIds has one entry per unit granted (the same type may repeat).</summary>
    public sealed class RewardClaimOutcome
    {
        public RewardClaimResult Result;
        public int Gems, Coins;
        public readonly List<string> PowerUpIds = new List<string>();
        /// <summary>Hero copies granted (one entry per copy: the id of the Common hero). Empty for most rewards.</summary>
        public readonly List<string> HeroIds = new List<string>();
    }

    /// <summary>
    /// Rewards (docs/26 section 4): per-day and per-week counters fed from play, the Main, Daily and Weekly reward tables, atomic claims.
    /// Days and weeks use the LOCAL clock (<see cref="Clock"/>, injectable for tests); a clock that goes backwards never resets or
    /// re-opens a period. Every claim changes the wallet, the power-up stock and the claimed list in one step and saves once.
    /// </summary>
    public sealed partial class ProgressService
    {
        RewardCatalog rewardDefs;

        /// <summary>The reward tables (built-in copy of Content/economy/rewards.json; tests and remote overrides may replace it).</summary>
        public RewardCatalog RewardDefs
        {
            get { return rewardDefs ?? (rewardDefs = RewardCatalog.BuiltIn()); }
            set { rewardDefs = value; }
        }

        /// <summary>Local time source. Null means DateTime.Now. Tests set it to move across midnight and Monday.</summary>
        public Func<DateTime> Clock { get; set; }

        DateTime NowLocal { get { return Clock != null ? Clock() : DateTime.Now; } }

        bool playDirty;
        float playFraction;

        // ---- period keys ----

        static string DayKeyOf(DateTime t) { return t.ToString("yyyyMMdd", CultureInfo.InvariantCulture); }

        /// <summary>Days since Monday: 0 = Monday ... 6 = Sunday.</summary>
        static int DaysSinceMonday(DateTime t) { return ((int)t.DayOfWeek + 6) % 7; }

        static string WeekKeyOf(DateTime t) { return DayKeyOf(t.Date.AddDays(-DaysSinceMonday(t))); }

        /// <summary>Time left until the next local midnight.</summary>
        public TimeSpan DailyResetIn()
        {
            DateTime now = NowLocal;
            return now.Date.AddDays(1) - now;
        }

        /// <summary>Time left until the next Monday 00:00 local time.</summary>
        public TimeSpan WeeklyResetIn()
        {
            DateTime now = NowLocal;
            return now.Date.AddDays(7 - DaysSinceMonday(now)) - now;
        }

        // ---- save access ----

        RewardsData Rw
        {
            get
            {
                RewardsData r = Data.Rewards;
                if (r == null) r = Data.Rewards = new RewardsData();
                if (r.Day == null) r.Day = new RewardPeriod();
                if (r.Week == null) r.Week = new RewardPeriod();
                if (r.Day.Stats == null) r.Day.Stats = new Dictionary<string, int>();
                if (r.Week.Stats == null) r.Week.Stats = new Dictionary<string, int>();
                if (r.Day.Claimed == null) r.Day.Claimed = new List<string>();
                if (r.Week.Claimed == null) r.Week.Claimed = new List<string>();
                if (r.Day.Key == null) r.Day.Key = "";
                if (r.Week.Key == null) r.Week.Key = "";
                if (r.MainClaimed == null) r.MainClaimed = new List<string>();
                if (r.GoalsClaimed == null) r.GoalsClaimed = new List<string>();
                if (r.Regions == null) r.Regions = new Dictionary<string, RegionStats>();
                return r;
            }
        }

        /// <summary>Starts a new day or week when the local date moved forward (counters and claims of the old period are dropped). Returns true when something changed.</summary>
        bool RolloverRewards()
        {
            RewardsData r = Rw;
            DateTime now = NowLocal;
            bool changed = false;
            string dk = DayKeyOf(now), wk = WeekKeyOf(now);
            if (string.CompareOrdinal(dk, r.Day.Key) > 0) { r.Day = new RewardPeriod { Key = dk }; changed = true; }
            if (string.CompareOrdinal(wk, r.Week.Key) > 0) { r.Week = new RewardPeriod { Key = wk }; changed = true; }
            return changed;
        }

        static int StatOf(RewardPeriod p, string key)
        {
            int v;
            return p.Stats.TryGetValue(key, out v) ? v : 0;
        }

        static void AddStat(RewardPeriod p, string key, int n)
        {
            if (n <= 0) return;
            p.Stats[key] = (int)Math.Min((long)StatOf(p, key) + n, int.MaxValue);
        }

        static void MaxStat(RewardPeriod p, string key, int n)
        {
            if (n > StatOf(p, key)) p.Stats[key] = n;
        }

        void AddBoth(string key, int n)
        {
            RewardsData r = Rw;
            AddStat(r.Day, key, n);
            AddStat(r.Week, key, n);
        }

        // ---- recording ----

        /// <summary>Records today's login (idempotent: saves only when the day or week had no login yet). Call when the app or the menu opens.</summary>
        public void RecordLogin()
        {
            bool changed = RolloverRewards();
            RewardsData r = Rw;
            if (StatOf(r.Day, RewardStat.Login) == 0) { r.Day.Stats[RewardStat.Login] = 1; changed = true; }
            int bit = 1 << DaysSinceMonday(NowLocal);
            int mask = StatOf(r.Week, RewardStat.LoginMask);
            if ((mask & bit) == 0) { r.Week.Stats[RewardStat.LoginMask] = mask | bit; changed = true; }
            if (changed) Commit();
        }

        /// <summary>
        /// Adds play time (seconds) to today and this week. Never writes a file by itself unless <paramref name="save"/> is true and a whole
        /// minute was crossed (menus). In a run pass false: the time reaches the disk with the next save or <see cref="FlushPlayTime"/>.
        /// </summary>
        public void AddPlaySeconds(int seconds, bool save = false, int regionIndex = 0)
        {
            if (seconds <= 0) return;
            RolloverRewards();
            RewardsData r = Rw;
            int before = StatOf(r.Day, RewardStat.PlaySeconds);
            AddBoth(RewardStat.PlaySeconds, seconds);
            if (regionIndex > 0) { RegionStats rs = RegionStatsFor(regionIndex, true); rs.PlaySeconds = SaturatingAdd(rs.PlaySeconds, seconds); }   // only play inside a region counts for its goals
            playDirty = true;
            if (save && before / 60 != StatOf(r.Day, RewardStat.PlaySeconds) / 60) FlushPlayTime();
        }

        /// <summary>Frame-time feed (named apart from the bundle timer TickPlayTime) for <see cref="AddPlaySeconds"/>: collects fractions and passes whole seconds on every 5 seconds or so.</summary>
        public void FeedPlayTime(float deltaSeconds, bool save, int regionIndex = 0)
        {
            if (deltaSeconds <= 0f) return;
            if (deltaSeconds > 1f) deltaSeconds = 1f;   // a long frame (resume from the background) is not play time
            playFraction += deltaSeconds;
            if (playFraction < 5f) return;
            int whole = (int)playFraction;
            playFraction -= whole;
            AddPlaySeconds(whole, save, regionIndex);
        }

        /// <summary>Writes pending play time to the save (no Changed event). Call when a run pauses, ends or is left.</summary>
        public void FlushPlayTime()
        {
            if (!playDirty) return;
            playDirty = false;
            store.Save(Data);
        }

        /// <summary>A campaign level was started on this difficulty setting (3 or more counts as a hard level). Saves when it counted.</summary>
        public void RecordLevelStarted(int difficultySetting)
        {
            startedDifficulty = difficultySetting;   // the clear of this level counts as a hard clear when it was started on Hard or Insane
            if (difficultySetting < RewardStat.HardDifficulty) return;
            RolloverRewards();
            if (StatOf(Rw.Day, RewardStat.Hard) > 0) return;   // already counted today: no save at every retry
            AddBoth(RewardStat.Hard, 1);
            Commit();
        }

        /// <summary>
        /// Run statistics that do not need a completed level: combo chains, best combo and power-ups used (the new ones since the last report,
        /// see RunSession.BuildOutcome). Call when a run fails or is left. Saves only when something counted.
        /// </summary>
        public void RecordRunStats(RunOutcome run)
        {
            RolloverRewards();
            if (ApplyRunDelta(run, run.Region)) Commit();
        }

        /// <summary>Difficulty setting of the campaign level started last (RecordLevelStarted); a clear reads it to count hard clears. Not saved.</summary>
        int startedDifficulty;

        /// <summary>The counters of one region (null when none exists and <paramref name="create"/> is false, or the index is not a region).</summary>
        RegionStats RegionStatsFor(int regionIndex, bool create)
        {
            if (regionIndex < 1 || regionIndex > RegionGoalTable.TotalRegions) return null;
            string key = RegionKey(regionIndex);
            RegionStats s;
            if (Rw.Regions.TryGetValue(key, out s) && s != null) return s;
            if (!create) return null;
            s = new RegionStats();
            Rw.Regions[key] = s;
            return s;
        }

        static readonly string[] regionKeys = BuildRegionKeys();
        static string[] BuildRegionKeys()
        {
            var k = new string[RegionGoalTable.TotalRegions + 1];
            for (int i = 0; i < k.Length; i++) k[i] = i.ToString(CultureInfo.InvariantCulture);
            return k;
        }
        static string RegionKey(int regionIndex) { return regionKeys[regionIndex]; }   // cached: no string building in the per-5-seconds play feed

        /// <summary>Run statistics of one region (Main goals): jumps, combo chains and power-ups used since the last report. Returns true when something counted.</summary>
        bool ApplyRegionDelta(RunOutcome run, int regionIndex)
        {
            if (regionIndex < 1 || (run.Jumps <= 0 && run.ComboChains <= 0 && run.PowerUpsUsed <= 0)) return false;
            RegionStats s = RegionStatsFor(regionIndex, true);
            if (s == null) return false;
            if (run.Jumps > 0) s.Jumps = SaturatingAdd(s.Jumps, run.Jumps);
            if (run.ComboChains > 0) s.Combos = SaturatingAdd(s.Combos, run.ComboChains);
            if (run.PowerUpsUsed > 0) s.PowerUps = SaturatingAdd(s.PowerUps, run.PowerUpsUsed);
            return true;
        }

        bool ApplyRunDelta(RunOutcome run, int regionIndex)
        {
            RewardsData r = Rw;
            bool any = ApplyRegionDelta(run, regionIndex);
            if (run.ComboChains > 0) { AddBoth(RewardStat.Combos, run.ComboChains); any = true; }
            if (run.BestCombo > 0)
            {
                MaxStat(r.Day, RewardStat.BestCombo, run.BestCombo);
                MaxStat(r.Week, RewardStat.BestCombo, run.BestCombo);
                if (run.BestCombo > r.BestCombo) { r.BestCombo = run.BestCombo; any = true; }
            }
            if (run.PowerUpsUsed > 0) { AddBoth(RewardStat.PowerUps, run.PowerUpsUsed); any = true; }
            return any;
        }

        /// <summary>Called by ApplyRun for a completed run, before its save: the completion counters plus the run statistics.</summary>
        void TrackCompletedRun(LevelDef level, RunOutcome run, bool firstClear, int coinsPaid = 0)
        {
            RolloverRewards();
            int regionIndex = repo.GetRegion(level.Region).Index;   // a completed run always counts for the level's own region
            ApplyRunDelta(run, regionIndex);
            if (coinsPaid > 0 && regionIndex > 0) { RegionStats rs = RegionStatsFor(regionIndex, true); if (rs != null) rs.Coins = SaturatingAdd(rs.Coins, coinsPaid); }
            if (!firstClear)
            {
                // a replay of a cleared level: the level counters take at most 3 such replays per day, across all levels
                RewardPeriod day = Rw.Day;
                if (StatOf(day, RewardStat.Replays) >= RewardStat.MaxCountedReplaysPerDay) return;   // replays beyond the daily limit count nothing
                day.Stats[RewardStat.Replays] = StatOf(day, RewardStat.Replays) + 1;
            }
            AddBoth(RewardStat.Levels, 1);
            if (startedDifficulty >= RewardStat.HardDifficulty)
            {
                AddBoth(RewardStat.HardClear, 1);   // daily "clear a hard level", weekly "clear 5 hard levels"
                if (regionIndex > 0) { RegionStats hs = RegionStatsFor(regionIndex, true); if (hs != null) hs.HardClears = SaturatingAdd(hs.HardClears, 1); }
            }
            if (level.Index == RegionGoalTable.NoWallLevelIndex) AddBoth(RewardStat.NoWall, 1);
            if (level.Index == 8 || level.Index == 15) AddBoth(RewardStat.GemLevel, 1);
            int stars = run.Stars;   // star pickups are never stripped on replays, so these two counters work on every level
            if (stars >= 2) AddBoth(RewardStat.Stars2, 1);
            if (stars > 0) AddBoth(RewardStat.StarsEarned, stars);
            if ((run.StarMask & 7) == 7) AddBoth(RewardStat.Stars3, 1);
        }

        // ---- reading ----

        /// <summary>Value of a counter for the Daily (today) or Weekly (this week) tab. Derived counters: weekly login days and play minutes.</summary>
        public int RewardStatValue(RewardTab tab, string stat)
        {
            RolloverRewards();
            RewardsData r = Rw;
            RewardPeriod p = tab == RewardTab.Weekly ? r.Week : r.Day;
            if (stat == RewardStat.LoginDays)
            {
                int n = 0;
                for (int m = StatOf(r.Week, RewardStat.LoginMask) & 127; m != 0; m >>= 1) n += m & 1;
                return n;
            }
            if (stat == RewardStat.PlayMinutes) return StatOf(p, RewardStat.PlaySeconds) / 60;
            return StatOf(p, stat);
        }

        /// <summary>Best combo chain ever reached.</summary>
        public int BestComboEver { get { return Rw.BestCombo; } }

        /// <summary>Points earned today: the points of every daily action that is done.</summary>
        public int DailyPoints()
        {
            int points = 0;
            foreach (RewardEntry a in RewardDefs.DailyActions) if (RewardProgress(RewardTab.Daily, a) >= a.Target) points += a.Points;
            return points;
        }

        /// <summary>Levels of the region with this index that are cleared.</summary>
        public int RegionLevelsCleared(int regionIndex)
        {
            RegionDef region = repo.TryGetRegionByIndex(regionIndex);
            if (region == null) return 0;
            int n = 0;
            foreach (string id in region.Levels) if (IsLevelCleared(id)) n++;
            return n;
        }

        /// <summary>The value an entry needs to be complete: its target.</summary>
        public int RewardTarget(RewardTab tab, RewardEntry e)
        {
            return e.Target;
        }

        /// <summary>Current progress of an entry, comparable with <see cref="RewardTarget"/>.</summary>
        public int RewardProgress(RewardTab tab, RewardEntry e)
        {
            if (e == null) return 0;
            if (tab == RewardTab.Main) return MainProgress(e);
            if (e.Stat == null) return DailyPoints();   // a daily points milestone
            return RewardStatValue(tab, e.Stat);
        }

        /// <summary>True when the entry (a claimable reward, not a daily action) was claimed in its period.</summary>
        public bool IsRewardClaimed(RewardTab tab, RewardEntry e)
        {
            if (e == null) return false;
            RolloverRewards();
            RewardsData r = Rw;
            if (tab == RewardTab.Main) return r.GoalsClaimed.Contains(e.Id);
            return (tab == RewardTab.Daily ? r.Day : r.Week).Claimed.Contains(e.Id);
        }

        /// <summary>True when the entry is complete and not claimed yet.</summary>
        public bool IsRewardReady(RewardTab tab, RewardEntry e)
        {
            if (e == null || IsRewardClaimed(tab, e) || RewardProgress(tab, e) < RewardTarget(tab, e)) return false;
            return tab != RewardTab.Main || GoalsOpen(e);
        }

        /// <summary>True when something in the tab can be claimed (drives the red dot on the tab).</summary>
        public bool HasClaimable(RewardTab tab)
        {
            foreach (RewardEntry e in ClaimableEntries(tab)) if (IsRewardReady(tab, e)) return true;
            return false;
        }

        /// <summary>True when any tab has something to claim (drives the red dot on the home Rewards button).</summary>
        public bool HasClaimable()
        {
            return HasClaimable(RewardTab.Main) || HasClaimable(RewardTab.Daily) || HasClaimable(RewardTab.Weekly);
        }

        /// <summary>The claimable rows of a tab (daily actions are progress only, not claimable).</summary>
        public IEnumerable<RewardEntry> ClaimableEntries(RewardTab tab)
        {
            RewardCatalog defs = RewardDefs;
            if (tab == RewardTab.Main)
            {
                foreach (RegionGoal g in defs.AllRegionGoals) yield return g;
            }
            else if (tab == RewardTab.Daily)
            {
                foreach (RewardEntry e in defs.DailyMilestones) yield return e;
            }
            else
            {
                foreach (RewardEntry e in defs.WeeklyTasks) yield return e;
            }
        }

        // ---- claiming ----

        static int StableHash(string s)
        {
            unchecked
            {
                int h = 17;
                if (s != null) foreach (char c in s) h = h * 31 + c;
                return h;
            }
        }

        /// <summary>
        /// Claims one reward. Atomic: the wallet, the power-up stock and the claimed mark change together and are saved once; any failure
        /// (unknown id, not complete, already claimed) changes nothing. Power-up units are random from the implemented types.
        /// </summary>
        public RewardClaimOutcome TryClaimReward(RewardTab tab, string id)
        {
            var outcome = new RewardClaimOutcome();
            RewardEntry e = RewardDefs.Find(tab, id);
            if (e == null) { outcome.Result = RewardClaimResult.Invalid; return outcome; }
            RolloverRewards();
            if (IsRewardClaimed(tab, e)) { outcome.Result = RewardClaimResult.AlreadyClaimed; return outcome; }
            if (RewardProgress(tab, e) < RewardTarget(tab, e) || (tab == RewardTab.Main && !GoalsOpen(e))) { outcome.Result = RewardClaimResult.NotReady; return outcome; }
            PayReward(tab, e, outcome);
            outcome.Result = RewardClaimResult.Ok;
            FinishClaim(outcome);
            return outcome;
        }

        /// <summary>
        /// Claims everything that is ready in a tab in one step: all rewards are paid and marked together and saved once (atomic). The outcome adds
        /// up gems, coins and power-up units; Result is NotReady when nothing was ready.
        /// </summary>
        public RewardClaimOutcome ClaimAll(RewardTab tab)
        {
            var outcome = new RewardClaimOutcome { Result = RewardClaimResult.NotReady };
            RolloverRewards();
            var ready = new List<RewardEntry>();
            foreach (RewardEntry e in ClaimableEntries(tab)) if (IsRewardReady(tab, e)) ready.Add(e);
            if (ready.Count == 0) return outcome;
            foreach (RewardEntry e in ready) PayReward(tab, e, outcome);
            outcome.Result = RewardClaimResult.Ok;
            FinishClaim(outcome);
            return outcome;
        }

        /// <summary>Saves a finished claim once. Hero copies go through AddCopies (the existing API: it saves), after the wallet and the claimed marks are already set, so that one save holds everything.</summary>
        void FinishClaim(RewardClaimOutcome outcome)
        {
            if (outcome.HeroIds.Count == 0) { Commit(); return; }
            for (int i = 0; i < outcome.HeroIds.Count; i++) AddCopies(outcome.HeroIds[i], 1);
        }

        /// <summary>The starter Common hero of a region (the one the region hands over, Momo for region 1), null when none exists.</summary>
        static CharacterSpec CommonHeroOf(int regionIndex)
        {
            foreach (CharacterSpec c in CharacterDefs.All) if (c.RegionStarter && c.Region == regionIndex) return c;
            return null;
        }

        /// <summary>
        /// One random Common hero of an OPEN region (region 1, or a region the player unlocked): repeatable from the per-account seed (the bundle seed) and the claim
        /// counter, never UnityEngine.Random. Null when no such hero exists.
        /// </summary>
        CharacterSpec RandomOpenCommonHero()
        {
            var open = new List<CharacterSpec>(5);
            foreach (CharacterSpec c in CharacterDefs.All)
            {
                if (!c.IsRegionHero) continue;
                RegionDef rd = c.Region > 1 ? repo.TryGetRegionByIndex(c.Region) : null;
                if (c.Region <= 1 || (rd != null && IsRegionUnlocked(rd))) open.Add(c);
            }
            if (open.Count == 0) return null;
            BundleSaveData b = Data.Bundles ?? (Data.Bundles = new BundleSaveData());
            EnsureBundleSeed(b);
            ulong roll = BundleMix(((ulong)(uint)b.Seed << 32) ^ (ulong)(uint)Rw.Claims ^ 0x4845524FUL);
            return open[(int)(roll % (ulong)open.Count)];
        }

        /// <summary>Pays one reward into the wallet and the power-up stock and marks it claimed, without saving (the caller saves once).</summary>
        void PayReward(RewardTab tab, RewardEntry e, RewardClaimOutcome outcome)
        {
            RewardsData r = Rw;
            Data.Gems = SaturatingAdd(Data.Gems, Math.Max(0, e.Gems));
            Data.Coins = SaturatingAdd(Data.Coins, Math.Max(0, e.Coins));
            outcome.Gems = SaturatingAdd(outcome.Gems, Math.Max(0, e.Gems));
            outcome.Coins = SaturatingAdd(outcome.Coins, Math.Max(0, e.Coins));
            if (e.PowerUps > 0)
            {
                var types = new List<PowerUpDef>(PowerUpDefs.Implemented);
                if (types.Count > 0)
                {
                    unchecked
                    {
                        var rng = new Rng(StableHash(e.Id) ^ ((r.Claims + 1) * (int)0x9E3779B1) ^ StableHash(r.Day.Key));
                        for (int i = 0; i < e.PowerUps; i++)
                        {
                            string pid = types[rng.Next(types.Count)].Id;
                            Data.Inventory.Powerups[pid] = SaturatingAdd(PowerUpCount(pid), 1);
                            outcome.PowerUpIds.Add(pid);
                        }
                    }
                }
            }
            if (e.HeroRegion != 0 || !string.IsNullOrEmpty(e.HeroId))
            {
                CharacterSpec hero = !string.IsNullOrEmpty(e.HeroId) ? CharacterDefs.Get(e.HeroId) : e.HeroRegion > 0 ? CommonHeroOf(e.HeroRegion) : RandomOpenCommonHero();
                if (hero != null) outcome.HeroIds.Add(hero.Id);   // granted by FinishClaim
            }
            if (tab == RewardTab.Main) r.GoalsClaimed.Add(e.Id);
            else if (tab == RewardTab.Daily) r.Day.Claimed.Add(e.Id);
            else r.Week.Claimed.Add(e.Id);
            r.Claims++;
        }
    }
}
