using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace FluffyClimbers.Economy
{
    // Save containers of the timed records (doc 46 section 9) and the pure rules that update them.
    // All additive: an old save has no entries (null / empty), nothing is migrated. JsonProperty names are short on purpose.

    /// <summary>
    /// The record of ONE level in ONE Gameplay mode. The pace fields, seed, hero, version, inputs and curve all belong to the best-PACE run;
    /// <see cref="BestTimeTicks"/> is its own best (it can come from another run).
    /// </summary>
    public sealed class ModeRecord
    {
        /// <summary>Floors and ticks of the best-Pace run (0 = no record yet).</summary>
        [JsonProperty("f")] public int Floors;
        [JsonProperty("t")] public int Ticks;
        /// <summary>Shortest Time ever (ticks), 0 = none.</summary>
        [JsonProperty("bt")] public int BestTimeTicks;
        /// <summary>Best medal ever earned (0 none, 1 bronze, 2 silver, 3 gold). It only goes up.</summary>
        [JsonProperty("m")] public int Medal;
        /// <summary>Layout seed (LevelSpec.Seed) and attempt seed of the best-Pace run.</summary>
        [JsonProperty("seed")] public int LayoutSeed;
        [JsonProperty("att")] public int AttemptSeed;
        [JsonProperty("hero")] public string HeroId = "";
        [JsonProperty("mode")] public int Mode;
        [JsonProperty("ver")] public string GameVersion = "";
        /// <summary>Run-length coded per-tick inputs of the best-Pace run (base64 in the JSON), see <see cref="InputCodec"/>.</summary>
        [JsonProperty("in")] public byte[] Inputs = new byte[0];
        /// <summary>Floors reached at the end of each second of the best-Pace run.</summary>
        [JsonProperty("c")] public int[] Curve = new int[0];

        public PaceValue Pace { get { return new PaceValue(Floors, Ticks); } }
        public bool HasPace { get { return Floors > 0 && Ticks > 0; } }
    }

    /// <summary>Endless records of one region in ONE Gameplay mode (doc 46 section 7).</summary>
    public sealed class EndlessModeRecord
    {
        /// <summary>Best floors in this mode (the old per-region EndlessBest stays and counts every mode).</summary>
        [JsonProperty("bf")] public int BestFloors;
        /// <summary>Floors and ticks of the best-Pace run.</summary>
        [JsonProperty("pf")] public int PaceFloors;
        [JsonProperty("pt")] public int PaceTicks;
        /// <summary>Longest survived Time (ticks), 0 = none.</summary>
        [JsonProperty("st")] public int BestSurvivedTicks;
        /// <summary>Best split per 100-floor stage: Splits[0] = ticks to reach floor 100, [1] = floor 200 ... 0 = never reached.</summary>
        [JsonProperty("sp")] public int[] Splits = new int[0];
        /// <summary>Seed, hero, version and floor-by-time curve of the best-Pace run (the curve is capped, see RecordRules.MaxEndlessCurve).</summary>
        [JsonProperty("seed")] public int LayoutSeed;
        [JsonProperty("hero")] public string HeroId = "";
        [JsonProperty("ver")] public string GameVersion = "";
        [JsonProperty("c")] public int[] Curve = new int[0];
        /// <summary>Gameplay mode (key of the record) and run-length coded per-tick inputs of the best-Pace run (<see cref="InputCodec"/>), for the later server check. Empty when not recorded or over <see cref="RecordRules.MaxLongInputBytes"/>.</summary>
        [JsonProperty("mode")] public int Mode;
        [JsonProperty("in")] public byte[] Inputs = new byte[0];

        public PaceValue Pace { get { return new PaceValue(PaceFloors, PaceTicks); } }
    }

    /// <summary>
    /// Time Attack (3:00) record of one region in one Gameplay mode. The score is the floors reached in 3:00; seed, hero, version, inputs and the
    /// floor-by-time curve belong to the run that set <see cref="BestFloors"/> (server check, Pace Ghost).
    /// </summary>
    public sealed class TimeAttackRecord
    {
        [JsonProperty("bf")] public int BestFloors;
        /// <summary>Clock ticks of the best run (10800 for a full run; fewer when the player fell before 3:00).</summary>
        [JsonProperty("t")] public int Ticks;
        [JsonProperty("seed")] public int LayoutSeed;
        [JsonProperty("hero")] public string HeroId = "";
        [JsonProperty("mode")] public int Mode;
        [JsonProperty("ver")] public string GameVersion = "";
        [JsonProperty("in")] public byte[] Inputs = new byte[0];
        [JsonProperty("c")] public int[] Curve = new int[0];
        /// <summary>
        /// Difficulty ramp the best run was played on (owner Bug List 8 item 3): <see cref="TimeAttackRamp.StartLevel"/> (10) = the ramp from campaign level 10.
        /// 0 = a record of the unreleased build that started at floor 1 (levels 1 to 3 difficulty): it is not comparable, so it counts as no record (<see cref="TimeAttackRamp.IsCurrent"/>) and the next counted run replaces it.
        /// Additive field: old saves load with 0.
        /// </summary>
        [JsonProperty("ramp")] public int RampStartLevel;
    }

    /// <summary>Which Time Attack difficulty ramp a stored record belongs to.</summary>
    public static class TimeAttackRamp
    {
        /// <summary>Level whose difficulty floor 1 of a Time Attack run has (doc 46 section 7, EndlessTower.TimeAttackStartLevel).</summary>
        public const int StartLevel = FluffyClimbers.Simulation.EndlessTower.TimeAttackStartLevel;
        /// <summary>True when the record was set on the current ramp (a record without the stamp is a legacy one and does not count).</summary>
        public static bool IsCurrent(TimeAttackRecord rec) { return rec != null && rec.RampStartLevel == StartLevel; }
    }

    /// <summary>Best single-run captures per region (not per mode): coins and gems per minute, both x 100 (hundredths).</summary>
    public sealed class RegionTimedRecord
    {
        [JsonProperty("cpm")] public int BestCoinsPerMinX100;
        [JsonProperty("gpm")] public int BestGemsPerMinX100;
    }

    /// <summary>
    /// Counters for the timed reward goals (doc 46 section 8). They belong to one local day or week and restart when the key changes
    /// (the keys are RewardPeriod.Key of the Daily and Weekly period).
    /// </summary>
    public sealed class TimedCounters
    {
        [JsonProperty("dayKey")] public string DayKey = "";
        /// <summary>Not revived level clears at a Pace of 0.6 plat/s or more today.</summary>
        [JsonProperty("paceClears")] public int PaceClearsToday;
        [JsonProperty("weekKey")] public string WeekKey = "";
        /// <summary>Distinct level ids that got a new best Pace this week.</summary>
        [JsonProperty("bestLevels")] public List<string> BestPaceLevelsWeek = new List<string>();
        /// <summary>Gold medals earned this week (a medal counts when it is first earned in a level and mode).</summary>
        [JsonProperty("goldWeek")] public int GoldWeek;
        /// <summary>Best Time Attack floors this week.</summary>
        [JsonProperty("taWeek")] public int TimeAttackFloorsWeek;
        /// <summary>Fastest endless time to floor 100 today in ticks (0 = not reached today). The Daily goal wants 3:00 or less.</summary>
        [JsonProperty("split100")] public int Split100TodayTicks;
    }

    /// <summary>What <c>ProgressService.RecordRun</c> reports to the result screen (and to the reward goals).</summary>
    public sealed class RecordResult
    {
        public RunKind Kind;
        /// <summary>The numbers of the run itself (also set for a revived run, for the result screen).</summary>
        public int Floors, Ticks, PaceHundredths;
        /// <summary>The run used a revive: nothing below was updated.</summary>
        public bool Revived;
        /// <summary>False when the run could not set a record (revived, or no floors / no time).</summary>
        public bool Counted;
        public bool NewBestPace, NewBestTime;
        /// <summary>Endless and Time Attack: new best floors in this mode / new longest survived Time / count of improved splits.</summary>
        public bool NewBestFloors, NewBestSurvived;
        public int NewSplits;
        /// <summary>No record existed before (the first clear of this level and mode, or the first endless run).</summary>
        public bool FirstRecord;
        /// <summary>Previous bests (0 = none): Pace as floors / ticks, Time and best floors.</summary>
        public int PrevPaceFloors, PrevPaceTicks, PrevBestTimeTicks, PrevBestFloors;
        public int PrevPaceHundredths { get { return TimeRecords.PaceHundredths(PrevPaceFloors, PrevPaceTicks); } }
        /// <summary>This run's Time minus the previous best Time in ticks (negative = faster); 0 when there was none. See <see cref="HasPrevTime"/>.</summary>
        public int DeltaTicks;
        /// <summary>This run's Pace minus the previous best Pace in hundredths of a plat/s (positive = faster); 0 when there was none.</summary>
        public int DeltaPaceHundredths;
        public bool HasPrevTime, HasPrevPace;
        /// <summary>Levels: the medal this run earned (None for a revived run or without target data), the medal held before, and whether it is higher now.</summary>
        public MedalLevel Medal, MedalBefore;
        public bool MedalImproved;
        /// <summary>True when this run earned a Gold medal that was not held before.</summary>
        public bool NewGold { get { return MedalImproved && Medal == MedalLevel.Gold; } }
        /// <summary>This run counts for the Daily "clear at 0.6 plat/s" goal.</summary>
        public bool PaceClear;
        /// <summary>Endless captures: the run beat the region's best coins or gems per minute.</summary>
        public bool NewBestCoinsPerMin, NewBestGemsPerMin;
        public int PrevCoinsPerMinX100, PrevGemsPerMinX100;
        /// <summary>Any record at all was set (the NEW BEST badge).</summary>
        public bool AnyNewBest { get { return NewBestPace || NewBestTime || NewBestFloors || NewBestSurvived || NewSplits > 0; } }
    }

    /// <summary>The improve-only update rules, pure and shared by dotnet tests and <c>ProgressService</c>.</summary>
    public static class RecordRules
    {
        /// <summary>Stored inputs above this many bytes are dropped (the record itself still counts). A normal level run is 1 to 3 KB.</summary>
        public const int MaxInputBytes = 12000;
        /// <summary>Stored inputs of an endless or Time Attack best run: a longer cap than a level run (an endless run can last many minutes). Over it the inputs are dropped, the record still counts.</summary>
        public const int MaxLongInputBytes = 30000;
        /// <summary>Longest endless curve kept (seconds).</summary>
        public const int MaxEndlessCurve = 900;

        static void Fill(RecordResult r, RunRecord run)
        {
            r.Kind = run.Kind;
            r.Floors = run.Floors < 0 ? 0 : run.Floors;
            r.Ticks = run.Ticks < 0 ? 0 : run.Ticks;
            r.PaceHundredths = TimeRecords.PaceHundredths(r.Floors, r.Ticks);
            r.Revived = run.Revived;
            r.Counted = run.Counts;
        }

        /// <summary>Applies a completed campaign run to the record of its level and mode (created by the caller). Returns what changed.</summary>
        public static RecordResult ApplyLevel(ModeRecord rec, RunRecord run, int targetPaceMilli)
        {
            var r = new RecordResult();
            Fill(r, run);
            r.Counted = run.Floors > 0 && run.Ticks > 0;   // owner 2026-11-10: a revived level run counts like any other (records, medals, goals); the clock already stops while dead
            r.PrevPaceFloors = rec.Floors;
            r.PrevPaceTicks = rec.Ticks;
            r.PrevBestTimeTicks = rec.BestTimeTicks;
            r.HasPrevPace = rec.HasPace;
            r.HasPrevTime = rec.BestTimeTicks > 0;
            r.FirstRecord = !rec.HasPace;
            r.MedalBefore = MedalOf(rec, targetPaceMilli);
            r.Medal = MedalLevel.None;
            if (!r.Counted) return r;

            if (r.HasPrevTime) r.DeltaTicks = run.Ticks - rec.BestTimeTicks;
            if (r.HasPrevPace) r.DeltaPaceHundredths = r.PaceHundredths - r.PrevPaceHundredths;

            r.PaceClear = TimeRecords.IsDailyPaceClear(run.Floors, run.Ticks);
            r.Medal = TimeRecords.MedalFor(run.Pace, targetPaceMilli);
            if ((int)r.Medal > rec.Medal) rec.Medal = (int)r.Medal;
            r.MedalImproved = r.Medal > r.MedalBefore;

            if (rec.BestTimeTicks <= 0 || run.Ticks < rec.BestTimeTicks)
            {
                rec.BestTimeTicks = run.Ticks;
                r.NewBestTime = true;
            }
            if (TimeRecords.IsBetter(run.Pace, rec.Pace))
            {
                rec.Floors = run.Floors;
                rec.Ticks = run.Ticks;
                rec.LayoutSeed = run.LayoutSeed;
                rec.AttemptSeed = run.AttemptSeed;
                rec.HeroId = run.HeroId ?? "";
                rec.Mode = run.Mode;
                rec.GameVersion = run.GameVersion ?? "";
                rec.Inputs = run.Inputs != null && run.Inputs.Length <= MaxInputBytes ? run.Inputs : new byte[0];
                rec.Curve = run.Curve ?? new int[0];
                r.NewBestPace = true;
            }
            return r;
        }

        /// <summary>The medal a record holds: the stored one, or what its best Pace earns against today's target data, whichever is higher.</summary>
        public static MedalLevel MedalOf(ModeRecord rec, int targetPaceMilli)
        {
            if (rec == null) return MedalLevel.None;
            MedalLevel derived = TimeRecords.MedalFor(rec.Pace, targetPaceMilli);
            return (int)derived > rec.Medal ? derived : (MedalLevel)rec.Medal;
        }

        /// <summary>Applies an ended endless run to the region's record for the run's mode.</summary>
        public static RecordResult ApplyEndless(EndlessModeRecord rec, RunRecord run)
        {
            var r = new RecordResult();
            Fill(r, run);
            r.PrevPaceFloors = rec.PaceFloors;
            r.PrevPaceTicks = rec.PaceTicks;
            r.PrevBestTimeTicks = rec.BestSurvivedTicks;
            r.PrevBestFloors = rec.BestFloors;
            r.HasPrevPace = rec.Pace.IsValid;
            r.HasPrevTime = rec.BestSurvivedTicks > 0;
            r.FirstRecord = rec.BestFloors <= 0 && !rec.Pace.IsValid;
            if (!r.Counted) return r;

            if (r.HasPrevTime) r.DeltaTicks = run.Ticks - rec.BestSurvivedTicks;   // positive = survived longer
            if (r.HasPrevPace) r.DeltaPaceHundredths = r.PaceHundredths - r.PrevPaceHundredths;

            if (run.Floors > rec.BestFloors) { rec.BestFloors = run.Floors; r.NewBestFloors = true; }
            if (run.Ticks > rec.BestSurvivedTicks) { rec.BestSurvivedTicks = run.Ticks; r.NewBestSurvived = true; }
            if (TimeRecords.IsBetter(run.Pace, rec.Pace))
            {
                rec.PaceFloors = run.Floors;
                rec.PaceTicks = run.Ticks;
                rec.LayoutSeed = run.LayoutSeed;
                rec.HeroId = run.HeroId ?? "";
                rec.GameVersion = run.GameVersion ?? "";
                rec.Mode = run.Mode;
                rec.Inputs = run.Inputs != null && run.Inputs.Length <= MaxLongInputBytes ? run.Inputs : new byte[0];
                int[] c = run.Curve ?? new int[0];
                if (c.Length > MaxEndlessCurve) { var cut = new int[MaxEndlessCurve]; Array.Copy(c, cut, MaxEndlessCurve); c = cut; }
                rec.Curve = c;
                r.NewBestPace = true;
            }
            int[] sp = run.Splits;
            if (sp != null)
            {
                int n = Math.Min(sp.Length, TimeRecords.MaxSplits);
                if (n > rec.Splits.Length) { var grown = new int[n]; Array.Copy(rec.Splits, grown, rec.Splits.Length); rec.Splits = grown; }
                for (int i = 0; i < n; i++)
                {
                    if (sp[i] <= 0) continue;
                    if (rec.Splits[i] <= 0 || sp[i] < rec.Splits[i]) { rec.Splits[i] = sp[i]; r.NewSplits++; }
                }
            }
            return r;
        }

        /// <summary>Applies a Time Attack run: the score is the floors reached (no revive exists in it).</summary>
        public static RecordResult ApplyTimeAttack(TimeAttackRecord rec, RunRecord run)
        {
            var r = new RecordResult();
            Fill(r, run);
            if (rec.BestFloors > 0 && !TimeAttackRamp.IsCurrent(rec)) rec.BestFloors = 0;   // legacy record of the old flat start: not comparable, replaced by the first counted run
            r.PrevBestFloors = rec.BestFloors;
            r.FirstRecord = rec.BestFloors <= 0;
            r.Counted = !run.Revived && run.Floors > 0;
            if (!r.Counted) return r;
            if (run.Floors > rec.BestFloors)
            {
                rec.RampStartLevel = TimeAttackRamp.StartLevel;
                rec.BestFloors = run.Floors;
                rec.Ticks = run.Ticks;
                rec.LayoutSeed = run.LayoutSeed;
                rec.HeroId = run.HeroId ?? "";
                rec.Mode = run.Mode;
                rec.GameVersion = run.GameVersion ?? "";
                rec.Inputs = run.Inputs != null && run.Inputs.Length <= MaxLongInputBytes ? run.Inputs : new byte[0];
                rec.Curve = run.Curve ?? new int[0];
                r.NewBestFloors = true;
            }
            return r;
        }

        /// <summary>Per region captures: best coins and gems per minute of one run (needs a Time; a revived run never sets them).</summary>
        public static void ApplyCaptures(RegionTimedRecord rec, RunRecord run, RecordResult r)
        {
            r.PrevCoinsPerMinX100 = rec.BestCoinsPerMinX100;
            r.PrevGemsPerMinX100 = rec.BestGemsPerMinX100;
            if (run.Revived || run.Ticks <= 0) return;
            int c = TimeRecords.PerMinuteX100(run.Coins, run.Ticks), g = TimeRecords.PerMinuteX100(run.Gems, run.Ticks);
            if (c > rec.BestCoinsPerMinX100) { rec.BestCoinsPerMinX100 = c; r.NewBestCoinsPerMin = true; }
            if (g > rec.BestGemsPerMinX100) { rec.BestGemsPerMinX100 = g; r.NewBestGemsPerMin = true; }
        }
    }
}
