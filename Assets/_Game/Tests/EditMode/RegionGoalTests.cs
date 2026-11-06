using System;
using System.Collections.Generic;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>
    /// Main tab = per-region goals (Bug List 5 F1.a, docs/39): the goal lists, region gating, retroactive progress from an existing save, the per-region
    /// counters, claiming (single, per region, hero copies) and the Admin "Main rewards ON" switch. The pure table checks (monotonic targets, budgets) are
    /// in Tests/Simulation/RewardTablesTests.cs.
    /// </summary>
    public class RegionGoalTests
    {
        ContentRepository repo;
        MemorySaveStore store;
        DateTime now;

        static readonly DateTime Monday = new DateTime(2026, 11, 2, 12, 0, 0);
        const string Region1 = "region_001_meadow_springs";

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            store = new MemorySaveStore();
            now = Monday;
            ProgressService.TestMainRewardsAllowed = false;
        }

        [TearDown]
        public void TearDown()
        {
            ProgressService.TestMainRewardsAllowed = false;
        }

        ProgressService NewService()
        {
            var svc = new ProgressService(repo, store, 5);
            svc.Clock = () => now;
            return svc;
        }

        static RunOutcome Won(int starMask = 1) { return new RunOutcome { Completed = true, StarMask = starMask, Score = 100 }; }

        void ClearLevels(ProgressService svc, int regionIndex, int count, int starMask = 1)
        {
            RegionDef r = repo.GetRegionByIndex(regionIndex);
            int n = 0;
            foreach (LevelDef l in repo.LevelsOf(r.Id)) { if (n++ >= count) break; svc.ApplyRun(l.Id, Won(starMask)); }
        }

        RegionGoal Goal(ProgressService svc, string id)
        {
            RegionGoal g = svc.RewardDefs.FindGoal(id);
            Assert.IsNotNull(g, id);
            return g;
        }

        /// <summary>A save that already has progress (an existing player): the level results and the best endless floor, no per-region counters.</summary>
        ProgressService SeededService()
        {
            var d = new SaveData();
            RegionDef r1 = repo.GetRegionByIndex(1);
            foreach (string id in r1.Levels) d.Levels[id] = new LevelProgress { Stars = 3, StarMask = 7, Clears = 1, BestScore = 100 };
            d.Levels[r1.Levels[6]].Clears = 2;   // the no-wall level, cleared twice
            d.EndlessBest[Region1] = 650;
            d.Coins = 1234;
            d.Gems = 56;
            store.Save(d);
            return NewService();
        }

        // ---- the lists ----

        [Test]
        public void EveryPlayableRegionHasTheFullGoalListAndComingSoonRegionsHaveNone()
        {
            ProgressService svc = NewService();
            var ids = new HashSet<string>();
            for (int r = 1; r <= RegionGoalTable.TotalRegions; r++)
            {
                IReadOnlyList<RegionGoal> goals = svc.RegionGoals(r);
                if (r <= RegionGoalTable.PlayableRegions)
                {
                    Assert.AreEqual(44, goals.Count, "region " + r);
                    Assert.IsFalse(svc.IsRegionComingSoon(r));
                    foreach (RegionGoal g in goals)
                    {
                        Assert.IsTrue(ids.Add(g.Id), "unique id " + g.Id);
                        Assert.AreEqual("g_" + r + "_" + g.Stat + "_" + g.Tier, g.Id);
                        Assert.AreEqual(r, g.Region);
                        Assert.Greater(g.Target, 0, g.Id);
                        Assert.IsFalse(string.IsNullOrEmpty(g.Title), g.Id);
                        Assert.IsFalse(g.Gems == 0 && g.Coins == 0 && g.PowerUps == 0, g.Id + " pays something");
                    }
                }
                else
                {
                    Assert.AreEqual(0, goals.Count, "Coming Soon region " + r + " has no goals yet");
                    Assert.IsTrue(svc.IsRegionComingSoon(r));
                    Assert.IsFalse(svc.HasClaimableInRegion(r));
                    Assert.IsFalse(svc.IsRegionGoalsOpen(r));
                }
            }
            Assert.AreEqual(0, svc.RegionGoals(0).Count);
            Assert.AreEqual(0, svc.RegionGoals(31).Count);
            Assert.AreEqual(5 * 44, svc.RewardDefs.AllRegionGoals.Count);
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimRegionGoal("g_6_stars_1").Result);
        }

        [Test]
        public void TheEndlessGoalsUseTheOwnersFloorTiers()
        {
            ProgressService svc = NewService();
            var floors = new List<int>();
            foreach (RegionGoal g in svc.RegionGoals(3)) if (g.Type == RegionGoalType.EndlessFloors) floors.Add(g.Target);
            CollectionAssert.AreEqual(new[] { 100, 200, 500, 1000, 1500, 2000, 3000, 4000, 5000, 7500, 10000 }, floors);
        }

        [Test]
        public void TheLastLevelsGoalGivesTheRegionsStarterHeroRegion1AlsoHandsOutItsFourOtherCommonsAndThereIsNoSeparateRegionBonus()
        {
            ProgressService svc = NewService();
            var region1Extra = new Dictionary<string, string>
            {
                { "g_1_levels_2", "bumble" }, { "g_1_levels_3", "ribbi" }, { "g_1_levels_4", "dandy" }, { "g_1_stars_5", "pip" },
                { "g_2_levels_2", "nib" }, { "g_2_levels_3", "spike" }, { "g_2_levels_4", "moss" }, { "g_2_stars_5", "fenn" }
            };
            for (int r = 1; r <= 5; r++)
            {
                int heroes = 0;
                foreach (RegionGoal g in svc.RegionGoals(r))
                {
                    if (!g.GivesHero) continue;
                    heroes++;
                    string named;
                    if (g.HeroId != null) { Assert.IsTrue(r <= 2); Assert.IsTrue(region1Extra.TryGetValue(g.Id, out named), g.Id); Assert.AreEqual(named, g.HeroId); Assert.AreEqual(0, g.HeroRegion, g.Id); }
                    else { Assert.AreEqual("g_" + r + "_levels_5", g.Id); Assert.AreEqual(r, g.HeroRegion); Assert.AreEqual(15, g.Target); }
                }
                Assert.AreEqual(r <= 2 ? 5 : 1, heroes, "region " + r);
            }
            Assert.AreEqual(0, svc.RewardDefs.RegionRewards.Count);
        }

        // ---- gating and retroactive progress ----

        [Test]
        public void AClosedRegionsGoalsCountButCannotBeClaimedUntilTheRegionIsUnlocked()
        {
            var d = new SaveData();
            RegionDef r2 = repo.GetRegionByIndex(2);
            d.Levels[r2.Levels[0]] = new LevelProgress { Stars = 3, StarMask = 7, Clears = 1 };   // progress in a region that is not unlocked
            store.Save(d);
            ProgressService svc = NewService();
            RegionGoal g = Goal(svc, "g_2_stars_1");
            Assert.IsFalse(svc.IsRegionGoalsOpen(2));
            Assert.AreEqual(3, svc.RegionGoalProgress(2, g));
            Assert.IsFalse(svc.IsRegionGoalReady(g));
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimRegionGoal("g_2_levels_1").Result);
            Assert.IsFalse(svc.HasClaimableMain());
            d.UnlockedRegions.Add(r2.Id);
            store.Save(d);
            ProgressService open = NewService();
            Assert.IsTrue(open.IsRegionGoalsOpen(2));
            Assert.IsFalse(open.IsRegionGoalReady(Goal(open, "g_2_stars_1")), "3 stars of the 9 needed");
        }

        [Test]
        public void ExistingProgressMakesGoalsReadyButNothingIsAutoPaid()
        {
            ProgressService svc = SeededService();
            Assert.AreEqual(1234, svc.Coins);
            Assert.AreEqual(56, svc.Gems);
            var ready = new List<string>();
            foreach (RegionGoal g in svc.RegionGoals(1)) if (svc.IsRegionGoalReady(g)) ready.Add(g.Id);
            // stars 5 + levels 5 + 3-star levels 3 + no-wall tier 1 (2 clears, 3 needed for tier 2) + endless 100, 200, 500 (best floor 650)
            Assert.AreEqual(5 + 5 + 3 + 1 + 3, ready.Count, string.Join(",", ready));
            CollectionAssert.Contains(ready, "g_1_stars_5");
            CollectionAssert.Contains(ready, "g_1_levels_5");
            CollectionAssert.Contains(ready, "g_1_nowall_1");
            CollectionAssert.DoesNotContain(ready, "g_1_nowall_2");
            CollectionAssert.Contains(ready, "g_1_endless_3");
            CollectionAssert.DoesNotContain(ready, "g_1_endless_4");
            // counters that were never stored start at zero in this build
            Assert.AreEqual(0, svc.RegionGoalProgress(Goal(svc, "g_1_jumps_1")));
            Assert.AreEqual(0, svc.RegionGoalProgress(Goal(svc, "g_1_combos_1")));
            Assert.AreEqual(0, svc.RegionGoalProgress(Goal(svc, "g_1_time_1")));
            Assert.AreEqual(0, svc.RegionGoalProgress(Goal(svc, "g_1_coins_1")));
            Assert.AreEqual(0, svc.RegionGoalProgress(Goal(svc, "g_1_powerups_1")));
            Assert.AreEqual(0, svc.RegionGoalProgress(Goal(svc, "g_1_hard_1")));
            Assert.AreEqual(45, svc.RegionGoalProgress(Goal(svc, "g_1_stars_5")));
            Assert.AreEqual(15, svc.RegionGoalProgress(Goal(svc, "g_1_levels_5")));
            Assert.AreEqual(15, svc.RegionGoalProgress(Goal(svc, "g_1_stars3_3")));
            Assert.AreEqual(2, svc.RegionGoalProgress(Goal(svc, "g_1_nowall_1")));
            Assert.AreEqual(650, svc.RegionGoalProgress(Goal(svc, "g_1_endless_11")));
            Assert.IsTrue(svc.HasClaimableMain());
            Assert.IsTrue(svc.HasClaimableInRegion(1));
            Assert.IsFalse(svc.HasClaimableInRegion(2));
            Assert.AreEqual(0, svc.RegionGoalsClaimedCount(1));
        }

        // ---- the per-region counters ----

        [Test]
        public void RegionCountersCountOnlyPlayInsideTheirRegion()
        {
            ProgressService svc = NewService();
            svc.RecordRunStats(new RunOutcome { Region = 2, Jumps = 120, ComboChains = 4, PowerUpsUsed = 3, BestCombo = 5 });
            svc.RecordRunStats(new RunOutcome { Region = 1, Jumps = 30 });
            svc.AddPlaySeconds(600, false, 2);
            svc.AddPlaySeconds(90, false, 0);   // menus: no region
            RegionStats r1 = svc.RegionStatsOf(1), r2 = svc.RegionStatsOf(2);
            Assert.AreEqual(30, r1.Jumps);
            Assert.AreEqual(0, r1.PlaySeconds);
            Assert.AreEqual(120, r2.Jumps);
            Assert.AreEqual(4, r2.Combos);
            Assert.AreEqual(3, r2.PowerUps);
            Assert.AreEqual(600, r2.PlaySeconds);
            Assert.AreEqual(0, svc.RegionStatsOf(3).Jumps);
            Assert.AreEqual(0, svc.RegionStatsOf(99).Jumps, "an invalid region reads as zero");
            Assert.AreEqual(10, svc.RegionGoalProgress(Goal(svc, "g_2_time_1")), "600 seconds = 10 minutes in region 2");
            Assert.AreEqual(690, svc.RewardStatValue(RewardTab.Daily, RewardStat.PlaySeconds), "the daily play time still counts everything, menus included");
            Assert.AreEqual(4, svc.RewardStatValue(RewardTab.Daily, RewardStat.Combos), "the daily combo counter is the global one");
        }

        [Test]
        public void ACompletedLevelCountsForItsOwnRegionIncludingCoinsPaidAndHardClears()
        {
            ProgressService svc = NewService();
            svc.RecordLevelStarted(3);
            RewardResult a = svc.ApplyRun("lvl_001_01", new RunOutcome { Completed = true, StarMask = 7, Score = 100, ComboChains = 2, PowerUpsUsed = 1, Jumps = 40, Region = 1 });
            RegionStats s = svc.RegionStatsOf(1);
            Assert.AreEqual(a.TotalCoins, s.Coins, "the coins paid for the clear");
            Assert.Greater(s.Coins, 0);
            Assert.AreEqual(2, s.Combos);
            Assert.AreEqual(1, s.PowerUps);
            Assert.AreEqual(40, s.Jumps);
            Assert.AreEqual(1, s.HardClears);
            Assert.AreEqual(0, svc.RegionStatsOf(2).Coins);
            svc.RecordLevelStarted(2);
            svc.ApplyRun("lvl_001_02", Won());
            Assert.AreEqual(1, svc.RegionStatsOf(1).HardClears, "a Normal clear is not a hard clear");
        }

        [Test]
        public void EndlessCoinsCountForTheRegionAndTheBestFloorIsTheGoalProgress()
        {
            ProgressService svc = NewService();
            EndlessReward er = svc.ApplyEndlessRun(Region1, 240, "20261102");
            Assert.Greater(er.Coins, 0);
            Assert.AreEqual(er.Coins, svc.RegionStatsOf(1).Coins);
            Assert.AreEqual(240, svc.RegionGoalProgress(Goal(svc, "g_1_endless_2")));
            Assert.IsTrue(svc.IsRegionGoalReady(Goal(svc, "g_1_endless_1")));
            Assert.IsTrue(svc.IsRegionGoalReady(Goal(svc, "g_1_endless_2")));
            Assert.IsFalse(svc.IsRegionGoalReady(Goal(svc, "g_1_endless_3")));
        }

        // ---- claiming ----

        [Test]
        public void AGoalPaysOnceAndNeverResetsWithTheClock()
        {
            ProgressService svc = SeededService();
            int gems = svc.Gems, coins = svc.Coins;
            RegionGoal g = Goal(svc, "g_1_stars_2");   // 18 stars: 3 gems
            RewardClaimOutcome ok = svc.TryClaimRegionGoal(g.Id);
            Assert.AreEqual(RewardClaimResult.Ok, ok.Result);
            Assert.AreEqual(3, ok.Gems);
            Assert.AreEqual(gems + 3, svc.Gems);
            Assert.AreEqual(coins, svc.Coins);
            Assert.IsTrue(svc.IsRegionGoalClaimed(g));
            Assert.IsFalse(svc.IsRegionGoalReady(g), "a claimed goal is no longer ready, its progress stays");
            Assert.AreEqual(45, svc.RegionGoalProgress(g));
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimRegionGoal(g.Id).Result);
            now = Monday.AddDays(40);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, svc.TryClaimRegionGoal(g.Id).Result);
            Assert.AreEqual(RewardClaimResult.NotReady, svc.TryClaimRegionGoal("g_1_endless_4").Result);
            Assert.AreEqual(RewardClaimResult.Invalid, svc.TryClaimRegionGoal("nope").Result);
            Assert.AreEqual(gems + 3, svc.Gems);
        }

        [Test]
        public void ClaimAllRegionPaysEverythingReadyOfThatRegionOnlyInOneSave()
        {
            ProgressService svc = SeededService();
            int expectGems = 0, expectCoins = 0, expectUnits = 0, expectHeroes = 0;
            foreach (RegionGoal g in svc.RegionGoals(1))
                if (svc.IsRegionGoalReady(g)) { expectGems += g.Gems; expectCoins += g.Coins; expectUnits += g.PowerUps; if (g.GivesHero) expectHeroes++; }
            int gems = svc.Gems, coins = svc.Coins, saves = 0;
            svc.Changed += () => saves++;
            RewardClaimOutcome res = svc.ClaimAllRegion(1);
            Assert.AreEqual(RewardClaimResult.Ok, res.Result);
            Assert.AreEqual(expectGems, res.Gems);
            Assert.AreEqual(expectCoins, res.Coins);
            Assert.AreEqual(expectUnits, res.PowerUpIds.Count);
            Assert.AreEqual(gems + expectGems, svc.Gems);
            Assert.AreEqual(coins + expectCoins, svc.Coins);
            Assert.AreEqual(expectHeroes, res.HeroIds.Count, "every ready hero goal gives its hero");
            CollectionAssert.Contains(res.HeroIds, "momo", "the clear-all goal is ready, so the region hero comes with it");
            Assert.IsFalse(svc.HasClaimableInRegion(1));
            Assert.AreEqual(RewardClaimResult.NotReady, svc.ClaimAllRegion(1).Result, "nothing left");
            Assert.AreEqual(RewardClaimResult.NotReady, svc.ClaimAllRegion(2).Result);
            Assert.GreaterOrEqual(saves, 1);
            Assert.IsTrue(svc.RegionGoalsClaimedCount(1) > 0);
        }

        [Test]
        public void ClearingAllLevelsAddsExactlyOneCopyOfTheRegionsCommonHero()
        {
            ProgressService svc = NewService();
            ClearLevels(svc, 1, 15, 7);
            ClearLevels(svc, 2, 15, 7);
            int momo = svc.Copies("momo"), capi = svc.Copies("forestgirl"), otto = svc.Copies("beachotter");
            RewardClaimOutcome r1 = svc.TryClaimRegionGoal("g_1_levels_5");
            Assert.AreEqual(RewardClaimResult.Ok, r1.Result);
            CollectionAssert.AreEqual(new[] { "momo" }, r1.HeroIds);
            Assert.AreEqual(momo + 1, svc.Copies("momo"));
            Assert.AreEqual(capi, svc.Copies("forestgirl"));
            RewardClaimOutcome r2 = svc.TryClaimRegionGoal("g_2_levels_5");
            CollectionAssert.AreEqual(new[] { "forestgirl" }, r2.HeroIds);
            Assert.AreEqual(capi + 1, svc.Copies("forestgirl"));
            Assert.AreEqual(otto, svc.Copies("beachotter"));
            svc.TryClaimRegionGoal("g_1_levels_5");   // already claimed: no second copy
            Assert.AreEqual(momo + 1, svc.Copies("momo"));
            // lower Levels goals give no starter hero
            RewardClaimOutcome low = svc.TryClaimRegionGoal("g_1_levels_1");
            Assert.AreEqual(0, low.HeroIds.Count);
            Assert.AreEqual(momo + 1, svc.Copies("momo"));
            // Region 1 progress hands out the four other Common heroes, one copy each; they are not owned before (docs/40)
            foreach (string id in new[] { "bumble", "ribbi", "dandy", "pip" }) Assert.AreEqual(0, svc.Copies(id), id + " is not free");
            CollectionAssert.AreEqual(new[] { "bumble" }, svc.TryClaimRegionGoal("g_1_levels_2").HeroIds);
            CollectionAssert.AreEqual(new[] { "ribbi" }, svc.TryClaimRegionGoal("g_1_levels_3").HeroIds);
            CollectionAssert.AreEqual(new[] { "dandy" }, svc.TryClaimRegionGoal("g_1_levels_4").HeroIds);
            CollectionAssert.AreEqual(new[] { "pip" }, svc.TryClaimRegionGoal("g_1_stars_5").HeroIds);
            foreach (string id in new[] { "bumble", "ribbi", "dandy", "pip" }) { Assert.AreEqual(1, svc.Copies(id), id); Assert.IsTrue(svc.OwnsCharacter(id), id); Assert.IsTrue(svc.IsCharacterVisible(id), id + " needs no reveal"); }
            Assert.AreEqual(0, svc.TryClaimRegionGoal("g_1_levels_2").HeroIds.Count, "claimed once");
            Assert.AreEqual(1, svc.Copies("bumble"));
        }

        [Test]
        public void ClaimedGoalsAndRegionCountersSurviveASaveRoundTrip()
        {
            ProgressService svc = NewService();
            svc.RecordRunStats(new RunOutcome { Region = 3, Jumps = 77, ComboChains = 5 });
            svc.AddPlaySeconds(130, false, 3);
            svc.FlushPlayTime();
            ClearLevels(svc, 1, 3, 7);
            Assert.AreEqual(RewardClaimResult.Ok, svc.TryClaimRegionGoal("g_1_levels_1").Result);
            var again = NewService();
            Assert.AreEqual(77, again.RegionStatsOf(3).Jumps);
            Assert.AreEqual(5, again.RegionStatsOf(3).Combos);
            Assert.AreEqual(130, again.RegionStatsOf(3).PlaySeconds);
            Assert.IsTrue(again.IsRegionGoalClaimed(Goal(again, "g_1_levels_1")));
            Assert.AreEqual(svc.Coins, again.Coins);
        }

        [Test]
        public void FailedGoalClaimsChangeNothingInTheSave()
        {
            ProgressService svc = NewService();
            svc.RecordLogin();
            string before = JsonConvert.SerializeObject(svc.Data);
            svc.TryClaimRegionGoal("g_1_stars_1");
            svc.TryClaimRegionGoal("g_4_levels_5");
            svc.TryClaimRegionGoal("bogus");
            svc.ClaimAllRegion(1);
            Assert.AreEqual(before, JsonConvert.SerializeObject(svc.Data));
        }

        [Test]
        public void MainTabReadsTheGoalListsThroughTheGenericApiToo()
        {
            ProgressService svc = SeededService();
            int n = 0;
            foreach (RewardEntry e in svc.ClaimableEntries(RewardTab.Main)) { n++; Assert.IsInstanceOf<RegionGoal>(e); }
            Assert.AreEqual(5 * 44, n);
            Assert.IsTrue(svc.HasClaimable(RewardTab.Main));
            RewardClaimOutcome all = svc.ClaimAll(RewardTab.Main);
            Assert.AreEqual(RewardClaimResult.Ok, all.Result);
            Assert.IsFalse(svc.HasClaimable(RewardTab.Main));
        }

        // ---- Admin switch ----

        [Test]
        public void AdminSwitchMarksEveryGoalOfEveryPlayableRegionReadyWithoutPayingAndRestoresOnOff()
        {
            ProgressService.TestMainRewardsAllowed = true;
            ProgressService svc = NewService();
            int gems = svc.Gems, coins = svc.Coins;
            Assert.IsFalse(svc.HasClaimableMain());
            Assert.IsTrue(svc.AdminSetMainRewardsReady(true));
            for (int r = 1; r <= 5; r++)
            {
                foreach (RegionGoal g in svc.RegionGoals(r))
                {
                    Assert.IsTrue(svc.IsRegionGoalReady(g), g.Id);
                    Assert.IsFalse(svc.IsRegionGoalClaimed(g), g.Id);
                }
                Assert.IsTrue(svc.HasClaimableInRegion(r));
            }
            Assert.IsFalse(svc.HasClaimableInRegion(6));
            Assert.AreEqual(gems, svc.Gems);
            Assert.AreEqual(coins, svc.Coins);
            Assert.AreEqual(0, svc.TotalStars(), "real progress untouched");
            Assert.AreEqual(0, svc.RegionStatsOf(1).Jumps);
            Assert.IsFalse(svc.AdminSetMainRewardsReady(false));
            Assert.IsFalse(svc.HasClaimableMain(), "back to the real progress");
            Assert.AreEqual(0, svc.RegionGoalProgress(Goal(svc, "g_1_jumps_1")));
        }

        [Test]
        public void AdminSwitchIsDroppedWhenSystemTestGoesOff()
        {
            ProgressService.TestMainRewardsAllowed = true;
            ProgressService svc = NewService();
            svc.AdminSetMainRewardsReady(true);
            ProgressService.TestMainRewardsAllowed = false;
            Assert.IsFalse(svc.AdminMainRewardsReady);
            Assert.IsFalse(svc.HasClaimableMain());
            ProgressService.TestMainRewardsAllowed = true;
            Assert.IsFalse(svc.AdminMainRewardsReady, "stays off");
            ProgressService.TestMainRewardsAllowed = false;
            Assert.IsFalse(svc.AdminSetMainRewardsReady(true), "cannot be switched on without System Test");
        }

        [Test]
        public void ResetAllClearsTheRegionCountersAndTheClaimedGoals()
        {
            ProgressService svc = SeededService();
            svc.RecordRunStats(new RunOutcome { Region = 1, Jumps = 9 });
            svc.TryClaimRegionGoal("g_1_stars_1");
            svc.ResetAll();
            Assert.AreEqual(0, svc.RegionStatsOf(1).Jumps);
            Assert.AreEqual(0, svc.RegionGoalsClaimedCount(1));
            Assert.IsFalse(svc.HasClaimableMain());
        }
    }
}
