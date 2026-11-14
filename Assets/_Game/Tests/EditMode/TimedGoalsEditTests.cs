using System;
using System.IO;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// Reward time goals (doc 46 section 8) and the medal target data: the Daily, Weekly and Main goals read the timed counters and gold medals, pay the existing
    /// reward sizes, restart with the day and week, and old saves start at 0. Written for Unity EditMode (not run by the dotnet suite).
    /// </summary>
    public class TimedGoalsEditTests
    {
        // Monday 9 November 2026, noon
        static readonly DateTime Monday = new DateTime(2026, 11, 9, 12, 0, 0);

        ContentRepository repo;
        RegionDef region1;
        MemorySaveStore store;
        ProgressService svc;
        IMedalTargetSource savedSource;
        DateTime now;

        static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            region1 = repo.GetRegionByIndex(1);
            ProgressService.TestUnlockAll = false;
            savedSource = MedalTargets.Source;
            store = new MemorySaveStore();
            now = Monday;
            svc = new ProgressService(repo, store, 5);
            svc.Clock = () => now;
        }

        [TearDown]
        public void TearDown()
        {
            MedalTargets.Source = savedSource;
        }

        string Lvl(int index) { return region1.Levels[index - 1]; }

        static RunRecord LevelRun(string levelId, int floors, int ticks, int mode = 2, bool revived = false)
        {
            return new RunRecord { Kind = RunKind.Level, LevelId = levelId, Floors = floors, Ticks = ticks, Mode = mode, Revived = revived, HeroId = "momo", GameVersion = "0.9.9.1" };
        }

        static int Daily(ProgressService s, string stat) { return s.RewardStatValue(RewardTab.Daily, stat); }
        static int Weekly(ProgressService s, string stat) { return s.RewardStatValue(RewardTab.Weekly, stat); }

        RewardEntry DailyAction(string id)
        {
            foreach (RewardEntry a in svc.RewardDefs.DailyActions) if (a.Id == id) return a;
            Assert.Fail("no daily action " + id);
            return null;
        }

        RewardEntry WeeklyTask(string id)
        {
            RewardEntry e = svc.RewardDefs.Find(RewardTab.Weekly, id);
            Assert.IsNotNull(e, id);
            return e;
        }

        // ---- the tables ----

        [Test]
        public void TheTimedGoalsAreInTheDailyAndWeeklyTables()
        {
            RewardCatalog c = RewardCatalog.BuiltIn();
            RewardEntry pace = null, floor = null;
            foreach (RewardEntry a in c.DailyActions) { if (a.Id == "d_pace2") pace = a; if (a.Id == "d_floor100") floor = a; }
            Assert.IsNotNull(pace);
            Assert.AreEqual(RewardStat.PaceClears, pace.Stat);
            Assert.AreEqual(2, pace.Target);
            Assert.AreEqual(12, pace.Points, "Main Bug List 2: 12 of the 200 daily points");
            Assert.IsNotNull(floor);
            Assert.AreEqual(RewardStat.Floor100In3, floor.Stat);
            Assert.AreEqual(1, floor.Target);

            RewardEntry p3 = c.Find(RewardTab.Weekly, "w_pace3"), g5 = c.Find(RewardTab.Weekly, "w_gold5"), ta = c.Find(RewardTab.Weekly, "w_ta100");
            Assert.AreEqual(3, p3.Target);
            Assert.AreEqual(5, g5.Target);
            Assert.AreEqual(100, ta.Target);
            Assert.AreEqual(RewardStat.BestPaceLevels, p3.Stat);
            Assert.AreEqual(RewardStat.GoldMedals, g5.Stat);
            Assert.AreEqual(RewardStat.TimeAttackFloors, ta.Stat);
            // economy review 2026-11-13: the weekly table has 25 tasks, gems only on the hard-skill tasks (50 a week), 5,500 task coins
            Assert.AreEqual(4, p3.Gems); Assert.AreEqual(200, p3.Coins);
            Assert.AreEqual(5, g5.Gems); Assert.AreEqual(250, g5.Coins); Assert.AreEqual(1, g5.PowerUps);
            Assert.AreEqual(0, ta.Gems); Assert.AreEqual(200, ta.Coins);
            Assert.AreEqual(25, c.WeeklyTasks.Count);
            Assert.AreEqual("w_hard10", c.WeeklyTasks[c.WeeklyTasks.Count - 1].Id, "the hardest task, with a hero, stays last");
        }

        // ---- Daily ----

        [Test]
        public void DailyPaceClearsCountClearsAtZeroPointSixAndPayFifteenPoints()
        {
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600));              // 1.00 plat/s
            Assert.AreEqual(1, Daily(svc, RewardStat.PaceClears));
            Assert.AreEqual(0, svc.DailyPoints());
            svc.RecordRun(LevelRun(Lvl(2), 30, 3600));              // 0.50: too slow
            svc.RecordRun(LevelRun(Lvl(3), 36, 3600, 2, true));     // revived, exactly 0.60: counts since 2026-11-10
            Assert.AreEqual(2, Daily(svc, RewardStat.PaceClears));
            Assert.AreEqual(12, svc.DailyPoints(), "d_pace2 is done");
            Assert.AreEqual(2, svc.RewardProgress(RewardTab.Daily, DailyAction("d_pace2")));
        }

        [Test]
        public void DailyFloor100WithinThreeMinutesNeedsTheEndlessSplit()
        {
            Func<int, RunRecord> endless = split => new RunRecord { Kind = RunKind.Endless, RegionId = region1.Id, Floors = 150, Ticks = 14000, Mode = 2, Splits = new[] { split }, Curve = new[] { 3 } };
            Assert.AreEqual(0, Daily(svc, RewardStat.Floor100In3));
            svc.RecordRun(endless(TimeRecords.TimeAttackTicks + 1));   // 3:00.02: too slow
            Assert.AreEqual(0, Daily(svc, RewardStat.Floor100In3));
            svc.RecordRun(endless(TimeRecords.TimeAttackTicks));       // exactly 3:00
            Assert.AreEqual(1, Daily(svc, RewardStat.Floor100In3));
            Assert.AreEqual(14, svc.DailyPoints(), "d_floor100 is worth 14 points");
            now = now.AddDays(1);
            Assert.AreEqual(0, Daily(svc, RewardStat.Floor100In3), "a new day starts empty");
            Assert.AreEqual(0, svc.DailyPoints());
        }

        [Test]
        public void ARevivedEndlessRunDoesNotCompleteTheDailyFloor100Goal()
        {
            svc.RecordRun(new RunRecord { Kind = RunKind.Endless, RegionId = region1.Id, Floors = 150, Ticks = 14000, Mode = 2, Revived = true, Splits = new[] { 5000 } });
            Assert.AreEqual(0, Daily(svc, RewardStat.Floor100In3));
        }

        [Test]
        public void TheDailyPointMilestonesStillWorkWithTheTimedGoalsInTheTwentyTasks()
        {
            svc.RecordLogin();                                       // 5
            svc.AddPlaySeconds(1500);                                // 7 + 11
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600));
            svc.RecordRun(LevelRun(Lvl(2), 60, 3600));               // d_pace2: 12
            svc.RecordRun(new RunRecord { Kind = RunKind.Endless, RegionId = region1.Id, Floors = 150, Ticks = 9000, Mode = 2, Splits = new[] { 5000 } });   // d_floor100: 14
            Assert.GreaterOrEqual(svc.DailyPoints(), 49, "5 + 18 + 12 + 14 (a medal task may add 8 when the shipped targets give a medal)");
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Daily, "dp_40").Result);
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Daily, "dp_80").Result);
        }

        // ---- Weekly ----

        [Test]
        public void WeeklyBeatYourBestPaceCountsDistinctLevelsAndPaysTheLevels20Package()
        {
            RewardEntry task = WeeklyTask("w_pace3");
            svc.RecordRun(LevelRun(Lvl(1), 30, 3600));
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600));               // the same level again: still one level
            svc.RecordRun(LevelRun(Lvl(2), 30, 3600));
            Assert.AreEqual(2, Weekly(svc, RewardStat.BestPaceLevels));
            Assert.IsFalse(svc.IsRewardReady(RewardTab.Weekly, task));
            svc.RecordRun(LevelRun(Lvl(3), 30, 3600));
            Assert.AreEqual(3, Weekly(svc, RewardStat.BestPaceLevels));
            Assert.IsTrue(svc.IsRewardReady(RewardTab.Weekly, task));
            int gems = svc.Gems, coins = svc.Coins;
            RewardClaimOutcome o = svc.TryClaimReward(RewardTab.Weekly, "w_pace3");
            Assert.AreEqual(RewardClaimResult.Ok, o.Result);
            Assert.AreEqual(4, svc.Gems - gems);
            Assert.AreEqual(200, svc.Coins - coins);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Weekly, "w_pace3").Result);
        }

        [Test]
        public void WeeklyGoldMedalsCountNewMedalsOnlyAndIncludeRevivedRuns()
        {
            var table = new DictionaryMedalTargets();
            for (int i = 1; i <= 6; i++) table.Set(Lvl(i), 2, 1000);
            MedalTargets.Source = table;
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600));               // 1.00 plat/s against 1.00: gold
            svc.RecordRun(LevelRun(Lvl(1), 61, 3600));               // better, gold again: not a new medal
            svc.RecordRun(LevelRun(Lvl(2), 50, 3600));               // 0.83: silver
            svc.RecordRun(LevelRun(Lvl(3), 600, 3600, 2, true));     // revived: counts, gold
            for (int i = 4; i <= 5; i++) svc.RecordRun(LevelRun(Lvl(i), 60, 3600));
            Assert.AreEqual(4, Weekly(svc, RewardStat.GoldMedals));  // levels 1, 3, 4, 5
            Assert.IsFalse(svc.IsRewardReady(RewardTab.Weekly, WeeklyTask("w_gold5")));
            svc.RecordRun(LevelRun(Lvl(2), 60, 3600));               // silver becomes gold: the fifth
            Assert.AreEqual(5, Weekly(svc, RewardStat.GoldMedals));
            Assert.IsTrue(svc.IsRewardReady(RewardTab.Weekly, WeeklyTask("w_gold5")));
            int coins = svc.Coins;
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w_gold5").Result);
            Assert.AreEqual(250, svc.Coins - coins);
        }

        [Test]
        public void WeeklyTimeAttackWantsOneHundredFloors()
        {
            RewardEntry task = WeeklyTask("w_ta100");
            svc.RecordRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = region1.Id, Floors = 99, Ticks = TimeRecords.TimeAttackTicks, Mode = 2 });
            Assert.AreEqual(99, Weekly(svc, RewardStat.TimeAttackFloors));
            Assert.IsFalse(svc.IsRewardReady(RewardTab.Weekly, task));
            svc.RecordRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = region1.Id, Floors = 100, Ticks = TimeRecords.TimeAttackTicks, Mode = 0 });   // another mode counts too
            Assert.IsTrue(svc.IsRewardReady(RewardTab.Weekly, task));
            int coins = svc.Coins;
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w_ta100").Result);
            Assert.AreEqual(200, svc.Coins - coins);
        }

        [Test]
        public void WeeklyTimedProgressIsKeptAcrossTheDaysAndRestartsOnMonday()
        {
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600));
            svc.RecordRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = region1.Id, Floors = 120, Ticks = TimeRecords.TimeAttackTicks, Mode = 2 });
            now = now.AddDays(3);                                    // Thursday
            Assert.AreEqual(1, Weekly(svc, RewardStat.BestPaceLevels));
            Assert.AreEqual(120, Weekly(svc, RewardStat.TimeAttackFloors));
            Assert.AreEqual(0, Daily(svc, RewardStat.PaceClears), "the Daily counter restarted with the day");
            now = now.AddDays(4);                                    // next Monday
            Assert.AreEqual(0, Weekly(svc, RewardStat.BestPaceLevels));
            Assert.AreEqual(0, Weekly(svc, RewardStat.TimeAttackFloors));
            Assert.AreEqual(0, Weekly(svc, RewardStat.GoldMedals));
        }

        // ---- Main (region goals) ----

        [Test]
        public void TheGoldMedalGoalsCountLevelsWithAGoldInAnyModeAndPayTheStarTierRewards()
        {
            var table = new DictionaryMedalTargets();
            for (int i = 1; i <= 6; i++) for (int m = 0; m < TimeRecords.ModeCount; m++) table.Set(Lvl(i), m, 1000);
            MedalTargets.Source = table;
            Assert.AreEqual(0, svc.GoldMedalLevelsInRegion(1));
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600, 0));
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600, 4));            // a second mode of the same level: still one level
            svc.RecordRun(LevelRun(Lvl(2), 50, 3600, 2));            // silver: not counted
            for (int i = 3; i <= 5; i++) svc.RecordRun(LevelRun(Lvl(i), 60, 3600, 1));
            Assert.AreEqual(4, svc.GoldMedalLevelsInRegion(1));
            Assert.AreEqual(5, svc.GoldMedalsInRegion(1), "the older helper still counts level x mode pairs");
            Assert.AreEqual(0, svc.GoldMedalLevelsInRegion(2));
            RegionGoal g1 = svc.RewardDefs.FindGoal("g_1_gold_1");
            Assert.IsNotNull(g1);
            Assert.AreEqual(5, g1.Target);
            Assert.AreEqual(4, svc.RegionGoalProgress(g1));
            Assert.IsFalse(svc.IsRegionGoalReady(g1));
            svc.RecordRun(LevelRun(Lvl(6), 60, 3600, 3));
            Assert.AreEqual(5, svc.RegionGoalProgress(g1));
            Assert.IsTrue(svc.IsRegionGoalReady(g1));
            Assert.IsTrue(svc.HasClaimableInRegion(1));

            int coins = svc.Coins;
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimRegionGoal("g_1_gold_1").Result);
            Assert.AreEqual(RegionGoalTable.ScaledCoins(100, 1), svc.Coins - coins, "gold tier 1 pays 100 coins");
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimRegionGoal("g_1_gold_1").Result);
            Assert.IsFalse(svc.IsRegionGoalReady(svc.RewardDefs.FindGoal("g_1_gold_2")));
            Assert.AreEqual(5 * 49, svc.RewardDefs.AllRegionGoals.Count);
        }

        [Test]
        public void TheGoldMedalGoalProgressSurvivesAReloadAndAnOldSaveStartsAtZero()
        {
            var table = new DictionaryMedalTargets();
            table.Set(Lvl(1), 2, 1000);
            MedalTargets.Source = table;
            svc.RecordRun(LevelRun(Lvl(1), 60, 3600));
            var again = new ProgressService(repo, store, 5) { Clock = () => now };
            Assert.AreEqual(1, again.GoldMedalLevelsInRegion(1));
            Assert.AreEqual(1, Weekly(again, RewardStat.GoldMedals));
            Assert.AreEqual(1, Daily(again, RewardStat.PaceClears));

            var fresh = new ProgressService(repo, new MemorySaveStore(), 5) { Clock = () => now };
            Assert.AreEqual(0, fresh.GoldMedalLevelsInRegion(1));
            Assert.AreEqual(0, Weekly(fresh, RewardStat.GoldMedals));
            Assert.AreEqual(0, Daily(fresh, RewardStat.Floor100In3));
            Assert.AreEqual(0, Weekly(fresh, RewardStat.TimeAttackFloors));
        }

        // ---- medal target data ----

        [Test]
        public void TheShippedMedalTargetsLoadAsTheMedalSource()
        {
            string res = Path.Combine(ProjectRoot, "Assets", "Resources", "Content", "medal_targets.json");
            string src = Path.Combine(ProjectRoot, "Content", "config", "medal_targets.json");
            Assert.IsTrue(File.Exists(src), "Content/config/medal_targets.json (written by MedalCalibrationTests, MEDAL_CAL=1)");
            Assert.AreEqual(File.ReadAllText(src), File.ReadAllText(res), "the Resources copy must equal the Content file");
            MedalTargets.Source = new NoMedalTargets();
            Assert.IsTrue(MedalTargetsBootstrap.InstallFromResources());
            Assert.IsInstanceOf<FileMedalTargets>(MedalTargets.Source);
            Assert.Greater(MedalTargets.Lookup(Lvl(1), 0), 0);
            foreach (RegionDef r in repo.Regions)
            {
                if (r.Index > RegionGoalTable.PlayableRegions) continue;
                foreach (string id in r.Levels)
                    for (int m = 0; m < TimeRecords.ModeCount; m++)
                        Assert.Greater(MedalTargets.Lookup(id, m), 0, id + " mode " + m);
            }
            // a level at the gold line earns gold through the real source
            int target = MedalTargets.Lookup(Lvl(1), 2);
            Assert.AreEqual(MedalLevel.Gold, TimeRecords.MedalFor(new PaceValue((int)Math.Ceiling(target * 0.85 / 1000.0 * 60.0) + 1, 3600), Lvl(1), 2));
            Assert.AreEqual(MedalLevel.None, TimeRecords.MedalFor(new PaceValue(1, 3600), Lvl(1), 2));
        }
    }
}
