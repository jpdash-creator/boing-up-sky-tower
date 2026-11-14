using FluffyClimbers.Content;
using FluffyClimbers.Economy;
using FluffyClimbers.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FluffyClimbers.Tests
{
    /// <summary>
    /// The numbers behind the crown calculation pop-up (owner 2026-11-13): the stored best run (floors, best combo, base score) per level / Endless region and Gameplay setting,
    /// the breakdown built from it, and the rule that the shown sum is exactly the stored crown.
    /// </summary>
    public class CrownBreakdownTests
    {
        ContentRepository repo;
        RegionDef region1;

        [SetUp]
        public void SetUp()
        {
            repo = ContentRepository.LoadFromResources();
            region1 = repo.GetRegionByIndex(1);
        }

        ProgressService NewService() { return new ProgressService(repo, new MemorySaveStore(), 5); }

        /// <summary>The base score of a run with the given floors and combo chain: 10 per floor plus 5 x step x step for every step of the chain.</summary>
        static int BaseOf(int floors, int chain)
        {
            int combo = 0;
            for (int s = 1; s <= chain; s++) combo += s * s * CrownScore.ComboPoints;
            return floors * CrownScore.FloorPoints + combo;
        }

        static RunOutcome Run(int floors, int chain, int mode) { return new RunOutcome { Completed = true, StarMask = 1, Score = BaseOf(floors, chain), Floors = floors, BestCombo = chain, Mode = mode }; }

        [Test]
        public void TheBreakdownSumsToTheStoredCrownInEverySetting()
        {
            for (int mode = 0; mode < TimeRecords.ModeCount; mode++)
            {
                var svc = NewService();
                svc.ApplyRun("lvl_001_01", Run(37, 5, mode));
                CrownBreakdown b = CrownBreakdown.From(svc.BestCrownRun("lvl_001_01", mode), mode);
                Assert.IsTrue(b.HasRun, "mode " + mode);
                Assert.IsTrue(b.PartsKnown, "mode " + mode);
                Assert.AreEqual(37, b.Floors);
                Assert.AreEqual(5, b.BestCombo);
                Assert.AreEqual(370, b.FloorPoints, "floors x 10");
                Assert.AreEqual(BaseOf(37, 5) - 370, b.ComboPoints, "combo points");
                Assert.AreEqual(b.FloorPoints + b.ComboPoints, b.BaseScore, "the two parts are the whole base score");
                Assert.AreEqual(SimConfig.DifficultyStepPercent[mode], b.BonusPercent, "the bonus is the setting percent of the code");
                Assert.AreEqual(svc.BestCrown("lvl_001_01", mode), b.Crown, "the calculated crown is the stored crown");
                Assert.AreEqual(CrownScore.FromScore(b.BaseScore, mode), b.Crown);
            }
        }

        [Test]
        public void ABetterRunReplacesTheRunBehindTheCrownAndAWorseOneDoesNot()
        {
            var svc = NewService();
            svc.ApplyRun("lvl_001_01", Run(30, 3, 2));
            svc.ApplyRun("lvl_001_01", Run(20, 1, 2));   // lower crown: the stored run stays
            CrownRun r = svc.BestCrownRun("lvl_001_01", 2);
            Assert.AreEqual(30, r.Floors);
            Assert.AreEqual(3, r.BestCombo);
            svc.ApplyRun("lvl_001_01", Run(45, 6, 2));
            r = svc.BestCrownRun("lvl_001_01", 2);
            Assert.AreEqual(45, r.Floors);
            Assert.AreEqual(6, r.BestCombo);
            Assert.IsNull(svc.BestCrownRun("lvl_001_01", 3), "no crown in another setting");
        }

        [Test]
        public void NoRunGivesTheFormulaWithPlaceholders()
        {
            var svc = NewService();
            CrownBreakdown b = CrownBreakdown.From(svc.BestCrownRun("lvl_001_01", 3), 3);
            Assert.IsFalse(b.HasRun);
            Assert.AreEqual(0, b.Crown);
            Assert.AreEqual(SimConfig.DifficultyStepPercent[3], b.BonusPercent, "the bonus is shown even without a run");
        }

        [Test]
        public void AnOldSaveShowsNoCrownFromItsModelessScoreAndAStoredCrownWithoutItsRun()
        {
            var data = new SaveData();
            data.Levels["lvl_001_01"] = new LevelProgress { Stars = 1, StarMask = 1, BestScore = 1000, Clears = 1 };   // legacy: no crowns, no run
            data.Levels["lvl_001_02"] = new LevelProgress { Stars = 1, StarMask = 1, BestScore = 1000, Clears = 1, Crowns = new[] { 0, 0, 0, 1400, 0 } };   // a crown without its run (first build of the crown): Hard x1.40 of 1000
            var store = new MemorySaveStore();
            store.Save(data);
            var svc = new ProgressService(repo, store, 5);

            CrownBreakdown legacy = CrownBreakdown.From(svc.BestCrownRun("lvl_001_01", 0), 0, svc.BestCrown("lvl_001_01", 0));
            Assert.IsFalse(legacy.HasRun, "a mode-less old best score is not a crown in any setting");
            Assert.AreEqual(0, legacy.Crown);

            CrownBreakdown noRun = CrownBreakdown.From(svc.BestCrownRun("lvl_001_02", 3), 3, svc.BestCrown("lvl_001_02", 3));
            Assert.IsTrue(noRun.HasRun);
            Assert.IsFalse(noRun.PartsKnown);
            Assert.AreEqual(1400, noRun.Crown, "the stored crown is shown as it is");
            Assert.AreEqual(1000, noRun.BaseScore, "the base score is recovered from the crown");
            Assert.AreEqual(CrownScore.FromScore(noRun.BaseScore, 3), noRun.Crown, "the recovered base score gives exactly that crown");

            // a crown that no base score can make (the bonus skips some values): still shown as stored, base unknown
            data.Levels["lvl_001_03"] = new LevelProgress { Stars = 1, StarMask = 1, Clears = 1, Crowns = new[] { 0, 0, 0, 1500, 0 } };
            store.Save(data);
            svc = new ProgressService(repo, store, 5);
            CrownBreakdown skipped = CrownBreakdown.From(svc.BestCrownRun("lvl_001_03", 3), 3, svc.BestCrown("lvl_001_03", 3));
            Assert.IsTrue(skipped.HasRun);
            Assert.AreEqual(1500, skipped.Crown);
            Assert.AreEqual(0, skipped.BaseScore);
        }

        [Test]
        public void BaseFromCrownFindsAScoreThatMakesTheCrown()
        {
            for (int mode = 0; mode < TimeRecords.ModeCount; mode++)
            {
                for (int score = 1; score <= 400; score++)
                {
                    int crown = CrownScore.FromScore(score, mode);
                    int b = CrownScore.BaseFromCrown(crown, mode);
                    Assert.Greater(b, 0, "crown " + crown + " mode " + mode);
                    Assert.AreEqual(crown, CrownScore.FromScore(b, mode));
                }
            }
        }

        [Test]
        public void ACrownRunSurvivesSaveAndLoadAndAnOldLevelHasNoRunField()
        {
            string json = JsonConvert.SerializeObject(new LevelProgress { BestScore = 5 });
            Assert.IsFalse(json.Contains("crownRuns"), "a level without crown runs writes no field");
            var store = new MemorySaveStore();
            var svc = new ProgressService(repo, store, 5);
            svc.ApplyRun("lvl_001_01", Run(12, 2, 1));
            var again = new ProgressService(repo, store, 5);
            CrownRun r = again.BestCrownRun("lvl_001_01", 1);
            Assert.AreEqual(12, r.Floors);
            Assert.AreEqual(2, r.BestCombo);
            Assert.AreEqual(BaseOf(12, 2), r.BaseScore);
        }

        // ---- Endless ----

        RunRecord EndlessRun(int floors, int chain, int mode)
        {
            return new RunRecord { Kind = RunKind.Endless, RegionId = region1.Id, Floors = floors, Ticks = 3600, Mode = mode, Score = BaseOf(floors, chain), BestCombo = chain };
        }

        [Test]
        public void AnEndlessRunStoresItsCrownPerSettingAndOnlyImproves()
        {
            var svc = NewService();
            RecordResult rr;
            Assert.AreEqual(0, svc.EndlessBestCrown(region1.Id, 3), "a new save has no endless crown");
            svc.FinishEndlessRun(EndlessRun(80, 4, 3), "20261113", out rr);
            int first = CrownScore.FromScore(BaseOf(80, 4), 3);
            Assert.AreEqual(first, svc.EndlessBestCrown(region1.Id, 3));
            Assert.AreEqual(0, svc.EndlessBestCrown(region1.Id, 2), "other settings have their own crown");
            svc.FinishEndlessRun(EndlessRun(50, 2, 3), "20261113", out rr);
            Assert.AreEqual(first, svc.EndlessBestCrown(region1.Id, 3), "a lower run does not replace it");
            svc.FinishEndlessRun(EndlessRun(120, 7, 3), "20261113", out rr);
            CrownRun run = svc.EndlessCrownRun(region1.Id, 3);
            Assert.AreEqual(120, run.Floors);
            Assert.AreEqual(7, run.BestCombo);
            CrownBreakdown b = CrownBreakdown.From(run, 3);
            Assert.AreEqual(svc.EndlessBestCrown(region1.Id, 3), b.Crown);
            Assert.AreEqual(b.BaseScore, b.FloorPoints + b.ComboPoints);
        }

        [Test]
        public void AnEndlessCrownSurvivesReloadAndTimeAttackStoresNone()
        {
            var store = new MemorySaveStore();
            var svc = new ProgressService(repo, store, 5);
            RecordResult rr;
            svc.FinishEndlessRun(EndlessRun(60, 3, 1), "20261113", out rr);
            var ta = new RunRecord { Kind = RunKind.TimeAttack, RegionId = region1.Id, Floors = 100, Ticks = TimeRecords.TimeAttackTicks, Mode = 1, Score = 5000, BestCombo = 9 };
            svc.FinishEndlessRun(ta, "20261113", out rr);
            var again = new ProgressService(repo, store, 5);
            Assert.AreEqual(CrownScore.FromScore(BaseOf(60, 3), 1), again.EndlessBestCrown(region1.Id, 1), "Time Attack has no crown and never replaces the endless one");
        }

        [Test]
        public void AnOldSaveWithoutEndlessCrownsLoadsWithZero()
        {
            SaveData d = JsonConvert.DeserializeObject<SaveData>("{\"version\":3,\"coins\":5,\"gems\":1}");
            Assert.IsNotNull(d.EndlessCrowns);
            var store = new MemorySaveStore();
            d.EndlessCrowns = null;   // even an explicit null is repaired
            store.Save(d);
            var svc = new ProgressService(repo, store, 5);
            Assert.AreEqual(0, svc.EndlessBestCrown(region1.Id, 0));
            Assert.IsNull(svc.EndlessCrownRun(region1.Id, 0));
        }
    }
}
