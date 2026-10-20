using System;
using BoingUpSkyTower.Content;
using BoingUpSkyTower.Economy;
using BoingUpSkyTower.Gameplay;
using BoingUpSkyTower.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BoingUpSkyTower.Tests
{
    /// <summary>Endless mode (T13): coin formula, daily cap, best floor save, old saves, and the endless RunSession.</summary>
    public class EndlessTests
    {
        ContentRepository repo;
        RegionDef region1;
        const string Day1 = "20261020", Day2 = "20261021";

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            region1 = repo.GetRegionByIndex(1);
            ProgressService.TestUnlockAll = false;
        }

        /// <summary>A store that hands out a save whose endless fields are null (as a broken or hand-edited file could).</summary>
        sealed class NullFieldsStore : ISaveStore
        {
            public SaveData Load() { return new SaveData { EndlessBest = null, EndlessDay = null }; }
            public void Save(SaveData data) { }
        }

        // ---- formula ----

        [Test]
        public void CoinFormulaIsOneCoinPerTwoFloorsPlusFiftyPerHundred()
        {
            Assert.AreEqual(0, EndlessRewards.RawCoins(0));
            Assert.AreEqual(0, EndlessRewards.RawCoins(1));
            Assert.AreEqual(1, EndlessRewards.RawCoins(2));
            Assert.AreEqual(49, EndlessRewards.RawCoins(99));
            Assert.AreEqual(100, EndlessRewards.RawCoins(100));        // 50 + 50 bonus
            Assert.AreEqual(225, EndlessRewards.RawCoins(250));        // 125 + 2 x 50
            Assert.AreEqual(300, EndlessRewards.RawCoins(300));        // 150 + 3 x 50
        }

        [Test]
        public void RegionFactorScalesTheCoinsAndRoundsHalfUp()
        {
            Assert.AreEqual(100, EndlessRewards.FullRateCoins(100, 1.0));
            Assert.AreEqual(150, EndlessRewards.FullRateCoins(100, 1.5));
            Assert.AreEqual(1, EndlessRewards.FullRateCoins(2, 1.03));   // 1.03 rounds to 1
            Assert.AreEqual(5, EndlessRewards.OverCapCoins(10, 0.5));
            Assert.AreEqual(3, EndlessRewards.OverCapCoins(5, 0.5));     // 2.5 rounds up
        }

        // ---- daily cap ----

        [Test]
        public void FirstThreeRunsOfTheDayPayInFullThenHalf()
        {
            var svc = new ProgressService(repo, new MemorySaveStore(), 5);
            int full = EndlessRewards.FullRateCoins(100, region1.CoinFactor);
            Assert.Greater(full, 0);
            int cap = repo.RemoteConfig.Economy.EndlessCoinDailyFullRateRuns;
            Assert.AreEqual(3, cap);
            for (int i = 1; i <= cap; i++)
            {
                EndlessReward r = svc.ApplyEndlessRun(region1.Id, 100, Day1);
                Assert.AreEqual(full, r.Coins, "run " + i);
                Assert.IsFalse(r.OverCap);
                Assert.AreEqual(i, r.RunsToday);
            }
            EndlessReward over = svc.ApplyEndlessRun(region1.Id, 100, Day1);
            Assert.IsTrue(over.OverCap);
            Assert.AreEqual(EndlessRewards.OverCapCoins(full, repo.RemoteConfig.Economy.EndlessCoinOverCapMultiplier), over.Coins);
            Assert.AreEqual(full, over.FullRateCoins);
            Assert.AreEqual(cap + 1, svc.EndlessRunsOn(Day1));
            Assert.AreEqual(full * cap + over.Coins, svc.Coins);
        }

        [Test]
        public void TheCounterResetsOnANewUtcDay()
        {
            var svc = new ProgressService(repo, new MemorySaveStore(), 5);
            for (int i = 0; i < 5; i++) svc.ApplyEndlessRun(region1.Id, 100, Day1);
            Assert.AreEqual(0, svc.EndlessRunsOn(Day2));
            EndlessReward next = svc.ApplyEndlessRun(region1.Id, 100, Day2);
            Assert.IsFalse(next.OverCap);
            Assert.AreEqual(1, next.RunsToday);
        }

        [Test]
        public void ARunWithoutCoinsDoesNotUseUpAFullRateRun()
        {
            var svc = new ProgressService(repo, new MemorySaveStore(), 5);
            EndlessReward tiny = svc.ApplyEndlessRun(region1.Id, 1, Day1);   // 1 floor = 0 coins
            Assert.AreEqual(0, tiny.Coins);
            Assert.AreEqual(0, svc.EndlessRunsOn(Day1));
            Assert.AreEqual(0, svc.Coins);
            Assert.AreEqual(1, svc.EndlessBestFloor(region1.Id));
        }

        [Test]
        public void LockedRegionCannotBePlayedEndless()
        {
            var svc = new ProgressService(repo, new MemorySaveStore(), 5);
            RegionDef r2 = repo.GetRegionByIndex(2);
            Assert.Throws<InvalidOperationException>(() => svc.ApplyEndlessRun(r2.Id, 50, Day1));
        }

        // ---- best floor and save ----

        [Test]
        public void BestFloorIsKeptPerRegionAndOnlyGrows()
        {
            var svc = new ProgressService(repo, new MemorySaveStore(), 5);
            Assert.AreEqual(0, svc.EndlessBestFloor(region1.Id));
            EndlessReward a = svc.ApplyEndlessRun(region1.Id, 40, Day1);
            Assert.IsTrue(a.NewBest);
            Assert.AreEqual(0, a.BestBefore);
            EndlessReward b = svc.ApplyEndlessRun(region1.Id, 25, Day1);
            Assert.IsFalse(b.NewBest);
            Assert.AreEqual(40, b.BestBefore);
            Assert.AreEqual(40, svc.EndlessBestFloor(region1.Id));
            EndlessReward c = svc.ApplyEndlessRun(region1.Id, 41, Day1);
            Assert.IsTrue(c.NewBest);
            Assert.AreEqual(41, svc.EndlessBestFloor(region1.Id));
        }

        [Test]
        public void EndlessResultsSurviveASaveRoundTrip()
        {
            var store = new MemorySaveStore();
            var svc = new ProgressService(repo, store, 5);
            svc.ApplyEndlessRun(region1.Id, 120, Day1);
            svc.ApplyEndlessRun(region1.Id, 30, Day1);
            int coins = svc.Coins;

            var again = new ProgressService(repo, store, 5);   // reload from the saved json
            Assert.AreEqual(120, again.EndlessBestFloor(region1.Id));
            Assert.AreEqual(2, again.EndlessRunsOn(Day1));
            Assert.AreEqual(coins, again.Coins);
        }

        [Test]
        public void OldSavesWithoutEndlessFieldsLoadWithSafeDefaults()
        {
            string old = "{\"version\":1,\"coins\":55,\"gems\":3,\"levels\":{\"lvl_001_01\":{\"stars\":1,\"starMask\":1,\"bestScore\":10,\"clears\":1}},\"unlockedRegions\":[]}";
            SaveData data = JsonConvert.DeserializeObject<SaveData>(old);
            Assert.IsNotNull(data.EndlessBest);
            Assert.AreEqual(0, data.EndlessBest.Count);
            Assert.AreEqual("", data.EndlessDay);
            Assert.AreEqual(0, data.EndlessRunsToday);
            Assert.AreEqual(55, data.Coins);
        }

        [Test]
        public void NullEndlessFieldsAreRepairedOnLoad()
        {
            var svc = new ProgressService(repo, new NullFieldsStore(), 5);
            Assert.AreEqual(0, svc.EndlessBestFloor(region1.Id));
            Assert.AreEqual(0, svc.EndlessRunsOn(Day1));
            EndlessReward r = svc.ApplyEndlessRun(region1.Id, 10, Day1);
            Assert.IsTrue(r.NewBest);
        }

        // ---- the endless RunSession ----

        [Test]
        public void EndlessSessionIsDeterministicPerSeedAndHasNoGoalOrRevive()
        {
            var cfg = new SimConfig();
            var a = new RunSession(repo, cfg, region1, 4711);
            var b = new RunSession(repo, cfg, region1, 4711);
            var c = new RunSession(repo, cfg, region1, 4712);
            Assert.IsTrue(a.IsEndless);
            Assert.IsNull(a.Level);
            Assert.AreEqual(4711, a.Seed);
            Assert.AreEqual(a.Data.ComputeHash(), b.Data.ComputeHash());
            Assert.AreNotEqual(a.Data.ComputeHash(), c.Data.ComputeHash());
            Assert.IsTrue(a.Spec.Endless);
            Assert.IsFalse(a.CanRevive);
            for (int i = 0; i < 600; i++) a.Step(SimInput.Jump);
            Assert.IsFalse(a.GoalReached);
        }

        [Test]
        public void EndlessTowerGrowsAheadOfThePlayerAndKeepsTheState()
        {
            var cfg = new SimConfig();
            var s = new RunSession(repo, cfg, region1, 99);
            int version = s.WorldVersion;
            int before = s.Data.Platforms.Length;
            s.Step(0);
            Assert.AreEqual(version, s.WorldVersion);   // the player is at the bottom: the 100 rows built at the start are enough
            s.State.Row = 80;                           // pretend the player climbed (tower must reach row 140)
            s.Step(0);
            Assert.AreNotEqual(version, s.WorldVersion);
            Assert.Greater(s.Data.Platforms.Length, before);
            Assert.GreaterOrEqual(s.Tower.TopRow, 140);
            Assert.AreEqual(s.Data.Platforms.Length, s.State.CrumbleTick.Length);
            Assert.AreSame(s.Data, s.Tower.Data);
        }

        [Test]
        public void EndlessSpecUsesTheRegionOffsetAndSignature()
        {
            RegionDef r5 = repo.GetRegionByIndex(5);
            LevelSpec spec = LevelSpecFactory.BuildEndless(r5, 7, repo.RemoteConfig.Generator.EndlessDifficultyFloors);
            Assert.IsTrue(spec.Endless);
            Assert.AreEqual(SignatureKind.SwingingGongs, spec.Signature);
            Assert.AreEqual(400, spec.EndlessDifficultyFloors);
            Assert.Greater(spec.RegionOffset.Raw, 0);
        }
    }
}
