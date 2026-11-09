using System;
using System.Collections.Generic;
using BoingUpSkyTower.Content;

namespace BoingUpSkyTower.Economy
{
    /// <summary>
    /// Timed records (doc 46): best Pace and Time per level and Gameplay mode, medals, endless and Time Attack records, and the counters of
    /// the timed reward goals. The rules themselves are pure (<see cref="RecordRules"/>); this part finds the records in the save, applies
    /// them and saves at once. Revived runs never set a best Time or Pace (they still get their numbers back for the result screen).
    /// </summary>
    public sealed partial class ProgressService
    {
        static readonly int[] NoCurve = new int[0];

        /// <summary>Saves written before the timed records (or with null fields) get empty containers.</summary>
        void EnsureTimedFields()
        {
            if (Data.EndlessRecords == null) Data.EndlessRecords = new Dictionary<string, EndlessModeRecord[]>();
            if (Data.TimeAttackRecords == null) Data.TimeAttackRecords = new Dictionary<string, TimeAttackRecord[]>();
            if (Data.RegionTimed == null) Data.RegionTimed = new Dictionary<string, RegionTimedRecord>();
            if (Data.TimedCounters == null) Data.TimedCounters = new TimedCounters();
            if (Data.TimedCounters.BestPaceLevelsWeek == null) Data.TimedCounters.BestPaceLevelsWeek = new List<string>();
        }

        // ---- queries for the UI (level popup, result screen, run HUD) ----

        /// <summary>The stored record of a level and mode, or null (do not change it; use RecordRun).</summary>
        public ModeRecord GetModeRecord(string levelId, int mode)
        {
            LevelProgress p = levelId == null ? null : GetProgress(levelId);
            if (p == null || p.Records == null || !TimeRecords.ValidMode(mode) || mode >= p.Records.Length) return null;
            return p.Records[mode];
        }

        /// <summary>Best Pace of a level in a Gameplay mode (floors / ticks; IsValid is false when there is no record yet).</summary>
        public PaceValue BestPace(string levelId, int mode)
        {
            ModeRecord r = GetModeRecord(levelId, mode);
            return r == null ? new PaceValue(0, 0) : r.Pace;
        }

        /// <summary>Best Time in ticks (shortest ever, kept apart from the best Pace), 0 = none.</summary>
        public int BestTime(string levelId, int mode)
        {
            ModeRecord r = GetModeRecord(levelId, mode);
            return r == null ? 0 : r.BestTimeTicks;
        }

        /// <summary>Floor-by-time curve of the best-Pace run (floors at the end of each second); an empty array when there is none. Do not modify it.</summary>
        public int[] BestCurve(string levelId, int mode)
        {
            ModeRecord r = GetModeRecord(levelId, mode);
            return r == null || r.Curve == null ? NoCurve : r.Curve;
        }

        /// <summary>The medal of a level and mode: the best ever earned, or what the best Pace earns against the current target data.</summary>
        public MedalLevel Medal(string levelId, int mode)
        {
            ModeRecord r = GetModeRecord(levelId, mode);
            return r == null ? MedalLevel.None : RecordRules.MedalOf(r, MedalTargets.Lookup(levelId, mode));
        }

        /// <summary>The best medal of a level over the five modes (for the level map badge).</summary>
        public MedalLevel BestMedal(string levelId)
        {
            MedalLevel best = MedalLevel.None;
            for (int m = 0; m < TimeRecords.ModeCount; m++) { MedalLevel x = Medal(levelId, m); if (x > best) best = x; }
            return best;
        }

        /// <summary>Gold medals in a region, counted over its levels AND the five modes (the Main "gold medals on N levels" line: 5 to 25 of 75 possible).</summary>
        public int GoldMedalsInRegion(int regionIndex)
        {
            RegionDef def = repo.TryGetRegionByIndex(regionIndex);
            if (def == null) return 0;
            int n = 0;
            foreach (string id in def.Levels)
                for (int m = 0; m < TimeRecords.ModeCount; m++)
                    if (Medal(id, m) == MedalLevel.Gold) n++;
            return n;
        }

        /// <summary>Gold medals in every region.</summary>
        public int GoldMedalsTotal()
        {
            int n = 0;
            foreach (RegionDef r in repo.Regions) n += GoldMedalsInRegion(r.Index);
            return n;
        }

        // ---- the entry point ----

        /// <summary>
        /// Applies a finished run to the records and saves at once. Level: call when a level run completed (together with ApplyRun). Endless: when the
        /// run ended (together with ApplyEndlessRun; set run.Coins and run.Gems for the per-minute captures). TimeAttack: when the 3:00 run ended.
        /// A revived run changes nothing and only returns its own numbers. Records only ever improve.
        /// </summary>
        public RecordResult RecordRun(RunRecord run)
        {
            if (run == null) throw new ArgumentNullException("run");
            EnsureTimedFields();
            run.Mode = TimeRecords.ClampMode(run.Mode);
            RecordResult res;
            switch (run.Kind)
            {
                case RunKind.Endless: res = RecordEndless(run); break;
                case RunKind.TimeAttack: res = RecordTimeAttack(run); break;
                default: res = RecordLevel(run); break;
            }
            if (res.Counted) Commit();   // a run that sets nothing has nothing to save
            return res;
        }

        RecordResult RecordLevel(RunRecord run)
        {
            LevelDef level = repo.GetLevel(run.LevelId);   // throws for an unknown id
            LevelProgress p;
            if (!Data.Levels.TryGetValue(level.Id, out p) || p == null) { p = new LevelProgress(); }
            ModeRecord rec = p.Records != null && run.Mode < p.Records.Length ? p.Records[run.Mode] : null;
            if (rec == null) rec = new ModeRecord { Mode = run.Mode };
            RecordResult res = RecordRules.ApplyLevel(rec, run, MedalTargets.Lookup(level.Id, run.Mode));
            if (res.Counted)
            {
                if (p.Records == null || p.Records.Length != TimeRecords.ModeCount)
                {
                    var fresh = new ModeRecord[TimeRecords.ModeCount];
                    if (p.Records != null) Array.Copy(p.Records, fresh, Math.Min(p.Records.Length, fresh.Length));
                    p.Records = fresh;
                }
                p.Records[run.Mode] = rec;
                Data.Levels[level.Id] = p;
                TrackTimedLevel(level.Id, res);
            }
            return res;
        }

        RecordResult RecordEndless(RunRecord run)
        {
            RegionDef region = repo.GetRegion(run.RegionId);
            EndlessModeRecord[] arr = ModeArray(Data.EndlessRecords, region.Id);
            EndlessModeRecord rec = arr[run.Mode] ?? new EndlessModeRecord();
            RecordResult res = RecordRules.ApplyEndless(rec, run);
            if (res.Counted)
            {
                arr[run.Mode] = rec;
                RegionTimedRecord t;
                if (!Data.RegionTimed.TryGetValue(region.Id, out t) || t == null) t = new RegionTimedRecord();
                RecordRules.ApplyCaptures(t, run, res);
                Data.RegionTimed[region.Id] = t;
                if (run.Splits != null && run.Splits.Length > 0 && run.Splits[0] > 0)
                {
                    TimedCounters c = TimedNow();
                    if (c.Split100TodayTicks <= 0 || run.Splits[0] < c.Split100TodayTicks) c.Split100TodayTicks = run.Splits[0];
                }
            }
            return res;
        }

        RecordResult RecordTimeAttack(RunRecord run)
        {
            RegionDef region = repo.GetRegion(run.RegionId);
            TimeAttackRecord[] arr = ModeArray(Data.TimeAttackRecords, region.Id);
            TimeAttackRecord rec = arr[run.Mode] ?? new TimeAttackRecord();
            RecordResult res = RecordRules.ApplyTimeAttack(rec, run);
            if (res.Counted)
            {
                arr[run.Mode] = rec;
                TimedCounters c = TimedNow();
                if (run.Floors > c.TimeAttackFloorsWeek) c.TimeAttackFloorsWeek = run.Floors;
                RegionTimedRecord t;   // coins and gems per minute count in Time Attack too (best single run of the region)
                if (!Data.RegionTimed.TryGetValue(region.Id, out t) || t == null) t = new RegionTimedRecord();
                RecordRules.ApplyCaptures(t, run, res);
                Data.RegionTimed[region.Id] = t;
            }
            return res;
        }

        /// <summary>
        /// One call for the end of an endless or Time Attack run (what the run controller should do; run.Coins arrives as the combo coins of RunSession.BuildRunRecord): pays the coins (endless or Time Attack table and the
        /// shared daily cap), then records the run. <c>run.Kind</c> picks the mode; the capture rate uses the FULL RATE coins (before the over-cap
        /// reduction, so a run past the cap still counts at its true pace of play) and the run's gems. Records are set even when the coins are reduced.
        /// </summary>
        public EndlessReward FinishEndlessRun(RunRecord run, string dayKey, out RecordResult recorded)
        {
            if (run == null) throw new ArgumentNullException("run");
            if (run.Kind == RunKind.Level) throw new ArgumentException("Level runs use ApplyRun and RecordRun.", "run");
            int floors = run.Floors < 0 ? 0 : run.Floors;
            int comboCoins = run.Coins;   // RunSession.BuildRunRecord puts the run's combo coins (SimState.Coins) here
            EndlessReward er = run.Kind == RunKind.TimeAttack
                ? ApplyTimeAttackRun(run.RegionId, floors, dayKey, comboCoins)
                : ApplyEndlessRun(run.RegionId, floors, dayKey, comboCoins);
            run.Coins = er.FullRateCoins;
            recorded = RecordRun(run);
            return er;
        }

        /// <summary>The per-mode array of a region in a dictionary, created (and stored) when missing or of the wrong length.</summary>
        static T[] ModeArray<T>(Dictionary<string, T[]> map, string regionId) where T : class
        {
            T[] arr;
            if (map.TryGetValue(regionId, out arr) && arr != null && arr.Length == TimeRecords.ModeCount) return arr;
            var fresh = new T[TimeRecords.ModeCount];
            if (arr != null) Array.Copy(arr, fresh, Math.Min(arr.Length, fresh.Length));
            map[regionId] = fresh;
            return fresh;
        }

        // ---- endless and Time Attack queries (T3 builds the screens on these) ----

        /// <summary>The endless record of a region and mode, or null.</summary>
        public EndlessModeRecord EndlessRecord(string regionId, int mode)
        {
            EndlessModeRecord[] arr;
            if (regionId == null || Data.EndlessRecords == null || !TimeRecords.ValidMode(mode) || !Data.EndlessRecords.TryGetValue(regionId, out arr) || arr == null || mode >= arr.Length) return null;
            return arr[mode];
        }

        /// <summary>Best endless Pace of a region and mode (IsValid false when none).</summary>
        public PaceValue EndlessBestPace(string regionId, int mode)
        {
            EndlessModeRecord r = EndlessRecord(regionId, mode);
            return r == null ? new PaceValue(0, 0) : r.Pace;
        }

        /// <summary>Longest endless Time survived in ticks (0 = none).</summary>
        public int EndlessBestSurvived(string regionId, int mode)
        {
            EndlessModeRecord r = EndlessRecord(regionId, mode);
            return r == null ? 0 : r.BestSurvivedTicks;
        }

        /// <summary>Best split of a stage: stage 1 = ticks to reach floor 100, stage 2 = floor 200 ... 0 = never reached.</summary>
        public int EndlessBestSplit(string regionId, int mode, int stage)
        {
            EndlessModeRecord r = EndlessRecord(regionId, mode);
            return r == null || r.Splits == null || stage < 1 || stage > r.Splits.Length ? 0 : r.Splits[stage - 1];
        }

        /// <summary>Floor-by-time curve of the best endless Pace run (empty when none).</summary>
        public int[] EndlessBestCurve(string regionId, int mode)
        {
            EndlessModeRecord r = EndlessRecord(regionId, mode);
            return r == null || r.Curve == null ? NoCurve : r.Curve;
        }

        /// <summary>Best floors of a region and mode in endless (the old per-region EndlessBestFloor counts every mode).</summary>
        public int EndlessBestFloorsInMode(string regionId, int mode)
        {
            EndlessModeRecord r = EndlessRecord(regionId, mode);
            return r == null ? 0 : r.BestFloors;
        }

        /// <summary>
        /// All best splits of an endless record: [0] = ticks to reach floor 100, [1] = floor 200 ... 0 = never reached. Empty when none. Do not modify.
        /// Format a split with TimeRecords.FormatTime; the live comparison is <see cref="EndlessSplitDelta"/>.
        /// </summary>
        public int[] EndlessSplits(string regionId, int mode)
        {
            EndlessModeRecord r = EndlessRecord(regionId, mode);
            return r == null || r.Splits == null ? NoCurve : r.Splits;
        }

        /// <summary>
        /// Live split delta for the run HUD: when the run has just reached floor <paramref name="stage"/> x 100 at <paramref name="elapsedTicks"/> on its clock,
        /// the ticks ahead (negative = faster) or behind (positive) of the best split of that stage. False when there is no best split yet.
        /// </summary>
        public bool EndlessSplitDelta(string regionId, int mode, int stage, int elapsedTicks, out int deltaTicks)
        {
            return TimeRecords.TrySplitDelta(EndlessSplits(regionId, mode), stage, elapsedTicks, out deltaTicks);
        }

        /// <summary>The Time Attack record of a region and mode (best floors in 3:00, plus seed, hero, version, inputs and curve of that run), or null when none. Do not modify.</summary>
        public TimeAttackRecord TimeAttackBest(string regionId, int mode)
        {
            TimeAttackRecord[] arr;
            if (regionId == null || Data.TimeAttackRecords == null || !TimeRecords.ValidMode(mode) || !Data.TimeAttackRecords.TryGetValue(regionId, out arr) || arr == null || mode >= arr.Length) return null;
            TimeAttackRecord rec = arr[mode];
            return rec != null && rec.BestFloors > 0 && TimeAttackRamp.IsCurrent(rec) ? rec : null;
        }

        /// <summary>Floor-by-time curve of the best Time Attack run (empty when none).</summary>
        public int[] TimeAttackBestCurve(string regionId, int mode)
        {
            TimeAttackRecord r = TimeAttackBest(regionId, mode);
            return r == null || r.Curve == null ? NoCurve : r.Curve;
        }

        /// <summary>Coins per minute x 100 of an amount over a run clock (live HUD value or a stat); see TimeRecords.RatePerMinute / FormatPerMinute.</summary>
        public static int RatePerMinute(int amount, int ticks) { return TimeRecords.RatePerMinute(amount, ticks); }

        /// <summary>Best floors in a 3:00 Time Attack of a region and mode (0 = none).</summary>
        public int TimeAttackBestFloors(string regionId, int mode)
        {
            TimeAttackRecord[] arr;
            if (regionId == null || Data.TimeAttackRecords == null || !TimeRecords.ValidMode(mode) || !Data.TimeAttackRecords.TryGetValue(regionId, out arr) || arr == null || mode >= arr.Length || arr[mode] == null || !TimeAttackRamp.IsCurrent(arr[mode])) return 0;
            return arr[mode].BestFloors;
        }

        /// <summary>Best coins per minute (x 100) of one run in the region, 0 = none.</summary>
        public int BestCoinsPerMinX100(string regionId)
        {
            RegionTimedRecord t;
            return regionId != null && Data.RegionTimed != null && Data.RegionTimed.TryGetValue(regionId, out t) && t != null ? t.BestCoinsPerMinX100 : 0;
        }

        /// <summary>Best gems per minute (x 100) of one run in the region, 0 = none.</summary>
        public int BestGemsPerMinX100(string regionId)
        {
            RegionTimedRecord t;
            return regionId != null && Data.RegionTimed != null && Data.RegionTimed.TryGetValue(regionId, out t) && t != null ? t.BestGemsPerMinX100 : 0;
        }

        // ---- counters of the timed reward goals (doc 46 section 8; T4 reads them) ----

        /// <summary>Not revived level clears at 0.6 plat/s or more today (local day, like the Daily tab).</summary>
        public int TimedPaceClearsToday() { return TimedNow().PaceClearsToday; }

        /// <summary>Distinct levels with a new best Pace this week.</summary>
        public int TimedBestPaceLevelsThisWeek() { return TimedNow().BestPaceLevelsWeek.Count; }

        /// <summary>Gold medals earned this week.</summary>
        public int TimedGoldThisWeek() { return TimedNow().GoldWeek; }

        /// <summary>Best Time Attack floors this week.</summary>
        public int TimedTimeAttackFloorsThisWeek() { return TimedNow().TimeAttackFloorsWeek; }

        /// <summary>Fastest endless time to floor 100 today in ticks (0 = none); the Daily goal wants at most 10800 (3:00).</summary>
        public int TimedSplit100TodayTicks() { return TimedNow().Split100TodayTicks; }

        /// <summary>The counters of the current day and week; restarted when the day or week changed. The result is the saved object.</summary>
        TimedCounters TimedNow()
        {
            EnsureTimedFields();
            TimedCounters c = Data.TimedCounters;
            RolloverRewards();
            RewardsData r = Rw;
            if (c.DayKey != r.Day.Key) { c.DayKey = r.Day.Key; c.PaceClearsToday = 0; c.Split100TodayTicks = 0; }
            if (c.WeekKey != r.Week.Key) { c.WeekKey = r.Week.Key; c.BestPaceLevelsWeek.Clear(); c.GoldWeek = 0; c.TimeAttackFloorsWeek = 0; }
            return c;
        }

        void TrackTimedLevel(string levelId, RecordResult res)
        {
            TimedCounters c = TimedNow();
            if (res.PaceClear) c.PaceClearsToday = SaturatingAdd(c.PaceClearsToday, 1);
            if (res.NewBestPace && !c.BestPaceLevelsWeek.Contains(levelId)) c.BestPaceLevelsWeek.Add(levelId);
            if (res.NewGold) c.GoldWeek = SaturatingAdd(c.GoldWeek, 1);
        }
    }
}
