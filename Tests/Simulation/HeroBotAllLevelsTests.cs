using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BoingUpSkyTower.Economy;
using Xunit;
using Xunit.Abstractions;

namespace BoingUpSkyTower.Simulation.Tests
{
    /// <summary>
    /// The end-to-end proof of the rebuilt roster: EVERY hero (25 lines x 5 tiers, Common to Legendary) is played by the solvability bot on EVERY shipped level (5 regions x 15 levels = 75),
    /// each with its own physics exactly as GameSession.BuildRunCfg applies them (the hero's CharacterStats on a clone of the default SimConfig). The bot must win the 1-star goal.
    /// Proves ALL 125 hero rows through the data accessor (CharacterDefs.AllIncludingInactive: the 100 inactive heroes never exist at runtime but their rows stay proven as data; HERO_BOT_HEROES restricts).
    /// Writes Tests/Simulation/HERO_BOT_REPORT.csv (one row per hero and level) and HERO_BOT_SUMMARY.md. Set HERO_BOT=1 (optional HERO_BOT_HEROES=id1,id2 to restrict).
    /// </summary>
    [Trait("Category", "HeroBot")]
    public class HeroBotAllLevelsTests
    {
        private readonly ITestOutputHelper output;
        public HeroBotAllLevelsTests(ITestOutputHelper output) { this.output = output; }

        sealed class Row { public string Hero, Level; public int Region, Index; public bool Solved; public int Nodes, Ticks, BestRow; }

        [Fact]
        public void EveryHeroSolvesEveryLevel()
        {
            if (Environment.GetEnvironmentVariable("HERO_BOT") != "1") return;
            string root = ContentLoader.FindRoot();
            string only = Environment.GetEnvironmentVariable("HERO_BOT_HEROES");
            CharacterSpec[] heroes = CharacterDefs.AllIncludingInactive.Where(h => string.IsNullOrEmpty(only) || only.Split(',').Contains(h.Id)).ToArray();
            List<LoadedLevel> levels = ContentLoader.LoadRegions(root, 1, 5);
            string onlyLevels = Environment.GetEnvironmentVariable("HERO_BOT_LEVELS");   // optional: restrict to some level ids, e.g. lvl_005_10
            if (!string.IsNullOrEmpty(onlyLevels)) levels = levels.Where(l => onlyLevels.Split(',').Contains(l.Id)).ToList();
            SimConfig baseCfg = new SimConfig();
            var rows = new ConcurrentBag<Row>();
            string progress = Path.Combine(root, "Tests", "Simulation", "HERO_BOT_PROGRESS.txt");
            File.WriteAllText(progress, "start " + DateTime.Now.ToString("s") + " heroes " + heroes.Length + " levels " + levels.Count + "\n");
            int envBudget = int.Parse(Environment.GetEnvironmentVariable("HERO_BOT_BUDGET") ?? "0");   // node budget per solve (0 = the shipped-level budget of the solvability tests)
            int total = heroes.Length * levels.Count, done = 0;
            var sw = Stopwatch.StartNew();

            // one unit of work per (level, hero); the level is built once per level (the builder ignores the hero)
            var work = new List<KeyValuePair<int, int>>(total);
            for (int li = 0; li < levels.Count; li++) for (int hi = 0; hi < heroes.Length; hi++) work.Add(new KeyValuePair<int, int>(li, hi));
            var built = new ConcurrentDictionary<int, LevelData>();
            Parallel.ForEach(work, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, w =>
            {
                LoadedLevel ll = levels[w.Key];
                CharacterSpec hero = heroes[w.Value];
                LevelData data = built.GetOrAdd(w.Key, k => LevelBuilder.Build(baseCfg, ll.Spec));
                SimConfig run = baseCfg.Clone();
                (hero.Stats ?? CharacterStats.Default).ApplyTo(run);   // exactly what GameSession.BuildRunCfg does
                Sim sim = new Sim(run, data, ll.Spec);
                int budget = envBudget > 0 ? envBudget : SolvabilityT4Tests.NodeBudget * Math.Max(1, ll.Spec.RowsFor() / 60);
                BotResult br = new LevelBot(sim, ll.Spec, data, budget).Solve();
                rows.Add(new Row { Hero = hero.Id, Level = ll.Id, Region = ll.Region, Index = ll.Spec.LevelIndex, Solved = br.Solved, Nodes = br.Nodes, Ticks = br.Ticks, BestRow = br.BestRow });
                int n = Interlocked.Increment(ref done);
                if (n % 250 == 0 || n == total) File.AppendAllText(progress, (n == total ? "FINISHED " : "") + n + "/" + total + " solved so far " + rows.Count(r => r.Solved) + " elapsed " + sw.Elapsed.TotalMinutes.ToString("F1") + " min\n");
            });

            Row[] all = rows.OrderBy(r => r.Hero, StringComparer.Ordinal).ThenBy(r => r.Region).ThenBy(r => r.Index).ToArray();
            var csv = new StringBuilder("hero,level,region,index,solved,nodes,ticks,bestRow\n");
            foreach (Row r in all) csv.Append(r.Hero).Append(',').Append(r.Level).Append(',').Append(r.Region).Append(',').Append(r.Index).Append(',').Append(r.Solved ? 1 : 0).Append(',').Append(r.Nodes).Append(',').Append(r.Ticks).Append(',').Append(r.BestRow).Append('\n');
            File.WriteAllText(Path.Combine(root, "Tests", "Simulation", (string.IsNullOrEmpty(onlyLevels) ? "HERO_BOT_REPORT.csv" : "HERO_BOT_RERUN.csv")), csv.ToString());

            Row[] failed = all.Where(r => !r.Solved).ToArray();
            var md = new StringBuilder();
            md.AppendLine("# Hero bot proof: every hero on every level");
            md.AppendLine();
            md.AppendLine(heroes.Length + " heroes x " + levels.Count + " levels = " + all.Length + " runs; solved " + (all.Length - failed.Length) + ", failed " + failed.Length + ". Total time " + sw.Elapsed.TotalMinutes.ToString("F1", CultureInfo.InvariantCulture) + " min.");
            md.AppendLine();
            md.AppendLine("| region | runs | solved | failed |");
            md.AppendLine("|---|---|---|---|");
            foreach (var g in all.GroupBy(r => r.Region).OrderBy(g => g.Key)) md.AppendLine("| " + g.Key + " | " + g.Count() + " | " + g.Count(r => r.Solved) + " | " + g.Count(r => !r.Solved) + " |");
            md.AppendLine();
            md.AppendLine("| rarity | runs | solved | failed |");
            md.AppendLine("|---|---|---|---|");
            string[] names = { "Common", "Uncommon", "Rare", "Elite", "Legendary" };
            foreach (var g in all.GroupBy(r => (int)CharacterDefs.GetIncludingInactive(r.Hero).Rarity).OrderBy(g => g.Key)) md.AppendLine("| " + names[g.Key] + " | " + g.Count() + " | " + g.Count(r => r.Solved) + " | " + g.Count(r => !r.Solved) + " |");
            if (failed.Length > 0)
            {
                md.AppendLine();
                md.AppendLine("Failed runs (hero, level): " + string.Join("; ", failed.Take(60).Select(r => r.Hero + " " + r.Level)) + (failed.Length > 60 ? " ..." : ""));
            }
            File.WriteAllText(Path.Combine(root, "Tests", "Simulation", (string.IsNullOrEmpty(onlyLevels) ? "HERO_BOT_SUMMARY.md" : "HERO_BOT_RERUN_SUMMARY.md")), md.ToString());
            output.WriteLine(md.ToString());
            Assert.True(failed.Length == 0, failed.Length + " hero/level runs were not solved; see HERO_BOT_SUMMARY.md");
        }
    }
}
