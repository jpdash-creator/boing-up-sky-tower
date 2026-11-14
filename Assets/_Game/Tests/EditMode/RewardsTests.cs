using System;
using System.IO;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Gameplay;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FluffyClimbers.Tests
{
    /// <summary>Rewards of docs/26 section 4: counters fed from play, daily and weekly resets on the local clock, claims, the Main tab, saving.</summary>
    public class RewardsTests
    {
        ContentRepository repo;
        MemorySaveStore store;
        DateTime now;

        // Monday 2 November 2026, noon (local clock of the tests)
        static readonly DateTime Monday = new DateTime(2026, 11, 2, 12, 0, 0);

        static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
            now = Monday;
        }

        ProgressService NewService()
        {
            var svc = new ProgressService(repo, store, 5);
            svc.Clock = () => now;
            return svc;
        }

        static RunOutcome Won(int starMask = 1) { return new RunOutcome { Completed = true, StarMask = starMask, Score = 100 }; }

        /// <summary>Clears levels 1..count of a region in order (the next level only unlocks after the previous one).</summary>
        void ClearLevels(ProgressService svc, int regionIndex, int count, int starMask = 1)
        {
            RegionDef r = repo.GetRegionByIndex(regionIndex);
            int n = 0;
            foreach (LevelDef l in repo.LevelsOf(r.Id)) { if (n++ >= count) break; svc.ApplyRun(l.Id, Won(starMask)); }
        }

        /// <summary>45 daily points (Main Bug List 2): login 5 + play 10 min 7 + play 25 min 11 + 10 combos 8 + 30 combos 14. Enough for the first milestone (dp_40).</summary>
        static void EarnFortyFivePoints(ProgressService svc)
        {
            svc.RecordLogin();
            svc.AddPlaySeconds(1800);
            svc.RecordRunStats(new RunOutcome { ComboChains = 30, BestCombo = 3 });   // no region: 30 combos in region 1 would also complete the Main goal g_1_combos_1
        }

        static int Daily(ProgressService svc, string stat) { return svc.RewardStatValue(RewardTab.Daily, stat); }
        static int Weekly(ProgressService svc, string stat) { return svc.RewardStatValue(RewardTab.Weekly, stat); }

        static int TotalPowerUps(ProgressService svc)
        {
            int n = 0;
            foreach (PowerUpDef d in svc.PowerUpDefs.All) n += svc.PowerUpCount(d.Id);
            return n;
        }

        // ---- the tables ----

        [Test]
        public void BuiltInCatalogMatchesTheJsonFile()
        {
            string json = File.ReadAllText(Path.Combine(ProjectRoot, "Content", "economy", "rewards.json"));
            RewardCatalog fromJson = RewardCatalog.FromJson(json);
            RewardCatalog builtIn = RewardCatalog.BuiltIn();
            AssertSame(builtIn.DailyActions, fromJson.DailyActions);
            AssertSame(builtIn.DailyMilestones, fromJson.DailyMilestones);
            AssertSame(builtIn.WeeklyTasks, fromJson.WeeklyTasks);
        }

        static void AssertSame(System.Collections.Generic.IReadOnlyList<RewardEntry> a, System.Collections.Generic.IReadOnlyList<RewardEntry> b)
        {
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Id, b[i].Id);
                Assert.AreEqual(a[i].Title, b[i].Title, a[i].Id);
                Assert.AreEqual(a[i].Stat, b[i].Stat, a[i].Id);
                Assert.AreEqual(a[i].Target, b[i].Target, a[i].Id);
                Assert.AreEqual(a[i].Points, b[i].Points, a[i].Id);
                Assert.AreEqual(a[i].Gems, b[i].Gems, a[i].Id);
                Assert.AreEqual(a[i].Coins, b[i].Coins, a[i].Id);
                Assert.AreEqual(a[i].PowerUps, b[i].PowerUps, a[i].Id);
                Assert.AreEqual(a[i].HeroRegion, b[i].HeroRegion, a[i].Id);
                Assert.AreEqual(a[i].HeroOrCoins, b[i].HeroOrCoins, a[i].Id);
            }
        }

        [Test]
        public void TablesFollowTheDocument()
        {
            RewardCatalog c = RewardCatalog.BuiltIn();
            Assert.AreEqual(RewardCatalog.DailyTaskCount, c.DailyActions.Count);
            Assert.AreEqual(20, c.DailyActions.Count, "Main Bug List 2: twenty daily tasks");
            Assert.AreEqual(200, c.DailyMaxPoints, "the twenty daily tasks are exactly 200 points");
            Assert.AreEqual(RewardCatalog.DailyTotalPoints, c.DailyMaxPoints);
            foreach (RewardEntry a in c.DailyActions) { Assert.GreaterOrEqual(a.Points, 5, a.Id); Assert.LessOrEqual(a.Points, 15, a.Id); Assert.IsNotNull(a.Stat, a.Id); Assert.Greater(a.Target, 0, a.Id); }
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (RewardEntry a in c.DailyActions) Assert.IsTrue(ids.Add(a.Id), "unique id " + a.Id);
            Assert.AreEqual(5, c.DailyMilestones.Count);
            int[] thresholds = { 40, 80, 120, 160, 200 };
            for (int i = 0; i < 5; i++) Assert.AreEqual(thresholds[i], c.DailyMilestones[i].Target);
            Assert.AreEqual(200, c.DailyMilestones[0].Coins);
            Assert.AreEqual(400, c.DailyMilestones[1].Coins);
            Assert.AreEqual(600, c.DailyMilestones[2].Coins);
            Assert.AreEqual(800, c.DailyMilestones[3].Coins);
            foreach (RewardEntry m in c.DailyMilestones) Assert.AreEqual(0, m.Gems, "owner 2026-11-13: zero gems in Daily (" + m.Id + ")");
            Assert.IsTrue(c.DailyMilestones[4].IsHeroOrCoins, "200 points: 50 percent hero, otherwise coins");
            Assert.AreEqual(1000, c.DailyMilestones[4].HeroOrCoins);
            Assert.AreEqual(0, c.DailyMilestones[4].Coins, "the 1,000 coins are paid only when the hero roll misses");
            Assert.AreEqual(RewardEntry.RandomOpenRegionHero, c.DailyMilestones[4].HeroRegion, "200 points: one random Common hero of an open region");
            for (int i = 0; i < 4; i++) Assert.AreEqual(0, c.DailyMilestones[i].HeroRegion, c.DailyMilestones[i].Id);
            Assert.AreEqual(RewardCatalog.WeeklyTaskCount, c.WeeklyTasks.Count);
            Assert.AreEqual(25, c.WeeklyTasks.Count, "Main Bug List 2: twenty-five weekly tasks");
            var wids = new System.Collections.Generic.HashSet<string>();
            int heroes = 0;
            foreach (RewardEntry w in c.WeeklyTasks) { Assert.IsTrue(wids.Add(w.Id), "unique id " + w.Id); if (w.HeroRegion != 0) heroes++; Assert.IsNotNull(w.Stat, w.Id); Assert.Greater(w.Target, 0, w.Id); }
            Assert.AreEqual(2, heroes, "the two hard-clear tasks give a random Common hero");
            Assert.AreEqual(RewardEntry.RandomOpenRegionHero, c.Find(RewardTab.Weekly, "w_hard5").HeroRegion);
            Assert.AreEqual(RewardEntry.RandomOpenRegionHero, c.Find(RewardTab.Weekly, "w_hard10").HeroRegion);
            Assert.AreEqual(1000, c.Find(RewardTab.Weekly, "w_hard5").HeroOrCoins);
            Assert.AreEqual(1000, c.Find(RewardTab.Weekly, "w_hard10").HeroOrCoins);
            int gemTasks = 0;
            foreach (RewardEntry w in c.WeeklyTasks) if (w.Gems > 0) gemTasks++;
            Assert.AreEqual(10, gemTasks, "gems only on the ten hard-skill tasks");
            int dayGems = 0, dayCoins = 0, weekGems = 0, weekCoins = 0, weekUnits = 0;
            foreach (RewardEntry m in c.DailyMilestones) { dayGems += m.Gems; dayCoins += m.Coins; }
            foreach (RewardEntry w in c.WeeklyTasks) { weekGems += w.Gems; weekCoins += w.Coins; weekUnits += w.PowerUps; }
            Assert.AreEqual(0, dayGems, "a full day pays no gems (owner 2026-11-13)");
            Assert.AreEqual(2000, dayCoins, "four coin gifts: 200 + 400 + 600 + 800 (the 200-point gift is a hero or 1,000 coins)");
            Assert.AreEqual(50, weekGems, "a full week of tasks pays 50 gems (was 120)");
            Assert.AreEqual(5500, weekCoins, "and 5,500 task coins (was 10,500), plus up to 2,000 from the two hero-or-coins gifts");
            Assert.AreEqual(5, weekUnits, "and 5 power-up units");
            Assert.AreEqual(0, c.StarMilestones.Count, "the 25..225 star cards are gone");
            Assert.AreEqual(0, c.RegionRewards.Count, "the five region rewards are gone (clear all levels is the last Levels goal)");
        }

        [Test]
        public void EveryDailyAndWeeklyStatIsKnownAndReadable()
        {
            RewardCatalog c = RewardCatalog.BuiltIn();
            ProgressService svc = NewService();
            foreach (RewardEntry e in c.DailyActions) Assert.DoesNotThrow(() => svc.RewardStatValue(RewardTab.Daily, e.Stat), e.Id);
            foreach (RewardEntry e in c.WeeklyTasks) Assert.DoesNotThrow(() => svc.RewardStatValue(RewardTab.Weekly, e.Stat), e.Id);
        }

        // ---- counters ----

        [Test]
        public void LoginCountsOncePerDayAndTheWeekCountsDays()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.RecordLogin();
            Assert.AreEqual(1, Daily(svc, RewardStat.Login));
            Assert.AreEqual(1, Weekly(svc, RewardStat.LoginDays));
            now = now.AddDays(1);   // Tuesday
            Assert.AreEqual(0, Daily(svc, RewardStat.Login), "a new day starts empty");
            svc.RecordLogin();
            Assert.AreEqual(1, Daily(svc, RewardStat.Login));
            Assert.AreEqual(2, Weekly(svc, RewardStat.LoginDays));
        }

        [Test]
        public void PlayTimeAddsUpAndFeedsTheMinuteGoals()
        {
            ProgressService svc = NewService();
            svc.AddPlaySeconds(599);
            Assert.AreEqual(0, svc.DailyPoints());
            Assert.AreEqual(9, Weekly(svc, RewardStat.PlayMinutes));
            svc.AddPlaySeconds(1);
            Assert.AreEqual(7, svc.DailyPoints(), "play 10 minutes is worth 7 points");
            Assert.AreEqual(10, Weekly(svc, RewardStat.PlayMinutes));
            svc.AddPlaySeconds(0);
            svc.AddPlaySeconds(-5);
            Assert.AreEqual(600, Daily(svc, RewardStat.PlaySeconds));
        }

        [Test]
        public void FeedPlayTimeCollectsWholeSecondsAndDoesNotWriteUntilFlushed()
        {
            ProgressService svc = NewService();
            for (int i = 0; i < 4; i++) svc.FeedPlayTime(1f, false);
            Assert.AreEqual(0, Daily(svc, RewardStat.PlaySeconds), "under 5 seconds nothing is passed on yet");
            svc.FeedPlayTime(1f, false);
            Assert.AreEqual(5, Daily(svc, RewardStat.PlaySeconds));
            Assert.AreEqual(0, Daily(new ProgressService(repo, store, 5) { Clock = () => now }, RewardStat.PlaySeconds), "a run never writes the save by itself");
            svc.FlushPlayTime();
            Assert.AreEqual(5, Daily(new ProgressService(repo, store, 5) { Clock = () => now }, RewardStat.PlaySeconds));
            svc.FeedPlayTime(30f, false);   // a long frame counts as at most one second
            Assert.AreEqual(5, Daily(svc, RewardStat.PlaySeconds));
        }

        [Test]
        public void CompletedLevelsAreCountedAndGiveTheLevelActions()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 1);
            Assert.AreEqual(1, Daily(svc, RewardStat.Levels));
            Assert.AreEqual(0, svc.DailyPoints(), "one level is not a daily action any more");
            ClearLevels(svc, 1, 3);   // a replay of level 1 (counted) and first clears of 2 and 3
            Assert.AreEqual(4, Daily(svc, RewardStat.Levels));
            Assert.AreEqual(6, svc.DailyPoints(), "complete 3 levels");
            Assert.AreEqual(4, Weekly(svc, RewardStat.Levels));
            svc.ApplyRun("lvl_001_01", new RunOutcome { Completed = false });   // a failed run counts nothing
            Assert.AreEqual(4, Daily(svc, RewardStat.Levels));
        }

        [Test]
        public void NoWallGemLevelAndStarCountersComeFromTheRunResult()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 6);
            Assert.AreEqual(0, Daily(svc, RewardStat.NoWall));
            Assert.AreEqual(6, Daily(svc, RewardStat.StarsEarned), "6 first clears with 1 star each");
            Assert.AreEqual(0, Daily(svc, RewardStat.Stars2));
            svc.ApplyRun("lvl_001_07", Won());
            Assert.AreEqual(0, Daily(svc, RewardStat.NoWall), "level 7 is a plain level (the no-wall-touch level is level 10 since Bug List 6 item 4)");
            Assert.AreEqual(0, Daily(svc, RewardStat.GemLevel));

            svc.ApplyRun("lvl_001_08", new RunOutcome { Completed = true, StarMask = 7, GemsCollected = 5, HiddenGems = 2 });
            Assert.AreEqual(0, Daily(svc, RewardStat.GemLevel), "level 8 has no hidden gem any more (owner 2026-11-13: gems only on levels 5, 10, 15, 20, 25)");
            Assert.AreEqual(1, Daily(svc, RewardStat.Stars2), "3 stars is also 2 or more");
            Assert.AreEqual(1, Daily(svc, RewardStat.Stars3));
            Assert.AreEqual(10, Daily(svc, RewardStat.StarsEarned), "7 + 3 stars");

            svc.ApplyRun("lvl_001_09", new RunOutcome { Completed = true, StarMask = 3 });
            Assert.AreEqual(2, Daily(svc, RewardStat.Stars2), "2 stars counts");
            Assert.AreEqual(1, Daily(svc, RewardStat.Stars3), "2 stars is not a 3-star clear");
            Assert.AreEqual(12, Daily(svc, RewardStat.StarsEarned));
            Assert.AreEqual(12, Weekly(svc, RewardStat.StarsEarned));
            svc.ApplyRun("lvl_001_10", new RunOutcome { Completed = true, StarMask = 1 });
            Assert.AreEqual(2, Daily(svc, RewardStat.Stars2), "1 star is not 2 stars");
            Assert.AreEqual(1, Daily(svc, RewardStat.NoWall), "level 10 is the no-wall-touch level");
            Assert.AreEqual(1, Weekly(svc, RewardStat.NoWall));
        }

        [Test]
        public void StarCountersWorkOnReplaysOfClearedLevelsAndSkipFailedRuns()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 1);
            svc.ApplyRun("lvl_001_01", Won(3));   // replay of a cleared level: stripped gems do not matter, the stars still count
            Assert.AreEqual(1, Daily(svc, RewardStat.Stars2));
            Assert.AreEqual(3, Daily(svc, RewardStat.StarsEarned), "1 star (first clear) + 2 stars (replay)");
            svc.ApplyRun("lvl_001_01", new RunOutcome { Completed = false, StarMask = 7 });
            Assert.AreEqual(3, Daily(svc, RewardStat.StarsEarned), "a failed run counts no stars");
        }

        [Test]
        public void ReplaysOfClearedLevelsCountAtMostTenPerDayInTotal()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 10);   // 10 first clears
            Assert.AreEqual(10, Daily(svc, RewardStat.Levels));
            for (int i = 0; i < 12; i++) svc.ApplyRun("lvl_001_10", Won(7));   // replays of the no-wall level
            Assert.AreEqual(20, Daily(svc, RewardStat.Levels), "only 10 of the 12 replays count");
            Assert.AreEqual(11, Daily(svc, RewardStat.NoWall), "1 first clear + 10 counted replays");
            Assert.AreEqual(10, Daily(svc, RewardStat.Stars3), "10 counted replays with 3 stars");
            svc.ApplyRun("lvl_001_01", Won());   // a replay of another level: still over the daily limit
            Assert.AreEqual(20, Daily(svc, RewardStat.Levels));
            ClearLevels(svc, 1, 11);             // level 11 is a first clear: always counts
            Assert.AreEqual(21, Daily(svc, RewardStat.Levels));
            Assert.AreEqual(21, Weekly(svc, RewardStat.Levels));
            now = now.AddDays(1);                // next day: replays count again
            svc.ApplyRun("lvl_001_01", Won());
            Assert.AreEqual(1, Daily(svc, RewardStat.Levels));
        }

        [Test]
        public void TheLastLevelCountsAsAGemLevelToo()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 25);
            Assert.AreEqual(2, Daily(svc, RewardStat.GemLevel), "levels 15 (hidden-gem goal) and 25 (owner 2026-11-13: level 8 lost its hidden gem)");
        }

        [Test]
        public void ComboChainsBestComboAndPowerUpsFeedTheCountersFromRunsAndFailedRuns()
        {
            ProgressService svc = NewService();
            svc.RecordRunStats(new RunOutcome { ComboChains = 2, BestCombo = 4, PowerUpsUsed = 1 });   // a failed or left run
            Assert.AreEqual(2, Daily(svc, RewardStat.Combos));
            Assert.AreEqual(4, Daily(svc, RewardStat.BestCombo));
            Assert.AreEqual(1, Daily(svc, RewardStat.PowerUps));
            Assert.AreEqual(4, svc.BestComboEver);
            Assert.AreEqual(0, svc.DailyPoints(), "2 combos and 1 power-up are not worth a task yet");
            svc.ApplyRun("lvl_001_01", new RunOutcome { Completed = true, StarMask = 1, ComboChains = 3, BestCombo = 2, PowerUpsUsed = 2 });
            Assert.AreEqual(5, Daily(svc, RewardStat.Combos));
            Assert.AreEqual(4, Daily(svc, RewardStat.BestCombo), "the best stays the best");
            Assert.AreEqual(3, Daily(svc, RewardStat.PowerUps));
            Assert.AreEqual(5, Weekly(svc, RewardStat.Combos));
            Assert.AreEqual(8, svc.DailyPoints(), "5 combos are not the daily 10, but 3 power-ups are worth 8 points");
        }

        [Test]
        public void HardLevelsStartedCountFromDifficultyThreeUp()
        {
            ProgressService svc = NewService();
            svc.RecordLevelStarted(0);
            svc.RecordLevelStarted(2);
            Assert.AreEqual(0, Daily(svc, RewardStat.Hard));
            svc.RecordLevelStarted(3);
            Assert.AreEqual(1, Daily(svc, RewardStat.Hard));
            svc.RecordLevelStarted(4);
            Assert.AreEqual(1, Daily(svc, RewardStat.Hard), "counted once per day (no save at every retry)");
            Assert.AreEqual(0, svc.DailyPoints(), "starting a hard level is not enough: it has to be cleared");
        }

        [Test]
        public void HardClearsNeedTheLevelToBeClearedOnHardOrInsane()
        {
            ProgressService svc = NewService();
            svc.RecordLevelStarted(2);
            svc.ApplyRun("lvl_001_01", Won());
            Assert.AreEqual(0, Daily(svc, RewardStat.HardClear), "cleared on Normal");
            svc.RecordLevelStarted(3);
            svc.ApplyRun("lvl_001_02", Won());
            Assert.AreEqual(1, Daily(svc, RewardStat.HardClear));
            Assert.AreEqual(1, Weekly(svc, RewardStat.HardClear));
            Assert.AreEqual(12, svc.DailyPoints(), "clear a hard level");
            svc.RecordLevelStarted(4);
            svc.ApplyRun("lvl_001_03", new RunOutcome { Completed = false });
            Assert.AreEqual(1, Daily(svc, RewardStat.HardClear), "a failed run is not a clear");
            Assert.AreEqual(1, svc.RegionStatsOf(1).HardClears);
        }

        static RunRecord LevelRecord(string levelId, int floors, int ticks)
        {
            return new RunRecord { Kind = RunKind.Level, LevelId = levelId, Floors = floors, Ticks = ticks, Mode = 2, HeroId = "momo", GameVersion = "0.9.9.1" };
        }

        [Test]
        public void AllTwentyDailyTasksGiveTwoHundredPointsAndTheFiveGiftsPayOutOnAFreshAccount()
        {
            IMedalTargetSource savedSource = MedalTargets.Source;
            try
            {
                var table = new DictionaryMedalTargets();
                RegionDef r1 = repo.GetRegionByIndex(1);
                table.Set(r1.Levels[0], 2, 1000);   // 60 floors in 3600 ticks = 1.00 plat/s: gold
                MedalTargets.Source = table;

                ProgressService svc = NewService();   // a fresh account: only region 1 is open
                Assert.AreEqual(0, svc.DailyPoints());
                svc.RecordLogin();
                svc.AddPlaySeconds(3600);
                svc.RecordLevelStarted(3);   // every clear below is a hard clear
                svc.RecordRunStats(new RunOutcome { ComboChains = 30, BestCombo = 9, PowerUpsUsed = 3, Jumps = 400, Region = 1 });
                ClearLevels(svc, 1, 11, 7);   // 11 first clears with 3 stars: 33 stars, 2,200 coins (200 each since the 2026-11-13 coin cut), 2,500 with the Endless coins
                svc.RecordRun(LevelRecord(r1.Levels[0], 60, 3600));   // 1.00 plat/s: a pace clear and a gold medal
                svc.RecordRun(LevelRecord(r1.Levels[1], 60, 3600));   // the second pace clear
                for (int i = 0; i < 3; i++) svc.ApplyEndlessRun(r1.Id, 120, "20261102");   // floor 120 three times
                svc.RecordRun(new RunRecord { Kind = RunKind.Endless, RegionId = r1.Id, Floors = 150, Ticks = 9000, Mode = 2, Splits = new[] { 5000 } });   // floor 100 within 3:00
                svc.RecordRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = r1.Id, Floors = 60, Ticks = TimeRecords.TimeAttackTicks, Mode = 2 });

                foreach (RewardEntry a in svc.RewardDefs.DailyActions)
                    Assert.GreaterOrEqual(svc.RewardProgress(RewardTab.Daily, a), a.Target, a.Id + " (" + a.Title + ") must be done");
                Assert.AreEqual(200, svc.DailyPoints());

                int gems = svc.Gems, coins = svc.Coins;
                int copies = svc.Copies("momo");
                int[] points = { 40, 80, 120, 160, 200 };
                RewardClaimOutcome last = null;
                foreach (int p in points) { last = svc.TryClaimReward(RewardTab.Daily, "dp_" + p); Assert.AreEqual(RewardClaimResult.Ok, last.Result, "dp_" + p); }
                Assert.AreEqual(0, svc.Gems - gems, "the Daily track pays no gems");
                Assert.AreEqual(0, TotalPowerUps(svc), "the daily track pays no power-up");
                if (last.HeroIds.Count == 1)
                {
                    Assert.AreEqual(200 + 400 + 600 + 800, svc.Coins - coins, "the hero roll won: the four coin gifts only");
                    Assert.AreEqual("momo", last.HeroIds[0], "only region 1 is open on a fresh account: Momo, another copy");
                    Assert.AreEqual(copies + 1, svc.Copies("momo"));
                }
                else
                {
                    Assert.AreEqual(0, last.HeroIds.Count);
                    Assert.AreEqual(200 + 400 + 600 + 800 + 1000, svc.Coins - coins, "the hero roll missed: 1,000 coins instead");
                    Assert.AreEqual(1000, last.Coins);
                    Assert.AreEqual(copies, svc.Copies("momo"));
                }
                Assert.IsFalse(svc.HasClaimable(RewardTab.Daily));
            }
            finally { MedalTargets.Source = savedSource; }
        }

        [Test]
        public void TheNewStatKindsCountJumpsEndlessRunsCoinsMedalsAndTimeAttackAndResetWithThePeriod()
        {
            IMedalTargetSource savedSource = MedalTargets.Source;
            try
            {
                var table = new DictionaryMedalTargets();
                RegionDef r1 = repo.GetRegionByIndex(1);
                table.Set(r1.Levels[0], 2, 1000);
                MedalTargets.Source = table;
                ProgressService svc = NewService();

                svc.RecordRunStats(new RunOutcome { Jumps = 120, Region = 1 });
                svc.RecordRunStats(new RunOutcome { Jumps = 30, Region = 1 });
                Assert.AreEqual(150, Daily(svc, RewardStat.Jumps));
                Assert.AreEqual(150, Weekly(svc, RewardStat.Jumps));

                svc.ApplyEndlessRun(r1.Id, 9, "20261102");     // under the minimum: not a finished run
                Assert.AreEqual(0, Daily(svc, RewardStat.EndlessRuns));
                svc.ApplyEndlessRun(r1.Id, 40, "20261102");
                svc.ApplyEndlessRun(r1.Id, 10, "20261102");
                Assert.AreEqual(2, Daily(svc, RewardStat.EndlessRuns));
                Assert.AreEqual(2, Weekly(svc, RewardStat.EndlessRuns));
                svc.ApplyTimeAttackRun(r1.Id, 80, "20261102");
                Assert.AreEqual(2, Daily(svc, RewardStat.EndlessRuns), "Time Attack is not an Endless run");

                int coinsAfterEndless = Daily(svc, RewardStat.CoinsEarned);
                Assert.Greater(coinsAfterEndless, 0, "endless and Time Attack payouts count as coins earned");
                ClearLevels(svc, 1, 1, 1);   // 56 + 48 = 104 coins for a 1-star first clear
                Assert.AreEqual(coinsAfterEndless + 104, Daily(svc, RewardStat.CoinsEarned));
                Assert.AreEqual(coinsAfterEndless + 104, Weekly(svc, RewardStat.CoinsEarned));

                Assert.AreEqual(0, Daily(svc, RewardStat.Medals));
                svc.RecordRun(LevelRecord(r1.Levels[0], 60, 3600));   // the first medal of the level and mode (gold)
                Assert.AreEqual(1, Daily(svc, RewardStat.Medals));
                svc.RecordRun(LevelRecord(r1.Levels[0], 61, 3600));   // a better run, same gold: not a new medal
                Assert.AreEqual(1, Daily(svc, RewardStat.Medals));
                Assert.AreEqual(1, Weekly(svc, RewardStat.Medals));

                svc.RecordRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = r1.Id, Floors = 70, Ticks = TimeRecords.TimeAttackTicks, Mode = 2 });
                svc.RecordRun(new RunRecord { Kind = RunKind.TimeAttack, RegionId = r1.Id, Floors = 55, Ticks = TimeRecords.TimeAttackTicks, Mode = 1 });
                Assert.AreEqual(70, Daily(svc, RewardStat.TimeAttackRun), "the best counted Time Attack run of today");

                var again = new ProgressService(repo, store, 5) { Clock = () => now };
                Assert.AreEqual(150, Daily(again, RewardStat.Jumps), "additive save fields survive a reload");
                Assert.AreEqual(70, Daily(again, RewardStat.TimeAttackRun));

                now = now.AddDays(1);   // Tuesday: the day restarts, the week keeps its counters
                foreach (string stat in new[] { RewardStat.Jumps, RewardStat.EndlessRuns, RewardStat.CoinsEarned, RewardStat.Medals, RewardStat.TimeAttackRun })
                    Assert.AreEqual(0, Daily(svc, stat), stat + " restarts with the day");
                Assert.AreEqual(150, Weekly(svc, RewardStat.Jumps));
                Assert.AreEqual(2, Weekly(svc, RewardStat.EndlessRuns));
                Assert.AreEqual(1, Weekly(svc, RewardStat.Medals));
                now = Monday.AddDays(7);   // next Monday
                Assert.AreEqual(0, Weekly(svc, RewardStat.Jumps));
                Assert.AreEqual(0, Weekly(svc, RewardStat.CoinsEarned));
            }
            finally { MedalTargets.Source = savedSource; }
        }

        // ---- resets with the clock ----

        [Test]
        public void DailyCountersAndClaimsResetAtLocalMidnightButTheWeekKeepsGoing()
        {
            now = new DateTime(2026, 11, 3, 23, 59, 0);   // Tuesday
            ProgressService svc = NewService();
            EarnFortyFivePoints(svc);
            Assert.AreEqual(45, svc.DailyPoints());
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Daily, "dp_40").Result);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Daily, "dp_40").Result);

            now = new DateTime(2026, 11, 4, 0, 0, 30);   // Wednesday, 30 seconds after midnight
            Assert.AreEqual(0, svc.DailyPoints());
            Assert.IsFalse(svc.IsRewardClaimed(RewardTab.Daily, svc.RewardDefs.Find(RewardTab.Daily, "dp_40")));
            Assert.IsFalse(svc.HasClaimable());
            Assert.AreEqual(1, Weekly(svc, RewardStat.LoginDays), "the week still has Tuesday's login");
            Assert.AreEqual(30, Weekly(svc, RewardStat.PlayMinutes));
            EarnFortyFivePoints(svc);
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Daily, "dp_40").Result, "claimable again on the new day");
            Assert.AreEqual(2, Weekly(svc, RewardStat.LoginDays));
            Assert.AreEqual(60, Weekly(svc, RewardStat.PlayMinutes));
        }

        [Test]
        public void WeeklyCountersAndClaimsResetOnMondayLocalTime()
        {
            ProgressService svc = NewService();
            for (int d = 0; d < 7; d++) { now = Monday.AddDays(d); svc.RecordLogin(); }   // Monday to Sunday
            Assert.AreEqual(7, Weekly(svc, RewardStat.LoginDays));
            int gems = svc.Gems;
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w_login5").Result);
            Assert.AreEqual(0, svc.Gems - gems, "w_login5 pays coins only (gems only on the hard-skill tasks)");
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Weekly, "w_login5").Result);

            now = new DateTime(2026, 11, 8, 23, 59, 59);   // still Sunday
            Assert.AreEqual(7, Weekly(svc, RewardStat.LoginDays));
            now = new DateTime(2026, 11, 9, 0, 0, 1);      // Monday: a new week
            Assert.AreEqual(0, Weekly(svc, RewardStat.LoginDays));
            Assert.IsFalse(svc.IsRewardClaimed(RewardTab.Weekly, svc.RewardDefs.Find(RewardTab.Weekly, "w_login5")));
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Weekly, "w_login5").Result);
        }

        [Test]
        public void ResetCountdownsPointAtMidnightAndMonday()
        {
            ProgressService svc = NewService();
            now = new DateTime(2026, 11, 4, 22, 30, 0);   // Wednesday
            Assert.AreEqual(TimeSpan.FromMinutes(90), svc.DailyResetIn());
            Assert.AreEqual(new TimeSpan(4, 1, 30, 0), svc.WeeklyResetIn(), "Thursday to Sunday is 4 days, then 90 minutes");
            now = new DateTime(2026, 11, 9, 0, 0, 0);     // exactly Monday midnight
            Assert.AreEqual(TimeSpan.FromDays(1), svc.DailyResetIn());
            Assert.AreEqual(TimeSpan.FromDays(7), svc.WeeklyResetIn());
        }

        [Test]
        public void AClockSetBackwardsNeverResetsOrReopensAPeriod()
        {
            ProgressService svc = NewService();
            now = Monday.AddDays(2);
            EarnFortyFivePoints(svc);
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Daily, "dp_40").Result);
            now = Monday;   // back to Monday
            Assert.AreEqual(45, svc.DailyPoints(), "no reset when the date goes back");
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Daily, "dp_40").Result);
        }

        // ---- claims ----

        [Test]
        public void ClaimNeedsTheTargetPaysOnceAndRejectsUnknownIds()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();   // 5 points
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Daily, "dp_40").Result);
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimReward(RewardTab.Daily, "nope").Result);
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimReward(RewardTab.Weekly, "dp_40").Result, "ids are per tab");
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimReward(RewardTab.Daily, null).Result);
            EarnFortyFivePoints(svc);
            int coins = svc.Coins;
            RewardClaimOutcome ok = svc.TryClaimReward(RewardTab.Daily, "dp_40");
            Assert.AreEqual(RewardClaimResult.Ok, ok.Result);
            Assert.AreEqual(200, ok.Coins);
            Assert.AreEqual(coins + 200, svc.Coins);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Daily, "dp_40").Result);
            Assert.AreEqual(coins + 200, svc.Coins, "a second claim pays nothing");
        }

        [Test]
        public void FailedClaimsChangeNothingInTheSave()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.TimedTimeAttackFloorsThisWeek();   // the first read starts today's timed counters; snapshot after that
            string before = JsonConvert.SerializeObject(svc.Data);
            svc.TryClaimReward(RewardTab.Daily, "dp_40");   // not ready
            svc.TryClaimReward(RewardTab.Main, "g_1_stars_1");   // not ready
            svc.TryClaimReward(RewardTab.Weekly, "bogus");
            Assert.AreEqual(before, JsonConvert.SerializeObject(svc.Data));
        }

        [Test]
        public void AClaimIsOneSaveWithWalletStockAndMarkTogether()
        {
            ProgressService svc = NewService();
            svc.RecordRunStats(new RunOutcome { PowerUpsUsed = 10 });
            int saves = 0;
            svc.Changed += () => saves++;
            int units = TotalPowerUps(svc);
            RewardClaimOutcome res = svc.TryClaimReward(RewardTab.Weekly, "w_powerups10");
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(1, saves, "one save per claim");
            Assert.AreEqual(2, res.PowerUpIds.Count);
            Assert.AreEqual(units + 2, TotalPowerUps(svc));
            Assert.GreaterOrEqual(svc.PowerUpCount(res.PowerUpIds[0]), 1);
            var reloaded = new ProgressService(repo, store, 5) { Clock = () => now };
            Assert.IsTrue(reloaded.IsRewardClaimed(RewardTab.Weekly, reloaded.RewardDefs.Find(RewardTab.Weekly, "w_powerups10")));
            Assert.AreEqual(2, TotalPowerUps(reloaded), "the stock and the mark reached the same save");
        }

        [Test]
        public void ClaimsSaturateInsteadOfOverflowing()
        {
            ProgressService svc = NewService();
            svc.AddGems(int.MaxValue - 2);
            svc.RecordRunStats(new RunOutcome { ComboChains = 300 });   // w_combos300 pays 5 gems
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w_combos300").Result);
            Assert.AreEqual(int.MaxValue, svc.Gems);
        }

        [Test]
        public void WeeklyTasksPayTheirRewards()
        {
            ProgressService svc = NewService();
            svc.AddPlaySeconds(100 * 60);
            svc.RecordRunStats(new RunOutcome { ComboChains = 150 });
            svc.RecordLogin();
            int coins = svc.Coins, gems = svc.Gems;
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w_play100").Result);
            Assert.AreEqual(250, svc.Coins - coins);
            Assert.AreEqual(0, svc.Gems - gems);
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Weekly, "w_login5").Result, "one login day is not five");
            int units = TotalPowerUps(svc);
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w_combos150").Result);
            Assert.AreEqual(250 + 300, svc.Coins - coins);
            Assert.AreEqual(0, svc.Gems - gems);
            Assert.AreEqual(0, TotalPowerUps(svc) - units, "the 150 combos task pays no power-up any more");
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Weekly, "w_hard5").Result);
        }

        [Test]
        public void EndlessFloorFeedsTheWeeklyTask()
        {
            ProgressService svc = NewService();
            svc.ApplyEndlessRun("region_001_meadow_springs", 80, "20261102");
            Assert.AreEqual(80, Weekly(svc, RewardStat.EndlessFloor));
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Weekly, "w_endless100").Result);
            svc.ApplyEndlessRun("region_001_meadow_springs", 120, "20261102");
            svc.ApplyEndlessRun("region_001_meadow_springs", 30, "20261102");
            Assert.AreEqual(120, Weekly(svc, RewardStat.EndlessFloor), "the best floor of the week");
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w_endless100").Result);
        }

        /// <summary>A service on its own save with a fixed per-account seed, five hard clears done (w_hard5 ready).</summary>
        ProgressService HardFiveService(int seed, MemorySaveStore st)
        {
            var seeded = new SaveData();
            seeded.Bundles.Seed = seed;
            st.Save(seeded);
            var svc = new ProgressService(repo, st, 5) { Clock = () => now };
            svc.RecordLevelStarted(3);
            ClearLevels(svc, 1, 5);   // five hard clears
            return svc;
        }

        /// <summary>First seed from <paramref name="from"/> upward whose w_hard5 roll gives the hero (true) or the coins (false) this week.</summary>
        int FindSeed(bool hero, int from)
        {
            for (int seed = from; seed < from + 200; seed++)
            {
                ProgressService svc = HardFiveService(seed, new MemorySaveStore());
                if (svc.HeroOrCoinsWins(RewardTab.Weekly, svc.RewardDefs.Find(RewardTab.Weekly, "w_hard5")) == hero) return seed;
            }
            Assert.Fail("no seed found");
            return 0;
        }

        [Test]
        public void TheTopWeeklyTaskGivesTheCommonHeroCopyWhenTheRollWins()
        {
            var st = new MemorySaveStore();
            ProgressService svc = HardFiveService(FindSeed(true, 1), st);
            Assert.AreEqual(5, Weekly(svc, RewardStat.HardClear));
            int before = svc.Copies("momo"), coins = svc.Coins, gems = svc.Gems;
            RewardClaimOutcome res = svc.TryClaimReward(RewardTab.Weekly, "w_hard5");
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(1, res.HeroIds.Count);
            Assert.AreEqual("momo", res.HeroIds[0], "only region 1 is open: Momo, its one ACTIVE Common hero");
            Assert.AreEqual(before + 1, svc.Copies("momo"));
            Assert.AreEqual(200, svc.Coins - coins, "the task's own 200 coins, no hero-miss coins");
            Assert.AreEqual(4, svc.Gems - gems);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Weekly, "w_hard5").Result);
            Assert.AreEqual(before + 1, svc.Copies("momo"), "no second copy");
        }

        [Test]
        public void TheTopWeeklyTaskGivesOneThousandMoreCoinsWhenTheRollMisses()
        {
            var st = new MemorySaveStore();
            ProgressService svc = HardFiveService(FindSeed(false, 1), st);
            int before = svc.Copies("momo"), coins = svc.Coins;
            RewardClaimOutcome res = svc.TryClaimReward(RewardTab.Weekly, "w_hard5");
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(0, res.HeroIds.Count);
            Assert.AreEqual(200 + 1000, res.Coins);
            Assert.AreEqual(coins + 1200, svc.Coins);
            Assert.AreEqual(before, svc.Copies("momo"));
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Weekly, "w_hard5").Result);
            Assert.AreEqual(coins + 1200, svc.Coins, "no second payment");
        }

        [Test]
        public void TheHeroOrCoinsRollIsStoredSoReloadingCannotReRollIt()
        {
            int seed = FindSeed(false, 1);
            var st = new MemorySaveStore();
            ProgressService svc = HardFiveService(seed, st);
            RewardEntry e = svc.RewardDefs.Find(RewardTab.Weekly, "w_hard5");
            Assert.IsFalse(svc.HeroOrCoinsWins(RewardTab.Weekly, e));
            Assert.AreEqual(1, svc.Data.Rewards.HeroRolls.Count, "the outcome is stored in the save");
            // a stored outcome wins over the seed: even a changed seed gives the same answer, and a reload keeps it
            svc.Data.Rewards.HeroRolls[new System.Collections.Generic.List<string>(svc.Data.Rewards.HeroRolls.Keys)[0]] = 0;
            svc.Data.Bundles.Seed = FindSeed(true, 1);
            Assert.IsFalse(svc.HeroOrCoinsWins(RewardTab.Weekly, e), "decided once, never re-rolled");
            RewardClaimOutcome res = svc.TryClaimReward(RewardTab.Weekly, "w_hard5");
            Assert.AreEqual(0, res.HeroIds.Count);
            var again = new ProgressService(repo, st, 5) { Clock = () => now };
            Assert.IsFalse(again.HeroOrCoinsWins(RewardTab.Weekly, again.RewardDefs.Find(RewardTab.Weekly, "w_hard5")), "same outcome after a reload");
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, again.TryClaimReward(RewardTab.Weekly, "w_hard5").Result);
        }

        [Test]
        public void TheHeroOrCoinsRollIsAPureFunctionOfTheSeedAndTheDifferentSeedsCanDiffer()
        {
            bool sawHero = false, sawCoins = false;
            for (int seed = 1; seed <= 60; seed++)
            {
                ProgressService a = HardFiveService(seed, new MemorySaveStore());
                ProgressService b = HardFiveService(seed, new MemorySaveStore());
                RewardEntry e = a.RewardDefs.Find(RewardTab.Weekly, "w_hard5");
                bool wa = a.HeroOrCoinsWins(RewardTab.Weekly, e);
                Assert.AreEqual(wa, b.HeroOrCoinsWins(RewardTab.Weekly, b.RewardDefs.Find(RewardTab.Weekly, "w_hard5")), "same seed, same roll (seed " + seed + ")");
                if (wa) sawHero = true; else sawCoins = true;
            }
            Assert.IsTrue(sawHero && sawCoins, "50 percent: both outcomes appear across seeds");
        }

        [Test]
        public void TheDailyTopGiftUsesTheSameStoredRollPerDay()
        {
            var st = new MemorySaveStore();
            var seeded = new SaveData();
            seeded.Bundles.Seed = 4242;
            st.Save(seeded);
            var svc = new ProgressService(repo, st, 5) { Clock = () => now };
            svc.AddPlaySeconds(1);
            RewardEntry top = svc.RewardDefs.Find(RewardTab.Daily, "dp_200");
            bool today = svc.HeroOrCoinsWins(RewardTab.Daily, top);
            Assert.AreEqual(today, svc.HeroOrCoinsWins(RewardTab.Daily, top));
            now = Monday.AddDays(1);
            svc.RecordLogin();   // a new day starts: the old day's stored roll is dropped
            svc.HeroOrCoinsWins(RewardTab.Daily, top);
            Assert.LessOrEqual(svc.Data.Rewards.HeroRolls.Count, 2, "only the current day and week are kept");
        }

        static int TotalOf(ProgressService svc, string[] ids)
        {
            int n = 0;
            foreach (string id in ids) n += svc.Copies(id);
            return n;
        }

        [Test]
        public void TheRandomWeeklyHeroIsRepeatableFromTheSeedAndNeverFromAClosedRegion()
        {
            string[] picks = new string[2];
            int heroSeed = FindSeed(true, 12345);   // a seed whose w_hard5 roll gives the hero
            for (int run = 0; run < 2; run++)
            {
                var st = new MemorySaveStore();
                var seeded = new SaveData();
                seeded.Bundles.Seed = heroSeed;
                seeded.UnlockedRegions.Add("region_002_mushroom_grove");
                seeded.UnlockedRegions.Add("region_003_sunny_beach");
                st.Save(seeded);
                var svc = new ProgressService(repo, st, 5) { Clock = () => now };
                svc.AddPlaySeconds(1);
                svc.Data.Rewards.Week.Stats[RewardStat.HardClear] = 5;
                RewardClaimOutcome res = svc.TryClaimReward(RewardTab.Weekly, "w_hard5");
                Assert.AreEqual(RewardClaimResult.Ok, res.Result);
                picks[run] = res.HeroIds[0];
                CollectionAssert.Contains(new[] { "momo", "forestgirl", "beachotter" }, picks[run], "regions 4 and 5 are closed");
            }
            Assert.AreEqual(picks[0], picks[1], "same seed and claim count, same hero");
        }

        // ---- Main: the region goals are tested in RegionGoalTests ----

        [Test]
        public void ClaimAllOnDailyAndWeeklyKeepsTabsApart()
        {
            ProgressService svc = NewService();
            EarnFortyFivePoints(svc);   // 45 points
            Assert.AreEqual(RewardClaimResult.NotReady, svc.ClaimAll(RewardTab.Weekly).Result);
            RewardClaimOutcome res = svc.ClaimAll(RewardTab.Daily);
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(200, res.Coins);
            Assert.IsTrue(svc.IsRewardClaimed(RewardTab.Daily, svc.RewardDefs.Find(RewardTab.Daily, "dp_40")));
        }

        // ---- red dot, saving ----

        [Test]
        public void HasClaimableFollowsProgressAndClaims()
        {
            ProgressService svc = NewService();
            Assert.IsFalse(svc.HasClaimable());
            EarnFortyFivePoints(svc);
            Assert.IsTrue(svc.HasClaimable(RewardTab.Daily));
            Assert.IsTrue(svc.HasClaimable());
            Assert.IsFalse(svc.HasClaimable(RewardTab.Weekly));
            svc.TryClaimReward(RewardTab.Daily, "dp_40");
            Assert.IsFalse(svc.HasClaimable());
        }

        [Test]
        public void CountersAndClaimsSurviveASaveRoundTrip()
        {
            ProgressService svc = NewService();
            EarnFortyFivePoints(svc);
            svc.RecordRunStats(new RunOutcome { ComboChains = 4, BestCombo = 6 });
            ClearLevels(svc, 1, 10, 7);   // 30 stars (g_1_stars_2)
            svc.TryClaimReward(RewardTab.Daily, "dp_40");
            svc.TryClaimReward(RewardTab.Main, "g_1_stars_2");
            svc.FlushPlayTime();

            var again = new ProgressService(repo, store, 5) { Clock = () => now };
            Assert.AreEqual(1, Daily(again, RewardStat.Login));
            Assert.AreEqual(1800, Daily(again, RewardStat.PlaySeconds));
            Assert.AreEqual(34, Daily(again, RewardStat.Combos));
            Assert.AreEqual(10, Daily(again, RewardStat.Levels));
            Assert.AreEqual(6, again.BestComboEver);
            Assert.AreEqual(1, Weekly(again, RewardStat.LoginDays));
            Assert.IsTrue(again.IsRewardClaimed(RewardTab.Daily, again.RewardDefs.Find(RewardTab.Daily, "dp_40")));
            Assert.IsTrue(again.IsRewardClaimed(RewardTab.Main, again.RewardDefs.Find(RewardTab.Main, "g_1_stars_2")));
            Assert.AreEqual(svc.Gems, again.Gems);
        }

        sealed class OldSaveStore : ISaveStore
        {
            public SaveData Load() { var d = new SaveData(); d.Rewards = null; d.Coins = 7; return d; }
            public void Save(SaveData data) { }
        }

        [Test]
        public void SavesWithoutRewardsLoadWithEmptyCounters()
        {
            var svc = new ProgressService(repo, new OldSaveStore(), 5) { Clock = () => now };
            Assert.AreEqual(7, svc.Coins);
            Assert.AreEqual(0, svc.DailyPoints());
            svc.RecordLogin();
            Assert.AreEqual(5, svc.DailyPoints(), "the login task is worth 5");
            Assert.AreEqual(0, svc.RegionStatsOf(1).Jumps, "no per-region counters in an old save");
            SaveData parsed = JsonConvert.DeserializeObject<SaveData>("{\"coins\":3}");
            Assert.IsNotNull(parsed.Rewards, "an old save file without the rewards block gets the default one");
        }

        // ---- the redesigned screen (docs/26 section 4, RewardsStyle): structure, slots and metrics (the pixel audit is CriticalUxTests rewards-*) ----

        RewardsScreen BuildScreen(ProgressService svc, RewardTab tab, out GameObject root)
        {
            root = new GameObject("RewardsTestRoot", typeof(RectTransform));
            var screen = new RewardsScreen((RectTransform)root.transform, svc, repo, null, null);
            screen.Build(tab);
            return screen;
        }

        [Test]
        public void ScreenBuildsOneRowPerEntryOnEveryTabWithoutArt()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Daily, out root);
            try
            {
                Assert.IsNotNull(root.transform.Find("RewardsRoot/Body/Tabs"));
                Assert.AreEqual(1 + 20, s.ListContent.childCount, "scene header + 20 tasks");
                Assert.IsNotNull(s.ListContent.Find("Scene/TrackPanel/Cells/Cell40"), "the first chest of the points track");
                Assert.IsNotNull(s.ListContent.Find("Scene/TrackPanel/Cells/Cell200"), "the last chest (the hero gift) of the points track");
                s.SelectTab(RewardTab.Weekly);
                Assert.AreEqual(1 + 25, s.ListContent.childCount, "scene header + 25 tasks");
                s.SelectTab(RewardTab.Main);
                Assert.AreEqual(49 + 1, s.ListContent.childCount, "region 1: 49 goal cards + the Endless divider");
                Assert.IsNull(root.transform.Find("RewardsRoot/Body/MainHeader/Band"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReadyCardsAreButtonsWithAGreenClaimButtonAndClaimAllFollowsTheSelectedRegion()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 5);   // g_1_levels_1 (clear 5 levels) is ready, nothing else
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                s.SelectRegion(1);
                Transform ready = s.ListContent.Find("Row_g_1_levels_1/Card");
                Assert.IsNotNull(ready.GetComponent<Button>(), "a ready card is the claim button");
                Assert.IsNotNull(ready.Find("RewardPlate").GetComponent<Button>(), "with a green Claim button as its reward plate");
                Transform open = s.ListContent.Find("Row_g_1_levels_2/Card");
                Assert.IsNull(open.GetComponent<Button>());
                Assert.IsNull(open.Find("RewardPlate").GetComponent<Button>(), "an open card has a plain reward plate");
                Assert.IsNotNull(root.transform.Find("RewardsRoot/Body/ClaimAll"), "Claim All while something is ready in the selected region");
                s.SelectRegion(2);
                Assert.IsNull(root.transform.Find("RewardsRoot/Body/ClaimAll"), "nothing is ready in region 2: no Claim All there");
                s.SelectRegion(1);
                Assert.IsNotNull(root.transform.Find("RewardsRoot/Body/ClaimAll"));

                s.ClaimAll(RewardTab.Main);
                Assert.IsNull(s.ListContent.Find("Row_g_1_levels_1"), "claimed: the card left the list (it waits in the collapsed Completed section)");
                Assert.IsNotNull(s.ListContent.Find("CompletedHeader"), "Completed (1) header at the bottom");
                Assert.IsNull(root.transform.Find("RewardsRoot/Body/ClaimAll"), "nothing left to claim in the region");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void DailyChestIsAButtonOnlyWhenReached()
        {
            ProgressService svc = NewService();
            EarnFortyFivePoints(svc);   // 45 points: the first chest only
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Daily, out root);
            try
            {
                Assert.IsNotNull(s.ListContent.Find("Scene/TrackPanel/Cells/Cell40").GetComponent<UnityEngine.UI.Button>());
                Assert.IsNull(s.ListContent.Find("Scene/TrackPanel/Cells/Cell80").GetComponent<UnityEngine.UI.Button>());
                Assert.IsNull(s.ListContent.Find("Scene/TrackPanel/Cells/Cell200").GetComponent<UnityEngine.UI.Button>());
                Assert.AreEqual("or 1K", s.ListContent.Find("Scene/TrackPanel/Cells/Cell200/Reward/Count").GetComponent<Text>().text, "the last chest shows the hero gift (hero, or 1,000 coins)");
                Assert.AreEqual("200", s.ListContent.Find("Scene/TrackPanel/Cells/Cell40/Reward/Count").GetComponent<Text>().text);
                Assert.AreEqual("400", s.ListContent.Find("Scene/TrackPanel/Cells/Cell80/Reward/Count").GetComponent<Text>().text);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReadyGoalCardHasAClaimButtonOfAtLeast48DpThatClaimsExactlyThatGoal()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 5);   // g_1_levels_1 = 60 coins; nothing else is complete
            int gems = svc.Gems, coins = svc.Coins;
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                s.SelectRegion(1);
                var card = (RectTransform)s.ListContent.Find("Row_g_1_levels_1/Card");
                var plate = (RectTransform)card.Find("RewardPlate");
                var btn = plate.GetComponent<Button>();
                Assert.IsNotNull(btn);
                Assert.GreaterOrEqual(plate.sizeDelta.x, UiKit.MinTouchPx - 1f, "button width >= 48 dp");
                Assert.GreaterOrEqual(plate.sizeDelta.y, UiKit.MinTouchPx - 1f, "button height >= 48 dp");
                var row = (RectTransform)card.parent;
                Assert.LessOrEqual(plate.sizeDelta.y, row.GetComponent<LayoutElement>().preferredHeight, "the button fits inside its card");
                Assert.IsNotNull(card.GetComponent<Button>(), "the card itself claims too");
                Assert.AreEqual("Claim 60 coins", plate.GetComponent<A11yLabel>().Text);

                btn.onClick.Invoke();   // tap the green button
                RegionGoal goal = svc.RewardDefs.FindGoal("g_1_levels_1");
                Assert.IsTrue(svc.IsRegionGoalClaimed(goal));
                Assert.AreEqual(coins + 60, svc.Coins, "paid exactly the 60 coins of that goal");
                Assert.AreEqual(gems, svc.Gems);
                Assert.AreEqual(1, svc.RegionGoalsClaimedCount(1), "no other goal was claimed");
                Assert.IsNull(s.ListContent.Find("Row_g_1_levels_1"), "claimed: moved into the collapsed Completed section");
                s.ListContent.Find("CompletedHeader/Toggle").GetComponent<Button>().onClick.Invoke();
                Assert.IsNotNull(s.ListContent.Find("Row_g_1_levels_1/Small/Check"), "expanded: the small completed card keeps the check badge");
                Assert.IsNull(s.ListContent.Find("Row_g_1_levels_1/Small").GetComponent<Button>(), "a completed card has no button");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void WeeklyReadyCardGetsTheSameClaimButton()
        {
            ProgressService svc = NewService();
            svc.AddPlaySeconds(100 * 60);
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Weekly, out root);
            try
            {
                var plate = (RectTransform)s.ListContent.Find("Row_w_play100/Card/RewardPlate");
                var btn = plate.GetComponent<UnityEngine.UI.Button>();
                Assert.IsNotNull(btn);
                Assert.GreaterOrEqual(plate.sizeDelta.y, UiKit.MinTouchPx - 1f);
                int coins = svc.Coins;
                btn.onClick.Invoke();
                Assert.AreEqual(coins + 250, svc.Coins);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void MainHeaderHasTheSceneBehindTheStripAndNoBandOrRail()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                Transform header = root.transform.Find("RewardsRoot/Body/MainHeader");
                Assert.IsNotNull(header, "the header is pinned above the list, not inside it");
                Assert.IsNull(s.ListContent.Find("MainHeader"), "...so the horizontal strip never fights the vertical list");
                Assert.IsNotNull(header.Find("Scene/SceneClip/Backdrop"), "the scene picture (or its sky fallback) sits behind the circle strip");
                Assert.IsNotNull(header.Find("Backdrop"), "dark glass strip behind the names");
                Assert.Less(header.Find("Scene").GetSiblingIndex(), header.Find("Strip").GetSiblingIndex(), "the scene is drawn first (behind)");
                Assert.IsNull(header.Find("Band"), "the purple band is gone");
                Assert.IsNull(s.ListContent.Find("Row_s_25"), "the 25..225 star cards are gone");
                foreach (Transform t in s.ListContent) Assert.IsNull(t.Find("Rail"), "no numbered rail");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ---- Main tab, Bug List 5 F1.a: the region strip, the goal lists, the small reward card ----

        [Test]
        public void RegionStripHas30EntriesFivePlayableAndTwentyFiveComingSoon()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                Assert.AreEqual(30, s.StripRow.childCount, "all 30 regions, built once");
                int playable = 0, soon = 0;
                for (int i = 1; i <= 30; i++)
                {
                    Transform slot = s.StripRow.Find("Slot" + i);
                    Assert.IsNotNull(slot, "Slot" + i);
                    string name = slot.Find("Name").GetComponent<Text>().text;
                    var circle = (RectTransform)slot.Find("Region" + i);
                    Assert.GreaterOrEqual(circle.sizeDelta.x, UiKit.MinTouchPx - 1f, "circle " + i + " is a 48 dp touch target");
                    if (i <= 5)
                    {
                        playable++;
                        Assert.AreNotEqual("Coming\nSoon", name, "region " + i + " keeps its real name");
                        Assert.IsFalse(name.Contains(" "), "names are split by hand: never a break inside a word");
                    }
                    else
                    {
                        soon++;
                        Assert.AreEqual("Coming\nSoon", name, "region " + i + " is Coming Soon (no real name)");
                        Assert.IsNotNull(circle.Find("State"), "region " + i + " shows a lock");
                        StringAssert.Contains("Coming Soon", circle.GetComponent<A11yLabel>().Text);
                    }
                }
                Assert.AreEqual(5, playable);
                Assert.AreEqual(25, soon);
                Assert.AreEqual(1, s.SelectedRegion);
                Assert.Greater(((RectTransform)s.StripRow.Find("Slot1/Region1")).sizeDelta.x, ((RectTransform)s.StripRow.Find("Slot2/Region2")).sizeDelta.x, "the selected circle is the big one");
                Assert.IsNotNull(s.StripRow.Find("Slot1/Ring"), "with a ring");
                Assert.IsNotNull(s.StripRow.Find("Slot1/Pointer"), "and a pointer");
                s.SelectRegion(4);
                Assert.IsNotNull(s.StripRow.Find("Slot4/Ring"), "the ring moves with the selection");
                Assert.IsNull(s.StripRow.Find("Slot1/Ring"));
                Assert.AreEqual(30, s.StripRow.childCount, "selecting never rebuilds or adds strip entries");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void StripSnapsAndCentresTheSelectedCircle()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                var snap = s.StripRow.GetComponentInParent<RegionStripSnap>();
                Assert.IsNotNull(snap);
                var sr = snap.GetComponent<ScrollRect>();
                Assert.IsTrue(sr.horizontal && !sr.vertical, "the strip scrolls sideways only");
                Assert.AreEqual(-0f * RewardsScreen.StripSlotW, snap.Content.anchoredPosition.x, 0.01f, "region 1 starts centred");
                Assert.AreEqual(-3f * RewardsScreen.StripSlotW, snap.XFor(3), 0.01f, "slot i is centred at content x = -i * slot width (padding of half a viewport at both ends)");
                snap.CenterOn(3, false);
                Assert.AreEqual(3, snap.NearestIndex);
                Assert.AreEqual(snap.XFor(3), snap.Content.anchoredPosition.x, 0.01f);
                snap.CenterOn(29, false);
                Assert.AreEqual(29, snap.NearestIndex, "the last circle can be centred too");
                // the glide is time based and rests at the target
                snap.CenterOn(0, true);
                for (int i = 0; i < 400; i++) snap.Step(0.016f);
                Assert.AreEqual(snap.XFor(0), snap.Content.anchoredPosition.x, 0.01f);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void SelectingARegionRebuildsTheGoalListWith44GoalsInTypeOrderAndEndlessLast()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                string firstOfPrevious = null;
                for (int r = 1; r <= 5; r++)
                {
                    s.SelectRegion(r);
                    var rows = new System.Collections.Generic.List<string>();
                    int dividerAt = -1, i = 0;
                    foreach (Transform t in s.ListContent)
                    {
                        if (t.name.StartsWith("Row_", StringComparison.Ordinal)) rows.Add(t.name.Substring(4));
                        else if (t.name == "EndlessDivider") dividerAt = rows.Count;
                        i++;
                    }
                    var goals = svc.RegionGoals(r);
                    Assert.AreEqual(49, goals.Count);
                    Assert.AreEqual(goals.Count, rows.Count, "region " + r + ": one card per goal");
                    for (int k = 0; k < goals.Count; k++) Assert.AreEqual(goals[k].Id, rows[k], "region " + r + " card " + k + " follows the type-group order");
                    int firstEndless = 0;
                    while (!goals[firstEndless].IsEndless) firstEndless++;
                    Assert.AreEqual(firstEndless, dividerAt, "the single 'Endless' divider sits right before the first endless tier");
                    for (int k = firstEndless; k < goals.Count; k++) Assert.IsTrue(goals[k].IsEndless, "endless tiers come last");
                    Assert.AreEqual(r, s.SelectedRegion);
                    Assert.AreNotEqual(firstOfPrevious, rows[0], "the list changes with every region (it is not static)");
                    firstOfPrevious = rows[0];
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ComingSoonRegionShowsOneLockedCardAndNoGoals()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                s.SelectRegion(8);
                int cards = 0;
                foreach (Transform t in s.ListContent) if (t.name.StartsWith("Row_", StringComparison.Ordinal)) cards++;
                Assert.AreEqual(0, cards, "no goal cards in a Coming Soon region");
                Assert.AreEqual(1, s.ListContent.childCount, "exactly one card");
                Transform soon = s.ListContent.Find("ComingSoon");
                Assert.IsNotNull(soon);
                Assert.AreEqual("Coming Soon", soon.Find("Label").GetComponent<Text>().text);
                Assert.IsNotNull(soon.Find("Lock"), "with a lock icon");
                Assert.IsNull(root.transform.Find("RewardsRoot/Body/ClaimAll"), "nothing to claim");
                s.SelectRegion(1);
                Assert.IsNull(s.ListContent.Find("ComingSoon"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ClaimAllOnMainClaimsOnlyTheSelectedRegion()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 25, 7);   // region 1: stars, levels, 3-star goals ready (and region 2 opens)
            ClearLevels(svc, 2, 5);        // region 2: g_2_levels_1 ready
            Assert.IsTrue(svc.HasClaimableInRegion(1));
            Assert.IsTrue(svc.HasClaimableInRegion(2));
            int readyOne = svc.RegionGoalsReadyCount(1);
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                s.SelectRegion(2);
                RewardClaimOutcome res = s.ClaimAll(RewardTab.Main);
                Assert.AreEqual(RewardClaimResult.Ok, res.Result);
                Assert.AreEqual(0, svc.RegionGoalsReadyCount(2), "region 2 is done");
                Assert.AreEqual(readyOne, svc.RegionGoalsReadyCount(1), "region 1 was not touched");
                Assert.AreEqual(0, svc.RegionGoalsClaimedCount(1));
                Assert.IsTrue(svc.HasClaimableMain(), "region 1 still has something to claim (red dot on its circle)");
                Assert.IsNotNull(s.StripRow.Find("Slot1/Region1/Badge"), "the circle of region 1 carries the alert dot");
                Assert.IsNull(s.StripRow.Find("Slot2/Region2/Badge"), "region 2 has nothing left");
                s.SelectRegion(1);
                Assert.AreEqual(RewardClaimResult.Ok, s.ClaimAll(RewardTab.Main).Result);
                Assert.AreEqual(0, svc.RegionGoalsReadyCount(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void HeroGoalAndWeeklyHeroShowAHeroFaceOnTheirRewardPlate()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                s.SelectRegion(1);
                Transform face = s.ListContent.Find("Row_g_1_levels_5/Card/RewardPlate/Hero");
                Assert.IsNotNull(face, "'clear all levels' gives the region hero: its face is on the plate");
                StringAssert.Contains("hero", face.GetComponent<A11yLabel>().Text);
                Assert.IsNotNull(s.ListContent.Find("Row_g_1_gold_5/Card/RewardPlate/Hero"), "'gold medals on 25 levels' hands out a random ACTIVE Common hero: the hero chip is on the plate");
                Assert.IsNull(s.ListContent.Find("Row_g_1_levels_4/Card/RewardPlate/Hero"), "'clear 20 levels' pays coins now (owner 2026-11-13: only two hero gifts per region)");
                Assert.IsNull(s.ListContent.Find("Row_g_1_levels_1/Card/RewardPlate/Hero"), "other goals have no hero");
                s.SelectTab(RewardTab.Weekly);
                Transform weekly = s.ListContent.Find("Row_w_hard5/Card/RewardPlate/Hero");
                Assert.IsNotNull(weekly, "the weekly top reward shows a hero chip");
                Assert.AreEqual("Random hero", weekly.GetComponent<A11yLabel>().Text);
                Assert.AreEqual("or 1K", s.ListContent.Find("Row_w_hard5/Card/RewardPlate").GetComponentsInChildren<Text>()[2].text, "hero, then 'or 1K' coins (the 50 percent roll misses)");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        GameObject lastOverlay;

        RewardsScreen BuildScreenWithHost(ProgressService svc, RewardTab tab, out GameObject root)
        {
            GameObject r = new GameObject("RewardsTestRoot", typeof(RectTransform));
            root = r;
            var host = new RewardsScreen.Host
            {
                NewOverlay = () =>
                {
                    lastOverlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
                    lastOverlay.transform.SetParent(r.transform, false);
                    return (RectTransform)lastOverlay.transform;
                },
                CloseOverlay = () => { if (lastOverlay != null) UnityEngine.Object.DestroyImmediate(lastOverlay); lastOverlay = null; }
            };
            var screen = new RewardsScreen((RectTransform)r.transform, svc, repo, null, host);
            screen.Build(tab);
            return screen;
        }

        [Test]
        public void SmallRewardCardHasIconChipsAndNoLongText()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreenWithHost(svc, RewardTab.Main, out root);
            try
            {
                var res = new RewardClaimOutcome { Result = RewardClaimResult.Ok, Gems = 40, Coins = 2000 };
                res.PowerUpIds.Add("pw_flight"); res.PowerUpIds.Add("pw_flight"); res.PowerUpIds.Add("pw_shield");
                s.ShowClaimed(res);
                RectTransform card = s.RewardCard;
                Assert.IsNotNull(card);
                int chips = 0;
                foreach (Transform t in card) if (t.name == "Chip") chips++;
                Assert.AreEqual(4, chips, "gem, coin and one chip per power-up type");
                foreach (Text t in card.GetComponentsInChildren<Text>()) Assert.LessOrEqual(t.text.Length, 5, "amounts only, no sentences: \"" + t.text + "\"");
                Assert.AreEqual("40", card.Find("Chip/Amount").GetComponent<Text>().text);
                // Bug List 8: the card is drawn 50 percent bigger (panel scale), and still fits the 1080 x 1920 reference with the Side margin
                Assert.AreEqual(RewardsScreen.RewardCardScale, card.localScale.x, 0.001f, "50 percent bigger in width");
                Assert.AreEqual(RewardsScreen.RewardCardScale, card.localScale.y, 0.001f, "50 percent bigger in height");
                Assert.LessOrEqual(card.sizeDelta.y * card.localScale.y, 1920f - 48f, "fits the height");
                Assert.LessOrEqual(card.sizeDelta.x * card.localScale.x, 1080f - 48f, "fits the width");
                var ok = (RectTransform)card.Find("OK");
                Assert.IsNotNull(ok.GetComponent<Button>());
                Assert.GreaterOrEqual(ok.sizeDelta.x, UiKit.MinTouchPx - 1f);
                Assert.GreaterOrEqual(ok.sizeDelta.y, UiKit.MinTouchPx - 1f);
                Assert.AreEqual("OK", ok.GetComponent<A11yLabel>().Text);
                StringAssert.Contains("40 gems", card.GetComponent<A11yLabel>().Text);

                // closes on OK, on a tap anywhere and by itself after about 4 seconds
                var timer = lastOverlay.GetComponent<RewardCardTimer>();
                Assert.AreEqual(4f, timer.Seconds, 0.01f);
                GameObject first = lastOverlay;
                ok.GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(first == null, "OK closed the card");

                s.ShowClaimed(res);
                GameObject second = lastOverlay;
                second.GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(second == null, "a tap anywhere closed it");

                s.ShowClaimed(res);
                GameObject third = lastOverlay;
                third.GetComponent<RewardCardTimer>().OnTimeUp();
                Assert.IsTrue(third == null, "the timer closes it");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void SmallRewardCardShowsAHeroFacePerHeroCopy()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreenWithHost(svc, RewardTab.Main, out root);
            try
            {
                var res = new RewardClaimOutcome { Result = RewardClaimResult.Ok, Gems = 8 };
                res.HeroIds.Add("forestgirl"); res.HeroIds.Add("forestgirl");
                s.ShowClaimed(res);
                int faces = 0, chips = 0;
                foreach (Transform t in s.RewardCard)
                {
                    if (t.name != "Chip") continue;
                    chips++;
                    if (t.Find("HeroFace") != null) faces++;
                }
                Assert.AreEqual(3, chips, "gems + 2 hero copies");
                Assert.AreEqual(2, faces, "one face portrait per hero copy");
                foreach (Text t in s.RewardCard.GetComponentsInChildren<Text>()) Assert.LessOrEqual(t.text.Length, 5, "no sentences");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ClaimingAGoalShowsTheSmallCardWithItsRewardChips()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 5);
            GameObject root;
            RewardsScreen s = BuildScreenWithHost(svc, RewardTab.Main, out root);
            try
            {
                s.SelectRegion(1);
                s.ListContent.Find("Row_g_1_levels_1/Card/RewardPlate").GetComponent<Button>().onClick.Invoke();
                Assert.IsNotNull(lastOverlay, "the claim opened the reward card");
                Assert.AreEqual("60", s.RewardCard.Find("Chip/Amount").GetComponent<Text>().text, "60 coins: a single coin chip");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void RewardCardIsFiftyPercentBiggerAndStaysInsideTheSafeAreaAtEveryAspect()
        {
            ProgressService svc = NewService();
            Vector2[] screens = { new Vector2(1080f, 1920f), new Vector2(1080f, 2340f), new Vector2(1080f, 2520f), new Vector2(1080f, 1440f) };   // 9:16, 9:19.5, 9:21, 3:4
            float oldScale = UiKit.TextScale;
            try
            {
                foreach (float ts in new[] { 1f, 1.3f })
                {
                    UiKit.TextScale = ts;
                    foreach (Vector2 size in screens)
                    {
                        GameObject root;
                        RewardsScreen s = BuildScreenWithHost(svc, RewardTab.Main, out root);
                        try
                        {
                            ((RectTransform)root.transform).sizeDelta = size;
                            var res = new RewardClaimOutcome { Result = RewardClaimResult.Ok, Gems = 40, Coins = 2000 };
                            res.PowerUpIds.Add("pw_flight"); res.PowerUpIds.Add("pw_shield");
                            res.HeroIds.Add("forestgirl"); res.HeroIds.Add("forestgirl");   // 6 chips, the tallest case
                            s.ShowClaimed(res);
                            RectTransform card = s.RewardCard;
                            float w = card.sizeDelta.x * card.localScale.x, h = card.sizeDelta.y * card.localScale.y;
                            string where = size + " text " + ts;
                            Assert.LessOrEqual(w, size.x - 48f + 0.5f, "width inside the safe area at " + where);
                            Assert.LessOrEqual(h, size.y - 48f + 0.5f, "height inside the safe area at " + where);
                            Assert.GreaterOrEqual(card.localScale.x, 1f, "never smaller than the old card at " + where);
                            // a small card (one chip) is exactly 50 percent bigger than the base size
                            s.ShowClaimed(new RewardClaimOutcome { Result = RewardClaimResult.Ok, Coins = 150 });
                            Assert.AreEqual(RewardsScreen.RewardCardScale, s.RewardCard.localScale.x, 0.001f, "one chip: exactly 1.5x at " + where);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(root); }
                    }
                }
            }
            finally { UiKit.TextScale = oldScale; }
        }

        [Test]
        public void BackArrowIsTopLeftWithTheSamePositionAndSizeAsEveryOtherScreen()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreenWithHost(svc, RewardTab.Main, out root);
            try
            {
                var back = (RectTransform)root.transform.Find("RewardsRoot/Back");
                Assert.IsNotNull(back, "Rewards has its Back arrow");
                Assert.AreEqual(new Vector2(0f, 1f), back.anchorMin, "top left like MetaController.AddBack");
                Assert.AreEqual(new Vector2(0f, 1f), back.anchorMax);
                Assert.AreEqual(new Vector2(20f, -20f), back.anchoredPosition, "20 / -20 like the other screens");
                Assert.AreEqual(new Vector2(150f, 150f), back.sizeDelta, "150 x 150 like the other screens");
                Assert.GreaterOrEqual(RewardsScreen.HeaderH, 20f + 150f, "the list starts below the Back arrow");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void TaskTitlesStayOnOneLineAtOneHundredAndOneHundredThirtyPercentText()
        {
            ProgressService svc = NewService();
            float oldScale = UiKit.TextScale;
            try
            {
                foreach (float ts in new[] { 1f, 1.3f })
                {
                    UiKit.TextScale = ts;
                    foreach (RewardTab tab in new[] { RewardTab.Daily, RewardTab.Weekly, RewardTab.Main })
                    {
                        GameObject root;
                        var r = new GameObject("RewardsTestRoot", typeof(RectTransform));
                        root = r;
                        ((RectTransform)r.transform).sizeDelta = new Vector2(1080f, 1920f);
                        var s = new RewardsScreen((RectTransform)r.transform, svc, repo, null, new RewardsScreen.Host { NewOverlay = () => (RectTransform)new GameObject("O", typeof(RectTransform), typeof(Image)).transform });
                        try
                        {
                            s.Build(tab);
                            if (tab == RewardTab.Main) s.SelectRegion(1);
                            LayoutRebuilder.ForceRebuildLayoutImmediate(s.ListContent);
                            int checkedTitles = 0;
                            foreach (Text t in s.ListContent.GetComponentsInChildren<Text>(true))
                            {
                                if (t.name != "Title") continue;
                                checkedTitles++;
                                Assert.LessOrEqual(t.preferredHeight, t.fontSize * 3.1f, "at most two lines: \"" + t.text + "\" (" + tab + ", text " + ts + ")");   // owner follow-up: two lines are allowed, the card has room for them
                                Assert.GreaterOrEqual(t.fontSize, Mathf.FloorToInt(UiKit.MinBodyTextPx), "never below the 16 sp floor: \"" + t.text + "\"");
                            }
                            Assert.Greater(checkedTitles, 0, "the " + tab + " tab lists task titles");
                        }
                        finally { UnityEngine.Object.DestroyImmediate(root); }
                    }
                }
            }
            finally { UiKit.TextScale = oldScale; }
        }

        [Test]
        public void CompactCardMetricsGrowWithTheTextSizeInsteadOfClipping()
        {
            float old = UiKit.TextScale;
            try
            {
                UiKit.TextScale = 1f;
                float h100 = RewardsScreen.CardHeight;
                Assert.AreEqual(10f, RewardsScreen.CardGap, "gap between cards");
                Assert.AreEqual(136f, RewardsScreen.CompactCardH);
                Assert.AreEqual(RewardsScreen.CardHeightFor(2f * RewardsScreen.LineH(44)), h100, 1.5f, "every card has room for a two-line title");
                Assert.GreaterOrEqual(h100, RewardsScreen.CompactCardH, "never below the compact base");
                Assert.GreaterOrEqual(h100, RewardsScreen.ClaimButtonH + 10f, "room for the 48 dp Claim button");
                Assert.Less(h100, 214f, "still tighter than the old 214 card");
                UiKit.TextScale = 1.3f;
                float h130 = RewardsScreen.CardHeight;
                Assert.GreaterOrEqual(h130, h100, "never shorter at 130 percent text");
                Assert.Greater(RewardsScreen.CardHeightFor(RewardsScreen.LineH(44) * 3f), RewardsScreen.CardHeightFor(RewardsScreen.LineH(44)), "a wrapped title grows the card");
                Assert.GreaterOrEqual(RewardsScreen.BottomReserve(), RewardsScreen.FooterH + 130f, "list ends above the tab row and the footer");
            }
            finally { UiKit.TextScale = old; }
        }

        [Test]
        public void TitleAndBarNeverOverlapAt130PercentText()
        {
            float old = UiKit.TextScale;
            try
            {
                foreach (float scale in new[] { 1f, 1.3f })
                {
                    UiKit.TextScale = scale;
                    foreach (RewardTab tab in new[] { RewardTab.Main, RewardTab.Daily, RewardTab.Weekly })
                    {
                        GameObject root;
                        RewardsScreen s = BuildScreen(NewService(), tab, out root);
                        try
                        {
                            int cards = 0;
                            foreach (Transform row in s.ListContent)
                            {
                                Transform card = row.Find("Card");
                                if (card == null) continue;
                                cards++;
                                var title = (RectTransform)card.Find("Title");
                                var bar = (RectTransform)card.Find("Bar");
                                float rowH = row.GetComponent<LayoutElement>().preferredHeight;
                                string who = scale + " " + tab + " " + row.name;
                                Assert.AreEqual(1f, title.anchorMin.y, who + ": title hangs from the top");
                                Assert.AreEqual(1f, bar.anchorMin.y, who + ": bar hangs from the top");
                                float titleBottom = title.offsetMin.y, barTop = bar.offsetMax.y;
                                Assert.LessOrEqual(barTop, titleBottom - RewardsScreen.TitleBarGap + 0.01f, who + ": title rect above the bar rect with a gap");
                                Assert.GreaterOrEqual(bar.offsetMin.y, -rowH, who + ": bar inside the card");
                                Assert.LessOrEqual(title.offsetMax.y, 0.01f, who + ": title inside the card");
                                if (scale == 1f) Assert.GreaterOrEqual(rowH, RewardsScreen.CardHeight - 1.5f, who + ": 100 percent card has room for two title lines");
                            }
                            Assert.Greater(cards, 0, tab + " has cards");
                        }
                        finally { UnityEngine.Object.DestroyImmediate(root); }
                    }
                }
            }
            finally { UiKit.TextScale = old; }
        }

        [Test]
        public void PointsTrackPositionRunsFromZeroThroughTheChestCentres()
        {
            var ms = RewardCatalog.BuiltIn().DailyMilestones;
            Assert.AreEqual(0f, RewardsScreen.TrackPosition(ms, 0), 1e-5f);
            Assert.AreEqual(0.5f / 5f, RewardsScreen.TrackPosition(ms, 40), 1e-5f, "40 points = the first chest centre");
            Assert.AreEqual(2.5f / 5f, RewardsScreen.TrackPosition(ms, 120), 1e-5f);
            Assert.AreEqual(4.5f / 5f, RewardsScreen.TrackPosition(ms, 200), 1e-5f);
            float prev = -1f;
            for (int p = 0; p <= 200; p += 5) { float v = RewardsScreen.TrackPosition(ms, p); Assert.GreaterOrEqual(v, prev); prev = v; }
        }

        [Test]
        public void EveryTaskHasAnIconAndTheChestKindsFollowTheThresholds()
        {
            RewardCatalog c = RewardCatalog.BuiltIn();
            var all = new System.Collections.Generic.List<RewardEntry>();
            foreach (RewardEntry e in c.DailyActions) all.Add(e);
            foreach (RewardEntry e in c.WeeklyTasks) all.Add(e);
            foreach (RegionGoal e in c.AllRegionGoals) all.Add(e);
            foreach (RewardEntry e in all)
            {
                string slot, icon, kit;
                RewardsScreen.TaskSources(e.Id, out slot, out icon, out kit);
                Assert.IsNotNull(ToyKit_Get(kit), e.Id + ": the UI kit fallback icon " + kit + " must exist so a missing slot still draws");
            }
            string[] kinds = { "bronze", "bronze", "gold", "gold", "purple" };
            for (int i = 0; i < 5; i++) Assert.AreEqual(kinds[i], RewardArt.ChestKind(i, 5), "chest " + i);
            Assert.IsNull(RewardArt.Slot("task_does_not_exist"), "a missing slot is null, never an error");
        }

        [Test]
        public void TaskIconsFollowTheTaskTypeForGoalsAndTheNewDailyAndWeeklyTasks()
        {
            string slot, icon, kit;
            Check("g_1_stars_1", "icon_star", out slot, out icon, out kit);
            Assert.AreEqual("flag1", Src("g_1_levels_2")); Assert.AreEqual("task_flag", Slot("g_1_levels_2"));
            Assert.AreEqual("star3", Src("g_1_stars3_1"), "3-star levels: three stars (icon_star3)");
            Assert.AreEqual("task_upgrade", Slot("g_1_jumps_1"), "jumps: the up arrow");
            Assert.AreEqual("task_flame", Slot("g_1_combos_1"));
            Assert.AreEqual("task_hourglass", Slot("g_1_time_1"));
            Assert.AreEqual("task_coins", Slot("g_1_coins_1"));
            Assert.AreEqual("bolt", Src("g_1_powerups_1"));
            Assert.AreEqual("task_boss_skull", Slot("g_1_hard_1"));
            Assert.AreEqual("wall", Src("g_1_nowall_1"));
            RewardsScreen.TaskSources("g_1_endless_1", out slot, out icon, out kit);
            Assert.AreEqual("icon_tower", kit, "endless floors: the tower");
            Assert.AreEqual("task_key", Slot("d_login"));
            Assert.AreEqual("task_hourglass", Slot("d_play10"));
            Assert.AreEqual("task_hourglass", Slot("d_play60"));
            Assert.AreEqual("task_coins", Slot("d_coins"));
            Assert.AreEqual("task_upgrade", Slot("w_jumps8000"));
            Assert.AreEqual("medal", Src("d_medal"));
            Assert.IsTrue(RewardsScreen.IsGoldMedalTask("w_gold10"));
            Assert.AreEqual("task_flag", Slot("d_level3"));
            Assert.AreEqual("task_flame", Slot("d_combo10"));
            Assert.AreEqual("task_boss_skull", Slot("d_hard"));
            Assert.AreEqual("wall", Src("w_nowall2"));
            Assert.AreEqual("bolt", Src("w_powerups10"));
            Assert.AreEqual("star1", Src("w_stars20"));
            RewardsScreen.TaskSources("w_endless100", out slot, out icon, out kit);
            Assert.AreEqual("icon_tower", kit);
            Assert.AreEqual("task_boss_skull", Slot("w_hard5"));
        }

        [Test]
        public void LongTitlesAreShortenedWhereTheIconSaysIt()
        {
            Assert.AreEqual("Reach floor 100", RewardsScreen.ShortTitle("Reach floor 100 in Endless"));
            Assert.AreEqual("No-wall level x2", RewardsScreen.ShortTitle("Clear the no-wall level 2 times"));
            Assert.AreEqual("Clear the no-wall level", RewardsScreen.ShortTitle("Clear the no-wall level"));
            Assert.AreEqual("3 stars on 15 levels", RewardsScreen.ShortTitle("Get 3 stars on 15 levels"));
            Assert.AreEqual("Earn 20 stars", RewardsScreen.ShortTitle("Earn 20 stars"));
            foreach (RegionGoal g in RewardCatalog.BuiltIn().AllRegionGoals) Assert.LessOrEqual(RewardsScreen.ShortTitle(g.Title).Length, 24, g.Id);
        }

        static void Check(string id, string expectKit, out string slot, out string icon, out string kit)
        {
            RewardsScreen.TaskSources(id, out slot, out icon, out kit);
            Assert.AreEqual(expectKit, kit, id);
        }

        static string Src(string id) { string s, i, k; RewardsScreen.TaskSources(id, out s, out i, out k); return i; }
        static string Slot(string id) { string s, i, k; RewardsScreen.TaskSources(id, out s, out i, out k); return s; }

        static Sprite ToyKit_Get(string name) { return FluffyClimbers.Gameplay.ToyKit.Get(name); }

        [Test]
        public void ResetAllClearsTheRewardCounters()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.ResetAll();
            Assert.AreEqual(0, Daily(svc, RewardStat.Login));
            Assert.AreEqual(0, svc.DailyPoints());
        }

        // ---- Bug List 10: hidden central play timer, one-line titles, Completed section, centred region names ----

        [Test]
        public void CentralPlayTimerAccumulatesOnlyFedRunTimeAndSurvivesTheSave()
        {
            ProgressService svc = NewService();
            Assert.AreEqual(0L, svc.PlayTotalSeconds);
            for (int i = 0; i < 12; i++) svc.FeedPlayTime(1f, false, 1);   // twelve live run seconds: two 5 second batches
            svc.FlushPlayTime();                                           // the two pending seconds are added too
            Assert.AreEqual(12L, svc.PlayTotalSeconds, "lifetime total");
            Assert.AreEqual(12, Daily(svc, RewardStat.PlaySeconds), "the daily counter grows with it");
            Assert.AreEqual(0, Weekly(svc, RewardStat.PlayMinutes), "12 s is under a minute");
            Assert.AreEqual(12L, new ProgressService(repo, store, 5) { Clock = () => now }.PlayTotalSeconds, "saved by the flush");
            svc.FlushPlayTime();
            Assert.AreEqual(12L, svc.PlayTotalSeconds, "a second flush adds nothing");
            Assert.AreEqual(0L, JsonConvert.DeserializeObject<SaveData>("{\"coins\":3}").Rewards.PlayTotalSeconds, "an old save starts at 0");
        }

        [Test]
        public void MenuTimeIsNeverFedIntoThePlayGoals()
        {
            ProgressService svc = NewService();
            // the meta scene no longer calls FeedPlayTime: only RunController does, while a run is live and not paused
            string meta = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "_Game/Gameplay/MetaController.cs"));
            Assert.IsFalse(meta.Contains("FeedPlayTime"), "menus, shop and idle time do not count");
            Assert.AreEqual(0, Daily(svc, RewardStat.PlaySeconds));
        }

        [Test]
        public void GoldMedalGoalTitleIsShortEnoughForOneLine()
        {
            Assert.AreEqual("Gold on 25 levels", RewardsScreen.ShortTitle("Gold medals on 25 levels"));
        }

        [Test]
        public void ClaimedMainGoalMovesToACollapsedCompletedSectionThatTogglesOpenAndClosed()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 5);
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                s.SelectRegion(1);
                Assert.IsNull(s.ListContent.Find("CompletedHeader"), "nothing completed yet: no section");
                s.Claim(RewardTab.Main, "g_1_levels_1");
                Assert.IsNull(s.ListContent.Find("Row_g_1_levels_1"), "collapsed by default");
                Transform header = s.ListContent.Find("CompletedHeader");
                Assert.IsNotNull(header);
                Assert.AreEqual("Completed (1)", header.Find("Toggle/Label").GetComponent<Text>().text);
                Assert.AreEqual(s.ListContent.childCount - 1, header.GetSiblingIndex(), "the section is last in the list");
                var hrt = (RectTransform)header;
                Assert.GreaterOrEqual(hrt.GetComponent<LayoutElement>().preferredHeight, UiKit.MinTouchPx - 1f, "header is at least 48 dp");
                header.Find("Toggle").GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(s.CompletedOpen);
                Transform small = s.ListContent.Find("Row_g_1_levels_1");
                Assert.IsNotNull(small, "expanded: the small card is listed");
                Assert.AreEqual(s.ListContent.childCount - 1, small.GetSiblingIndex(), "...after the header, at the bottom");
                s.ListContent.Find("CompletedHeader/Toggle").GetComponent<Button>().onClick.Invoke();
                Assert.IsFalse(s.CompletedOpen);
                Assert.IsNull(s.ListContent.Find("Row_g_1_levels_1"), "collapsed again");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void FinishedDailyActionMovesToCompletedAtOnceAndWeeklyClaimsMoveToo()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();   // d_login is finished
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Daily, out root);
            try
            {
                Assert.IsNull(s.ListContent.Find("Row_d_login"), "the finished action is not in the open list");
                Assert.AreEqual("Completed (1)", s.ListContent.Find("CompletedHeader/Toggle/Label").GetComponent<Text>().text);
                Assert.IsNotNull(s.ListContent.Find("Row_d_play10"), "open actions stay");
                s.SelectTab(RewardTab.Weekly);
                Assert.IsFalse(s.CompletedOpen, "a tab change collapses the section");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void RegionNamesAreCentredOnTheNameZoneMiddleLine()
        {
            ProgressService svc = NewService();
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                int n = 0;
                foreach (Text t in root.GetComponentsInChildren<Text>(true))
                {
                    if (t.name != "Name") continue;
                    n++;
                    Assert.AreEqual(TextAnchor.MiddleCenter, t.alignment, "name \"" + t.text + "\" is vertically centred");
                }
                Assert.Greater(n, 0);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void CompletedHeaderArrowStaysInsideTheHeaderInBothStatesOnEveryTab()
        {
            float oldScale = UiKit.TextScale;
            try
            {
                foreach (float ts in new[] { 1f, 1.3f })
                {
                    UiKit.TextScale = ts;
                    foreach (RewardTab tab in new[] { RewardTab.Main, RewardTab.Daily, RewardTab.Weekly })
                    {
                        ProgressService svc = NewService();
                        if (tab == RewardTab.Main) ClearLevels(svc, 1, 5);
                        else if (tab == RewardTab.Daily) svc.RecordLogin();
                        else svc.AddPlaySeconds(6000);
                        GameObject root;
                        root = new GameObject("RewardsTestRoot", typeof(RectTransform));
                        ((RectTransform)root.transform).sizeDelta = new Vector2(1080f, 1920f);   // a real screen size: a zero-size root would squeeze the header narrower than its own arrow
                        var s = new RewardsScreen((RectTransform)root.transform, svc, repo, null, null);
                        s.Build(tab);
                        try
                        {
                            if (tab == RewardTab.Main) { s.SelectRegion(1); s.Claim(RewardTab.Main, "g_1_levels_1"); }
                            else if (tab == RewardTab.Weekly) s.Claim(RewardTab.Weekly, "w_play100");
                            Vector2 firstCentre = Vector2.zero, firstSize = Vector2.zero;
                            for (int state = 0; state < 2; state++)
                            {
                                LayoutRebuilder.ForceRebuildLayoutImmediate(s.ListContent);
                                Transform header = s.ListContent.Find("CompletedHeader");
                                Assert.IsNotNull(header, tab + " has a Completed header");
                                var hrt = (RectTransform)header;
                                Assert.Greater(hrt.rect.width, 600f, "the header has a real width after layout");
                                var arrow = (RectTransform)header.Find("Toggle/ArrowSlot/Arrow");
                                var hc = new Vector3[4]; var ac = new Vector3[4];
                                hrt.GetWorldCorners(hc); arrow.GetWorldCorners(ac);
                                string who = tab + " text " + ts + " open=" + s.CompletedOpen;
                                for (int i = 0; i < 4; i++)
                                {
                                    Assert.GreaterOrEqual(ac[i].x, hc[0].x - 0.01f, who + ": arrow inside the header (left)");
                                    Assert.LessOrEqual(ac[i].x, hc[2].x + 0.01f, who + ": arrow inside the header (right)");
                                    Assert.GreaterOrEqual(ac[i].y, hc[0].y - 0.01f, who + ": arrow inside the header (bottom)");
                                    Assert.LessOrEqual(ac[i].y, hc[2].y + 0.01f, who + ": arrow inside the header (top)");
                                }
                                Vector2 centre = new Vector2((ac[0].x + ac[2].x) * 0.5f - hc[0].x, (ac[0].y + ac[2].y) * 0.5f - hc[0].y);
                                Vector2 size = new Vector2(Mathf.Abs(ac[2].x - ac[0].x), Mathf.Abs(ac[2].y - ac[0].y));   // a 180 degree turn swaps the corners: compare sizes as magnitudes
                                Assert.AreEqual(((hc[0].y + hc[2].y) - (ac[0].y + ac[2].y)) * 0.5f, 0f, 0.5f, who + ": vertically centred");
                                if (state == 0) { firstCentre = centre; firstSize = size; }
                                else
                                {
                                    Assert.AreEqual(firstCentre.x, centre.x, 0.01f, who + ": same place in both states");
                                    Assert.AreEqual(firstCentre.y, centre.y, 0.01f);
                                    Assert.AreEqual(firstSize.x, size.x, 0.01f);
                                }
                                header.Find("Toggle").GetComponent<Button>().onClick.Invoke();
                            }
                        }
                        finally { UnityEngine.Object.DestroyImmediate(root); }
                    }
                }
            }
            finally { UiKit.TextScale = oldScale; }
        }
    }
}
