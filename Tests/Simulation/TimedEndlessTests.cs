using BoingUpSkyTower.Economy;
using Newtonsoft.Json;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// Doc 46 section 7, the logic side (T3): endless splits and live split delta, the 3:00 Time Attack clock and record (score = floors, own
    /// record with seed / hero / version / inputs / curve, no revive), coins and gems per minute, and the stored inputs of the best runs
    /// (the data a server will replay). Pure C#, deterministic. Does not touch the shared medal table.
    /// </summary>
    public class TimedEndlessTests
    {
        static RunRecord Endless(int floors, int ticks, int[] splits, byte[] inputs = null, int mode = 2, bool revived = false)
        {
            return new RunRecord
            {
                Kind = RunKind.Endless, RegionId = "region_001", Floors = floors, Ticks = ticks, Splits = splits, Revived = revived, Mode = mode,
                Curve = new[] { 1, 2 }, HeroId = "momo", GameVersion = "0.51.0.66", LayoutSeed = 4242, Inputs = inputs ?? new byte[] { 1, 2, 3 }
            };
        }

        static RunRecord Ta(int floors, int ticks = TimeRecords.TimeAttackTicks, int mode = 2, byte[] inputs = null)
        {
            return new RunRecord
            {
                Kind = RunKind.TimeAttack, RegionId = "region_001", Floors = floors, Ticks = ticks, Mode = mode, HeroId = "momo", GameVersion = "0.51.0.66",
                LayoutSeed = 99, Inputs = inputs ?? new byte[] { 9, 8, 7 }, Curve = new[] { 1, 3, 6 }
            };
        }

        // ---- splits ----

        [Fact]
        public void SplitStagesAreEveryHundredFloors()
        {
            Assert.Equal(0, TimeRecords.SplitStageOf(0));
            Assert.Equal(0, TimeRecords.SplitStageOf(99));
            Assert.Equal(1, TimeRecords.SplitStageOf(100));
            Assert.Equal(2, TimeRecords.SplitStageOf(250));
            Assert.Equal(300, TimeRecords.SplitStageFloor(3));
            Assert.Equal(0, TimeRecords.SplitStageFloor(0));
        }

        [Fact]
        public void LiveSplitDeltaIsFasterNegativeAndSlowerPositive()
        {
            int[] best = { 3000, 7000, 0 };   // stage 3 never reached
            int d;
            Assert.True(TimeRecords.TrySplitDelta(best, 1, 2700, out d));
            Assert.Equal(-300, d);
            Assert.True(TimeRecords.TrySplitDelta(best, 2, 7600, out d));
            Assert.Equal(600, d);
            Assert.False(TimeRecords.TrySplitDelta(best, 3, 9000, out d));   // no best split yet
            Assert.Equal(0, d);
            Assert.False(TimeRecords.TrySplitDelta(best, 4, 9000, out d));   // beyond the stored stages
            Assert.False(TimeRecords.TrySplitDelta(best, 0, 9000, out d));
            Assert.False(TimeRecords.TrySplitDelta(null, 1, 9000, out d));
        }

        [Fact]
        public void BestSplitsOnlyGetFasterAndSurviveSlowerRuns()
        {
            var rec = new EndlessModeRecord();
            RecordRules.ApplyEndless(rec, Endless(210, 9000, new[] { 3000, 8000 }));
            RecordResult slower = RecordRules.ApplyEndless(rec, Endless(210, 9500, new[] { 3500, 8500 }));
            Assert.Equal(0, slower.NewSplits);
            Assert.Equal(new[] { 3000, 8000 }, rec.Splits);
            RecordResult faster = RecordRules.ApplyEndless(rec, Endless(110, 4000, new[] { 2900 }));
            Assert.Equal(1, faster.NewSplits);
            Assert.Equal(new[] { 2900, 8000 }, rec.Splits);   // the second stage keeps its own best
        }

        [Fact]
        public void SplitsAreCappedAtThirtyStages()
        {
            var rec = new EndlessModeRecord();
            var splits = new int[40];
            for (int i = 0; i < splits.Length; i++) splits[i] = 1000 * (i + 1);
            RecordRules.ApplyEndless(rec, Endless(4000, 60000, splits));
            Assert.Equal(TimeRecords.MaxSplits, rec.Splits.Length);
        }

        // ---- endless best run data (server check) ----

        [Fact]
        public void EndlessBestPaceRunKeepsSeedHeroVersionModeAndInputs()
        {
            var rec = new EndlessModeRecord();
            RecordRules.ApplyEndless(rec, Endless(100, 6000, null, new byte[] { 5, 6, 7, 8 }, 3));
            Assert.Equal(4242, rec.LayoutSeed);
            Assert.Equal("momo", rec.HeroId);
            Assert.Equal("0.51.0.66", rec.GameVersion);
            Assert.Equal(3, rec.Mode);
            Assert.Equal(new byte[] { 5, 6, 7, 8 }, rec.Inputs);

            // a slower run does not replace the stored best-pace run, a faster one does
            RecordRules.ApplyEndless(rec, Endless(100, 9000, null, new byte[] { 1 }, 3));
            Assert.Equal(new byte[] { 5, 6, 7, 8 }, rec.Inputs);
            RecordRules.ApplyEndless(rec, Endless(100, 5000, null, new byte[] { 2, 2 }, 3));
            Assert.Equal(new byte[] { 2, 2 }, rec.Inputs);
        }

        [Fact]
        public void LongInputsAreKeptUpToTheLongCapAndDroppedAboveIt()
        {
            var big = new byte[RecordRules.MaxLongInputBytes];
            var tooBig = new byte[RecordRules.MaxLongInputBytes + 1];
            var rec = new EndlessModeRecord();
            RecordRules.ApplyEndless(rec, Endless(100, 6000, null, big));
            Assert.Equal(big.Length, rec.Inputs.Length);
            var rec2 = new EndlessModeRecord();
            RecordResult r = RecordRules.ApplyEndless(rec2, Endless(100, 6000, null, tooBig));
            Assert.True(r.NewBestPace);               // the record counts
            Assert.Empty(rec2.Inputs);                // only the inputs are dropped
            Assert.True(RecordRules.MaxLongInputBytes > RecordRules.MaxInputBytes);
        }

        // ---- Time Attack ----

        [Fact]
        public void TimeAttackClockIsExactlyThreeMinutes()
        {
            Assert.Equal(10800, TimeRecords.TimeAttackTicks);
            Assert.Equal(10800, TimeRecords.TimeAttackTicksLeft(0));
            Assert.Equal(10800, TimeRecords.TimeAttackTicksLeft(-4));
            Assert.Equal(1, TimeRecords.TimeAttackTicksLeft(10799));
            Assert.Equal(0, TimeRecords.TimeAttackTicksLeft(10800));
            Assert.Equal(0, TimeRecords.TimeAttackTicksLeft(99999));
            Assert.False(TimeRecords.TimeAttackExpired(10799));
            Assert.True(TimeRecords.TimeAttackExpired(10800));
            Assert.Equal("03:00.0", TimeRecords.FormatTime(TimeRecords.TimeAttackTicks));
        }

        [Fact]
        public void TimeAttackStoresTheBestFloorsRunAndOnlyImproves()
        {
            var rec = new TimeAttackRecord();
            RecordResult first = RecordRules.ApplyTimeAttack(rec, Ta(100));
            Assert.True(first.Counted);
            Assert.True(first.NewBestFloors);
            Assert.True(first.FirstRecord);
            Assert.Equal(100, rec.BestFloors);
            Assert.Equal(TimeRecords.TimeAttackTicks, rec.Ticks);
            Assert.Equal(99, rec.LayoutSeed);
            Assert.Equal("momo", rec.HeroId);
            Assert.Equal("0.51.0.66", rec.GameVersion);
            Assert.Equal(2, rec.Mode);
            Assert.Equal(new byte[] { 9, 8, 7 }, rec.Inputs);
            Assert.Equal(new[] { 1, 3, 6 }, rec.Curve);

            RecordResult worse = RecordRules.ApplyTimeAttack(rec, Ta(90, inputs: new byte[] { 1 }));
            Assert.True(worse.Counted);
            Assert.False(worse.NewBestFloors);
            Assert.Equal(100, worse.PrevBestFloors);
            Assert.Equal(new byte[] { 9, 8, 7 }, rec.Inputs);

            RecordResult equal = RecordRules.ApplyTimeAttack(rec, Ta(100, inputs: new byte[] { 2 }));
            Assert.False(equal.NewBestFloors);   // equal floors keep the first run
            Assert.Equal(new byte[] { 9, 8, 7 }, rec.Inputs);

            RecordResult better = RecordRules.ApplyTimeAttack(rec, Ta(130, 10000, 2, new byte[] { 4, 4 }));
            Assert.True(better.NewBestFloors);
            Assert.Equal(130, rec.BestFloors);
            Assert.Equal(10000, rec.Ticks);       // fell before 3:00: still a valid score
            Assert.Equal(new byte[] { 4, 4 }, rec.Inputs);
        }

        [Fact]
        public void TimeAttackHasNoReviveAndZeroFloorsCountForNothing()
        {
            var rec = new TimeAttackRecord();
            RunRecord revived = Ta(500);
            revived.Revived = true;
            Assert.False(RecordRules.ApplyTimeAttack(rec, revived).Counted);
            Assert.Equal(0, rec.BestFloors);
            Assert.False(RecordRules.ApplyTimeAttack(rec, Ta(0)).Counted);
            Assert.Equal(0, rec.BestFloors);
        }

        [Fact]
        public void TimeAttackRecordsPerModeAreSeparateArrays()
        {
            var arr = new TimeAttackRecord[TimeRecords.ModeCount];
            for (int m = 0; m < TimeRecords.ModeCount; m++)
            {
                arr[m] = new TimeAttackRecord();
                RecordRules.ApplyTimeAttack(arr[m], Ta(50 + m * 10, mode: m));
            }
            for (int m = 0; m < TimeRecords.ModeCount; m++)
            {
                Assert.Equal(50 + m * 10, arr[m].BestFloors);
                Assert.Equal(m, arr[m].Mode);
            }
        }

        // ---- per minute stats ----

        [Fact]
        public void RatePerMinuteAndItsText()
        {
            Assert.Equal(60000, TimeRecords.RatePerMinute(600, 3600));   // 600 coins in one minute = 600.00 /min
            Assert.Equal(300, TimeRecords.RatePerMinute(3, 3600));
            Assert.Equal(0, TimeRecords.RatePerMinute(0, 3600));
            Assert.Equal(0, TimeRecords.RatePerMinute(10, 0));
            Assert.Equal(0, TimeRecords.RatePerMinute(-5, 3600));
            Assert.Equal(TimeRecords.PerMinuteX100(77, 5000), TimeRecords.RatePerMinute(77, 5000));
            Assert.Equal("600.00/min", TimeRecords.FormatPerMinute(60000));
            Assert.Equal("0.05/min", TimeRecords.FormatPerMinute(5));
            Assert.Equal("3.00/min", TimeRecords.FormatPerMinute(300));
        }

        [Fact]
        public void CapturesAreBestOfASingleRunAndRevivedRunsAreExcluded()
        {
            var rec = new RegionTimedRecord();
            RunRecord run = Ta(100, 10800);
            run.Coins = 540; run.Gems = 6;   // 3 minutes: 180 coins and 2 gems per minute
            var r = new RecordResult();
            RecordRules.ApplyCaptures(rec, run, r);
            Assert.Equal(18000, rec.BestCoinsPerMinX100);
            Assert.Equal(200, rec.BestGemsPerMinX100);
            Assert.True(r.NewBestCoinsPerMin && r.NewBestGemsPerMin);

            run.Coins = 100; run.Gems = 1;
            var r2 = new RecordResult();
            RecordRules.ApplyCaptures(rec, run, r2);
            Assert.False(r2.NewBestCoinsPerMin || r2.NewBestGemsPerMin);
            Assert.Equal(18000, rec.BestCoinsPerMinX100);

            run.Coins = 99999; run.Revived = true;
            var r3 = new RecordResult();
            RecordRules.ApplyCaptures(rec, run, r3);
            Assert.False(r3.NewBestCoinsPerMin);
            Assert.Equal(18000, rec.BestCoinsPerMinX100);
        }

        // ---- save JSON ----

        [Fact]
        public void NewEndlessAndTimeAttackFieldsRoundTripAndOldJsonStillLoads()
        {
            var e = new EndlessModeRecord { BestFloors = 321, Mode = 3, Inputs = new byte[] { 0, 200, 255 }, Splits = new[] { 2000 } };
            EndlessModeRecord e2 = JsonConvert.DeserializeObject<EndlessModeRecord>(JsonConvert.SerializeObject(e));
            Assert.Equal(3, e2.Mode);
            Assert.Equal(new byte[] { 0, 200, 255 }, e2.Inputs);
            Assert.Equal(new[] { 2000 }, e2.Splits);

            var t = new TimeAttackRecord { BestFloors = 140, Ticks = 10800, LayoutSeed = -7, HeroId = "capi", Mode = 4, GameVersion = "1.0.0.1", Inputs = new byte[] { 3, 4 }, Curve = new[] { 2, 5 }, RampStartLevel = 10 };
            TimeAttackRecord t2 = JsonConvert.DeserializeObject<TimeAttackRecord>(JsonConvert.SerializeObject(t));
            Assert.Equal(140, t2.BestFloors);
            Assert.Equal(-7, t2.LayoutSeed);
            Assert.Equal("capi", t2.HeroId);
            Assert.Equal(4, t2.Mode);
            Assert.Equal(new byte[] { 3, 4 }, t2.Inputs);
            Assert.Equal(new[] { 2, 5 }, t2.Curve);
            Assert.Equal(10, t2.RampStartLevel);

            // an old save with only the floors
            TimeAttackRecord old = JsonConvert.DeserializeObject<TimeAttackRecord>("{\"bf\":77}");
            Assert.Equal(77, old.BestFloors);
            Assert.NotNull(old.Inputs);
            Assert.NotNull(old.Curve);
            Assert.Equal("", old.HeroId);
            Assert.Equal(0, old.RampStartLevel);   // legacy record of the flat-start Time Attack
            Assert.False(TimeAttackRamp.IsCurrent(old));
            EndlessModeRecord oldE = JsonConvert.DeserializeObject<EndlessModeRecord>("{\"bf\":10,\"sp\":[100]}");
            Assert.NotNull(oldE.Inputs);
            Assert.Equal(new[] { 100 }, oldE.Splits);
        }

        // ---- determinism: seed plus the recorded inputs replay an endless run ----

        static void Drive(EndlessWorld world, ref SimState s, int input)
        {
            world.Sim.Step(ref s, input);
            world.Grow(ref s);   // exactly as RunSession does after every tick
        }

        [Fact]
        public void ARecordedEndlessRunReplaysFromSeedAndInputsAndTheThreeMinuteClockStopsIt()
        {
            var cfg = new SimConfig();
            LevelSpec spec = EndlessTower.MakeSpec(20261109, Fx.FromRatio(0, 10000), SignatureKind.BounceFlowers, 400);
            var world = new EndlessWorld(cfg, spec);
            SimState s = world.NewState();
            var rec = new InputRecorder();
            uint seed = 7;
            int ticks = 0;
            while (!s.Dead && ticks < 14000)
            {
                seed = seed * 1664525u + 1013904223u;
                int input = SimInput.Jump | ((seed >> 20) % 3 == 0 ? SimInput.Left : SimInput.Right);
                rec.Add(input);
                Drive(world, ref s, input);
                ticks++;
                int run = s.StartTick < 0 ? 0 : s.Tick - s.StartTick;
                if (TimeRecords.TimeAttackExpired(run)) break;   // what RunSession does in Time Attack
            }
            Assert.True(s.StartTick >= 0, "the scripted run jumped");
            int elapsed = s.Tick - s.StartTick;
            if (!s.Dead) Assert.Equal(TimeRecords.TimeAttackTicks, elapsed);   // the clock stops the run on exactly 10800 ticks
            else Assert.True(elapsed < TimeRecords.TimeAttackTicks);

            int[] inputs = InputCodec.Decode(rec.ToBytes());
            Assert.Equal(ticks, inputs.Length);
            var world2 = new EndlessWorld(new SimConfig(), EndlessTower.MakeSpec(20261109, Fx.FromRatio(0, 10000), SignatureKind.BounceFlowers, 400));
            SimState r = world2.NewState();
            foreach (int input in inputs) Drive(world2, ref r, input);
            Assert.Equal(s.Row, r.Row);
            Assert.Equal(s.Tick, r.Tick);
            Assert.Equal(s.StartTick, r.StartTick);
            Assert.Equal(s.Score, r.Score);
            Assert.Equal(s.Dead, r.Dead);
            Assert.Equal(s.Coins, r.Coins);
        }

        // ---- Time Attack difficulty ramp (owner Bug List 8 item 3): floor 1 = level 10, +1 level per 100 floors, level 25 held ----

        [Theory]
        [InlineData(0, 10)]
        [InlineData(1, 10)]
        [InlineData(100, 10)]
        [InlineData(101, 11)]
        [InlineData(200, 11)]
        [InlineData(201, 12)]
        [InlineData(1500, 24)]
        [InlineData(1501, 25)]
        [InlineData(1600, 25)]
        [InlineData(1601, 25)]
        [InlineData(5000, 25)]
        [InlineData(100000, 25)]
        [InlineData(int.MaxValue, 25)]
        public void TimeAttackStartsAtLevel10RisesOneLevelPerHundredFloorsAndHoldsLevel25(int floor, int level)
        {
            Assert.Equal(level, EndlessTower.LevelForFloorTimeAttack(floor, 25));
            Assert.Equal(level, EndlessTower.LevelForFloor(floor, 25, true));
        }

        [Fact]
        public void TimeAttackLevelIsNeverBelowTenNeverFallsAndNeverEntersTheEndlessCycle()
        {
            int prev = 10;
            for (int f = -5; f <= 30000; f++)
            {
                int l = EndlessTower.LevelForFloorTimeAttack(f, 25);
                Assert.InRange(l, 10, 25);
                Assert.True(l >= prev, "level fell at floor " + f);
                prev = l;
            }
            Assert.Equal(25, prev);
            // a normal endless tower is untouched by the flag
            for (int f = 0; f <= 4000; f++) Assert.Equal(EndlessTower.LevelForFloor(f, 25), EndlessTower.LevelForFloor(f, 25, false));
            // legacy 15-level regions: levels 10..15, then hold 15
            Assert.Equal(10, EndlessTower.LevelForFloorTimeAttack(1, 15));
            Assert.Equal(15, EndlessTower.LevelForFloorTimeAttack(501, 15));
            Assert.Equal(15, EndlessTower.LevelForFloorTimeAttack(9999, 15));
        }

        [Fact]
        public void TimeAttackTowerUsesLevel10DifficultyFromFloor1AndStaysPrefixStableAndReplayable()
        {
            var cfg = new SimConfig();
            Fx off = Fx.FromRatio(500, 10000);
            LevelSpec ta = EndlessTower.MakeSpec(777, off, SignatureKind.BounceFlowers, 400, 25, true);
            LevelSpec en = EndlessTower.MakeSpec(777, off, SignatureKind.BounceFlowers, 400);
            Assert.True(ta.TimeAttack);
            Assert.False(en.TimeAttack);
            var tTa = new EndlessTower(cfg, ta);
            var tEn = new EndlessTower(cfg, en);
            Assert.True(tTa.DifficultyAt(1) == LevelSpec.DifficultyForLevel(cfg, 10, off, 25));
            Assert.True(tTa.DifficultyAt(150) == LevelSpec.DifficultyForLevel(cfg, 11, off, 25));
            Assert.True(tTa.DifficultyAt(3000) == LevelSpec.DifficultyForLevel(cfg, 25, off, 25));
            Assert.True(tEn.DifficultyAt(1) == LevelSpec.DifficultyForLevel(cfg, 1, off, 25));
            Assert.True(tTa.DifficultyAt(1) > tEn.DifficultyAt(1));
            Assert.True(tTa.Data.EndlessTimeAttack);
            Assert.False(tEn.Data.EndlessTimeAttack);
            Assert.NotEqual(tTa.Fingerprint, tEn.Fingerprint);   // the Time Attack tower is a different tower than the endless one of the same seed

            // prefix stable: growing never changes what exists, and a second tower of the same spec grown in another order is identical
            var a = new EndlessTower(cfg, ta);
            Platform[] early = a.Data.Platforms;
            a.EnsureAhead(400);
            var b = new EndlessTower(cfg, EndlessTower.MakeSpec(777, off, SignatureKind.BounceFlowers, 400, 25, true));
            b.EnsureAhead(200);
            b.EnsureAhead(400);
            Assert.Equal(a.Fingerprint, b.Fingerprint);
            Platform[] pa = a.Data.Platforms, pb = b.Data.Platforms;
            Assert.Equal(pa.Length, pb.Length);
            for (int i = 0; i < pa.Length; i++)
                Assert.True(pa[i].X == pb[i].X && pa[i].Y == pb[i].Y && pa[i].Width == pb[i].Width && pa[i].Kind == pb[i].Kind, "platform " + i);
            for (int i = 0; i < early.Length; i++)
                Assert.True(early[i].Y == pa[i].Y && early[i].Kind == pa[i].Kind && early[i].X == pa[i].X, "prefix platform " + i);
        }

        [Fact]
        public void TimeAttackScrollStartsAtTheLevel10SpeedAndEndlessScrollStillStartsAtLevel1()
        {
            var cfg = new SimConfig();
            var ta = new EndlessWorld(cfg, EndlessTower.MakeSpec(5, Fx.Zero, SignatureKind.None, 400, 25, true));
            var en = new EndlessWorld(cfg, EndlessTower.MakeSpec(5, Fx.Zero, SignatureKind.None, 400));
            SimState s1 = ta.NewState(), s2 = en.NewState();
            for (int i = 0; i < 120; i++) { Drive(ta, ref s1, SimInput.Jump); Drive(en, ref s2, SimInput.Jump); }
            Assert.True(ta.Sim.ScrollPerTickAt(s1, s1.Tick) > en.Sim.ScrollPerTickAt(s2, s2.Tick), "the Time Attack screen scrolls faster at the start (level 10 speed)");
        }

        [Fact]
        public void ALegacyFlatStartTimeAttackRecordIsReplacedByTheFirstCountedRunEvenWithFewerFloors()
        {
            var rec = new TimeAttackRecord { BestFloors = 500 };   // no ramp stamp: set on the old flat start
            RecordResult r = RecordRules.ApplyTimeAttack(rec, Ta(40));
            Assert.True(r.Counted);
            Assert.True(r.FirstRecord);
            Assert.True(r.NewBestFloors);
            Assert.Equal(40, rec.BestFloors);
            Assert.Equal(TimeAttackRamp.StartLevel, rec.RampStartLevel);
            Assert.Equal(10, TimeAttackRamp.StartLevel);
            RecordResult worse = RecordRules.ApplyTimeAttack(rec, Ta(30));
            Assert.False(worse.NewBestFloors);
            Assert.Equal(40, rec.BestFloors);
        }
}
}
