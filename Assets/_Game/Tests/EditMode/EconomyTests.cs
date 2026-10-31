using System;
using System.IO;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using NUnit.Framework;
using UnityEngine;

namespace BoingUpSkyTower.Tests
{
    public class EconomyTests
    {
        ContentRepository repo;
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            tempDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "EconomyTests_" + Guid.NewGuid().ToString("N")));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        ProgressService NewService(int limit = 5) { return new ProgressService(repo, new MemorySaveStore(), limit); }

        static RunOutcome Won(int starMask = 1, int coins = 0, int gems = 0, int score = 100)
        {
            return new RunOutcome { Completed = true, StarMask = starMask, CoinsCollected = coins, GemsCollected = gems, Score = score };
        }

        void ClearRegion(ProgressService svc, int regionIndex, int starMask = 1)
        {
            RegionDef r = repo.GetRegionByIndex(regionIndex);
            foreach (LevelDef l in repo.LevelsOf(r.Id)) svc.ApplyRun(l.Id, Won(starMask));
        }

        // ---- wallet ----

        [Test]
        public void SpendingMoreThanOwnedFailsAndChangesNothing()
        {
            var svc = NewService();
            svc.AddGems(10);
            Assert.IsFalse(svc.TrySpendGems(11));
            Assert.AreEqual(10, svc.Gems);
            Assert.IsTrue(svc.TrySpendGems(10));
            Assert.AreEqual(0, svc.Gems);
            Assert.IsFalse(svc.TrySpendCoins(1));
        }

        [Test]
        public void NegativeAmountsAreRejected()
        {
            var svc = NewService();
            Assert.Throws<ArgumentOutOfRangeException>(() => svc.AddCoins(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => svc.TrySpendGems(-5));
        }

        [Test]
        public void WalletSaturatesInsteadOfOverflowing()
        {
            var svc = NewService();
            svc.AddCoins(int.MaxValue);
            svc.AddCoins(10);
            Assert.AreEqual(int.MaxValue, svc.Coins);
        }

        [Test]
        public void ChangedEventFiresOnEveryMutation()
        {
            var svc = NewService();
            int n = 0;
            svc.Changed += () => n++;
            svc.AddCoins(5);
            svc.TrySpendCoins(2);
            svc.ApplyRun("lvl_001_01", Won());
            Assert.AreEqual(3, n);
        }

        // ---- save ----

        [Test]
        public void ProgressSurvivesARestart()
        {
            string path = Path.Combine(tempDir, "save.json");
            var svc = new ProgressService(repo, new FileSaveStore(path));
            svc.ApplyRun("lvl_001_01", Won(starMask: 3, coins: 7, score: 555));
            int coins = svc.Coins;

            var reloaded = new ProgressService(repo, new FileSaveStore(path));
            Assert.AreEqual(coins, reloaded.Coins);
            LevelProgress p = reloaded.GetProgress("lvl_001_01");
            Assert.AreEqual(2, p.Stars);
            Assert.AreEqual(555, p.BestScore);
            Assert.IsFalse(File.Exists(path + ".tmp"), "no temp file left behind");
        }

        [Test]
        public void CorruptMainFileRecoversFromTheBackup()
        {
            string path = Path.Combine(tempDir, "save.json");
            var store = new FileSaveStore(path);
            var svc = new ProgressService(repo, store);
            svc.AddGems(40);
            svc.AddGems(2); // second save creates the backup of the 40-gem state
            File.WriteAllText(path, "{ this is not json");

            var store2 = new FileSaveStore(path);
            var recovered = new ProgressService(repo, store2);
            Assert.AreEqual("backup", store2.LastLoadSource);
            Assert.AreEqual(40, recovered.Gems);
        }

        [Test]
        public void FullyCorruptSaveStartsFreshWithoutThrowing()
        {
            string path = Path.Combine(tempDir, "save.json");
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(path, "garbage");
            File.WriteAllText(path + ".bak", "more garbage");
            var store = new FileSaveStore(path);
            var svc = new ProgressService(repo, store);
            Assert.AreEqual("none", store.LastLoadSource);
            Assert.AreEqual(0, svc.Coins);
        }

        [Test]
        public void SaveFromANewerAppVersionIsNotLoaded()
        {
            string path = Path.Combine(tempDir, "save.json");
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(path, "{\"version\":99,\"coins\":5,\"gems\":5,\"levels\":{},\"unlockedRegions\":[]}");
            var svc = new ProgressService(repo, new FileSaveStore(path));
            Assert.AreEqual(0, svc.Coins);
        }

        // ---- unlocks ----

        [Test]
        public void LevelsUnlockInOrderAndLockedLevelsCannotBePlayed()
        {
            var svc = NewService();
            LevelDef l1 = repo.GetLevel("lvl_001_01");
            LevelDef l2 = repo.GetLevel("lvl_001_02");
            Assert.IsTrue(svc.IsLevelUnlocked(l1));
            Assert.IsFalse(svc.IsLevelUnlocked(l2));
            Assert.Throws<InvalidOperationException>(() => svc.ApplyRun("lvl_001_02", Won()));
            svc.ApplyRun("lvl_001_01", Won());
            Assert.IsTrue(svc.IsLevelUnlocked(l2));
        }

        [Test]
        public void ClearingLevel15UnlocksTheNextRegion()
        {
            var svc = NewService();
            RegionDef r2 = repo.GetRegionByIndex(2);
            Assert.IsFalse(svc.IsRegionUnlocked(r2));
            Assert.IsFalse(svc.IsLevelUnlocked(repo.GetLevel("lvl_002_01")));
            ClearRegion(svc, 1);
            Assert.IsTrue(svc.IsRegionUnlocked(r2));
            Assert.IsTrue(svc.IsLevelUnlocked(repo.GetLevel("lvl_002_01")));
        }

        [Test]
        public void HomePageFollowsTheRegionBeingPlayed()
        {
            var svc = NewService();
            Assert.AreEqual(1, svc.CurrentRegion().Index);
            ClearRegion(svc, 1, starMask: 3);                               // every level of region 1 with 2 stars
            Assert.AreEqual(2, svc.CurrentRegion().Index, "region 1 done: the home page shows region 2");
            svc.ApplyRun("lvl_002_01", Won(starMask: 3));                   // reached level 2 of the new region
            Assert.AreEqual(2, svc.CurrentRegion().Index);
            ClearRegion(svc, 2, starMask: 3);
            Assert.AreEqual(3, svc.CurrentRegion().Index, "and so on");
        }

        [Test]
        public void Level15ReportsTheRegionItUnlocked()
        {
            var svc = NewService();
            RegionDef r1 = repo.GetRegionByIndex(1);
            RewardResult last = default(RewardResult);
            foreach (LevelDef l in repo.LevelsOf(r1.Id)) last = svc.ApplyRun(l.Id, Won());
            Assert.AreEqual(repo.GetRegionByIndex(2).Id, last.UnlockedRegionId);
        }

        [Test]
        public void Region5Level15DoesNotUnlockRegion6InTheFiveRegionBuild()
        {
            var svc = NewService(5);
            for (int r = 1; r <= 5; r++) ClearRegion(svc, r);
            RegionDef r6 = repo.GetRegionByIndex(6);
            Assert.IsTrue(r6.IsLive, "content still marks region 6 live");
            Assert.IsFalse(svc.IsRegionUnlocked(r6));
            Assert.IsFalse(svc.CanEnterRegion(r6));
        }

        [Test]
        public void RaisingThePlayableLimitUnlocksRegion6AfterRegion5()
        {
            var svc = NewService(6);
            for (int r = 1; r <= 5; r++) ClearRegion(svc, r);
            Assert.IsTrue(svc.CanEnterRegion(repo.GetRegionByIndex(6)));
        }

        [Test]
        public void TeaserRegionsCanNeverBeEntered()
        {
            var svc = NewService(30);
            RegionDef teaser = repo.GetRegionByIndex(21);
            Assert.IsFalse(teaser.IsLive);
            Assert.IsFalse(svc.IsRegionPlayable(teaser));
            Assert.IsFalse(svc.CanEnterRegion(teaser));
        }

        // ---- gem skip ----

        [Test]
        public void GemSkipCostsTheRegionPriceAndUnlocksTheNextRegion()
        {
            var svc = NewService();
            RegionDef r2 = repo.GetRegionByIndex(2);
            Assert.AreEqual(100, svc.GemSkipPrice(r2));
            Assert.AreEqual(SkipResult.NotEnoughGems, svc.TrySkipToRegion(r2));
            svc.AddGems(130);
            Assert.AreEqual(SkipResult.Ok, svc.TrySkipToRegion(r2));
            Assert.AreEqual(30, svc.Gems);
            Assert.IsTrue(svc.CanEnterRegion(r2));
            Assert.AreEqual(SkipResult.AlreadyUnlocked, svc.TrySkipToRegion(r2));
        }

        [Test]
        public void GemSkipOnlyReachesTheNextRegionInSequence()
        {
            var svc = NewService();
            svc.AddGems(5000);
            Assert.AreEqual(SkipResult.NotNextRegion, svc.TrySkipToRegion(repo.GetRegionByIndex(4)));
            Assert.AreEqual(SkipResult.Ok, svc.TrySkipToRegion(repo.GetRegionByIndex(2)));
            Assert.AreEqual(SkipResult.Ok, svc.TrySkipToRegion(repo.GetRegionByIndex(3)));
            Assert.AreEqual(5000 - 100 - 116, svc.Gems);
        }

        [Test]
        public void GemSkipIsRefusedBeyondThePlayableLimit()
        {
            var svc = NewService(5);
            svc.AddGems(5000);
            for (int r = 2; r <= 5; r++) Assert.AreEqual(SkipResult.Ok, svc.TrySkipToRegion(repo.GetRegionByIndex(r)));
            Assert.AreEqual(SkipResult.NotPlayable, svc.TrySkipToRegion(repo.GetRegionByIndex(6)));
        }

        // ---- rewards ----

        [Test]
        public void ClearPaysSeventyPlusSixtyPerStarAndOnlyThreeStarReplaysPayHalf()
        {
            var svc = NewService();
            LevelDef l = repo.GetLevel("lvl_001_01");
            double factor = repo.GetRegionByIndex(1).CoinFactor;

            RewardResult first = svc.ApplyRun(l.Id, Won(starMask: 1));       // 1 star: 70 + 60 = 130
            Assert.IsTrue(first.FirstClear);
            Assert.AreEqual((int)System.Math.Round(130 * factor), first.PayoutCoins);

            RewardResult second = svc.ApplyRun(l.Id, Won(starMask: 3));      // the level has 1 star only: full pay, 2 stars = 190
            Assert.AreEqual((int)System.Math.Round(190 * factor), second.PayoutCoins);

            RewardResult third = svc.ApplyRun(l.Id, Won(starMask: 7));       // still below 3 stars before the run: full pay, 3 stars = 250
            Assert.AreEqual((int)System.Math.Round(250 * factor), third.PayoutCoins);

            RewardResult replay = svc.ApplyRun(l.Id, Won(starMask: 7));      // now the level has 3 stars: 50 percent (owner 2026-10-25)
            Assert.AreEqual((int)System.Math.Round(250 * factor) / 2, replay.PayoutCoins, 1);
            Assert.AreEqual(50, EconomyRules.ReplayPercent);
        }

        [Test]
        public void PayoutFormulaCoversZeroToThreeStarsAndComboInRegions1To5()
        {
            for (int region = 1; region <= 5; region++)
            {
                RegionDef r = repo.GetRegionByIndex(region);
                LevelDef l = repo.LevelsOf(r.Id)[0];
                for (int stars = 0; stars <= 3; stars++)
                    foreach (int combo in new[] { 0, 5, 35, 200 })
                    {
                        int mask = stars == 0 ? 0 : (1 << stars) - 1;
                        RewardResult res = RewardCalculator.Compute(l, r, Won(starMask: mask, coins: combo), null);
                        long expected = (long)System.Math.Round((70 + 60 * stars) * r.CoinFactor, System.MidpointRounding.AwayFromZero)
                                      + (long)System.Math.Round(combo * r.CoinFactor, System.MidpointRounding.AwayFromZero);
                        Assert.AreEqual((int)expected, res.PayoutCoins, "region " + region + ", " + stars + " stars, combo " + combo);
                    }
            }
            Assert.AreEqual(70, RewardCalculator.Compute(repo.GetLevel("lvl_001_01"), repo.GetRegionByIndex(1), Won(starMask: 0), null).PayoutCoins);
            Assert.AreEqual(250, RewardCalculator.Compute(repo.GetLevel("lvl_001_01"), repo.GetRegionByIndex(1), Won(starMask: 7), null).PayoutCoins);
        }

        [Test]
        public void ThreeStarReplayPaysHalfWithComboAndNoGems()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Won(starMask: 7));
            RewardResult replay = svc.ApplyRun("lvl_001_01", Won(starMask: 7, coins: 40, gems: 3));
            Assert.AreEqual((250 + 40) / 2, replay.PayoutCoins, 1);
            Assert.AreEqual(20, replay.ComboCoins);
            Assert.AreEqual(0, replay.PickupGems);
            Assert.AreEqual(0, replay.TotalGems);
        }

        [Test]
        public void ReplayBelowThreeStarsPaysInFullAndKeepsTheGems()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Won(starMask: 3));
            double factor = repo.GetRegionByIndex(1).CoinFactor;
            RewardResult replay = svc.ApplyRun("lvl_001_01", Won(starMask: 3, gems: 2));
            Assert.AreEqual((int)System.Math.Round(190 * factor), replay.PayoutCoins);
            Assert.AreEqual(2, replay.PickupGems);
        }

        [Test]
        public void RegionFactorScalesThePayout()
        {
            var svc = NewService(5);
            svc.AddGems(5000);
            for (int r = 2; r <= 3; r++) svc.TrySkipToRegion(repo.GetRegionByIndex(r));
            RewardResult res = svc.ApplyRun("lvl_003_01", Won(starMask: 1));
            RewardResult base1 = NewService().ApplyRun("lvl_001_01", Won(starMask: 1));
            Assert.Greater(res.PayoutCoins, base1.PayoutCoins, "a later region pays more");
        }

        [Test]
        public void EachStarAddsSixtyCoins()
        {
            var one = NewService().ApplyRun("lvl_001_01", Won(starMask: 1));
            var three = NewService().ApplyRun("lvl_001_01", Won(starMask: 7));
            double factor = repo.GetRegionByIndex(1).CoinFactor;
            Assert.AreEqual((int)System.Math.Round(120 * factor), three.PayoutCoins - one.PayoutCoins, 1);
            Assert.AreEqual((int)System.Math.Round(180 * factor), three.StarBonusCoins, 1);
        }

        [Test]
        public void ComboCoinsArePaidAtTheEndAndNoGemsDropOnAThreeStarReplay()
        {
            var svc = NewService();
            RewardResult first = svc.ApplyRun("lvl_001_01", Won(starMask: 7, coins: 25, gems: 1));
            Assert.AreEqual(25, first.ComboCoins, 1, "combo coins are part of the payout");
            Assert.AreEqual(1, first.PickupGems);
            RewardResult again = svc.ApplyRun("lvl_001_01", Won(starMask: 7, coins: 25, gems: 1));   // the level has 3 stars: no gems again
            Assert.AreEqual(0, again.PickupGems);
            Assert.AreEqual(0, again.TotalGems);
        }

        [Test]
        public void PickupGemsArePaidAsReportedAndMilestonesPayOnTheFirstClearOnly()
        {
            var svc = NewService();
            Assert.AreEqual(5, svc.ApplyRun("lvl_001_01", Won(gems: 5)).PickupGems, "many gems per level now (about 5 on a long level)");
            Assert.AreEqual(0, svc.ApplyRun("lvl_001_02", Won(gems: 0)).RewardGems);
            var l15 = NewService();
            for (int i = 1; i <= 14; i++) l15.ApplyRun("lvl_001_" + i.ToString("D2"), Won());
            RewardResult first = l15.ApplyRun("lvl_001_15", Won(gems: 3));
            Assert.GreaterOrEqual(first.RewardGems, 5, "the first clear of level 15 guarantees 5 gems");
            Assert.AreEqual(3, first.PickupGems);
            RewardResult again = l15.ApplyRun("lvl_001_15", Won(gems: 3));
            Assert.AreEqual(0, again.RewardGems, "milestone gems pay on the first clear only");
        }

        [Test]
        public void NormalLevelsGiveGemsOnlyFromPickups()
        {
            var svc = NewService();
            RewardResult res = svc.ApplyRun("lvl_001_01", Won(gems: 0));
            Assert.AreEqual(0, res.RewardGems);
            Assert.AreEqual(0, res.TotalGems);
        }

        [Test]
        public void FailedRunChangesNothing()
        {
            var svc = NewService();
            RewardResult res = svc.ApplyRun("lvl_001_01", new RunOutcome { Completed = false, CoinsCollected = 99 });
            Assert.AreEqual(0, res.TotalCoins);
            Assert.AreEqual(0, svc.Coins);
            Assert.IsNull(svc.GetProgress("lvl_001_01"));
        }

        [Test]
        public void BestStarsAndScoreNeverDecrease()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Won(starMask: 7, score: 900));
            svc.ApplyRun("lvl_001_01", Won(starMask: 1, score: 100));
            LevelProgress p = svc.GetProgress("lvl_001_01");
            Assert.AreEqual(3, p.Stars);
            Assert.AreEqual(900, p.BestScore);
            Assert.AreEqual(2, p.Clears);
            Assert.AreEqual(3, svc.TotalStars());
        }

        [Test]
        public void StarsAccumulateAcrossRunsByMask()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Won(starMask: 1));
            svc.ApplyRun("lvl_001_01", Won(starMask: 2)); // earned only star 2 this time
            Assert.AreEqual(2, svc.GetProgress("lvl_001_01").Stars);
        }

        [Test]
        public void HiddenGemIsTheGoalAndPaysNoExtraGem()
        {
            var svc = NewService();
            RunOutcome run = Won();
            run.HiddenGems = 1;
            RewardResult first = svc.ApplyRun("lvl_001_01", run);
            Assert.AreEqual(0, first.HiddenGemReward, "gems are rare: only the planned pickups pay");
            Assert.AreEqual(0, svc.Gems);
        }
    }
}
