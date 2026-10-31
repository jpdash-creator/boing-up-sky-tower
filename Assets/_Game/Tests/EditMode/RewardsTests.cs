using System;
using System.IO;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
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
        public void NoWallGemLevelAllGemsGemsAndThreeStarsComeFromTheRunResult()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 6);
            Assert.AreEqual(0, Daily(svc, RewardStat.NoWall));
            svc.ApplyRun("lvl_001_07", Won());
            Assert.AreEqual(1, Daily(svc, RewardStat.NoWall), "level 7 is the no-wall-touch level");
            Assert.AreEqual(1, Weekly(svc, RewardStat.NoWall));
            Assert.AreEqual(0, Daily(svc, RewardStat.GemLevel));

            svc.ApplyRun("lvl_001_08", new RunOutcome { Completed = true, StarMask = 7, GemsCollected = 5, HiddenGems = 2, GemPickupsTotal = 3, GemPickupsTaken = 3 });
            Assert.AreEqual(1, Daily(svc, RewardStat.GemLevel), "level 8 is a gem level");
            Assert.AreEqual(1, Daily(svc, RewardStat.AllGems));
            Assert.AreEqual(7, Daily(svc, RewardStat.Gems), "gems and the hidden gem collected in levels");
            Assert.AreEqual(1, Daily(svc, RewardStat.Stars3));

            svc.ApplyRun("lvl_001_09", new RunOutcome { Completed = true, StarMask = 3, GemsCollected = 1, GemPickupsTotal = 3, GemPickupsTaken = 2 });
            Assert.AreEqual(1, Daily(svc, RewardStat.AllGems), "2 of 3 gem pickups is not all of them");
            Assert.AreEqual(1, Daily(svc, RewardStat.Stars3), "2 stars is not a 3-star clear");
            svc.ApplyRun("lvl_001_10", new RunOutcome { Completed = true, StarMask = 1, GemPickupsTotal = 0, GemPickupsTaken = 0 });
            Assert.AreEqual(1, Daily(svc, RewardStat.AllGems), "a level without gem pickups never counts as all gems");
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
            svc.ApplyRun("lvl_001_08", new RunOutcome { Completed = true, StarMask = 1, GemsCollected = 2, GemPickupsTotal = 2, GemPickupsTaken = 2 });
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
