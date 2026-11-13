using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>The crown per level and Gameplay setting (owner 2026-11-12): stored per mode on a run, old saves keep their legacy best score as a Relaxed crown.</summary>
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
        public void AnOldSaveShowsItsBestScoreAsARelaxedCrownOnly()
        {
            var data = new SaveData();
            data.Levels["lvl_001_01"] = new LevelProgress { Stars = 1, StarMask = 1, BestScore = 1000, Clears = 1 };
            var store = new MemorySaveStore();
            store.Save(data);
            var svc = new ProgressService(repo, store, 5);
            Assert.AreEqual(1100, svc.BestCrown("lvl_001_01", 0), "1000 x 1.10");
            Assert.AreEqual(0, svc.BestCrown("lvl_001_01", 4), "an old score is not credited to a harder setting");
            Assert.AreEqual(0, svc.BestCrown("lvl_001_02", 0));
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
