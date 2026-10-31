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

    /// <summary>Everything the Rewards screen needs from the save (docs/26 section 4). Additive part of SaveData, no version bump.</summary>
    public sealed class RewardsData
    {
        [JsonProperty("day")] public RewardPeriod Day = new RewardPeriod();
        [JsonProperty("week")] public RewardPeriod Week = new RewardPeriod();
        /// <summary>Star and region rewards already claimed (claimed once, never reset).</summary>
        [JsonProperty("mainClaimed")] public List<string> MainClaimed = new List<string>();
        /// <summary>Best combo chain ever reached (lifetime).</summary>
        [JsonProperty("bestCombo")] public int BestCombo;
        /// <summary>Claims made so far: seeds the random power-up picks so they are repeatable.</summary>
        [JsonProperty("claims")] public int Claims;
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
        public void AddPlaySeconds(int seconds, bool save = false)
        {
            if (seconds <= 0) return;
            RolloverRewards();
            RewardsData r = Rw;
            int before = StatOf(r.Day, RewardStat.PlaySeconds);
            AddBoth(RewardStat.PlaySeconds, seconds);
            playDirty = true;
            if (save && before / 60 != StatOf(r.Day, RewardStat.PlaySeconds) / 60) FlushPlayTime();
        }

        /// <summary>Frame-time feed (named apart from the bundle timer TickPlayTime) for <see cref="AddPlaySeconds"/>: collects fractions and passes whole seconds on every 5 seconds or so.</summary>
        public void FeedPlayTime(float deltaSeconds, bool save)
        {
            if (deltaSeconds <= 0f) return;
            if (deltaSeconds > 1f) deltaSeconds = 1f;   // a long frame (resume from the background) is not play time
            playFraction += deltaSeconds;
            if (playFraction < 5f) return;
            int whole = (int)playFraction;
            playFraction -= whole;
            AddPlaySeconds(whole, save);
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
            if (ApplyRunDelta(run)) Commit();
        }

        bool ApplyRunDelta(RunOutcome run)
        {
            RewardsData r = Rw;
            bool any = false;
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
        void TrackCompletedRun(LevelDef level, RunOutcome run, bool firstClear)
        {
            RolloverRewards();
            ApplyRunDelta(run);
            if (!firstClear)
            {
                // a replay of a cleared level: the level counters take at most 3 such replays per day, across all levels
                RewardPeriod day = Rw.Day;
                if (StatOf(day, RewardStat.Replays) >= RewardStat.MaxCountedReplaysPerDay) return;   // gems collected on replays are not a thing (pickups are stripped), so nothing else is lost
                day.Stats[RewardStat.Replays] = StatOf(day, RewardStat.Replays) + 1;
            }
            AddBoth(RewardStat.Levels, 1);
            if (level.Index == 7) AddBoth(RewardStat.NoWall, 1);
            if (level.Index == 8 || level.Index == 15) AddBoth(RewardStat.GemLevel, 1);
            if (run.GemPickupsTotal > 0 && run.GemPickupsTaken >= run.GemPickupsTotal) AddBoth(RewardStat.AllGems, 1);
            AddBoth(RewardStat.Gems, Math.Max(0, run.GemsCollected) + Math.Max(0, run.HiddenGems));
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

        bool IsRegionEntry(RewardEntry e)
        {
            foreach (RewardEntry r in RewardDefs.RegionRewards) if (ReferenceEquals(r, e)) return true;
            return false;
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

        /// <summary>The value an entry needs to be complete: its target, or all levels of the region for a region reward.</summary>
        public int RewardTarget(RewardTab tab, RewardEntry e)
        {
            if (tab == RewardTab.Main && IsRegionEntry(e))
            {
                RegionDef region = repo.TryGetRegionByIndex(e.Target);
                return region != null ? region.Levels.Count : int.MaxValue;
            }
            return e.Target;
        }

        /// <summary>Current progress of an entry, comparable with <see cref="RewardTarget"/>.</summary>
        public int RewardProgress(RewardTab tab, RewardEntry e)
        {
            if (e == null) return 0;
            if (tab == RewardTab.Main) return IsRegionEntry(e) ? RegionLevelsCleared(e.Target) : TotalStars();
            if (e.Stat == null) return DailyPoints();   // a daily points milestone
            return RewardStatValue(tab, e.Stat);
        }

        /// <summary>True when the entry (a claimable reward, not a daily action) was claimed in its period.</summary>
        public bool IsRewardClaimed(RewardTab tab, RewardEntry e)
        {
            if (e == null) return false;
            RolloverRewards();
            RewardsData r = Rw;
            if (tab == RewardTab.Main) return r.MainClaimed.Contains(e.Id);
            return (tab == RewardTab.Daily ? r.Day : r.Week).Claimed.Contains(e.Id);
        }

        /// <summary>True when the entry is complete and not claimed yet.</summary>
        public bool IsRewardReady(RewardTab tab, RewardEntry e)
        {
            return e != null && !IsRewardClaimed(tab, e) && RewardProgress(tab, e) >= RewardTarget(tab, e);
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
                foreach (RewardEntry e in defs.StarMilestones) yield return e;
                foreach (RewardEntry e in defs.RegionRewards) yield return e;
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
            if (RewardProgress(tab, e) < RewardTarget(tab, e)) { outcome.Result = RewardClaimResult.NotReady; return outcome; }

            RewardsData r = Rw;
            Data.Gems = SaturatingAdd(Data.Gems, Math.Max(0, e.Gems));
            Data.Coins = SaturatingAdd(Data.Coins, Math.Max(0, e.Coins));
            outcome.Gems = Math.Max(0, e.Gems);
            outcome.Coins = Math.Max(0, e.Coins);
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
            if (tab == RewardTab.Main) r.MainClaimed.Add(e.Id);
            else if (tab == RewardTab.Daily) r.Day.Claimed.Add(e.Id);
            else r.Week.Claimed.Add(e.Id);
            r.Claims++;
            outcome.Result = RewardClaimResult.Ok;
            Commit();
            return outcome;
        }
    }
}
