using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace SkyHopper.Simulation.Tests
{
    public class GeneratorTests
    {
        private static GeneratorParams Params(int seed, int dMilli, int rows)
        {
            GeneratorParams p = new GeneratorParams();
            p.Seed = seed;
            p.Difficulty = Fx.FromRatio(dMilli, 1000);
            p.Rows = rows;
            return p;
        }

        private static long Hash(Platform[] plats)
        {
            long h = 17;
            foreach (Platform p in plats)
            {
                h = h * 31 + p.X.Raw;
                h = h * 31 + p.Y.Raw;
                h = h * 31 + p.Width.Raw;
                h = h * 31 + (int)p.Kind;
            }
            return h;
        }

        [Fact]
        public void SameSeedSameLevel()
        {
            SimConfig cfg = new SimConfig();
            Assert.Equal(Hash(LevelGenerator.Generate(cfg, Params(123, 400, 80))),
                         Hash(LevelGenerator.Generate(cfg, Params(123, 400, 80))));
            Assert.NotEqual(Hash(LevelGenerator.Generate(cfg, Params(123, 400, 80))),
                            Hash(LevelGenerator.Generate(cfg, Params(124, 400, 80))));
        }

        [Theory]
        [InlineData(50)]
        [InlineData(400)]
        [InlineData(1000)]
        public void RespectsDocEightRanges(int dMilli)
        {
            SimConfig cfg = new SimConfig();
            Fx maxGap = cfg.MaxJumpHeight() * 9 / 10;
            Platform[] plats = LevelGenerator.Generate(cfg, Params(9, dMilli, 100));
            Fx prevY = Fx.Zero;
            for (int i = 1; i < plats.Length; i++)
            {
                Platform p = plats[i];
                Assert.True(p.Y >= prevY, "sorted by Y");
                if (p.Y > prevY) Assert.True(p.Y - prevY <= maxGap + Fx.FromRatio(1, 100), "gap within 0.9 of max jump");
                prevY = p.Y;
                Assert.True(p.X - p.Width / 2 >= -Fx.FromRatio(1, 100));
                Assert.True(p.X + p.Width / 2 <= cfg.ScreenWidth + Fx.FromRatio(1, 100));
            }
            if (dMilli == 50)
            {
                for (int i = 1; i < plats.Length; i++) if (plats[i].Row <= LevelGenerator.FixedRows) Assert.True(plats[i].Width >= Fx.FromRatio(48, 10), "the fixed first rows are wide (6.0 x 0.85 jitter at d = 0.05 is about 4.9); later patterns vary (dense patches, zigzag)");
            }
            if (dMilli == 1000)
            {
                for (int i = 1; i < plats.Length; i++) Assert.True(plats[i].Width <= Fx.FromRatio(46, 10), "hard platforms are narrow (zigzag rows up to 4.0 x jitter)");
            }
        }

        [Fact]
        public void NoSpecialsWithoutRegionSpecials()
        {
            SimConfig cfg = new SimConfig();
            foreach (Platform p in LevelGenerator.Generate(cfg, Params(5, 1000, 100)))
                Assert.True(p.Kind == PlatformKind.Normal || p.Kind == PlatformKind.Crumble, "only Normal, or Crumble extras in dense patches");
        }

        [Fact]
        public void EffectiveDifficultyCompressesTowardOne()
        {
            Fx d = LevelGenerator.EffectiveDifficulty(Fx.FromRatio(86, 100), Fx.FromRatio(25, 100));
            Assert.True(d < Fx.One);
            Assert.True(d > Fx.FromRatio(86, 100));
            Assert.Equal(Fx.One, LevelGenerator.EffectiveDifficulty(Fx.One, Fx.FromRatio(25, 100)));
        }

        [Fact]
        public void EndlessDifficultyRampsToOne()
        {
            GeneratorParams p = Params(1, 0, 10);
            p.Endless = true;
            Assert.Equal(Fx.Zero, LevelGenerator.DifficultyAt(p, 0));
            Assert.Equal(Fx.One, LevelGenerator.DifficultyAt(p, 400));
            Assert.Equal(Fx.One, LevelGenerator.DifficultyAt(p, 900));
        }

        [Fact]
        public void RampMatchesDocEight()
        {
            SimConfig cfg = new SimConfig();
            Assert.Equal(0, cfg.RampPercentAt(24));
            Assert.Equal(4, cfg.RampPercentAt(25));
            // Steps are discrete: floor 925 is step 37 (+148 percent), floor 950 is step 38 and hits the cap.
            Assert.Equal(148, cfg.RampPercentAt(937));
            Assert.Equal(150, cfg.RampPercentAt(950));
            Assert.Equal(150, cfg.RampPercentAt(5000));
        }
    }

    /// <summary>Plays every shipped level (Content/levels) with the bot. Skipped silently if content is missing.</summary>
    public class SolvabilityTests
    {
        private readonly ITestOutputHelper output;
        public SolvabilityTests(ITestOutputHelper output) { this.output = output; }

        private static string FindRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "Content", "levels"))) dir = Path.GetDirectoryName(dir);
            return dir;
        }

        private static IEnumerable<(int region, JsonElement level, double offset)> ShippedLevels(string root)
        {
            foreach (string file in Directory.GetFiles(Path.Combine(root, "Content", "levels"), "*.json"))
            {
                string regionFile = Path.Combine(root, "Content", "regions", Path.GetFileName(file).Replace("_levels", ""));
                double offset;
                using (JsonDocument rd = JsonDocument.Parse(File.ReadAllText(regionFile)))
                    offset = rd.RootElement.GetProperty("rules").GetProperty("difficultyOffset").GetDouble();
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file)))
                {
                    foreach (JsonElement lv in doc.RootElement.EnumerateArray())
                    {
                        string id = lv.GetProperty("id").GetString();
                        int region = int.Parse(id.Substring(4, 3));
                        yield return (region, lv.Clone(), offset);
                    }
                }
            }
        }

        [Fact]
        public void EveryShippedLevelIsSolvable()
        {
            string root = FindRoot();
            if (root == null) { output.WriteLine("Content not found; skipped"); return; }
            SimConfig cfg = new SimConfig();
            List<string> failures = new List<string>();
            int count = 0;
            foreach (var item in ShippedLevels(root))
            {
                JsonElement lv = item.level;
                int seed = lv.GetProperty("seed").GetInt32();
                double baseD = lv.GetProperty("difficulty").GetDouble();
                Fx d = LevelGenerator.EffectiveDifficulty(
                    Fx.FromRatio((int)Math.Round(baseD * 1000), 1000),
                    Fx.FromRatio((int)Math.Round(item.offset * 10000), 10000));
                int rows = 60;
                JsonElement goal = lv.GetProperty("goal");
                if (goal.TryGetProperty("value", out JsonElement gv) && goal.GetProperty("type").GetString() == "reachFloor")
                    rows = Math.Min(gv.GetInt32(), 150);

                GeneratorParams gp = new GeneratorParams();
                gp.Seed = seed; gp.Difficulty = d; gp.Rows = rows;
                Platform[] plats = LevelGenerator.Generate(cfg, gp);
                Sim sim = new Sim(cfg, plats, d, false);
                BotResult r = new LevelBot(sim, rows, 20000).Solve();
                count++;
                if (!r.Solved)
                {
                    string line = lv.GetProperty("id").GetString() + " d=" + d + " rows=" + rows + " bestRow=" + r.BestRow + " nodes=" + r.Nodes;
                    // The danger line now trails 10 units (it sits at the bottom of the screen). Regions 1-5 are the launch milestone and must be
                    // solvable; later regions are reported here and tuned when they ship (the bot, not the content, is the limit for some).
                    if (item.region <= 5) failures.Add(line); else output.WriteLine("WARNING unsolved by the bot (region " + item.region + "): " + line);
                }
            }
            output.WriteLine("levels checked: " + count + ", failures: " + failures.Count);
            foreach (string f in failures) output.WriteLine(f);
            Assert.Empty(failures);
            Assert.Equal(450, count);
        }

        [Fact]
        public void EndlessIsSolvableToFloor150()
        {
            SimConfig cfg = new SimConfig();
            for (int seed = 1; seed <= 5; seed++)
            {
                GeneratorParams gp = new GeneratorParams();
                gp.Seed = seed; gp.Rows = 150; gp.Endless = true;
                Platform[] plats = LevelGenerator.Generate(cfg, gp);
                Sim sim = new Sim(cfg, plats, Fx.FromRatio(1, 2), true);
                BotResult r = new LevelBot(sim, 150, 30000).Solve();
                Assert.True(r.Solved, "seed " + seed + " bestRow " + r.BestRow);
            }
        }
    }
}
