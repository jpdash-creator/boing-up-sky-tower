using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>The crown per level and Gameplay setting (owner 2026-11-12): stored per mode on a run, a setting that was never played has crown 0 (the mode-less legacy best score is never used).</summary>
    public class CrownEditTests
    {
        ContentRepository repo;

        [SetUp]
        public void SetUp() { repo = ContentRepository.LoadFromResources(); }

        ProgressService NewService() { return new ProgressService(repo, new MemorySaveStore(), 5); }

        static RunOutcome Run(int score, int mode) { return new RunOutcome { Completed = true, StarMask = 1, Score = score, Mode = mode }; }

        [Test]
        public void ARunStoresTheCrownOfItsSettingAndKeepsTheBestPerSetting()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Run(1000, 4));   // Insane x1.50
            Assert.AreEqual(1500, svc.BestCrown("lvl_001_01", 4));
            Assert.AreEqual(0, svc.BestCrown("lvl_001_01", 3), "other settings have no crown yet");
            svc.ApplyRun("lvl_001_01", Run(800, 4));    // lower: unchanged
            Assert.AreEqual(1500, svc.BestCrown("lvl_001_01", 4));
            svc.ApplyRun("lvl_001_01", Run(1000, 1));   // Easy x1.20
            Assert.AreEqual(1200, svc.BestCrown("lvl_001_01", 1));
            Assert.AreEqual(1000, svc.GetProgress("lvl_001_01").BestScore, "the legacy base best score is still kept");
        }

        [Test]
        public void AnOldBestScoreIsNeverShownAsACrownInAnySetting()
        {
            var data = new SaveData();
            data.Levels["lvl_001_01"] = new LevelProgress { Stars = 1, StarMask = 1, BestScore = 1000, Clears = 1 };
            var store = new MemorySaveStore();
            store.Save(data);
            var svc = new ProgressService(repo, store, 5);
            for (int mode = 0; mode < TimeRecords.ModeCount; mode++) Assert.AreEqual(0, svc.BestCrown("lvl_001_01", mode), "mode " + mode);
        }

        [Test]
        public void ARunInOneSettingNeverCreatesACrownInAnother()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Run(1945, 4));   // Insane only
            Assert.AreEqual(2918, svc.BestCrown("lvl_001_01", 4));
            for (int mode = 0; mode < 4; mode++) Assert.AreEqual(0, svc.BestCrown("lvl_001_01", mode), "mode " + mode + " was not played");
            Assert.IsNull(svc.BestCrownRun("lvl_001_01", 0));
            svc.ApplyRun("lvl_001_01", Run(1020, 0));   // then Relaxed with a lower base
            Assert.AreEqual(1122, svc.BestCrown("lvl_001_01", 0));
            Assert.AreEqual(2918, svc.BestCrown("lvl_001_01", 4));
        }

        [Test]
        public void CrownsSurviveSaveAndLoadAndAnOldSaveHasNoCrownsField()
        {
            string json = JsonConvert.SerializeObject(new LevelProgress { BestScore = 5 }, new JsonSerializerSettings());
            Assert.IsFalse(json.Contains("crowns"), "a level without crowns writes no field");
            LevelProgress p = JsonConvert.DeserializeObject<LevelProgress>("{\"stars\":1,\"starMask\":1,\"bestScore\":7,\"clears\":1}");
            Assert.IsNull(p.Crowns);
            Assert.AreEqual(7, p.BestScore);
        }
    }
}
