using System;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Timed records (doc 46) through ProgressService: RecordRun, the queries, medals, the reward counters, old saves, and the RunSession clock.</summary>
    public class TimeRecordsEditTests
    {
        ContentRepository repo;
        RegionDef region1, region2;
        string lvl1, lvl2, lvl3;
        MemorySaveStore store;
        ProgressService svc;
        IMedalTargetSource savedSource;
        DateTime now;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            region1 = repo.GetRegionByIndex(1);
            region2 = repo.GetRegionByIndex(2);
            lvl1 = region1.Levels[0];
            lvl2 = region1.Levels[1];
            lvl3 = region1.Levels[2];
            ProgressService.TestUnlockAll = false;
            savedSource = MedalTargets.Source;
            store = new MemorySaveStore();
            svc = new ProgressService(repo, store, 5);
            now = new DateTime(2026, 11, 9, 12, 0, 0);   // a Monday
            svc.Clock = () => now;
        }

        [TearDown]
        public void TearDown()
        {
            MedalTargets.Source = savedSource;
        }

        static RunRecord LevelRun(string levelId, int floors, int ticks, int mode = 2, bool revived = false)
        {
            return new RunRecord { Kind = RunKind.Level, LevelId = levelId, Floors = floors, Ticks = ticks, Mode = mode, Revived = revived, HeroId = "momo", GameVersion = "0.9.9.1", LayoutSeed = 11, AttemptSeed = 3, Inputs = new byte[] { 0, 10, 4, 1 }, Curve = new[] { 1, 3, 6 } };
        }

        [Test]
        public void ARunIsSavedAndReadBackThroughTheQueriesAndAfterAReload()
        {
            RecordResult r = svc.RecordRun(LevelRun(lvl1, 60, 4344));
            Assert.IsTrue(r.Counted);
            Assert.IsTrue(r.NewBestPace);
            Assert.IsTrue(r.NewBestTime);
            Assert.AreEqual(60, svc.BestPace(lvl1, 2).Floors);
            Assert.AreEqual(4344, svc.BestPace(lvl1, 2).Ticks);
            Assert.AreEqual(4344, svc.BestTime(lvl1, 2));
            Assert.AreEqual(new[] { 1, 3, 6 }, svc.BestCurve(lvl1, 2));
            Assert.AreEqual(MedalLevel.None, svc.Medal(lvl1, 2));      // no target data yet

            var again = new ProgressService(repo, store, 5);           // the save was written at once
            Assert.AreEqual(60, again.BestPace(lvl1, 2).Floors);
            Assert.AreEqual(4344, again.BestTime(lvl1, 2));
            ModeRecord rec = again.GetModeRecord(lvl1, 2);
            Assert.AreEqual("momo", rec.HeroId);
            Assert.AreEqual("0.9.9.1", rec.GameVersion);
            Assert.AreEqual(11, rec.LayoutSeed);
            Assert.AreEqual(new byte[] { 0, 10, 4, 1 }, rec.Inputs);
        }

        [Test]
        public void RecordsOnlyImproveAndTheTimeIsKeptApartFromThePace()
        {
            svc.RecordRun(LevelRun(lvl1, 60, 4344));
            RecordResult slow = svc.RecordRun(LevelRun(lvl1, 40, 4344));
            Assert.IsFalse(slow.NewBestPace);
            Assert.AreEqual(60, svc.BestPace(lvl1, 2).Floors);
            RecordResult timeOnly = svc.RecordRun(LevelRun(lvl1, 30, 4000));   // worse pace, shorter time
            Assert.IsFalse(timeOnly.NewBestPace);
            Assert.IsTrue(timeOnly.NewBestTime);
            Assert.AreEqual(60, svc.BestPace(lvl1, 2).Floors);
            Assert.AreEqual(4000, svc.BestTime(lvl1, 2));
            RecordResult fast = svc.RecordRun(LevelRun(lvl1, 60, 3000));
            Assert.IsTrue(fast.NewBestPace);
            Assert.AreEqual(3000, svc.BestPace(lvl1, 2).Ticks);
            Assert.AreEqual(3000, svc.BestTime(lvl1, 2));
            Assert.AreEqual(3000 - 4000, fast.DeltaTicks);
        }

        [Test]
        public void RevivedRunsNeverSetARecordOrSaveAnything()
        {
            RecordResult r = svc.RecordRun(LevelRun(lvl1, 60, 4344, 2, true));
            Assert.IsFalse(r.Counted);
            Assert.IsTrue(r.Revived);
            Assert.AreEqual(60, r.Floors);
            Assert.AreEqual(83, r.PaceHundredths);
            Assert.IsFalse(svc.BestPace(lvl1, 2).IsValid);
            Assert.AreEqual(0, svc.BestTime(lvl1, 2));
            Assert.IsNull(svc.GetProgress(lvl1), "a revived run does not even create a level entry");
            svc.RecordRun(LevelRun(lvl1, 10, 3000));
            svc.RecordRun(LevelRun(lvl1, 500, 1000, 2, true));
            Assert.AreEqual(10, svc.BestPace(lvl1, 2).Floors);
        }

        [Test]
        public void TheFiveModesAreSeparateAndPerLevel()
        {
            svc.RecordRun(LevelRun(lvl1, 60, 4344, 0));
            svc.RecordRun(LevelRun(lvl1, 20, 4344, 4));
            svc.RecordRun(LevelRun(lvl2, 30, 4344, 0));
            Assert.AreEqual(60, svc.BestPace(lvl1, 0).Floors);
            Assert.AreEqual(20, svc.BestPace(lvl1, 4).Floors);
            Assert.IsFalse(svc.BestPace(lvl1, 1).IsValid);
            Assert.IsFalse(svc.BestPace(lvl1, 2).IsValid);
            Assert.IsFalse(svc.BestPace(lvl1, 3).IsValid);
            Assert.AreEqual(30, svc.BestPace(lvl2, 0).Floors);
            Assert.IsFalse(svc.BestPace(lvl2, 4).IsValid);
            Assert.IsFalse(svc.BestPace(lvl1, 7).IsValid, "an invalid mode is just empty");
            Assert.IsFalse(svc.BestPace("no_such_level", 0).IsValid);
            Assert.AreEqual(0, svc.BestCurve("no_such_level", 0).Length);
        }

        [Test]
        public void MedalsFollowTheTargetDataAndGoldIsCountedPerRegionOverLevelsAndModes()
        {
            var table = new DictionaryMedalTargets();
            table.Set(lvl1, 0, 1000);
            table.Set(lvl1, 1, 1000);
            table.Set(lvl2, 0, 1000);
            table.Set(lvl3, 0, 1000);
            MedalTargets.Source = table;
            RecordResult silver = svc.RecordRun(LevelRun(lvl1, 7, 600, 0));     // 0.70 plat/s against 1.0
            Assert.AreEqual(MedalLevel.Silver, silver.Medal);
            Assert.AreEqual(MedalLevel.Silver, svc.Medal(lvl1, 0));
            RecordResult gold = svc.RecordRun(LevelRun(lvl1, 17, 1200, 0));
            Assert.IsTrue(gold.NewGold);
            svc.RecordRun(LevelRun(lvl1, 17, 1200, 1));                          // gold in a second mode of the same level
            svc.RecordRun(LevelRun(lvl2, 17, 1200, 0));
            svc.RecordRun(LevelRun(lvl3, 1, 1200, 0));                           // below bronze
            Assert.AreEqual(3, svc.GoldMedalsInRegion(1));
            Assert.AreEqual(0, svc.GoldMedalsInRegion(2));
            Assert.AreEqual(3, svc.GoldMedalsTotal());
            Assert.AreEqual(MedalLevel.None, svc.Medal(lvl3, 0));
            Assert.AreEqual(MedalLevel.Gold, svc.BestMedal(lvl1));
            // the medal survives a later weak run
            svc.RecordRun(LevelRun(lvl1, 1, 1200, 0));
            Assert.AreEqual(MedalLevel.Gold, svc.Medal(lvl1, 0));
            // and a revived run cannot earn one
            RecordResult cheat = svc.RecordRun(LevelRun(lvl3, 50, 1200, 0, true));
            Assert.AreEqual(MedalLevel.None, cheat.Medal);
            Assert.AreEqual(MedalLevel.None, svc.Medal(lvl3, 0));
        }

        [Test]
        public void TheRewardCountersFollowTheDayAndTheWeek()
        {
            svc.RecordRun(LevelRun(lvl1, 60, 4344));           // 0.83 plat/s: a pace clear and a new best
            svc.RecordRun(LevelRun(lvl2, 6, 600));             // exactly 0.60 plat/s: counts
            svc.RecordRun(LevelRun(lvl3, 5, 600));             // 0.50: no pace clear, still a new best
            svc.RecordRun(LevelRun(lvl1, 30, 4344));           // 0.41 plat/s and worse than the best: nothing
            svc.RecordRun(LevelRun(lvl1, 600, 1000, 2, true)); // revived: nothing
            Assert.AreEqual(2, svc.TimedPaceClearsToday());
            Assert.AreEqual(3, svc.TimedBestPaceLevelsThisWeek());

            now = now.AddDays(1);                              // Tuesday: a new day, the same week
            Assert.AreEqual(0, svc.TimedPaceClearsToday());
            Assert.AreEqual(3, svc.TimedBestPaceLevelsThisWeek());
            svc.RecordRun(LevelRun(lvl1, 80, 4344));           // a better pace again, level 1 is already counted this week
            Assert.AreEqual(3, svc.TimedBestPaceLevelsThisWeek());
            Assert.AreEqual(1, svc.TimedPaceClearsToday());

            now = now.AddDays(6);                              // next Monday: a new week
            Assert.AreEqual(0, svc.TimedBestPaceLevelsThisWeek());
            Assert.AreEqual(0, svc.TimedGoldThisWeek());
        }

        [Test]
        public void GoldMedalsEarnedThisWeekAreCountedOncePerNewGold()
        {
            var table = new DictionaryMedalTargets();
            table.Set(lvl1, 2, 1000);
            MedalTargets.Source = table;
            svc.RecordRun(LevelRun(lvl1, 17, 1200));
            svc.RecordRun(LevelRun(lvl1, 18, 1200));           // gold again: not a new medal
            Assert.AreEqual(1, svc.TimedGoldThisWeek());
        }

        [Test]
        public void EndlessAndTimeAttackRecordsAreSavedPerRegionAndMode()
        {
            var run = new RunRecord { Kind = RunKind.Endless, RegionId = region1.Id, Floors = 250, Ticks = 12000, Mode = 3, Splits = new[] { 3000, 7000 }, Curve = new[] { 5, 10 }, Coins = 600 };
            RecordResult r = svc.RecordRun(run);
            Assert.IsTrue(r.NewBestFloors);
            Assert.IsTrue(r.NewBestPace);
            Assert.AreEqual(250, svc.EndlessBestFloorsInMode(region1.Id, 3));
            Assert.AreEqual(0, svc.EndlessBestFloorsInMode(region1.Id, 2));
            Assert.AreEqual(12000, svc.EndlessBestSurvived(region1.Id, 3));
            Assert.AreEqual(250, svc.EndlessBestPace(region1.Id, 3).Floors);
            Assert.AreEqual(3000, svc.EndlessBestSplit(region1.Id, 3, 1));
            Assert.AreEqual(7000, svc.EndlessBestSplit(region1.Id, 3, 2));
            Assert.AreEqual(0, svc.EndlessBestSplit(region1.Id, 3, 3));
            Assert.AreEqual(new[] { 5, 10 }, svc.EndlessBestCurve(region1.Id, 3));
            Assert.AreEqual(3000, svc.TimedSplit100TodayTicks());
            Assert.AreEqual(3000, new ProgressService(repo, store, 5).EndlessBestSplit(region1.Id, 3, 1));   // saved
            Assert.AreEqual(TimeRecords.PerMinuteX100(600, 12000), svc.BestCoinsPerMinX100(region1.Id));
            Assert.AreEqual(0, svc.EndlessBestFloor(region1.Id), "the old per-region best floor is still ApplyEndlessRun's job");

            var ta = new RunRecord { Kind = RunKind.TimeAttack, RegionId = region2.Id, Floors = 100, Ticks = TimeRecords.TimeAttackTicks, Mode = 1 };
            Assert.IsTrue(svc.RecordRun(ta).NewBestFloors);
            ta.Floors = 90;
            Assert.IsFalse(svc.RecordRun(ta).NewBestFloors);
            Assert.AreEqual(100, svc.TimeAttackBestFloors(region2.Id, 1));
            Assert.AreEqual(0, svc.TimeAttackBestFloors(region2.Id, 0));
            Assert.AreEqual(100, svc.TimedTimeAttackFloorsThisWeek());
        }

        [Test]
        public void OldSavesWithoutTheNewFieldsLoadAndWork()
        {
            const string oldJson = "{\"version\":3,\"coins\":50,\"gems\":2,\"levels\":{\"lvl_001_01\":{\"stars\":2,\"starMask\":3,\"bestScore\":900,\"clears\":4}},\"endlessBest\":{\"region_001_sunny_meadow\":120}}";
            SaveData data = JsonConvert.DeserializeObject<SaveData>(oldJson);
            Assert.IsNull(data.Levels["lvl_001_01"].Records);
            var legacy = new ProgressService(repo, new FixedStore(data), 5);
            Assert.AreEqual(4, legacy.GetProgress("lvl_001_01").Clears);
            Assert.IsFalse(legacy.BestPace("lvl_001_01", 0).IsValid);
            Assert.AreEqual(MedalLevel.None, legacy.Medal("lvl_001_01", 0));
            Assert.AreEqual(0, legacy.GoldMedalsInRegion(1));
            Assert.AreEqual(0, legacy.TimedPaceClearsToday());
            RecordResult r = legacy.RecordRun(LevelRun("lvl_001_01", 60, 4344));   // records join the old level entry
            Assert.IsTrue(r.NewBestPace);
            Assert.AreEqual(4, legacy.GetProgress("lvl_001_01").Clears);
            Assert.AreEqual(2, legacy.GetProgress("lvl_001_01").Stars);
        }

        [Test]
        public void NullContainersInAHandEditedSaveAreRepaired()
        {
            var data = new SaveData { EndlessRecords = null, TimeAttackRecords = null, RegionTimed = null, TimedCounters = null };
            var s = new ProgressService(repo, new FixedStore(data), 5);
            Assert.AreEqual(0, s.TimeAttackBestFloors(region1.Id, 0));
            Assert.AreEqual(0, s.BestCoinsPerMinX100(region1.Id));
            Assert.AreEqual(0, s.TimedGoldThisWeek());
        }

        [Test]
        public void TheSaveWithRecordsRoundTripsThroughJson()
        {
            svc.RecordRun(LevelRun(lvl1, 60, 4344, 3));
            string json = JsonConvert.SerializeObject(svc.Data);
            SaveData back = JsonConvert.DeserializeObject<SaveData>(json);
            ModeRecord rec = back.Levels[lvl1].Records[3];
            Assert.AreEqual(60, rec.Floors);
            Assert.AreEqual(4344, rec.Ticks);
            Assert.AreEqual(new[] { 1, 3, 6 }, rec.Curve);
            Assert.IsNull(back.Levels[lvl1].Records[0]);
        }

        [Test]
        public void ResetAllClearsTheRecords()
        {
            svc.RecordRun(LevelRun(lvl1, 60, 4344));
            svc.ResetAll();
            Assert.IsFalse(svc.BestPace(lvl1, 2).IsValid);
            Assert.AreEqual(0, svc.TimedPaceClearsToday());
        }

        sealed class FixedStore : ISaveStore
        {
            readonly SaveData data;
            public FixedStore(SaveData d) { data = d; }
            public SaveData Load() { return data; }
            public void Save(SaveData d) { }
        }

        // ---- the run clock (RunSession) ----

        static SimInputScript Script() { return new SimInputScript(); }

        sealed class SimInputScript
        {
            public int At(int tick) { return tick < 30 ? 0 : (tick % 40 < 6 ? SimInput.Jump : SimInput.Right); }
        }

        [Test]
        public void TheRunClockStartsAtTheFirstJumpAndStopsWithTheRun()
        {
            var cfg = new SimConfig();
            var session = new RunSession(repo, cfg, lvl1, false, 7);
            session.Mode = 1;
            session.HeroId = "momo";
            Assert.AreEqual(0, session.RunTicks, "no clock before the first jump");
            for (int t = 0; t < 29; t++) session.Step(0);
            Assert.AreEqual(0, session.RunTicks, "idle ticks before the first jump do not count");
            var script = Script();
            int guard = 0;
            while (session.State.StartTick < 0 && guard++ < 400) session.Step(SimInput.Jump);
            Assert.GreaterOrEqual(session.State.StartTick, 0);
            int first = session.State.StartTick;
            for (int t = 0; t < 120 && !session.Over; t++) session.Step(script.At(t));
            Assert.AreEqual(session.State.Tick - first, session.RunTicks);
            Assert.AreEqual(session.State.Row, session.RunFloors);
            Assert.IsFalse(session.RunRevived);

            RunRecord rec = session.BuildRunRecord("1.0.0.1");
            Assert.AreEqual(RunKind.Level, rec.Kind);
            Assert.AreEqual(lvl1, rec.LevelId);
            Assert.AreEqual(session.RunTicks, rec.Ticks);
            Assert.AreEqual(session.State.Row, rec.Floors);
            Assert.AreEqual(session.Spec.Seed, rec.LayoutSeed);
            Assert.AreEqual(7, rec.AttemptSeed);
            Assert.AreEqual(1, rec.Mode);
            Assert.AreEqual("momo", rec.HeroId);
            Assert.AreEqual("1.0.0.1", rec.GameVersion);
            Assert.IsFalse(rec.Revived);
            Assert.AreEqual(session.State.Tick, InputCodec.DecodedLength(rec.Inputs), "one coded input per simulated tick");
            Assert.AreEqual(session.RunTicks / TimeRecords.TicksPerSecond, rec.Curve.Length, "one curve point per full second of the clock");
            for (int i = 1; i < rec.Curve.Length; i++) Assert.GreaterOrEqual(rec.Curve[i], rec.Curve[i - 1], "floors never decrease");
        }
    }
}
