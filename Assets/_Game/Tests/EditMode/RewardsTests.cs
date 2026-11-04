using System;
using System.IO;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace BoingUpSkyTower.Tests
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
            AssertSame(builtIn.StarMilestones, fromJson.StarMilestones);
            AssertSame(builtIn.RegionRewards, fromJson.RegionRewards);
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
            }
        }

        [Test]
        public void TablesFollowTheDocument()
        {
            RewardCatalog c = RewardCatalog.BuiltIn();
            int[] stars = { 25, 50, 75, 100, 125, 150, 175, 200, 225 };
            int[] gems = { 20, 0, 30, 0, 40, 0, 60, 80, 100 };
            int[] coins = { 0, 1000, 0, 1500, 0, 2000, 0, 0, 0 };
            int[] powerUps = { 0, 0, 0, 0, 1, 0, 2, 3, 5 };
            Assert.AreEqual(9, c.StarMilestones.Count);
            for (int i = 0; i < 9; i++)
            {
                Assert.AreEqual(stars[i], c.StarMilestones[i].Target);
                Assert.AreEqual(gems[i], c.StarMilestones[i].Gems);
                Assert.AreEqual(coins[i], c.StarMilestones[i].Coins);
                Assert.AreEqual(powerUps[i], c.StarMilestones[i].PowerUps);
            }
            Assert.AreEqual(5, c.RegionRewards.Count);
            foreach (RewardEntry r in c.RegionRewards) { Assert.AreEqual(50, r.Gems); Assert.AreEqual(2000, r.Coins); }
            Assert.AreEqual(10, c.DailyActions.Count);
            Assert.AreEqual(100, c.DailyMaxPoints);
            Assert.AreEqual(5, c.DailyMilestones.Count);
            Assert.AreEqual(11, c.WeeklyTasks.Count);
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
            svc.AddPlaySeconds(299);
            Assert.AreEqual(0, svc.DailyPoints());
            Assert.AreEqual(4, Weekly(svc, RewardStat.PlayMinutes));
            svc.AddPlaySeconds(1);
            Assert.AreEqual(10, svc.DailyPoints(), "play 5 minutes is worth 10 points");
            Assert.AreEqual(5, Weekly(svc, RewardStat.PlayMinutes));
            svc.AddPlaySeconds(0);
            svc.AddPlaySeconds(-5);
            Assert.AreEqual(300, Daily(svc, RewardStat.PlaySeconds));
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
            Assert.AreEqual(10, svc.DailyPoints(), "complete a level");
            ClearLevels(svc, 1, 3);   // a replay of level 1 (counted) and first clears of 2 and 3
            Assert.AreEqual(4, Daily(svc, RewardStat.Levels));
            Assert.AreEqual(20, svc.DailyPoints(), "complete 3 levels");
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
            Assert.AreEqual(1, Daily(svc, RewardStat.NoWall), "level 7 is the no-wall-touch level");
            Assert.AreEqual(1, Weekly(svc, RewardStat.NoWall));
            Assert.AreEqual(0, Daily(svc, RewardStat.GemLevel));

            svc.ApplyRun("lvl_001_08", new RunOutcome { Completed = true, StarMask = 7, GemsCollected = 5, HiddenGems = 2 });
            Assert.AreEqual(1, Daily(svc, RewardStat.GemLevel), "level 8 is a gem level");
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
        public void ReplaysOfClearedLevelsCountAtMostThreePerDayInTotal()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 7);   // 7 first clears
            Assert.AreEqual(7, Daily(svc, RewardStat.Levels));
            for (int i = 0; i < 5; i++) svc.ApplyRun("lvl_001_07", Won(7));   // replays of the no-wall level
            Assert.AreEqual(10, Daily(svc, RewardStat.Levels), "only 3 of the 5 replays count");
            Assert.AreEqual(4, Daily(svc, RewardStat.NoWall), "1 first clear + 3 counted replays");
            Assert.AreEqual(3, Daily(svc, RewardStat.Stars3), "3 counted replays with 3 stars");
            svc.ApplyRun("lvl_001_01", Won());   // a replay of another level: still over the daily limit
            Assert.AreEqual(10, Daily(svc, RewardStat.Levels));
            ClearLevels(svc, 1, 8);              // level 8 is a first clear: always counts
            Assert.AreEqual(11, Daily(svc, RewardStat.Levels));
            Assert.AreEqual(11, Weekly(svc, RewardStat.Levels));
            now = now.AddDays(1);                // next day: replays count again
            svc.ApplyRun("lvl_001_01", Won());
            Assert.AreEqual(1, Daily(svc, RewardStat.Levels));
        }

        [Test]
        public void Level15CountsAsAGemLevelToo()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 15);
            Assert.AreEqual(2, Daily(svc, RewardStat.GemLevel), "levels 8 and 15");
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
            Assert.AreEqual(20, svc.DailyPoints(), "a combo of 3 or more and a power-up used");
            svc.ApplyRun("lvl_001_01", new RunOutcome { Completed = true, StarMask = 1, ComboChains = 3, BestCombo = 2, PowerUpsUsed = 2 });
            Assert.AreEqual(5, Daily(svc, RewardStat.Combos));
            Assert.AreEqual(4, Daily(svc, RewardStat.BestCombo), "the best stays the best");
            Assert.AreEqual(3, Daily(svc, RewardStat.PowerUps));
            Assert.AreEqual(5, Weekly(svc, RewardStat.Combos));
            Assert.IsTrue(svc.IsRewardReady(RewardTab.Weekly, svc.RewardDefs.Find(RewardTab.Weekly, "w_combos5")));
        }

        [Test]
        public void HardLevelsCountFromDifficultyThreeUp()
        {
            ProgressService svc = NewService();
            svc.RecordLevelStarted(0);
            svc.RecordLevelStarted(2);
            Assert.AreEqual(0, Daily(svc, RewardStat.Hard));
            svc.RecordLevelStarted(3);
            Assert.AreEqual(1, Daily(svc, RewardStat.Hard));
            svc.RecordLevelStarted(4);
            Assert.AreEqual(1, Daily(svc, RewardStat.Hard), "counted once per day (no save at every retry)");
            Assert.AreEqual(10, svc.DailyPoints());
        }

        [Test]
        public void AllTenDailyActionsGiveOneHundredPoints()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.AddPlaySeconds(300);
            svc.RecordLevelStarted(3);
            svc.RecordRunStats(new RunOutcome { ComboChains = 1, BestCombo = 3, PowerUpsUsed = 1 });
            ClearLevels(svc, 1, 7);
            svc.ApplyRun("lvl_001_08", new RunOutcome { Completed = true, StarMask = 3 });   // 2 stars: the "earn 2 stars in a level" action (always possible, also on cleared levels)
            Assert.AreEqual(100, svc.DailyPoints());

            int gems = svc.Gems, coins = svc.Coins;
            int[] points = { 20, 40, 60, 80, 100 };
            foreach (int p in points) Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Daily, "dp_" + p).Result, "dp_" + p);
            Assert.AreEqual(10 + 15 + 20, svc.Gems - gems);
            Assert.AreEqual(500 + 1000, svc.Coins - coins);
            Assert.AreEqual(1, TotalPowerUps(svc), "100 points pay one random power-up");
            Assert.IsFalse(svc.HasClaimable(RewardTab.Daily));
        }

        // ---- resets with the clock ----

        [Test]
        public void DailyCountersAndClaimsResetAtLocalMidnightButTheWeekKeepsGoing()
        {
            now = new DateTime(2026, 11, 3, 23, 59, 0);   // Tuesday
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.AddPlaySeconds(300);
            Assert.AreEqual(20, svc.DailyPoints());
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Daily, "dp_20").Result);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Daily, "dp_20").Result);

            now = new DateTime(2026, 11, 4, 0, 0, 30);   // Wednesday, 30 seconds after midnight
            Assert.AreEqual(0, svc.DailyPoints());
            Assert.IsFalse(svc.IsRewardClaimed(RewardTab.Daily, svc.RewardDefs.Find(RewardTab.Daily, "dp_20")));
            Assert.IsFalse(svc.HasClaimable());
            Assert.AreEqual(1, Weekly(svc, RewardStat.LoginDays), "the week still has Tuesday's login");
            Assert.AreEqual(5, Weekly(svc, RewardStat.PlayMinutes));
            svc.RecordLogin();
            svc.AddPlaySeconds(300);
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Daily, "dp_20").Result, "claimable again on the new day");
            Assert.AreEqual(2, Weekly(svc, RewardStat.LoginDays));
            Assert.AreEqual(10, Weekly(svc, RewardStat.PlayMinutes));
        }

        [Test]
        public void WeeklyCountersAndClaimsResetOnMondayLocalTime()
        {
            ProgressService svc = NewService();
            for (int d = 0; d < 7; d++) { now = Monday.AddDays(d); svc.RecordLogin(); }   // Monday to Sunday
            Assert.AreEqual(7, Weekly(svc, RewardStat.LoginDays));
            int gems = svc.Gems;
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w_login7").Result);
            Assert.AreEqual(30, svc.Gems - gems);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Weekly, "w_login7").Result);

            now = new DateTime(2026, 11, 8, 23, 59, 59);   // still Sunday
            Assert.AreEqual(7, Weekly(svc, RewardStat.LoginDays));
            now = new DateTime(2026, 11, 9, 0, 0, 1);      // Monday: a new week
            Assert.AreEqual(0, Weekly(svc, RewardStat.LoginDays));
            Assert.IsFalse(svc.IsRewardClaimed(RewardTab.Weekly, svc.RewardDefs.Find(RewardTab.Weekly, "w_login7")));
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Weekly, "w_login7").Result);
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
            svc.RecordLogin();
            svc.AddPlaySeconds(300);
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Daily, "dp_20").Result);
            now = Monday;   // back to Monday
            Assert.AreEqual(20, svc.DailyPoints(), "no reset when the date goes back");
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Daily, "dp_20").Result);
        }

        // ---- claims ----

        [Test]
        public void ClaimNeedsTheTargetPaysOnceAndRejectsUnknownIds()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();   // 10 points
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Daily, "dp_20").Result);
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimReward(RewardTab.Daily, "nope").Result);
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimReward(RewardTab.Weekly, "dp_20").Result, "ids are per tab");
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimReward(RewardTab.Daily, null).Result);
            svc.AddPlaySeconds(300);
            int gems = svc.Gems;
            RewardClaimOutcome ok = svc.TryClaimReward(RewardTab.Daily, "dp_20");
            Assert.AreEqual(RewardClaimResult.Ok, ok.Result);
            Assert.AreEqual(10, ok.Gems);
            Assert.AreEqual(gems + 10, svc.Gems);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Daily, "dp_20").Result);
            Assert.AreEqual(gems + 10, svc.Gems, "a second claim pays nothing");
        }

        [Test]
        public void FailedClaimsChangeNothingInTheSave()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            string before = JsonConvert.SerializeObject(svc.Data);
            svc.TryClaimReward(RewardTab.Daily, "dp_20");   // not ready
            svc.TryClaimReward(RewardTab.Main, "s_25");      // not ready
            svc.TryClaimReward(RewardTab.Weekly, "bogus");
            Assert.AreEqual(before, JsonConvert.SerializeObject(svc.Data));
        }

        [Test]
        public void AClaimIsOneSaveWithWalletStockAndMarkTogether()
        {
            ProgressService svc = NewService();
            svc.RecordRunStats(new RunOutcome { ComboChains = 50 });
            int saves = 0;
            svc.Changed += () => saves++;
            int units = TotalPowerUps(svc);
            RewardClaimOutcome res = svc.TryClaimReward(RewardTab.Weekly, "w_combos50");
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(1, saves, "one save per claim");
            Assert.AreEqual(1, res.PowerUpIds.Count);
            Assert.AreEqual(units + 1, TotalPowerUps(svc));
            Assert.AreEqual(1, svc.PowerUpCount(res.PowerUpIds[0]));
            var reloaded = new ProgressService(repo, store, 5) { Clock = () => now };
            Assert.IsTrue(reloaded.IsRewardClaimed(RewardTab.Weekly, reloaded.RewardDefs.Find(RewardTab.Weekly, "w_combos50")));
            Assert.AreEqual(1, TotalPowerUps(reloaded), "the stock and the mark reached the same save");
        }

        [Test]
        public void ClaimsSaturateInsteadOfOverflowing()
        {
            ProgressService svc = NewService();
            svc.AddGems(int.MaxValue - 5);
            svc.RecordLogin();
            svc.AddPlaySeconds(300);
            svc.TryClaimReward(RewardTab.Daily, "dp_20");
            Assert.AreEqual(int.MaxValue, svc.Gems);
        }

        [Test]
        public void WeeklyTasksPayTheirRewards()
        {
            ProgressService svc = NewService();
            svc.AddPlaySeconds(100 * 60);
            svc.RecordRunStats(new RunOutcome { ComboChains = 10 });
            svc.RecordLogin();
            int coins = svc.Coins, gems = svc.Gems;
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w_play100").Result);
            Assert.AreEqual(500, svc.Coins - coins);
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Weekly, "w_play150").Result);
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w_combos5").Result);
            Assert.AreEqual(300, svc.Coins - coins - 500);
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w_combos10").Result);
            Assert.AreEqual(10, svc.Gems - gems);
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Weekly, "w_combos25").Result);
        }

        // ---- Main: stars and regions ----

        [Test]
        public void StarMilestonesAndRegionRewardsAreClaimedOnce()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 15, 7);   // 45 stars, region 1 cleared
            Assert.AreEqual(45, svc.TotalStars());
            Assert.AreEqual(15, svc.RegionLevelsCleared(1));
            RewardCatalog defs = svc.RewardDefs;
            Assert.IsTrue(svc.IsRewardReady(RewardTab.Main, defs.Find(RewardTab.Main, "s_25")));
            Assert.IsFalse(svc.IsRewardReady(RewardTab.Main, defs.Find(RewardTab.Main, "s_50")));
            Assert.IsTrue(svc.IsRewardReady(RewardTab.Main, defs.Find(RewardTab.Main, "r_1")));
            Assert.IsFalse(svc.IsRewardReady(RewardTab.Main, defs.Find(RewardTab.Main, "r_2")));
            Assert.IsTrue(svc.HasClaimable());

            int gems = svc.Gems, coins = svc.Coins;
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Main, "s_25").Result);
            Assert.AreEqual(20, svc.Gems - gems);
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Main, "r_1").Result);
            Assert.AreEqual(70, svc.Gems - gems);
            Assert.AreEqual(2000, svc.Coins - coins);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Main, "s_25").Result);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Main, "r_1").Result);
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Main, "s_50").Result);
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Main, "r_2").Result);
            Assert.IsFalse(svc.HasClaimable(RewardTab.Main));
        }

        [Test]
        public void MainClaimsNeverResetWithTheClock()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 15, 7);
            svc.TryClaimReward(RewardTab.Main, "s_25");
            now = Monday.AddDays(30);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Main, "s_25").Result);
        }

        [Test]
        public void AllFiveRegionsAndNineStarMilestonesPayTheDocumentedTotals()
        {
            ProgressService svc = NewService();
            for (int r = 1; r <= 5; r++) ClearLevels(svc, r, 15, 7);
            Assert.AreEqual(225, svc.TotalStars());
            int gems = svc.Gems, coins = svc.Coins, units = TotalPowerUps(svc);
            foreach (RewardEntry e in svc.ClaimableEntries(RewardTab.Main))
                Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Main, e.Id).Result, e.Id);
            Assert.AreEqual(20 + 30 + 40 + 60 + 80 + 100 + 5 * 50, svc.Gems - gems);
            Assert.AreEqual(1000 + 1500 + 2000 + 5 * 2000, svc.Coins - coins);
            Assert.AreEqual(1 + 2 + 3 + 5, TotalPowerUps(svc) - units);
            Assert.IsFalse(svc.HasClaimable(RewardTab.Main));
        }

        [Test]
        public void ClaimAllPaysEverythingReadyInOneSave()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 15, 7);   // 45 stars, region 1 cleared: s_25 and r_1 ready
            int gems = svc.Gems, coins = svc.Coins, saves = 0;
            svc.Changed += () => saves++;
            RewardClaimOutcome res = svc.ClaimAll(RewardTab.Main);
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(1, saves, "one save for everything");
            Assert.AreEqual(70, res.Gems);
            Assert.AreEqual(2000, res.Coins);
            Assert.AreEqual(gems + 70, svc.Gems);
            Assert.AreEqual(coins + 2000, svc.Coins);
            Assert.IsFalse(svc.HasClaimable(RewardTab.Main));
            Assert.AreEqual(RewardClaimResult.NotReady, svc.ClaimAll(RewardTab.Main).Result, "nothing left");
            Assert.AreEqual(1, saves, "an empty claim saves nothing");
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Main, "r_1").Result);
        }

        [Test]
        public void ClaimAllOnDailyAndWeeklyKeepsTabsApart()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.AddPlaySeconds(300);   // 20 points
            Assert.AreEqual(RewardClaimResult.NotReady, svc.ClaimAll(RewardTab.Weekly).Result);
            RewardClaimOutcome res = svc.ClaimAll(RewardTab.Daily);
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(10, res.Gems);
            Assert.IsTrue(svc.IsRewardClaimed(RewardTab.Daily, svc.RewardDefs.Find(RewardTab.Daily, "dp_20")));
        }

        // ---- red dot, saving ----

        [Test]
        public void HasClaimableFollowsProgressAndClaims()
        {
            ProgressService svc = NewService();
            Assert.IsFalse(svc.HasClaimable());
            svc.RecordLogin();
            svc.AddPlaySeconds(300);
            Assert.IsTrue(svc.HasClaimable(RewardTab.Daily));
            Assert.IsTrue(svc.HasClaimable());
            Assert.IsFalse(svc.HasClaimable(RewardTab.Weekly));
            svc.TryClaimReward(RewardTab.Daily, "dp_20");
            Assert.IsFalse(svc.HasClaimable());
        }

        [Test]
        public void CountersAndClaimsSurviveASaveRoundTrip()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.AddPlaySeconds(300);
            svc.RecordRunStats(new RunOutcome { ComboChains = 4, BestCombo = 6 });
            ClearLevels(svc, 1, 9, 7);   // 27 stars
            svc.TryClaimReward(RewardTab.Daily, "dp_20");
            svc.TryClaimReward(RewardTab.Main, "s_25");
            svc.FlushPlayTime();

            var again = new ProgressService(repo, store, 5) { Clock = () => now };
            Assert.AreEqual(1, Daily(again, RewardStat.Login));
            Assert.AreEqual(300, Daily(again, RewardStat.PlaySeconds));
            Assert.AreEqual(4, Daily(again, RewardStat.Combos));
            Assert.AreEqual(9, Daily(again, RewardStat.Levels));
            Assert.AreEqual(6, again.BestComboEver);
            Assert.AreEqual(1, Weekly(again, RewardStat.LoginDays));
            Assert.IsTrue(again.IsRewardClaimed(RewardTab.Daily, again.RewardDefs.Find(RewardTab.Daily, "dp_20")));
            Assert.IsTrue(again.IsRewardClaimed(RewardTab.Main, again.RewardDefs.Find(RewardTab.Main, "s_25")));
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
            Assert.AreEqual(10, svc.DailyPoints());
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
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                Assert.AreEqual(1 + 9, s.ListContent.childCount, "region header + 9 star cards");
                foreach (RewardEntry e in svc.RewardDefs.StarMilestones) Assert.IsNotNull(s.ListContent.Find("Row_" + e.Id + "/Card"), e.Id);
                Assert.IsNotNull(root.transform.Find("RewardsRoot/Body/Tabs"));
                s.SelectTab(RewardTab.Daily);
                Assert.AreEqual(1 + 10, s.ListContent.childCount, "scene header + 10 actions");
                Assert.IsNotNull(s.ListContent.Find("Scene/TrackPanel/Cells/Cell20"), "the first chest of the points track");
                s.SelectTab(RewardTab.Weekly);
                Assert.AreEqual(1 + 11, s.ListContent.childCount, "scene header + 11 tasks");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReadyCardsAreButtonsWithAClaimTagAndClaimAllFollowsTheTab()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 15, 7);   // s_25 and r_1 ready
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Main, out root);
            try
            {
                Transform ready = s.ListContent.Find("Row_s_25/Card");
                Assert.IsNotNull(ready.GetComponent<UnityEngine.UI.Button>(), "a ready card is the claim button");
                Assert.IsNotNull(ready.Find("Tag"), "with the green CLAIM tag");
                Transform open = s.ListContent.Find("Row_s_50/Card");
                Assert.IsNull(open.GetComponent<UnityEngine.UI.Button>());
                Assert.IsNull(open.Find("Tag"));
                Assert.IsNotNull(root.transform.Find("RewardsRoot/Body/ClaimAll"), "Claim All while something is ready");
                s.SelectRegion(1);   // clearing region 1 moves the current region on; the ready bonus belongs to region 1
                Assert.IsNotNull(s.ListContent.Find("MainHeader/Band").GetComponent<UnityEngine.UI.Button>(), "the region band is claimable too");

                svc.ClaimAll(RewardTab.Main);
                s.SelectTab(RewardTab.Main);
                Assert.IsNull(s.ListContent.Find("Row_s_25/Card").GetComponent<UnityEngine.UI.Button>());
                Assert.IsNull(root.transform.Find("RewardsRoot/Body/ClaimAll"), "nothing left to claim");
                // the same 15 cleared levels (45 stars, the no-wall level) also complete weekly tasks, so Claim All follows the Weekly tab on its own
                s.SelectTab(RewardTab.Weekly);
                Assert.IsNotNull(root.transform.Find("RewardsRoot/Body/ClaimAll"), "weekly tasks are ready too (levels, stars, no-wall)");
                svc.ClaimAll(RewardTab.Weekly);
                s.SelectTab(RewardTab.Weekly);
                Assert.IsNull(root.transform.Find("RewardsRoot/Body/ClaimAll"), "nothing left to claim in Weekly");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void DailyChestIsAButtonOnlyWhenReached()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.AddPlaySeconds(300);   // 20 points: the first chest only
            GameObject root;
            RewardsScreen s = BuildScreen(svc, RewardTab.Daily, out root);
            try
            {
                Assert.IsNotNull(s.ListContent.Find("Scene/TrackPanel/Cells/Cell20").GetComponent<UnityEngine.UI.Button>());
                Assert.IsNull(s.ListContent.Find("Scene/TrackPanel/Cells/Cell40").GetComponent<UnityEngine.UI.Button>());
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void CardMetricsGrowWithTheTextSizeAndLeaveRoomForTheClaimTag()
        {
            float old = UiKit.TextScale;
            try
            {
                UiKit.TextScale = 1f;
                float h100 = RewardsScreen.CardHeight, gap100 = RewardsScreen.CardGap;
                UiKit.TextScale = 1.3f;
                float h130 = RewardsScreen.CardHeight, gap130 = RewardsScreen.CardGap;
                Assert.GreaterOrEqual(h100, 214f);
                Assert.Greater(h130, h100, "taller cards at 130 percent text");
                Assert.Greater(gap130, gap100);
                Assert.GreaterOrEqual(gap100, 8f + 20f, "the CLAIM tag sticks out above its card and stays clear of the card above");
                Assert.GreaterOrEqual(RewardsScreen.BottomReserve(), RewardsScreen.FooterH + 130f, "list ends above the tab row and the footer");
            }
            finally { UiKit.TextScale = old; }
        }

        [Test]
        public void PointsTrackPositionRunsFromZeroThroughTheChestCentres()
        {
            var ms = RewardCatalog.BuiltIn().DailyMilestones;
            Assert.AreEqual(0f, RewardsScreen.TrackPosition(ms, 0), 1e-5f);
            Assert.AreEqual(0.5f / 5f, RewardsScreen.TrackPosition(ms, 20), 1e-5f, "20 points = the first chest centre");
            Assert.AreEqual(2.5f / 5f, RewardsScreen.TrackPosition(ms, 60), 1e-5f);
            Assert.AreEqual(4.5f / 5f, RewardsScreen.TrackPosition(ms, 100), 1e-5f);
            float prev = -1f;
            for (int p = 0; p <= 100; p += 5) { float v = RewardsScreen.TrackPosition(ms, p); Assert.GreaterOrEqual(v, prev); prev = v; }
        }

        [Test]
        public void EveryTaskHasAnIconAndTheChestKindsFollowTheThresholds()
        {
            RewardCatalog c = RewardCatalog.BuiltIn();
            var all = new System.Collections.Generic.List<RewardEntry>();
            foreach (RewardEntry e in c.DailyActions) all.Add(e);
            foreach (RewardEntry e in c.WeeklyTasks) all.Add(e);
            foreach (RewardEntry e in c.StarMilestones) all.Add(e);
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

        static Sprite ToyKit_Get(string name) { return BoingUpSkyTower.Gameplay.ToyKit.Get(name); }

        [Test]
        public void ResetAllClearsTheRewardCounters()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.ResetAll();
            Assert.AreEqual(0, Daily(svc, RewardStat.Login));
            Assert.AreEqual(0, svc.DailyPoints());
        }
    }
}
