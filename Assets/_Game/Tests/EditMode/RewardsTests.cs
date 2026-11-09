using System;
using System.IO;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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
            }
        }

        [Test]
        public void TablesFollowTheDocument()
        {
            RewardCatalog c = RewardCatalog.BuiltIn();
            Assert.AreEqual(6, c.DailyActions.Count, "Daily: log in, play 5, play 15, complete 3 levels, 5 combos, clear a hard level");
            Assert.AreEqual(100, c.DailyMaxPoints);
            Assert.AreEqual(5, c.DailyMilestones.Count);
            int[] thresholds = { 20, 40, 60, 80, 100 };
            for (int i = 0; i < 5; i++) Assert.AreEqual(thresholds[i], c.DailyMilestones[i].Target);
            Assert.AreEqual(9, c.WeeklyTasks.Count, "Weekly: login 5 days, play 100 min, 150 combos, 5 hard, 2 no-wall, 10 power-ups, 20 stars, 20 levels, endless floor 100");
            Assert.AreEqual(RewardEntry.RandomOpenRegionHero, c.WeeklyTasks[8].HeroRegion, "the top weekly task gives a random Common hero of an open region");
            for (int i = 0; i < 8; i++) Assert.AreEqual(0, c.WeeklyTasks[i].HeroRegion, c.WeeklyTasks[i].Id);
            int dayGems = 0, dayCoins = 0, weekGems = 0;
            foreach (RewardEntry m in c.DailyMilestones) { dayGems += m.Gems; dayCoins += m.Coins; }
            foreach (RewardEntry w in c.WeeklyTasks) weekGems += w.Gems;
            Assert.AreEqual(10, dayGems, "a full day pays 10 gems");
            Assert.AreEqual(1500, dayCoins);
            Assert.AreEqual(72, weekGems, "a full week of tasks pays 72 gems");
            Assert.LessOrEqual(7 * dayGems + weekGems, 150, "perfect Daily + Weekly stays under the tight gem budget (docs/39)");
            Assert.AreEqual(0, c.StarMilestones.Count, "the 25..225 star cards are gone");
            Assert.AreEqual(0, c.RegionRewards.Count, "the five region rewards are gone (clear all levels is the last Levels goal)");
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
            Assert.AreEqual(15, svc.DailyPoints(), "play 5 minutes is worth 15 points");
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
            Assert.AreEqual(0, svc.DailyPoints(), "one level is not a daily action any more");
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
            Assert.AreEqual(0, Daily(svc, RewardStat.NoWall), "level 7 is a plain level (the no-wall-touch level is level 10 since Bug List 6 item 4)");
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
        public void ReplaysOfClearedLevelsCountAtMostThreePerDayInTotal()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 10);   // 10 first clears
            Assert.AreEqual(10, Daily(svc, RewardStat.Levels));
            for (int i = 0; i < 5; i++) svc.ApplyRun("lvl_001_10", Won(7));   // replays of the no-wall level
            Assert.AreEqual(13, Daily(svc, RewardStat.Levels), "only 3 of the 5 replays count");
            Assert.AreEqual(4, Daily(svc, RewardStat.NoWall), "1 first clear + 3 counted replays");
            Assert.AreEqual(3, Daily(svc, RewardStat.Stars3), "3 counted replays with 3 stars");
            svc.ApplyRun("lvl_001_01", Won());   // a replay of another level: still over the daily limit
            Assert.AreEqual(13, Daily(svc, RewardStat.Levels));
            ClearLevels(svc, 1, 11);             // level 11 is a first clear: always counts
            Assert.AreEqual(14, Daily(svc, RewardStat.Levels));
            Assert.AreEqual(14, Weekly(svc, RewardStat.Levels));
            now = now.AddDays(1);                // next day: replays count again
            svc.ApplyRun("lvl_001_01", Won());
            Assert.AreEqual(1, Daily(svc, RewardStat.Levels));
        }

        [Test]
        public void TheLastLevelCountsAsAGemLevelToo()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 25);
            Assert.AreEqual(2, Daily(svc, RewardStat.GemLevel), "levels 8 and 25");
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
            Assert.AreEqual(0, svc.DailyPoints(), "2 combos are not yet the daily 5");
            svc.ApplyRun("lvl_001_01", new RunOutcome { Completed = true, StarMask = 1, ComboChains = 3, BestCombo = 2, PowerUpsUsed = 2 });
            Assert.AreEqual(5, Daily(svc, RewardStat.Combos));
            Assert.AreEqual(4, Daily(svc, RewardStat.BestCombo), "the best stays the best");
            Assert.AreEqual(3, Daily(svc, RewardStat.PowerUps));
            Assert.AreEqual(5, Weekly(svc, RewardStat.Combos));
            Assert.AreEqual(15, svc.DailyPoints(), "5 combos is worth 15 points");
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
            Assert.AreEqual(20, svc.DailyPoints(), "clear a hard level");
            svc.RecordLevelStarted(4);
            svc.ApplyRun("lvl_001_03", new RunOutcome { Completed = false });
            Assert.AreEqual(1, Daily(svc, RewardStat.HardClear), "a failed run is not a clear");
            Assert.AreEqual(1, svc.RegionStatsOf(1).HardClears);
        }

        [Test]
        public void AllSixDailyActionsGiveOneHundredPoints()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.AddPlaySeconds(900);
            svc.RecordLevelStarted(3);
            svc.RecordRunStats(new RunOutcome { ComboChains = 5, BestCombo = 3, PowerUpsUsed = 1, Region = 1 });
            ClearLevels(svc, 1, 3);   // the hard setting is still active: these clears are hard clears too
            Assert.AreEqual(100, svc.DailyPoints());

            int gems = svc.Gems, coins = svc.Coins;
            int[] points = { 20, 40, 60, 80, 100 };
            foreach (int p in points) Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Daily, "dp_" + p).Result, "dp_" + p);
            Assert.AreEqual(3 + 3 + 4, svc.Gems - gems);
            Assert.AreEqual(300 + 500 + 700, svc.Coins - coins);
            Assert.AreEqual(0, TotalPowerUps(svc), "the daily track pays no power-up any more (docs/39)");
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
            Assert.AreEqual(25, svc.DailyPoints());
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
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w_login5").Result);
            Assert.AreEqual(8, svc.Gems - gems);
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
            svc.RecordLogin();
            svc.AddPlaySeconds(300);
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Daily, "dp_20").Result);
            now = Monday;   // back to Monday
            Assert.AreEqual(25, svc.DailyPoints(), "no reset when the date goes back");
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
            int coins = svc.Coins;
            RewardClaimOutcome ok = svc.TryClaimReward(RewardTab.Daily, "dp_20");
            Assert.AreEqual(RewardClaimResult.Ok, ok.Result);
            Assert.AreEqual(300, ok.Coins);
            Assert.AreEqual(coins + 300, svc.Coins);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Daily, "dp_20").Result);
            Assert.AreEqual(coins + 300, svc.Coins, "a second claim pays nothing");
        }

        [Test]
        public void FailedClaimsChangeNothingInTheSave()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            string before = JsonConvert.SerializeObject(svc.Data);
            svc.TryClaimReward(RewardTab.Daily, "dp_20");   // not ready
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
            svc.RecordLogin();
            svc.AddPlaySeconds(900);   // 10 + 15 + 20 = 45 points: dp_40 pays 3 gems
            svc.TryClaimReward(RewardTab.Daily, "dp_40");
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
            Assert.AreEqual(500, svc.Coins - coins);
            Assert.AreEqual(8, svc.Gems - gems);
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimReward(RewardTab.Weekly, "w_login5").Result, "one login day is not five");
            int units = TotalPowerUps(svc);
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimReward(RewardTab.Weekly, "w_combos150").Result);
            Assert.AreEqual(500 + 800, svc.Coins - coins);
            Assert.AreEqual(8 + 10, svc.Gems - gems);
            Assert.AreEqual(1, TotalPowerUps(svc) - units);
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

        [Test]
        public void TheTopWeeklyTaskGivesExactlyOneCommonHeroCopyOfAnOpenRegion()
        {
            ProgressService svc = NewService();
            svc.RecordLevelStarted(3);
            ClearLevels(svc, 1, 5);   // five hard clears
            Assert.AreEqual(5, Weekly(svc, RewardStat.HardClear));
            var region1 = new[] { "momo" };
            int before = 0;
            foreach (string id in region1) before += svc.Copies(id);
            RewardClaimOutcome res = svc.TryClaimReward(RewardTab.Weekly, "w_hard5");
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(1, res.HeroIds.Count);
            CollectionAssert.Contains(region1, res.HeroIds[0], "only region 1 is open: Momo, its one ACTIVE Common hero");
            Assert.AreEqual(before + 1, TotalOf(svc, region1));
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimReward(RewardTab.Weekly, "w_hard5").Result);
            Assert.AreEqual(before + 1, TotalOf(svc, region1), "no second copy");
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
            for (int run = 0; run < 2; run++)
            {
                var st = new MemorySaveStore();
                var seeded = new SaveData();
                seeded.Bundles.Seed = 12345;
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
            svc.RecordLogin();
            svc.AddPlaySeconds(300);   // 25 points
            Assert.AreEqual(RewardClaimResult.NotReady, svc.ClaimAll(RewardTab.Weekly).Result);
            RewardClaimOutcome res = svc.ClaimAll(RewardTab.Daily);
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(300, res.Coins);
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
            ClearLevels(svc, 1, 10, 7);   // 30 stars (g_1_stars_2)
            svc.TryClaimReward(RewardTab.Daily, "dp_20");
            svc.TryClaimReward(RewardTab.Main, "g_1_stars_2");
            svc.FlushPlayTime();

            var again = new ProgressService(repo, store, 5) { Clock = () => now };
            Assert.AreEqual(1, Daily(again, RewardStat.Login));
            Assert.AreEqual(300, Daily(again, RewardStat.PlaySeconds));
            Assert.AreEqual(4, Daily(again, RewardStat.Combos));
            Assert.AreEqual(10, Daily(again, RewardStat.Levels));
            Assert.AreEqual(6, again.BestComboEver);
            Assert.AreEqual(1, Weekly(again, RewardStat.LoginDays));
            Assert.IsTrue(again.IsRewardClaimed(RewardTab.Daily, again.RewardDefs.Find(RewardTab.Daily, "dp_20")));
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
            Assert.AreEqual(10, svc.DailyPoints());
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
                Assert.AreEqual(1 + 6, s.ListContent.childCount, "scene header + 6 actions");
                Assert.IsNotNull(s.ListContent.Find("Scene/TrackPanel/Cells/Cell20"), "the first chest of the points track");
                s.SelectTab(RewardTab.Weekly);
                Assert.AreEqual(1 + 9, s.ListContent.childCount, "scene header + 9 tasks");
                s.SelectTab(RewardTab.Main);
                Assert.AreEqual(44 + 1, s.ListContent.childCount, "region 1: 44 goal cards + the Endless divider");
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
                Assert.IsNull(s.ListContent.Find("Row_g_1_levels_1/Card").GetComponent<Button>(), "claimed: no button any more");
                Assert.IsNull(root.transform.Find("RewardsRoot/Body/ClaimAll"), "nothing left to claim in the region");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void DailyChestIsAButtonOnlyWhenReached()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            svc.AddPlaySeconds(300);   // 25 points: the first chest only
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
        public void ReadyGoalCardHasAClaimButtonOfAtLeast48DpThatClaimsExactlyThatGoal()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 5);   // g_1_levels_1 = 150 coins; nothing else is complete
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
                Assert.AreEqual("Claim 150 coins", plate.GetComponent<A11yLabel>().Text);

                btn.onClick.Invoke();   // tap the green button
                RegionGoal goal = svc.RewardDefs.FindGoal("g_1_levels_1");
                Assert.IsTrue(svc.IsRegionGoalClaimed(goal));
                Assert.AreEqual(coins + 150, svc.Coins, "paid exactly the 150 coins of that goal");
                Assert.AreEqual(gems, svc.Gems);
                Assert.AreEqual(1, svc.RegionGoalsClaimedCount(1), "no other goal was claimed");
                Assert.IsNull(s.ListContent.Find("Row_g_1_levels_1/Card").GetComponent<Button>(), "claimed: green check card, no button");
                Assert.IsNotNull(s.ListContent.Find("Row_g_1_levels_1/Card/Check"), "claimed cards keep the check badge");
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
                Assert.AreEqual(coins + 500, svc.Coins);
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
                    Assert.AreEqual(44, goals.Count);
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
                Assert.IsNotNull(s.ListContent.Find("Row_g_1_levels_4/Card/RewardPlate/Hero"), "'clear 20 levels' hands out a random ACTIVE Common hero: the hero chip is on the plate");
                Assert.IsNull(s.ListContent.Find("Row_g_1_levels_1/Card/RewardPlate/Hero"), "other goals have no hero");
                s.SelectTab(RewardTab.Weekly);
                Transform weekly = s.ListContent.Find("Row_w_hard5/Card/RewardPlate/Hero");
                Assert.IsNotNull(weekly, "the weekly top reward shows a hero chip");
                Assert.AreEqual("Random hero", weekly.GetComponent<A11yLabel>().Text);
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
                Assert.Less(card.sizeDelta.y, 980f * 0.6f, "about half of the old 980 px reward popup");
                Assert.Less(card.sizeDelta.x, 1080f);
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
                Assert.AreEqual("150", s.RewardCard.Find("Chip/Amount").GetComponent<Text>().text, "150 coins: a single coin chip");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
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
                Assert.AreEqual(RewardsScreen.CompactCardH, h100, 1.5f, "compact base at 100 percent text (was 214)");
                Assert.GreaterOrEqual(h100, RewardsScreen.ClaimButtonH + 10f, "room for the 48 dp Claim button");
                Assert.Less(h100, 214f * 0.75f, "at least 25 percent tighter than before");
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
                                if (scale == 1f) Assert.AreEqual(RewardsScreen.CompactCardH, rowH, 1.5f, who + ": 100 percent card unchanged");
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
            Assert.AreEqual("task_hourglass", Slot("d_play5"));
            Assert.AreEqual("task_flag", Slot("d_level3"));
            Assert.AreEqual("task_flame", Slot("d_combo5"));
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
