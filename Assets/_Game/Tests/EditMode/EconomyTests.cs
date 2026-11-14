using System;
using System.IO;
using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using NUnit.Framework;
using UnityEngine;

namespace FluffyClimbers.Tests
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
            Assert.AreEqual(500, svc.GemSkipPrice(r2), "region skip is 500 gems per region (owner 2026-11-13)");
            Assert.AreEqual(SkipResult.NotEnoughGems, svc.TrySkipToRegion(r2));
            svc.AddGems(530);
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
            Assert.AreEqual(5000 - 500 - 500, svc.Gems);
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
        public void ClearPaysFiftySixPlusFortyEightPerStarAndOnlyThreeStarReplaysPayAQuarter()
        {
            var svc = NewService();
            LevelDef l = repo.GetLevel("lvl_001_01");
            double factor = repo.GetRegionByIndex(1).CoinFactor;

            RewardResult first = svc.ApplyRun(l.Id, Won(starMask: 1));       // 1 star: 56 + 48 = 104
            Assert.IsTrue(first.FirstClear);
            Assert.AreEqual((int)System.Math.Round(104 * factor), first.PayoutCoins);

            RewardResult second = svc.ApplyRun(l.Id, Won(starMask: 3));      // the level has 1 star only: full pay, 2 stars = 152
            Assert.AreEqual((int)System.Math.Round(152 * factor), second.PayoutCoins);

            RewardResult third = svc.ApplyRun(l.Id, Won(starMask: 7));       // still below 3 stars before the run: full pay, 3 stars = 200
            Assert.AreEqual((int)System.Math.Round(200 * factor), third.PayoutCoins);

            RewardResult replay = svc.ApplyRun(l.Id, Won(starMask: 7));      // now the level has 3 stars: 25 percent (owner 2026-11-13, was 50)
            Assert.AreEqual((int)System.Math.Round(200 * factor) / 4, replay.PayoutCoins, 1);
            Assert.AreEqual(25, EconomyRules.ReplayPercent);
        }

        [Test]
        public void PayoutFormulaCoversZeroToThreeStarsAndComboInRegions1To5()
        {
            for (int region = 1; region <= 5; region++)
            {
                RegionDef r = repo.GetRegionByIndex(region);
                LevelDef l = repo.LevelsOf(r.Id)[0];
                for (int stars = 0; stars <= 3; stars++)
                    foreach (int combo in new[] { 0, 5, 35, 200 })   // combo = the coins the simulation counted (5 per combo step); a level pays 4 per step
                    {
                        int mask = stars == 0 ? 0 : (1 << stars) - 1;
                        RewardResult res = RewardCalculator.Compute(l, r, Won(starMask: mask, coins: combo), null);
                        long expected = (long)System.Math.Round((56 + 48 * stars) * r.CoinFactor, System.MidpointRounding.AwayFromZero)
                                      + (long)System.Math.Round(combo * 4 / 5 * r.CoinFactor, System.MidpointRounding.AwayFromZero);
                        Assert.AreEqual((int)expected, res.PayoutCoins, "region " + region + ", " + stars + " stars, combo " + combo);
                    }
            }
            Assert.AreEqual(56, RewardCalculator.Compute(repo.GetLevel("lvl_001_01"), repo.GetRegionByIndex(1), Won(starMask: 0), null).PayoutCoins);
            Assert.AreEqual(200, RewardCalculator.Compute(repo.GetLevel("lvl_001_01"), repo.GetRegionByIndex(1), Won(starMask: 7), null).PayoutCoins);
        }

        [Test]
        public void ThreeStarReplayPaysAQuarterWithComboAndNoGems()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Won(starMask: 7));
            RewardResult replay = svc.ApplyRun("lvl_001_01", Won(starMask: 7, coins: 40, gems: 3));
            Assert.AreEqual((200 + 32) / 4, replay.PayoutCoins, 1);   // 40 sim coins = 8 combo steps = 32 level coins
            Assert.AreEqual(8, replay.ComboCoins);
            Assert.AreEqual(0, replay.PickupGems);
            Assert.AreEqual(0, replay.TotalGems);
        }

        [Test]
        public void ReplayBelowThreeStarsPaysInFull()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Won(starMask: 3));
            double factor = repo.GetRegionByIndex(1).CoinFactor;
            RewardResult replay = svc.ApplyRun("lvl_001_01", Won(starMask: 3, gems: 2));
            Assert.AreEqual((int)System.Math.Round(152 * factor), replay.PayoutCoins);
            Assert.AreEqual(0, replay.PickupGems, "no gem pickups pay any more");
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
        public void EachStarAddsFortyEightCoins()
        {
            var one = NewService().ApplyRun("lvl_001_01", Won(starMask: 1));
            var three = NewService().ApplyRun("lvl_001_01", Won(starMask: 7));
            double factor = repo.GetRegionByIndex(1).CoinFactor;
            Assert.AreEqual((int)System.Math.Round(96 * factor), three.PayoutCoins - one.PayoutCoins, 1);
            Assert.AreEqual((int)System.Math.Round(144 * factor), three.StarBonusCoins, 1);
        }

        [Test]
        public void ComboCoinsArePaidAtTheEndAtFourPerStepAndNoGemsDropOnAThreeStarReplay()
        {
            var svc = NewService();
            RewardResult first = svc.ApplyRun("lvl_001_01", Won(starMask: 7, coins: 25, gems: 1));   // 25 sim coins = 5 combo steps = 20 level coins
            Assert.AreEqual(20, first.ComboCoins, 1, "combo coins are part of the payout");
            Assert.AreEqual(0, first.PickupGems, "no gem pickups pay any more");
            RewardResult again = svc.ApplyRun("lvl_001_01", Won(starMask: 7, coins: 25, gems: 1));   // the level has 3 stars: no gems again
            Assert.AreEqual(0, again.PickupGems);
            Assert.AreEqual(0, again.TotalGems);
        }

        [Test]
        public void OnlyTheMilestoneLevelsPayFixedGemsOnTheFirstClear()
        {
            var svc = NewService();
            int[] want = new int[26];
            want[5] = 1; want[10] = 1; want[15] = 2; want[20] = 2; want[25] = 3;
            int total = 0;
            for (int i = 1; i <= 25; i++)
            {
                RewardResult first = svc.ApplyRun("lvl_001_" + i.ToString("D2"), Won(gems: 4));   // reported pickups are ignored
                Assert.AreEqual(want[i], first.RewardGems, "level " + i + " first clear");
                Assert.AreEqual(0, first.PickupGems);
                Assert.AreEqual(0, first.HiddenGemReward);
                total += first.RewardGems;
            }
            Assert.AreEqual(9, total, "9 milestone gems per region");
            RewardResult again = svc.ApplyRun("lvl_001_25", Won());
            Assert.AreEqual(0, again.RewardGems, "milestone gems pay on the first clear only");
            Assert.AreEqual(0, again.HeroBonusGems);
            Assert.AreEqual(0, again.HeroBonusCoins);
        }

        [Test]
        public void TheSelectedHeroPaysItsRarityBonusOnTheFirstClearOfMilestoneLevelsOnly()
        {
            var momo = NewService();
            for (int i = 1; i <= 4; i++) Assert.AreEqual(0, momo.ApplyRun("lvl_001_0" + i, Won()).HeroBonusCoins, "no bonus on level " + i);
            RewardResult c5 = momo.ApplyRun("lvl_001_05", Won());   // no hero id = a Common hero
            Assert.AreEqual(50, c5.HeroBonusCoins);
            Assert.AreEqual(0, c5.HeroBonusGems);
            Assert.AreEqual(c5.PayoutCoins + 50, c5.TotalCoins);
            Assert.AreEqual(c5.RewardGems, c5.TotalGems);
            Assert.AreEqual(0, momo.ApplyRun("lvl_001_05", Won()).HeroBonusCoins, "first clear only");

            string[] ids = { "momo", null, null, null, null };
            foreach (CharacterSpec c in CharacterDefs.All)
                if (c.Rarity != CharacterRarity.Common && ids[(int)c.Rarity] == null) ids[(int)c.Rarity] = c.Id;
            for (int rarity = 1; rarity <= 4; rarity++)
            {
                if (ids[rarity] == null) continue;
                var svc = NewService();
                for (int i = 1; i <= 4; i++) svc.ApplyRun("lvl_001_0" + i, Won());
                RunOutcome run = Won();
                run.HeroId = ids[rarity];
                RewardResult r = svc.ApplyRun("lvl_001_05", run);
                Assert.AreEqual(FluffyClimbers.Simulation.CoinPayoutRules.HeroBonusCoinsByRarity[rarity], r.HeroBonusCoins, "rarity " + rarity);
                int sure = FluffyClimbers.Simulation.CoinPayoutRules.HeroBonusGemsByRarity[rarity];
                Assert.That(r.HeroBonusGems, Is.EqualTo(sure).Or.EqualTo(sure + 1), "rarity " + rarity);
                Assert.AreEqual(r.RewardGems + r.HeroBonusGems, r.TotalGems);
                Assert.AreEqual(c5.PayoutCoins + FluffyClimbers.Simulation.CoinPayoutRules.HeroBonusCoinsByRarity[rarity], r.TotalCoins, "fixed coins: no region factor, no cut");
            }
        }

        [Test]
        public void NormalLevelsGiveNoGems()
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
        public void TheHiddenGemOfLevel15IsOnlyTheGoalObjectItsTwoGemsAreTheMilestoneReward()
        {
            var svc = NewService();
            for (int i = 1; i <= 14; i++) svc.ApplyRun("lvl_001_" + i.ToString("D2"), Won());
            RunOutcome run = Won();
            run.HiddenGems = 2;
            RewardResult first = svc.ApplyRun("lvl_001_15", run);
            Assert.AreEqual(0, first.HiddenGemReward, "no separate gem for the hidden gem pickup");
            Assert.AreEqual(2, first.RewardGems, "level 15 pays its 2 gems on the first clear");
            Assert.AreEqual(0, svc.ApplyRun("lvl_001_15", run).TotalGems, "first clear only");
        }

        [Test]
        public void HiddenGemOnOtherLevelsPaysNoExtraGem()
        {
            var svc = NewService();
            RunOutcome run = Won();
            run.HiddenGems = 1;
            RewardResult first = svc.ApplyRun("lvl_001_01", run);
            Assert.AreEqual(0, first.HiddenGemReward);
            Assert.AreEqual(0, svc.Gems);
        }
    }
}
