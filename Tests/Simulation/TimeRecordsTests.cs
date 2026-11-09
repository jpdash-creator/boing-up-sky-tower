using System.Collections.Generic;
using System.Text;
using BoingUpSkyTower.Economy;
using Newtonsoft.Json;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Timed gameplay (doc 46 section 11): Time and Pace maths and text, the exact Pace comparison, medals, the run-length input code,
    /// the improve-only record rules (level, endless, Time Attack) and the JSON of the new save containers (old JSON still loads).
    /// One class on purpose: a few tests change the shared medal table and target source and put them back, so they must not overlap other tests.
    /// </summary>
    public class TimeRecordsTests
    {
        // ---- time and pace text ----

        [Theory]
        [InlineData(0, "00:00.0")]
        [InlineData(-5, "00:00.0")]
        [InlineData(1, "00:00.0")]        // 0.0167 s
        [InlineData(3, "00:00.1")]        // 0.05 s rounds half up to 0.1
        [InlineData(6, "00:00.1")]
        [InlineData(4344, "01:12.4")]     // 72.4 s exactly
        [InlineData(4346, "01:12.4")]     // 72.433 s
        [InlineData(4347, "01:12.5")]     // 72.45 s rounds half up
        [InlineData(3599, "01:00.0")]     // 59.983 s rounds up to a whole minute
        public void FormatTimeExamples(int ticks, string expected)
        {
            Assert.Equal(expected, TimeRecords.FormatTime(ticks));
        }

        [Fact]
        public void FormatTimeOverTenMinutesAndHoursRollIntoMinutes()
        {
            Assert.Equal("12:00.0", TimeRecords.FormatTime(12 * 60 * 60));
            Assert.Equal("60:00.0", TimeRecords.FormatTime(60 * 60 * 60));      // one hour reads 60:00.0
            Assert.Equal("99:59.9", TimeRecords.FormatTime(99 * 3600 + 59 * 60 + 54));   // 99:59.9 in ticks (5999.9 s)
            Assert.Equal("99:59.9", TimeRecords.FormatTime(2000000));           // capped, never 100:00.0
        }

        [Fact]
        public void FormatPaceExamples()
        {
            Assert.Equal("0.83 plat/s", TimeRecords.FormatPace(60, 4344));      // 60 floors in 01:12.4
            Assert.Equal("1.00 plat/s", TimeRecords.FormatPace(1, 60));
            Assert.Equal("0.60 plat/s", TimeRecords.FormatPace(6, 600));
            Assert.Equal("12.50 plat/s", TimeRecords.FormatPace(25, 120));
            Assert.Equal("0.00 plat/s", TimeRecords.FormatPace(0, 600));        // no floors
            Assert.Equal("0.00 plat/s", TimeRecords.FormatPace(10, 0));         // no time: safe, no division by zero
            Assert.Equal("0.00 plat/s", TimeRecords.FormatPace(-3, 600));
        }

        [Fact]
        public void PaceRoundsHalfUpToTwoDecimals()
        {
            Assert.Equal(1, TimeRecords.PaceHundredths(1, 12000));    // exactly 0.005 rounds up
            Assert.Equal(0, TimeRecords.PaceHundredths(1, 12001));    // just under 0.005
            Assert.Equal(83, TimeRecords.PaceHundredths(5, 361));     // 0.8310
            Assert.Equal(83, TimeRecords.PaceHundredths(5, 360));     // 0.8333
            Assert.Equal(84, TimeRecords.PaceHundredths(84, 6000));   // 0.8400 exactly: 84 floors in 100 s
            Assert.Equal(830, TimeRecords.PaceMilli(60, 4338));       // 0.83004
        }

        [Fact]
        public void FormatIntoACachedStringBuilderMatchesTheStringVersion()
        {
            var sb = new StringBuilder(32);
            for (int ticks = 0; ticks < 20000; ticks += 37)
            {
                sb.Length = 0;
                TimeRecords.FormatTime(sb, ticks);
                Assert.Equal(TimeRecords.FormatTime(ticks), sb.ToString());
            }
            sb.Length = 0;
            TimeRecords.FormatPace(sb, 60, 4344);
            Assert.Equal("0.83 plat/s", sb.ToString());
            sb.Length = 0;
            TimeRecords.FormatDeltaTime(sb, 190);
            Assert.Equal("+3.2 s", sb.ToString());
        }

        [Fact]
        public void FormatsIgnoreTheCurrentCulture()
        {
            var old = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                Assert.Equal("0.83 plat/s", TimeRecords.FormatPace(60, 4344));
                Assert.Equal("01:12.4", TimeRecords.FormatTime(4344));
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = old; }
        }

        [Fact]
        public void DeltaTexts()
        {
            Assert.Equal("+3.2 s", TimeRecords.FormatDeltaTime(190));
            Assert.Equal("-0.4 s", TimeRecords.FormatDeltaTime(-24));
            Assert.Equal("0.0 s", TimeRecords.FormatDeltaTime(0));
            Assert.Equal("0.0 s", TimeRecords.FormatDeltaTime(2));      // under a twentieth of a second: no sign
            Assert.Equal("+0.05 plat/s", TimeRecords.FormatDeltaPace(5));
            Assert.Equal("-0.12 plat/s", TimeRecords.FormatDeltaPace(-12));
            Assert.Equal("0.00 plat/s", TimeRecords.FormatDeltaPace(0));
        }

        // ---- pace comparison ----

        [Fact]
        public void PaceIsComparedExactlyByCrossMultiplication()
        {
            Assert.Equal(0, TimeRecords.ComparePace(60, 4344, 120, 8688));   // the same ratio, no float error
            Assert.Equal(1, TimeRecords.ComparePace(61, 4344, 60, 4344));
            Assert.Equal(-1, TimeRecords.ComparePace(60, 4345, 60, 4344));
            // a difference far below float resolution: 1000000 / 1000001 against 999999 / 1000000
            Assert.Equal(1, TimeRecords.ComparePace(1000000, 1000001, 999999, 1000000));
            // large numbers do not overflow
            Assert.Equal(0, TimeRecords.ComparePace(int.MaxValue, int.MaxValue, 1, 1));
        }

        [Fact]
        public void ZeroFloorsOrZeroTimeIsSafeAndNeverBest()
        {
            Assert.Equal(0, TimeRecords.ComparePace(0, 0, 0, 100));
            Assert.Equal(-1, TimeRecords.ComparePace(0, 100, 1, 100));
            Assert.Equal(1, TimeRecords.ComparePace(1, 100, 5, 0));
            Assert.False(TimeRecords.IsBetter(new PaceValue(0, 100), new PaceValue(0, 0)));
            Assert.False(TimeRecords.IsBetter(new PaceValue(10, 0), new PaceValue(0, 0)));
            Assert.True(TimeRecords.IsBetter(new PaceValue(1, 6000), new PaceValue(0, 0)));   // any real pace beats no record
        }

        [Fact]
        public void IsBetterUsesPaceAndBreaksTiesByShorterTime()
        {
            var old = new PaceValue(120, 8688);
            Assert.True(TimeRecords.IsBetter(new PaceValue(61, 4344), old));           // faster pace
            Assert.False(TimeRecords.IsBetter(new PaceValue(59, 4344), old));          // slower pace
            Assert.True(TimeRecords.IsBetter(new PaceValue(60, 4344), old));           // equal pace, shorter time
            Assert.False(TimeRecords.IsBetter(old, new PaceValue(60, 4344)));          // equal pace, longer time
            Assert.False(TimeRecords.IsBetter(old, old));                              // identical: no change
        }

        // ---- medals ----

        [Fact]
        public void MedalThresholdsAreFiftyFiveSeventyEightyFivePercentAndInclusive()
        {
            // target 1.000 plat/s: lines at 0.55, 0.70, 0.85 (pace = floors x 60 / ticks)
            const int T = 1000;
            Assert.Equal(MedalLevel.None, TimeRecords.MedalFor(new PaceValue(5489, 600000), T));    // 0.54890
            Assert.Equal(MedalLevel.Bronze, TimeRecords.MedalFor(new PaceValue(11, 1200), T));      // exactly 0.55
            Assert.Equal(MedalLevel.Bronze, TimeRecords.MedalFor(new PaceValue(69, 6000), T));      // 0.69
            Assert.Equal(MedalLevel.Silver, TimeRecords.MedalFor(new PaceValue(7, 600), T));        // exactly 0.70
            Assert.Equal(MedalLevel.Silver, TimeRecords.MedalFor(new PaceValue(84, 6000), T));      // 0.84
            Assert.Equal(MedalLevel.Gold, TimeRecords.MedalFor(new PaceValue(17, 1200), T));        // exactly 0.85
            Assert.Equal(MedalLevel.Gold, TimeRecords.MedalFor(new PaceValue(500, 1200), T));       // far above
        }

        [Fact]
        public void NoTargetDataOrNoPaceEarnsNothing()
        {
            Assert.Equal(MedalLevel.None, TimeRecords.MedalFor(new PaceValue(100, 100), 0));
            Assert.Equal(MedalLevel.None, TimeRecords.MedalFor(new PaceValue(100, 100), -5));
            Assert.Equal(MedalLevel.None, TimeRecords.MedalFor(new PaceValue(0, 100), 1000));
            Assert.Equal(MedalLevel.None, TimeRecords.MedalFor(new PaceValue(100, 0), 1000));
        }

        [Fact]
        public void ChangingAPercentageChangesTheMedalAndTheTargetSourceIsUsed()
        {
            int[] saved = (int[])TimeRecords.MedalPercents.Clone();
            IMedalTargetSource savedSource = MedalTargets.Source;
            try
            {
                var run = new PaceValue(7, 600);   // 0.70 plat/s against a 1.0 target: Silver today
                Assert.Equal(MedalLevel.Silver, TimeRecords.MedalFor(run, 1000));
                TimeRecords.MedalPercents[1] = 75;   // the owner moves the silver line up
                Assert.Equal(MedalLevel.Bronze, TimeRecords.MedalFor(run, 1000));
                TimeRecords.MedalPercents[0] = 71;   // and bronze above this run
                Assert.Equal(MedalLevel.None, TimeRecords.MedalFor(run, 1000));

                System.Array.Copy(saved, TimeRecords.MedalPercents, 3);
                var table = new DictionaryMedalTargets();
                table.Set("lvl_001_01", 2, 1000);
                MedalTargets.Source = table;
                Assert.Equal(MedalLevel.Silver, TimeRecords.MedalFor(run, "lvl_001_01", 2));
                Assert.Equal(MedalLevel.None, TimeRecords.MedalFor(run, "lvl_001_01", 3));   // other mode: no data
                Assert.Equal(MedalLevel.None, TimeRecords.MedalFor(run, "lvl_999_99", 2));
                MedalTargets.Source = new NoMedalTargets();
                Assert.Equal(0, MedalTargets.Lookup("lvl_001_01", 2));
            }
            finally
            {
                System.Array.Copy(saved, TimeRecords.MedalPercents, 3);
                MedalTargets.Source = savedSource;
            }
        }

        [Fact]
        public void MedalLinePaceIsTheTargetTimesThePercent()
        {
            Assert.Equal(550, TimeRecords.MedalLinePaceMilli(MedalLevel.Bronze, 1000));
            Assert.Equal(700, TimeRecords.MedalLinePaceMilli(MedalLevel.Silver, 1000));
            Assert.Equal(850, TimeRecords.MedalLinePaceMilli(MedalLevel.Gold, 1000));
            Assert.Equal(0, TimeRecords.MedalLinePaceMilli(MedalLevel.None, 1000));
            Assert.Equal(0, TimeRecords.MedalLinePaceMilli(MedalLevel.Gold, 0));
        }

        [Fact]
        public void DailyPaceClearEdge()
        {
            Assert.True(TimeRecords.IsDailyPaceClear(6, 600));       // exactly 0.6 plat/s
            Assert.False(TimeRecords.IsDailyPaceClear(599, 60000));  // 0.599
            Assert.False(TimeRecords.IsDailyPaceClear(0, 600));
            Assert.False(TimeRecords.IsDailyPaceClear(10, 0));
        }

        // ---- the curve ----

        [Fact]
        public void CurveGivesTheTimeToReachAFloorAndTheFloorsAtATime()
        {
            int[] curve = { 3, 7, 12 };   // floors at the end of second 1, 2, 3
            Assert.Equal(0, TimeRecords.TicksToReach(curve, 0));
            Assert.Equal(20, TimeRecords.TicksToReach(curve, 1));
            Assert.Equal(60, TimeRecords.TicksToReach(curve, 3));
            Assert.Equal(90, TimeRecords.TicksToReach(curve, 5));
            Assert.Equal(180, TimeRecords.TicksToReach(curve, 12));
            Assert.Equal(-1, TimeRecords.TicksToReach(curve, 13));
            Assert.Equal(-1, TimeRecords.TicksToReach(null, 5));
            Assert.Equal(1500, TimeRecords.FloorsMilliAtTick(curve, 30));
            Assert.Equal(5000, TimeRecords.FloorsMilliAtTick(curve, 90));
            Assert.Equal(12000, TimeRecords.FloorsMilliAtTick(curve, 1000));   // holds the last value
            Assert.Equal(0, TimeRecords.FloorsMilliAtTick(curve, 0));
            int delta;
            Assert.True(TimeRecords.TryDeltaTicks(curve, 5, 100, out delta));
            Assert.Equal(10, delta);                                           // 10 ticks slower than the best run
            Assert.True(TimeRecords.TryDeltaTicks(curve, 5, 80, out delta));
            Assert.Equal(-10, delta);
            Assert.False(TimeRecords.TryDeltaTicks(curve, 50, 80, out delta));
            Assert.Equal(0, delta);
        }

        [Fact]
        public void PerMinuteRates()
        {
            Assert.Equal(60000, TimeRecords.PerMinuteX100(600, 3600));     // 600 coins in one minute = 600.00 per minute
            Assert.Equal(30000, TimeRecords.PerMinuteX100(600, 7200));
            Assert.Equal(0, TimeRecords.PerMinuteX100(600, 0));
            Assert.Equal(0, TimeRecords.PerMinuteX100(0, 600));
        }

        // ---- input code ----

        static int[] Pattern(int n)
        {
            var a = new int[n];
            uint seed = 12345;
            int cur = 0, left = 0;
            for (int i = 0; i < n; i++)
            {
                if (left == 0)
                {
                    seed = seed * 1664525u + 1013904223u;
                    cur = (int)((seed >> 8) % 4) == 0 ? 131071 : (int)((seed >> 12) % 8);   // includes the biggest value (all 17 bits)
                    left = 1 + (int)((seed >> 20) % 300);                                   // runs up to 300 ticks
                }
                a[i] = cur;
                left--;
            }
            return a;
        }

        [Fact]
        public void RunLengthEncodeDecodeRoundTrip()
        {
            foreach (int n in new[] { 0, 1, 2, 59, 600, 12345 })
            {
                int[] src = Pattern(n);
                byte[] bytes = InputCodec.Encode(src, n);
                Assert.Equal(n, InputCodec.DecodedLength(bytes));
                Assert.Equal(src, InputCodec.Decode(bytes));
            }
        }

        [Fact]
        public void LongIdleRunsAreSmallAndTheRecorderMatchesTheEncoder()
        {
            var rec = new InputRecorder();
            var src = new int[3600];   // a minute of no input then a tap pattern
            for (int i = 0; i < src.Length; i++) src[i] = i >= 1800 && i % 90 < 5 ? SimInput.Jump | SimInput.Right : 0;
            for (int i = 0; i < src.Length; i++) rec.Add(src[i]);
            Assert.Equal(3600, rec.Ticks);
            byte[] bytes = rec.ToBytes();
            Assert.Equal(InputCodec.Encode(src, src.Length), bytes);
            Assert.Equal(src, InputCodec.Decode(bytes));
            Assert.True(bytes.Length < 400, "a minute of play is only a few hundred bytes, was " + bytes.Length);
            rec.Clear();
            Assert.Equal(0, rec.Ticks);
            Assert.Empty(rec.ToBytes());
        }

        [Fact]
        public void RecorderGrowsPastItsStartCapacity()
        {
            var rec = new InputRecorder(16);
            var src = new int[2000];
            for (int i = 0; i < src.Length; i++) src[i] = i % 2;   // worst case: every tick a new run
            for (int i = 0; i < src.Length; i++) rec.Add(src[i]);
            Assert.Equal(src, InputCodec.Decode(rec.ToBytes()));
        }

        [Fact]
        public void DamagedInputDataIsRejectedNotThrown()
        {
            Assert.Null(InputCodec.Decode(new byte[] { 0x80 }));                  // cut inside a varint
            Assert.Null(InputCodec.Decode(new byte[] { 5 }));                     // a value without a run length
            Assert.Equal(-1, InputCodec.DecodedLength(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }));
            Assert.Empty(InputCodec.Decode(new byte[0]));
            Assert.Empty(InputCodec.Decode(null));
        }

        // ---- replay: seed plus inputs reproduce floors and ticks ----

        [Fact]
        public void ReplayOfSeedAndDecodedInputsReproducesFloorsAndTicks()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;   // content folder not found: covered elsewhere
            LoadedLevel level = ContentLoader.LoadRegions(root, 1, 1)[0];
            var cfg = new SimConfig();
            LevelData data = LevelBuilder.Build(cfg, level.Spec);

            var rec = new InputRecorder();
            var sim = new Sim(cfg, data, level.Spec);
            SimState s = sim.NewState();
            uint seed = 99;
            for (int t = 0; t < 2400 && !s.Dead; t++)
            {
                seed = seed * 1664525u + 1013904223u;
                int input = (int)(seed >> 24) % 5 == 0 ? SimInput.Jump : ((seed >> 20) % 3 == 0 ? SimInput.Left : SimInput.Right);
                input |= SimInput.Jump & -(int)((seed >> 10) & 1);
                rec.Add(input);
                sim.Step(ref s, input);
            }
            Assert.True(s.StartTick >= 0, "the scripted run jumped");

            int[] inputs = InputCodec.Decode(rec.ToBytes());
            var sim2 = new Sim(cfg, LevelBuilder.Build(cfg, level.Spec), level.Spec);
            SimState r = sim2.NewState();
            foreach (int input in inputs) sim2.Step(ref r, input);
            Assert.Equal(s.Row, r.Row);
            Assert.Equal(s.Tick, r.Tick);
            Assert.Equal(s.StartTick, r.StartTick);
            Assert.Equal(s.Score, r.Score);
            Assert.Equal(s.Dead, r.Dead);
        }

        // ---- record rules: level ----

        static RunRecord Run(int floors, int ticks, bool revived = false, int mode = 2)
        {
            return new RunRecord { Kind = RunKind.Level, LevelId = "lvl_001_01", Floors = floors, Ticks = ticks, Revived = revived, Mode = mode, HeroId = "momo", GameVersion = "1.2.3.4", LayoutSeed = 77, AttemptSeed = 5, Inputs = new byte[] { 1, 2, 3 }, Curve = new[] { 1, 2, 3 } };
        }

        [Fact]
        public void FirstRunSetsEveryField()
        {
            var rec = new ModeRecord();
            RecordResult r = RecordRules.ApplyLevel(rec, Run(60, 4344), 0);
            Assert.True(r.Counted);
            Assert.True(r.NewBestPace);
            Assert.True(r.NewBestTime);
            Assert.True(r.FirstRecord);
            Assert.True(r.AnyNewBest);
            Assert.False(r.HasPrevPace);
            Assert.Equal(60, rec.Floors);
            Assert.Equal(4344, rec.Ticks);
            Assert.Equal(4344, rec.BestTimeTicks);
            Assert.Equal(77, rec.LayoutSeed);
            Assert.Equal(5, rec.AttemptSeed);
            Assert.Equal("momo", rec.HeroId);
            Assert.Equal("1.2.3.4", rec.GameVersion);
            Assert.Equal(new byte[] { 1, 2, 3 }, rec.Inputs);
            Assert.Equal(new[] { 1, 2, 3 }, rec.Curve);
            Assert.Equal(83, r.PaceHundredths);
        }

        [Fact]
        public void OnlyAnImprovementReplacesTheRecord()
        {
            var rec = new ModeRecord();
            RecordRules.ApplyLevel(rec, Run(60, 4344), 0);

            RecordResult slower = RecordRules.ApplyLevel(rec, Run(50, 4344), 0);
            Assert.True(slower.Counted);
            Assert.False(slower.NewBestPace);
            Assert.False(slower.NewBestTime);
            Assert.False(slower.AnyNewBest);
            Assert.Equal(60, rec.Floors);

            RecordResult same = RecordRules.ApplyLevel(rec, Run(60, 4344), 0);   // identical run
            Assert.False(same.NewBestPace);
            Assert.False(same.NewBestTime);

            RecordResult better = RecordRules.ApplyLevel(rec, Run(20, 1000), 0);   // faster pace (1.2) and shorter time
            Assert.True(better.NewBestPace);
            Assert.True(better.NewBestTime);
            Assert.Equal(20, rec.Floors);
            Assert.Equal(1000, rec.Ticks);
            Assert.Equal(60, better.PrevPaceFloors);
            Assert.Equal(4344, better.PrevPaceTicks);
            Assert.Equal(4344, better.PrevBestTimeTicks);
            Assert.Equal(1000 - 4344, better.DeltaTicks);
            Assert.Equal(120 - 83, better.DeltaPaceHundredths);
            Assert.False(better.FirstRecord);
        }

        [Fact]
        public void ABetterTimeAloneUpdatesOnlyTheTimeAndKeepsThePaceRun()
        {
            var rec = new ModeRecord();
            RecordRules.ApplyLevel(rec, Run(60, 4344), 0);
            RecordResult r = RecordRules.ApplyLevel(rec, Run(30, 4000), 0);   // pace 0.45 (worse), time 4000 (shorter)
            Assert.False(r.NewBestPace);
            Assert.True(r.NewBestTime);
            Assert.Equal(4000, rec.BestTimeTicks);
            Assert.Equal(60, rec.Floors);        // the pace run (and its curve, seed, inputs) stay with the best Pace
            Assert.Equal(4344, rec.Ticks);
            Assert.Equal(-344, r.DeltaTicks);
        }

        [Fact]
        public void EqualPaceWithShorterTimeIsABetterPace()
        {
            var rec = new ModeRecord();
            RecordRules.ApplyLevel(rec, Run(120, 8688), 0);
            RecordResult r = RecordRules.ApplyLevel(rec, Run(60, 4344), 0);
            Assert.True(r.NewBestPace);
            Assert.Equal(4344, rec.Ticks);
        }

        [Fact]
        public void RevivedRunsNeverSetARecordButReturnTheirNumbers()
        {
            var rec = new ModeRecord();
            RunRecord rr = Run(60, 4344, revived: true);
            RecordResult r = RecordRules.ApplyLevel(rec, rr, 1000);
            Assert.False(r.Counted);
            Assert.True(r.Revived);
            Assert.False(r.NewBestPace);
            Assert.False(r.NewBestTime);
            Assert.False(r.AnyNewBest);
            Assert.False(r.PaceClear);
            Assert.Equal(MedalLevel.None, r.Medal);
            Assert.Equal(60, r.Floors);          // the numbers are still there for the result screen
            Assert.Equal(4344, r.Ticks);
            Assert.Equal(83, r.PaceHundredths);
            Assert.False(rec.HasPace);
            Assert.Equal(0, rec.BestTimeTicks);
            Assert.Equal(0, rec.Medal);

            RecordRules.ApplyLevel(rec, Run(10, 3000), 0);
            RecordResult r2 = RecordRules.ApplyLevel(rec, Run(500, 1000, revived: true), 0);   // would be a huge record
            Assert.False(r2.NewBestPace);
            Assert.Equal(10, rec.Floors);
            Assert.Equal(3000, rec.BestTimeTicks);
        }

        [Fact]
        public void RunsWithoutFloorsOrTimeCountForNothing()
        {
            var rec = new ModeRecord();
            Assert.False(RecordRules.ApplyLevel(rec, Run(0, 600), 0).Counted);
            Assert.False(RecordRules.ApplyLevel(rec, Run(10, 0), 0).Counted);
            Assert.False(rec.HasPace);
        }

        [Fact]
        public void MedalIsEarnedStoredAndNeverLost()
        {
            var rec = new ModeRecord();
            // target 1.0 plat/s: 0.70 plat/s = Silver
            RecordResult r = RecordRules.ApplyLevel(rec, Run(7, 600), 1000);
            Assert.Equal(MedalLevel.Silver, r.Medal);
            Assert.Equal(MedalLevel.None, r.MedalBefore);
            Assert.True(r.MedalImproved);
            Assert.False(r.NewGold);
            Assert.Equal((int)MedalLevel.Silver, rec.Medal);

            RecordResult worse = RecordRules.ApplyLevel(rec, Run(1, 600), 1000);   // a poor run later
            Assert.Equal(MedalLevel.None, worse.Medal);
            Assert.Equal(MedalLevel.Silver, worse.MedalBefore);
            Assert.False(worse.MedalImproved);
            Assert.Equal((int)MedalLevel.Silver, rec.Medal);

            RecordResult gold = RecordRules.ApplyLevel(rec, Run(17, 1200), 1000);
            Assert.True(gold.NewGold);
            Assert.Equal((int)MedalLevel.Gold, rec.Medal);
            Assert.Equal(MedalLevel.Gold, RecordRules.MedalOf(rec, 1000));
            Assert.Equal(MedalLevel.Gold, RecordRules.MedalOf(rec, 0));            // earned medals survive missing target data
        }

        [Fact]
        public void MedalOfFollowsNewTargetDataForAnOldRecord()
        {
            var rec = new ModeRecord { Floors = 7, Ticks = 600 };   // 0.70 plat/s, saved before any target existed
            Assert.Equal(MedalLevel.None, RecordRules.MedalOf(rec, 0));
            Assert.Equal(MedalLevel.Silver, RecordRules.MedalOf(rec, 1000));
            Assert.Equal(MedalLevel.None, RecordRules.MedalOf(null, 1000));
        }

        [Fact]
        public void InputsOverTheCapAreDroppedButTheRecordCounts()
        {
            var rec = new ModeRecord();
            RunRecord rr = Run(60, 4344);
            rr.Inputs = new byte[RecordRules.MaxInputBytes + 1];
            RecordResult r = RecordRules.ApplyLevel(rec, rr, 0);
            Assert.True(r.NewBestPace);
            Assert.Empty(rec.Inputs);
        }

        [Fact]
        public void EachModeKeepsItsOwnRecord()
        {
            // one ModeRecord per Gameplay mode (LevelProgress.Records[mode]): updating one never touches another
            var records = new ModeRecord[TimeRecords.ModeCount];
            for (int m = 0; m < records.Length; m++) records[m] = new ModeRecord { Mode = m };
            RecordRules.ApplyLevel(records[2], Run(60, 4344, mode: 2), 0);
            RecordRules.ApplyLevel(records[4], Run(10, 5000, mode: 4), 0);
            Assert.Equal(60, records[2].Floors);
            Assert.Equal(10, records[4].Floors);
            Assert.False(records[0].HasPace);
            Assert.False(records[1].HasPace);
            Assert.False(records[3].HasPace);
            Assert.True(TimeRecords.ValidMode(4));
            Assert.False(TimeRecords.ValidMode(5));
            Assert.Equal(4, TimeRecords.ClampMode(9));
            Assert.Equal(0, TimeRecords.ClampMode(-1));
        }

        // ---- record rules: endless, Time Attack, captures ----

        static RunRecord Endless(int floors, int ticks, int[] splits, bool revived = false)
        {
            return new RunRecord { Kind = RunKind.Endless, RegionId = "region_001", Floors = floors, Ticks = ticks, Splits = splits, Revived = revived, Curve = new[] { 1 }, HeroId = "momo", GameVersion = "1" };
        }

        [Fact]
        public void EndlessKeepsBestFloorsPaceSurvivedTimeAndSplitsSeparately()
        {
            var rec = new EndlessModeRecord();
            RecordResult a = RecordRules.ApplyEndless(rec, Endless(250, 12000, new[] { 3000, 7000 }));
            Assert.True(a.NewBestFloors);
            Assert.True(a.NewBestSurvived);
            Assert.True(a.NewBestPace);
            Assert.Equal(2, a.NewSplits);
            Assert.True(a.FirstRecord);
            Assert.Equal(new[] { 3000, 7000 }, rec.Splits);

            // fewer floors but a faster pace, a shorter life, one faster split and one new stage
            RecordResult b = RecordRules.ApplyEndless(rec, Endless(200, 6000, new[] { 3200, 6000 }));
            Assert.False(b.NewBestFloors);
            Assert.False(b.NewBestSurvived);
            Assert.True(b.NewBestPace);           // 200 / 6000 beats 250 / 12000
            Assert.Equal(1, b.NewSplits);         // only the second stage got faster
            Assert.Equal(new[] { 3000, 6000 }, rec.Splits);
            Assert.Equal(250, rec.BestFloors);
            Assert.Equal(12000, rec.BestSurvivedTicks);
            Assert.Equal(200, rec.PaceFloors);
            Assert.False(b.FirstRecord);

            // a longer run: more floors and a new stage, slower pace
            RecordResult c = RecordRules.ApplyEndless(rec, Endless(420, 30000, new[] { 3100, 6500, 9000, 20000 }));
            Assert.True(c.NewBestFloors);
            Assert.True(c.NewBestSurvived);
            Assert.False(c.NewBestPace);
            Assert.Equal(2, c.NewSplits);         // stages 3 and 4 are new
            Assert.Equal(new[] { 3000, 6000, 9000, 20000 }, rec.Splits);
        }

        [Fact]
        public void EndlessRevivedOrEmptyRunsChangeNothing()
        {
            var rec = new EndlessModeRecord();
            Assert.False(RecordRules.ApplyEndless(rec, Endless(250, 12000, new[] { 3000 }, revived: true)).Counted);
            Assert.False(RecordRules.ApplyEndless(rec, Endless(0, 100, null)).Counted);
            Assert.Equal(0, rec.BestFloors);
            Assert.Empty(rec.Splits);
        }

        [Fact]
        public void EndlessCurveIsCapped()
        {
            var rec = new EndlessModeRecord();
            var run = Endless(100, 5000, null);
            run.Curve = new int[RecordRules.MaxEndlessCurve + 50];
            RecordRules.ApplyEndless(rec, run);
            Assert.Equal(RecordRules.MaxEndlessCurve, rec.Curve.Length);
        }

        [Fact]
        public void TimeAttackScoreIsTheFloorsAndOnlyImproves()
        {
            var rec = new TimeAttackRecord();
            var run = new RunRecord { Kind = RunKind.TimeAttack, RegionId = "region_001", Floors = 100, Ticks = TimeRecords.TimeAttackTicks };
            Assert.True(RecordRules.ApplyTimeAttack(rec, run).NewBestFloors);
            Assert.Equal(100, rec.BestFloors);
            run.Floors = 80;
            RecordResult lower = RecordRules.ApplyTimeAttack(rec, run);
            Assert.True(lower.Counted);
            Assert.False(lower.NewBestFloors);
            Assert.Equal(100, rec.BestFloors);
            run.Floors = 150; run.Revived = true;
            Assert.False(RecordRules.ApplyTimeAttack(rec, run).Counted);
            Assert.Equal(100, rec.BestFloors);
            Assert.Equal(10800, TimeRecords.TimeAttackTicks);
        }

        [Fact]
        public void CoinsAndGemsPerMinuteAreBestOfASingleRun()
        {
            var rec = new RegionTimedRecord();
            var run = Endless(100, 3600, null);
            run.Coins = 600; run.Gems = 3;
            var r = new RecordResult();
            RecordRules.ApplyCaptures(rec, run, r);
            Assert.True(r.NewBestCoinsPerMin);
            Assert.True(r.NewBestGemsPerMin);
            Assert.Equal(60000, rec.BestCoinsPerMinX100);
            Assert.Equal(300, rec.BestGemsPerMinX100);

            run.Coins = 100;   // a poorer run
            var r2 = new RecordResult();
            RecordRules.ApplyCaptures(rec, run, r2);
            Assert.False(r2.NewBestCoinsPerMin);
            Assert.Equal(60000, rec.BestCoinsPerMinX100);
            Assert.Equal(60000, r2.PrevCoinsPerMinX100);

            run.Coins = 99999; run.Revived = true;
            var r3 = new RecordResult();
            RecordRules.ApplyCaptures(rec, run, r3);
            Assert.False(r3.NewBestCoinsPerMin);
        }

        // ---- save JSON (old saves load, new fields round trip) ----

        [Fact]
        public void ModeRecordsRoundTripThroughJson()
        {
            var records = new ModeRecord[TimeRecords.ModeCount];
            records[2] = new ModeRecord { Floors = 60, Ticks = 4344, BestTimeTicks = 4000, Medal = 2, LayoutSeed = -5, AttemptSeed = 9, HeroId = "momo", Mode = 2, GameVersion = "0.9.1.2", Inputs = new byte[] { 0, 5, 4, 200, 255 }, Curve = new[] { 1, 3, 6 } };
            string json = JsonConvert.SerializeObject(records);
            Assert.Contains("\"f\":60", json);
            ModeRecord[] back = JsonConvert.DeserializeObject<ModeRecord[]>(json);
            Assert.Equal(TimeRecords.ModeCount, back.Length);
            Assert.Null(back[0]);
            Assert.Equal(60, back[2].Floors);
            Assert.Equal(4344, back[2].Ticks);
            Assert.Equal(4000, back[2].BestTimeTicks);
            Assert.Equal(2, back[2].Medal);
            Assert.Equal(-5, back[2].LayoutSeed);
            Assert.Equal("momo", back[2].HeroId);
            Assert.Equal("0.9.1.2", back[2].GameVersion);
            Assert.Equal(new byte[] { 0, 5, 4, 200, 255 }, back[2].Inputs);
            Assert.Equal(new[] { 1, 3, 6 }, back[2].Curve);
        }

        [Fact]
        public void EndlessTimeAttackCapturesAndCountersRoundTripAndOldJsonLoads()
        {
            var endless = new Dictionary<string, EndlessModeRecord[]>();
            endless["region_001"] = new EndlessModeRecord[TimeRecords.ModeCount];
            endless["region_001"][1] = new EndlessModeRecord { BestFloors = 321, PaceFloors = 300, PaceTicks = 9000, BestSurvivedTicks = 9100, Splits = new[] { 2000, 4100 }, Curve = new[] { 4, 9 }, HeroId = "momo" };
            var back = JsonConvert.DeserializeObject<Dictionary<string, EndlessModeRecord[]>>(JsonConvert.SerializeObject(endless));
            Assert.Equal(321, back["region_001"][1].BestFloors);
            Assert.Equal(new[] { 2000, 4100 }, back["region_001"][1].Splits);
            Assert.Null(back["region_001"][0]);

            var ta = new Dictionary<string, TimeAttackRecord[]>();
            ta["region_002"] = new TimeAttackRecord[TimeRecords.ModeCount];
            ta["region_002"][4] = new TimeAttackRecord { BestFloors = 140 };
            Assert.Equal(140, JsonConvert.DeserializeObject<Dictionary<string, TimeAttackRecord[]>>(JsonConvert.SerializeObject(ta))["region_002"][4].BestFloors);

            var counters = new TimedCounters { DayKey = "20261109", PaceClearsToday = 2, WeekKey = "20261109", GoldWeek = 3, TimeAttackFloorsWeek = 100, Split100TodayTicks = 9000 };
            counters.BestPaceLevelsWeek.Add("lvl_001_01");
            TimedCounters c = JsonConvert.DeserializeObject<TimedCounters>(JsonConvert.SerializeObject(counters));
            Assert.Equal(2, c.PaceClearsToday);
            Assert.Equal(3, c.GoldWeek);
            Assert.Equal(9000, c.Split100TodayTicks);
            Assert.Equal(new[] { "lvl_001_01" }, c.BestPaceLevelsWeek.ToArray());

            // older saves simply do not have the fields: defaults, never null containers
            TimedCounters fromOld = JsonConvert.DeserializeObject<TimedCounters>("{}");
            Assert.NotNull(fromOld.BestPaceLevelsWeek);
            Assert.Equal(0, fromOld.GoldWeek);
            ModeRecord m = JsonConvert.DeserializeObject<ModeRecord>("{\"f\":3,\"t\":100}");
            Assert.NotNull(m.Inputs);
            Assert.NotNull(m.Curve);
            Assert.Equal("", m.HeroId);
            EndlessModeRecord e = JsonConvert.DeserializeObject<EndlessModeRecord>("{}");
            Assert.NotNull(e.Splits);
            Assert.Equal(new RegionTimedRecord().BestCoinsPerMinX100, JsonConvert.DeserializeObject<RegionTimedRecord>("{}").BestCoinsPerMinX100);
        }
    }
}
