using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoingUpSkyTower.Economy;
using Xunit;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// The medal target data (doc 46 section 6): the loader (FileMedalTargets), the written file Content/config/medal_targets.json and its Resources copy.
    /// Read-only: only MedalCalibrationTests.Calibrate (MEDAL_CAL=1) writes the file.
    /// </summary>
    public class MedalTargetFileTests
    {
        static string FilePath(string root) { return Path.Combine(root, "Content", "config", "medal_targets.json"); }

        [Fact]
        public void TheLoaderReadsTargetsPerLevelAndModeAndAnswersZeroForUnknownThings()
        {
            string json = "{ \"version\": 1, \"percents\": [55, 70, 85], \"targets\": { \"lvl_001_01\": [2684, 2680, 2600, 2500, 2400], \"lvl_001_02\": [0, 1500] } }";
            FileMedalTargets t = FileMedalTargets.FromJson(json);
            Assert.Equal(2, t.LevelCount);
            Assert.Equal(2684, t.TargetPaceMilli("lvl_001_01", 0));
            Assert.Equal(2400, t.TargetPaceMilli("lvl_001_01", 4));
            Assert.Equal(1500, t.TargetPaceMilli("lvl_001_02", 1));
            Assert.Equal(0, t.TargetPaceMilli("lvl_001_02", 0));    // 0 = no data
            Assert.Equal(0, t.TargetPaceMilli("lvl_001_02", 3));    // the row is shorter than five entries
            Assert.Equal(0, t.TargetPaceMilli("lvl_009_09", 0));    // unknown level
            Assert.Equal(0, t.TargetPaceMilli("lvl_001_01", 5));    // invalid mode
            Assert.Equal(0, t.TargetPaceMilli("lvl_001_01", -1));
            Assert.Equal(0, t.TargetPaceMilli(null, 0));
            Assert.True(t.Has("lvl_001_01"));
            Assert.False(t.Has("nope"));
        }

        [Fact]
        public void ABrokenFileIsNoticedNotIgnored()
        {
            Assert.Throws<FormatException>(() => FileMedalTargets.FromJson("{ \"version\": 2, \"targets\": {} }"));
            Assert.Throws<FormatException>(() => FileMedalTargets.FromJson("{ \"version\": 1 }"));
            Assert.Throws<FormatException>(() => FileMedalTargets.FromJson("{ \"version\": 1, \"targets\": { \"a\": 5 } }"));
        }

        [Fact]
        public void WritingThenReadingGivesTheSameTableInSortedOrder()
        {
            var table = new Dictionary<string, int[]>
            {
                { "lvl_002_01", new[] { 1, 2, 3, 4, 5 } },
                { "lvl_001_02", new[] { 10, 0, 30, 40, 50 } },
            };
            string text = FileMedalTargets.ToJson(table, 5, "a \"quoted\" note");
            Assert.True(text.IndexOf("lvl_001_02", StringComparison.Ordinal) < text.IndexOf("lvl_002_01", StringComparison.Ordinal), "sorted by id");
            Assert.EndsWith("\n", text);
            Assert.DoesNotContain("\r", text);
            FileMedalTargets back = FileMedalTargets.FromJson(text);
            Assert.Equal(2, back.LevelCount);
            foreach (var kv in table)
                for (int m = 0; m < 5; m++) Assert.Equal(kv.Value[m], back.TargetPaceMilli(kv.Key, m));
            Assert.Equal(text, FileMedalTargets.ToJson(table, 5, "a \"quoted\" note"));   // deterministic
        }

        [Fact]
        public void TheThreeMedalLinesAreFiftyFiveSeventyAndEightyFivePercentOfTheTarget()
        {
            Assert.Equal(new[] { 55, 70, 85 }, TimeRecords.MedalPercents);
            var table = new DictionaryMedalTargets();
            table.Set("lvl_001_01", 2, 1000);   // 1.000 plat/s reference
            FileMedalTargets file = FileMedalTargets.FromJson("{ \"version\": 1, \"targets\": { \"lvl_001_01\": [0, 0, 1000, 0, 0] } }");
            Assert.Equal(table.TargetPaceMilli("lvl_001_01", 2), file.TargetPaceMilli("lvl_001_01", 2));
            int target = file.TargetPaceMilli("lvl_001_01", 2);
            // pace = floors / (ticks / 60): 3600 ticks = 60 s, so floors / 60 plat/s
            Assert.Equal(MedalLevel.None, TimeRecords.MedalFor(new PaceValue(32, 3600), target));     // 0.533
            Assert.Equal(MedalLevel.Bronze, TimeRecords.MedalFor(new PaceValue(33, 3600), target));   // 0.550 exactly on the line
            Assert.Equal(MedalLevel.Bronze, TimeRecords.MedalFor(new PaceValue(41, 3600), target));   // 0.683
            Assert.Equal(MedalLevel.Silver, TimeRecords.MedalFor(new PaceValue(42, 3600), target));   // 0.700 exactly
            Assert.Equal(MedalLevel.Silver, TimeRecords.MedalFor(new PaceValue(50, 3600), target));   // 0.833
            Assert.Equal(MedalLevel.Gold, TimeRecords.MedalFor(new PaceValue(51, 3600), target));     // 0.850 exactly
            Assert.Equal(MedalLevel.Gold, TimeRecords.MedalFor(new PaceValue(120, 3600), target));
            Assert.Equal(MedalLevel.None, TimeRecords.MedalFor(new PaceValue(120, 3600), 0));         // no data, no medal
        }

        [Fact]
        public void TheShippedTargetFileCoversEveryLevelAndAllFiveModes()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            string path = FilePath(root);
            Assert.True(File.Exists(path), "Content/config/medal_targets.json is missing: run the calibration (MEDAL_CAL=1 dotnet test Tests/Simulation --filter MedalCalibrationTests.Calibrate)");
            string text = File.ReadAllText(path);
            FileMedalTargets t = FileMedalTargets.FromJson(text);
            List<LoadedLevel> levels = ContentLoader.LoadRegions(root, 1, 5);
            Assert.Equal(125, levels.Count);
            Assert.Equal(125, t.LevelCount);
            foreach (LoadedLevel l in levels)
            {
                Assert.True(t.Has(l.Id), l.Id + " has no target row");
                for (int m = 0; m < TimeRecords.ModeCount; m++)
                {
                    int v = t.TargetPaceMilli(l.Id, m);
                    Assert.True(v > 0, l.Id + " mode " + m + ": the bot solved no layout, so nobody could earn a medal here");
                    Assert.InRange(v, 300, 4000);   // 0.3 to 4 plat/s: a sane band for a bot on 60 to 100 rows
                }
            }
            // the file records which medal table it was calibrated for
            Newtonsoft.Json.Linq.JObject o = Newtonsoft.Json.Linq.JObject.Parse(text);
            Assert.Equal(TimeRecords.MedalPercents.ToList(), o["percents"].Select(x => (int)x).ToList());
        }

        [Fact]
        public void TheResourcesCopyEqualsTheContentFile()
        {
            string root = ContentLoader.FindRoot();
            if (root == null) return;
            string content = FilePath(root);
            string res = Path.Combine(root, "Assets", "Resources", "Content", "medal_targets.json");
            if (!File.Exists(content)) return;   // the coverage test reports the missing file
            Assert.True(File.Exists(res), "Assets/Resources/Content/medal_targets.json is missing (copy of Content/config/medal_targets.json)");
            Assert.Equal(File.ReadAllText(content), File.ReadAllText(res));
        }

        [Fact]
        public void TheShippedValuesStillMatchTheBotForOneLevel()
        {
            // guards a stale file: when the generator, physics or bot change, level 1 of region 1 moves first and this fails until the calibration is rerun
            string root = ContentLoader.FindRoot();
            if (root == null || !File.Exists(FilePath(root))) return;
            FileMedalTargets t = FileMedalTargets.FromJson(File.ReadAllText(FilePath(root)));
            LoadedLevel l = ContentLoader.LoadRegions(root, 1, 1).First(x => x.Spec.LevelIndex == 1);
            for (int m = 0; m < TimeRecords.ModeCount; m += 2)
            {
                var samples = new List<MedalCalibrationTests.PaceSample>();
                for (int k = 0; k < MedalCalibrationTests.Layouts; k++) samples.Add(MedalCalibrationTests.Measure(l, m, k, MedalCalibrationTests.FullBudget));
                Assert.Equal(t.TargetPaceMilli(l.Id, m), MedalCalibrationTests.MeanPaceMilli(samples));
            }
        }
    }
}
